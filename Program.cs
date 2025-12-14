using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;
using Pv;
using Spectre.Console;
using TextCopy;
using Whisper.net;
using Whisper.net.Ggml;

namespace Vox;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
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
        AnsiConsole.MarkupLine("  [grey]3.[/] Press [cyan]ENTER[/] when done");
        AnsiConsole.MarkupLine("  [grey]4.[/] Transcription is printed and copied to clipboard");
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

            // Record audio
            AnsiConsole.MarkupLine("[cyan]Listening[/] on default audio device...");
            AnsiConsole.MarkupLine("[dim]Press ENTER when you're done speaking.[/]");

            var audioData = RecordAudio(config);

            if (audioData.Length == 0)
            {
                AnsiConsole.MarkupLine("[red]No audio detected.[/]");
                return 1;
            }

            // Transcribe
            var transcription = "";
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync("[cyan]Transcribing...[/]",
                    async ctx => { transcription = await TranscribeAudio(modelPath, audioData, config); });

            // Output results
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold cyan]Transcription:[/]");
            AnsiConsole.WriteLine(transcription);
            AnsiConsole.WriteLine();

            // Copy to clipboard
            if (config.Clipboard.Enabled)
            {
                await ClipboardService.SetTextAsync(transcription);
                AnsiConsole.MarkupLine("[green]✓ Copied to clipboard[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
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

    private static short[] RecordAudio(VoxConfig config)
    {
        var audioBuffer = new List<short>();

        using var recorder = PvRecorder.Create(
            deviceIndex: -1, // -1 = default device
            frameLength: 512
        );

        recorder.Start();

        // Record until user presses Enter
        var recordingTask = Task.Run(() =>
        {
            while (!Console.KeyAvailable || Console.ReadKey(true).Key != ConsoleKey.Enter)
            {
                var frame = recorder.Read();
                audioBuffer.AddRange(frame);
            }
        });

        recordingTask.Wait();
        recorder.Stop();

        return audioBuffer.ToArray();
    }

    private static async Task<string> TranscribeAudio(string modelPath, short[] audioData, VoxConfig config)
    {
        try
        {
            // Convert short[] to float[] (whisper.net expects float samples)
            var floatSamples = audioData.Select(s => s / 32768f).ToArray();

            using var whisperFactory = WhisperFactory.FromPath(modelPath);
            using var processor = whisperFactory.CreateBuilder()
                .WithLanguage(config.Whisper.Language)
                .Build();

            var transcription = new StringBuilder();

            await foreach (var result in processor.ProcessAsync(floatSamples)) transcription.Append(result.Text);

            return transcription.ToString().Trim();
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Transcription failed: {ex.Message}\nModel path: {modelPath}\nInner: {ex.InnerException?.Message}",
                ex);
        }
    }
}