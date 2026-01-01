using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Howl.Core.Models;

namespace Howl.Services;

public class DebugExportService
{
    public string ExportPromptPreview(
        string systemPrompt,
        string contextPrompt,
        string observationPayload,
        string instructionRequest,
        RecordingSession session,
        List<StepCandidate> steps,
        string outputPath)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("                HOWL - DEBUG MODE EXPORT");
        sb.AppendLine("               Prompt Preview (Not sent to AI)");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Session ID: {session.Id}");
        sb.AppendLine($"Duration: {session.Duration.TotalSeconds:F1} seconds");
        sb.AppendLine($"Clicks Captured: {session.Clicks.Count}");
        sb.AppendLine($"Window Changes: {session.WindowEvents.Count}");
        sb.AppendLine($"Keystrokes Captured: {session.Keystrokes.Count}");
        sb.AppendLine($"Steps Detected: {steps.Count}");
        sb.AppendLine();

        // Recording Details
        sb.AppendLine("───────────────────────────────────────────────────────────────");
        sb.AppendLine("RECORDING DETAILS");
        sb.AppendLine("───────────────────────────────────────────────────────────────");
        sb.AppendLine();

        sb.AppendLine("Applications Used:");
        var apps = session.WindowEvents.Select(w => w.ProcessName).Distinct().ToList();
        foreach (var app in apps)
        {
            sb.AppendLine($"  - {app}");
        }
        sb.AppendLine();

        sb.AppendLine("Click Events:");
        for (int i = 0; i < Math.Min(10, session.Clicks.Count); i++)
        {
            var click = session.Clicks[i];
            sb.AppendLine($"  {i + 1}. [{click.Timestamp:HH:mm:ss.fff}] {click.Type} click at ({click.Position.X}, {click.Position.Y})");
        }
        if (session.Clicks.Count > 10)
        {
            sb.AppendLine($"  ... and {session.Clicks.Count - 10} more clicks");
        }
        sb.AppendLine();

        sb.AppendLine("Keystroke Events:");
        for (int i = 0; i < Math.Min(20, session.Keystrokes.Count); i++)
        {
            var keystroke = session.Keystrokes[i];
            var displayText = keystroke.GetDisplayText();
            sb.AppendLine($"  {i + 1}. [{keystroke.Timestamp:HH:mm:ss.fff}] {displayText} in \"{keystroke.WindowTitle}\"");
        }
        if (session.Keystrokes.Count > 20)
        {
            sb.AppendLine($"  ... and {session.Keystrokes.Count - 20} more keystrokes");
        }
        sb.AppendLine();

        sb.AppendLine("Detected Steps:");
        foreach (var step in steps)
        {
            sb.AppendLine($"  Step {step.Index}: {step.WindowTitle}");
            sb.AppendLine($"    Trigger: {step.Trigger}");
            sb.AppendLine($"    Change Type: {step.ChangeType}");
            sb.AppendLine($"    Time: {step.Timestamp:HH:mm:ss}");
            sb.AppendLine($"    Screenshot: {step.ScreenshotPath ?? "N/A"}");

            if (!string.IsNullOrEmpty(step.TextEntered))
            {
                sb.AppendLine($"    Text Entered: \"{step.TextEntered}\"");
            }

            if (step.Keystrokes.Any())
            {
                sb.AppendLine($"    Keystrokes: {step.Keystrokes.Count}");
                var shortcuts = step.Keystrokes
                    .Where(k => k.CtrlPressed || k.AltPressed)
                    .Select(k => k.GetDisplayText())
                    .Distinct()
                    .ToList();

                if (shortcuts.Any())
                {
                    sb.AppendLine($"    Shortcuts: {string.Join(", ", shortcuts)}");
                }
            }

            sb.AppendLine();
        }

        // Full Prompt that would be sent to Gemini
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("FULL PROMPT (What would be sent to Gemini API)");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();

        sb.AppendLine("┌─────────────────────────────────────────────────────────────┐");
        sb.AppendLine("│ SYSTEM PROMPT                                               │");
        sb.AppendLine("└─────────────────────────────────────────────────────────────┘");
        sb.AppendLine();
        sb.AppendLine(systemPrompt);
        sb.AppendLine();

        sb.AppendLine("┌─────────────────────────────────────────────────────────────┐");
        sb.AppendLine("│ CONTEXT PROMPT                                              │");
        sb.AppendLine("└─────────────────────────────────────────────────────────────┘");
        sb.AppendLine();
        sb.AppendLine(contextPrompt);
        sb.AppendLine();

        sb.AppendLine("┌─────────────────────────────────────────────────────────────┐");
        sb.AppendLine("│ OBSERVATION PAYLOAD                                         │");
        sb.AppendLine("└─────────────────────────────────────────────────────────────┘");
        sb.AppendLine();
        sb.AppendLine(observationPayload);
        sb.AppendLine();

        sb.AppendLine("┌─────────────────────────────────────────────────────────────┐");
        sb.AppendLine("│ INSTRUCTION REQUEST                                         │");
        sb.AppendLine("└─────────────────────────────────────────────────────────────┘");
        sb.AppendLine();
        sb.AppendLine(instructionRequest);
        sb.AppendLine();

        // Combined prompt
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("COMBINED PROMPT (Exact text sent to API)");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();

        var fullPrompt = $@"{systemPrompt}

{contextPrompt}

{observationPayload}

{instructionRequest}";

        sb.AppendLine(fullPrompt);
        sb.AppendLine();

        // API Details
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("API CONFIGURATION");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine("Endpoint: https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-exp:generateContent");
        sb.AppendLine("Method: POST");
        sb.AppendLine("Content-Type: application/json");
        sb.AppendLine();
        sb.AppendLine("Generation Config:");
        sb.AppendLine("  - response_mime_type: application/json");
        sb.AppendLine("  - temperature: 0.3");
        sb.AppendLine("  - topP: 0.8");
        sb.AppendLine("  - topK: 40");
        sb.AppendLine("  - maxOutputTokens: 2048");
        sb.AppendLine();

        // Expected Response Format
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("EXPECTED JSON RESPONSE FORMAT");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine(@"{
  ""title"": ""How to [Task Name]"",
  ""summary"": ""Brief description of what this guide accomplishes"",
  ""prerequisites"": [
    ""Optional prerequisite 1"",
    ""Optional prerequisite 2""
  ],
  ""steps"": [
    {
      ""stepNumber"": 1,
      ""instruction"": ""Clear, action-oriented instruction"",
      ""screenshotReference"": ""step_01.png""
    },
    {
      ""stepNumber"": 2,
      ""instruction"": ""Another clear instruction"",
      ""screenshotReference"": ""step_02.png""
    }
  ]
}");
        sb.AppendLine();

        // Token estimate
        var charCount = fullPrompt.Length;
        var estimatedTokens = charCount / 4; // Rough estimate: 1 token ≈ 4 chars
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("STATISTICS");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"Total Characters: {charCount:N0}");
        sb.AppendLine($"Estimated Tokens: ~{estimatedTokens:N0}");
        sb.AppendLine($"Screenshots: {steps.Count}");
        sb.AppendLine();

        // Footer
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("END OF DEBUG EXPORT");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine("To use this prompt with the Gemini API:");
        sb.AppendLine("1. Copy the 'COMBINED PROMPT' section above");
        sb.AppendLine("2. Send it to the Gemini API with the configuration shown");
        sb.AppendLine("3. The API will return JSON in the expected format");
        sb.AppendLine();
        sb.AppendLine("Screenshots referenced in the steps can be found in:");
        sb.AppendLine($"  {session.OutputDirectory}\\frames\\");
        sb.AppendLine();

        // Write to file
        File.WriteAllText(outputPath, sb.ToString());

        // Also copy screenshots to output directory
        var outputDir = Path.GetDirectoryName(outputPath)!;
        var debugScreenshotsDir = Path.Combine(outputDir, "screenshots");
        Directory.CreateDirectory(debugScreenshotsDir);

        foreach (var step in steps.Where(s => !string.IsNullOrEmpty(s.ScreenshotPath)))
        {
            var sourceFile = step.ScreenshotPath!;
            if (File.Exists(sourceFile))
            {
                var destFile = Path.Combine(debugScreenshotsDir, $"step_{step.Index:D2}.png");
                File.Copy(sourceFile, destFile, true);
            }
        }

        return outputPath;
    }
}
