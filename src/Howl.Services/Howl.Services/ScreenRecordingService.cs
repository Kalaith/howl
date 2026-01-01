using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Howl.Core.Models;

namespace Howl.Services;

public class ScreenRecordingService : IDisposable
{
    private RecordingSession? _currentSession;
    private bool _isRecording;
    private string _tempDirectory;
    private IntPtr _mouseHookID = IntPtr.Zero;
    private IntPtr _keyboardHookID = IntPtr.Zero;
    private LowLevelMouseProc? _mouseProc;
    private LowLevelKeyboardProc? _keyboardProc;
    private System.Threading.Timer? _windowCheckTimer;
    private System.Threading.Timer? _screenshotTimer;
    private string _lastWindowTitle = string.Empty;
    private int _screenshotCounter = 0;

    // Windows API constants
    private const int WH_MOUSE_LL = 14;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    public ScreenRecordingService()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "Howl");
        Directory.CreateDirectory(_tempDirectory);
    }

    public RecordingSession StartRecording()
    {
        if (_isRecording)
            throw new InvalidOperationException("Recording is already in progress");

        _currentSession = new RecordingSession();
        _currentSession.OutputDirectory = Path.Combine(_tempDirectory, $"session_{_currentSession.Id}");
        Directory.CreateDirectory(_currentSession.OutputDirectory);
        Directory.CreateDirectory(Path.Combine(_currentSession.OutputDirectory, "frames"));

        _isRecording = true;

        // Set up mouse hook
        _mouseProc = MouseHookCallback;
        using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
        using (var curModule = curProcess.MainModule)
        {
            if (curModule != null)
            {
                _mouseHookID = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        // Set up keyboard hook
        _keyboardProc = KeyboardHookCallback;
        using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
        using (var curModule = curProcess.MainModule)
        {
            if (curModule != null)
            {
                _keyboardHookID = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        // Set up window title monitoring
        _windowCheckTimer = new System.Threading.Timer(CheckWindowTitle, null, 0, 500);

        // Set up continuous screenshot capture (every 4 seconds)
        _screenshotTimer = new System.Threading.Timer(CaptureTimedScreenshot, null, 0, 4000);

        return _currentSession;
    }

    public RecordingSession StopRecording()
    {
        if (!_isRecording || _currentSession == null)
            throw new InvalidOperationException("No recording in progress");

        _isRecording = false;
        _currentSession.EndTime = DateTime.Now;

        // Unhook mouse
        if (_mouseHookID != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHookID);
            _mouseHookID = IntPtr.Zero;
        }

        // Unhook keyboard
        if (_keyboardHookID != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHookID);
            _keyboardHookID = IntPtr.Zero;
        }

        // Stop window monitoring
        _windowCheckTimer?.Dispose();
        _windowCheckTimer = null;

        // Stop screenshot timer
        _screenshotTimer?.Dispose();
        _screenshotTimer = null;

        return _currentSession;
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _isRecording && _currentSession != null)
        {
            if (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONDOWN)
            {
                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                var clickType = wParam == (IntPtr)WM_LBUTTONDOWN ? ClickType.Left : ClickType.Right;
                var clickEvent = new ClickEvent(
                    new Point(hookStruct.pt.x, hookStruct.pt.y),
                    clickType,
                    DateTime.Now
                );

                _currentSession.Clicks.Add(clickEvent);
            }
        }

        return CallNextHookEx(_mouseHookID, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _isRecording && _currentSession != null)
        {
            if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
            {
                KBDLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                int vkCode = hookStruct.vkCode;

                // Get current window title
                IntPtr hwnd = GetForegroundWindow();
                var sb = new System.Text.StringBuilder(256);
                GetWindowText(hwnd, sb, 256);
                string windowTitle = sb.ToString();

                // Get key name
                string keyName = GetKeyName(vkCode);

                // Check modifier states
                bool ctrlPressed = (GetKeyState(0x11) & 0x8000) != 0; // VK_CONTROL
                bool altPressed = (GetKeyState(0x12) & 0x8000) != 0;  // VK_MENU
                bool shiftPressed = (GetKeyState(0x10) & 0x8000) != 0; // VK_SHIFT

                var keystrokeEvent = new KeystrokeEvent(vkCode, keyName, DateTime.Now, windowTitle)
                {
                    CtrlPressed = ctrlPressed,
                    AltPressed = altPressed,
                    ShiftPressed = shiftPressed,
                    IsModifier = IsModifierKey(vkCode)
                };

                // Try to get printable character
                if (IsPrintableKey(vkCode) && !ctrlPressed && !altPressed)
                {
                    keystrokeEvent.Text = GetPrintableChar(vkCode, shiftPressed);
                }

                _currentSession.Keystrokes.Add(keystrokeEvent);
            }
        }

        return CallNextHookEx(_keyboardHookID, nCode, wParam, lParam);
    }

    private bool IsModifierKey(int vkCode)
    {
        return vkCode == 0x10 || vkCode == 0x11 || vkCode == 0x12 || // Shift, Ctrl, Alt
               vkCode == 0xA0 || vkCode == 0xA1 || // Left/Right Shift
               vkCode == 0xA2 || vkCode == 0xA3 || // Left/Right Ctrl
               vkCode == 0xA4 || vkCode == 0xA5;   // Left/Right Alt
    }

    private bool IsPrintableKey(int vkCode)
    {
        return (vkCode >= 0x30 && vkCode <= 0x39) || // 0-9
               (vkCode >= 0x41 && vkCode <= 0x5A) || // A-Z
               (vkCode >= 0x60 && vkCode <= 0x69) || // Numpad 0-9
               vkCode == 0x20; // Space
    }

    private string? GetPrintableChar(int vkCode, bool shiftPressed)
    {
        // Letters
        if (vkCode >= 0x41 && vkCode <= 0x5A)
        {
            char c = (char)vkCode;
            return shiftPressed ? c.ToString() : c.ToString().ToLower();
        }

        // Numbers
        if (vkCode >= 0x30 && vkCode <= 0x39)
        {
            if (shiftPressed)
            {
                string[] shiftedNumbers = { ")", "!", "@", "#", "$", "%", "^", "&", "*", "(" };
                return shiftedNumbers[vkCode - 0x30];
            }
            return ((char)vkCode).ToString();
        }

        // Space
        if (vkCode == 0x20)
            return " ";

        // Numpad
        if (vkCode >= 0x60 && vkCode <= 0x69)
            return (vkCode - 0x60).ToString();

        return null;
    }

    private string GetKeyName(int vkCode)
    {
        return vkCode switch
        {
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x10 => "Shift",
            0x11 => "Ctrl",
            0x12 => "Alt",
            0x1B => "Esc",
            0x20 => "Space",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0x2D => "Insert",
            0x2E => "Delete",
            0x70 => "F1",
            0x71 => "F2",
            0x72 => "F3",
            0x73 => "F4",
            0x74 => "F5",
            0x75 => "F6",
            0x76 => "F7",
            0x77 => "F8",
            0x78 => "F9",
            0x79 => "F10",
            0x7A => "F11",
            0x7B => "F12",
            >= 0x30 and <= 0x39 => ((char)vkCode).ToString(), // 0-9
            >= 0x41 and <= 0x5A => ((char)vkCode).ToString(), // A-Z
            _ => $"Key{vkCode}"
        };
    }

    private void CheckWindowTitle(object? state)
    {
        if (!_isRecording || _currentSession == null)
            return;

        try
        {
            IntPtr hwnd = GetForegroundWindow();
            var sb = new System.Text.StringBuilder(256);
            GetWindowText(hwnd, sb, 256);
            string title = sb.ToString();

            if (title != _lastWindowTitle && !string.IsNullOrWhiteSpace(title))
            {
                GetWindowThreadProcessId(hwnd, out uint processId);
                var process = System.Diagnostics.Process.GetProcessById((int)processId);

                var windowEvent = new WindowEvent(title, process.ProcessName, DateTime.Now);
                _currentSession.WindowEvents.Add(windowEvent);
                _lastWindowTitle = title;
            }
        }
        catch
        {
            // Ignore errors in window title checking
        }
    }

    private void CaptureTimedScreenshot(object? state)
    {
        if (!_isRecording || _currentSession == null)
            return;

        try
        {
            var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
            if (primaryScreen == null)
                return;

            var bounds = primaryScreen.Bounds;
            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
            }

            string filename = $"frame_{_screenshotCounter:D4}.png";
            string filepath = Path.Combine(_currentSession.OutputDirectory!, "frames", filename);
            bitmap.Save(filepath, ImageFormat.Png);

            _screenshotCounter++;
        }
        catch
        {
            // Ignore screenshot errors
        }
    }

    public void Dispose()
    {
        if (_mouseHookID != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHookID);
        }
        if (_keyboardHookID != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHookID);
        }
        _windowCheckTimer?.Dispose();
        _screenshotTimer?.Dispose();
    }
}
