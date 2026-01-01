using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Howl.Core.Models;

namespace Howl.Services;

public class StepDetectionService
{

    public List<StepCandidate> DetectSteps(RecordingSession session)
    {
        Console.WriteLine($"[StepDetection] Starting step detection");
        Console.WriteLine($"[StepDetection] Clicks: {session.Clicks.Count}, WindowEvents: {session.WindowEvents.Count}, Keystrokes: {session.Keystrokes.Count}");

        // Get all screenshots (NO duplicate removal)
        var framesDir = Path.Combine(session.OutputDirectory!, "frames");
        var screenshots = Directory.Exists(framesDir)
            ? Directory.GetFiles(framesDir, "*.png").OrderBy(f => f).ToList()
            : new List<string>();

        Console.WriteLine($"[StepDetection] Total screenshots: {screenshots.Count}");

        // Create one step per screenshot (time-based)
        var candidates = new List<StepCandidate>();

        for (int i = 0; i < screenshots.Count; i++)
        {
            var screenshotFile = screenshots[i];
            var fileInfo = new FileInfo(screenshotFile);
            var timestamp = fileInfo.CreationTime;

            // Find window title at time of screenshot
            var windowTitle = FindWindowTitleAtTime(session.WindowEvents, timestamp);

            var candidate = new StepCandidate(i + 1, windowTitle, StepTrigger.VisualChange, timestamp)
            {
                ChangeType = VisualChangeType.NewContent,
                ScreenshotPath = screenshotFile
            };

            candidates.Add(candidate);
        }

        Console.WriteLine($"[StepDetection] Created {candidates.Count} step candidates");

        // Associate keystrokes with steps
        AssociateKeystrokes(candidates, session);

        // Log final step count
        Console.WriteLine($"[StepDetection] Final step count: {candidates.Count}");
        foreach (var step in candidates)
        {
            Console.WriteLine($"[StepDetection] Step {step.Index}: {step.WindowTitle}, Keys: {step.Keystrokes.Count}, Text: {step.TextEntered}");
        }

        return candidates;
    }

    private string FindWindowTitleAtTime(List<WindowEvent> windowEvents, DateTime timestamp)
    {
        var relevantEvent = windowEvents
            .Where(w => w.Timestamp <= timestamp)
            .OrderByDescending(w => w.Timestamp)
            .FirstOrDefault();

        return relevantEvent?.WindowTitle ?? "Unknown";
    }

    private void AssociateKeystrokes(List<StepCandidate> candidates, RecordingSession session)
    {
        if (candidates.Count == 0 || session.Keystrokes.Count == 0)
            return;

        for (int i = 0; i < candidates.Count; i++)
        {
            var step = candidates[i];
            var stepStart = i == 0 ? session.StartTime : candidates[i - 1].Timestamp;
            var stepEnd = step.Timestamp.AddSeconds(2); // Include keystrokes within 2 seconds after step

            // Find keystrokes that belong to this step
            var stepKeystrokes = session.Keystrokes
                .Where(k => k.Timestamp >= stepStart && k.Timestamp <= stepEnd &&
                           k.WindowTitle == step.WindowTitle)
                .ToList();

            step.Keystrokes.AddRange(stepKeystrokes);

            // Build text representation of entered text
            if (stepKeystrokes.Any())
            {
                var textChars = stepKeystrokes
                    .Where(k => !string.IsNullOrEmpty(k.Text) && !k.IsModifier)
                    .Select(k => k.Text)
                    .ToList();

                if (textChars.Any())
                {
                    step.TextEntered = string.Join("", textChars);
                }
            }
        }
    }
}

