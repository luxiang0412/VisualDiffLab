namespace VisualDiffLab.Core.Comparison;

/// <summary>Per-pixel outcome of a comparison. Stored as one byte per canvas pixel.</summary>
public enum DiffClassification : byte
{
    Same = 0,
    Changed = 1,
    OnlyInA = 2,
    OnlyInB = 3,
    Ignored = 4,
}
