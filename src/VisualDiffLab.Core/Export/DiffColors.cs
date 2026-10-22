using VisualDiffLab.Core.Models;

namespace VisualDiffLab.Core.Export;

/// <summary>
/// Fixed, documented colors shared by the heatmap, difference overlay export and
/// side-by-side export. Changing these constants is a product decision.
/// </summary>
public static class DiffColors
{
    public const byte OverlayAlpha = 217; // 85% opacity

    public static readonly Rgba Changed = Rgba.FromHex(0xE3008C); // magenta
    public static readonly Rgba Added = Rgba.FromHex(0x00A2FF);   // blue (only in B)
    public static readonly Rgba Removed = Rgba.FromHex(0xFFB900); // amber (only in A)

    public static readonly Rgba DifferenceExportBackground = Rgba.FromHex(0x1F1F1F);
    public static readonly Rgba SideBySideBackground = Rgba.FromHex(0x2B2B2B);
    public static readonly Rgba SideBySideLabelBar = Rgba.FromHex(0x3A3A3A);
    public static readonly Rgba SideBySideLabelText = Rgba.FromHex(0xF5F5F5);
}
