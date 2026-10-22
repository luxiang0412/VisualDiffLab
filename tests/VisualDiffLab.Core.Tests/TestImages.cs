using System;
using VisualDiffLab.Core.Models;

namespace VisualDiffLab.Core.Tests;

internal static class TestImages
{
    public static ImageDocument Solid(int width, int height, Rgba color, string fileName = "image.png") =>
        Create(width, height, (_, _) => color, fileName: fileName);

    public static ImageDocument Create(
        int width,
        int height,
        Func<int, int, Rgba> pixel,
        string format = "PNG",
        string fileName = "image.png")
    {
        byte[] buffer = new byte[(long)width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Rgba color = pixel(x, y);
                int index = ((y * width) + x) * 4;
                buffer[index] = Premultiply(color.B, color.A);
                buffer[index + 1] = Premultiply(color.G, color.A);
                buffer[index + 2] = Premultiply(color.R, color.A);
                buffer[index + 3] = color.A;
            }
        }

        return new ImageDocument(fileName, format, width, height, buffer);
    }

    public static Rgba GetPixel(byte[] bgra, int width, int x, int y)
    {
        int index = ((y * width) + x) * 4;
        return new Rgba(bgra[index + 2], bgra[index + 1], bgra[index], bgra[index + 3]);
    }

    public static byte Premultiply(byte value, byte alpha) => (byte)((value * alpha + 127) / 255);
}
