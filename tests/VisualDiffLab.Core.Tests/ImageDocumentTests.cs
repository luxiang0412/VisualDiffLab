using System;
using VisualDiffLab.Core.Models;
using Xunit;

namespace VisualDiffLab.Core.Tests;

public class ImageDocumentTests
{
    [Fact]
    public void ZeroSizeImage_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImageDocument("empty.png", "PNG", 0, 0, Array.Empty<byte>()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImageDocument("empty.png", "PNG", 0, 10, Array.Empty<byte>()));
    }

    [Fact]
    public void PixelBufferLengthMustMatchDimensions()
    {
        Assert.Throws<ArgumentException>(() => new ImageDocument("bad.png", "PNG", 2, 2, new byte[15]));
    }

    [Fact]
    public void MetadataIsExposed()
    {
        ImageDocument document = new("screen.png", "PNG", 3, 2, new byte[3 * 2 * 4]);

        Assert.Equal("screen.png", document.FileName);
        Assert.Equal("PNG", document.Format);
        Assert.Equal(3, document.Width);
        Assert.Equal(2, document.Height);
        Assert.Equal(12, document.Stride);
        Assert.Equal(6, document.PixelCount);
    }

    [Fact]
    public void OversizedImage_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ImageDocument("huge.png", "PNG", ImageDocument.MaxDimension + 1, 1, new byte[4]));
    }
}
