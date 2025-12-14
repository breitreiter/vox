# Development Notes

Notes for Claude (or future developers) working on vox.

## Future Enhancement: CUDA Support

**Current state:** CPU-only implementation. Works fine with small/medium models, accuracy is good.

**Why consider CUDA:**
- User has RTX 4090 at home
- Would enable faster transcription with larger models
- Could make `large` model practical for daily use
- Performance boost for repeated transcriptions

### Implementation Plan

#### 1. Package Dependencies

Add CUDA runtime to `vox.csproj`:
```xml
<PackageReference Include="Whisper.net.Runtime.Cuda" Version="1.9.0" />
```

Note: This is ~500MB+ with CUDA dependencies. Consider making it optional or having separate build configurations.

#### 2. Code Changes

**In `Program.cs` - WhisperFactory creation:**

Current (CPU):
```csharp
using var whisperFactory = WhisperFactory.FromPath(modelPath);
```

With GPU support:
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

**New helper method:**
```csharp
static bool TryEnableCuda(WhisperFactoryBuilder builder)
{
    try
    {
        builder.WithCuda(); // or whatever the API is
        return true;
    }
    catch
    {
        return false; // CUDA not available or failed to initialize
    }
}
```

#### 3. Enhanced Diagnostics

Update `--diagnose` to show GPU info:
- GPU model name
- CUDA version
- Available VRAM
- Whether CUDA is available and working

Example:
```csharp
// In Diagnose method
try
{
    var gpuInfo = GetGpuInfo(); // Need to implement
    table.AddRow(
        "[cyan]GPU[/]",
        "[green]✓ Available[/]",
        $"{gpuInfo.Name} ({gpuInfo.VramGB}GB VRAM, CUDA {gpuInfo.CudaVersion})"
    );
}
catch
{
    table.AddRow(
        "[cyan]GPU[/]",
        "[grey]○ Not Available[/]",
        "CPU-only mode"
    );
}
```

#### 4. Configuration

The `useGpu` setting in `appsettings.json` is already in place:
```json
{
  "whisper": {
    "useGpu": "auto"  // auto | force-cpu | force-gpu
  }
}
```

Behavior:
- `auto`: Try GPU, fall back to CPU silently
- `force-gpu`: Try GPU, fail with error if unavailable
- `force-cpu`: Always use CPU (useful for testing)

#### 5. User Requirements Documentation

Update README.md to add GPU section:

**For CUDA (NVIDIA GPUs):**
- NVIDIA GPU with CUDA compute capability 3.5+
- CUDA Toolkit 11.x or 12.x installed
- Updated NVIDIA drivers
- Minimum 2GB VRAM (4GB+ recommended for large models)

**Installation:**
```bash
# Check CUDA availability
nvidia-smi

# If CUDA is available, rebuild with CUDA support
dotnet build -c Release
```

#### 6. Platform Considerations

- **Windows/Linux**: CUDA support
- **macOS**: Would need Metal support (different implementation path)
- **ARM64**: Limited/no CUDA support

Consider build configurations:
- `vox-cpu`: Lightweight, CPU-only
- `vox-cuda`: Includes CUDA runtime
- `vox`: Auto-detects and includes appropriate runtime

#### 7. Testing Checklist

- [ ] GPU available, CUDA works: Should use GPU
- [ ] GPU available, CUDA fails: Should fall back to CPU
- [ ] No GPU: Should use CPU without errors
- [ ] `force-gpu` with no GPU: Should show helpful error
- [ ] `force-cpu` with GPU: Should respect setting
- [ ] Multiple runs: GPU doesn't leak memory
- [ ] Large model on GPU: Handles VRAM limits gracefully

#### 8. Performance Expectations

Rough estimates (medium model):
- CPU (modern): 2-5x real-time (10 second audio = 20-50 seconds processing)
- GPU (RTX 4090): 10-20x real-time (10 second audio = 0.5-1 seconds processing)

Large model would be practical on 4090 but unusably slow on CPU.

### Open Questions

1. Should we bundle CUDA runtime or require user installation?
2. Do we need multiple build targets (CPU-only vs CUDA)?
3. How do we handle CUDA version mismatches?
4. Should we add telemetry to track GPU vs CPU usage?

### Dependencies to Research

- Exact whisper.net API for GPU selection
- CUDA version compatibility matrix
- VRAM requirements for each model size
- Metal support for macOS (separate track)

### Notes

- Current CPU implementation is fine for interactive use
- GPU support is "nice to have" not "must have"
- Prioritize graceful degradation over forcing GPU
- Keep the "minimal friction" philosophy - should work out of box

---

## Other Future Enhancements

### Voice Activity Detection (VAD)
Instead of manual Enter press, auto-detect when user stops speaking.
- Could use Whisper's built-in VAD
- Trade-off: convenience vs control

### Language Auto-Detection
Currently requires language in config. Could auto-detect.
- Whisper supports this
- Small accuracy cost for auto-detection

### Streaming Transcription
Show partial results as user speaks.
- More complex implementation
- Requires different Whisper API usage

### Model Manager
CLI tool to manage models:
```bash
vox models list
vox models download large
vox models delete tiny
```

### Export Formats
Support output beyond clipboard:
- `--output file.txt`
- `--format json` (with timestamps)
- `--format srt` (subtitle format)
