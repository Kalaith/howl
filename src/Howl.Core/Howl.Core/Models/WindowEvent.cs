using System;

namespace Howl.Core.Models;

public class WindowEvent
{
    public string WindowTitle { get; set; }
    public string ProcessName { get; set; }
    public DateTime Timestamp { get; set; }

    public WindowEvent(string windowTitle, string processName, DateTime timestamp)
    {
        WindowTitle = windowTitle;
        ProcessName = processName;
        Timestamp = timestamp;
    }
}
