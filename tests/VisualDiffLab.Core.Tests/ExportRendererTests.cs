using System;
using VisualDiffLab.Core.Comparison;
using VisualDiffLab.Core.Export;
using VisualDiffLab.Core.IgnoreRegions;
using VisualDiffLab.Core.Models;
using Xunit;

namespace VisualDiffLab.Core.Tests;

public class ExportRendererTests
{
    private static readonly Rgba Black = new(0, 0, 0);
    private static readonly Rgba White = new(255, 255, 255);

    [Fact]
    public void DifferenceOverlay_UsesCanvasSize()
    {
        ImageDocument a = TestImages.Solid(4, 2, Black);
        ImageDocument b = TestImages.Solid(6, 5, Black);
        DiffResult diff = DiffEngine.Compare(a, b, DiffOptions.Default);

        byte[] output = DifferenceOverlayRenderer.ComposeExport(diff, a);

        Assert.Equal(6 * 5 * 4, output.Length);
    }

    [Fact]
    public void DifferenceOverlay_ChangedPixelsUseFixedColorOverA()
    {
        ImageDocument a = TestImages.Solid(4, 4, Black);
        ImageDocument b = TestImages.Solid(4, 4, White);
        DiffResult diff = DiffEngine.Compare(a, b, new DiffOptions(0, null));

        byte[] output = DifferenceOverlayRenderer.ComposeExport(diff, a);

        Rgba pixel = TestImages.GetPixel(output, 4, 3, 3);
        Assert.Equal(new Rgba(193, 0, 119, 255), pixel);
    }

    [Fact]
    public void DifferenceOverlay_MarksAddedAreaWithAddedColor()
    {
        ImageDocument a = TestImages.Solid(2, 2, Black);
        ImageDocument b = TestImages.Solid(4, 4, Black);
        DiffResult diff = DiffEngine.Compare(a, b, new DiffOptions(0, null));

        byte[] output = DifferenceOverlayRenderer.ComposeExport(diff, a);

        Assert.Equal(new Rgba(4, 142, 221, 255), TestImages.GetPixel(output, 4, 3, 3));
        Assert.Equal(Black with { A = 255 }, TestImages.GetPixel(output, 4, 0, 0));
    }

    [Fact]
    public void DifferenceOverlay_MarksRemovedAreaWithRemovedColor()
    {
        ImageDocument a = TestImages.Solid(4, 4, Black);
        ImageDocument b = TestImages.Solid(2, 2, Black);
        DiffResult diff = DiffEngine.Compare(a, b, new DiffOptions(0, null));

        byte[] output = DifferenceOverlayRenderer.ComposeExport(diff, a);

        Assert.Equal(new Rgba(217, 157, 0, 255), TestImages.GetPixel(output, 4, 3, 3));
    }

    [Fact]
    public void DifferenceOverlay_IgnoredPixelsShowPlainBase()
    {
        ImageDocument a = TestImages.Solid(4, 4, Black);
        ImageDocument b = TestImages.Solid(4, 4, White);
        DiffResult diff = DiffEngine.Compare(a, b, new DiffOptions(0, new[] { new IgnoreRegion(0, 0, 2, 2) }));

        byte[] output = DifferenceOverlayRenderer.ComposeExport(diff, a);

        Assert.Equal(new Rgba(0, 0, 0, 255), TestImages.GetPixel(output, 4, 0, 0));
        Assert.Equal(new Rgba(193, 0, 119, 255), TestImages.GetPixel(output, 4, 3, 3));
    }

    [Fact]
    public void HeatmapOverlay_IsTransparentOnlyForSameAndIgnoredPixels()
    {
        ImageDocument a = TestImages.Solid(4, 4, Black);
        ImageDocument b = TestImages.Create(4, 4, (x, y) => x < 2 ? Black : White);
        DiffResult diff = DiffEngine.Compare(a, b, new DiffOptions(0, new[] { new IgnoreRegion(0, 0, 1, 1) }));

        byte[] overlay = DifferenceOverlayRenderer.BuildHeatmapOverlay(diff);

        Assert.Equal(new Rgba(0, 0, 0, 0), TestImages.GetPixel(overlay, 4, 1, 3));
        Assert.Equal(new Rgba(0, 0, 0, 0), TestImages.GetPixel(overlay, 4, 0, 0));
        Assert.Equal(new Rgba(193, 0, 119, 217), TestImages.GetPixel(overlay, 4, 3, 3));
    }

    [Fact]
    public void SideBySide_UsesNaturalSizesAndFixedPadding()
    {
        ImageDocument a = TestImages.Solid(40, 20, new Rgba(255, 0, 0));
        ImageDocument b = TestImages.Solid(60, 30, new Rgba(0, 0, 255));

        (int width, int height) = SideBySideRenderer.GetOutputSize(a, b);
        byte[] output = SideBySideRenderer.Compose(a, b);

        Assert.Equal(116, width);
        Assert.Equal(62, height);
        Assert.Equal(width * height * 4, output.Length);
        Assert.Equal(new Rgba(58, 58, 58, 255), TestImages.GetPixel(output, width, 0, 0));
        Assert.Equal(new Rgba(43, 43, 43, 255), TestImages.GetPixel(output, width, 45, 0));
        Assert.Equal(new Rgba(255, 0, 0, 255), TestImages.GetPixel(output, width, 0, 32));
        Assert.Equal(new Rgba(255, 0, 0, 255), TestImages.GetPixel(output, width, 39, 51));
        Assert.Equal(new Rgba(43, 43, 43, 255), TestImages.GetPixel(output, width, 39, 52));
        Assert.Equal(new Rgba(0, 0, 255, 255), TestImages.GetPixel(output, width, 56, 61));
    }

    [Fact]
    public void SideBySide_DrawsVisibleLabels()
    {
        ImageDocument a = TestImages.Solid(40, 20, new Rgba(255, 0, 0));
        ImageDocument b = TestImages.Solid(60, 30, new Rgba(0, 0, 255));

        byte[] output = SideBySideRenderer.Compose(a, b);

        (int width, _) = SideBySideRenderer.GetOutputSize(a, b);
        Assert.Equal(new Rgba(245, 245, 245, 255), TestImages.GetPixel(output, width, 15, 5));
        Assert.Equal(new Rgba(245, 245, 245, 255), TestImages.GetPixel(output, width, 71, 5));
    }

    [Fact]
    public void SideBySide_FlattensTransparentPixelsOverNeutralBackground()
    {
        ImageDocument a = TestImages.Solid(32, 16, new Rgba(255, 0, 0, 0));
        ImageDocument b = TestImages.Solid(32, 16, new Rgba(0, 0, 255, 0));

        byte[] output = SideBySideRenderer.Compose(a, b);

        (int width, _) = SideBySideRenderer.GetOutputSize(a, b);
        Assert.Equal(new Rgba(43, 43, 43, 255), TestImages.GetPixel(output, width, 5, 40));
    }

    [Fact]
    public void SideBySide_RejectsOversizedOutput()
    {
        Assert.Throws<InvalidOperationException>(() => SideBySideRenderer.EnsureSupported(20000, 20000));
    }
}
