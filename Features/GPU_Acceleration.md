# Problem

The current implementation uses CPU-only Whisper inference. While this works adequately for small/medium models, it creates friction for users who want:

- Faster transcription with larger, more accurate models
- Lower latency in the revision loop (multiple transcriptions per session)
- Practical use of the `large` model, which is unusably slow on CPU

# Proposed Solution

Add optional GPU acceleration via CUDA (NVIDIA) with graceful fallback to CPU.

## Configuration

Add `useGpu` setting to `appsettings.json`:
```json
{
  "whisper": {
    "useGpu": "auto"
  }
}
```

Behavior:
- `auto`: Try GPU, fall back to CPU silently (default)
- `force-gpu`: Try GPU, fail with error if unavailable
- `force-cpu`: Always use CPU (useful for testing/debugging)

## Package Dependencies

Add CUDA runtime package:
```xml
<PackageReference Include="Whisper.net.Runtime.Cuda" Version="1.9.0" />
```

Note: This adds ~500MB+ with CUDA dependencies. Consider:
- Separate build configurations (cpu-only vs cuda)
- Conditional package inclusion based on build target

## Code Changes

**WhisperFactory creation:**
```csharp
var builder = WhisperFactory.FromPath(modelPath);

if (config.Whisper.UseGpu != "force-cpu" && TryEnableCuda(builder))
{
    AnsiConsole.MarkupLine("[green]Using GPU acceleration[/]");
}
else
{
    AnsiConsole.MarkupLine("[yellow]Using CPU[/]");
}

using var whisperFactory = builder.Build();
```

**Helper method:**
```csharp
static bool TryEnableCuda(WhisperFactoryBuilder builder)
{
    try
    {
        builder.WithCuda();
        return true;
    }
    catch
    {
        return false;
    }
}
```

## Enhanced Diagnostics

Update `--diagnose` to show GPU info:
- GPU model name
- CUDA version
- Available VRAM
- Whether CUDA is available and working

## User Requirements

**For CUDA (NVIDIA GPUs):**
- NVIDIA GPU with CUDA compute capability 3.5+
- CUDA Toolkit 11.x or 12.x installed
- Updated NVIDIA drivers
- Minimum 2GB VRAM (4GB+ recommended for large models)

## Platform Considerations

| Platform | Acceleration | Notes |
|----------|--------------|-------|
| Windows/Linux | CUDA | Primary target |
| macOS | Metal | Separate implementation path |
| ARM64 | Limited | No CUDA support |

## Performance Expectations

Rough estimates (medium model):
- CPU (modern): 2-5x real-time (10s audio = 20-50s processing)
- GPU (RTX 4090): 10-20x real-time (10s audio = 0.5-1s processing)

Large model would be practical on GPU but unusably slow on CPU.

## Testing Checklist

- [ ] GPU available, CUDA works: Should use GPU
- [ ] GPU available, CUDA fails: Should fall back to CPU
- [ ] No GPU: Should use CPU without errors
- [ ] `force-gpu` with no GPU: Should show helpful error
- [ ] `force-cpu` with GPU: Should respect setting
- [ ] Multiple runs: GPU doesn't leak memory
- [ ] Large model on GPU: Handles VRAM limits gracefully

## Open Questions

1. Should we bundle CUDA runtime or require user installation?
2. Do we need multiple build targets (CPU-only vs CUDA)?
3. How do we handle CUDA version mismatches?
4. Metal support for macOS - separate feature or same config?

## Design Principles

- Graceful degradation over hard failures
- Keep "minimal friction" philosophy - should work out of box
- GPU is "nice to have" not "must have"
- Silent fallback in `auto` mode, verbose only on explicit `force-gpu`
