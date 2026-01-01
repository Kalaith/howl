using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Howl.Core.Models;
using Howl.Services.Models;

namespace Howl.Services;

public class HowlOrchestrator
{
    private readonly ScreenRecordingService _recordingService;
    private readonly StepDetectionService _stepDetectionService;
    private readonly PromptBuilderService _promptBuilderService;
    private readonly HtmlExportService _htmlExportService;
    private readonly DebugExportService _debugExportService;

    private object _aiService; // Can be GeminiService or LMStudioService

    private RecordingSession? _currentSession;

    public HowlOrchestrator(
        ScreenRecordingService recordingService,
        StepDetectionService stepDetectionService,
        PromptBuilderService promptBuilderService,
        object aiService,
        HtmlExportService htmlExportService,
        DebugExportService debugExportService)
    {
        _recordingService = recordingService ?? throw new ArgumentNullException(nameof(recordingService));
        _stepDetectionService = stepDetectionService ?? throw new ArgumentNullException(nameof(stepDetectionService));
        _promptBuilderService = promptBuilderService ?? throw new ArgumentNullException(nameof(promptBuilderService));
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _htmlExportService = htmlExportService ?? throw new ArgumentNullException(nameof(htmlExportService));
        _debugExportService = debugExportService ?? throw new ArgumentNullException(nameof(debugExportService));
    }

    public void SetAIService(object aiService)
    {
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
    }

    public RecordingSession StartRecording()
    {
        _currentSession = _recordingService.StartRecording();
        return _currentSession;
    }

    public RecordingSession StopRecording()
    {
        if (_currentSession == null)
            throw new InvalidOperationException("No recording in progress");

        _currentSession = _recordingService.StopRecording();
        return _currentSession;
    }

    public async Task<string> ProcessAndExportAsync(string outputPath, Action<string>? progressCallback = null)
    {
        if (_currentSession == null)
            throw new InvalidOperationException("No session to process");

        try
        {
            // Step 1: Detect steps
            progressCallback?.Invoke("Detecting steps from recording...");
            var steps = _stepDetectionService.DetectSteps(_currentSession);
            _currentSession.StepCandidates = steps;

            Console.WriteLine($"[Orchestrator] Detected {steps.Count} steps");

            if (steps.Count == 0)
            {
                throw new InvalidOperationException("No steps detected in recording. Please record some actions.");
            }

            progressCallback?.Invoke($"Detected {steps.Count} unique steps");

            // Step 2: Build prompts
            progressCallback?.Invoke("Building AI prompts...");
            var systemPrompt = _promptBuilderService.BuildSystemPrompt();

            var applications = _currentSession.WindowEvents
                .Select(w => w.ProcessName)
                .Distinct()
                .ToList();

            var contextPrompt = _promptBuilderService.BuildContextPrompt(_currentSession, applications);
            var observationPayload = _promptBuilderService.BuildObservationPayload(steps);
            var instructionRequest = _promptBuilderService.BuildInstructionRequest();

            // Step 3: Call AI API - process each step individually to avoid context overflow
            progressCallback?.Invoke("Generating instructions with AI...");
            Console.WriteLine($"[Orchestrator] Processing {steps.Count} steps individually");

            var instructions = new List<InstructionStep>();

            for (int i = 0; i < steps.Count; i++)
            {
                progressCallback?.Invoke($"Generating instruction {i + 1}/{steps.Count}...");

                var currentStep = steps[i];
                var previousStep = i > 0 ? steps[i - 1] : null;

                var instructionText = await GenerateSingleStepAsync(
                    systemPrompt,
                    currentStep,
                    previousStep,
                    i + 1
                );

                var instruction = new InstructionStep
                {
                    StepNumber = i + 1,
                    Instruction = instructionText
                };

                // Use actual screenshot filename
                if (!string.IsNullOrEmpty(currentStep.ScreenshotPath))
                {
                    var filename = Path.GetFileName(currentStep.ScreenshotPath);
                    instruction.ScreenshotReference = filename;
                    Console.WriteLine($"[Orchestrator] Step {i + 1}: {filename} - {instructionText}");
                }

                instructions.Add(instruction);
            }

            // Generate title and summary based on all instructions
            var response = new GeminiResponse
            {
                Title = GenerateTitle(applications),
                Summary = $"A {steps.Count}-step guide",
                Instructions = instructions.ToArray()
            };

            Console.WriteLine($"[Orchestrator] Generated {instructions.Count} instructions");

            // Step 4: Export to HTML
            progressCallback?.Invoke("Exporting to HTML...");
            var htmlPath = _htmlExportService.ExportToHtml(response, _currentSession, outputPath);

            progressCallback?.Invoke("Done!");
            return htmlPath;
        }
        catch (Exception ex)
        {
            progressCallback?.Invoke($"Error: {ex.Message}");
            throw;
        }
    }

    public async Task<string> ProcessAndExportToZipAsync(string zipPath, Action<string>? progressCallback = null)
    {
        if (_currentSession == null)
            throw new InvalidOperationException("No session to process");

        try
        {
            // Step 1: Detect steps
            progressCallback?.Invoke("Detecting steps from recording...");
            var steps = _stepDetectionService.DetectSteps(_currentSession);
            _currentSession.StepCandidates = steps;

            Console.WriteLine($"[Orchestrator] Detected {steps.Count} steps");

            if (steps.Count == 0)
            {
                throw new InvalidOperationException("No steps detected in recording. Please record some actions.");
            }

            progressCallback?.Invoke($"Detected {steps.Count} unique steps");

            // Step 2: Build prompts
            progressCallback?.Invoke("Building AI prompts...");
            var systemPrompt = _promptBuilderService.BuildSystemPrompt();

            var applications = _currentSession.WindowEvents
                .Select(w => w.ProcessName)
                .Distinct()
                .ToList();

            var contextPrompt = _promptBuilderService.BuildContextPrompt(_currentSession, applications);
            var observationPayload = _promptBuilderService.BuildObservationPayload(steps);
            var instructionRequest = _promptBuilderService.BuildInstructionRequest();

            // Step 3: Call AI API - process each step individually to avoid context overflow
            progressCallback?.Invoke("Generating instructions with AI...");
            Console.WriteLine($"[Orchestrator] Processing {steps.Count} steps individually");

            var instructions = new List<InstructionStep>();

            for (int i = 0; i < steps.Count; i++)
            {
                progressCallback?.Invoke($"Generating instruction {i + 1}/{steps.Count}...");

                var currentStep = steps[i];
                var previousStep = i > 0 ? steps[i - 1] : null;

                var instructionText = await GenerateSingleStepAsync(
                    systemPrompt,
                    currentStep,
                    previousStep,
                    i + 1
                );

                var instruction = new InstructionStep
                {
                    StepNumber = i + 1,
                    Instruction = instructionText
                };

                // Use actual screenshot filename
                if (!string.IsNullOrEmpty(currentStep.ScreenshotPath))
                {
                    var filename = Path.GetFileName(currentStep.ScreenshotPath);
                    instruction.ScreenshotReference = filename;
                    Console.WriteLine($"[Orchestrator] Step {i + 1}: {filename} - {instructionText}");
                }

                instructions.Add(instruction);
            }

            // Generate title and summary based on all instructions
            var response = new GeminiResponse
            {
                Title = GenerateTitle(applications),
                Summary = $"A {steps.Count}-step guide",
                Instructions = instructions.ToArray()
            };

            Console.WriteLine($"[Orchestrator] Generated {instructions.Count} instructions");

            // Step 4: Export to ZIP
            progressCallback?.Invoke("Exporting to ZIP...");
            var resultPath = _htmlExportService.ExportToZip(response, _currentSession, zipPath);

            progressCallback?.Invoke("Done!");
            return resultPath;
        }
        catch (Exception ex)
        {
            progressCallback?.Invoke($"Error: {ex.Message}");
            throw;
        }
    }

    private async Task<string> GenerateSingleStepAsync(
        string systemPrompt,
        StepCandidate currentStep,
        StepCandidate? previousStep,
        int stepNumber)
    {
        if (_aiService is LMStudioService lmStudioService)
        {
            return await lmStudioService.GenerateSingleStepInstructionAsync(
                systemPrompt,
                currentStep,
                previousStep,
                stepNumber);
        }
        else if (_aiService is GeminiService geminiService)
        {
            // Gemini doesn't have single-step method yet, return fallback
            return $"Step {stepNumber} in {currentStep.WindowTitle}";
        }
        else
        {
            throw new InvalidOperationException("Unknown AI service type");
        }
    }

    private string GenerateTitle(List<string> applications)
    {
        if (applications.Any())
        {
            var mainApp = applications.First();
            return $"How to use {mainApp}";
        }
        return "Computer Task Guide";
    }

    private async Task<GeminiResponse> GenerateWithAIAsync(
        string systemPrompt,
        string contextPrompt,
        string observationPayload,
        string instructionRequest,
        List<StepCandidate>? stepCandidates = null)
    {
        if (_aiService is GeminiService geminiService)
        {
            return await geminiService.GenerateWithRetryAsync(
                systemPrompt,
                contextPrompt,
                observationPayload,
                instructionRequest);
        }
        else if (_aiService is LMStudioService lmStudioService)
        {
            return await lmStudioService.GenerateWithRetryAsync(
                systemPrompt,
                contextPrompt,
                observationPayload,
                instructionRequest,
                stepCandidates);
        }
        else
        {
            throw new InvalidOperationException("Unknown AI service type");
        }
    }

    public Task<string> ProcessAndExportDebugAsync(string outputPath, Action<string>? progressCallback = null)
    {
        if (_currentSession == null)
            throw new InvalidOperationException("No session to process");

        try
        {
            // Step 1: Detect steps
            progressCallback?.Invoke("Detecting steps from recording...");
            var steps = _stepDetectionService.DetectSteps(_currentSession);
            _currentSession.StepCandidates = steps;

            Console.WriteLine($"[Orchestrator] Detected {steps.Count} steps");

            if (steps.Count == 0)
            {
                throw new InvalidOperationException("No steps detected in recording. Please record some actions.");
            }

            progressCallback?.Invoke($"Detected {steps.Count} unique steps");

            // Step 2: Build prompts
            progressCallback?.Invoke("Building AI prompts...");
            var systemPrompt = _promptBuilderService.BuildSystemPrompt();

            var applications = _currentSession.WindowEvents
                .Select(w => w.ProcessName)
                .Distinct()
                .ToList();

            var contextPrompt = _promptBuilderService.BuildContextPrompt(_currentSession, applications);
            var observationPayload = _promptBuilderService.BuildObservationPayload(steps);
            var instructionRequest = _promptBuilderService.BuildInstructionRequest();

            // Step 3: Export debug information (SKIP AI CALL)
            progressCallback?.Invoke("Exporting debug information (skipping AI)...");
            var debugPath = _debugExportService.ExportPromptPreview(
                systemPrompt,
                contextPrompt,
                observationPayload,
                instructionRequest,
                _currentSession,
                steps,
                outputPath
            );

            progressCallback?.Invoke("Done! Debug export created.");
            return Task.FromResult(debugPath);
        }
        catch (Exception ex)
        {
            progressCallback?.Invoke($"Error: {ex.Message}");
            throw;
        }
    }
}
