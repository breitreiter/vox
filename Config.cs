namespace Vox;

public class VoxConfig
{
    public WhisperConfig Whisper { get; set; } = new();
    public AudioConfig Audio { get; set; } = new();
    public ClipboardConfig Clipboard { get; set; } = new();
    public LlmConfig Llm { get; set; } = new();
}

public class WhisperConfig
{
    public string ModelSize { get; set; } = "small";
    public string? ModelPath { get; set; }
    public string UseGpu { get; set; } = "auto";
    public int DownloadTimeout { get; set; } = 300;
    public string Language { get; set; } = "en";
}

public class AudioConfig
{
    public string? DeviceName { get; set; }
    public int SampleRate { get; set; } = 16000;
}

public class ClipboardConfig
{
    public bool Enabled { get; set; } = true;
}

public class LlmConfig
{
    public bool Enabled { get; set; } = true;
    public string ModeToggleKey { get; set; } = "Tab";
    public string Provider { get; set; } = "Anthropic";
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "claude-sonnet-4-20250514";
}