using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Howl.Core.Models;
using Howl.Services.Configuration;
using Howl.Services.Exceptions;
using Howl.Services.Models;

namespace Howl.Services;

public class LMStudioService
{
    private readonly LMStudioConfiguration _config;
    private readonly HttpClient _httpClient;

    public LMStudioService(LMStudioConfiguration config, HttpClient httpClient)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    private string BuildEndpoint()
    {
        return $"{_config.BaseUrl}/v1/chat/completions";
    }

    public async Task<List<string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_config.BaseUrl}/v1/models", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[LM Studio] Failed to fetch models: {response.StatusCode}");
                return new List<string>();
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var result = JsonSerializer.Deserialize<ModelsResponse>(responseContent, options);

            if (result?.Data != null)
            {
                var modelIds = result.Data.Select(m => m.Id).Where(id => !string.IsNullOrEmpty(id)).ToList();
                Console.WriteLine($"[LM Studio] Found {modelIds.Count} models");
                return modelIds!;
            }

            return new List<string>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LM Studio] Error fetching models: {ex.Message}");
            return new List<string>();
        }
    }

    public async Task<GeminiResponse> GenerateInstructionsAsync(
        string systemPrompt,
        string contextPrompt,
        string observationPayload,
        string instructionRequest,
        List<StepCandidate>? stepCandidates = null,
        CancellationToken cancellationToken = default)
    {
        object payload;

        // Check if vision is enabled and we have screenshots
        if (_config.EnableVision && stepCandidates != null && stepCandidates.Any(s => !string.IsNullOrEmpty(s.ScreenshotPath)))
        {
            Console.WriteLine($"[LM Studio] Vision enabled - sending {stepCandidates.Count} screenshots");

            // Build vision-compatible message with images
            var userContent = new List<object>();

            // Add text content
            userContent.Add(new
            {
                type = "text",
                text = $@"{contextPrompt}

{observationPayload}

{instructionRequest}

IMPORTANT: Respond with valid JSON only, no additional text."
            });

            // Add screenshots
            foreach (var step in stepCandidates)
            {
                if (!string.IsNullOrEmpty(step.ScreenshotPath) && File.Exists(step.ScreenshotPath))
                {
                    var base64Image = EncodeImageToBase64(step.ScreenshotPath);
                    userContent.Add(new
                    {
                        type = "image_url",
                        image_url = new
                        {
                            url = $"data:image/png;base64,{base64Image}"
                        }
                    });
                }
            }

            var messages = new List<object>
            {
                new
                {
                    role = "system",
                    content = systemPrompt
                },
                new
                {
                    role = "user",
                    content = userContent.ToArray()
                }
            };

            payload = new
            {
                model = _config.Model,
                messages = messages,
                temperature = _config.Temperature,
                max_tokens = _config.MaxTokens
            };
        }
        else
        {
            Console.WriteLine($"[LM Studio] Vision disabled - sending text only");

            // Text-only format (original)
            payload = new
            {
                model = _config.Model,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = systemPrompt
                    },
                    new
                    {
                        role = "user",
                        content = $@"{contextPrompt}

{observationPayload}

{instructionRequest}

IMPORTANT: Respond with valid JSON only, no additional text."
                    }
                },
                temperature = _config.Temperature,
                max_tokens = _config.MaxTokens
            };
        }

        var request = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint())
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };

        try
        {
            Console.WriteLine($"[LM Studio] Sending request to: {BuildEndpoint()}");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            Console.WriteLine($"[LM Studio] Response status: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"[LM Studio] Error response: {errorContent}");
                throw new GeminiApiException(
                    $"LM Studio API returned {response.StatusCode}: {errorContent}",
                    (int)response.StatusCode);
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            Console.WriteLine($"[LM Studio] Raw response length: {responseContent.Length} characters");
            Console.WriteLine($"[LM Studio] Response preview: {responseContent.Substring(0, Math.Min(200, responseContent.Length))}...");

            // Try to deserialize OpenAI-compatible response
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var result = JsonSerializer.Deserialize<OpenAIResponse>(responseContent, options);

                Console.WriteLine($"[LM Studio] Deserialized. Choices count: {result?.Choices?.Length ?? 0}");

                if (result?.Choices != null && result.Choices.Length > 0)
                {
                    var content = result.Choices[0].Message?.Content;
                    Console.WriteLine($"[LM Studio] First choice content length: {content?.Length ?? 0}");
                    Console.WriteLine($"[LM Studio] Content preview: {content?.Substring(0, Math.Min(200, content.Length))}...");
                }

                return ParseOpenAIResponse(result);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[LM Studio] JSON parse error: {ex.Message}");
                throw new GeminiApiException(
                    $"Failed to parse LM Studio response. Response: {responseContent.Substring(0, Math.Min(500, responseContent.Length))}...",
                    ex);
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[LM Studio] HTTP error: {ex.Message}");
            throw new GeminiApiException($"Failed to communicate with LM Studio: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            Console.WriteLine($"[LM Studio] Timeout error: {ex.Message}");
            throw new GeminiApiException("Request to LM Studio timed out", ex);
        }
    }

    public async Task<string> GenerateSingleStepInstructionAsync(
        string systemPrompt,
        StepCandidate currentStep,
        StepCandidate? previousStep,
        int stepNumber,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[LM Studio] Generating instruction for step {stepNumber}");

        // Build prompt for this single step
        var promptText = new StringBuilder();
        promptText.AppendLine($"Step {stepNumber}:");
        promptText.AppendLine($"- Window: \"{currentStep.WindowTitle}\"");

        if (!string.IsNullOrEmpty(currentStep.TextEntered))
        {
            promptText.AppendLine($"- Text entered: \"{currentStep.TextEntered}\"");
        }

        if (currentStep.Keystrokes.Any())
        {
            var shortcuts = currentStep.Keystrokes
                .Where(k => k.CtrlPressed || k.AltPressed || k.IsModifier)
                .Select(k => k.GetDisplayText())
                .Distinct()
                .ToList();

            if (shortcuts.Any())
            {
                promptText.AppendLine($"- Keyboard shortcuts: {string.Join(", ", shortcuts)}");
            }
        }

        promptText.AppendLine();
        promptText.AppendLine("Based on the screenshot and actions above, write ONE clear instruction describing what happened.");
        promptText.AppendLine("Respond with ONLY the instruction text, no JSON, no formatting, just the instruction sentence.");

        var userContent = new List<object>();

        // Add text
        userContent.Add(new
        {
            type = "text",
            text = promptText.ToString()
        });

        // Add previous screenshot if enabled and available
        if (_config.SendPreviousScreenshot &&
            previousStep != null &&
            !string.IsNullOrEmpty(previousStep.ScreenshotPath) &&
            File.Exists(previousStep.ScreenshotPath))
        {
            var base64Image = EncodeImageToBase64(previousStep.ScreenshotPath);
            userContent.Add(new
            {
                type = "image_url",
                image_url = new
                {
                    url = $"data:image/jpeg;base64,{base64Image}"
                }
            });
        }

        // Add current screenshot
        if (!string.IsNullOrEmpty(currentStep.ScreenshotPath) && File.Exists(currentStep.ScreenshotPath))
        {
            var base64Image = EncodeImageToBase64(currentStep.ScreenshotPath);
            userContent.Add(new
            {
                type = "image_url",
                image_url = new
                {
                    url = $"data:image/jpeg;base64,{base64Image}"
                }
            });
        }

        var messages = new List<object>
        {
            new
            {
                role = "system",
                content = systemPrompt
            },
            new
            {
                role = "user",
                content = userContent.ToArray()
            }
        };

        var payload = new
        {
            model = _config.Model,
            messages = messages,
            temperature = _config.Temperature,
            max_tokens = 150 // Shorter since we only need one instruction
        };

        var request = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint())
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"[LM Studio] Error response: {errorContent}");
                throw new GeminiApiException(
                    $"LM Studio API returned {response.StatusCode}: {errorContent}",
                    (int)response.StatusCode);
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var result = JsonSerializer.Deserialize<OpenAIResponse>(responseContent, options);

            if (result?.Choices != null && result.Choices.Length > 0)
            {
                var instruction = result.Choices[0].Message?.Content?.Trim();
                Console.WriteLine($"[LM Studio] Step {stepNumber} instruction: {instruction}");
                return instruction ?? string.Empty;
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LM Studio] Error generating step {stepNumber}: {ex.Message}");
            throw;
        }
    }

    public async Task<GeminiResponse> GenerateWithRetryAsync(
        string systemPrompt,
        string contextPrompt,
        string observationPayload,
        string instructionRequest,
        List<StepCandidate>? stepCandidates = null,
        CancellationToken cancellationToken = default)
    {
        int attempt = 0;
        Exception? lastException = null;

        Console.WriteLine($"[LM Studio] Starting GenerateWithRetryAsync, max retries: {_config.MaxRetries}");

        while (attempt < _config.MaxRetries)
        {
            try
            {
                Console.WriteLine($"[LM Studio] Attempt {attempt + 1}/{_config.MaxRetries}");

                return await GenerateInstructionsAsync(
                    systemPrompt,
                    contextPrompt,
                    observationPayload,
                    instructionRequest,
                    stepCandidates,
                    cancellationToken);
            }
            catch (GeminiApiException ex) when (ex.StatusCode >= 500)
            {
                // Retry on server errors
                Console.WriteLine($"[LM Studio] Server error (5xx), will retry: {ex.Message}");
                lastException = ex;
                attempt++;
                if (attempt < _config.MaxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    Console.WriteLine($"[LM Studio] Waiting {delay.TotalSeconds}s before retry...");
                    await Task.Delay(delay, cancellationToken);
                }
            }
            catch (GeminiApiException ex) when (ex.StatusCode >= 400 && ex.StatusCode < 500)
            {
                // Client errors - don't retry, throw immediately
                Console.WriteLine($"[LM Studio] Client error (4xx), not retrying: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                // Network or other errors - retry
                Console.WriteLine($"[LM Studio] Error, will retry: {ex.GetType().Name}: {ex.Message}");
                lastException = ex;
                attempt++;
                if (attempt < _config.MaxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    Console.WriteLine($"[LM Studio] Waiting {delay.TotalSeconds}s before retry...");
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        // If we get here, all retries failed
        Console.WriteLine($"[LM Studio] All retries exhausted. Last error: {lastException?.Message}");
        throw new GeminiApiException(
            $"Failed after {_config.MaxRetries} attempts. Last error: {lastException?.Message}",
            lastException!);
    }

    private GeminiResponse ParseOpenAIResponse(OpenAIResponse? apiResponse)
    {
        Console.WriteLine($"[LM Studio] ParseOpenAIResponse called");
        Console.WriteLine($"[LM Studio] apiResponse is null: {apiResponse == null}");
        Console.WriteLine($"[LM Studio] Choices is null: {apiResponse?.Choices == null}");
        Console.WriteLine($"[LM Studio] Choices length: {apiResponse?.Choices?.Length ?? -1}");

        if (apiResponse?.Choices == null || apiResponse.Choices.Length == 0)
        {
            Console.WriteLine($"[LM Studio] ERROR: No choices in response");
            throw new GeminiApiException("No response returned from LM Studio");
        }

        var choice = apiResponse.Choices[0];
        Console.WriteLine($"[LM Studio] Choice.Message is null: {choice.Message == null}");

        var textContent = choice.Message?.Content;

        Console.WriteLine($"[LM Studio] Content is null or empty: {string.IsNullOrEmpty(textContent)}");
        Console.WriteLine($"[LM Studio] Content length: {textContent?.Length ?? 0}");

        if (string.IsNullOrEmpty(textContent))
        {
            Console.WriteLine($"[LM Studio] ERROR: Empty content");
            throw new GeminiApiException("Empty response from LM Studio");
        }

        // Extract JSON from response (in case there's extra text)
        var jsonContent = ExtractJson(textContent);

        Console.WriteLine($"[LM Studio] Extracted JSON length: {jsonContent.Length}");
        Console.WriteLine($"[LM Studio] JSON preview: {jsonContent.Substring(0, Math.Min(300, jsonContent.Length))}...");

        // Parse the JSON response
        try
        {
            var instructions = JsonSerializer.Deserialize<GeminiInstructionResponse>(jsonContent);

            if (instructions == null)
            {
                Console.WriteLine($"[LM Studio] ERROR: Deserialized instructions is null");
                throw new GeminiApiException("Failed to deserialize instruction response");
            }

            Console.WriteLine($"[LM Studio] Successfully parsed. Title: {instructions.Title}");
            Console.WriteLine($"[LM Studio] Steps count: {instructions.Steps?.Length ?? 0}");

            return new GeminiResponse
            {
                Instructions = instructions.Steps,
                Title = instructions.Title,
                Summary = instructions.Summary,
                Prerequisites = instructions.Prerequisites
            };
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[LM Studio] JSON parse error: {ex.Message}");
            Console.WriteLine($"[LM Studio] Failed JSON: {jsonContent}");
            throw new GeminiApiException(
                $"Failed to parse JSON response from LM Studio. Response: {textContent.Substring(0, Math.Min(500, textContent.Length))}...",
                ex);
        }
    }

    private string ExtractJson(string text)
    {
        // Try to find JSON object in the response
        var startIndex = text.IndexOf('{');
        var endIndex = text.LastIndexOf('}');

        if (startIndex >= 0 && endIndex > startIndex)
        {
            return text.Substring(startIndex, endIndex - startIndex + 1);
        }

        // If no JSON found, return original text
        return text;
    }

    private string EncodeImageToBase64(string imagePath)
    {
        try
        {
            using var originalImage = Image.FromFile(imagePath);

            // Calculate new dimensions maintaining aspect ratio
            int newWidth = originalImage.Width;
            int newHeight = originalImage.Height;

            if (originalImage.Width > _config.MaxImageWidth)
            {
                newWidth = _config.MaxImageWidth;
                newHeight = (int)((double)originalImage.Height / originalImage.Width * newWidth);
            }

            // Resize image
            using var resizedImage = new Bitmap(newWidth, newHeight);
            using (var graphics = Graphics.FromImage(resizedImage))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(originalImage, 0, 0, newWidth, newHeight);
            }

            // Convert to JPEG with compression
            using var memoryStream = new MemoryStream();
            var jpegEncoder = GetEncoder(ImageFormat.Jpeg);
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)_config.JpegQuality);

            resizedImage.Save(memoryStream, jpegEncoder, encoderParams);
            var imageBytes = memoryStream.ToArray();

            var originalSize = new FileInfo(imagePath).Length;
            var newSize = imageBytes.Length;
            var reduction = (1 - (double)newSize / originalSize) * 100;

            Console.WriteLine($"[LM Studio] Image resized: {originalImage.Width}x{originalImage.Height} -> {newWidth}x{newHeight}, " +
                             $"Size: {originalSize / 1024}KB -> {newSize / 1024}KB ({reduction:F1}% reduction)");

            return Convert.ToBase64String(imageBytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LM Studio] Failed to encode image {imagePath}: {ex.Message}");
            throw;
        }
    }

    private ImageCodecInfo GetEncoder(ImageFormat format)
    {
        var codecs = ImageCodecInfo.GetImageDecoders();
        foreach (var codec in codecs)
        {
            if (codec.FormatID == format.Guid)
            {
                return codec;
            }
        }
        return null!;
    }

    // OpenAI-compatible response models
    private class OpenAIResponse
    {
        public string? Id { get; set; }
        public string? Object { get; set; }
        public long? Created { get; set; }
        public string? Model { get; set; }
        public Choice[]? Choices { get; set; }
        public Usage? Usage { get; set; }
    }

    private class Choice
    {
        public int? Index { get; set; }
        public Message? Message { get; set; }
        public string? FinishReason { get; set; }
    }

    private class Message
    {
        public string? Role { get; set; }
        public string? Content { get; set; }
    }

    private class Usage
    {
        public int? PromptTokens { get; set; }
        public int? CompletionTokens { get; set; }
        public int? TotalTokens { get; set; }
    }

    private class ModelsResponse
    {
        public string? Object { get; set; }
        public ModelData[]? Data { get; set; }
    }

    private class ModelData
    {
        public string? Id { get; set; }
        public string? Object { get; set; }
        public long? Created { get; set; }
        public string? OwnedBy { get; set; }
    }
}
