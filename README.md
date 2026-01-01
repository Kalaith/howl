# Howl

**Record once. Let Howl explain it.**

Howl is a desktop application that automatically converts recorded screen activity into clean, step-by-step HTML guides with screenshots and natural language instructions.

## Features

- **Automatic Screen Recording**: Captures clicks and window changes automatically
- **AI-Powered Instructions**: Uses Google Gemini API to generate natural language steps
- **Smart Step Detection**: Applies heuristics to identify meaningful actions and merge redundant steps
- **Beautiful HTML Output**: Generates professional, responsive HTML guides
- **Export Options**: Save as HTML or ZIP file

## Prerequisites

- Windows 10 or later
- .NET 9.0 SDK
- Google Gemini API key (optional - only needed for AI mode, not for Debug Mode)

## Setup

### 1. Build the Application

```bash
cd howl
dotnet build
```

### 2. Run the Application

```bash
.\run-howl.bat
```

Or run directly:
```bash
cd src/Howl.Desktop/Howl.Desktop
dotnet run
```

### 3. Configure API Key (Two Options)

**Option A: Enter in the App** (Recommended for quick start)
1. Launch Howl
2. If no API key is detected, you'll see an API key input field
3. Paste your Gemini API key
4. Click "Save"
5. Start recording!

**Note:** API key is stored only for the current session.

**Option B: Set Environment Variable** (Recommended for permanent setup)

Set the `GEMINI_API_KEY` environment variable:

**PowerShell:**
```powershell
[System.Environment]::SetEnvironmentVariable('GEMINI_API_KEY', 'your-api-key-here', 'User')
```

**Command Prompt:**
```cmd
setx GEMINI_API_KEY "your-api-key-here"
```

**Important:** Restart your terminal/IDE after setting the environment variable.

### 4. Get a Gemini API Key

1. Go to [Google AI Studio](https://aistudio.google.com/app/apikey)
2. Create or select a project
3. Generate an API key
4. Copy the API key
5. Use Option A or B above to configure it

**Don't have an API key yet?** Use **Debug Mode** to test Howl without one!

## How to Use

1. **Launch Howl**: Open the application
2. **Start Recording**: Click the "Start Recording" button
3. **Perform Your Task**: Click through the actions you want to document
4. **Stop Recording**: Click the "Stop Recording" button
5. **Save**: Choose where to save your HTML guide
6. **Wait for Processing**: Howl will:
   - Detect meaningful steps from your clicks
   - Send screenshots to Gemini API
   - Generate natural language instructions
   - Create a beautiful HTML guide
7. **View Your Guide**: Open the generated HTML file in your browser

## Debug Mode (Skip AI)

Howl includes a **Debug Mode** that lets you test the recording and step detection without calling the Gemini API. This is useful for:

- **Testing**: See what would be sent to the AI without API costs
- **Debugging**: Inspect the exact prompt structure
- **Offline Work**: Use Howl without internet connection
- **Development**: Fine-tune your recordings before processing

### How to Use Debug Mode

1. **Enable Debug Mode**: Check the "Debug Mode (Skip AI - Export prompt only)" checkbox
2. **Record Normally**: Click Start/Stop as usual
3. **Export Debug File**: Save as a `.txt` file instead of HTML
4. **Review Output**: The file contains:
   - Recording statistics (clicks, duration, apps used)
   - All detected steps with details
   - Complete prompt that would be sent to Gemini
   - Expected JSON response format
   - Token estimates

### Debug Output Example

```
═══════════════════════════════════════════════════════════════
                HOWL - DEBUG MODE EXPORT
               Prompt Preview (Not sent to AI)
═══════════════════════════════════════════════════════════════

Session ID: abc123...
Duration: 45.2 seconds
Clicks Captured: 5
Steps Detected: 3

[Full recording details, prompts, and configuration...]
```

The screenshots are also copied to a `screenshots/` folder next to the debug file.

## Tips for Best Results

- **Be Deliberate**: Make purposeful clicks with brief pauses between actions
- **Avoid Rapid Clicking**: Multiple clicks within 300ms will be merged
- **Clear Intent**: Each click should represent a meaningful action
- **Window Titles**: Descriptive window titles help the AI understand context
- **Recording Length**: Keep recordings under 5 minutes for best results

## Project Structure

```
howl/
├── src/
│   ├── Howl.Core/          # Domain models
│   ├── Howl.Services/      # Business logic & services
│   └── Howl.Desktop/       # WPF application
├── design.md               # Design document
├── scope.md                # Original scope document
└── README.md               # This file
```

## Architecture

Howl follows a clean architecture pattern:

- **Howl.Core**: Domain models (ClickEvent, WindowEvent, StepCandidate, RecordingSession)
- **Howl.Services**:
  - `ScreenRecordingService`: Captures screen activity using Windows hooks
  - `StepDetectionService`: Applies heuristics to detect meaningful steps
  - `GeminiService`: Communicates with Google Gemini API
  - `PromptBuilderService`: Constructs prompts for the AI
  - `HtmlExportService`: Generates HTML output
  - `HowlOrchestrator`: Coordinates all services
- **Howl.Desktop**: WPF user interface

## How It Works

### 1. Recording
- Global mouse hook captures all clicks
- Window title changes are monitored
- Screenshots are taken after each click (with 300ms delay for UI updates)

### 2. Step Detection
The system applies these heuristics:
- **Suppress** multiple clicks within 300ms
- **Detect** state changes (window title changes, UI updates)
- **Merge** adjacent steps in the same window within 2 seconds
- **Associate** screenshots with detected steps

### 3. AI Processing
Prompts are structured into layers:
- **System Prompt**: Defines Howl's behavior and rules
- **Context Prompt**: Describes the recording environment
- **Observation Payload**: Structured data about detected steps
- **Instruction Request**: Specifies the desired JSON output format

### 4. Export
- Generates responsive HTML with embedded CSS
- Copies screenshots to output directory
- Optional ZIP export for easy sharing

## Example Output

The generated HTML includes:
- Clear, action-oriented title
- Brief summary
- Prerequisites (if applicable)
- Numbered steps with screenshots
- Professional styling

## Configuration

Edit `Howl.Services/Configuration/GeminiConfiguration.cs` to adjust:
- **Model**: Default is `gemini-2.0-flash-exp`
- **Temperature**: Default is 0.3 (lower = more consistent)
- **MaxRetries**: Default is 3
- **TimeoutSeconds**: Default is 30
- **MaxOutputTokens**: Default is 2048

## Troubleshooting

### "GEMINI_API_KEY is not configured"
- Make sure you've set the environment variable
- Restart your terminal/IDE after setting it
- Verify with: `echo %GEMINI_API_KEY%` (cmd) or `$env:GEMINI_API_KEY` (PowerShell)

### "No steps detected in recording"
- Make sure you clicked at least once during recording
- Clicks must be at least 300ms apart to be detected separately
- Try recording a longer session with more deliberate actions

### Recording doesn't start
- Howl requires Windows 10 or later
- Make sure you're running with appropriate permissions
- Check if antivirus is blocking the mouse hook

### AI generates poor instructions
- Try recording with clearer, more deliberate actions
- Ensure window titles are descriptive
- Keep recordings focused on a single task
- Avoid rapid clicking or mouse movements

### Want to inspect what's being sent to the AI?
- Enable **Debug Mode** before stopping the recording
- This will export the complete prompt without calling the API
- Review the step detection and prompt structure
- Useful for understanding why certain steps were detected or merged

## Limitations (MVP)

This is the MVP version. Current limitations:
- Single monitor only
- No audio narration
- No keystroke logging
- No video editing/trimming
- No redaction tools
- Windows only

See `design.md` for planned future features.

## License

This project is created for educational and demonstration purposes.

## Credits

Built with:
- .NET 9.0 & WPF
- Google Gemini API
- Windows API for screen capture and mouse hooks

---

**Howl** - Record once. Let Howl explain it.
