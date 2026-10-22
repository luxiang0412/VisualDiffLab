using VisualDiffLab.App.Localization;
using VisualDiffLab.Core.IgnoreRegions;

namespace VisualDiffLab.App.ViewModels;

public sealed class RegionItemViewModel
{
    public RegionItemViewModel(IgnoreRegion region, int index, LocalizationService loc)
    {
        Region = region;
        Index = index;
        Label = $"R{index}";
        Details = loc.Format("Region_DetailsFormat", region.X, region.Y, region.Width, region.Height);
        DeleteAutomationName = loc.Format("Region_DeleteName", index);
        DeleteToolTip = loc.Translate("Action_DeleteRegion");
    }

    public IgnoreRegion Region { get; }

    public Guid Id => Region.Id;

    public int Index { get; }

    public string Label { get; }

    public string Details { get; }

    public string DeleteAutomationName { get; }

    public string DeleteToolTip { get; }
}
