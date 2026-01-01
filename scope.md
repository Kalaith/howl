HOWL — Initial Product Spec
Product Name

Howl

Tagline

Record once. Let Howl explain it.

Core Use Case

A user performs a task on their computer.
Howl records it.
Howl outputs a clean, step-by-step HTML guide with screenshots and instructions.

Think:

Internal docs

Tutorials

Bug reproduction

Onboarding

“How I did this” without typing

Target Users

Developers

Designers

QA testers

Support teams

Indie founders documenting workflows

Non-technical staff creating SOPs

MVP Scope (Be Ruthless)
1. Desktop Recorder (MVP)

Platforms: Start with Windows. Mac second.

Capture

Screen video (single monitor MVP)

Cursor position

Click events (left/right)

Window title changes

Controls

Start / Stop recording

Pause (optional for v1)

Highlight clicks visually

Non-Goals (v1)

Webcam

Audio narration

Keystroke logging

Multi-monitor stitching

2. Processing Pipeline

Triggered when recording stops.

Steps

Video ingestion

Frame extraction

Keyframe detection

Click-based frame capture

Scene change detection

Frame filtering

Remove duplicates

Blur sensitive areas (optional later)

LLM pass

Infer intent per step

Generate natural language instructions

Structure output

Ordered steps

Title + short summary

Optional prerequisites section

3. Output Format (Primary)

HTML Document

Structure

<h1>How to Deploy the App</h1>
<p>Recorded with Howl</p>

<ol>
  <li>
    <img src="step1.png" />
    <p>Open the deployment dashboard and select your project.</p>
  </li>
  ...
</ol>


Export Options

Download ZIP (HTML + images)

Copy-paste friendly markdown

Shareable link (later)

UX Flow

Open Howl

Click Start Recording

Perform task naturally

Click Stop

See “Howl is explaining your actions…”

Preview generated steps

Export HTML

The magic moment is step 6.
That’s where trust is earned.

Key Design Principles

No timelines

No editing unless necessary

Defaults should be “good enough”

The user is documenting, not filmmaking

Technical Direction (Suggested)
Desktop App

Tauri or Electron (Tauri preferred for weight)

Native OS screen capture APIs

Local temporary storage

Backend

Frame extraction service

LLM orchestration

Prompt tuned for procedural explanation

Stateless where possible

Storage

Local first

Optional cloud sync later

Differentiators (Lean Into These)

Step inference, not timestamps

Minimal user input

Clean HTML, not proprietary formats

Feels like the app watched and understood

Future Extensions (Do Not Build Yet)

Redaction tools

Voice narration → text

Team libraries

Versioned guides

Auto-update when process changes

IDE plugins

“Explain this bug” mode for QA

Why Howl Works

Most tools record.
Some tools transcribe.
Howl explains.

That distinction is your moat.

1. Howl — LLM Prompt Structure
Core Philosophy

The LLM does interpretation and narration, not discovery.

You tell it:

what happened

in what order

with visual anchors

It tells you:

why it mattered

how to say it like a human

Prompt Layers (Do Not Collapse These)
System Prompt (Static, Strong)

This never changes per request.

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


This is your spine.

Context Prompt (Generated per Recording)

This sets the scene.

Task context:
- Operating system: Windows
- Application(s) used:
  - "Visual Studio Code"
  - "Google Chrome"
- Approximate task duration: 2 minutes
- Goal (inferred): "Deploy a web application"


Goal inference can be LLM-powered later. For now, heuristic or empty is fine.

Observation Payload (Structured, Not Prose)

This is where most people fail.
Never dump raw logs.

Use structured JSON-like data.

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

StepCandidate 3:
- Trigger: Left click
- Window title: "Chrome - Deploy"
- UI change: Button activated
- Screenshot: step_03.png


Important:

You are suggesting step boundaries, not asserting them.

The LLM is allowed to merge or discard.

Instruction Request (Explicit Output Contract)

Be painfully clear.

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


If you want HTML later, wrap it here, not upstream.

Optional Post-Processing Prompt (Second Pass)

Used only if output is messy.

Rewrite the instructions to:
- Reduce redundancy
- Improve clarity
- Ensure consistent tone


This saves tokens in the main pass.

Why This Structure Works

Deterministic

Easy to diff

Debuggable when output is bad

Lets heuristics do the heavy lifting

The LLM becomes a narrator, not a detective.

2. Step Detection Heuristics (This Is the Real Engine)
Prime Directive

Steps are moments of commitment.
Not movement. Not hesitation. Commitment.

Core Step Triggers (MVP)

A new step candidate is created when any of these occur:

1. Click That Changes State

Left click

Followed by:

Window title change

New UI surface

Modal open

Page navigation

Enabled → disabled button

This is your bread.

2. Window / App Focus Change

Alt-tab

New foreground window

Title changes meaningfully

This often signals a new intent.

3. Significant Visual Change

Use cheap heuristics first.

Frame diff above threshold

Layout shift

New dominant UI block

No ML required yet.

4. Terminal or Editor Commit

If detectable:

Enter pressed

Command executed

Save triggered

These are decisive actions.

Step Suppression Rules (Critical)

Without this, you’ll get garbage.

Suppress if:

Multiple clicks within 300ms

Clicks on same screen region

No visual change follows

Mouse movement without click

Scroll without stopping

Scrolling only becomes a step if it reveals a new actionable area.

Step Merging Rules

After raw detection, run a merge pass.

Merge adjacent steps if:

Same window

Same UI surface

Occur within 2 seconds

No meaningful visual delta

Example:

Click tab

Click button immediately after
→ single step

Screenshot Selection Rules

Each step gets one canonical image.

Pick the frame:

After UI settles

After animation ends

Where the result is visible

Never the click moment.
Always the outcome.

Step Data Model (C# Friendly)
class StepCandidate {
    int Index;
    string WindowTitle;
    StepTrigger Trigger;
    Bitmap Screenshot;
    VisualChangeType ChangeType;
    DateTime Timestamp;
}


This feeds the prompt, not the video.

Quality Bar for a “Good Step”

Before sending to LLM, ask:

Could a human explain this action in one sentence?

If no, merge or discard.

Why This Will Feel Magical

Because the system:

Waits for intent

Captures outcomes

Speaks in human goals

The LLM just gives it voice.