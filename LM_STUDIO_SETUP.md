# LM Studio Integration

Howl now supports **LM Studio** as the default AI provider! This means you can run everything locally without needing a Gemini API key.

## What Changed

### ✅ LM Studio is Now the Default
- No API key required
- Runs completely local on your machine
- Works with any OpenAI-compatible endpoint

### ✅ Dual AI Provider Support
- **LM Studio (Local)** - Default, no setup needed
- **Gemini (Cloud)** - Available as alternative with API key

## Requirements

### For LM Studio:
1. **LM Studio** running at `http://127.0.0.1:1234`
2. Model loaded: **zai-org/glm-4.6v-flash** (vision-capable model)
3. Server started in LM Studio

### For Gemini:
1. Google Gemini API key
2. Saved in config file or environment variable

## How to Use

### Starting LM Studio:
1. Open LM Studio
2. Load the vision model (zai-org/glm-4.6v-flash)
3. Start the local server
4. Verify it's running at `http://127.0.0.1:1234`

### Using Howl with LM Studio:
1. Launch Howl: `H:\claude\howl\run-howl.bat`
2. LM Studio is automatically selected
3. Record your session
4. Process with AI - no API key needed!

### Switching to Gemini:
1. Click the "Gemini (Cloud)" radio button
2. Enter your API key if not already configured
3. Record and process as normal

## UI Changes

New AI provider selection in the app:

```
○ LM Studio (Local)    ● Gemini (Cloud)
```

- **LM Studio**: Local AI, no authentication
- **Gemini**: Cloud AI, requires API key

## Configuration

### LM Studio Settings
Located in: `H:\claude\howl\src\Howl.Services\Howl.Services\Configuration\LMStudioConfiguration.cs`

Default settings:
```csharp
BaseUrl = "http://127.0.0.1:1234"
Model = "zai-org/glm-4.6v-flash" // Vision-capable model
Temperature = 0.3
MaxTokens = 2048
EnableVision = true // Sends screenshots to AI
```

### Changing the Endpoint
If your LM Studio runs on a different port or host, update the configuration:

```csharp
public string BaseUrl { get; set; } = "http://localhost:5000";
```

## API Compatibility

LM Studio provides an **OpenAI-compatible API** at:
```
POST http://127.0.0.1:1234/v1/chat/completions
```

The service automatically formats prompts in the OpenAI chat format:
- System message: Instructions for the AI
- User message: Your recording data and context

**Note**: Howl does NOT use `response_format` to avoid compatibility issues with different LM Studio versions. Instead, it relies on clear prompt instructions and intelligent JSON extraction from the response.

## Troubleshooting

### "Failed to communicate with LM Studio"
- **Check**: Is LM Studio running?
- **Check**: Is the model loaded?
- **Check**: Is the server started in LM Studio?
- **Check**: Can you access `http://127.0.0.1:1234` in your browser?

### "Request timed out"
- Large recordings may take longer to process
- Default timeout: 120 seconds
- Consider using a faster model or shorter recordings

### "BadRequest: response_format" Error
✅ **Fixed!** This error has been resolved by removing the incompatible response_format parameter.
- The app now relies on prompt instructions for JSON output
- Automatically extracts JSON from the LLM response
- Works with all LM Studio versions

### "Failed to parse JSON response"
If the model doesn't return valid JSON:
- Try a more instruction-following model
- Check the Debug Mode export to see what the model returned
- Consider switching to Gemini for more reliable output

### Switch to Gemini
If LM Studio isn't working, click "Gemini (Cloud)" and use the cloud API as a fallback.

## Benefits of LM Studio

✅ **Privacy**: Everything runs locally
✅ **No costs**: No API charges
✅ **Offline**: Works without internet
✅ **Customizable**: Use any compatible model
✅ **Fast**: No network latency

## Model Recommendations

For Howl, use vision-capable models that:
- Support vision/multimodal input
- Support JSON output
- Handle long prompts well
- Can process multiple images

**Currently configured: zai-org/glm-4.6v-flash**

### Vision Support

Howl now sends screenshots to the AI so it can see exactly what was on screen at each step. This dramatically improves instruction quality since the AI can:
- See the actual UI elements
- Understand visual context
- Write more accurate, specific instructions
- Describe what the user clicked or interacted with

To disable vision and use text-only mode, set `EnableVision = false` in LMStudioConfiguration.cs
