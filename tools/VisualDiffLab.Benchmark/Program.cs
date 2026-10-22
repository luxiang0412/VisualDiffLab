using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using VisualDiffLab.Core.Comparison;
using VisualDiffLab.Core.Export;
using VisualDiffLab.Core.Models;
using VisualDiffLab.Imaging;

namespace VisualDiffLab.Benchmark;

/// <summary>
/// Reproducible warm-cache benchmark for the CPU-bound pipeline. Run from the repository
/// root after building Release:
///   dotnet run -c Release --project tools/VisualDiffLab.Benchmark
/// </summary>
internal static class Program
{
    private const int Iterations = 5;

    private static async Task<int> Main(string[] args)
    {
        string root = FindRepositoryRoot();
        string aPath = args.Length > 0 ? args[0] : Path.Combine(root, "assets", "test-images", "dashboard-4k-before.png");
        string bPath = args.Length > 1 ? args[1] : Path.Combine(root, "assets", "test-images", "dashboard-4k-after.png");
        if (!File.Exists(aPath) || !File.Exists(bPath))
        {
            Console.Error.WriteLine("Benchmark images were not found. Run scripts/generate-test-images.py first.");
            return 2;
        }

        Console.WriteLine($"A: {aPath}");
        Console.WriteLine($"B: {bPath}");

        await WarmUpAsync(aPath, bPath);

        List<IterationResult> results = [];
        for (int i = 0; i < Iterations; i++)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            ImageDocument a = await ImageLoader.LoadAsync(aPath);
            ImageDocument b = await ImageLoader.LoadAsync(bPath);
            double decodeMs = stopwatch.Elapsed.TotalMilliseconds;

            stopwatch.Restart();
            DiffResult diff = DiffEngine.Compare(a, b, DiffOptions.Default);
            byte[] overlay = DifferenceOverlayRenderer.BuildHeatmapOverlay(diff);
            double diffWithOverlayMs = stopwatch.Elapsed.TotalMilliseconds;

            stopwatch.Restart();
            byte[] exportOverlay = DifferenceOverlayRenderer.ComposeExport(diff, a);
            double exportOverlayMs = stopwatch.Elapsed.TotalMilliseconds;

            stopwatch.Restart();
            byte[] sideBySide = SideBySideRenderer.Compose(a, b);
            double sideBySideMs = stopwatch.Elapsed.TotalMilliseconds;

            (int sideWidth, int sideHeight) = SideBySideRenderer.GetOutputSize(a, b);
            IterationResult result = new(
                i + 1,
                a.Width,
                a.Height,
                b.Width,
                b.Height,
                decodeMs,
                diffWithOverlayMs,
                exportOverlayMs,
                sideBySideMs,
                diff.DifferentPixelCount,
                diff.ComparedPixelCount,
                overlay.LongLength,
                exportOverlay.LongLength,
                sideBySide.LongLength,
                sideWidth,
                sideHeight);
            results.Add(result);
            Console.WriteLine(
                $"#{result.Iteration}: decode={result.DecodeMs:F1}ms diff+heatmap={result.DiffMs:F1}ms " +
                $"exportOverlay={result.ExportOverlayMs:F1}ms sideBySide={result.SideBySideMs:F1}ms " +
                $"different={result.DifferentPixels:N0}");
        }

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);

        string outputDirectory = Path.Combine(root, "artifacts", "performance");
        Directory.CreateDirectory(outputDirectory);
        Summary summary = new(
            DateTimeOffset.Now.ToString("O"),
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString(),
            RuntimeInformation.ProcessArchitecture.ToString(),
            RuntimeInformation.FrameworkDescription,
            Environment.ProcessorCount,
            Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "unknown",
            Path.GetFileName(aPath),
            Path.GetFileName(bPath),
            Iterations,
            results,
            Median(results.Select(r => r.DecodeMs)),
            results.Max(r => r.DecodeMs),
            Median(results.Select(r => r.DiffMs)),
            results.Max(r => r.DiffMs),
            Median(results.Select(r => r.ExportOverlayMs)),
            results.Max(r => r.ExportOverlayMs),
            Median(results.Select(r => r.SideBySideMs)),
            results.Max(r => r.SideBySideMs),
            GC.GetTotalMemory(forceFullCollection: true));

        string jsonPath = Path.Combine(outputDirectory, "benchmark.json");
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
        string markdownPath = Path.Combine(outputDirectory, "benchmark.md");
        await File.WriteAllTextAsync(markdownPath, BuildMarkdown(summary));
        Console.WriteLine($"Wrote {jsonPath}");
        Console.WriteLine($"Wrote {markdownPath}");
        return 0;
    }

    private static string BuildMarkdown(Summary summary)
    {
        StringBuilder builder = new();
        builder.AppendLine("# Visual Diff Lab benchmark");
        builder.AppendLine();
        builder.AppendLine($"- Timestamp: {summary.Timestamp}");
        builder.AppendLine($"- OS: {summary.OsDescription} ({summary.OsArchitecture})");
        builder.AppendLine($"- Runtime: {summary.FrameworkDescription}");
        builder.AppendLine($"- Logical processors: {summary.LogicalProcessors}");
        builder.AppendLine($"- Processor: {summary.Processor}");
        builder.AppendLine($"- Images: {Path.GetFileName(summary.ImageAPath)} / {Path.GetFileName(summary.ImageBPath)}");
        builder.AppendLine($"- Iterations: {summary.Iterations} (warm file cache)");
        builder.AppendLine($"- Managed heap after GC: {summary.ManagedHeapBytes / (1024.0 * 1024.0):F0} MB");
        builder.AppendLine("- The product working-set target is measured in the real UI by tests/VisualDiffLab.UITests/PerformanceUiTests (see artifacts/performance/ui-performance.md).");
        builder.AppendLine();
        builder.AppendLine("| # | Decode A+B (ms) | Diff + heatmap (ms) | Diff export compose (ms) | Side-by-side compose (ms) | Different px |");
        builder.AppendLine("|---:|---:|---:|---:|---:|---:|");
        foreach (IterationResult result in summary.Results)
        {
            builder.AppendLine(
                $"| {result.Iteration} | {result.DecodeMs:F1} | {result.DiffMs:F1} | {result.ExportOverlayMs:F1} | {result.SideBySideMs:F1} | {result.DifferentPixels:N0} |");
        }

        builder.AppendLine();
        builder.AppendLine("| Metric | Median (ms) | Max (ms) | Target |");
        builder.AppendLine("|---|---:|---:|---:|");
        builder.AppendLine($"| Decode A+B (4K) | {summary.DecodeMedianMs:F1} | {summary.DecodeMaxMs:F1} | ≤ 2500 (load + compare) |");
        builder.AppendLine($"| Diff + heatmap overlay | {summary.DiffMedianMs:F1} | {summary.DiffMaxMs:F1} | ≤ 1500 |");
        builder.AppendLine($"| Difference export compose | {summary.ExportOverlayMedianMs:F1} | {summary.ExportOverlayMaxMs:F1} | no target (informational) |");
        builder.AppendLine($"| Side-by-side compose | {summary.SideBySideMedianMs:F1} | {summary.SideBySideMaxMs:F1} | no target (informational) |");
        return builder.ToString();
    }

    private static async Task WarmUpAsync(string aPath, string bPath)
    {
        ImageDocument warmA = await ImageLoader.LoadAsync(aPath);
        ImageDocument warmB = await ImageLoader.LoadAsync(bPath);
        DiffResult warmDiff = DiffEngine.Compare(warmA, warmB, DiffOptions.Default);
        _ = DifferenceOverlayRenderer.BuildHeatmapOverlay(warmDiff);
        _ = DifferenceOverlayRenderer.ComposeExport(warmDiff, warmA);
        _ = SideBySideRenderer.Compose(warmA, warmB);
    }

    private static double Median(IEnumerable<double> values)
    {
        double[] sorted = values.OrderBy(value => value).ToArray();
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle];
    }

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "VisualDiffLab.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        return Directory.GetCurrentDirectory();
    }

    private sealed record IterationResult(
        int Iteration,
        int AWidth,
        int AHeight,
        int BWidth,
        int BHeight,
        double DecodeMs,
        double DiffMs,
        double ExportOverlayMs,
        double SideBySideMs,
        long DifferentPixels,
        long ComparedPixels,
        long OverlayBytes,
        long ExportOverlayBytes,
        long SideBySideBytes,
        int SideBySideWidth,
        int SideBySideHeight);

    private sealed record Summary(
        string Timestamp,
        string OsDescription,
        string OsArchitecture,
        string ProcessArchitecture,
        string FrameworkDescription,
        int LogicalProcessors,
        string Processor,
        string ImageAPath,
        string ImageBPath,
        int Iterations,
        IReadOnlyList<IterationResult> Results,
        double DecodeMedianMs,
        double DecodeMaxMs,
        double DiffMedianMs,
        double DiffMaxMs,
        double ExportOverlayMedianMs,
        double ExportOverlayMaxMs,
        double SideBySideMedianMs,
        double SideBySideMaxMs,
        long ManagedHeapBytes);
}
