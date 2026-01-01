using System;
using System.Collections.Generic;
using System.Drawing;

namespace Howl.Core.Models;

public enum StepTrigger
{
    Click,
    WindowChange,
    VisualChange,
    KeyboardCommit
}

public enum VisualChangeType
{
    PageNavigation,
    ModalOpen,
    ModalClose,
    StateChange,
    NewContent
}

public class StepCandidate
{
    public int Index { get; set; }
    public string WindowTitle { get; set; }
    public StepTrigger Trigger { get; set; }
    public Bitmap? Screenshot { get; set; }
    public string? ScreenshotPath { get; set; }
    public VisualChangeType ChangeType { get; set; }
    public DateTime Timestamp { get; set; }
    public List<KeystrokeEvent> Keystrokes { get; set; }
    public string? TextEntered { get; set; }

    public StepCandidate(int index, string windowTitle, StepTrigger trigger, DateTime timestamp)
    {
        Index = index;
        WindowTitle = windowTitle;
        Trigger = trigger;
        Timestamp = timestamp;
        Keystrokes = new List<KeystrokeEvent>();
    }
}
