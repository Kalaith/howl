using System;
using System.Collections.Generic;

namespace Howl.Core.Models;

public class RecordingSession
{
    public Guid Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<ClickEvent> Clicks { get; set; }
    public List<WindowEvent> WindowEvents { get; set; }
    public List<KeystrokeEvent> Keystrokes { get; set; }
    public List<StepCandidate> StepCandidates { get; set; }
    public string? VideoPath { get; set; }
    public string? OutputDirectory { get; set; }

    public RecordingSession()
    {
        Id = Guid.NewGuid();
        StartTime = DateTime.Now;
        Clicks = new List<ClickEvent>();
        WindowEvents = new List<WindowEvent>();
        Keystrokes = new List<KeystrokeEvent>();
        StepCandidates = new List<StepCandidate>();
    }

    public TimeSpan Duration => (EndTime ?? DateTime.Now) - StartTime;
}
