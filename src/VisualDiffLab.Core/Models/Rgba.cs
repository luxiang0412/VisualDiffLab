namespace VisualDiffLab.Core.Models;

/// <summary>A straight (non-premultiplied) sRGB color.</summary>
public readonly record struct Rgba(byte R, byte G, byte B, byte A = 255)
{
    public static Rgba FromHex(uint rgb) =>
        new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
}
