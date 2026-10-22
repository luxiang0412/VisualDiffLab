using System;
using System.Linq;
using VisualDiffLab.Core.Comparison;
using VisualDiffLab.Core.IgnoreRegions;
using VisualDiffLab.Core.Models;
using Xunit;

namespace VisualDiffLab.Core.Tests;

public class DiffEngineTests
{
    private static readonly Rgba Black = new(0, 0, 0);
    private static readonly Rgba White = new(255, 255, 255);

    [Fact]
    public void IdenticalImages_ReportNoDifferences()
    {
        ImageDocument image = TestImages.Create(16, 9, (x, y) => new Rgba((byte)(x * 10), (byte)(y * 20), 40));

        DiffResult result = DiffEngine.Compare(image, image, DiffOptions.Default);

        Assert.Equal(16, result.CanvasWidth);
        Assert.Equal(9, result.CanvasHeight);
        Assert.Equal(144, result.ComparedPixelCount);
        Assert.Equal(0, result.DifferentPixelCount);
        Assert.Equal(0.0, result.DifferenceRatio);
        Assert.All(result.ClassificationMask, value => Assert.Equal((byte)DiffClassification.Same, value));
    }

    [Fact]
    public void SinglePixelDifference_IsDetectedAtThresholdZero()
    {
        ImageDocument a = TestImages.Solid(8, 8, Black);
        ImageDocument b = TestImages.Create(8, 8, (x, y) => x == 3 && y == 4 ? White : Black);

        DiffResult result = DiffEngine.Compare(a, b, new DiffOptions(0, null));

        Assert.Equal(1, result.DifferentPixelCount);
        Assert.Equal(64, result.ComparedPixelCount);
        Assert.Equal((byte)DiffClassification.Changed, result.ClassificationMask[(4 * 8) + 3]);
        Assert.Equal(1.0 / 64.0, result.DifferenceRatio);
    }

    [Theory]
    [InlineData(16, DiffClassification.Same)]
    [InlineData(17, DiffClassification.Changed)]
    public void Threshold_UsesStrictlyGreaterThan(int delta, DiffClassification expected)
    {
        ImageDocument a = TestImages.Solid(4, 4, Black);
        ImageDocument b = TestImages.Solid(4, 4, new Rgba((byte)delta, (byte)delta, (byte)delta));

        DiffResult result = DiffEngine.Compare(a, b, new DiffOptions(16, null));

        Assert.All(result.ClassificationMask, value => Assert.Equal((byte)expected, value));
        Assert.Equal(expected == DiffClassification.Changed ? 16 : 0, result.DifferentPixelCount);
    }

    [Fact]
    public void LargestChannelDifferenceDecides()
    {
        ImageDocument a = TestImages.Solid(2, 2, new Rgba(100, 100, 100, 255));
        ImageDocument b = TestImages.Solid(2, 2, new Rgba(110, 100, 100, 255));

        DiffResult below = DiffEngine.Compare(a, b, new DiffOptions(10, null));
        DiffResult above = DiffEngine.Compare(a, b, new DiffOptions(9, null));

        Assert.Equal(0, below.DifferentPixelCount);
        Assert.Equal(4, above.DifferentPixelCount);
    }

    [Fact]
    public void TransparentPixelsWithDifferentHiddenRgb_AreSame()
    {
        ImageDocument a = TestImages.Solid(2, 1, new Rgba(255, 0, 0, 0));
        ImageDocument b = TestImages.Solid(2, 1, new Rgba(0, 0, 255, 0));

        DiffResult result = DiffEngine.Compare(a, b, new DiffOptions(0, null));

        Assert.Equal(0, result.DifferentPixelCount);
    }

    [Fact]
    public void AlphaDifferenceWithSameRgb_IsDifferent()
    {
        ImageDocument a = TestImages.Solid(1, 1, new Rgba(200, 100, 50, 255));
        ImageDocument b = TestImages.Solid(1, 1, new Rgba(200, 100, 50, 128));

        DiffResult result = DiffEngine.Compare(a, b, new DiffOptions(16, null));

        Assert.Equal(1, result.DifferentPixelCount);
    }

    [Fact]
    public void WiderImageA_ProducesRemovedPixelsOverCanvas()
    {
        ImageDocument a = TestImages.Solid(4, 2, Black);
        ImageDocument b = TestImages.Solid(2, 1, Black);

        DiffResult result = DiffEngine.Compare(a, b, new DiffOptions(16, null));

        Assert.Equal(4, result.CanvasWidth);
        Assert.Equal(2, result.CanvasHeight);
        Assert.Equal(8, result.ComparedPixelCount);
        Assert.Equal(6, result.RemovedPixelCount);
        Assert.Equal(0, result.AddedPixelCount);
        Assert.Equal(6, result.DifferentPixelCount);
        Assert.All(
            Enumerable.Range(0, 8),
            index => Assert.Equal(
                index is 0 or 1 ? (byte)DiffClassification.Same : (byte)DiffClassification.OnlyInA,
                result.ClassificationMask[index]));
    }

    [Fact]
    public void WiderImageB_ProducesAddedPixelsOverCanvas()
    {
        ImageDocument a = TestImages.Solid(2, 1, Black);
        ImageDocument b = TestImages.Solid(4, 3, Black);

        DiffResult result = DiffEngine.Compare(a, b, new DiffOptions(0, null));

        Assert.Equal(4, result.CanvasWidth);
        Assert.Equal(3, result.CanvasHeight);
        Assert.Equal(12, result.ComparedPixelCount);
        Assert.Equal(10, result.AddedPixelCount);
        Assert.Equal(0, result.RemovedPixelCount);
        Assert.Equal(10, result.DifferentPixelCount);
    }

    [Fact]
    public void IgnoreRegionCoveringCanvas_ProducesNoComparedPixels()
    {
        ImageDocument a = TestImages.Solid(4, 4, Black);
        ImageDocument b = TestImages.Solid(4, 4, White);
        IgnoreRegion region = new(0, 0, 4, 4);

        DiffResult result = DiffEngine.Compare(a, b, new DiffOptions(0, new[] { region }));

        Assert.Equal(0, result.ComparedPixelCount);
        Assert.Equal(0, result.DifferentPixelCount);
        Assert.Equal(16, result.IgnoredPixelCount);
        Assert.Null(result.DifferenceRatio);
        Assert.All(result.ClassificationMask, value => Assert.Equal((byte)DiffClassification.Ignored, value));
    }

    [Fact]
    public void PartialIgnoreRegion_IsExcludedFromStatistics()
    {
        ImageDocument a = TestImages.Solid(4, 4, Black);
        ImageDocument b = TestImages.Solid(4, 4, White);
        IgnoreRegion region = new(0, 0, 2, 2);

        DiffResult result = DiffEngine.Compare(a, b, new DiffOptions(0, new[] { region }));

        Assert.Equal(4, result.IgnoredPixelCount);
        Assert.Equal(12, result.ComparedPixelCount);
        Assert.Equal(12, result.DifferentPixelCount);
        Assert.Equal(1.0, result.DifferenceRatio);
    }

    [Fact]
    public void OverlappingIgnoreRegions_UseTheirUnion()
    {
        ImageDocument a = TestImages.Solid(4, 4, Black);
        ImageDocument b = TestImages.Solid(4, 4, White);
        IgnoreRegion first = new(0, 0, 2, 2);
        IgnoreRegion second = new(1, 1, 2, 2);

        DiffResult result = DiffEngine.Compare(a, b, new DiffOptions(0, new[] { first, second }));

        Assert.Equal(7, result.IgnoredPixelCount);
        Assert.Equal(9, result.ComparedPixelCount);
    }

    [Fact]
    public void OutOfBoundsIgnoreRegions_AreClipped()
    {
        ImageDocument a = TestImages.Solid(4, 4, Black);
        ImageDocument b = TestImages.Solid(4, 4, White);
        IgnoreRegion outside = new(-10, -10, 5, 5);
        IgnoreRegion straddling = new(3, 3, 10, 10);

        DiffResult result = DiffEngine.Compare(a, b, new DiffOptions(0, new[] { outside, straddling }));

        Assert.Equal(15, result.ComparedPixelCount);
        Assert.Equal(1, result.IgnoredPixelCount);
    }

    [Fact]
    public void IgnoredRegionOverridesSizeDifference()
    {
        ImageDocument a = TestImages.Solid(4, 2, Black);
        ImageDocument b = TestImages.Solid(2, 1, Black);
        IgnoreRegion region = new(2, 0, 2, 2);

        DiffResult result = DiffEngine.Compare(a, b, new DiffOptions(0, new[] { region }));

        Assert.Equal(4, result.IgnoredPixelCount);
        Assert.Equal(4, result.ComparedPixelCount);
        Assert.Equal(2, result.DifferentPixelCount); // rows y=0 and y=1 at x=2,3 are ignored, row y=1 x=0,1 remain OnlyInA
    }

    [Fact]
    public void ExtremeAspectRatio_IsHandledAtBoundaryPixels()
    {
        ImageDocument a = TestImages.Solid(1, 1000, Black);
        ImageDocument b = TestImages.Solid(1000, 1, Black);

        DiffResult result = DiffEngine.Compare(a, b, new DiffOptions(0, null));

        Assert.Equal(1000, result.CanvasWidth);
        Assert.Equal(1000, result.CanvasHeight);
        Assert.Equal(1_000_000, result.ComparedPixelCount);
        Assert.Equal(999, result.RemovedPixelCount);
        Assert.Equal(999, result.AddedPixelCount);
        Assert.Equal(1_998, result.DifferentPixelCount);
        Assert.Equal((byte)DiffClassification.Same, result.ClassificationMask[0]);
        Assert.Equal((byte)DiffClassification.OnlyInA, result.ClassificationMask[1000]);
        Assert.Equal((byte)DiffClassification.OnlyInB, result.ClassificationMask[1]);
    }

    [Fact]
    public void ResultIsDeterministic()
    {
        ImageDocument a = TestImages.Create(64, 32, (x, y) => new Rgba((byte)(x * 3), (byte)(y * 5), 7, (byte)(x % 2 == 0 ? 255 : 128)));
        ImageDocument b = TestImages.Create(60, 36, (x, y) => new Rgba((byte)(x * 3), (byte)(y * 5), (byte)(y % 3 == 0 ? 30 : 7), 255));

        DiffResult first = DiffEngine.Compare(a, b, new DiffOptions(12, null));
        DiffResult second = DiffEngine.Compare(a, b, new DiffOptions(12, null));

        Assert.Equal(first.DifferentPixelCount, second.DifferentPixelCount);
        Assert.Equal(first.ComparedPixelCount, second.ComparedPixelCount);
        Assert.Equal(first.ClassificationMask, second.ClassificationMask);
    }

    [Fact]
    public void Cancellation_IsObserved()
    {
        ImageDocument a = TestImages.Solid(512, 512, Black);
        ImageDocument b = TestImages.Solid(512, 512, Black);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            DiffEngine.Compare(a, b, DiffOptions.Default, cancellationToken: cancellation.Token));
    }

    [Fact]
    public void InvalidThreshold_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiffOptions(-1, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiffOptions(65, null));
    }

    [Fact]
    public void VersionIsStampedOnResult()
    {
        ImageDocument image = TestImages.Solid(2, 2, Black);

        DiffResult result = DiffEngine.Compare(image, image, DiffOptions.Default, version: 42);

        Assert.Equal(42, result.Version);
    }
}
