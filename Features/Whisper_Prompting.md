# Whisper Prompting

**Status:** Research complete, ready to experiment when bandwidth allows.

## The Opportunity

Whisper supports an `initial_prompt` parameter that biases transcription toward specific vocabulary and style. This could fix recurring misrecognitions without relying on LLM correction as a fallback.

## API

Whisper.net exposes this via the processor builder:
```csharp
using var processor = whisperFactory.CreateBuilder()
    .WithLanguage(config.Whisper.Language)
    .WithPrompt("your prompt here")
    .WithCarryInitialPrompt(true) // optional: reuse across 30s windows
    .Build();
```

## Key Findings

From [OpenAI's Whisper Prompting Guide](https://cookbook.openai.com/examples/whisper_prompting_guide):

1. **224 token limit** - only the final 224 tokens are used; longer prompts are truncated from the front
2. **Style imitation, not instruction following** - Whisper mimics the style/vocabulary of the prompt, doesn't follow directives
3. **Complete sentences > word lists** - `"Aimee discussed Claude, Anthropic, and vox."` works better than `"Claude, Anthropic, vox"`
4. **Longer = more reliable** - short prompts are unreliable; pack in context

## Implementation Ideas

**Option A: Static config**
```json
{
  "whisper": {
    "prompt": "A conversation about software development, Claude, Anthropic, and voice transcription using vox."
  }
}
```

**Option B: Vocabulary list that gets templated into a sentence**
```json
{
  "whisper": {
    "vocabulary": ["Claude", "Anthropic", "vox", "Whisper"]
  }
}
```
Code generates: `"Discussion mentioning Claude, Anthropic, vox, Whisper."`

**Option C: Both** - vocabulary for quick additions, prompt for full control

## Option D: LLM-Corrected Feedback Loop

Instead of a static prompt, feed the LLM-corrected transcription back to Whisper as the prompt for subsequent segments:

```
Segment N transcribed by Whisper
    → LLM applies corrections
    → Corrected text becomes prompt for Segment N+1
```

**Why this could work well:**
- Vocabulary Whisper misrecognizes (e.g., "Claude" → "clawed") gets corrected by the LLM, then fed back as the prompt, biasing Whisper toward the correct spelling
- Adaptive - context naturally shifts as topics change
- Style consistency (punctuation, capitalization) gets reinforced

**Risk is minimal** because the LLM only applies explicit user corrections, not rewrites. It honors the contract of not rewriting text unprompted.

This is similar to what `WithCarryInitialPrompt(true)` does, but uses the *corrected* output instead of Whisper's raw output.

**Cold start:** First segment has no context. Could fall back to a static prompt (Option A/B) initially, or just start empty.

## Questions to Answer

- Does the feedback loop measurably improve recognition of problem words?
- What's the right balance of vocabulary vs natural sentence structure for the static fallback?
- Should static prompt be in system.md (tinker-friendly) or appsettings.json (config-like)?

## References

- [Whisper.net source](https://github.com/sandrohanea/whisper.net/blob/main/Whisper.net/WhisperProcessorBuilder.cs)
- [OpenAI Whisper Prompting Guide](https://cookbook.openai.com/examples/whisper_prompting_guide)
- [Prompt Engineering in Whisper (Medium)](https://medium.com/axinc-ai/prompt-engineering-in-whisper-6bb18003562d)
