# vox

Minimal friction voice transcription CLI. Records audio, transcribes locally with Whisper, optionally refines with LLM, copies to clipboard.

## Architecture

```
Program.cs          - Main entry point, transcription loop, audio recording
Config.cs           - Configuration model (VoxConfig)
Providers/          - LLM provider abstraction (AnthropicProvider)
Features/           - Feature specifications and design docs
```

## Key Flow

`RunTranscription()` → `ProcessTranscriptionLoop()`:
1. Record audio with mode toggling (TAB switches Content/Instruction)
2. Transcribe with Whisper
3. Map segments to modes based on toggle timestamps
4. Optionally process with LLM (if instructions provided)
5. Revision loop: record → transcribe → append content → apply instructions
6. Copy final text to clipboard

## Configuration

`appsettings.json`:
- `whisper.modelSize`: tiny, base, small, medium, large
- `whisper.language`: Language code (e.g., "en")
- `llm.enabled`: Toggle LLM processing
- `llm.apiKey`: Anthropic API key
- `llm.model`: Claude model ID
- `clipboard.enabled`: Auto-copy to clipboard

## Commands

- `vox` - Start transcription
- `vox --diagnose` - Check system capabilities
- `vox --list-devices` - Show audio input devices
- `vox --help` - Show help

## Dependencies

- Whisper.net - Local speech-to-text
- PvRecorder (Picovoice) - Cross-platform audio capture
- Microsoft.Extensions.AI - LLM abstraction
- Spectre.Console - Terminal UI
- TextCopy - Clipboard access

## Feature Specs

See `Features/` directory:
- `LLM_Revision.md` - Draft-then-refine workflow with LLM
- `GPU_Acceleration.md` - CUDA support for faster transcription

## Current State

- CPU-only Whisper (GPU support planned)
- Anthropic Claude for LLM refinement
- Linux/Windows/macOS compatible
