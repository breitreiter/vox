# vox

Minimal friction voice transcription for the command line.

> In December 2025, I had an arm injury and needed speech-to-text. I wasn't happy with any of the existing options out
> there. So here we are.

## What is vox?

vox is a simple CLI tool that records audio from your microphone, transcribes it locally using OpenAI's Whisper model,
and copies the result to your clipboard.

## Installation

1. Build the project:
   ```bash
   dotnet build
   ```

2. Run from the build directory:
   ```bash
   ./bin/Debug/net8.0/vox
   ```

Or install globally (optional):

```bash
dotnet publish -c Release
sudo cp bin/Release/net8.0/vox /usr/local/bin/
```

## First Run

On first run, vox will:

1. Download the Whisper model (~466MB for the default "small" model)
2. Show a progress bar during download
3. Cache the model at `~/.vox/models/` for future use

Subsequent runs are instant—no download needed.

## Usage

**Basic usage:**

```bash
vox
```

Speak into your microphone, press Enter when done. Transcription is printed and copied to clipboard.

**List audio devices:**

```bash
vox --list-devices
```

**System diagnostics:**

```bash
vox --diagnose
```

**Help:**

```bash
vox --help
```

## Configuration

Configuration is stored in `appsettings.json`:

```json
{
  "whisper": {
    "modelSize": "small",        // tiny, base, small, medium, large
    "modelPath": null,           // null = auto, or set explicit path
    "useGpu": "auto",            // auto, force-cpu, force-gpu (not implemented)
    "language": "en"             // target language
  },
  "audio": {
    "deviceName": null,          // null = default, or device from --list-devices
    "sampleRate": 16000
  },
  "clipboard": {
    "enabled": true
  }
}
```

### Model Sizes

- **tiny** (75MB): Fastest, least accurate
- **base** (140MB): Good for low-end hardware
- **small** (466MB): **Recommended** - best balance of speed and accuracy
- **medium** (1.5GB): Better accuracy, slower
- **large** (3GB): Best accuracy, requires significant resources

## Platform-Specific Setup

### Linux

Install clipboard tools:

```bash
sudo apt install xsel
# or
sudo apt install xclip
```

vox will warn you on startup if these are missing.

### macOS

No additional setup required. Clipboard support works out of the box.

### Windows

No additional setup required. Clipboard support works out of the box.

## Troubleshooting

### "Clipboard tools not found" on Linux

Install `xsel` or `xclip`:

```bash
sudo apt install xsel
```

You can also disable clipboard in `appsettings.json` if you don't need it.

### Wrong microphone being used

List available devices:

```bash
vox --list-devices
```

Then configure the device in `appsettings.json`:

```json
{
  "audio": {
    "deviceName": "Your Device Name Here"
  }
}
```

### Model download fails or is interrupted

Delete the partial download and try again:

```bash
rm ~/.vox/models/ggml-*.bin
vox
```

The model will re-download automatically.

### Transcription quality is poor

Try a larger model in `appsettings.json`:

```json
{
  "whisper": {
    "modelSize": "medium"
  }
}
```

Larger models are more accurate but slower. The `small` model is recommended for most use cases.

### "No audio detected"

Check your microphone:

1. Verify your mic works in other applications
2. Run `vox --list-devices` to see available devices
3. Try selecting a different device in `appsettings.json`
4. Check system audio settings/permissions

## FAQ

**Q: Does this require an internet connection?**
A: Only for the initial model download. After that, everything runs locally.

**Q: Does this send my audio anywhere?**
A: No. All processing happens locally on your machine.

**Q: Can I use this with a different language?**
A: Yes! Change the `language` setting in `appsettings.json`. Whisper supports many languages.

**Q: Why is the first run slow?**
A: The Whisper model needs to download (~466MB for small model). Subsequent runs are instant.

**Q: Can I use this with GPU acceleration?**
A: GPU acceleration is not yet implemented. The CPU-only version works well for most use cases with the small or medium
models.

**Q: Where are the models stored?**
A: `~/.vox/models/` (Linux/macOS) or `%USERPROFILE%\.vox\models\` (Windows)

**Q: Can I use multiple models?**
A: Yes. Change `modelSize` in `appsettings.json`. Each model downloads separately and they coexist in the cache.

## License

MIT License

Copyright (c) 2025

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## Credits

- Built with [Whisper.net](https://github.com/sandrohanea/whisper.net)
- Uses [OpenAI Whisper](https://github.com/openai/whisper) models
- Audio recording via [PvRecorder](https://github.com/Picovoice/pvrecorder)
- UI powered by [Spectre.Console](https://github.com/spectreconsole/spectre.console)
