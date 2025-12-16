using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Pv;
using Spectre.Console;
using TextCopy;
using Vox.Providers;
using Whisper.net;
using Whisper.net.Ggml;

namespace Vox;

public enum RecordingMode { Content, Instruction }

public static class MarkupHelper
{
    /// <summary>
    /// Escapes text for safe use in Spectre.Console markup strings.
    /// Handles both user content and any text that might contain markup-like characters.
    /// </summary>
    public static string Sanitize(string text)
    {
        return text.Replace("[", "[[").Replace("]", "]]");
    }
}

public class ModeToggleEvent
{
    public TimeSpan Timestamp { get; set; }
    public RecordingMode Mode { get; set; }
}

public class RecordingResult
{
    public short[] AudioData { get; set; } = Array.Empty<short>();
    public List<ModeToggleEvent> ToggleEvents { get; set; } = new();
    public TimeSpan TotalDuration { get; set; }
}

public class TranscriptionSegment
{
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
    public string Text { get; set; } = "";
}

public class LabeledSegment
{
    public string Text { get; set; } = "";
    public RecordingMode Mode { get; set; }
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
}

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        // Preamble
        AnsiConsole.MarkupLine("🎙️[grey]vox 0.1α[/]");
        
        // Handle command-line flags
        if (args.Contains("--help") || args.Contains("-h"))
        {
            ShowHelp();
            return 0;
        }

        // Load configuration
        var config = LoadConfiguration();

        if (args.Contains("--list-devices")) return ListDevices();

        if (args.Contains("--diagnose")) return await Diagnose(config);

        // Run normal transcription flow
        return await RunTranscription(config);
    }

    private static void ShowHelp()
    {
        AnsiConsole.MarkupLine("[bold cyan]vox[/] - minimal friction voice transcription");
        AnsiConsole.MarkupLine("A simple CLI tool for voice-to-text transcription using Whisper.");
        AnsiConsole.MarkupLine("Records audio from your microphone, transcribes it locally, and copies to clipboard.");
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold]Command[/]"))
            .AddColumn(new TableColumn("[bold]Description[/]"));

        table.AddRow(
            "[cyan]vox[/]",
            "Start voice recording and transcription"
        );
        table.AddRow(
            "[cyan]vox --help[/], [cyan]-h[/]",
            "Show this help message"
        );
        table.AddRow(
            "[cyan]vox --list-devices[/]",
            "List available audio input devices"
        );
        table.AddRow(
            "[cyan]vox --diagnose[/]",
            "Check system capabilities and configuration"
        );

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold cyan]Quick Start[/]");
        AnsiConsole.MarkupLine("  [grey]1.[/] Run [cyan]vox[/] to start recording");
        AnsiConsole.MarkupLine("  [grey]2.[/] Speak into your microphone");
        AnsiConsole.MarkupLine("  [grey]3.[/] Press [cyan]TAB[/] to toggle between Content/Instruction mode");
        AnsiConsole.MarkupLine("  [grey]4.[/] Press [cyan]ENTER[/] when done");
        AnsiConsole.MarkupLine("  [grey]5.[/] Transcription is printed and copied to clipboard");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold cyan]Modes[/]");
        AnsiConsole.MarkupLine("  [green]Content[/] - Text for final output (default mode)");
        AnsiConsole.MarkupLine("  [yellow]Instruction[/] - Directions for processing the content");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[dim]Configuration: ~/.vox/models/ (model cache)[/]");
        AnsiConsole.MarkupLine("[dim]Config file: appsettings.json[/]");
    }

    private static VoxConfig LoadConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false, false)
            .Build();

        var config = new VoxConfig();
        configuration.Bind(config);
        return config;
    }

    private static int ListDevices()
    {
        try
        {
            var devices = PvRecorder.GetAvailableDevices();

            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .AddColumn(new TableColumn("[bold]Index[/]").Centered())
                .AddColumn(new TableColumn("[bold]Device Name[/]"));

            if (devices.Length == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No audio input devices found.[/]");
                return 0;
            }

            for (var i = 0; i < devices.Length; i++) table.AddRow($"[cyan]{i}[/]", $"[white]{devices[i]}[/]");

            AnsiConsole.Write(
                new Panel(table)
                    .Header("[bold cyan]Available Audio Input Devices[/]")
                    .BorderColor(Color.Cyan1)
            );
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error listing devices: {ex.Message}[/]");
            return 1;
        }

        return 0;
    }

    private static async Task<int> Diagnose(VoxConfig config)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold]Component[/]"))
            .AddColumn(new TableColumn("[bold]Status[/]"))
            .AddColumn(new TableColumn("[bold]Details[/]"));

        // Check PvRecorder
        try
        {
            var devices = PvRecorder.GetAvailableDevices();
            table.AddRow(
                "[cyan]PvRecorder[/]",
                "[green]✓ Available[/]",
                $"{devices.Length} device(s) found"
            );
        }
        catch (Exception ex)
        {
            table.AddRow(
                "[cyan]PvRecorder[/]",
                "[red]✗ Error[/]",
                ex.Message
            );
        }

        // Check clipboard
        try
        {
            await ClipboardService.SetTextAsync("test");
            table.AddRow(
                "[cyan]Clipboard[/]",
                "[green]✓ Available[/]",
                "Copy to clipboard enabled"
            );
        }
        catch
        {
            table.AddRow(
                "[cyan]Clipboard[/]",
                "[yellow]⚠ Warning[/]",
                "Install xsel or xclip"
            );
        }

        // Check model cache
        var modelPath = GetModelPath(config);
        if (File.Exists(modelPath))
        {
            var fileInfo = new FileInfo(modelPath);
            table.AddRow(
                "[cyan]Whisper Model[/]",
                "[green]✓ Cached[/]",
                $"{config.Whisper.ModelSize} ({fileInfo.Length / 1024 / 1024}MB)"
            );
        }
        else
        {
            table.AddRow(
                "[cyan]Whisper Model[/]",
                "[yellow]○ Not Downloaded[/]",
                $"{config.Whisper.ModelSize} (will download on first run)"
            );
        }

        // GPU/CUDA check
        table.AddRow(
            "[cyan]GPU Acceleration[/]",
            "[grey]○ Not Implemented[/]",
            "CPU-only mode"
        );

        AnsiConsole.Write(
            new Panel(table)
                .Header("[bold cyan]vox diagnostics[/]")
                .BorderColor(Color.Cyan1)
        );

        return 0;
    }

    private static void CheckClipboardAvailability()
    {
        // Only check on Linux
        if (!OperatingSystem.IsLinux()) return;

        // Check for xsel or xclip
        var hasXsel = CommandExists("xsel");
        var hasXclip = CommandExists("xclip");

        if (!hasXsel && !hasXclip)
        {
            AnsiConsole.MarkupLine("[yellow]⚠ Warning: Clipboard tools not found[/]");
            AnsiConsole.MarkupLine("[grey]  Install xsel or xclip to enable clipboard copy:[/]");
            AnsiConsole.MarkupLine("[dim]  sudo apt install xsel[/]");
            AnsiConsole.WriteLine();
        }
    }

    private static bool CommandExists(string command)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<int> RunTranscription(VoxConfig config)
    {
        try
        {
            // Check clipboard availability on Linux
            if (config.Clipboard.Enabled) CheckClipboardAvailability();

            // Ensure model is downloaded
            var modelPath = await EnsureModelDownloaded(config);

            // Run the unified transcription loop
            var finalText = await ProcessTranscriptionLoop(config, modelPath);

            if (string.IsNullOrEmpty(finalText))
            {
                return 1;
            }

            // Copy to clipboard
            if (config.Clipboard.Enabled)
            {
                await ClipboardService.SetTextAsync(finalText);
                AnsiConsole.MarkupLine("[green]✓ Copied to clipboard[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {MarkupHelper.Sanitize(ex.Message)}");
            return 1;
        }
    }

    private static (string? instruction, string content) FormatSegmentsAsXml(
        List<LabeledSegment> labeledSegments)
    {
        var contentBuilder = new StringBuilder();
        var instructionBuilder = new StringBuilder();

        foreach (var segment in labeledSegments)
        {
            if (segment.Mode == RecordingMode.Content)
            {
                contentBuilder.AppendLine(segment.Text.Trim());
            }
            else // RecordingMode.Instruction
            {
                instructionBuilder.AppendLine(segment.Text.Trim());
            }
        }

        var content = contentBuilder.ToString().Trim();
        var instruction = instructionBuilder.ToString().Trim();

        return (
            string.IsNullOrEmpty(instruction) ? null : instruction,
            content
        );
    }

    private static async Task<string> ProcessTranscriptionLoop(VoxConfig config, string modelPath)
    {
        // Try to initialize LLM client
        IChatClient? chatClient = null;
        if (config.Llm.Enabled && !string.IsNullOrEmpty(config.Llm.ApiKey))
        {
            try
            {
                var llmConfig = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ApiKey"] = config.Llm.ApiKey,
                        ["Model"] = config.Llm.Model
                    })
                    .Build();

                var provider = new AnthropicProvider();
                if (provider.CanCreate(llmConfig))
                {
                    chatClient = provider.CreateClient(llmConfig);
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]⚠ Failed to initialize LLM: {MarkupHelper.Sanitize(ex.Message)}[/]");
            }
        }

        string currentText = "";
        string systemPrompt = "";
        bool isFirstIteration = true;

        while (true)
        {
            // Prompt for continue/exit (after first iteration)
            if (!isFirstIteration)
            {
                AnsiConsole.MarkupLine("[dim]Press ENTER to revise/append, or ESC to accept and exit.[/]");
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Escape) break;
                if (key.Key != ConsoleKey.Enter) continue;
            }

            // Record audio
            AnsiConsole.MarkupLine("[dim]Press TAB to change input modes. Press ENTER when done speaking.[/]");
            var recordingResult = RecordAudio(config);

            if (recordingResult.AudioData.Length == 0)
            {
                if (isFirstIteration)
                {
                    AnsiConsole.MarkupLine("[red]No audio detected.[/]");
                    return "";
                }
                AnsiConsole.MarkupLine("[yellow]No audio detected, keeping current version.[/]");
                break;
            }

            // Transcribe
            List<TranscriptionSegment> segments = new();
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync("[cyan]Transcribing...[/]",
                    async ctx => { segments = await TranscribeAudio(modelPath, recordingResult.AudioData, config); });

            var labeledSegments = MapSegmentsToModes(segments, recordingResult.ToggleEvents);
            var (instruction, content) = FormatSegmentsAsXml(labeledSegments);

            AnsiConsole.WriteLine();
            foreach (var segment in labeledSegments)
            {
                var modeColor = segment.Mode == RecordingMode.Content ? "green" : "yellow";
                var modeName = MarkupHelper.Sanitize(segment.Mode.ToString());
                var segmentText = MarkupHelper.Sanitize(segment.Text);
                AnsiConsole.MarkupLine($"[{modeColor}]{modeName}:[/] {segmentText}");
            }
            AnsiConsole.WriteLine();
            
            if (isFirstIteration)
            {
                // Build system prompt with initial instruction (if any)
                systemPrompt = BuildSystemPrompt(instruction);
            }

            // Append any content
            if (!string.IsNullOrEmpty(content))
            {
                currentText = string.IsNullOrEmpty(currentText) ? content : currentText + "\n\n" + content;
            }

            // Determine if we should call the LLM
            bool shouldCallLlm = chatClient != null &&
                ((isFirstIteration && !string.IsNullOrEmpty(instruction)) ||
                 (!isFirstIteration && !string.IsNullOrEmpty(instruction)));

            if (shouldCallLlm)
            {
                string? revisionRequest = isFirstIteration ? null : instruction;
                string spinnerText = "[cyan]Processing with LLM...[/]";

                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("cyan"))
                    .StartAsync(spinnerText,
                        async ctx => { currentText = await CallLlm(chatClient!, systemPrompt, currentText, revisionRequest); });
                
                // Show result (only if updated)
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold cyan]AI revision:[/]");
                AnsiConsole.WriteLine(currentText);
                AnsiConsole.WriteLine();
            }

            isFirstIteration = false;

            // If LLM is not available, exit after first iteration
            if (chatClient == null) break;
        }

        return currentText;
    }

    private static string BuildSystemPrompt(string? instruction)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine(
            "You are a transcription refinement assistant. Your job is to refine spoken text into written prose.");
        prompt.AppendLine();
        prompt.AppendLine("CRITICAL GUIDELINES:");
        prompt.AppendLine("- You are generating text AS the user, not FOR the user");
        prompt.AppendLine("- Preserve the user's voice, tone, and style");
        prompt.AppendLine("- Do NOT transform into corporate-speak or 'AI slop'");
        prompt.AppendLine("- Only apply requested changes, don't over-edit");
        prompt.AppendLine("- Remove filler words (um, uh, like) unless they add meaning");
        prompt.AppendLine("- Fix grammar and punctuation for written form");
        prompt.AppendLine();

        if (!string.IsNullOrEmpty(instruction))
        {
            prompt.AppendLine("INSTRUCTIONS FROM USER:");
            prompt.AppendLine(instruction);
            prompt.AppendLine();
        }

        prompt.AppendLine("When revising, apply ONLY the requested changes. Return only the refined text, no explanations.");

        return prompt.ToString();
    }

    private static async Task<string> CallLlm(IChatClient client, string systemPrompt, string content,
        string? revisionRequest)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt)
        };

        if (string.IsNullOrEmpty(revisionRequest))
        {
            // Initial processing
            messages.Add(new ChatMessage(ChatRole.User, $"Refine this spoken text:\n\n{content}"));
        }
        else
        {
            // Revision
            messages.Add(new ChatMessage(ChatRole.User, $"Current text:\n\n{content}"));
            messages.Add(new ChatMessage(ChatRole.User, $"Revision request: {revisionRequest}"));
        }

        var response = await client.GetResponseAsync(messages);

        // Extract text from the first message in the response
        var firstMessage = response.Messages.FirstOrDefault();
        return firstMessage?.Text ?? content;
    }

    private static string GetModelPath(VoxConfig config)
    {
        // If explicit path is set, use it
        if (!string.IsNullOrEmpty(config.Whisper.ModelPath)) return config.Whisper.ModelPath;

        // Otherwise use cache directory
        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".vox",
            "models"
        );

        Directory.CreateDirectory(cacheDir);
        return Path.Combine(cacheDir, $"ggml-{config.Whisper.ModelSize}.bin");
    }

    private static async Task<string> EnsureModelDownloaded(VoxConfig config)
    {
        var modelPath = GetModelPath(config);

        if (File.Exists(modelPath)) return modelPath;

        // First-run experience
        var modelType = GetGgmlType(config.Whisper.ModelSize);
        var modelSizeMb = GetModelSizeMb(config.Whisper.ModelSize);

        AnsiConsole.MarkupLine(
            $"[cyan]First time setup:[/] downloading whisper model ([bold]{config.Whisper.ModelSize}[/], ~{modelSizeMb}MB)...");
        AnsiConsole.WriteLine();

        try
        {
            using var httpClient = new HttpClient();
            var downloader = new WhisperGgmlDownloader(httpClient);
            using var modelStream = await downloader.GetGgmlModelAsync(modelType);
            using var fileWriter = File.OpenWrite(modelPath);

            await AnsiConsole.Progress()
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new DownloadedColumn(),
                    new TransferSpeedColumn()
                )
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask($"[cyan]Downloading {config.Whisper.ModelSize} model[/]",
                        maxValue: modelSizeMb * 1024 * 1024);

                    var buffer = new byte[81920];
                    long totalBytes = 0;
                    int bytesRead;

                    while ((bytesRead = await modelStream.ReadAsync(buffer)) > 0)
                    {
                        await fileWriter.WriteAsync(buffer.AsMemory(0, bytesRead));
                        totalBytes += bytesRead;
                        task.Value = totalBytes;
                    }

                    task.Value = task.MaxValue;
                });

            AnsiConsole.MarkupLine("[green]✓ Setup complete![/] Ready to transcribe.");
            AnsiConsole.WriteLine();
        }
        catch (Exception ex)
        {
            // Clean up partial download
            if (File.Exists(modelPath)) File.Delete(modelPath);
            throw new Exception($"Failed to download model: {ex.Message}", ex);
        }

        return modelPath;
    }

    private static GgmlType GetGgmlType(string modelSize)
    {
        return modelSize.ToLower() switch
        {
            "tiny" => GgmlType.Tiny,
            "tiny.en" => GgmlType.TinyEn,
            "base" => GgmlType.Base,
            "base.en" => GgmlType.BaseEn,
            "small" => GgmlType.Small,
            "small.en" => GgmlType.SmallEn,
            "medium" => GgmlType.Medium,
            "medium.en" => GgmlType.MediumEn,
            "large" => GgmlType.LargeV3,
            _ => GgmlType.Small
        };
    }

    private static int GetModelSizeMb(string modelSize)
    {
        return modelSize.ToLower() switch
        {
            "tiny" or "tiny.en" => 75,
            "base" or "base.en" => 140,
            "small" or "small.en" => 466,
            "medium" or "medium.en" => 1450,
            "large" => 2950,
            _ => 466
        };
    }

    private static string GetModeDescription(RecordingMode mode)
    {
        return mode switch
        {
            RecordingMode.Content => "text for final output",
            RecordingMode.Instruction => "directions for processing",
            _ => ""
        };
    }

    private static RecordingResult RecordAudio(VoxConfig config)
    {
        var audioBuffer = new List<short>();
        var toggleEvents = new List<ModeToggleEvent>();
        var currentMode = RecordingMode.Content;
        var startTime = DateTime.UtcNow;
        var recording = true;

        using var recorder = PvRecorder.Create(
            deviceIndex: -1, // -1 = default device
            frameLength: 512
        );

        recorder.Start();

        // Log initial mode
        var logMode = (RecordingMode mode, TimeSpan elapsed) =>
        {
            var color = mode == RecordingMode.Content ? "green" : "yellow";
            var desc = GetModeDescription(mode);
            var modeType = mode == RecordingMode.Content ? "content" : "instructions";
            AnsiConsole.MarkupLine($"[gray][[{elapsed:mm\\:ss}]][/] [{color}]Listening for {modeType}[/][gray] ({desc})[/]");
        };

        logMode(currentMode, TimeSpan.Zero);

        // Record with keyboard input handling
        while (recording)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Enter)
                {
                    recording = false;
                    break;
                }
                else if (key.Key == ConsoleKey.Tab)
                {
                    // Toggle mode
                    var elapsed = DateTime.UtcNow - startTime;
                    currentMode = currentMode == RecordingMode.Content
                        ? RecordingMode.Instruction
                        : RecordingMode.Content;

                    toggleEvents.Add(new ModeToggleEvent
                    {
                        Timestamp = elapsed,
                        Mode = currentMode
                    });

                    // Log mode change
                    logMode(currentMode, elapsed);
                }
            }

            var frame = recorder.Read();
            audioBuffer.AddRange(frame);
        }

        recorder.Stop();

        var totalDuration = DateTime.UtcNow - startTime;

        return new RecordingResult
        {
            AudioData = audioBuffer.ToArray(),
            ToggleEvents = toggleEvents,
            TotalDuration = totalDuration
        };
    }

    private static async Task<List<TranscriptionSegment>> TranscribeAudio(string modelPath, short[] audioData, VoxConfig config)
    {
        try
        {
            // Convert short[] to float[] (whisper.net expects float samples)
            var floatSamples = audioData.Select(s => s / 32768f).ToArray();

            using var whisperFactory = WhisperFactory.FromPath(modelPath);
            using var processor = whisperFactory.CreateBuilder()
                .WithLanguage(config.Whisper.Language)
                .Build();

            var segments = new List<TranscriptionSegment>();

            await foreach (var result in processor.ProcessAsync(floatSamples))
            {
                segments.Add(new TranscriptionSegment
                {
                    Start = result.Start,
                    End = result.End,
                    Text = result.Text
                });
            }

            return segments;
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Transcription failed: {ex.Message}\nModel path: {modelPath}\nInner: {ex.InnerException?.Message}",
                ex);
        }
    }

    private static List<LabeledSegment> MapSegmentsToModes(
        List<TranscriptionSegment> segments,
        List<ModeToggleEvent> toggleEvents)
    {
        // Build mode timeline: starts with Content at 0:00, then add all toggle events
        var modeTimeline = new List<ModeToggleEvent>
        {
            new() { Timestamp = TimeSpan.Zero, Mode = RecordingMode.Content }
        };
        modeTimeline.AddRange(toggleEvents);

        // Label each segment with its mode
        var labeledSegments = new List<LabeledSegment>();

        foreach (var segment in segments)
        {
            // Find the active mode at segment's midpoint
            var segmentMidpoint = segment.Start + TimeSpan.FromTicks((segment.End - segment.Start).Ticks / 2);

            // Find the last toggle before or at the midpoint
            var activeMode = RecordingMode.Content;
            foreach (var toggle in modeTimeline)
            {
                if (toggle.Timestamp <= segmentMidpoint)
                {
                    activeMode = toggle.Mode;
                }
                else
                {
                    break; // Toggles are chronological
                }
            }

            labeledSegments.Add(new LabeledSegment
            {
                Text = segment.Text,
                Mode = activeMode,
                Start = segment.Start,
                End = segment.End
            });
        }

        // Merge consecutive segments with same mode
        var mergedSegments = new List<LabeledSegment>();
        LabeledSegment? current = null;

        foreach (var segment in labeledSegments)
        {
            if (current == null || current.Mode != segment.Mode)
            {
                if (current != null)
                {
                    mergedSegments.Add(current);
                }
                current = new LabeledSegment
                {
                    Text = segment.Text,
                    Mode = segment.Mode,
                    Start = segment.Start,
                    End = segment.End
                };
            }
            else
            {
                // Merge with current segment
                current.Text += " " + segment.Text;
                current.End = segment.End;
            }
        }

        if (current != null)
        {
            mergedSegments.Add(current);
        }

        return mergedSegments;
    }
}