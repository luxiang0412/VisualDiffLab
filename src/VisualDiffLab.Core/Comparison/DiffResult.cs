namespace VisualDiffLab.Core.Comparison;

/// <summary>
/// The single source of truth for statistics, heatmap rendering and exports.
/// </summary>
public sealed class DiffResult
{
    public DiffResult(
        int canvasWidth,
        int canvasHeight,
        int threshold,
        long version,
        byte[] classificationMask,
        long comparedPixelCount,
        long differentPixelCount,
        long ignoredPixelCount,
        long addedPixelCount,
        long removedPixelCount)
    {
        ArgumentNullException.ThrowIfNull(classificationMask);
        long expected = (long)canvasWidth * canvasHeight;
        if (canvasWidth < 0 || canvasHeight < 0 || classificationMask.LongLength != expected)
        {
            throw new ArgumentException("Classification mask does not match the canvas dimensions.", nameof(classificationMask));
        }

        CanvasWidth = canvasWidth;
        CanvasHeight = canvasHeight;
        Threshold = threshold;
        Version = version;
        ClassificationMask = classificationMask;
        ComparedPixelCount = comparedPixelCount;
        DifferentPixelCount = differentPixelCount;
        IgnoredPixelCount = ignoredPixelCount;
        AddedPixelCount = addedPixelCount;
        RemovedPixelCount = removedPixelCount;
    }

    public int CanvasWidth { get; }

    public int CanvasHeight { get; }

    public int Threshold { get; }

    /// <summary>Monotonic session version this result belongs to. Stale results must be discarded.</summary>
    public long Version { get; }

    /// <summary>One <see cref="DiffClassification"/> byte per canvas pixel, row-major.</summary>
    public byte[] ClassificationMask { get; }

    /// <summary>Canvas pixels that participate in the comparison (not ignored).</summary>
    public long ComparedPixelCount { get; }

    public long DifferentPixelCount { get; }

    public long IgnoredPixelCount { get; }

    /// <summary>Pixels present only in B (image A is smaller).</summary>
    public long AddedPixelCount { get; }

    /// <summary>Pixels present only in A (image B is smaller).</summary>
    public long RemovedPixelCount { get; }

    /// <summary>0..1, or null when every canvas pixel was ignored (never divide by zero).</summary>
    public double? DifferenceRatio =>
        ComparedPixelCount == 0 ? null : (double)DifferentPixelCount / ComparedPixelCount;

    public static DiffResult Empty(long version = 0) =>
        new(0, 0, DiffOptions.DefaultThreshold, version, Array.Empty<byte>(), 0, 0, 0, 0, 0);
}
