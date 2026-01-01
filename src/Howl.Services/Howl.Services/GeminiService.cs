using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Howl.Services.Configuration;
using Howl.Services.Exceptions;
using Howl.Services.Models;

namespace Howl.Services;

public class GeminiService
{
    private readonly GeminiConfiguration _config;
    private readonly HttpClient _httpClient;

    public GeminiService(GeminiConfiguration config, HttpClient httpClient)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    private string BuildEndpoint()
    {
        return $"{_config.BaseUrl}/{_config.Model}:generateContent";
    }

    public async Task<GeminiResponse> GenerateInstructionsAsync(
        string systemPrompt,
        string contextPrompt,
        string observationPayload,
        string instructionRequest,
        CancellationToken cancellationToken = default)
    {
        // Combine all prompt parts
        string fullPrompt = $@"{systemPrompt}

{contextPrompt}

{observationPayload}

{instructionRequest}";

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = fullPrompt }
                    }
                }
            },
            generationConfig = new
            {
                response_mime_type = "application/json",
                temperature = _config.Temperature,
                topP = 0.8,
                topK = 40,
                maxOutputTokens = _config.MaxOutputTokens
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint())
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };

        // Add API key as header (official Gemini API authentication method)
        request.Headers.Add("x-goog-api-key", _config.ApiKey);

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

                // Try to parse error details from JSON response
                string errorMessage;
                try
                {
                    var errorJson = JsonSerializer.Deserialize<JsonElement>(errorContent);
                    if (errorJson.TryGetProperty("error", out var errorObj))
                    {
                        var message = errorObj.TryGetProperty("message", out var msgProp)
                            ? msgProp.GetString()
                            : "Unknown error";
                        var status = errorObj.TryGetProperty("status", out var statusProp)
                            ? statusProp.GetString()
                            : response.StatusCode.ToString();
                        errorMessage = $"Gemini API error ({status}): {message}";
                    }
                    else
                    {
                        errorMessage = $"Gemini API returned {response.StatusCode}: {errorContent}";
                    }
                }
                catch
                {
                    errorMessage = $"Gemini API returned {response.StatusCode}: {errorContent}";
                }

                throw new GeminiApiException(errorMessage, (int)response.StatusCode);
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            // Try to deserialize the response
            try
            {
                var result = JsonSerializer.Deserialize<GeminiApiResponse>(responseContent);
                return ParseGeminiResponse(result);
            }
            catch (JsonException ex)
            {
                throw new GeminiApiException(
                    $"Failed to parse Gemini API response. Response: {responseContent.Substring(0, Math.Min(500, responseContent.Length))}...",
                    ex);
            }
        }
        catch (HttpRequestException ex)
        {
            throw new GeminiApiException($"Failed to communicate with Gemini API: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new GeminiApiException("Request to Gemini API timed out", ex);
        }
    }

    public async Task<GeminiResponse> GenerateWithRetryAsync(
        string systemPrompt,
        string contextPrompt,
        string observationPayload,
        string instructionRequest,
        CancellationToken cancellationToken = default)
    {
        int attempt = 0;
        Exception? lastException = null;

        while (attempt < _config.MaxRetries)
        {
            try
            {
                return await GenerateInstructionsAsync(
                    systemPrompt,
                    contextPrompt,
                    observationPayload,
                    instructionRequest,
                    cancellationToken);
            }
            catch (GeminiApiException ex) when (ex.StatusCode >= 500)
            {
                // Retry on server errors
                lastException = ex;
                attempt++;
                if (attempt < _config.MaxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
                }
            }
            catch (GeminiApiException ex) when (ex.StatusCode == 429)
            {
                // Rate limit - wait longer
                lastException = ex;
                attempt++;
                if (attempt < _config.MaxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10 * attempt), cancellationToken);
                }
            }
            catch (GeminiApiException ex) when (ex.StatusCode >= 400 && ex.StatusCode < 500)
            {
                // Client errors - don't retry, throw immediately with details
                throw;
            }
            catch (Exception ex)
            {
                // Network or other errors - retry
                lastException = ex;
                attempt++;
                if (attempt < _config.MaxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
                }
            }
        }

        // If we get here, all retries failed
        throw new GeminiApiException(
            $"Failed after {_config.MaxRetries} attempts. Last error: {lastException?.Message}",
            lastException!);
    }

    private GeminiResponse ParseGeminiResponse(GeminiApiResponse? apiResponse)
    {
        if (apiResponse?.Candidates == null || apiResponse.Candidates.Length == 0)
        {
            throw new GeminiApiException("No candidates returned from Gemini API");
        }

        var candidate = apiResponse.Candidates[0];
        var textContent = candidate.Content?.Parts?[0]?.Text;

        if (string.IsNullOrEmpty(textContent))
        {
            throw new GeminiApiException("Empty response from Gemini API");
        }

        // Parse the JSON response from Gemini
        var instructions = JsonSerializer.Deserialize<GeminiInstructionResponse>(textContent);

        if (instructions == null)
        {
            throw new GeminiApiException("Failed to deserialize instruction response");
        }

        return new GeminiResponse
        {
            Instructions = instructions.Steps,
            Title = instructions.Title,
            Summary = instructions.Summary,
            Prerequisites = instructions.Prerequisites
        };
    }
}
