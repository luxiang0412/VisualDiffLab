using VisualDiffLab.Core.IgnoreRegions;

namespace VisualDiffLab.Core.Comparison;

/// <summary>Immutable settings for one comparison run.</summary>
public sealed class DiffOptions
{
    public const int MinThreshold = 0;
    public const int MaxThreshold = 64;
    public const int DefaultThreshold = 16;

    public static readonly DiffOptions Default = new(DefaultThreshold, Array.Empty<IgnoreRegion>());

    public DiffOptions(int threshold, IReadOnlyList<IgnoreRegion>? ignoreRegions)
    {
        if (threshold is < MinThreshold or > MaxThreshold)
        {
            throw new ArgumentOutOfRangeException(
                nameof(threshold),
                $"Threshold must be between {MinThreshold} and {MaxThreshold} (got {threshold}).");
        }

        Threshold = threshold;
        IgnoreRegions = ignoreRegions ?? Array.Empty<IgnoreRegion>();
    }

    /// <summary>Maximum per-channel difference (0-64) that is still considered "same".</summary>
    public int Threshold { get; }

    /// <summary>Regions excluded from the comparison, in comparison-canvas coordinates.</summary>
    public IReadOnlyList<IgnoreRegion> IgnoreRegions { get; }
}
