using System;
using VisualDiffLab.Core.Models;
using VisualDiffLab.Rendering;
using Xunit;

namespace VisualDiffLab.Rendering.Tests;

public class ViewTransformTests
{
    private const double Tolerance = 0.000001;

    private static ViewTransform Create(double viewportWidth, double viewportHeight, int contentWidth, int contentHeight)
    {
        ViewTransform transform = new();
        transform.SetViewport(viewportWidth, viewportHeight);
        transform.SetContent(contentWidth, contentHeight);
        transform.Fit();
        return transform;
    }

    [Fact]
    public void Fit_ScalesAndCentersContentWithPadding()
    {
        ViewTransform transform = Create(1000, 800, 2000, 1000);

        double expectedZoom = Math.Min((1000 - 48) / 2000.0, (800 - 48) / 1000.0);
        Assert.Equal(expectedZoom, transform.Zoom, 6);
        Assert.Equal((1000 - (2000 * expectedZoom)) / 2, transform.OffsetX, 6);
        Assert.Equal((800 - (1000 * expectedZoom)) / 2, transform.OffsetY, 6);
    }

    [Fact]
    public void Fit_DoesNotUpscaleBeyondMaximumZoom()
    {
        ViewTransform transform = Create(100000, 100000, 4, 4);

        Assert.Equal(ViewTransform.MaxZoom, transform.Zoom);
    }

    [Fact]
    public void ImageAndViewportCoordinatesRoundTrip()
    {
        ViewTransform transform = Create(900, 700, 1500, 900);

        (double viewportX, double viewportY) = transform.ImageToViewport(321.5, 217.25);
        (double imageX, double imageY) = transform.ViewportToImage(viewportX, viewportY);

        Assert.Equal(321.5, imageX, 6);
        Assert.Equal(217.25, imageY, 6);
    }

    [Fact]
    public void ZoomAt_KeepsAnchorStable()
    {
        ViewTransform transform = Create(1000, 800, 2000, 1600);
        transform.ZoomAt(2.0, 500, 400);
        (double beforeX, double beforeY) = transform.ViewportToImage(300, 200);

        transform.ZoomAt(3.0, 300, 200);

        (double afterX, double afterY) = transform.ViewportToImage(300, 200);
        Assert.Equal(beforeX, afterX, 6);
        Assert.Equal(beforeY, afterY, 6);
    }

    [Fact]
    public void Pan_IsClampedToContentBounds()
    {
        ViewTransform transform = Create(1000, 800, 2000, 1600);
        transform.ZoomAt(2.0, 500, 400);

        transform.PanBy(100000, 100000);
        Assert.Equal(0, transform.OffsetX, 6);
        Assert.Equal(0, transform.OffsetY, 6);

        transform.PanBy(-100000, -100000);
        Assert.Equal(1000 - (2000 * transform.Zoom), transform.OffsetX, 6);
        Assert.Equal(800 - (1600 * transform.Zoom), transform.OffsetY, 6);
    }

    [Fact]
    public void Pan_IsIgnoredWhenContentFits()
    {
        ViewTransform transform = Create(1000, 800, 500, 400);
        double offsetX = transform.OffsetX;
        double offsetY = transform.OffsetY;

        transform.PanBy(120, 80);

        Assert.Equal(offsetX, transform.OffsetX, 6);
        Assert.Equal(offsetY, transform.OffsetY, 6);
    }

    [Fact]
    public void ActualSize_RespectsDpiScale()
    {
        ViewTransform transform = Create(1000, 800, 500, 400);
        transform.SetActualSize(1.5);

        Assert.Equal(1.0 / 1.5, transform.Zoom, 6);
        Assert.Equal(100.0, transform.GetDisplayZoomPercent(1.5), 6);
        Assert.True(transform.IsAtActualSize(1.5));
        Assert.False(transform.IsAtActualSize(1.0));
    }

    [Fact]
    public void Zoom_IsClamped()
    {
        ViewTransform transform = Create(1000, 800, 2000, 2000);
        transform.ZoomAt(10000, 500, 400);
        Assert.Equal(ViewTransform.MaxZoom, transform.Zoom);

        transform.ZoomAt(0.0000001, 500, 400);
        Assert.Equal(ViewTransform.MinZoom, transform.Zoom);
    }

    [Fact]
    public void ZoomBy_RejectsInvalidFactors()
    {
        ViewTransform transform = Create(1000, 800, 2000, 2000);
        double zoom = transform.Zoom;

        transform.ZoomBy(0, 10, 10);
        transform.ZoomBy(double.NaN, 10, 10);
        transform.ZoomBy(double.PositiveInfinity, 10, 10);

        Assert.Equal(zoom, transform.Zoom, 6);
    }
}

public class CanvasGeometryTests
{
    [Fact]
    public void SideBySide_SplitsWithSinglePixelGap()
    {
        var viewports = CanvasGeometry.GetViewports(1000, 800, ComparisonMode.SideBySide, hasA: true, hasB: true);

        Assert.Equal(2, viewports.Count);
        Assert.Equal(0, viewports[0].Bounds.X);
        Assert.Equal(499.5, viewports[0].Bounds.Width, 6);
        Assert.Equal(500.5, viewports[1].Bounds.X, 6);
        Assert.Equal(499.5, viewports[1].Bounds.Width, 6);
        Assert.Equal(0, viewports[0].ImageIndex);
        Assert.Equal(1, viewports[1].ImageIndex);
    }

    [Theory]
    [InlineData(ComparisonMode.Wipe)]
    [InlineData(ComparisonMode.Overlay)]
    [InlineData(ComparisonMode.Heatmap)]
    public void SingleViewportModes_UseFullCanvas(ComparisonMode mode)
    {
        var viewports = CanvasGeometry.GetViewports(1000, 800, mode, hasA: true, hasB: true);

        Assert.Single(viewports);
        Assert.Equal(1000, viewports[0].Bounds.Width, 6);
        Assert.Equal(0, viewports[0].ImageIndex);
    }

    [Fact]
    public void SingleImage_FallsBackToAvailableImage()
    {
        var onlyB = CanvasGeometry.GetViewports(1000, 800, ComparisonMode.SideBySide, hasA: false, hasB: true);
        var onlyA = CanvasGeometry.GetViewports(1000, 800, ComparisonMode.SideBySide, hasA: true, hasB: false);

        Assert.Single(onlyB);
        Assert.Equal(1, onlyB[0].ImageIndex);
        Assert.Single(onlyA);
        Assert.Equal(0, onlyA[0].ImageIndex);
    }

    [Fact]
    public void ZeroSizedViewport_ProducesNoViewports()
    {
        Assert.Empty(CanvasGeometry.GetViewports(0, 800, ComparisonMode.SideBySide, true, true));
    }
}
