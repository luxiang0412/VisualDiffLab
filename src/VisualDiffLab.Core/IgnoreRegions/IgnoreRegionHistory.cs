namespace VisualDiffLab.Core.IgnoreRegions;

/// <summary>
/// Ordered ignore-region collection with add/remove undo. Every mutation bumps
/// <see cref="Version"/> so stale asynchronous results can be rejected.
/// </summary>
public sealed class IgnoreRegionHistory
{
    private readonly List<IgnoreRegion> _regions = [];
    private readonly Stack<Change> _undoStack = new();

    public event EventHandler? Changed;

    public IReadOnlyList<IgnoreRegion> Regions => _regions;

    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>Increments on every change, including undo.</summary>
    public long Version { get; private set; }

    public IgnoreRegion Add(IgnoreRegion region, int? index = null)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (_regions.Any(r => r.Id == region.Id))
        {
            throw new ArgumentException("A region with the same id already exists.", nameof(region));
        }

        int insertAt = index is >= 0 and var i && i <= _regions.Count ? i : _regions.Count;
        _regions.Insert(insertAt, region);
        _undoStack.Push(new Change(ChangeKind.Add, region, insertAt));
        Version++;
        Changed?.Invoke(this, EventArgs.Empty);
        return region;
    }

    public bool Remove(Guid id)
    {
        int index = _regions.FindIndex(r => r.Id == id);
        if (index < 0)
        {
            return false;
        }

        IgnoreRegion removed = _regions[index];
        _regions.RemoveAt(index);
        _undoStack.Push(new Change(ChangeKind.Remove, removed, index));
        Version++;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Undoes the most recent add or delete. Returns false when nothing to undo.</summary>
    public bool Undo()
    {
        if (_undoStack.Count == 0)
        {
            return false;
        }

        Change change = _undoStack.Pop();
        switch (change.Kind)
        {
            case ChangeKind.Add:
                int index = _regions.FindIndex(r => r.Id == change.Region.Id);
                if (index >= 0)
                {
                    _regions.RemoveAt(index);
                }

                break;

            case ChangeKind.Remove:
                int insertAt = Math.Clamp(change.Index, 0, _regions.Count);
                _regions.Insert(insertAt, change.Region);
                break;

            default:
                throw new InvalidOperationException($"Unknown change kind {change.Kind}.");
        }

        Version++;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Clear()
    {
        if (_regions.Count == 0)
        {
            return;
        }

        _regions.Clear();
        _undoStack.Clear();
        Version++;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private enum ChangeKind
    {
        Add,
        Remove,
    }

    private readonly record struct Change(ChangeKind Kind, IgnoreRegion Region, int Index);
}
