# Howl — Design Document

**Version:** 1.0
**Last Updated:** 2026-01-01
**Status:** Initial Design

---

## 1. Executive Summary

### 1.1 Product Overview

**Howl** is a desktop application that automatically converts recorded screen activity into clean, step-by-step HTML guides with screenshots and natural language instructions.

**Tagline:** Record once. Let Howl explain it.

### 1.2 Core Value Proposition

Most tools record. Some tools transcribe. **Howl explains.**

Howl leverages AI to understand user intent from screen recordings, transforming raw video into professional documentation without manual editing or extensive user input.

### 1.3 Target Users

- Developers creating internal documentation
- Designers documenting workflows
- QA testers reproducing bugs
- Support teams building knowledge bases
- Indie founders documenting processes
- Non-technical staff creating SOPs

---

## 2. System Architecture

### 2.1 High-Level Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Desktop Application                   │
│                  (Tauri/Electron - Windows MVP)          │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐ │
│  │   Recorder   │  │  Processing  │  │    Export    │ │
│  │   Engine     │→ │   Pipeline   │→ │    Engine    │ │
│  └──────────────┘  └──────────────┘  └──────────────┘ │
│         ↓                  ↓                   ↓        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐ │
│  │ Screen       │  │ Step         │  │ HTML/MD      │ │
│  │ Capture API  │  │ Detection    │  │ Generator    │ │
│  └──────────────┘  └──────────────┘  └──────────────┘ │
│                          ↓                              │
│                   ┌──────────────┐                      │
│                   │     LLM      │                      │
│                   │ Orchestrator │                      │
│                   └──────────────┘                      │
└─────────────────────────────────────────────────────────┘
                           ↓
                  ┌─────────────────┐
                  │ Local File      │
                  │ System          │
                  └─────────────────┘
```

### 2.2 Core Components

1. **Recorder Engine**: Captures screen activity, clicks, and window state
2. **Processing Pipeline**: Extracts frames, detects steps, filters duplicates
3. **Step Detection Engine**: Applies heuristics to identify meaningful actions
4. **LLM Orchestrator**: Generates natural language instructions via Gemini API (see Section 5.5)
5. **Export Engine**: Produces HTML/Markdown output with screenshots

### 2.3 Technology Stack

- **Desktop Framework**: Tauri (preferred) or Electron
- **Screen Capture**: Native OS APIs (Windows Screen Capture API)
- **Video Processing**: Frame extraction service
- **AI/LLM**: Google Gemini 2.5 Flash API (prompt-tuned for procedural explanation)
- **HTTP Client**: HttpClient (.NET) for API communication
- **Storage**: Local-first temporary storage
- **Output**: Static HTML + PNG images
- **Configuration**: Environment variables + appsettings.json

---

## 3. Functional Requirements

### 3.1 Recording Capabilities (MVP)

#### Capture
- Screen video (single monitor only)
- Cursor position tracking
- Click events (left/right click detection)
- Window title changes
- Application focus changes

#### Controls
- Start/Stop recording
- Visual click highlighting during recording
- Pause functionality (optional for v1)

#### Non-Goals (v1)
- ❌ Webcam capture
- ❌ Audio narration
- ❌ Keystroke logging
- ❌ Multi-monitor stitching

### 3.2 Processing Pipeline

#### Stage 1: Video Ingestion
- Accept recorded video stream
- Store temporarily on local disk

#### Stage 2: Frame Extraction
- Extract frames at key moments
- Implement keyframe detection
- Capture frames on click events
- Detect scene changes

#### Stage 3: Frame Filtering
- Remove duplicate frames
- Apply blur to sensitive areas (future)
- Select canonical screenshot per step

#### Stage 4: Step Detection
Apply heuristics (see Section 5.2) to identify:
- Meaningful user actions
- State-changing interactions
- Intent boundaries

#### Stage 5: LLM Processing
- Infer intent per step
- Generate natural language instructions
- Structure output hierarchically

### 3.3 Output Format

#### Primary: HTML Document

**Structure:**
```html
<h1>How to Deploy the App</h1>
<p>Recorded with Howl</p>

<ol>
  <li>
    <img src="step1.png" />
    <p>Open the deployment dashboard and select your project.</p>
  </li>
  <li>
    <img src="step2.png" />
    <p>Click the Deploy button to start the deployment process.</p>
  </li>
  ...
</ol>
```

#### Export Options
- Download ZIP (HTML + images)
- Copy-paste friendly Markdown
- Shareable link (future)

---

## 4. User Experience Flow

### 4.1 Primary User Journey

1. **Open Howl** → Desktop app launches
2. **Click "Start Recording"** → Screen recording begins with visual feedback
3. **Perform task naturally** → User executes their workflow normally
4. **Click "Stop"** → Recording ends, processing begins
5. **"Howl is explaining your actions…"** → Progress indicator
6. **Preview generated steps** → Review and validate output ✨ *Magic moment*
7. **Export HTML** → Download or copy final guide

### 4.2 Design Principles

1. **No Timeline Editing** — Users document, not film
2. **Defaults Should Be "Good Enough"** — Minimal manual editing
3. **Focus on Intent, Not Actions** — Explain the "why," not just the "what"
4. **Trust Through Accuracy** — Step 6 is where trust is earned

---

## 5. Technical Design

### 5.1 Recording Engine

#### Windows Screen Capture Implementation
```csharp
// Conceptual structure
class RecordingSession {
    VideoStream videoStream;
    List<ClickEvent> clicks;
    List<WindowEvent> windowEvents;
    DateTime startTime;

    void Start();
    void Stop();
    void PauseResume(); // Optional v1
}

class ClickEvent {
    Point position;
    ClickType type; // Left, Right
    DateTime timestamp;
}

class WindowEvent {
    string windowTitle;
    string processName;
    DateTime timestamp;
}
```

### 5.2 Step Detection Heuristics

**Prime Directive:** *Steps are moments of commitment, not movement.*

#### Core Step Triggers

**1. Click That Changes State**
- Left click followed by:
  - Window title change
  - New UI surface
  - Modal open/close
  - Page navigation
  - Button state change (enabled → disabled)

**2. Window/App Focus Change**
- Alt-tab detection
- New foreground window
- Meaningful title change

**3. Significant Visual Change**
- Frame diff above threshold (no ML required)
- Layout shift detection
- New dominant UI block

**4. Terminal or Editor Commit**
- Enter key pressed (if detectable)
- Command executed
- Save action triggered

#### Step Suppression Rules

**Suppress step candidates if:**
- Multiple clicks within 300ms
- Clicks on same screen region without visual change
- No visual change follows click
- Mouse movement without click
- Continuous scrolling without stopping

**Note:** Scrolling becomes a step only if it reveals a new actionable area.

#### Step Merging Rules

**Merge adjacent steps if:**
- Same window
- Same UI surface
- Occur within 2 seconds
- No meaningful visual delta

**Example:** Click tab → Click button immediately after = single step

#### Screenshot Selection Rules

Each step gets one canonical image. Select the frame:
- ✅ After UI settles
- ✅ After animation ends
- ✅ Where the result is visible
- ❌ Never the click moment
- ✅ Always the outcome

### 5.3 Step Data Model

```csharp
class StepCandidate {
    int Index;
    string WindowTitle;
    StepTrigger Trigger;
    Bitmap Screenshot;
    VisualChangeType ChangeType;
    DateTime Timestamp;
}

enum StepTrigger {
    Click,
    WindowChange,
    VisualChange,
    KeyboardCommit
}

enum VisualChangeType {
    PageNavigation,
    ModalOpen,
    ModalClose,
    StateChange,
    NewContent
}
```

**Quality Bar:** Before sending to LLM, ask: *"Could a human explain this action in one sentence?"* If no, merge or discard.

### 5.4 LLM Prompt Architecture

**Philosophy:** The LLM does interpretation and narration, not discovery.

**You tell it:**
- What happened
- In what order
- With visual anchors

**It tells you:**
- Why it mattered
- How to say it like a human

#### Prompt Layers (Do Not Collapse)

**1. System Prompt (Static)**
```
You are Howl, a system that explains recorded computer actions as clear,
step-by-step instructions for another human to follow.

Rules:
- Do not mention timestamps.
- Do not mention the recording or the user.
- Do not describe mouse movement unless it matters.
- Prefer intent over mechanics.
- Each step must be achievable by a human.
- If an action is ambiguous, choose the most likely user intent.
- Write in clear, neutral instructional language.
- One action per step.
```

**2. Context Prompt (Generated per Recording)**
```
Task context:
- Operating system: Windows
- Application(s) used:
  - "Visual Studio Code"
  - "Google Chrome"
- Approximate task duration: 2 minutes
- Goal (inferred): "Deploy a web application"
```

**3. Observation Payload (Structured JSON)**
```
Observed actions (ordered):

StepCandidate 1:
- Trigger: Left click
- Window title: "Chrome - Dashboard"
- UI change: Page navigation
- Screenshot: step_01.png

StepCandidate 2:
- Trigger: Left click
- Window title: "Chrome - Project Settings"
- UI change: Modal opened
- Screenshot: step_02.png
```

**Important:** You are suggesting step boundaries, not asserting them. The LLM may merge or discard.

**4. Instruction Request (Explicit Output Contract)**
```
Generate a numbered list of instructions.

For each instruction:
- Write one sentence.
- Describe what the user is trying to accomplish.
- Refer implicitly to the screenshot provided.
- Do not name UI elements unless necessary.
- Do not include tips or warnings.

Output format:
1. <instruction text>
2. <instruction text>
...
```

**5. Optional Post-Processing Prompt (Second Pass)**
```
Rewrite the instructions to:
- Reduce redundancy
- Improve clarity
- Ensure consistent tone
```

#### Why This Structure Works
- ✅ Deterministic
- ✅ Easy to diff
- ✅ Debuggable when output is bad
- ✅ Lets heuristics do the heavy lifting
- ✅ LLM becomes a narrator, not a detective

### 5.5 Gemini API Integration

**Model:** Google Gemini 2.5 Flash (or latest stable version)
**Endpoint:** `https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent`

#### Configuration

```csharp
public class GeminiConfiguration
{
    public string ApiKey { get; set; }
    public string Model { get; set; } = "gemini-2.5-flash";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/models";

    public void LoadFromEnvironment()
    {
        ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? throw new InvalidOperationException("GEMINI_API_KEY is not configured");
    }
}
```

#### Service Implementation

```csharp
public class GeminiService
{
    private readonly GeminiConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;

    public GeminiService(GeminiConfiguration config, HttpClient httpClient)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _endpoint = $"{_config.BaseUrl}/{_config.Model}:generateContent?key={_config.ApiKey}";
    }

    public async Task<GeminiResponse> GenerateInstructionsAsync(
        string systemPrompt,
        string contextPrompt,
        string observationPayload,
        string instructionRequest,
        CancellationToken cancellationToken = default)
    {
        // Combine all prompt parts
        string fullPrompt = $@"{systemPrompt}

{contextPrompt}

{observationPayload}

{instructionRequest}";

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = fullPrompt }
                    }
                }
            },
            generationConfig = new
            {
                response_mime_type = "application/json",
                temperature = 0.3,  // Lower temperature for more consistent output
                topP = 0.8,
                topK = 40,
                maxOutputTokens = 2048
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new GeminiApiException(
                    $"Gemini API returned {response.StatusCode}: {errorContent}",
                    (int)response.StatusCode);
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<GeminiApiResponse>(responseContent);

            return ParseGeminiResponse(result);
        }
        catch (HttpRequestException ex)
        {
            throw new GeminiApiException("Failed to communicate with Gemini API", ex);
        }
    }

    private GeminiResponse ParseGeminiResponse(GeminiApiResponse apiResponse)
    {
        if (apiResponse?.Candidates == null || apiResponse.Candidates.Length == 0)
        {
            throw new GeminiApiException("No candidates returned from Gemini API");
        }

        var candidate = apiResponse.Candidates[0];
        var textContent = candidate.Content?.Parts?[0]?.Text;

        if (string.IsNullOrEmpty(textContent))
        {
            throw new GeminiApiException("Empty response from Gemini API");
        }

        // Parse the JSON response from Gemini
        var instructions = JsonSerializer.Deserialize<GeminiInstructionResponse>(textContent);

        return new GeminiResponse
        {
            Instructions = instructions.Steps,
            Title = instructions.Title,
            Summary = instructions.Summary,
            Prerequisites = instructions.Prerequisites
        };
    }
}
```

#### Request/Response Models

```csharp
// API request/response models
public class GeminiApiResponse
{
    [JsonPropertyName("candidates")]
    public Candidate[] Candidates { get; set; }
}

public class Candidate
{
    [JsonPropertyName("content")]
    public Content Content { get; set; }

    [JsonPropertyName("finishReason")]
    public string FinishReason { get; set; }
}

public class Content
{
    [JsonPropertyName("parts")]
    public Part[] Parts { get; set; }
}

public class Part
{
    [JsonPropertyName("text")]
    public string Text { get; set; }
}

// Expected instruction format from Gemini
public class GeminiInstructionResponse
{
    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; }

    [JsonPropertyName("prerequisites")]
    public string[] Prerequisites { get; set; }

    [JsonPropertyName("steps")]
    public InstructionStep[] Steps { get; set; }
}

public class InstructionStep
{
    [JsonPropertyName("stepNumber")]
    public int StepNumber { get; set; }

    [JsonPropertyName("instruction")]
    public string Instruction { get; set; }

    [JsonPropertyName("screenshotReference")]
    public string ScreenshotReference { get; set; }
}

// Domain model
public class GeminiResponse
{
    public string Title { get; set; }
    public string Summary { get; set; }
    public string[] Prerequisites { get; set; }
    public InstructionStep[] Instructions { get; set; }
}

// Custom exception
public class GeminiApiException : Exception
{
    public int? StatusCode { get; }

    public GeminiApiException(string message) : base(message) { }

    public GeminiApiException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public GeminiApiException(string message, Exception innerException)
        : base(message, innerException) { }
}
```

#### Updated Instruction Request Prompt for JSON Output

Since we're using `response_mime_type: "application/json"`, the instruction request should specify the exact JSON schema:

```
Generate a JSON object with the following structure:

{
  "title": "A clear, action-oriented title for this guide",
  "summary": "A brief 1-2 sentence summary of what this guide accomplishes",
  "prerequisites": ["Optional array of things needed before starting"],
  "steps": [
    {
      "stepNumber": 1,
      "instruction": "Clear, concise instruction text",
      "screenshotReference": "step_01.png"
    }
  ]
}

Rules for instructions:
- Write one sentence per step
- Describe what the user is trying to accomplish
- Refer implicitly to the screenshot
- Do not name UI elements unless necessary
- Do not include tips or warnings
- Focus on intent, not mechanics
```

#### Error Handling Strategy

```csharp
public async Task<GeminiResponse> GenerateWithRetry(
    string systemPrompt,
    string contextPrompt,
    string observationPayload,
    string instructionRequest,
    int maxRetries = 3)
{
    int attempt = 0;
    Exception lastException = null;

    while (attempt < maxRetries)
    {
        try
        {
            return await GenerateInstructionsAsync(
                systemPrompt,
                contextPrompt,
                observationPayload,
                instructionRequest);
        }
        catch (GeminiApiException ex) when (ex.StatusCode >= 500)
        {
            // Retry on server errors
            lastException = ex;
            attempt++;
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt))); // Exponential backoff
        }
        catch (GeminiApiException ex) when (ex.StatusCode == 429)
        {
            // Rate limit - wait longer
            lastException = ex;
            attempt++;
            await Task.Delay(TimeSpan.FromSeconds(10 * attempt));
        }
        catch (GeminiApiException ex) when (ex.StatusCode >= 400 && ex.StatusCode < 500)
        {
            // Client errors - don't retry
            throw;
        }
    }

    throw new GeminiApiException(
        $"Failed after {maxRetries} attempts",
        lastException);
}
```

#### Configuration in App Settings

**appsettings.json:**
```json
{
  "Gemini": {
    "Model": "gemini-2.5-flash",
    "BaseUrl": "https://generativelanguage.googleapis.com/v1beta/models",
    "MaxRetries": 3,
    "TimeoutSeconds": 30,
    "Temperature": 0.3,
    "MaxOutputTokens": 2048
  }
}
```

**Environment Variables:**
```
GEMINI_API_KEY=your_api_key_here
```

**Important Notes:**
- API key is passed as a query parameter: `?key={apiKey}`
- Use `response_mime_type: "application/json"` for structured output
- Gemini 2.5 Flash is optimized for speed and cost-effectiveness
- Temperature 0.3 provides good balance between consistency and natural language
- The response is nested: `candidates[0].content.parts[0].text` contains the JSON string
- Always validate and parse the JSON response before using it

---

## 6. Data Flow

### 6.1 End-to-End Process

```
┌──────────────┐
│ User Records │
│   Workflow   │
└──────┬───────┘
       │
       ▼
┌────────────────────────┐
│  Recording Engine      │
│  - Video stream        │
│  - Click events        │
│  - Window events       │
└──────┬─────────────────┘
       │
       ▼
┌────────────────────────┐
│  Frame Extraction      │
│  - Keyframe detection  │
│  - Click-based capture │
│  - Scene changes       │
└──────┬─────────────────┘
       │
       ▼
┌────────────────────────┐
│  Step Detection        │
│  - Apply triggers      │
│  - Suppression rules   │
│  - Merging logic       │
└──────┬─────────────────┘
       │
       ▼
┌────────────────────────┐
│  Frame Filtering       │
│  - Remove duplicates   │
│  - Select canonical    │
└──────┬─────────────────┘
       │
       ▼
┌────────────────────────┐
│  LLM Processing        │
│  - Intent inference    │
│  - Instruction gen     │
└──────┬─────────────────┘
       │
       ▼
┌────────────────────────┐
│  HTML Generation       │
│  - Format output       │
│  - Package images      │
└──────┬─────────────────┘
       │
       ▼
┌────────────────────────┐
│  Export (ZIP/MD/HTML)  │
└────────────────────────┘
```

### 6.2 File Storage Structure

```
temp/
  session_{guid}/
    raw_video.mp4
    frames/
      frame_0001.png
      frame_0002.png
      ...
    events.json
    step_candidates.json

output/
  guide_{guid}/
    index.html
    step_01.png
    step_02.png
    ...
    guide.md (optional)
```

---

## 7. Non-Functional Requirements

### 7.1 Performance

- Recording should have minimal performance impact (<5% CPU overhead)
- Processing should complete within 2x recording duration
- Frame extraction should handle 60 FPS input
- Step detection should run in <5 seconds for 2-minute recordings

### 7.2 Reliability

- Recording must never crash during capture
- All user data stored locally until export
- Graceful handling of LLM API failures
- Auto-save recording buffer every 30 seconds

### 7.3 Usability

- First-time user can create a guide in <3 minutes
- No configuration required for basic use
- Preview must load in <2 seconds
- Export must be one-click

### 7.4 Privacy & Security

- All recordings stored locally by default
- No data sent to cloud except LLM API calls (screenshots + metadata)
- User must opt-in to any cloud features
- Clear indicators when recording is active
- Option to auto-delete temporary files after export

---

## 8. Differentiators

### 8.1 Competitive Advantages

1. **Step Inference, Not Timestamps** — Understands intent, not just clicks
2. **Minimal User Input** — No manual annotation required
3. **Clean HTML Output** — Not proprietary formats
4. **Intent Understanding** — Feels like the app watched and understood

### 8.2 Magic Moment

*Preview screen showing AI-generated steps that perfectly match user intent.*

This is where trust is earned. The output must be so accurate that users are delighted, not frustrated.

---

## 9. Future Extensions (Not MVP)

**Do not build these yet.**

- Redaction tools for sensitive data
- Voice narration → text conversion
- Team libraries and shared guides
- Versioned guides with change tracking
- Auto-update when process changes
- IDE plugins for developer workflows
- "Explain this bug" mode for QA teams
- Multi-monitor support
- Cloud sync and shareable links
- Collaborative editing

---

## 10. Implementation Phases

### Phase 1: Core MVP (Windows Only)
- [ ] Desktop app shell (Tauri/Electron)
- [ ] Screen recording engine
- [ ] Click + window event capture
- [ ] Basic frame extraction
- [ ] Step detection heuristics
- [ ] Gemini API service implementation (Section 5.5)
  - [ ] GeminiService class with retry logic
  - [ ] Configuration management (API key)
  - [ ] Request/response models
  - [ ] Error handling
- [ ] LLM prompt integration and testing
- [ ] HTML export with screenshot embedding

### Phase 2: Polish & Validation
- [ ] Preview UI
- [ ] Export options (ZIP, Markdown)
- [ ] Visual click highlighting
- [ ] Error handling & recovery
- [ ] User testing & feedback
- [ ] Performance optimization

### Phase 3: Cross-Platform
- [ ] macOS support
- [ ] Platform-specific capture APIs
- [ ] Unified UI across platforms

### Phase 4: Advanced Features
- [ ] Post-processing editor (optional edits)
- [ ] Redaction tools
- [ ] Cloud storage integration
- [ ] Shareable links

---

## 11. Success Metrics

### 11.1 Quality Metrics
- **Step Accuracy**: >90% of generated steps match user intent
- **Instruction Clarity**: >80% of users understand steps without clarification
- **Screenshot Relevance**: >95% of screenshots show the correct UI state

### 11.2 Usage Metrics
- Time to first export: <5 minutes
- User retention after first use: >60%
- Average guides created per user per week: >2

### 11.3 Technical Metrics
- Recording overhead: <5% CPU
- Processing speed: <2x recording duration
- Export success rate: >99%

---

## 12. Open Questions & Decisions Needed

### Decided:
1. ✅ **LLM Provider**: Google Gemini 2.5 Flash API (see Section 5.5)
   - Cost-effective and fast
   - Structured JSON output support
   - Proven reliability from existing implementation

### Still Open:
1. **Frame Rate**: What FPS for frame extraction? (Recommend 1-5 FPS)
2. **Video Codec**: Which codec for temporary storage? (H.264, VP9?)
3. **Pricing Model**: Free tier limits? Subscription vs one-time purchase?
4. **Telemetry**: What usage data should we collect (if any)?
5. **API Cost Management**: How to handle Gemini API costs?
   - Batch processing vs real-time?
   - Local caching of results?
   - User API key vs hosted service?

---

## 13. Appendix

### 13.1 Use Cases

**Internal Documentation**
> Developer records a deployment process once, shares HTML guide with team.

**Bug Reproduction**
> QA tester records bug occurrence, sends guide to engineering with exact steps.

**Onboarding**
> Manager records company processes, creates library of SOPs for new hires.

**Tutorial Creation**
> Designer records Figma workflow, shares with design community.

**"How I Did This"**
> Indie founder documents complex setup without typing a single word.

### 13.2 Example Output

**Input:** User deploys a web app via a dashboard
**Output:**
```html
<h1>How to Deploy the Application</h1>
<p>Recorded with Howl</p>

<ol>
  <li>
    <img src="step1.png" />
    <p>Open the deployment dashboard and select your project from the list.</p>
  </li>
  <li>
    <img src="step2.png" />
    <p>Click the Deploy button in the top right corner.</p>
  </li>
  <li>
    <img src="step3.png" />
    <p>Confirm the deployment by clicking Yes in the dialog.</p>
  </li>
  <li>
    <img src="step4.png" />
    <p>Wait for the deployment to complete. You'll see a success message when it's done.</p>
  </li>
</ol>
```

---

**End of Design Document**
