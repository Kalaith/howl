# Debug Mode Guide

## What is Debug Mode?

Debug Mode allows you to test Howl's recording and step detection **without** calling the Gemini API. Instead of generating an HTML guide, it exports a detailed text file showing exactly what would be sent to the AI.

## Why Use Debug Mode?

### 1. **Testing Without Cost**
- No API calls = No costs
- Perfect for testing your recording technique
- Iterate quickly without waiting for AI responses

### 2. **Understanding the System**
- See exactly what prompt is constructed
- Understand how steps are detected and merged
- Learn what information the AI receives

### 3. **Debugging Issues**
- Figure out why certain steps weren't detected
- See which clicks were merged
- Inspect window title changes
- Review the exact observation payload

### 4. **Offline Development**
- Work without internet connection
- No need for API key during development
- Test the recording engine independently

## How to Enable

1. Launch Howl
2. **Check the "Debug Mode (Skip AI - Export prompt only)" checkbox**
3. Record your actions normally
4. Stop recording
5. Save as `.txt` file (default: `howl-debug-YYYY-MM-DD-HHMMSS.txt`)

## What You Get

### Debug Text File

A comprehensive text file containing:

#### Recording Details
- Session ID and timestamp
- Total duration
- Number of clicks captured
- Number of window changes
- Applications used
- Detected steps with timestamps

#### Complete Prompt Structure

1. **System Prompt** - The rules that define Howl's behavior
2. **Context Prompt** - OS info, apps used, duration
3. **Observation Payload** - Structured step data
4. **Instruction Request** - Expected JSON output format

#### Statistics
- Total character count
- Estimated token count
- Number of screenshots

### Screenshots Folder

All captured screenshots copied to `screenshots/` directory next to the debug file:
```
howl-debug-2026-01-01-143022.txt
screenshots/
  ├── step_01.png
  ├── step_02.png
  └── step_03.png
```

## Example Debug Output

```
═══════════════════════════════════════════════════════════════
                HOWL - DEBUG MODE EXPORT
               Prompt Preview (Not sent to AI)
═══════════════════════════════════════════════════════════════

Generated: 2026-01-01 14:30:22
Session ID: 8f3a2b1c-...
Duration: 42.5 seconds
Clicks Captured: 8
Window Changes: 3
Steps Detected: 4

───────────────────────────────────────────────────────────────
RECORDING DETAILS
───────────────────────────────────────────────────────────────

Applications Used:
  - explorer
  - notepad
  - chrome

Click Events:
  1. [14:29:45.123] Left click at (342, 567)
  2. [14:29:47.890] Left click at (125, 89)
  3. [14:29:50.234] Left click at (456, 234)
  ...

Detected Steps:
  Step 1: File Explorer
    Trigger: Click
    Change Type: StateChange
    Time: 14:29:45
    Screenshot: C:\...\frames\frame_0001.png

  Step 2: Notepad
    Trigger: WindowChange
    Change Type: PageNavigation
    Time: 14:29:47
    Screenshot: C:\...\frames\frame_0003.png

  ...

═══════════════════════════════════════════════════════════════
FULL PROMPT (What would be sent to Gemini API)
═══════════════════════════════════════════════════════════════

┌─────────────────────────────────────────────────────────────┐
│ SYSTEM PROMPT                                               │
└─────────────────────────────────────────────────────────────┘

You are Howl, a system that explains recorded computer actions...

[Complete system prompt here]

┌─────────────────────────────────────────────────────────────┐
│ CONTEXT PROMPT                                              │
└─────────────────────────────────────────────────────────────┘

Task context:
- Operating system: Windows
- Application(s) used:
  - "explorer"
  - "notepad"
- Approximate task duration: 42 seconds

[And so on...]

═══════════════════════════════════════════════════════════════
STATISTICS
═══════════════════════════════════════════════════════════════

Total Characters: 2,543
Estimated Tokens: ~635
Screenshots: 4
```

## Using Debug Output

### 1. Verify Step Detection

Check the "Detected Steps" section to see if Howl captured the right moments:
- Are all important actions present?
- Were any unnecessary steps included?
- Were similar steps properly merged?

### 2. Review the Prompt

Look at the "OBSERVATION PAYLOAD" section:
- Does it accurately describe your actions?
- Are window titles helpful?
- Is the context clear?

### 3. Estimate Costs

Use the statistics section:
- Check estimated token count
- Multiply by your API pricing
- Optimize recordings to reduce tokens

### 4. Test Iteration

Use debug mode to:
1. Record an action
2. Review the detection
3. Adjust your technique
4. Record again
5. Repeat until satisfied

Then disable debug mode and generate the real guide.

## Comparison: Debug vs Normal Mode

| Feature | Debug Mode | Normal Mode |
|---------|-----------|-------------|
| **API Call** | ❌ Skipped | ✅ Called |
| **Output** | Text file | HTML guide |
| **Cost** | Free | API usage |
| **Screenshots** | Separate folder | Embedded in HTML |
| **Speed** | Instant | 30-60 seconds |
| **Internet** | Not required | Required |
| **Use Case** | Testing/debugging | Production use |

## Tips for Using Debug Mode

1. **Test First**: Always do a debug export first when trying new recording techniques
2. **Review Regularly**: Check debug output to understand what the AI sees
3. **Optimize Prompts**: If you modify prompt templates, use debug mode to verify
4. **Share for Help**: Debug output is great for getting support (no API key needed)
5. **Learn the System**: Read through a few debug exports to understand Howl's logic

## When NOT to Use Debug Mode

- When you actually want to generate a guide (obviously!)
- When testing the full end-to-end flow including AI
- When you want to see how the AI interprets your actions
- When demonstrating Howl to others

## Pro Tip: Hybrid Workflow

1. **Record with debug mode enabled** - Quick feedback, no cost
2. **Review and refine** - Make sure steps look good
3. **Re-record if needed** - Adjust your actions
4. **Disable debug mode** - Final recording
5. **Generate guide** - Call the API once with confidence

This saves time and money by getting the recording right before calling the API!

---

**Debug mode is your friend.** Use it liberally during development and testing!
