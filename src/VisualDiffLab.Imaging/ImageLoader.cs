using System.Runtime.InteropServices;
using VisualDiffLab.Core.Models;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace VisualDiffLab.Imaging;

/// <summary>
/// Decodes PNG/JPEG/BMP into the normalized BGRA8 premultiplied sRGB representation used
/// by <see cref="ImageDocument"/>. EXIF orientation is respected and ICC profiles are
/// converted to sRGB by Windows Imaging Component.
/// </summary>
public static class ImageLoader
{
    public const long MaxDecodedPixelCount = ImageDocument.MaxPixelCount;

    public static async Task<ImageDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ImageLoadException(ImageLoadErrorKind.FileNotFound, "No file was selected.");
        }

        StorageFile file;
        try
        {
            file = await StorageFile.GetFileFromPathAsync(path);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new ImageLoadException(ImageLoadErrorKind.FileNotFound, "The file could not be found. It may have been moved or deleted.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ImageLoadException(ImageLoadErrorKind.AccessDenied, "Access to the file was denied by Windows permissions.", ex);
        }
        catch (Exception ex)
        {
            throw MapException(ex, path);
        }

        return await LoadAsync(file, cancellationToken);
    }

    public static async Task<ImageDocument> LoadAsync(StorageFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            cancellationToken.ThrowIfCancellationRequested();

            if (decoder.FrameCount == 0)
            {
                throw new ImageLoadException(ImageLoadErrorKind.EmptyImage, "The image contains no frames.");
            }

            string? format = GetSupportedFormat(decoder.DecoderInformation.CodecId);
            if (format is null)
            {
                throw new ImageLoadException(
                    ImageLoadErrorKind.UnsupportedFormat,
                    "Unsupported image format. Visual Diff Lab supports PNG, JPEG and BMP files.");
            }

            long sourcePixels = (long)decoder.PixelWidth * decoder.PixelHeight;
            if (sourcePixels <= 0)
            {
                throw new ImageLoadException(ImageLoadErrorKind.EmptyImage, "The image has zero width or height.");
            }

            if (sourcePixels > MaxDecodedPixelCount)
            {
                throw new ImageLoadException(
                    ImageLoadErrorKind.TooLarge,
                    $"The image is {decoder.PixelWidth}x{decoder.PixelHeight} ({sourcePixels:N0} pixels). " +
                    $"The supported limit is {MaxDecodedPixelCount:N0} pixels.");
            }

            BitmapTransform transform = new();
            PixelDataProvider provider = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.ColorManageToSRgb);

            byte[] pixels = provider.DetachPixelData();
            cancellationToken.ThrowIfCancellationRequested();

            int width = (int)decoder.OrientedPixelWidth;
            int height = (int)decoder.OrientedPixelHeight;
            if ((long)width * height * 4 != pixels.LongLength)
            {
                // Fall back to the un-oriented dimensions for decoders that ignore EXIF rotation.
                width = (int)decoder.PixelWidth;
                height = (int)decoder.PixelHeight;
            }

            if ((long)width * height * 4 != pixels.LongLength)
            {
                throw new ImageLoadException(
                    ImageLoadErrorKind.CorruptFile,
                    "The decoded pixel data does not match the image dimensions.");
            }

            return new ImageDocument(file.Name, format, width, height, pixels);
        }
        catch (ImageLoadException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ImageLoadException(ImageLoadErrorKind.AccessDenied, "Access to the file was denied by Windows permissions.", ex);
        }
        catch (FileNotFoundException ex)
        {
            throw new ImageLoadException(ImageLoadErrorKind.FileNotFound, "The file could not be found.", ex);
        }
        catch (Exception ex)
        {
            throw MapException(ex, file.Path);
        }
    }

    /// <summary>Returns PNG/JPEG/BMP for supported codecs, otherwise null.</summary>
    public static string? GetSupportedFormat(Guid codecId)
    {
        if (codecId == BitmapDecoder.PngDecoderId)
        {
            return "PNG";
        }

        if (codecId == BitmapDecoder.JpegDecoderId)
        {
            return "JPEG";
        }

        if (codecId == BitmapDecoder.BmpDecoderId)
        {
            return "BMP";
        }

        return null;
    }

    private static ImageLoadException MapException(Exception ex, string? path)
    {
        int hr = ex.HResult;
        string extension = Path.GetExtension(path ?? string.Empty).ToUpperInvariant();

        if (ex is FileNotFoundException or DirectoryNotFoundException || hr == unchecked((int)0x80070002))
        {
            return new ImageLoadException(ImageLoadErrorKind.FileNotFound, "The file could not be found. It may have been moved or deleted.", ex);
        }

        if (ex is UnauthorizedAccessException || hr == unchecked((int)0x80070005))
        {
            return new ImageLoadException(ImageLoadErrorKind.AccessDenied, "Access to the file was denied by Windows permissions.", ex);
        }

        if (hr is unchecked((int)0x8007000E) or unchecked((int)0x80070008))
        {
            return new ImageLoadException(ImageLoadErrorKind.TooLarge, "There is not enough memory to decode this image.", ex);
        }

        if (ex is COMException ||
            ex is IOException ||
            hr == unchecked((int)0x88982F50) || // WINCODEC_ERR_COMPONENTNOTFOUND
            hr == unchecked((int)0x88982F60) || // WINCODEC_ERR_BADIMAGE
            hr == unchecked((int)0x8007000D))   // ERROR_INVALID_DATA
        {
            string suffix = string.IsNullOrEmpty(extension) ? string.Empty : $" ({extension})";
            return new ImageLoadException(
                ImageLoadErrorKind.CorruptFile,
                $"The file{suffix} could not be decoded. It may be damaged or use an unsupported encoding.",
                ex);
        }

        return new ImageLoadException(
            ImageLoadErrorKind.Unknown,
            "The image could not be opened. The file may be damaged or in an unsupported format.",
            ex);
    }
}
