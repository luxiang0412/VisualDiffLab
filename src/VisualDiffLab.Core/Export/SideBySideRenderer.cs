using VisualDiffLab.Core.Models;

namespace VisualDiffLab.Core.Export;

/// <summary>
/// Composes the exported Side-by-side PNG: natural-size A and B, a neutral background
/// for size differences and a fixed 32 px label bar above each image.
/// </summary>
public static class SideBySideRenderer
{
    public const int LabelBarHeight = 32;
    public const int Gap = 16;
    public const long MaxOutputPixelCount = 120_000_000;

    public static (int Width, int Height) GetOutputSize(ImageDocument imageA, ImageDocument imageB)
    {
        ArgumentNullException.ThrowIfNull(imageA);
        ArgumentNullException.ThrowIfNull(imageB);
        int width = checked(imageA.Width + Gap + imageB.Width);
        int height = checked(LabelBarHeight + Math.Max(imageA.Height, imageB.Height));
        return (width, height);
    }

    public static void EnsureSupported(int width, int height)
    {
        long pixels = (long)width * height;
        if (pixels > MaxOutputPixelCount)
        {
            throw new InvalidOperationException(
                $"Side-by-side output would be {width}x{height} ({pixels:N0} pixels), " +
                $"which exceeds the limit of {MaxOutputPixelCount:N0} pixels.");
        }
    }

    public static byte[] Compose(ImageDocument imageA, ImageDocument imageB)
    {
        (int width, int height) = GetOutputSize(imageA, imageB);
        EnsureSupported(width, height);

        byte[] output = new byte[(long)width * height * 4];
        Fill(output, DiffColors.SideBySideBackground);

        int imageTop = LabelBarHeight;
        int imageHeight = Math.Max(imageA.Height, imageB.Height);
        FillRect(output, width, 0, 0, imageA.Width, LabelBarHeight, DiffColors.SideBySideLabelBar);
        FillRect(output, width, imageA.Width + Gap, 0, imageB.Width, LabelBarHeight, DiffColors.SideBySideLabelBar);
        FillRect(output, width, 0, imageTop, imageA.Width, imageHeight, DiffColors.SideBySideBackground);
        FillRect(output, width, imageA.Width + Gap, imageTop, imageB.Width, imageHeight, DiffColors.SideBySideBackground);

        BlitFlattened(output, width, imageA, 0, imageTop);
        BlitFlattened(output, width, imageB, imageA.Width + Gap, imageTop);

        int glyphOffsetY = (LabelBarHeight - (GlyphHeight * GlyphScale)) / 2;
        DrawGlyph(output, width, 'A', 12, glyphOffsetY, 0, imageA.Width);
        DrawGlyph(output, width, 'B', imageA.Width + Gap + 12, glyphOffsetY, imageA.Width + Gap, imageB.Width);

        return output;
    }

    private const int GlyphWidth = 5;
    private const int GlyphHeight = 7;
    private const int GlyphScale = 3;

    private static readonly byte[] GlyphA =
    [
        0b01110,
        0b10001,
        0b10001,
        0b11111,
        0b10001,
        0b10001,
        0b10001,
    ];

    private static readonly byte[] GlyphB =
    [
        0b11110,
        0b10001,
        0b10001,
        0b11110,
        0b10001,
        0b10001,
        0b11110,
    ];

    private static void DrawGlyph(byte[] buffer, int bufferWidth, char glyph, int x, int y, int barX, int barWidth)
    {
        // Skip labels on images too narrow to host a legible glyph; never draw outside the bar.
        if (barWidth < (GlyphWidth * GlyphScale) + 8 || x < barX || x + (GlyphWidth * GlyphScale) > barX + barWidth)
        {
            return;
        }

        byte[] rows = glyph == 'A' ? GlyphA : GlyphB;
        Rgba color = DiffColors.SideBySideLabelText;
        for (int row = 0; row < GlyphHeight; row++)
        {
            for (int column = 0; column < GlyphWidth; column++)
            {
                int bit = 1 << (GlyphWidth - 1 - column);
                if ((rows[row] & bit) == 0)
                {
                    continue;
                }

                for (int sy = 0; sy < GlyphScale; sy++)
                {
                    for (int sx = 0; sx < GlyphScale; sx++)
                    {
                        int px = x + (column * GlyphScale) + sx;
                        int py = y + (row * GlyphScale) + sy;
                        if (px < 0 || py < 0 || px >= bufferWidth)
                        {
                            continue;
                        }

                        SetPixel(buffer, bufferWidth, px, py, color);
                    }
                }
            }
        }
    }

    private static void BlitFlattened(byte[] destination, int destinationWidth, ImageDocument image, int offsetX, int offsetY)
    {
        Rgba background = DiffColors.SideBySideBackground;
        byte[] source = image.Pixels;
        for (int y = 0; y < image.Height; y++)
        {
            int sourceRow = y * image.Stride;
            int destinationRow = (offsetY + y) * destinationWidth;
            for (int x = 0; x < image.Width; x++)
            {
                int sourceIndex = sourceRow + (x * 4);
                int alpha = source[sourceIndex + 3];
                int inverse = 255 - alpha;
                int b = Math.Min(255, source[sourceIndex] + ((background.B * inverse) / 255));
                int g = Math.Min(255, source[sourceIndex + 1] + ((background.G * inverse) / 255));
                int r = Math.Min(255, source[sourceIndex + 2] + ((background.R * inverse) / 255));
                int destinationIndex = (destinationRow + offsetX + x) * 4;
                destination[destinationIndex] = (byte)b;
                destination[destinationIndex + 1] = (byte)g;
                destination[destinationIndex + 2] = (byte)r;
                destination[destinationIndex + 3] = 255;
            }
        }
    }

    private static void Fill(byte[] buffer, Rgba color)
    {
        for (int i = 0; i < buffer.Length; i += 4)
        {
            Destination(buffer, i, color);
        }
    }

    private static void FillRect(byte[] buffer, int bufferWidth, int x, int y, int width, int height, Rgba color)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        for (int row = y; row < y + height; row++)
        {
            int rowStart = row * bufferWidth;
            for (int column = x; column < x + width; column++)
            {
                Destination(buffer, (rowStart + column) * 4, color);
            }
        }
    }

    private static void SetPixel(byte[] buffer, int bufferWidth, int x, int y, Rgba color)
    {
        int index = ((y * bufferWidth) + x) * 4;
        if (index < 0 || index + 3 >= buffer.Length)
        {
            return;
        }

        Destination(buffer, index, color);
    }

    private static void Destination(byte[] buffer, int index, Rgba color)
    {
        buffer[index] = color.B;
        buffer[index + 1] = color.G;
        buffer[index + 2] = color.R;
        buffer[index + 3] = color.A;
    }
}
