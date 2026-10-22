using System;
using System.IO;
using System.Threading.Tasks;
using VisualDiffLab.Core.Models;
using VisualDiffLab.Imaging;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Xunit;

namespace VisualDiffLab.Imaging.Tests;

public sealed class ImagingTestDirectory : IDisposable
{
    public ImagingTestDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "vdl-imaging-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup of test artifacts.
        }
    }
}

public class ImageLoaderTests
{
    private static byte[] CreateBgra(int width, int height, Func<int, int, Rgba> pixel)
    {
        byte[] buffer = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Rgba color = pixel(x, y);
                int index = ((y * width) + x) * 4;
                int alpha = color.A;
                buffer[index] = (byte)((color.B * alpha + 127) / 255);
                buffer[index + 1] = (byte)((color.G * alpha + 127) / 255);
                buffer[index + 2] = (byte)((color.R * alpha + 127) / 255);
                buffer[index + 3] = (byte)alpha;
            }
        }

        return buffer;
    }

    private static async Task<StorageFile> CreateEmptyFileAsync(string path)
    {
        await File.WriteAllBytesAsync(path, Array.Empty<byte>());
        return await StorageFile.GetFileFromPathAsync(path);
    }

    private static async Task EncodeAsync(
        string path,
        Guid encoderId,
        int width,
        int height,
        byte[] pixels,
        BitmapAlphaMode alphaMode = BitmapAlphaMode.Premultiplied,
        ushort? exifOrientation = null)
    {
        StorageFile file = await CreateEmptyFileAsync(path);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        stream.Size = 0;
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, alphaMode, (uint)width, (uint)height, 96, 96, pixels);

        if (exifOrientation is not null)
        {
            BitmapPropertySet properties = new();
            properties.Add("System.Photo.Orientation", new BitmapTypedValue(exifOrientation.Value, PropertyType.UInt16));
            await encoder.BitmapProperties.SetPropertiesAsync(properties);
        }

        await encoder.FlushAsync();
    }

    private static Rgba Pixel(ImageDocument document, int x, int y)
    {
        int index = ((y * document.Stride) + (x * 4));
        return new Rgba(document.Pixels[index + 2], document.Pixels[index + 1], document.Pixels[index], document.Pixels[index + 3]);
    }

    [Fact]
    public async Task Png_RoundTripsDimensionsAndPixels()
    {
        using ImagingTestDirectory directory = new();
        string path = directory.File("roundtrip.png");
        byte[] pixels = CreateBgra(9, 5, (x, y) => new Rgba((byte)(x * 20), (byte)(y * 30), 77));

        await EncodeAsync(path, BitmapEncoder.PngEncoderId, 9, 5, pixels);
        ImageDocument document = await ImageLoader.LoadAsync(path);

        Assert.Equal("PNG", document.Format);
        Assert.Equal(9, document.Width);
        Assert.Equal(5, document.Height);
        Assert.Equal("roundtrip.png", document.FileName);
        Assert.Equal(new Rgba(40, 30, 77, 255), Pixel(document, 2, 1));
    }

    [Fact]
    public async Task Png_TransparentHiddenRgbIsPremultipliedAway()
    {
        using ImagingTestDirectory directory = new();
        string path = directory.File("hidden-rgb.png");
        byte[] pixels = CreateBgra(2, 1, (_, _) => new Rgba(255, 0, 0, 0));

        await EncodeAsync(path, BitmapEncoder.PngEncoderId, 2, 1, pixels, BitmapAlphaMode.Straight);
        ImageDocument document = await ImageLoader.LoadAsync(path);

        Assert.Equal(new Rgba(0, 0, 0, 0), Pixel(document, 0, 0));
    }

    [Fact]
    public async Task Jpeg_IsDecodedWithExifOrientationApplied()
    {
        using ImagingTestDirectory directory = new();
        string path = directory.File("rotated.jpg");
        byte[] pixels = CreateBgra(8, 4, (_, _) => new Rgba(20, 120, 220));

        // 6 = rotate 90 degrees clockwise; a valid decoder must swap dimensions.
        await EncodeAsync(path, BitmapEncoder.JpegEncoderId, 8, 4, pixels, exifOrientation: 6);
        ImageDocument document = await ImageLoader.LoadAsync(path);

        Assert.Equal("JPEG", document.Format);
        Assert.Equal(4, document.Width);
        Assert.Equal(8, document.Height);
        Assert.Equal(4 * 8 * 4, document.Pixels.Length);
    }

    [Fact]
    public async Task Bmp_IsDecoded()
    {
        using ImagingTestDirectory directory = new();
        string path = directory.File("bitmap.bmp");
        byte[] pixels = CreateBgra(6, 3, (_, _) => new Rgba(10, 200, 30));

        await EncodeAsync(path, BitmapEncoder.BmpEncoderId, 6, 3, pixels);
        ImageDocument document = await ImageLoader.LoadAsync(path);

        Assert.Equal("BMP", document.Format);
        Assert.Equal(6, document.Width);
        Assert.Equal(3, document.Height);
        Assert.Equal(new Rgba(10, 200, 30, 255), Pixel(document, 5, 2));
    }

    [Fact]
    public async Task CorruptFile_ThrowsUserFacingError()
    {
        using ImagingTestDirectory directory = new();
        string path = directory.File("corrupt.png");
        await File.WriteAllBytesAsync(path, new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4, 5, 6, 7 });

        ImageLoadException exception = await Assert.ThrowsAsync<ImageLoadException>(() => ImageLoader.LoadAsync(path));
        Assert.False(string.IsNullOrWhiteSpace(exception.UserMessage));
        Assert.NotEqual(ImageLoadErrorKind.UnsupportedFormat, exception.Kind);
    }

    [Fact]
    public async Task UnsupportedFormat_IsRejectedWithClearMessage()
    {
        using ImagingTestDirectory directory = new();
        string path = directory.File("animation.gif");
        byte[] pixels = CreateBgra(4, 4, (_, _) => new Rgba(0, 0, 0));

        await EncodeAsync(path, BitmapEncoder.GifEncoderId, 4, 4, pixels);

        ImageLoadException exception = await Assert.ThrowsAsync<ImageLoadException>(() => ImageLoader.LoadAsync(path));
        Assert.Equal(ImageLoadErrorKind.UnsupportedFormat, exception.Kind);
        Assert.Contains("PNG, JPEG and BMP", exception.UserMessage);
    }

    [Fact]
    public async Task MissingFile_ReportsFileNotFound()
    {
        using ImagingTestDirectory directory = new();
        string path = directory.File("does-not-exist.png");

        ImageLoadException exception = await Assert.ThrowsAsync<ImageLoadException>(() => ImageLoader.LoadAsync(path));
        Assert.Equal(ImageLoadErrorKind.FileNotFound, exception.Kind);
    }

    [Fact]
    public async Task LockedFile_ReportsAUserFacingError()
    {
        using ImagingTestDirectory directory = new();
        string path = directory.File("locked.png");
        byte[] pixels = CreateBgra(4, 4, (_, _) => new Rgba(1, 2, 3));
        await EncodeAsync(path, BitmapEncoder.PngEncoderId, 4, 4, pixels);

        using FileStream locked = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        ImageLoadException exception = await Assert.ThrowsAsync<ImageLoadException>(() => ImageLoader.LoadAsync(path));
        Assert.Contains(
            exception.Kind,
            new[] { ImageLoadErrorKind.AccessDenied, ImageLoadErrorKind.Unknown, ImageLoadErrorKind.CorruptFile });
    }

    [Fact]
    public async Task FourKImage_DecodesAtExpectedSize()
    {
        using ImagingTestDirectory directory = new();
        string path = directory.File("4k.png");
        byte[] pixels = CreateBgra(3840, 2160, (x, y) => new Rgba((byte)(x % 251), (byte)(y % 241), 64));

        await EncodeAsync(path, BitmapEncoder.PngEncoderId, 3840, 2160, pixels);
        ImageDocument document = await ImageLoader.LoadAsync(path);

        Assert.Equal(3840, document.Width);
        Assert.Equal(2160, document.Height);
        Assert.Equal(3840 * 2160 * 4, document.Pixels.Length);
    }
}

public class PngEncoderTests
{
    [Fact]
    public async Task Save_ProducesFileReadableByDecoder()
    {
        using ImagingTestDirectory directory = new();
        string path = directory.File("export.png");
        await File.WriteAllBytesAsync(path, Array.Empty<byte>());
        StorageFile file = await StorageFile.GetFileFromPathAsync(path);

        byte[] pixels = new byte[7 * 3 * 4];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 10;      // B
            pixels[i + 1] = 20;  // G
            pixels[i + 2] = 30;  // R
            pixels[i + 3] = 255; // A
        }

        await PngEncoder.SaveAsync(file, 7, 3, pixels);

        ImageDocument document = await ImageLoader.LoadAsync(path);
        Assert.Equal("PNG", document.Format);
        Assert.Equal(7, document.Width);
        Assert.Equal(3, document.Height);
        Assert.Equal(30, document.Pixels[2]);
        Assert.Equal(20, document.Pixels[1]);
        Assert.Equal(10, document.Pixels[0]);
    }

    [Fact]
    public async Task Save_RejectsMismatchedBuffer()
    {
        using ImagingTestDirectory directory = new();
        string path = directory.File("bad.png");
        await File.WriteAllBytesAsync(path, Array.Empty<byte>());
        StorageFile file = await StorageFile.GetFileFromPathAsync(path);

        await Assert.ThrowsAsync<ArgumentException>(() => PngEncoder.SaveAsync(file, 4, 4, new byte[10]));
    }

    [Fact]
    public async Task Save_OverwritesExistingFileWithoutTrailingBytes()
    {
        using ImagingTestDirectory directory = new();
        string path = directory.File("overwrite.png");
        await File.WriteAllBytesAsync(path, new byte[4096]);
        StorageFile file = await StorageFile.GetFileFromPathAsync(path);

        await PngEncoder.SaveAsync(file, 2, 2, new byte[2 * 2 * 4]);

        ImageDocument document = await ImageLoader.LoadAsync(path);
        Assert.Equal(2, document.Width);
        Assert.Equal(2, document.Height);
    }
}
