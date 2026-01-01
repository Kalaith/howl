# Howl Quick Start Guide

Get up and running with Howl in 2 minutes!

## Step 1: Run Howl

Double-click `run-howl.bat` or run in terminal:
```bash
.\run-howl.bat
```

That's it! Howl will launch without requiring an API key.

## Step 2 (Optional): Add Your API Key

You have two options:

**Option A: Enter in App** (Quick & Easy)
1. When Howl launches, you'll see an API key input field (if not set in environment)
2. Paste your Gemini API key
3. Click "Save"
4. Done!

**Option B: Set as Environment Variable** (Permanent)
```powershell
[System.Environment]::SetEnvironmentVariable('GEMINI_API_KEY', 'YOUR-KEY-HERE', 'User')
```
Then restart Howl.

### Don't have an API key?
1. Visit https://aistudio.google.com/app/apikey
2. Sign in and create an API key
3. Copy it

**OR** just use Debug Mode to test without an API key!

## Step 3: Record Your First Guide

1. Click **"Start Recording"**
2. Perform 3-5 simple actions (e.g., open Notepad, type something, save file)
3. Click **"Stop Recording"**
4. Choose where to save (HTML for AI mode, or TXT for Debug mode)
5. Wait 30-60 seconds for AI processing (or instant for Debug mode)
6. Open the generated file!

## What to Record

**Good Examples:**
- How to create a new file in an application
- How to deploy an app from a dashboard
- How to export data from a web app
- How to configure a setting

**Tips:**
- 3-10 steps work best
- Click deliberately with brief pauses
- Each click should be a meaningful action
- Avoid rapid clicking

## Troubleshooting

**"GEMINI_API_KEY is not configured"**
→ You need to set the environment variable and restart your terminal

**"No steps detected"**
→ Make sure you clicked at least once during recording

**App doesn't start**
→ Run `dotnet build` first to make sure everything compiles

## Want to Test Without Using the API?

**Enable Debug Mode!**

1. Check the "Debug Mode (Skip AI - Export prompt only)" checkbox
2. Record as normal
3. Export saves a `.txt` file instead of HTML
4. See exactly what would be sent to Gemini
5. No API costs, works offline

Perfect for:
- Testing your recording technique
- Understanding how steps are detected
- Working without internet
- Learning how Howl works

See [DEBUG_MODE.md](DEBUG_MODE.md) for details.

## What You Get

Howl generates a beautiful HTML guide with:
- Clear title and summary
- Step-by-step instructions
- Screenshots for each step
- Professional styling
- Responsive design

## Example Output

Your generated HTML will look like:

```html
How to Create a New Text File

1. Open File Explorer and navigate to the desired location
   [Screenshot showing File Explorer]

2. Right-click in the empty space to open the context menu
   [Screenshot showing context menu]

3. Select "New" and then "Text Document" to create the file
   [Screenshot showing New > Text Document]
```

## Next Steps

- Read the full [README.md](README.md) for detailed documentation
- Check [design.md](design.md) to understand how Howl works
- Experiment with different workflows
- Share your generated guides!

---

**Need Help?** Check the Troubleshooting section in README.md
