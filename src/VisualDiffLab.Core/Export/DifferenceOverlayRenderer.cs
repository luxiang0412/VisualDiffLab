using VisualDiffLab.Core.Comparison;
using VisualDiffLab.Core.Models;

namespace VisualDiffLab.Core.Export;

/// <summary>
/// Produces the pixels used by both the on-screen heatmap and the exported
/// Difference Overlay PNG, so statistics, rendering and export can never disagree.
/// </summary>
public static class DifferenceOverlayRenderer
{
    /// <summary>
    /// Builds a premultiplied BGRA overlay for Win2D: transparent where pixels are
    /// the same or ignored, fixed diff colors everywhere else.
    /// </summary>
    public static byte[] BuildHeatmapOverlay(DiffResult diff)
    {
        ArgumentNullException.ThrowIfNull(diff);
        byte[] overlay = new byte[(long)diff.CanvasWidth * diff.CanvasHeight * 4];
        BuildHeatmapOverlay(diff, overlay);
        return overlay;
    }

    /// <summary>
    /// Fills a caller-owned overlay buffer. Reusing the buffer avoids allocating a fresh
    /// 33 MB array on every threshold change for 4K images.
    /// </summary>
    public static void BuildHeatmapOverlay(DiffResult diff, byte[] destination)
    {
        ArgumentNullException.ThrowIfNull(diff);
        ArgumentNullException.ThrowIfNull(destination);
        int width = diff.CanvasWidth;
        int height = diff.CanvasHeight;
        long expected = (long)width * height * 4;
        if (destination.LongLength != expected)
        {
            throw new ArgumentException($"Overlay buffer must be exactly {expected:N0} bytes.", nameof(destination));
        }

        Array.Clear(destination);
        byte[] mask = diff.ClassificationMask;

        for (int i = 0; i < mask.Length; i++)
        {
            Rgba? color = mask[i] switch
            {
                (byte)DiffClassification.Changed => DiffColors.Changed,
                (byte)DiffClassification.OnlyInA => DiffColors.Removed,
                (byte)DiffClassification.OnlyInB => DiffColors.Added,
                _ => null,
            };

            if (color is null)
            {
                continue;
            }

            Rgba c = color.Value;
            int index = i * 4;
            destination[index] = Premultiply(c.B, DiffColors.OverlayAlpha);
            destination[index + 1] = Premultiply(c.G, DiffColors.OverlayAlpha);
            destination[index + 2] = Premultiply(c.R, DiffColors.OverlayAlpha);
            destination[index + 3] = DiffColors.OverlayAlpha;
        }
    }

    /// <summary>
    /// Composes the exported difference overlay: A as the opaque base, fixed colors on
    /// changed/added/removed pixels, ignored pixels shown as the plain base. Returns an
    /// opaque BGRA8 buffer of the comparison canvas size.
    /// </summary>
    public static byte[] ComposeExport(DiffResult diff, ImageDocument imageA)
    {
        ArgumentNullException.ThrowIfNull(diff);
        ArgumentNullException.ThrowIfNull(imageA);

        int width = diff.CanvasWidth;
        int height = diff.CanvasHeight;
        if (imageA.Width > width || imageA.Height > height)
        {
            throw new ArgumentException("Image A is larger than the comparison canvas.", nameof(imageA));
        }

        Rgba background = DiffColors.DifferenceExportBackground;
        byte[] output = new byte[(long)width * height * 4];
        byte[] mask = diff.ClassificationMask;
        byte[] source = imageA.Pixels;
        int sourceStride = imageA.Stride;

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            bool rowInA = y < imageA.Height;

            for (int x = 0; x < width; x++)
            {
                int index = (row + x) * 4;
                Rgba baseColor;
                if (rowInA && x < imageA.Width)
                {
                    int sourceIndex = (y * sourceStride) + (x * 4);
                    baseColor = FlattenOver(source, sourceIndex, background);
                }
                else
                {
                    baseColor = background;
                }

                Rgba? overlay = mask[row + x] switch
                {
                    (byte)DiffClassification.Changed => DiffColors.Changed,
                    (byte)DiffClassification.OnlyInA => DiffColors.Removed,
                    (byte)DiffClassification.OnlyInB => DiffColors.Added,
                    _ => null,
                };

                Rgba result = overlay is null
                    ? baseColor
                    : Blend(baseColor, overlay.Value, DiffColors.OverlayAlpha);

                WriteOpaque(output, index, result);
            }
        }

        return output;
    }

    private static Rgba FlattenOver(byte[] premultipliedSource, int index, Rgba background)
    {
        int alpha = premultipliedSource[index + 3];
        if (alpha == 255)
        {
            return new Rgba(premultipliedSource[index + 2], premultipliedSource[index + 1], premultipliedSource[index], 255);
        }

        int inverse = 255 - alpha;
        int b = premultipliedSource[index] + ((background.B * inverse) / 255);
        int g = premultipliedSource[index + 1] + ((background.G * inverse) / 255);
        int r = premultipliedSource[index + 2] + ((background.R * inverse) / 255);
        return new Rgba((byte)Math.Min(255, r), (byte)Math.Min(255, g), (byte)Math.Min(255, b), 255);
    }

    private static Rgba Blend(Rgba baseColor, Rgba overlay, byte alpha)
    {
        int inverse = 255 - alpha;
        int r = ((overlay.R * alpha) + (baseColor.R * inverse)) / 255;
        int g = ((overlay.G * alpha) + (baseColor.G * inverse)) / 255;
        int b = ((overlay.B * alpha) + (baseColor.B * inverse)) / 255;
        return new Rgba((byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255), (byte)Math.Clamp(b, 0, 255), 255);
    }

    private static byte Premultiply(byte value, byte alpha) => (byte)((value * alpha + 127) / 255);

    internal static void WriteOpaque(byte[] buffer, int index, Rgba color)
    {
        buffer[index] = color.B;
        buffer[index + 1] = color.G;
        buffer[index + 2] = color.R;
        buffer[index + 3] = 255;
    }
}
