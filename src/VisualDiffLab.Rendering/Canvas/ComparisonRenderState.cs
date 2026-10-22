using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using VisualDiffLab.Core.IgnoreRegions;
using VisualDiffLab.Core.Models;
using Windows.Foundation;
using Windows.UI;

namespace VisualDiffLab.Rendering;

/// <summary>Viewports in DIPs for the current mode. ImageIndex 0 = A, 1 = B.</summary>
public readonly record struct ImageViewport(Rect Bounds, int ImageIndex);

public static class CanvasGeometry
{
    public const double ViewportGap = 1.0;

    public static IReadOnlyList<ImageViewport> GetViewports(
        double width,
        double height,
        ComparisonMode mode,
        bool hasA,
        bool hasB)
    {
        if (width <= 0 || height <= 0)
        {
            return Array.Empty<ImageViewport>();
        }

        if (mode == ComparisonMode.SideBySide && hasA && hasB)
        {
            double leftWidth = Math.Max(1, (width - ViewportGap) / 2);
            double rightWidth = Math.Max(1, width - ViewportGap - leftWidth);
            return
            [
                new ImageViewport(new Rect(0, 0, leftWidth, height), 0),
                new ImageViewport(new Rect(leftWidth + ViewportGap, 0, rightWidth, height), 1),
            ];
        }

        int primary = hasA ? 0 : 1;
        return [new ImageViewport(new Rect(0, 0, width, height), primary)];
    }
}

/// <summary>Everything the renderer needs for one frame. Owned by the UI layer.</summary>
public sealed class ComparisonRenderState
{
    public CanvasBitmap? ImageA { get; set; }

    public CanvasBitmap? ImageB { get; set; }

    public CanvasBitmap? HeatmapOverlay { get; set; }

    public ViewTransform View { get; } = new();

    public ComparisonMode Mode { get; set; } = ComparisonMode.SideBySide;

    public double OverlayOpacity { get; set; } = 0.5;

    public double WipeFraction { get; set; } = 0.5;

    public int CanvasWidth { get; set; }

    public int CanvasHeight { get; set; }

    public IReadOnlyList<IgnoreRegion> Regions { get; set; } = Array.Empty<IgnoreRegion>();

    public Guid? SelectedRegionId { get; set; }

    /// <summary>Pending selection rectangle in image coordinates.</summary>
    public Rect? PendingRegion { get; set; }

    public bool IsRegionMode { get; set; }

    public Color Accent { get; set; } = Color.FromArgb(255, 0, 120, 212);

    public Color RegionStroke { get; set; } = Color.FromArgb(235, 255, 255, 255);

    public Color RegionSelectedStroke { get; set; } = Color.FromArgb(255, 0, 120, 212);

    public Color RegionLabelBackground { get; set; } = Color.FromArgb(220, 31, 31, 31);

    public Color RegionLabelForeground { get; set; } = Color.FromArgb(255, 255, 255, 255);

    public Color CanvasOutline { get; set; } = Color.FromArgb(90, 128, 128, 128);

    public Color AddedOutline { get; set; } = Color.FromArgb(200, 0, 162, 255);

    public Color RemovedOutline { get; set; } = Color.FromArgb(200, 255, 185, 0);

    public CanvasTextFormat? LabelFormat { get; set; }
}
