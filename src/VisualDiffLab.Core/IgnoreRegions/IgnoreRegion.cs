namespace VisualDiffLab.Core.IgnoreRegions;

/// <summary>
/// A rectangular area to exclude from difference statistics. Coordinates are in
/// comparison-canvas pixels (origin at the top-left of the canvas), never screen pixels.
/// </summary>
public sealed class IgnoreRegion
{
    public IgnoreRegion(int x, int y, int width, int height, Guid? id = null, string? label = null)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Ignore region width and height must be positive.");
        }

        X = x;
        Y = y;
        Width = width;
        Height = height;
        Id = id ?? Guid.NewGuid();
        Label = label;
    }

    public Guid Id { get; }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }

    public string? Label { get; }

    public int Right => X + Width;

    public int Bottom => Y + Height;

    /// <summary>
    /// Builds a normalized, clipped region from two drag corners. Returns null when the
    /// resulting area is empty after clipping against the canvas.
    /// </summary>
    public static IgnoreRegion? FromCorners(
        int x0,
        int y0,
        int x1,
        int y1,
        int canvasWidth,
        int canvasHeight,
        Guid? id = null,
        string? label = null)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            return null;
        }

        int left = Math.Clamp(Math.Min(x0, x1), 0, canvasWidth);
        int top = Math.Clamp(Math.Min(y0, y1), 0, canvasHeight);
        int right = Math.Clamp(Math.Max(x0, x1), 0, canvasWidth);
        int bottom = Math.Clamp(Math.Max(y0, y1), 0, canvasHeight);

        if (right <= left || bottom <= top)
        {
            return null;
        }

        return new IgnoreRegion(left, top, right - left, bottom - top, id, label);
    }

    /// <summary>Returns a copy clipped to the given canvas, or null when fully outside.</summary>
    public IgnoreRegion? Clip(int canvasWidth, int canvasHeight)
    {
        int left = Math.Max(0, X);
        int top = Math.Max(0, Y);
        int right = Math.Min(canvasWidth, Right);
        int bottom = Math.Min(canvasHeight, Bottom);
        if (right <= left || bottom <= top)
        {
            return null;
        }

        return new IgnoreRegion(left, top, right - left, bottom - top, Id, Label);
    }

    public bool Contains(int x, int y) => x >= X && y >= Y && x < Right && y < Bottom;

    public override string ToString() =>
        $"{(string.IsNullOrWhiteSpace(Label) ? "Region" : Label)} ({X}, {Y}) {Width}x{Height}";
}
