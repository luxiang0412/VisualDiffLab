using VisualDiffLab.Core.IgnoreRegions;
using VisualDiffLab.Core.Models;

namespace VisualDiffLab.Core.Comparison;

/// <summary>
/// Deterministic per-pixel comparison. Pixels are compared in the normalized
/// comparison-canvas space:
/// <list type="bullet">
/// <item>canvas = max(widthA, widthB) x max(heightA, heightB);</item>
/// <item>both images are aligned at the canvas origin (0,0), never scaled or shifted;</item>
/// <item>a pixel present in only one image counts as different (added/removed);</item>
/// <item>pixels covered by the union of ignore regions are excluded from all statistics.</item>
/// </list>
/// Image pixels are BGRA8 premultiplied sRGB, so fully transparent pixels have zeroed
/// RGB and cannot create false differences from hidden color data.
/// </summary>
public static class DiffEngine
{
    public const long MaxCanvasPixelCount = 100_000_000;

    public static DiffResult Compare(
        ImageDocument imageA,
        ImageDocument imageB,
        DiffOptions options,
        long version = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageA);
        ArgumentNullException.ThrowIfNull(imageB);
        ArgumentNullException.ThrowIfNull(options);

        int canvasWidth = Math.Max(imageA.Width, imageB.Width);
        int canvasHeight = Math.Max(imageA.Height, imageB.Height);
        long canvasPixels = (long)canvasWidth * canvasHeight;
        if (canvasPixels > MaxCanvasPixelCount)
        {
            throw new InvalidOperationException(
                $"Comparison canvas is {canvasWidth}x{canvasHeight} ({canvasPixels:N0} pixels), " +
                $"which exceeds the limit of {MaxCanvasPixelCount:N0} pixels.");
        }

        byte[] mask = new byte[canvasPixels];
        byte[]? ignored = BuildIgnoreMask(options.IgnoreRegions, canvasWidth, canvasHeight);

        ReadOnlySpan<byte> pixelsA = imageA.Pixels;
        ReadOnlySpan<byte> pixelsB = imageB.Pixels;
        int strideA = imageA.Stride;
        int strideB = imageB.Stride;
        int threshold = options.Threshold;

        long compared = 0;
        long different = 0;
        long ignoredCount = 0;
        long added = 0;
        long removed = 0;

        for (int y = 0; y < canvasHeight; y++)
        {
            if ((y & 0x1F) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            bool rowInA = y < imageA.Height;
            bool rowInB = y < imageB.Height;
            int rowOffsetA = y * strideA;
            int rowOffsetB = y * strideB;
            int maskRow = y * canvasWidth;

            for (int x = 0; x < canvasWidth; x++)
            {
                int maskIndex = maskRow + x;

                if (ignored is not null && ignored[maskIndex] != 0)
                {
                    mask[maskIndex] = (byte)DiffClassification.Ignored;
                    ignoredCount++;
                    continue;
                }

                bool hasA = rowInA && x < imageA.Width;
                bool hasB = rowInB && x < imageB.Width;
                compared++;

                if (hasA && hasB)
                {
                    int indexA = rowOffsetA + (x * 4);
                    int indexB = rowOffsetB + (x * 4);

                    int db = Math.Abs(pixelsA[indexA] - pixelsB[indexB]);
                    int dg = Math.Abs(pixelsA[indexA + 1] - pixelsB[indexB + 1]);
                    int dr = Math.Abs(pixelsA[indexA + 2] - pixelsB[indexB + 2]);
                    int da = Math.Abs(pixelsA[indexA + 3] - pixelsB[indexB + 3]);

                    int maxDelta = Math.Max(Math.Max(db, dg), Math.Max(dr, da));
                    if (maxDelta > threshold)
                    {
                        mask[maskIndex] = (byte)DiffClassification.Changed;
                        different++;
                    }
                    else
                    {
                        mask[maskIndex] = (byte)DiffClassification.Same;
                    }
                }
                else if (hasA)
                {
                    mask[maskIndex] = (byte)DiffClassification.OnlyInA;
                    different++;
                    removed++;
                }
                else if (hasB)
                {
                    mask[maskIndex] = (byte)DiffClassification.OnlyInB;
                    different++;
                    added++;
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        return new DiffResult(
            canvasWidth,
            canvasHeight,
            threshold,
            version,
            mask,
            compared,
            different,
            ignoredCount,
            added,
            removed);
    }

    private static byte[]? BuildIgnoreMask(
        IReadOnlyList<IgnoreRegion> regions,
        int canvasWidth,
        int canvasHeight)
    {
        if (regions.Count == 0)
        {
            return null;
        }

        byte[] ignored = new byte[(long)canvasWidth * canvasHeight];
        foreach (IgnoreRegion region in regions)
        {
            int left = Math.Clamp(region.X, 0, canvasWidth);
            int top = Math.Clamp(region.Y, 0, canvasHeight);
            int right = Math.Clamp(region.Right, 0, canvasWidth);
            int bottom = Math.Clamp(region.Bottom, 0, canvasHeight);
            if (right <= left || bottom <= top)
            {
                continue;
            }

            for (int y = top; y < bottom; y++)
            {
                int rowStart = (y * canvasWidth) + left;
                ignored.AsSpan(rowStart, right - left).Fill(1);
            }
        }

        return ignored;
    }
}
