using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;
using Application = FlaUI.Core.Application;

namespace VisualDiffLab.UITests;

public class PerformanceUiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly ITestOutputHelper _output;

    public PerformanceUiTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void FourK_Workflow_Performance_IsRecorded()
    {
        string root = TestPaths.RepositoryRoot();
        string exe = TestPaths.AppExecutable(root);
        string logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VisualDiffLab",
            "logs",
            "app.log");
        long logStart = File.Exists(logPath) ? new FileInfo(logPath).Length : 0;

        TestPaths.KillRunningInstances();
        Stopwatch startup = Stopwatch.StartNew();
        using Application app = Application.Launch(new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"--a \"{TestPaths.Image(root, "dashboard-4k-before.png")}\" --b \"{TestPaths.Image(root, "dashboard-4k-after.png")}\" --lang en-US",
            UseShellExecute = false,
        });

        List<double> modeSwitchMs = [];
        List<double> thresholdMs = [];

        try
        {
            using UIA3Automation automation = new();
            Window window = app.GetMainWindow(automation, TimeSpan.FromSeconds(30))!;
            window.SetForeground();
            TestPaths.SetTopMost(window, true);
            TestPaths.ForceForeground(window);

            string different = UiActions.WaitForText(
                window,
                "StatDifferentValue",
                text => text != "—" && text != "Comparing…",
                TimeSpan.FromSeconds(60));
            startup.Stop();
            _output.WriteLine($"process start to first 4K diff visible: {startup.Elapsed.TotalMilliseconds:F0} ms ({different} px)");

            using Process appProcess = Process.GetProcessById(app.ProcessId);
            appProcess.Refresh();
            long workingSetMb = appProcess.WorkingSet64 / (1024 * 1024);
            _output.WriteLine($"working set after 4K load + heatmap: {workingSetMb} MB");

            // Mode switches, five samples (first frame timing is logged by the app).
            string[] modes = ["ModeWipe", "ModeOverlay", "ModeHeatmap", "ModeSideBySide", "ModeWipe", "ModeOverlay"];
            foreach (string mode in modes)
            {
                UiActions.SelectMode(window, mode);
                Thread.Sleep(500);
            }

            // Threshold recompute, five samples (120 ms debounce + full 4K diff).
            string expectedHigh = "—";
            for (int i = 0; i < 5; i++)
            {
                Stopwatch thresholdWatch = Stopwatch.StartNew();
                UiActions.SetSlider(window, "ThresholdSlider", 64);
                string high = UiActions.WaitForText(
                    window,
                    "StatDifferentValue",
                    text => text != "—" && text != "Comparing…" && text != different,
                    TimeSpan.FromSeconds(30));
                thresholdWatch.Stop();
                thresholdMs.Add(thresholdWatch.Elapsed.TotalMilliseconds);
                expectedHigh = high;

                thresholdWatch.Restart();
                UiActions.SetSlider(window, "ThresholdSlider", 16);
                UiActions.WaitForText(
                    window,
                    "StatDifferentValue",
                    text => ParseCount(text) == ParseCount(different),
                    TimeSpan.FromSeconds(30));
                thresholdWatch.Stop();
                thresholdMs.Add(thresholdWatch.Elapsed.TotalMilliseconds);
            }

            _output.WriteLine($"threshold high value: {expectedHigh}");
            _output.WriteLine($"threshold compute wall clock ms: {string.Join(", ", thresholdMs.Select(value => value.ToString("F0", CultureInfo.InvariantCulture)))}");

            Thread.Sleep(500);
            appProcess.Refresh();
            workingSetMb = appProcess.WorkingSet64 / (1024 * 1024);
            _output.WriteLine($"working set after 4K workflows: {workingSetMb} MB");

            UiActions.SelectMode(window, "ModeHeatmap");
            UiActions.Invoke(window, "FitButton");
            TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-4-high-dpi-4k-heatmap.png");
            UiActions.SelectMode(window, "ModeSideBySide");
            UiActions.Invoke(window, "FitButton");
            TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-4-performance-4k-side-by-side.png");

            // Read the diagnostics log to get the in-app first-frame timings and compute times.
            string logText = File.Exists(logPath) ? File.ReadAllText(logPath) : string.Empty;
            if (logStart > 0 && logStart <= logText.Length)
            {
                logText = logText[(int)logStart..];
            }

            List<double> appModeMs = [];
            List<double> appDiffMs = [];
            List<double> appDecodeMs = [];
            foreach (string line in logText.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("{\"timestamp", StringComparison.Ordinal) == false)
                {
                    continue;
                }

                try
                {
                    using JsonDocument document = JsonDocument.Parse(line.Trim());
                    string message = document.RootElement.GetProperty("message").GetString() ?? string.Empty;
                    if (message.StartsWith("Mode first frame:", StringComparison.Ordinal))
                    {
                        appModeMs.Add(ExtractDuration(message));
                    }
                    else if (message.StartsWith("Comparison:", StringComparison.Ordinal))
                    {
                        appDiffMs.Add(ExtractDuration(message));
                    }
                    else if (message.StartsWith("Image decoded:", StringComparison.Ordinal))
                    {
                        appDecodeMs.Add(ExtractDuration(message));
                    }
                }
                catch (JsonException)
                {
                }
            }

            _output.WriteLine($"in-app mode first frame ms: {string.Join(", ", appModeMs.Select(value => value.ToString("F1", CultureInfo.InvariantCulture)))}");
            _output.WriteLine($"in-app 4K diff+heatmap ms: {string.Join(", ", appDiffMs.Select(value => value.ToString("F1", CultureInfo.InvariantCulture)))}");
            _output.WriteLine($"in-app 4K decode ms: {string.Join(", ", appDecodeMs.Select(value => value.ToString("F1", CultureInfo.InvariantCulture)))}");

            Assert.NotEmpty(appModeMs);
            Assert.NotEmpty(appDiffMs);
            double modeMax = appModeMs.Max();
            double diffMax = appDiffMs.Max();
            Assert.True(modeMax <= 100, $"Mode switch first frame exceeded 100 ms: {modeMax:F1} ms");
            Assert.True(diffMax <= 1500, $"4K diff exceeded 1500 ms: {diffMax:F1} ms");
            Assert.True(workingSetMb <= 500, $"Working set exceeded 500 MB: {workingSetMb} MB");

            WriteReport(
                root,
                startup.Elapsed.TotalMilliseconds,
                workingSetMb,
                appModeMs,
                appDiffMs,
                appDecodeMs,
                thresholdMs);
        }
        finally
        {
            try
            {
                app.Close();
                if (!app.HasExited)
                {
                    app.Kill();
                }
            }
            catch (Exception)
            {
                TestPaths.KillRunningInstances();
            }

            TestPaths.KillRunningInstances();
        }
    }

    private static void WriteReport(
        string root,
        double startupMs,
        long workingSetMb,
        List<double> modeMs,
        List<double> diffMs,
        List<double> decodeMs,
        List<double> thresholdMs)
    {
        string directory = Path.Combine(root, "artifacts", "performance");
        Directory.CreateDirectory(directory);
        string markdown = $"""
            # UI performance (4K)

            - Timestamp: {DateTimeOffset.Now:O}
            - OS: {Environment.OSVersion.VersionString}
            - Runtime: {Environment.Version}
            - Logical processors: {Environment.ProcessorCount}
            - Images: dashboard-4k-before.png / dashboard-4k-after.png (3840x2160 PNG)
            - Measurement: UI Automation driving the real Release/Debug app; app log timestamps for in-app work.
            - Process start to first 4K difference visible: {startupMs:F0} ms
            - Working set after 4K workflows: {workingSetMb} MB (target <= 500 MB)
            - Mode switch first frame (ms): {string.Join(", ", modeMs.Select(v => v.ToString("F1", CultureInfo.InvariantCulture)))}
            - Mode switch median/max: {Median(modeMs):F1} / {modeMs.Max():F1} ms (target <= 100 ms)
            - Diff + heatmap (ms): {string.Join(", ", diffMs.Select(v => v.ToString("F1", CultureInfo.InvariantCulture)))}
            - Diff median/max: {Median(diffMs):F1} / {diffMs.Max():F1} ms (target <= 1500 ms)
            - Decode A/B (ms): {string.Join(", ", decodeMs.Select(v => v.ToString("F1", CultureInfo.InvariantCulture)))}
            - Threshold change wall clock incl. 120 ms debounce (ms): {string.Join(", ", thresholdMs.Select(v => v.ToString("F0", CultureInfo.InvariantCulture)))}
            - Threshold median/max: {Median(thresholdMs):F0} / {thresholdMs.Max():F0} ms

            Screenshots: `artifacts/acceptance/phase-4-high-dpi-4k-heatmap.png`,
            `artifacts/acceptance/phase-4-performance-4k-side-by-side.png`.
            """;
        File.WriteAllText(Path.Combine(directory, "ui-performance.md"), markdown);
        File.WriteAllText(
            Path.Combine(directory, "ui-performance.json"),
            JsonSerializer.Serialize(new
            {
                startupMs,
                workingSetMb,
                modeMs,
                diffMs,
                decodeMs,
                thresholdMs,
                modeMedian = Median(modeMs),
                modeMax = modeMs.Max(),
                diffMedian = Median(diffMs),
                diffMax = diffMs.Max(),
            }, JsonOptions));
    }

    private static double Median(List<double> values)
    {
        double[] sorted = values.OrderBy(value => value).ToArray();
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle];
    }

    private static double ExtractDuration(string message)
    {
        Match match = Regex.Match(message, @"durationMs=([0-9.]+)", RegexOptions.CultureInvariant);
        return match.Success ? double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
    }

    private static int ParseCount(string text) =>
        int.TryParse(text.Replace(",", string.Empty).Replace("%", string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : 0;
}
