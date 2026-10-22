namespace VisualDiffLab.Core.Models;

/// <summary>
/// A decoded image normalized to BGRA8, premultiplied alpha, sRGB, top-down rows.
/// Instances are immutable; the pixel buffer is owned by the document.
/// </summary>
public sealed class ImageDocument
{
    public const int MaxDimension = 16384;
    public const long MaxPixelCount = 80_000_000;

    public ImageDocument(string fileName, string format, int width, int height, byte[] pixels)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        ArgumentNullException.ThrowIfNull(pixels);

        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), $"Image dimensions must be positive (got {width}x{height}).");
        }

        if (width > MaxDimension || height > MaxDimension)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                $"Image dimensions exceed the supported limit of {MaxDimension} pixels per side (got {width}x{height}).");
        }

        long pixelCount = (long)width * height;
        if (pixelCount > MaxPixelCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                $"Image exceeds the supported limit of {MaxPixelCount:N0} pixels (got {pixelCount:N0}).");
        }

        long expectedBytes = pixelCount * 4;
        if (pixels.LongLength != expectedBytes)
        {
            throw new ArgumentException(
                $"Pixel buffer length {pixels.LongLength:N0} does not match {width}x{height} BGRA8 ({expectedBytes:N0} bytes).",
                nameof(pixels));
        }

        FileName = fileName;
        Format = format;
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    /// <summary>File name without directory. The app deliberately does not store or log full paths.</summary>
    public string FileName { get; }

    /// <summary>Human readable source format: PNG, JPEG or BMP.</summary>
    public string Format { get; }

    public int Width { get; }

    public int Height { get; }

    /// <summary>BGRA8 premultiplied sRGB pixels, row-major, stride = Width * 4. Do not mutate.</summary>
    public byte[] Pixels { get; }

    public int Stride => Width * 4;

    public long PixelCount => (long)Width * Height;

    public override string ToString() => $"{FileName} ({Format}, {Width}x{Height})";
}
