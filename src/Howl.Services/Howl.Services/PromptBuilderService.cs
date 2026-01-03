using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Howl.Core.Models;

namespace Howl.Services;

public class PromptBuilderService
{
    public string BuildSystemPrompt()
    {
        return @"You are Howl, a system that explains recorded computer actions as clear,
step-by-step instructions for another human to follow.

You will see a screenshot and metadata about what happened at that moment.
Use the visual information and keyboard/mouse data to describe the action.

Rules:
- Write ONE clear sentence describing the action taken.
- Do not mention timestamps or the recording.
- Do not mention ""the user"" - write as if instructing someone.
- Be specific about what was clicked or typed based on the screenshot.
- Prefer intent over mechanics (e.g., ""Save the file"" not ""Click the save button"").
- If you see text input in the metadata, mention what was typed.
- If you see a keyboard shortcut, mention it naturally (e.g., ""Press Ctrl+C to copy"").
- Keep it concise - one action per instruction.";
    }

    public string BuildContextPrompt(RecordingSession session, List<string> applications)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Task context:");
        sb.AppendLine("- Operating system: Windows");

        if (applications.Any())
        {
            sb.AppendLine("- Application(s) used:");
            foreach (var app in applications.Distinct())
            {
                sb.AppendLine($"  - \"{app}\"");
            }
        }

        sb.AppendLine($"- Approximate task duration: {FormatDuration(session.Duration)}");

        return sb.ToString();
    }

    public string BuildObservationPayload(List<StepCandidate> steps)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Observed actions (ordered, with corresponding screenshots):");
        sb.AppendLine();
        sb.AppendLine("Each StepCandidate below has a screenshot showing what was on screen.");
        sb.AppendLine("Use the visual information to understand what the user was doing.");
        sb.AppendLine();

        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            sb.AppendLine($"StepCandidate {i + 1} (see screenshot {i + 1}):");
            sb.AppendLine($"- Window title: \"{step.WindowTitle}\"");

            // Include keystroke information if available
            if (!string.IsNullOrEmpty(step.TextEntered))
            {
                sb.AppendLine($"- Text entered: \"{step.TextEntered}\"");
            }

            if (step.Keystrokes.Any())
            {
                var shortcuts = step.Keystrokes
                    .Where(k => k.CtrlPressed || k.AltPressed || k.IsModifier)
                    .Select(k => k.GetDisplayText())
                    .Distinct()
                    .ToList();

                if (shortcuts.Any())
                {
                    sb.AppendLine($"- Keyboard shortcuts: {string.Join(", ", shortcuts)}");
                }
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    public string BuildInstructionRequest()
    {
        return @"Generate a JSON object with the following structure:

{
  ""title"": ""A clear, action-oriented title for this guide"",
  ""summary"": ""A brief 1-2 sentence summary of what this guide accomplishes"",
  ""prerequisites"": [""Optional array of things needed before starting""],
  ""steps"": [
    {
      ""stepNumber"": 1,
      ""instruction"": ""Clear, concise instruction text""
    },
    {
      ""stepNumber"": 2,
      ""instruction"": ""Clear, concise instruction text""
    }
  ]
}

CRITICAL REQUIREMENTS:
- You MUST create ONE step for EVERY StepCandidate provided above
- If you see 6 StepCandidates, you MUST output 6 steps
- If you see 10 StepCandidates, you MUST output 10 steps
- NEVER skip or combine steps - each StepCandidate gets exactly one instruction
- Match the stepNumber to the StepCandidate number (StepCandidate 1 = step 1, etc.)

Rules for instructions:
- Write one sentence per step
- Describe what the user is trying to accomplish
- Refer implicitly to the screenshot
- Do not name UI elements unless necessary
- Do not include tips or warnings
- Focus on intent, not mechanics";
    }

    /// <summary>
    /// Builds a prompt for refining a list of instructions with full context to ensure consistency.
    /// </summary>
    /// <param name="steps">The step candidates containing context information.</param>
    /// <param name="initialInstructions">The initial instruction texts to be refined.</param>
    /// <returns>A formatted prompt string for instruction refinement.</returns>
    public string BuildRefinementPrompt(List<StepCandidate> steps, List<string> initialInstructions)
    {
        var promptText = new StringBuilder();
        promptText.AppendLine("Review and refine these step-by-step instructions for accuracy and consistency.");
        promptText.AppendLine();
        promptText.AppendLine("Current instructions:");
        for (int i = 0; i < initialInstructions.Count; i++)
        {
            promptText.AppendLine($"{i + 1}. {initialInstructions[i]}");
        }
        promptText.AppendLine();
        promptText.AppendLine("Context for each step:");
        for (int i = 0; i < steps.Count; i++)
        {
            promptText.AppendLine($"Step {i + 1}:");
            promptText.AppendLine($"  Window: \"{steps[i].WindowTitle}\"");
            if (!string.IsNullOrEmpty(steps[i].TextEntered))
            {
                promptText.AppendLine($"  Text entered: \"{steps[i].TextEntered}\"");
            }
            if (steps[i].Keystrokes.Any())
            {
                var shortcuts = steps[i].Keystrokes
                    .Where(k => k.CtrlPressed || k.AltPressed || k.IsModifier)
                    .Select(k => k.GetDisplayText())
                    .Distinct()
                    .ToList();
                if (shortcuts.Any())
                {
                    promptText.AppendLine($"  Keyboard shortcuts: {string.Join(", ", shortcuts)}");
                }
            }
        }
        promptText.AppendLine();
        promptText.AppendLine("Refine the instructions to:");
        promptText.AppendLine("- Ensure step 1 and step " + steps.Count + " make sense as the beginning and end");
        promptText.AppendLine("- Fix any contradictions (e.g., don't say 'started' and 'initiated' for different steps)");
        promptText.AppendLine("- Make descriptions specific and actionable");
        promptText.AppendLine("- Keep each instruction under 200 chars");
        promptText.AppendLine();
        promptText.AppendLine("Respond with a JSON object containing the refined instructions:");
        promptText.AppendLine("{");
        promptText.AppendLine("  \"instructions\": [");
        promptText.AppendLine("    \"Refined instruction for step 1\",");
        promptText.AppendLine("    \"Refined instruction for step 2\"");
        promptText.AppendLine("  ]");
        promptText.AppendLine("}");
        promptText.AppendLine();
        promptText.AppendLine("Respond with ONLY the JSON object, no markdown, no explanation.");

        return promptText.ToString();
    }

    private string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes < 1)
            return $"{(int)duration.TotalSeconds} seconds";
        else if (duration.TotalMinutes < 60)
            return $"{(int)duration.TotalMinutes} minutes";
        else
            return $"{(int)duration.TotalHours} hours";
    }
}
