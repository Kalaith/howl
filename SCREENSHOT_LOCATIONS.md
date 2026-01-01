# Screenshot Locations in Howl

## Where Are Screenshots Saved?

When you record a session, screenshots are automatically captured and saved to your Windows temporary folder:

```
%TEMP%\Howl\session_{SessionId}\frames\
```

### Example Path:
```
C:\Users\YourUsername\AppData\Local\Temp\Howl\session_12345678-1234-1234-1234-123456789abc\frames\
```

## How to Find Your Screenshots

### 1. **During Recording**
After you stop recording, the app will show you the path in the progress area:
```
Screenshots saved to: C:\Users\...\AppData\Local\Temp\Howl\session_...\frames\
```

### 2. **If Export is Cancelled**
The app will ask: "Would you like to view the screenshots that were captured?"
- Click "Yes" to open the folder directly

### 3. **If Export Fails**
The app will ask: "Export failed, but screenshots were captured. Would you like to view them?"
- Click "Yes" to open the folder directly

### 4. **Manual Navigation**
1. Press `Windows + R`
2. Type: `%TEMP%\Howl`
3. Press Enter
4. Navigate to the session folder
5. Open the `frames` subfolder

## Screenshot Files

Screenshots are named sequentially:
- `frame_0001.png`
- `frame_0002.png`
- `frame_0003.png`
- etc.

## Debug Mode Export

When using **Debug Mode**, screenshots are copied to a folder next to your debug export file:

```
YourExportLocation\screenshots\
```

With files renamed to:
- `step_01.png`
- `step_02.png`
- `step_03.png`
- etc.

## Tips

- Screenshots are captured **300ms after each click** to allow UI updates
- Screenshots persist until you manually delete them from the temp folder
- Use Debug Mode to get a copy of screenshots in a more permanent location
