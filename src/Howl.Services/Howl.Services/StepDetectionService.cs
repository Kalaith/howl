using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Howl.Core.Models;

namespace Howl.Services;

public class StepDetectionService
{
    private const int ClickMergeWindowMs = 300;
    private const int StepMergeWindowMs = 2000;
    private const double VisualChangeThreshold = 0.1; // 10% pixel difference
    private const double DuplicateThreshold = 0.95; // 95% similarity = duplicate

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

    private bool CheckWindowChangeAfterClick(List<WindowEvent> windowEvents, DateTime clickTime)
    {
        return windowEvents.Any(w =>
            w.Timestamp > clickTime &&
            (w.Timestamp - clickTime).TotalMilliseconds < 1000);
    }

    private List<StepCandidate> MergeAdjacentSteps(List<StepCandidate> candidates)
    {
        if (candidates.Count <= 1)
            return candidates;

        var merged = new List<StepCandidate>();
        merged.Add(candidates[0]);

        for (int i = 1; i < candidates.Count; i++)
        {
            var current = candidates[i];
            var previous = merged[merged.Count - 1];

            // Check if we should merge
            bool shouldMerge =
                current.WindowTitle == previous.WindowTitle &&
                (current.Timestamp - previous.Timestamp).TotalMilliseconds < StepMergeWindowMs;

            if (!shouldMerge)
            {
                merged.Add(current);
            }
            // If merging, we just skip the current one (keep the first)
        }

        // Renumber
        for (int i = 0; i < merged.Count; i++)
        {
            merged[i].Index = i + 1;
        }

        return merged;
    }

    private void AssociateScreenshots(List<StepCandidate> candidates, RecordingSession session)
    {
        if (string.IsNullOrEmpty(session.OutputDirectory))
            return;

        var framesDir = Path.Combine(session.OutputDirectory, "frames");
        if (!Directory.Exists(framesDir))
            return;

        var screenshots = Directory.GetFiles(framesDir, "*.png")
            .OrderBy(f => f)
            .ToList();

        // Simple association: match step index to screenshot index
        for (int i = 0; i < candidates.Count && i < screenshots.Count; i++)
        {
            candidates[i].ScreenshotPath = screenshots[i];
        }
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

    private int RemoveDuplicateScreenshots(RecordingSession session)
    {
        if (string.IsNullOrEmpty(session.OutputDirectory))
            return 0;

        var framesDir = Path.Combine(session.OutputDirectory, "frames");
        if (!Directory.Exists(framesDir))
            return 0;

        var screenshots = Directory.GetFiles(framesDir, "*.png")
            .OrderBy(f => f)
            .ToList();

        if (screenshots.Count <= 1)
            return 0;

        var toDelete = new List<string>();
        Bitmap? previousBitmap = null;

        try
        {
            for (int i = 0; i < screenshots.Count; i++)
            {
                try
                {
                    using var currentBitmap = new Bitmap(screenshots[i]);

                    if (previousBitmap != null)
                    {
                        // Compare with previous screenshot
                        double similarity = CalculateImageSimilarity(previousBitmap, currentBitmap);

                        if (similarity >= DuplicateThreshold)
                        {
                            // Mark current as duplicate
                            toDelete.Add(screenshots[i]);
                        }
                        else
                        {
                            // This is different, update previous
                            previousBitmap?.Dispose();
                            previousBitmap = new Bitmap(currentBitmap);
                        }
                    }
                    else
                    {
                        // First image, keep it
                        previousBitmap = new Bitmap(currentBitmap);
                    }
                }
                catch
                {
                    // Skip problematic images
                    continue;
                }
            }

            // Delete duplicates
            foreach (var file in toDelete)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore deletion errors
                }
            }
        }
        finally
        {
            previousBitmap?.Dispose();
        }

        return toDelete.Count;
    }

    private double CalculateImageSimilarity(Bitmap img1, Bitmap img2)
    {
        // Quick check: different sizes = different images
        if (img1.Width != img2.Width || img1.Height != img2.Height)
            return 0.0;

        // Sample pixels at regular intervals (every 10 pixels) for performance
        int sampleSize = 10;
        int matchingPixels = 0;
        int totalSamples = 0;

        for (int x = 0; x < img1.Width; x += sampleSize)
        {
            for (int y = 0; y < img1.Height; y += sampleSize)
            {
                try
                {
                    var pixel1 = img1.GetPixel(x, y);
                    var pixel2 = img2.GetPixel(x, y);

                    // Calculate color difference
                    int rDiff = Math.Abs(pixel1.R - pixel2.R);
                    int gDiff = Math.Abs(pixel1.G - pixel2.G);
                    int bDiff = Math.Abs(pixel1.B - pixel2.B);

                    // If colors are very similar (within 5 units), count as match
                    if (rDiff < 5 && gDiff < 5 && bDiff < 5)
                    {
                        matchingPixels++;
                    }

                    totalSamples++;
                }
                catch
                {
                    // Skip invalid coordinates
                    continue;
                }
            }
        }

        return totalSamples > 0 ? (double)matchingPixels / totalSamples : 0.0;
    }
}
