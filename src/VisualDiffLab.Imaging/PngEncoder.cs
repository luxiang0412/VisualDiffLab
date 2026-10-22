using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace VisualDiffLab.Imaging;

/// <summary>Encodes opaque or premultiplied BGRA8 buffers as PNG files.</summary>
public static class PngEncoder
{
    public const double DefaultDpi = 96.0;

    public static async Task SaveAsync(
        StorageFile file,
        int width,
        int height,
        byte[] bgraPixels,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(bgraPixels);
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "PNG dimensions must be positive.");
        }

        long expected = (long)width * height * 4;
        if (bgraPixels.LongLength != expected)
        {
            throw new ArgumentException(
                $"Pixel buffer length {bgraPixels.LongLength:N0} does not match {width}x{height} BGRA8 ({expected:N0} bytes).",
                nameof(bgraPixels));
        }

        cancellationToken.ThrowIfCancellationRequested();

        using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        stream.Size = 0;

        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            (uint)width,
            (uint)height,
            DefaultDpi,
            DefaultDpi,
            bgraPixels);

        cancellationToken.ThrowIfCancellationRequested();
        await encoder.FlushAsync();
    }
}
