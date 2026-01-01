using System;
using System.Drawing;

namespace Howl.Core.Models;

public enum ClickType
{
    Left,
    Right
}

public class ClickEvent
{
    public Point Position { get; set; }
    public ClickType Type { get; set; }
    public DateTime Timestamp { get; set; }

    public ClickEvent(Point position, ClickType type, DateTime timestamp)
    {
        Position = position;
        Type = type;
        Timestamp = timestamp;
    }
}
