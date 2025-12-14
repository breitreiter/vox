# Problem
The current design forces the user to treat the transcription tool as a keyboard replacement, leading to three primary usability frictions:

**Cognitive Tax**
Mental composition, rehearsal, and attempted verbatim regurgitation of prose. Failure to separate the Drafting (stream of thought) phase from the Refining (written prose) phase.

**Revision Burden**
Dependence on manual, high-effort, keyboard/mouse-based editing to finalize text. The app is a one-way transcriber (audio to text), lacking an integrated editing loop.

**Lost Nuance**
Necessary use of speech-native tools (tone, cadence) to convey meaning, which is stripped in transcription. The tool fails to recognize the difference between spoken language (contextual) and written language (explicit), requiring manual tone correction.

# Proposed Solution
Restructure the interaction from a single-shot capture-and-transcribe to a draft-then-refine workflow:

## Initial Capture
- On startup, inform user about the delimiter keyword (configurable in appsettings.json, default: "dictate"): "Use 'dictate' to separate context from content. Example: 'Email to Bob about the meeting dictate Hey Bob...'"
- Capture initial audio (allow fragmentary input, rambling, unstructured input)
- Generate transcription using Whisper
- Parse transcript for delimiter keyword (case-insensitive):
  - If present: text before delimiter = intent/context (passed to LLM as context), text after = content (passed to LLM for processing)
  - If absent: entire transcript = content, LLM infers intent from content itself

## Revision Loop (Multi-turn)
- Present current version of transcript
- Default to revision mode - automatically listen for more audio
- User dictates revision instructions (e.g., "make it more formal", "change the meeting time to 3pm", "remove that last sentence")
- Transcribe revision request
- Send to LLM: original content + revision request → generate new version
- Repeat loop until user signals completion

## Exit
- User indicates done (voice command, hotkey, or Ctrl+D - TBD)
- Copy final version to clipboard
- Exit

## LLM Integration
- Use Claude via official Anthropic .NET SDK
- System prompt must emphasize:
  - Working from spoken text (not written prose)
  - Preserve the user's voice and style
  - The LLM is generating text **as** the user, not **for** the user
  - Avoid transforming into "AI slop" or corporate-speak
  - Apply only the requested changes, don't over-edit

