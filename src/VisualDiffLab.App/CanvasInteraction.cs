using System.Diagnostics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using VisualDiffLab.App.Services;
using VisualDiffLab.App.ViewModels;
using VisualDiffLab.Core.Comparison;
using VisualDiffLab.Core.Export;
using VisualDiffLab.Core.IgnoreRegions;
using VisualDiffLab.Core.Models;
using VisualDiffLab.Rendering;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.System;

namespace VisualDiffLab.App;

/// <summary>
/// Canvas lifecycle, rendering synchronization and all pointer/keyboard interaction.
/// Screen coordinates are always converted through <see cref="ViewTransform"/> so
/// ignore regions stay in image space under any zoom, pan, mode or DPI.
/// </summary>
public sealed partial class MainWindow
{
    private bool _spaceHeld;
    private bool _isPanning;
    private bool _isDraggingRegion;
    private bool _isDraggingWipe;
    private Point _lastPointerPosition;
    private Point _regionStartImage;
    private Point _regionCurrentImage;
    private Rect _activeViewportBounds;

    private double RasterizationScale => RootGrid.XamlRoot?.RasterizationScale ?? 1.0;

    private void Canvas_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
    {
        _canvasReady = true;
        _renderState.HeatmapOverlay = null;
        _overlayBitmapWidth = 0;
        _overlayBitmapHeight = 0;
        UploadDocumentBitmaps();
        RefreshOverlayBitmap();
    }

    private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (!_canvasReady)
        {
            return;
        }

        SyncRenderState();
        ComparisonRenderer.Draw(args.DrawingSession, sender.ActualWidth, sender.ActualHeight, _renderState);

        if (!_modeFrameLogged && _modeChangeTimestamp != 0)
        {
            _modeFrameLogged = true;
            double elapsed = Stopwatch.GetElapsedTime(_modeChangeTimestamp).TotalMilliseconds;
            _modeChangeTimestamp = 0;
            DiagnosticsLog.Shared.Info($"Mode first frame: mode={ViewModel.Mode} durationMs={elapsed:F1}");
        }
    }

    private void SyncRenderState()
    {
        _renderState.Mode = ViewModel.Mode;
        _renderState.OverlayOpacity = ViewModel.OverlayOpacityValue / 100.0;
        _renderState.WipeFraction = ViewModel.WipePositionValue / 100.0;
        _renderState.Regions = _regionSnapshot;
        _renderState.SelectedRegionId = ViewModel.SelectedRegionId;
        _renderState.IsRegionMode = ViewModel.IsRegionMode;
        _renderState.CanvasWidth = CanvasPixelSize.Width;
        _renderState.CanvasHeight = CanvasPixelSize.Height;
    }

    private (int Width, int Height) CanvasPixelSize
    {
        get
        {
            int width = Math.Max(ViewModel.ImageA?.Width ?? 0, ViewModel.ImageB?.Width ?? 0);
            int height = Math.Max(ViewModel.ImageA?.Height ?? 0, ViewModel.ImageB?.Height ?? 0);
            return (width, height);
        }
    }

    private IReadOnlyList<ImageViewport> GetViewports() =>
        CanvasGeometry.GetViewports(
            Canvas.ActualWidth,
            Canvas.ActualHeight,
            ViewModel.Mode,
            ViewModel.ImageA is not null,
            ViewModel.ImageB is not null);

    private ImageViewport? FindViewport(Point position)
    {
        foreach (ImageViewport viewport in GetViewports())
        {
            if (viewport.Bounds.Contains(position))
            {
                return viewport;
            }
        }

        return null;
    }

    private void UploadDocumentBitmaps()
    {
        _renderState.CanvasWidth = CanvasPixelSize.Width;
        _renderState.CanvasHeight = CanvasPixelSize.Height;

        if (!_canvasReady)
        {
            return;
        }

        try
        {
            CanvasBitmap? previousA = _renderState.ImageA;
            CanvasBitmap? previousB = _renderState.ImageB;
            _renderState.ImageA = CreateBitmap(ViewModel.ImageA);
            _renderState.ImageB = CreateBitmap(ViewModel.ImageB);
            DisposeBitmap(previousA);
            DisposeBitmap(previousB);

            if (ViewModel.ImageA is null && ViewModel.ImageB is null)
            {
                CanvasBitmap? previousOverlay = _renderState.HeatmapOverlay;
                _renderState.HeatmapOverlay = null;
                _overlayBitmapWidth = 0;
                _overlayBitmapHeight = 0;
                _overlayScratch = null;
                DisposeBitmap(previousOverlay);
            }

            Canvas.Invalidate();
        }
        catch (Exception exception)
        {
            DiagnosticsLog.Shared.Error("Failed to create canvas bitmaps", exception);
            ViewModel.SetTransientMessage("Message_DisplayFailed");
        }
    }

    private CanvasBitmap? CreateBitmap(ImageDocument? document)
    {
        if (document is null)
        {
            return null;
        }

        return CanvasBitmap.CreateFromBytes(
            Canvas,
            document.Pixels,
            document.Width,
            document.Height,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            96f);
    }

    private void ReleaseOverlayResources()
    {
        CanvasBitmap? previous = _renderState.HeatmapOverlay;
        _renderState.HeatmapOverlay = null;
        _overlayBitmapWidth = 0;
        _overlayBitmapHeight = 0;
        _overlayScratch = null;
        DisposeBitmap(previous);
    }

    /// <summary>
    /// Rebuilds the heatmap overlay into a reused scratch buffer and updates a reused
    /// CanvasBitmap. This keeps memory flat across threshold changes on 4K images.
    /// </summary>
    private void RefreshOverlayBitmap()
    {
        if (!_canvasReady)
        {
            return;
        }

        DiffResult? diff = ViewModel.CurrentDiff;
        if (diff is null)
        {
            ReleaseOverlayResources();
            return;
        }

        int width = diff.CanvasWidth;
        int height = diff.CanvasHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        try
        {
            int requiredBytes = checked(width * height * 4);
            if (_overlayScratch is null || _overlayScratch.Length != requiredBytes)
            {
                _overlayScratch = new byte[requiredBytes];
            }

            DifferenceOverlayRenderer.BuildHeatmapOverlay(diff, _overlayScratch);

            bool needsNewBitmap = _renderState.HeatmapOverlay is null ||
                                  _overlayBitmapWidth != width ||
                                  _overlayBitmapHeight != height;
            if (needsNewBitmap)
            {
                CanvasBitmap? previous = _renderState.HeatmapOverlay;
                _renderState.HeatmapOverlay = null;
                DisposeBitmap(previous);
                _renderState.HeatmapOverlay = CanvasBitmap.CreateFromBytes(
                    Canvas,
                    _overlayScratch,
                    width,
                    height,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    96f);
                _overlayBitmapWidth = width;
                _overlayBitmapHeight = height;
            }
            else if (_renderState.HeatmapOverlay is { } existingOverlay)
            {
                existingOverlay.SetPixelBytes(_overlayScratch);
            }
        }
        catch (Exception exception)
        {
            DiagnosticsLog.Shared.Error("Failed to update heatmap bitmap", exception);
        }
    }

    private void UpdateViewportLayout(bool keepCenter)
    {
        ViewTransform view = _renderState.View;
        (int contentWidth, int contentHeight) = CanvasPixelSize;
        bool hasCenter = keepCenter &&
                         _viewInitialized &&
                         view.ContentWidth > 0 &&
                         view.ContentHeight > 0 &&
                         view.ViewportWidth > 0 &&
                         view.ViewportHeight > 0;

        double centerX = 0;
        double centerY = 0;
        if (hasCenter)
        {
            (centerX, centerY) = view.ViewportToImage(view.ViewportWidth / 2, view.ViewportHeight / 2);
        }

        IReadOnlyList<ImageViewport> viewports = GetViewports();
        if (viewports.Count == 0)
        {
            return;
        }

        ImageViewport primary = viewports[0];
        view.SetViewport(primary.Bounds.Width, primary.Bounds.Height);
        view.SetContent(contentWidth, contentHeight);
        if (hasCenter)
        {
            view.CenterOnImagePoint(centerX, centerY);
        }

        UpdateZoomText();
        Canvas.Invalidate();
    }

    private void FitToView()
    {
        IReadOnlyList<ImageViewport> viewports = GetViewports();
        if (viewports.Count == 0)
        {
            return;
        }

        (int contentWidth, int contentHeight) = CanvasPixelSize;
        if (contentWidth <= 0 || contentHeight <= 0)
        {
            return;
        }

        ViewTransform view = _renderState.View;
        view.SetViewport(viewports[0].Bounds.Width, viewports[0].Bounds.Height);
        view.SetContent(contentWidth, contentHeight);
        view.Fit();
        _viewInitialized = true;
        UpdateZoomText();
        Canvas.Invalidate();
    }

    private void ActualSize()
    {
        IReadOnlyList<ImageViewport> viewports = GetViewports();
        if (viewports.Count == 0)
        {
            return;
        }

        (int contentWidth, int contentHeight) = CanvasPixelSize;
        if (contentWidth <= 0 || contentHeight <= 0)
        {
            return;
        }

        ViewTransform view = _renderState.View;
        view.SetViewport(viewports[0].Bounds.Width, viewports[0].Bounds.Height);
        view.SetContent(contentWidth, contentHeight);
        view.SetActualSize(RasterizationScale);
        _viewInitialized = true;
        UpdateZoomText();
        Canvas.Invalidate();
    }

    private void UpdateZoomText() =>
        ViewModel.UpdateZoom(_renderState.View.GetDisplayZoomPercent(RasterizationScale));

    // ---------------------------------------------------------------------
    // Pointer interaction
    // ---------------------------------------------------------------------

    private void Canvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(Canvas);
        ImageViewport? viewport = FindViewport(point.Position);
        if (viewport is null)
        {
            return;
        }

        double localX = point.Position.X - viewport.Value.Bounds.X;
        double localY = point.Position.Y - viewport.Value.Bounds.Y;
        int delta = point.Properties.MouseWheelDelta;
        double factor = Math.Pow(1.1, delta / 120.0);
        _renderState.View.ZoomBy(factor, localX, localY);
        _viewInitialized = true;
        UpdateZoomText();
        Canvas.Invalidate();
        e.Handled = true;
    }

    private void Canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(Canvas);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        Canvas.Focus(FocusState.Pointer);
        ImageViewport? viewport = FindViewport(point.Position);
        if (viewport is null)
        {
            return;
        }

        if (_spaceHeld)
        {
            _isPanning = true;
            _lastPointerPosition = point.Position;
            Canvas.CapturePointer(e.Pointer);
            e.Handled = true;
            return;
        }

        if (ViewModel.IsRegionMode)
        {
            BeginRegion(e, viewport.Value, point.Position);
            e.Handled = true;
            return;
        }

        if (ViewModel.Mode == ComparisonMode.Wipe &&
            ViewModel.CanCompare &&
            IsNearWipeHandle(viewport.Value, point.Position))
        {
            _isDraggingWipe = true;
            _activeViewportBounds = viewport.Value.Bounds;
            Canvas.CapturePointer(e.Pointer);
            UpdateWipeFromPointer(point.Position);
            e.Handled = true;
            return;
        }

        SelectRegionAt(viewport.Value, point.Position);
    }

    private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(Canvas);

        if (_isPanning)
        {
            double deltaX = point.Position.X - _lastPointerPosition.X;
            double deltaY = point.Position.Y - _lastPointerPosition.Y;
            _renderState.View.PanBy(deltaX, deltaY);
            _lastPointerPosition = point.Position;
            Canvas.Invalidate();
            return;
        }

        if (_isDraggingRegion)
        {
            if (TryGetImagePoint(_activeViewportBounds, point.Position, out double imageX, out double imageY))
            {
                _regionCurrentImage = new Point(imageX, imageY);
                _renderState.PendingRegion = NormalizeRegionRect(_regionStartImage, _regionCurrentImage);
                Canvas.Invalidate();
            }

            return;
        }

        if (_isDraggingWipe)
        {
            UpdateWipeFromPointer(point.Position);
        }
    }

    private void Canvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(Canvas);
        if (_isDraggingRegion)
        {
            FinishRegion(point.Position);
        }

        _isPanning = false;
        _isDraggingWipe = false;
        Canvas.ReleasePointerCapture(e.Pointer);
        Canvas.Invalidate();
        e.Handled = true;
    }

    private void Canvas_PointerCanceled(object sender, PointerRoutedEventArgs e) =>
        CancelActiveDrag(clearPendingRegion: true);

    private void Canvas_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isPanning = false;
        _isDraggingWipe = false;
        _isDraggingRegion = false;
        _renderState.PendingRegion = null;
        Canvas.Invalidate();
    }

    private void Canvas_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.IsRegionMode)
        {
            return;
        }

        FitToView();
        e.Handled = true;
    }

    private void CancelActiveDrag(bool clearPendingRegion)
    {
        _isPanning = false;
        _isDraggingWipe = false;
        _isDraggingRegion = false;
        if (clearPendingRegion)
        {
            _renderState.PendingRegion = null;
        }

        try
        {
            Canvas.ReleasePointerCaptures();
        }
        catch (Exception)
        {
            // No active capture to release.
        }

        Canvas.Invalidate();
    }

    private bool IsNearWipeHandle(ImageViewport viewport, Point position)
    {
        double fraction = Math.Clamp(ViewModel.WipePositionValue / 100.0, 0.02, 0.98);
        double splitX = viewport.Bounds.X + (fraction * viewport.Bounds.Width);
        return Math.Abs(position.X - splitX) <= 14;
    }

    private void UpdateWipeFromPointer(Point position)
    {
        if (_activeViewportBounds.Width <= 0)
        {
            return;
        }

        double fraction = (position.X - _activeViewportBounds.X) / _activeViewportBounds.Width;
        ViewModel.WipePositionValue = Math.Clamp(fraction * 100.0, 2, 98);
        Canvas.Invalidate();
    }

    private void BeginRegion(PointerRoutedEventArgs e, ImageViewport viewport, Point position)
    {
        if (!TryGetImagePoint(viewport.Bounds, position, out double imageX, out double imageY))
        {
            return;
        }

        _activeViewportBounds = viewport.Bounds;
        _regionStartImage = new Point(imageX, imageY);
        _regionCurrentImage = _regionStartImage;
        _renderState.PendingRegion = new Rect(_regionStartImage, _regionStartImage);
        _isDraggingRegion = true;
        Canvas.CapturePointer(e.Pointer);
        Canvas.Invalidate();
    }

    private void FinishRegion(Point releasePosition)
    {
        if (!_isDraggingRegion)
        {
            return;
        }

        if (TryGetImagePoint(_activeViewportBounds, releasePosition, out double imageX, out double imageY))
        {
            _regionCurrentImage = new Point(imageX, imageY);
        }

        _isDraggingRegion = false;
        _renderState.PendingRegion = null;

        (int width, int height) = CanvasPixelSize;
        int x0 = (int)Math.Round(_regionStartImage.X);
        int y0 = (int)Math.Round(_regionStartImage.Y);
        int x1 = (int)Math.Round(_regionCurrentImage.X);
        int y1 = (int)Math.Round(_regionCurrentImage.Y);

        IgnoreRegion? region = IgnoreRegion.FromCorners(x0, y0, x1, y1, width, height);
        if (region is not null)
        {
            ViewModel.AddRegion(region);
        }

        Canvas.Invalidate();
    }

    private bool TryGetImagePoint(Rect viewport, Point position, out double imageX, out double imageY)
    {
        (double x, double y) = _renderState.View.ViewportToImage(
            position.X - viewport.X,
            position.Y - viewport.Y);

        (int width, int height) = CanvasPixelSize;
        imageX = Math.Clamp(x, 0, width);
        imageY = Math.Clamp(y, 0, height);
        return width > 0 && height > 0;
    }

    private static Rect NormalizeRegionRect(Point start, Point end)
    {
        double left = Math.Min(start.X, end.X);
        double top = Math.Min(start.Y, end.Y);
        return new Rect(left, top, Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
    }

    private void SelectRegionAt(ImageViewport viewport, Point position)
    {
        if (!TryGetImagePoint(viewport.Bounds, position, out double imageX, out double imageY))
        {
            return;
        }

        int x = (int)Math.Floor(imageX);
        int y = (int)Math.Floor(imageY);
        RegionItemViewModel? hit = null;
        for (int i = ViewModel.Regions.Count - 1; i >= 0; i--)
        {
            RegionItemViewModel candidate = ViewModel.Regions[i];
            if (candidate.Region.Contains(x, y))
            {
                hit = candidate;
                break;
            }
        }

        ViewModel.SelectedRegion = hit;
        Canvas.Invalidate();
    }

    // ---------------------------------------------------------------------
    // Keyboard
    // ---------------------------------------------------------------------

    private void Canvas_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Space:
                if (!_spaceHeld)
                {
                    _spaceHeld = true;
                }

                e.Handled = true;
                break;

            case VirtualKey.Escape:
                if (_isDraggingRegion)
                {
                    CancelActiveDrag(clearPendingRegion: true);
                }
                else if (ViewModel.IsRegionMode)
                {
                    ViewModel.IsRegionMode = false;
                }
                else if (ViewModel.SelectedRegion is not null)
                {
                    ViewModel.SelectedRegion = null;
                }

                e.Handled = true;
                break;

            case VirtualKey.Number1:
                ViewModel.Mode = ComparisonMode.SideBySide;
                e.Handled = true;
                break;

            case VirtualKey.Number2:
                ViewModel.Mode = ComparisonMode.Wipe;
                e.Handled = true;
                break;

            case VirtualKey.Number3:
                ViewModel.Mode = ComparisonMode.Overlay;
                e.Handled = true;
                break;

            case VirtualKey.Number4:
                ViewModel.Mode = ComparisonMode.Heatmap;
                e.Handled = true;
                break;

            case VirtualKey.F:
                FitToView();
                e.Handled = true;
                break;

            case VirtualKey.Number0:
                ActualSize();
                e.Handled = true;
                break;

            case VirtualKey.Delete:
                if (ViewModel.SelectedRegion is { } selected)
                {
                    ViewModel.RemoveRegion(selected.Id);
                    e.Handled = true;
                }

                break;

            case VirtualKey.Left:
                if (ViewModel.Mode == ComparisonMode.Wipe)
                {
                    ViewModel.WipePositionValue = Math.Max(2, ViewModel.WipePositionValue - 1);
                    Canvas.Invalidate();
                    e.Handled = true;
                }

                break;

            case VirtualKey.Right:
                if (ViewModel.Mode == ComparisonMode.Wipe)
                {
                    ViewModel.WipePositionValue = Math.Min(98, ViewModel.WipePositionValue + 1);
                    Canvas.Invalidate();
                    e.Handled = true;
                }

                break;
        }
    }

    private void Canvas_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Space)
        {
            _spaceHeld = false;
            e.Handled = true;
        }
    }
}
