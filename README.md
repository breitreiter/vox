# vox

Minimal friction voice transcription for the command line.

In December 2025, I had an arm injury and needed speech-to-text. I wasn't happy with any of the existing tools. 

I started with vanilla transcription, but found that was a pretty unpleasant way of working. So I added the current transcribe/refine loop, which seems to work pretty well.

## What is vox?

vox is a CLI tool that:
1. Records audio from your microphone
2. Transcribes it locally using OpenAI's Whisper model
3. Optionally processes it with an LLM (Claude)
4. Copies the result to your clipboard

During recording, toggle between **Content** mode (text you want in the output) and **Instruction** mode (directions for how to process that text) with the TAB key. 

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

**Basic transcription:**

```bash
vox
```

1. Speak into your microphone
2. Press **Tab** to toggle between Content and Instruction modes
3. Press **Enter** when done speaking
4. Review the raw transcription
5. If LLM is enabled and you provided instructions, review the processed result
6. Press **Enter** to append more content or give new instructions
7. Press **Escape** to accept and copy to clipboard

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
  },
  "llm": {
    "enabled": true,             // enable LLM processing
    "modeToggleKey": "Tab",      // key to toggle between content/instruction modes
    "provider": "Anthropic",     // currently only Anthropic (Claude) is supported
    "apiKey": null,              // your Anthropic API key (or set ANTHROPIC_API_KEY env var)
    "model": "claude-sonnet-4-20250514"
  }
}
```

**LLM Processing:**
- Set `llm.enabled` to `false` to disable LLM features (transcription only)
- Set `llm.apiKey` to your Anthropic API key to enable processing
- When enabled, you can toggle to Instruction mode to tell Claude how to process your text

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
A: Only for the initial Whisper model download. After that, transcription runs locally. If you enable LLM processing, it makes API calls to Anthropic.

**Q: Does this send my audio anywhere?**
A: No. Audio is transcribed locally. Only the text (not audio) is sent to Claude if you enable LLM processing.

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

**Q: Do I need an Anthropic API key?**
A: Only if you want LLM processing. Set `llm.enabled` to `false` for transcription-only mode.

**Q: How does mode switching work?**
A: Press Tab during recording to toggle between Content (text output) and Instruction (processing directions). The tool uses Whisper's segment timestamps to map which parts were spoken in which mode.

**Q: Can I just use this for transcription without any LLM features?**
A: Yes. Set `llm.enabled` to `false` in `appsettings.json`. You'll get raw transcription copied to clipboard.

## Credits

- Built with [Whisper.net](https://github.com/sandrohanea/whisper.net)
- Uses [OpenAI Whisper](https://github.com/openai/whisper) models
- Audio recording via [PvRecorder](https://github.com/Picovoice/pvrecorder)
- UI powered by [Spectre.Console](https://github.com/spectreconsole/spectre.console)
- LLM integration via [Anthropic SDK](https://github.com/anthropics/anthropic-sdk-dotnet)
