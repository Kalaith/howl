using System;

namespace Howl.Core.Models;

public enum KeyEventType
{
    KeyDown,
    KeyUp
}

public class KeystrokeEvent
{
    public int VirtualKeyCode { get; set; }
    public string Key { get; set; }
    public string? Text { get; set; }
    public KeyEventType EventType { get; set; }
    public DateTime Timestamp { get; set; }
    public string WindowTitle { get; set; }
    public bool IsModifier { get; set; }
    public bool CtrlPressed { get; set; }
    public bool AltPressed { get; set; }
    public bool ShiftPressed { get; set; }

    public KeystrokeEvent(int virtualKeyCode, string key, DateTime timestamp, string windowTitle)
    {
        VirtualKeyCode = virtualKeyCode;
        Key = key;
        Timestamp = timestamp;
        WindowTitle = windowTitle;
        EventType = KeyEventType.KeyDown;
    }

    public string GetDisplayText()
    {
        if (!string.IsNullOrEmpty(Text))
            return Text;

        // Special keys
        if (IsModifier || CtrlPressed || AltPressed || ShiftPressed)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (CtrlPressed) parts.Add("Ctrl");
            if (AltPressed) parts.Add("Alt");
            if (ShiftPressed) parts.Add("Shift");
            if (!IsModifier) parts.Add(Key);
            return string.Join("+", parts);
        }

        return Key;
    }
}
