using VisualDiffLab.Core.Comparison;
using VisualDiffLab.Core.Export;
using VisualDiffLab.Core.Models;
using VisualDiffLab.Imaging;
using Windows.Storage;

namespace VisualDiffLab.App.Services;

/// <summary>Composes and encodes exports off the UI thread.</summary>
public static class ExportService
{
    public static async Task ExportDifferenceOverlayAsync(
        StorageFile file,
        DiffResult diff,
        ImageDocument imageA,
        CancellationToken cancellationToken)
    {
        byte[] pixels = await Task.Run(
            () => DifferenceOverlayRenderer.ComposeExport(diff, imageA),
            cancellationToken);
        await PngEncoder.SaveAsync(file, diff.CanvasWidth, diff.CanvasHeight, pixels, cancellationToken);
    }

    public static async Task ExportSideBySideAsync(
        StorageFile file,
        ImageDocument imageA,
        ImageDocument imageB,
        CancellationToken cancellationToken)
    {
        (int width, int height) = SideBySideRenderer.GetOutputSize(imageA, imageB);
        SideBySideRenderer.EnsureSupported(width, height);
        byte[] pixels = await Task.Run(() => SideBySideRenderer.Compose(imageA, imageB), cancellationToken);
        await PngEncoder.SaveAsync(file, width, height, pixels, cancellationToken);
    }
}
