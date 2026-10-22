using System;
using System.Linq;
using VisualDiffLab.Core.IgnoreRegions;
using Xunit;

namespace VisualDiffLab.Core.Tests;

public class IgnoreRegionTests
{
    [Fact]
    public void FromCorners_NormalizesDragDirection()
    {
        IgnoreRegion? region = IgnoreRegion.FromCorners(30, 40, 10, 20, 100, 100);

        Assert.NotNull(region);
        Assert.Equal(10, region!.X);
        Assert.Equal(20, region.Y);
        Assert.Equal(20, region.Width);
        Assert.Equal(20, region.Height);
    }

    [Fact]
    public void FromCorners_ClipsToCanvas()
    {
        IgnoreRegion? region = IgnoreRegion.FromCorners(-20, -10, 30, 40, 100, 100);

        Assert.NotNull(region);
        Assert.Equal(new IgnoreRegion(0, 0, 30, 40).X, region!.X);
        Assert.Equal(0, region.Y);
        Assert.Equal(30, region.Width);
        Assert.Equal(40, region.Height);
    }

    [Fact]
    public void FromCorners_ReturnsNullWhenEmptyAfterClipping()
    {
        Assert.Null(IgnoreRegion.FromCorners(10, 10, 10, 40, 100, 100));
        Assert.Null(IgnoreRegion.FromCorners(200, 200, 300, 300, 100, 100));
        Assert.Null(IgnoreRegion.FromCorners(0, 0, 10, 10, 0, 0));
    }

    [Fact]
    public void Clip_ReturnsNullWhenFullyOutside()
    {
        IgnoreRegion region = new(200, 200, 10, 10);

        Assert.Null(region.Clip(100, 100));
    }

    [Fact]
    public void InvalidRegionSize_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IgnoreRegion(0, 0, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IgnoreRegion(0, 0, 10, -1));
    }
}

public class IgnoreRegionHistoryTests
{
    [Fact]
    public void AddAndUndo_RestoreEmptyCollection()
    {
        IgnoreRegionHistory history = new();
        IgnoreRegion region = new(1, 2, 3, 4);

        history.Add(region);
        Assert.Single(history.Regions);
        Assert.True(history.CanUndo);

        Assert.True(history.Undo());
        Assert.Empty(history.Regions);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void RemoveAndUndo_RestoreOriginalPosition()
    {
        IgnoreRegionHistory history = new();
        IgnoreRegion first = new(0, 0, 1, 1, label: "first");
        IgnoreRegion second = new(1, 1, 1, 1, label: "second");
        IgnoreRegion third = new(2, 2, 1, 1, label: "third");
        history.Add(first);
        history.Add(second);
        history.Add(third);

        Assert.True(history.Remove(second.Id));
        Assert.Equal(new[] { "first", "third" }, history.Regions.Select(r => r.Label));

        Assert.True(history.Undo());
        Assert.Equal(new[] { "first", "second", "third" }, history.Regions.Select(r => r.Label));
    }

    [Fact]
    public void VersionIncrementsOnEveryChangeIncludingUndo()
    {
        IgnoreRegionHistory history = new();
        long start = history.Version;

        IgnoreRegion region = history.Add(new IgnoreRegion(0, 0, 5, 5));
        Assert.Equal(start + 1, history.Version);

        history.Remove(region.Id);
        Assert.Equal(start + 2, history.Version);

        history.Undo();
        Assert.Equal(start + 3, history.Version);
    }

    [Fact]
    public void DuplicateId_IsRejected()
    {
        IgnoreRegionHistory history = new();
        IgnoreRegion region = new(0, 0, 5, 5);
        history.Add(region);

        Assert.Throws<ArgumentException>(() => history.Add(new IgnoreRegion(1, 1, 2, 2, region.Id)));
    }

    [Fact]
    public void ChangedEventFires()
    {
        IgnoreRegionHistory history = new();
        int raised = 0;
        history.Changed += (_, _) => raised++;

        IgnoreRegion region = history.Add(new IgnoreRegion(0, 0, 5, 5));
        history.Remove(region.Id);
        history.Undo();

        Assert.Equal(3, raised);
    }

    [Fact]
    public void UndoOnEmptyHistory_ReturnsFalse()
    {
        Assert.False(new IgnoreRegionHistory().Undo());
    }
}
