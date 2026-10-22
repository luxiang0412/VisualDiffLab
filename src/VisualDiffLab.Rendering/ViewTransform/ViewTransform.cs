namespace VisualDiffLab.Rendering;

/// <summary>
/// The single coordinate model used by the canvas. Zoom is measured in viewport DIPs per
/// image pixel; the offset is the viewport position (DIPs) of image pixel (0,0).
/// Rendering and hit-testing both use this class, so they cannot disagree.
/// </summary>
public sealed class ViewTransform
{
    public const double MinZoom = 0.01;
    public const double MaxZoom = 32.0;
    public const double FitPadding = 24.0;

    public double Zoom { get; private set; } = 1.0;

    public double OffsetX { get; private set; }

    public double OffsetY { get; private set; }

    public double ViewportWidth { get; private set; }

    public double ViewportHeight { get; private set; }

    public int ContentWidth { get; private set; }

    public int ContentHeight { get; private set; }

    public void SetViewport(double width, double height)
    {
        ViewportWidth = Math.Max(0, width);
        ViewportHeight = Math.Max(0, height);
        ClampPan();
    }

    public void SetContent(int width, int height)
    {
        ContentWidth = Math.Max(0, width);
        ContentHeight = Math.Max(0, height);
    }

    /// <summary>Scales the content to fit inside the viewport with a fixed padding, then centers it.</summary>
    public void Fit()
    {
        if (ViewportWidth <= 0 || ViewportHeight <= 0 || ContentWidth <= 0 || ContentHeight <= 0)
        {
            return;
        }

        double availableWidth = Math.Max(1, ViewportWidth - (FitPadding * 2));
        double availableHeight = Math.Max(1, ViewportHeight - (FitPadding * 2));
        double zoom = Math.Min(availableWidth / ContentWidth, availableHeight / ContentHeight);
        Zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        OffsetX = (ViewportWidth - (ContentWidth * Zoom)) / 2;
        OffsetY = (ViewportHeight - (ContentHeight * Zoom)) / 2;
        ClampPan();
    }

    /// <summary>100% means one image pixel per physical screen pixel at the given rasterization scale.</summary>
    public void SetActualSize(double rasterizationScale)
    {
        if (rasterizationScale <= 0)
        {
            rasterizationScale = 1;
        }

        ZoomAt(1.0 / rasterizationScale, ViewportWidth / 2, ViewportHeight / 2);
    }

    /// <summary>Zooms while keeping the image point under the anchor fixed.</summary>
    public void ZoomAt(double zoom, double anchorX, double anchorY)
    {
        double newZoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        if (ContentWidth <= 0 || ContentHeight <= 0)
        {
            Zoom = newZoom;
            return;
        }

        double imageX = (anchorX - OffsetX) / Zoom;
        double imageY = (anchorY - OffsetY) / Zoom;
        Zoom = newZoom;
        OffsetX = anchorX - (imageX * Zoom);
        OffsetY = anchorY - (imageY * Zoom);
        ClampPan();
    }

    public void ZoomBy(double factor, double anchorX, double anchorY)
    {
        if (factor <= 0 || double.IsNaN(factor) || double.IsInfinity(factor))
        {
            return;
        }

        ZoomAt(Zoom * factor, anchorX, anchorY);
    }

    /// <summary>Moves the view so the given image point is centered in the viewport.</summary>
    public void CenterOnImagePoint(double imageX, double imageY)
    {
        if (ViewportWidth <= 0 || ViewportHeight <= 0 || ContentWidth <= 0 || ContentHeight <= 0)
        {
            return;
        }

        OffsetX = (ViewportWidth / 2) - (imageX * Zoom);
        OffsetY = (ViewportHeight / 2) - (imageY * Zoom);
        ClampPan();
    }

    public void PanBy(double deltaX, double deltaY)
    {
        OffsetX += deltaX;
        OffsetY += deltaY;
        ClampPan();
    }

    public (double X, double Y) ImageToViewport(double imageX, double imageY) =>
        (OffsetX + (imageX * Zoom), OffsetY + (imageY * Zoom));

    public (double X, double Y) ViewportToImage(double viewportX, double viewportY) =>
        ((viewportX - OffsetX) / Zoom, (viewportY - OffsetY) / Zoom);

    /// <summary>Zoom percentage shown in the status bar (100% = physical pixel ratio).</summary>
    public double GetDisplayZoomPercent(double rasterizationScale) =>
        Zoom * Math.Max(rasterizationScale, 0.01) * 100.0;

    public bool IsAtActualSize(double rasterizationScale) =>
        Math.Abs(Zoom - (1.0 / Math.Max(rasterizationScale, 0.01))) < 0.0005;

    /// <summary>
    /// Centers content smaller than the viewport; otherwise prevents panning the content
    /// completely out of view.
    /// </summary>
    private void ClampPan()
    {
        if (ViewportWidth <= 0 || ViewportHeight <= 0 || ContentWidth <= 0 || ContentHeight <= 0)
        {
            return;
        }

        double scaledWidth = ContentWidth * Zoom;
        double scaledHeight = ContentHeight * Zoom;

        if (scaledWidth <= ViewportWidth)
        {
            OffsetX = (ViewportWidth - scaledWidth) / 2;
        }
        else
        {
            double minX = ViewportWidth - scaledWidth;
            OffsetX = Math.Clamp(OffsetX, minX, 0);
        }

        if (scaledHeight <= ViewportHeight)
        {
            OffsetY = (ViewportHeight - scaledHeight) / 2;
        }
        else
        {
            double minY = ViewportHeight - scaledHeight;
            OffsetY = Math.Clamp(OffsetY, minY, 0);
        }
    }
}
