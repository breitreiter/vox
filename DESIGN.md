vox is a very simple cli command for voice transcription. it is designed for minimal friction.

**ux**

*first-run experience:*

- detect if model needs downloading (first run or model missing)
- notify user: "First time setup: downloading whisper model (small, ~500MB)..."
- show progress indicator (braille animation or progress bar)
- notify when complete: "Setup complete! Ready to transcribe."
- proceed to normal startup

*normal flow:*

- notifies the user that it is listening on an appropriate audio device
- prompts the user to speak and press a key when complete
- when the user indicates work is complete, it transcribes any detected audio to english (todo-configure later?)
- prints the transcription to the console (so the user can check for errors)
- copies the transcription to the clipboard (so the text can be easily reused)
- notifies the user that the text is in the clipboard

skew toward static configuration in an appsettings.json rather than copious cli flags

**flags**

- `--list-devices` to enumerate available microphone devices (useful for appsettings.json configuration)
- `--diagnose` to check system capabilities (audio devices, GPU/CUDA availability, model cache, clipboard support)

**architecture**
use whisper.net for transcription (cross-platform, supports gpu acceleration)
use PvRecorder (Picovoice) for audio recording (cross-platform: windows/macos/linux, purpose-built for speech)

- whisper.net requires 16kHz WAV format; PvRecorder can provide this
- PvRecorder supports device enumeration for --list-devices flag
- audio device selection configured in appsettings.json (device names/IDs vary by platform)

**model selection strategy**

- default to "small" model (best value for general use, ~500MB)
- lazy download on first run from HuggingFace via whisper.net's built-in downloader
- models cached to ~/.vox/models/ (or platform equivalent)
- gpu acceleration: auto-detect CUDA, fallback to CPU if unavailable
- start with CPU-only support, add GPU as nice-to-have enhancement

**configuration (appsettings.json)**
lazy defaults with escape hatches for when auto-magic breaks:

```json
{
  "whisper": {
    "modelSize": "small",           // tiny|base|small|medium|large (or .en variants)
    "modelPath": null,              // null = auto-download to cache, or explicit path for manual downloads
    "useGpu": "auto",               // auto|force-cpu|force-gpu
    "downloadTimeout": 300,         // seconds
    "language": "en"                // target language (future: auto-detect)
  },
  "audio": {
    "deviceName": null,             // null = system default, or specific device from --list-devices
    "sampleRate": 16000             // whisper requirement
  },
  "clipboard": {
    "enabled": true
  }
}
```

**failure modes & escape hatches**

- GPU detection fails → force-cpu in config
- Download fails → manual download, set modelPath
- Device issues → --list-devices, then configure deviceName
- Any weirdness → --diagnose shows what's broken