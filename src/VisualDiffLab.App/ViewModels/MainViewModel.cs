using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Diagnostics;
using VisualDiffLab.App.Localization;
using VisualDiffLab.App.Services;
using VisualDiffLab.Core.Comparison;
using VisualDiffLab.Core.IgnoreRegions;
using VisualDiffLab.Core.Models;
using VisualDiffLab.Core.Export;
using VisualDiffLab.Imaging;
using Windows.Storage;

namespace VisualDiffLab.App.ViewModels;

public sealed record UserError(string Title, string Message);

public sealed class DiffUpdatedEventArgs : EventArgs
{
    public DiffUpdatedEventArgs(DiffResult? diff)
    {
        Diff = diff;
    }

    public DiffResult? Diff { get; }
}

/// <summary>
/// UI state and the asynchronous comparison pipeline. All properties are updated on the
/// UI thread; the pixel work itself runs on the thread pool with cancellation and version
/// checks so a stale result can never overwrite a newer threshold or image.
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private const int CompareDebounceMilliseconds = 120;

    private readonly IgnoreRegionHistory _regionHistory = new();
    private readonly DiagnosticsLog _log;
    private readonly LocalizationService _loc;
    private readonly object _versionGate = new();

    private ImageDocument? _imageA;
    private ImageDocument? _imageB;
    private DiffResult? _currentDiff;
    private CancellationTokenSource? _compareCancellation;
    private long _version;
    private bool _disposed;

    private ComparisonMode _mode = ComparisonMode.SideBySide;
    private int _threshold = DiffOptions.DefaultThreshold;
    private double _overlayOpacity = 50;
    private double _wipePosition = 50;
    private bool _isRegionMode;
    private bool _isBusy;
    private bool _isExporting;
    private bool _isInspectorVisible = true;
    private string _busyKey = "Busy_Working";
    private string? _transientKey;
    private object?[] _transientArgs = Array.Empty<object?>();
    private string _zoomText = "100%";
    private string _differentPixelsText = "—";
    private string _comparedPixelsText = "—";
    private string _ratioText = "—";
    private string _ignoredPixelsText = "—";
    private string _sizeDifferenceText = "—";
    private string _diffSummaryText = "—";
    private RegionItemViewModel? _selectedRegion;

    public MainViewModel(DiagnosticsLog log, LocalizationService loc)
    {
        _log = log;
        _loc = loc;
        _loc.LanguageChanged += OnLanguageChanged;
    }

    public event EventHandler? ImagesChanged;

    public event EventHandler<DiffUpdatedEventArgs>? DiffUpdated;

    public event EventHandler<UserError>? ErrorRaised;

    public event EventHandler? ModeChanged;

    public event EventHandler? FitRequested;

    public ImageDocument? ImageA => _imageA;

    public ImageDocument? ImageB => _imageB;

    public bool HasAnyImage => _imageA is not null || _imageB is not null;

    public bool CanCompare => _imageA is not null && _imageB is not null;

    public bool CanSwap => HasAnyImage;

    public bool CanClear => HasAnyImage;

    public bool CanExport => CanCompare && _currentDiff is not null && !IsBusy && !IsExporting;

    public bool IsThresholdEnabled => CanCompare;

    public bool CanUndoRegion => _regionHistory.CanUndo;

    public bool HasRegions => Regions.Count > 0;

    public DiffResult? CurrentDiff => _currentDiff;

    public ComparisonMode Mode
    {
        get => _mode;
        set
        {
            if (!SetProperty(ref _mode, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ModeName));
            OnPropertyChanged(nameof(OverlayControlsVisibility));
            OnPropertyChanged(nameof(WipeControlsVisibility));
            ModeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string ModeName => _loc.Translate(_mode switch
    {
        ComparisonMode.SideBySide => "Mode_SideBySide",
        ComparisonMode.Wipe => "Mode_Wipe",
        ComparisonMode.Overlay => "Mode_Overlay",
        ComparisonMode.Heatmap => "Mode_Heatmap",
        _ => "Unknown",
    });

    /// <summary>Bound to the slider; 0-64, strict greater-than comparison.</summary>
    public double ThresholdValue
    {
        get => _threshold;
        set
        {
            int rounded = Math.Clamp((int)Math.Round(value), DiffOptions.MinThreshold, DiffOptions.MaxThreshold);
            if (!SetProperty(ref _threshold, rounded))
            {
                return;
            }

            OnPropertyChanged(nameof(ThresholdText));
            ClearStatistics(_loc.Translate("Stat_Comparing"));
            _ = RecomputeAsync(debounce: true);
        }
    }

    public string ThresholdText => _threshold.ToString(_loc.EffectiveCulture);

    public double OverlayOpacityValue
    {
        get => _overlayOpacity;
        set
        {
            if (SetProperty(ref _overlayOpacity, Math.Clamp(value, 0, 100)))
            {
                OnPropertyChanged(nameof(OverlayOpacityText));
            }
        }
    }

    public string OverlayOpacityText => $"{Math.Round(_overlayOpacity):0}%";

    public double WipePositionValue
    {
        get => _wipePosition;
        set
        {
            if (SetProperty(ref _wipePosition, Math.Clamp(value, 2, 98)))
            {
                OnPropertyChanged(nameof(WipePositionText));
            }
        }
    }

    public string WipePositionText => $"{Math.Round(_wipePosition):0}%";

    public bool IsRegionMode
    {
        get => _isRegionMode;
        set
        {
            if (!SetProperty(ref _isRegionMode, value))
            {
                return;
            }

            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusDetailText));
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
            {
                return;
            }

            OnPropertyChanged(nameof(BusyVisibility));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(CanExport));
            OnPropertyChanged(nameof(ExportDifferenceButtonEnabled));
            OnPropertyChanged(nameof(ExportSideBySideButtonEnabled));
        }
    }

    public bool IsExporting
    {
        get => _isExporting;
        private set
        {
            if (!SetProperty(ref _isExporting, value))
            {
                return;
            }

            OnPropertyChanged(nameof(BusyVisibility));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(CanExport));
            OnPropertyChanged(nameof(ExportDifferenceButtonEnabled));
            OnPropertyChanged(nameof(ExportSideBySideButtonEnabled));
        }
    }

    public bool IsInspectorVisible
    {
        get => _isInspectorVisible;
        set
        {
            if (SetProperty(ref _isInspectorVisible, value))
            {
                OnPropertyChanged(nameof(InspectorVisibility));
            }
        }
    }

    public string ZoomText
    {
        get => _zoomText;
        private set => SetProperty(ref _zoomText, value);
    }

    public string ImageAInfo => _imageA is null
        ? _loc.Translate("Info_NotLoaded")
        : _loc.Format("Info_ImageFormat", _imageA.FileName, _imageA.Format, _imageA.Width, _imageA.Height);

    public string ImageBInfo => _imageB is null
        ? _loc.Translate("Info_NotLoaded")
        : _loc.Format("Info_ImageFormat", _imageB.FileName, _imageB.Format, _imageB.Width, _imageB.Height);

    public string CanvasInfo => CanCompare
        ? _loc.Format("Canvas_Size", Math.Max(_imageA!.Width, _imageB!.Width), Math.Max(_imageA.Height, _imageB.Height))
        : _loc.Translate("Canvas_LoadPrompt");

    public string DifferentPixelsText
    {
        get => _differentPixelsText;
        private set => SetProperty(ref _differentPixelsText, value);
    }

    public string ComparedPixelsText
    {
        get => _comparedPixelsText;
        private set => SetProperty(ref _comparedPixelsText, value);
    }

    public string RatioText
    {
        get => _ratioText;
        private set => SetProperty(ref _ratioText, value);
    }

    public string IgnoredPixelsText
    {
        get => _ignoredPixelsText;
        private set => SetProperty(ref _ignoredPixelsText, value);
    }

    public string SizeDifferenceText
    {
        get => _sizeDifferenceText;
        private set => SetProperty(ref _sizeDifferenceText, value);
    }

    public string DiffSummaryText
    {
        get => _diffSummaryText;
        private set => SetProperty(ref _diffSummaryText, value);
    }

    public string TransientMessage =>
        _transientKey is null ? string.Empty : _loc.Format(_transientKey, _transientArgs);

    public string StatusText
    {
        get
        {
            if (IsBusy)
            {
                return _loc.Translate(_busyKey);
            }

            if (IsExporting)
            {
                return _loc.Translate("Busy_Exporting");
            }

            if (!HasAnyImage)
            {
                return _loc.Translate("Status_OpenPrompt");
            }

            if (!CanCompare)
            {
                return _loc.Translate(_imageA is null ? "Status_WaitingForA" : "Status_WaitingForB");
            }

            if (IsRegionMode)
            {
                return _loc.Translate("Status_RegionMode");
            }

            return string.IsNullOrEmpty(TransientMessage) ? _loc.Translate("Status_Ready") : TransientMessage;
        }
    }

    public string StatusDetailText
    {
        get
        {
            if (IsBusy)
            {
                return string.Empty;
            }

            if (IsRegionMode)
            {
                return _loc.Translate("Status_RegionModeDetail");
            }

            return TransientMessage;
        }
    }

    public Visibility EmptyStateVisibility => HasAnyImage ? Visibility.Collapsed : Visibility.Visible;

    public Visibility SingleImageHintVisibility => HasAnyImage && !CanCompare ? Visibility.Visible : Visibility.Collapsed;

    public bool ShowSingleImageHint => HasAnyImage && !CanCompare;

    public string SingleImageHintText => _loc.Translate(_imageA is null ? "Hint_SingleImage_B" : "Hint_SingleImage_A");

    public Visibility BusyVisibility => IsBusy || IsExporting ? Visibility.Visible : Visibility.Collapsed;

    public Visibility InspectorVisibility => IsInspectorVisible ? Visibility.Visible : Visibility.Collapsed;

    public Visibility OverlayControlsVisibility => _mode == ComparisonMode.Overlay ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WipeControlsVisibility => _mode == ComparisonMode.Wipe ? Visibility.Visible : Visibility.Collapsed;

    public bool ExportDifferenceButtonEnabled => CanExport;

    public bool ExportSideBySideButtonEnabled => CanCompare && !IsBusy && !IsExporting;

    public string ExportDifferenceSizeText => _currentDiff is null
        ? "—"
        : $"{_currentDiff.CanvasWidth} × {_currentDiff.CanvasHeight} px";

    public string ExportSideBySideSizeText
    {
        get
        {
            if (!CanCompare)
            {
                return "—";
            }

            (int width, int height) = SideBySideRenderer.GetOutputSize(_imageA!, _imageB!);
            return $"{width} × {height} px";
        }
    }

    public ObservableCollection<RegionItemViewModel> Regions { get; } = [];

    public RegionItemViewModel? SelectedRegion
    {
        get => _selectedRegion;
        set
        {
            if (!SetProperty(ref _selectedRegion, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedRegionId));
        }
    }

    public Guid? SelectedRegionId => _selectedRegion?.Id;

    public async Task LoadImageAsync(bool slotA, StorageFile file)
    {
        if (_disposed)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(file);
        SetBusy(true, "Busy_Loading");
        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            ImageDocument document = await ImageLoader.LoadAsync(file);
            stopwatch.Stop();
            _log.Info($"Image decoded: format={document.Format} size={document.Width}x{document.Height} durationMs={stopwatch.ElapsedMilliseconds}");

            if (slotA)
            {
                _imageA = document;
            }
            else
            {
                _imageB = document;
            }

            NotifyImagesChanged();
            FitRequested?.Invoke(this, EventArgs.Empty);
            await RecomputeAsync(debounce: false);
            SetTransientMessage(slotA ? "Message_ImageALoaded" : "Message_ImageBLoaded");
        }
        catch (ImageLoadException exception)
        {
            _log.Error($"Image load failed: {exception.Kind}", exception);
            RaiseError(_loc.Translate("Error_OpenTitle"), _loc.ImageLoadMessage(exception.Kind));
        }
        catch (Exception exception)
        {
            _log.Error("Image load failed", exception);
            RaiseError(_loc.Translate("Error_OpenTitle"), _loc.Translate("Error_Image_Unknown"));
        }
        finally
        {
            SetBusy(false);
        }
    }

    public async Task LoadImageFromPathAsync(bool slotA, string path)
    {
        if (_disposed)
        {
            return;
        }

        SetBusy(true, "Busy_Loading");
        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            ImageDocument document = await ImageLoader.LoadAsync(Path.GetFullPath(path));
            stopwatch.Stop();
            _log.Info($"Image decoded: format={document.Format} size={document.Width}x{document.Height} durationMs={stopwatch.ElapsedMilliseconds}");

            if (slotA)
            {
                _imageA = document;
            }
            else
            {
                _imageB = document;
            }

            NotifyImagesChanged();
            FitRequested?.Invoke(this, EventArgs.Empty);
            await RecomputeAsync(debounce: false);
            SetTransientMessage(slotA ? "Message_ImageALoaded" : "Message_ImageBLoaded");
        }
        catch (ImageLoadException exception)
        {
            _log.Error($"Image load failed: {exception.Kind}", exception);
            RaiseError(_loc.Translate("Error_OpenTitle"), _loc.ImageLoadMessage(exception.Kind));
        }
        catch (Exception exception)
        {
            _log.Error("Image load failed", exception);
            RaiseError(_loc.Translate("Error_OpenTitle"), _loc.Translate("Error_Image_Unknown"));
        }
        finally
        {
            SetBusy(false);
        }
    }

    public void SwapImages()
    {
        if (_imageA is null && _imageB is null)
        {
            return;
        }

        (_imageA, _imageB) = (_imageB, _imageA);
        SetTransientMessage("Message_Swapped");
        NotifyImagesChanged();
        _ = RecomputeAsync(debounce: false);
    }

    public void ClearComparison()
    {
        _compareCancellation?.Cancel();
        _compareCancellation = null;
        _imageA = null;
        _imageB = null;
        _currentDiff = null;
        _regionHistory.Clear();
        RefreshRegions(null);
        SelectedRegion = null;
        IsRegionMode = false;
        ClearStatistics("—");
        DiffUpdated?.Invoke(this, new DiffUpdatedEventArgs(null));
        SetTransientMessage("Message_Cleared");
        NotifyImagesChanged();
    }

    public void AddRegion(IgnoreRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        _regionHistory.Add(region);
        RefreshRegions(region.Id);
        SetTransientMessage("Message_RegionAdded", Regions.Count);
        _ = RecomputeAsync(debounce: false);
    }

    public void RemoveRegion(Guid id)
    {
        if (!_regionHistory.Remove(id))
        {
            return;
        }

        RefreshRegions(null);
        SetTransientMessage("Message_RegionDeleted");
        _ = RecomputeAsync(debounce: false);
    }

    public void UndoRegionChange()
    {
        if (!_regionHistory.Undo())
        {
            return;
        }

        RefreshRegions(SelectedRegionId);
        SetTransientMessage("Message_RegionUndone");
        _ = RecomputeAsync(debounce: false);
    }

    public void UpdateZoom(double percent)
    {
        ZoomText = $"{Math.Round(percent):0}%";
    }

    public void SetTransientMessage(string key, params object?[] args)
    {
        _transientKey = key;
        _transientArgs = args;
        OnPropertyChanged(nameof(TransientMessage));
        OnPropertyChanged(nameof(StatusText));
    }

    public void ClearTransientMessage()
    {
        _transientKey = null;
        _transientArgs = Array.Empty<object?>();
        OnPropertyChanged(nameof(TransientMessage));
        OnPropertyChanged(nameof(StatusText));
    }


    public void SetBusy(bool busy, string? key = null)
    {
        if (key is not null)
        {
            _busyKey = key;
        }

        IsBusy = busy;
    }

    /// <summary>Used by the UI while composing and encoding an export.</summary>
    public void SetExporting(bool exporting)
    {
        IsExporting = exporting;
    }

    public async Task RecomputeAsync(bool debounce)
    {
        if (_disposed)
        {
            return;
        }

        ImageDocument? imageA = _imageA;
        ImageDocument? imageB = _imageB;
        long version = NextVersion();

        if (imageA is null || imageB is null)
        {
            _compareCancellation?.Cancel();
            _compareCancellation = null;
            _currentDiff = null;
            ClearStatistics("—");
            OnPropertyChanged(nameof(CanExport));
            OnPropertyChanged(nameof(ExportDifferenceButtonEnabled));
            OnPropertyChanged(nameof(ExportDifferenceSizeText));
            OnPropertyChanged(nameof(ExportSideBySideSizeText));
            DiffUpdated?.Invoke(this, new DiffUpdatedEventArgs(null));
            return;
        }

        _compareCancellation?.Cancel();
        CancellationTokenSource cancellation = new();
        _compareCancellation = cancellation;
        CancellationToken token = cancellation.Token;

        SetBusy(true, "Busy_Comparing");
        ClearStatistics(_loc.Translate("Stat_Comparing"));
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(ExportDifferenceButtonEnabled));
        OnPropertyChanged(nameof(ExportDifferenceSizeText));

        try
        {
            if (debounce)
            {
                await Task.Delay(CompareDebounceMilliseconds, token);
            }

            if (!IsCurrent(version))
            {
                return;
            }

            int threshold = _threshold;
            IgnoreRegion[] regions = _regionHistory.Regions.ToArray();
            DiffOptions options = new(threshold, regions);
            Stopwatch stopwatch = Stopwatch.StartNew();

            DiffResult computed = await Task.Run(
                () => DiffEngine.Compare(imageA, imageB, options, version, token),
                token);

            stopwatch.Stop();
            if (!IsCurrent(version))
            {
                _log.Info("Comparison result discarded as stale");
                return;
            }

            _currentDiff = computed;
            _log.Info(
                $"Comparison: canvas={computed.CanvasWidth}x{computed.CanvasHeight} " +
                $"threshold={threshold} regions={regions.Length} different={computed.DifferentPixelCount} " +
                $"compared={computed.ComparedPixelCount} durationMs={stopwatch.ElapsedMilliseconds}");

            UpdateStatistics(computed);
            DiffUpdated?.Invoke(this, new DiffUpdatedEventArgs(computed));
            OnPropertyChanged(nameof(CanExport));
            OnPropertyChanged(nameof(ExportDifferenceButtonEnabled));
            OnPropertyChanged(nameof(ExportDifferenceSizeText));
            ClearTransientMessage();
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer request (threshold move, new image, shutdown).
        }
        catch (Exception exception)
        {
            if (!IsCurrent(version))
            {
                return;
            }

            _log.Error("Comparison failed", exception);
            _currentDiff = null;
            ClearStatistics("—");
            OnPropertyChanged(nameof(CanExport));
            OnPropertyChanged(nameof(ExportDifferenceButtonEnabled));
            DiffUpdated?.Invoke(this, new DiffUpdatedEventArgs(null));
            RaiseError(
                _loc.Translate("Error_ComparisonTitle"),
                _loc.Translate("Error_ComparisonMessage"));
        }
        finally
        {
            if (IsCurrent(version))
            {
                SetBusy(false);
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _loc.LanguageChanged -= OnLanguageChanged;
        _compareCancellation?.Cancel();
        _compareCancellation = null;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ModeName));
        OnPropertyChanged(nameof(ImageAInfo));
        OnPropertyChanged(nameof(ImageBInfo));
        OnPropertyChanged(nameof(CanvasInfo));
        OnPropertyChanged(nameof(SingleImageHintText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusDetailText));
        OnPropertyChanged(nameof(TransientMessage));
        OnPropertyChanged(nameof(ThresholdText));
        if (_currentDiff is not null)
        {
            UpdateStatistics(_currentDiff);
        }

        RefreshRegions(SelectedRegionId);
    }

    private void NotifyImagesChanged()
    {
        OnPropertyChanged(nameof(ImageAInfo));
        OnPropertyChanged(nameof(ImageBInfo));
        OnPropertyChanged(nameof(CanvasInfo));
        OnPropertyChanged(nameof(HasAnyImage));
        OnPropertyChanged(nameof(CanCompare));
        OnPropertyChanged(nameof(CanSwap));
        OnPropertyChanged(nameof(CanClear));
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(IsThresholdEnabled));
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(SingleImageHintVisibility));
        OnPropertyChanged(nameof(ShowSingleImageHint));
        OnPropertyChanged(nameof(SingleImageHintText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusDetailText));
        OnPropertyChanged(nameof(ExportDifferenceButtonEnabled));
        OnPropertyChanged(nameof(ExportSideBySideButtonEnabled));
        OnPropertyChanged(nameof(ExportSideBySideSizeText));
        ImagesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshRegions(Guid? selectId)
    {
        Regions.Clear();
        for (int i = 0; i < _regionHistory.Regions.Count; i++)
        {
            Regions.Add(new RegionItemViewModel(_regionHistory.Regions[i], i + 1, _loc));
        }

        SelectedRegion = selectId is null ? null : Regions.FirstOrDefault(r => r.Id == selectId);
        OnPropertyChanged(nameof(HasRegions));
        OnPropertyChanged(nameof(CanUndoRegion));
    }

    private void UpdateStatistics(DiffResult diff)
    {
        CultureInfo culture = _loc.EffectiveCulture;
        DifferentPixelsText = diff.DifferentPixelCount.ToString("N0", culture);
        ComparedPixelsText = diff.ComparedPixelCount.ToString("N0", culture);
        RatioText = diff.DifferenceRatio is { } ratio ? ratio.ToString("P2", culture) : _loc.Translate("Stat_NotAvailable");
        IgnoredPixelsText = diff.IgnoredPixelCount.ToString("N0", culture);
        SizeDifferenceText = diff.AddedPixelCount == 0 && diff.RemovedPixelCount == 0
            ? _loc.Translate("Coverage_Full")
            : _loc.Format("Coverage_AddedRemoved", diff.AddedPixelCount.ToString("N0", culture), diff.RemovedPixelCount.ToString("N0", culture));
        DiffSummaryText = diff.DifferentPixelCount == 0
            ? _loc.Translate("Stat_NoDifferences")
            : _loc.Format("Stat_DifferentSummary", diff.DifferentPixelCount.ToString("N0", culture), RatioText);
        OnPropertyChanged(nameof(StatusText));
    }

    private void ClearStatistics(string placeholder)
    {
        DifferentPixelsText = placeholder;
        ComparedPixelsText = placeholder;
        RatioText = placeholder;
        IgnoredPixelsText = placeholder;
        SizeDifferenceText = placeholder;
        DiffSummaryText = placeholder;
    }

    private long NextVersion()
    {
        lock (_versionGate)
        {
            return ++_version;
        }
    }

    private bool IsCurrent(long version)
    {
        lock (_versionGate)
        {
            return !_disposed && version == _version;
        }
    }

    private void RaiseError(string title, string message)
    {
        ErrorRaised?.Invoke(this, new UserError(title, message));
    }


}
