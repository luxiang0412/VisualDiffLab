using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using VisualDiffLab.Core.IgnoreRegions;
using VisualDiffLab.Core.Models;
using Windows.Foundation;
using Windows.UI;

namespace VisualDiffLab.Rendering;

/// <summary>
/// Immediate-mode Win2D renderer for all four modes. Drawing and hit-testing share
/// <see cref="ViewTransform"/> and <see cref="CanvasGeometry"/>.
/// </summary>
public static class ComparisonRenderer
{
    public static void Draw(CanvasDrawingSession session, double width, double height, ComparisonRenderState state)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(state);

        bool hasA = state.ImageA is not null;
        bool hasB = state.ImageB is not null;
        if (!hasA && !hasB)
        {
            return;
        }

        IReadOnlyList<ImageViewport> viewports = CanvasGeometry.GetViewports(width, height, state.Mode, hasA, hasB);
        if (viewports.Count == 0)
        {
            return;
        }

        foreach (ImageViewport viewport in viewports)
        {
            CanvasBitmap? image = viewport.ImageIndex == 0 ? state.ImageA : state.ImageB;
            if (image is not null)
            {
                DrawImage(session, viewport.Bounds, state, image, 1f);
            }
        }

        // Image boundaries and canvas extent make size mismatches explicit.
        foreach (ImageViewport viewport in viewports)
        {
            CanvasBitmap? image = viewport.ImageIndex == 0 ? state.ImageA : state.ImageB;
            if (image is null)
            {
                continue;
            }

            using (session.CreateLayer(1f, viewport.Bounds))
            {
                Rect imageBounds = ImageRect(viewport.Bounds, state, (int)image.SizeInPixels.Width, (int)image.SizeInPixels.Height);
                session.DrawRectangle(imageBounds, state.CanvasOutline, 1f);

                if (state.CanvasWidth > 0 && state.CanvasHeight > 0)
                {
                    Rect canvasBounds = ImageRect(viewport.Bounds, state, state.CanvasWidth, state.CanvasHeight);
                    if (canvasBounds.Width > imageBounds.Width + 0.5 || canvasBounds.Height > imageBounds.Height + 0.5)
                    {
                        Color outline = viewport.ImageIndex == 0 ? state.RemovedOutline : state.AddedOutline;
                        using CanvasStrokeStyle dashed = new() { DashStyle = CanvasDashStyle.Dash };
                        session.DrawRectangle(canvasBounds, outline, 1f, dashed);
                    }
                }
            }
        }

        if (state.Mode == ComparisonMode.SideBySide && hasA && hasB)
        {
            DrawModeDivider(session, width, height, state);
        }
        else if (hasA && hasB)
        {
            ImageViewport viewport = viewports[0];
            switch (state.Mode)
            {
                case ComparisonMode.Wipe:
                    DrawWipe(session, viewport.Bounds, state);
                    break;
                case ComparisonMode.Overlay:
                    DrawImage(session, viewport.Bounds, state, state.ImageB!, (float)Math.Clamp(state.OverlayOpacity, 0, 1));
                    break;
                case ComparisonMode.Heatmap:
                    if (state.HeatmapOverlay is not null)
                    {
                        DrawImage(session, viewport.Bounds, state, state.HeatmapOverlay, 1f);
                    }

                    break;
            }
        }

        DrawRegions(session, viewports, state);
        DrawViewportLabels(session, viewports, state);
    }

    /// <summary>Gets the viewport under a point, or null outside all viewports.</summary>
    public static ImageViewport? HitTestViewport(
        IReadOnlyList<ImageViewport> viewports,
        double x,
        double y)
    {
        foreach (ImageViewport viewport in viewports)
        {
            if (viewport.Bounds.Contains(new Point(x, y)))
            {
                return viewport;
            }
        }

        return null;
    }

    public static Rect ImageRect(Rect viewport, ComparisonRenderState state, int imageWidth, int imageHeight)
    {
        (double x, double y) = state.View.ImageToViewport(0, 0);
        return new Rect(
            viewport.X + x,
            viewport.Y + y,
            imageWidth * state.View.Zoom,
            imageHeight * state.View.Zoom);
    }

    private static void DrawImage(
        CanvasDrawingSession session,
        Rect viewport,
        ComparisonRenderState state,
        CanvasBitmap image,
        float opacity)
    {
        using (session.CreateLayer(opacity, viewport))
        {
            Rect destination = ImageRect(viewport, state, (int)image.SizeInPixels.Width, (int)image.SizeInPixels.Height);
            Rect source = new(0, 0, (int)image.SizeInPixels.Width, (int)image.SizeInPixels.Height);
            session.DrawImage(
                image,
                destination,
                source,
                1f,
                CanvasImageInterpolation.Linear);
        }
    }

    private static void DrawModeDivider(CanvasDrawingSession session, double width, double height, ComparisonRenderState state)
    {
        double x = (width - CanvasGeometry.ViewportGap) / 2;
        session.FillRectangle(new Rect(x, 0, CanvasGeometry.ViewportGap, height), state.Accent);
    }

    private static void DrawWipe(CanvasDrawingSession session, Rect viewport, ComparisonRenderState state)
    {
        double split = viewport.X + (Math.Clamp(state.WipeFraction, 0.02, 0.98) * viewport.Width);
        Rect clip = new(split, viewport.Y, viewport.Right - split, viewport.Height);
        if (clip.Width > 0)
        {
            DrawImage(session, clip, state, state.ImageB!, 1f);
        }

        session.FillRectangle(new Rect(split - 0.75, viewport.Y, 1.5, viewport.Height), state.Accent);

        // Visible handle: circle plus chevrons, independent of color alone.
        double centerY = viewport.Y + (viewport.Height / 2);
        session.FillCircle((float)split, (float)centerY, 11f, state.Accent);
        session.FillCircle((float)split, (float)centerY, 8f, Color.FromArgb(255, 255, 255, 255));
        session.DrawLine(
            (float)(split - 3.5),
            (float)centerY,
            (float)(split - 1),
            (float)(centerY - 3),
            state.Accent,
            1.5f);
        session.DrawLine(
            (float)(split - 3.5),
            (float)centerY,
            (float)(split - 1),
            (float)(centerY + 3),
            state.Accent,
            1.5f);
        session.DrawLine(
            (float)(split + 3.5),
            (float)centerY,
            (float)(split + 1),
            (float)(centerY - 3),
            state.Accent,
            1.5f);
        session.DrawLine(
            (float)(split + 3.5),
            (float)centerY,
            (float)(split + 1),
            (float)(centerY + 3),
            state.Accent,
            1.5f);
    }

    private static void DrawRegions(
        CanvasDrawingSession session,
        IReadOnlyList<ImageViewport> viewports,
        ComparisonRenderState state)
    {
        if (state.Regions.Count == 0 && state.PendingRegion is null)
        {
            return;
        }

        using CanvasStrokeStyle dashed = new() { DashStyle = CanvasDashStyle.Dash };

        foreach (ImageViewport viewport in viewports)
        {
            using (session.CreateLayer(1f, viewport.Bounds))
            {
                for (int i = 0; i < state.Regions.Count; i++)
                {
                    IgnoreRegion region = state.Regions[i];
                    bool selected = state.SelectedRegionId == region.Id;
                    Rect rect = new Rect(
                        ImageRect(viewport.Bounds, state, 0, 0).X + (region.X * state.View.Zoom),
                        ImageRect(viewport.Bounds, state, 0, 0).Y + (region.Y * state.View.Zoom),
                        region.Width * state.View.Zoom,
                        region.Height * state.View.Zoom);

                    Color stroke = selected ? state.RegionSelectedStroke : state.RegionStroke;
                    float thickness = selected ? 2.5f : 1.25f;
                    session.DrawRectangle(rect, stroke, thickness);

                    // Second inset outline provides a non-color selection cue.
                    if (selected)
                    {
                        session.DrawRectangle(
                            new Rect(rect.X + 2, rect.Y + 2, Math.Max(0, rect.Width - 4), Math.Max(0, rect.Height - 4)),
                            stroke,
                            1f,
                            dashed);

                        const float handle = 6f;
                        foreach (Point corner in new[]
                        {
                            new Point(rect.Left, rect.Top),
                            new Point(rect.Right, rect.Top),
                            new Point(rect.Left, rect.Bottom),
                            new Point(rect.Right, rect.Bottom),
                        })
                        {
                            session.FillRectangle(
                                new Rect(corner.X - (handle / 2), corner.Y - (handle / 2), handle, handle),
                                stroke);
                        }
                    }

                    DrawRegionLabel(session, state, rect, $"R{i + 1}", selected);
                }

                if (state.PendingRegion is { } pending)
                {
                    Rect pendingRect = ImageRect(viewport.Bounds, state, state.CanvasWidth, state.CanvasHeight);
                    pendingRect = new Rect(
                        pendingRect.X + (pending.X * state.View.Zoom),
                        pendingRect.Y + (pending.Y * state.View.Zoom),
                        pending.Width * state.View.Zoom,
                        pending.Height * state.View.Zoom);

                    session.FillRectangle(pendingRect, Color.FromArgb(48, state.Accent.R, state.Accent.G, state.Accent.B));
                    session.DrawRectangle(pendingRect, state.Accent, 1.5f, dashed);
                }
            }
        }
    }

    private static void DrawRegionLabel(
        CanvasDrawingSession session,
        ComparisonRenderState state,
        Rect regionRect,
        string text,
        bool selected)
    {
        if (state.LabelFormat is null)
        {
            return;
        }

        Rect chip = new(
            regionRect.X + 2,
            Math.Max(regionRect.Y + 2, regionRect.Y),
            30,
            18);
        session.FillRoundedRectangle(chip, 3, 3, state.RegionLabelBackground);
        session.DrawText(
            selected ? text + " *" : text,
            chip,
            state.RegionLabelForeground,
            state.LabelFormat);
    }

    private static void DrawViewportLabels(
        CanvasDrawingSession session,
        IReadOnlyList<ImageViewport> viewports,
        ComparisonRenderState state)
    {
        if (state.Mode != ComparisonMode.SideBySide || viewports.Count < 2 || state.LabelFormat is null)
        {
            return;
        }

        foreach (ImageViewport viewport in viewports)
        {
            string text = viewport.ImageIndex == 0 ? "A" : "B";
            Rect chip = new(viewport.Bounds.X + 10, viewport.Bounds.Y + 10, 28, 22);
            session.FillRoundedRectangle(chip, 4, 4, state.RegionLabelBackground);
            session.DrawText(text, chip, state.RegionLabelForeground, state.LabelFormat);
        }
    }
}
