using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using VisualDiffLab.App.Localization;
using VisualDiffLab.App.Services;
using VisualDiffLab.App.ViewModels;
using VisualDiffLab.Core.Comparison;
using VisualDiffLab.Core.Export;
using VisualDiffLab.Core.IgnoreRegions;
using VisualDiffLab.Core.Models;
using VisualDiffLab.Rendering;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.DirectX;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using Windows.UI.ViewManagement;
using WinRT.Interop;

namespace VisualDiffLab.App;

public sealed partial class MainWindow : Window, IDisposable
{
    private readonly StartupOptions _startupOptions;
    private readonly ComparisonRenderState _renderState = new();
    private readonly CanvasTextFormat _labelFormat;
    private readonly UISettings _uiSettings = new();
    private IgnoreRegion[] _regionSnapshot = [];
    private WindowMinSize? _minSize;
    private bool _canvasReady;
    private bool _viewInitialized;
    private bool _dialogOpen;
    private bool _syncingMode;
    private bool _syncingLanguage;
    private bool _syncingTheme;
    private bool _cleanedUp;
    private bool _startupLoaded;
    private bool _dragOverLogged;
    private AppThemeMode _themeMode = AppThemeMode.System;

    private long _modeChangeTimestamp;
    private bool _modeFrameLogged;

    private byte[]? _overlayScratch;
    private int _overlayBitmapWidth;
    private int _overlayBitmapHeight;

    public MainWindow(StartupOptions startupOptions)
    {
        _startupOptions = startupOptions;
        Loc = new LocalizationService();
        Loc.SetLanguage(startupOptions.Language);
        ViewModel = new MainViewModel(DiagnosticsLog.Shared, Loc);

        InitializeComponent();
        RootGrid.DataContext = Loc;
        ApplyLanguageToCodeStrings();
        SyncLanguageRadios();
        SyncThemeRadios();

        _labelFormat = new CanvasTextFormat
        {
            FontSize = 12,
            FontFamily = "Segoe UI Variable Text",
            HorizontalAlignment = CanvasHorizontalAlignment.Center,
            VerticalAlignment = CanvasVerticalAlignment.Center,
            WordWrapping = CanvasWordWrapping.NoWrap,
        };
        _renderState.LabelFormat = _labelFormat;

        HookEvents();
        ConfigureWindow();
        ApplyTheme();
        UpdatePalette();

        RootGrid.Loaded += RootGrid_Loaded;
    }

    public MainViewModel ViewModel { get; }

    public LocalizationService Loc { get; }

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (_startupLoaded)
        {
            return;
        }

        _startupLoaded = true;
        _syncingMode = true;
        ModeSelector.SelectedItem = (SelectorBarItem)ModeSelector.Items[0];
        _syncingMode = false;
        UpdateViewportLayout(keepCenter: false);

        if (!string.IsNullOrWhiteSpace(_startupOptions.ImageAPath))
        {
            await ViewModel.LoadImageFromPathAsync(true, _startupOptions.ImageAPath);
        }

        if (!string.IsNullOrWhiteSpace(_startupOptions.ImageBPath))
        {
            await ViewModel.LoadImageFromPathAsync(false, _startupOptions.ImageBPath);
        }

        Canvas.Focus(FocusState.Programmatic);
    }

    private void HookEvents()
    {
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.ImagesChanged += ViewModel_ImagesChanged;
        ViewModel.DiffUpdated += ViewModel_DiffUpdated;
        ViewModel.ErrorRaised += ViewModel_ErrorRaised;
        ViewModel.FitRequested += ViewModel_FitRequested;
        ViewModel.Regions.CollectionChanged += Regions_CollectionChanged;
        Loc.LanguageChanged += OnLanguageChanged;

        Canvas.SizeChanged += Canvas_SizeChanged;
        RootGrid.ActualThemeChanged += (_, _) => UpdatePalette();
        RootGrid.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(RootGrid_KeyDown), handledEventsToo: true);

        RootGrid.DragOver += RootGrid_DragOver;
        RootGrid.DragLeave += (_, _) =>
        {
            DropOverlay.Visibility = Visibility.Collapsed;
            _dragOverLogged = false;
        };
        RootGrid.Drop += RootGrid_Drop;

        Activated += MainWindow_Activated;
        Closed += MainWindow_Closed;
        AppWindow.Closing += AppWindow_Closing;
    }

    private void ConfigureWindow()
    {
        try
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            uint dpi = GetDpiForWindowSafe(hwnd);
            double scale = Math.Max(1, dpi) / 96.0;
            AppWindow.ResizeClient(new SizeInt32((int)Math.Round(1280 * scale), (int)Math.Round(800 * scale)));

            DisplayArea area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            SizeInt32 size = AppWindow.Size;
            int x = area.WorkArea.X + Math.Max(0, (area.WorkArea.Width - size.Width) / 2);
            int y = area.WorkArea.Y + Math.Max(0, (area.WorkArea.Height - size.Height) / 2);
            AppWindow.Move(new PointInt32(x, y));

            _minSize = new WindowMinSize(hwnd, 1024, 640);

            string icon = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(icon))
            {
                AppWindow.SetIcon(icon);
            }
        }
        catch (Exception exception)
        {
            DiagnosticsLog.Shared.Error("Window configuration failed", exception);
        }
    }

    private static uint GetDpiForWindowSafe(IntPtr hwnd)
    {
        try
        {
            return GetDpiForWindow(hwnd);
        }
        catch (DllNotFoundException)
        {
            return 96;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.Mode):
                SyncModeSelector();
                UpdateViewportLayout(keepCenter: true);
                if (ViewModel.Mode == ComparisonMode.Heatmap)
                {
                    RefreshOverlayBitmap();
                }
                else
                {
                    ReleaseOverlayResources();
                }

                _modeChangeTimestamp = Stopwatch.GetTimestamp();
                _modeFrameLogged = false;
                break;
            case nameof(MainViewModel.IsRegionMode):
                SyncRegionModeButton();
                Canvas.Invalidate();
                break;
            case nameof(MainViewModel.SelectedRegion):
            case nameof(MainViewModel.SelectedRegionId):
            case nameof(MainViewModel.OverlayOpacityValue):
            case nameof(MainViewModel.WipePositionValue):
                Canvas.Invalidate();
                break;
            case nameof(MainViewModel.IsInspectorVisible):
                SyncInspectorToggle();
                break;
            case nameof(MainViewModel.CanCompare):
            case nameof(MainViewModel.HasAnyImage):
                Canvas.Invalidate();
                break;
        }
    }

    private void Regions_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        _regionSnapshot = ViewModel.Regions.Select(region => region.Region).ToArray();
        _renderState.Regions = _regionSnapshot;
        Canvas.Invalidate();
    }

    private void ViewModel_ImagesChanged(object? sender, EventArgs e) => UploadDocumentBitmaps();

    private void ViewModel_DiffUpdated(object? sender, DiffUpdatedEventArgs e)
    {
        // The heatmap bitmap is only maintained while Heatmap mode is visible. Building a
        // 4K overlay on every threshold change in another mode wastes ~33 MB per upload.
        if (ViewModel.Mode == ComparisonMode.Heatmap)
        {
            RefreshOverlayBitmap();
        }

        Canvas.Invalidate();
    }

    private async void ViewModel_ErrorRaised(object? sender, UserError error)
    {
        if (_dialogOpen)
        {
            return;
        }

        await ShowErrorAsync(error.Title, error.Message);
    }

    private void ViewModel_FitRequested(object? sender, EventArgs e)
    {
        UpdateViewportLayout(keepCenter: false);
        FitToView();
    }

    private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateViewportLayout(keepCenter: true);

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            CancelActiveDrag(clearPendingRegion: true);
        }
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args) => Cleanup();

    private void MainWindow_Closed(object sender, WindowEventArgs args) => Cleanup();

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    private void Cleanup()
    {
        if (_cleanedUp)
        {
            return;
        }

        _cleanedUp = true;
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ViewModel.ImagesChanged -= ViewModel_ImagesChanged;
        ViewModel.DiffUpdated -= ViewModel_DiffUpdated;
        ViewModel.ErrorRaised -= ViewModel_ErrorRaised;
        ViewModel.FitRequested -= ViewModel_FitRequested;
        ViewModel.Regions.CollectionChanged -= Regions_CollectionChanged;
        Loc.LanguageChanged -= OnLanguageChanged;
        ViewModel.Dispose();

        DisposeBitmap(_renderState.ImageA);
        DisposeBitmap(_renderState.ImageB);
        DisposeBitmap(_renderState.HeatmapOverlay);
        _renderState.ImageA = null;
        _renderState.ImageB = null;
        _renderState.HeatmapOverlay = null;
        _overlayScratch = null;

        _minSize?.Dispose();
        _minSize = null;
        _labelFormat.Dispose();

        try
        {
            Canvas.RemoveFromVisualTree();
        }
        catch (Exception exception)
        {
            DiagnosticsLog.Shared.Error("Canvas cleanup failed", exception);
        }

        DiagnosticsLog.Shared.Info("Application exit");
    }

    private static void DisposeBitmap(CanvasBitmap? bitmap)
    {
        if (bitmap is null)
        {
            return;
        }

        try
        {
            bitmap.Dispose();
        }
        catch (Exception exception)
        {
            DiagnosticsLog.Shared.Error("Bitmap dispose failed", exception);
        }
    }

    // ---------------------------------------------------------------------
    // Commands
    // ---------------------------------------------------------------------

    private async void OpenA_Click(object sender, RoutedEventArgs e) => await PickAndLoadAsync(true);

    private async void OpenB_Click(object sender, RoutedEventArgs e) => await PickAndLoadAsync(false);

    private async void EmptyOpenA_Click(object sender, RoutedEventArgs e) => await PickAndLoadAsync(true);

    private async void EmptyOpenB_Click(object sender, RoutedEventArgs e) => await PickAndLoadAsync(false);

    private void Swap_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SwapImages();
        Canvas.Focus(FocusState.Programmatic);
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearComparison();
        Canvas.Focus(FocusState.Programmatic);
    }

    private void Fit_Click(object sender, RoutedEventArgs e) => FitToView();

    private void ActualSize_Click(object sender, RoutedEventArgs e) => ActualSize();

    private void UndoRegion_Click(object sender, RoutedEventArgs e) => ViewModel.UndoRegionChange();

    private void DeleteRegion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: RegionItemViewModel item })
        {
            ViewModel.RemoveRegion(item.Id);
        }
    }

    private void RegionMode_Click(object sender, RoutedEventArgs e)
    {
        bool enabled = RegionModeButton.IsChecked == true;
        if (sender is Button)
        {
            enabled = true;
        }

        ViewModel.IsRegionMode = enabled;
        if (enabled)
        {
            Canvas.Focus(FocusState.Programmatic);
        }
    }

    private void InspectorToggle_Click(object sender, RoutedEventArgs e) =>
        ViewModel.IsInspectorVisible = InspectorToggleButton.IsChecked == true;

    private void InspectorClose_Click(object sender, RoutedEventArgs e) =>
        ViewModel.IsInspectorVisible = false;

    private void SyncInspectorToggle()
    {
        bool value = ViewModel.IsInspectorVisible;
        if (InspectorToggleButton.IsChecked != value)
        {
            InspectorToggleButton.IsChecked = value;
        }
    }

    private void SyncRegionModeButton()
    {
        bool value = ViewModel.IsRegionMode;
        if (RegionModeButton.IsChecked != value)
        {
            RegionModeButton.IsChecked = value;
        }
    }

    private void ModeSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (_syncingMode || ModeSelector.SelectedItem is null)
        {
            return;
        }

        int index = ModeSelector.Items.IndexOf(ModeSelector.SelectedItem);
        if (index >= 0)
        {
            ViewModel.Mode = (ComparisonMode)(index + 1);
        }
    }

    private void SyncModeSelector()
    {
        int index = (int)ViewModel.Mode - 1;
        if (index < 0 || index >= ModeSelector.Items.Count)
        {
            return;
        }

        SelectorBarItem item = (SelectorBarItem)ModeSelector.Items[index];
        if (!ReferenceEquals(ModeSelector.SelectedItem, item))
        {
            _syncingMode = true;
            ModeSelector.SelectedItem = item;
            _syncingMode = false;
        }
    }

    private void LanguageRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_syncingLanguage)
        {
            return;
        }

        RadioButton? radio = sender as RadioButton;
        Loc.Language = ReferenceEquals(radio, LanguageEnglishRadio)
            ? AppLanguage.English
            : ReferenceEquals(radio, LanguageChineseRadio)
                ? AppLanguage.Chinese
                : AppLanguage.System;
    }

    private void SyncLanguageRadios()
    {
        _syncingLanguage = true;
        LanguageSystemRadio.IsChecked = Loc.Language == AppLanguage.System;
        LanguageEnglishRadio.IsChecked = Loc.Language == AppLanguage.English;
        LanguageChineseRadio.IsChecked = Loc.Language == AppLanguage.Chinese;
        _syncingLanguage = false;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        SyncLanguageRadios();
        ApplyLanguageToCodeStrings();
    }

    /// <summary>Strings on flyout items/secondary commands are applied in code so they are
    /// guaranteed to refresh when the flyout is not part of the visual tree.</summary>
    private void ApplyLanguageToCodeStrings()
    {
        ExportDifferenceMenuItem.Text = Loc["Command_ExportDifference"];
        ExportSideBySideMenuItem.Text = Loc["Command_ExportSideBySide"];

        UndoRegionButton.Label = Loc["Command_UndoRegion"];
        ClearButton.Label = Loc["Command_Clear"];
        ShortcutsButton.Label = Loc["Command_Shortcuts"];
        AboutButton.Label = Loc["Command_About"];

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(UndoRegionButton, Loc["UndoRegion_Name"]);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ClearButton, Loc["Command_Clear"]);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ShortcutsButton, Loc["Command_Shortcuts_Name"]);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(AboutButton, Loc["Command_About_Name"]);

        SyncThemeRadios();
    }

    private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_syncingTheme)
        {
            return;
        }

        RadioButton? radio = sender as RadioButton;
        _themeMode = ReferenceEquals(radio, ThemeLightRadio)
            ? AppThemeMode.Light
            : ReferenceEquals(radio, ThemeDarkRadio)
                ? AppThemeMode.Dark
                : AppThemeMode.System;
        ApplyTheme();
        Canvas.Invalidate();
    }

    private void SyncThemeRadios()
    {
        _syncingTheme = true;
        ThemeSystemRadio.IsChecked = _themeMode == AppThemeMode.System;
        ThemeLightRadio.IsChecked = _themeMode == AppThemeMode.Light;
        ThemeDarkRadio.IsChecked = _themeMode == AppThemeMode.Dark;
        _syncingTheme = false;
    }

    private void ApplyTheme()
    {
        RootGrid.RequestedTheme = _themeMode switch
        {
            AppThemeMode.Light => ElementTheme.Light,
            AppThemeMode.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };

        try
        {
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                AppWindow.TitleBar.PreferredTheme = _themeMode switch
                {
                    AppThemeMode.Light => TitleBarTheme.Light,
                    AppThemeMode.Dark => TitleBarTheme.Dark,
                    _ => TitleBarTheme.UseDefaultAppMode,
                };
            }
        }
        catch (Exception exception)
        {
            DiagnosticsLog.Shared.Error("Title bar theme update failed", exception);
        }

        UpdatePalette();
    }

    private void UpdatePalette()
    {
        bool dark = RootGrid.ActualTheme == ElementTheme.Dark;
        Color accent = _uiSettings.GetColorValue(UIColorType.Accent);
        _renderState.Accent = accent;
        _renderState.RegionSelectedStroke = accent;
        _renderState.RegionStroke = dark ? Color.FromArgb(235, 255, 255, 255) : Color.FromArgb(235, 24, 24, 24);
        _renderState.RegionLabelBackground = Color.FromArgb(220, 24, 24, 24);
        _renderState.RegionLabelForeground = Colors.White;
        _renderState.CanvasOutline = dark ? Color.FromArgb(96, 200, 200, 200) : Color.FromArgb(96, 70, 70, 70);
        _renderState.AddedOutline = Color.FromArgb(220, DiffColors.Added.R, DiffColors.Added.G, DiffColors.Added.B);
        _renderState.RemovedOutline = Color.FromArgb(220, DiffColors.Removed.R, DiffColors.Removed.G, DiffColors.Removed.B);
    }

    private void OpenShortcut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        _ = ShowOpenChooserAsync();
    }

    private void ExportShortcut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.HasAnyImage && !_dialogOpen)
        {
            ExportButton.Flyout.ShowAt(ExportButton);
        }
    }

    private void FitShortcut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        FitToView();
    }

    private void ActualSizeShortcut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        ActualSize();
    }

    private void UndoShortcut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (!_dialogOpen)
        {
            ViewModel.UndoRegionChange();
        }
    }

    private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_dialogOpen || e.Key != VirtualKey.Escape)
        {
            return;
        }

        if (_isDraggingRegion)
        {
            CancelActiveDrag(clearPendingRegion: true);
            e.Handled = true;
        }
        else if (ViewModel.IsRegionMode)
        {
            ViewModel.IsRegionMode = false;
            e.Handled = true;
        }
        else if (ViewModel.SelectedRegion is not null)
        {
            ViewModel.SelectedRegion = null;
            e.Handled = true;
        }
    }

    // ---------------------------------------------------------------------
    // File open, drag/drop
    // ---------------------------------------------------------------------

    private async Task PickAndLoadAsync(bool slotA)
    {
        try
        {
            FileOpenPicker picker = new()
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                ViewMode = PickerViewMode.List,
            };
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".bmp");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

            StorageFile? file = await picker.PickSingleFileAsync();
            if (file is not null)
            {
                await ViewModel.LoadImageAsync(slotA, file);
                Canvas.Focus(FocusState.Programmatic);
            }
        }
        catch (Exception exception)
        {
            DiagnosticsLog.Shared.Error("File picker failed", exception);
            await ShowErrorAsync(Loc["Dialog_OpenPickerFailed"], Loc["Dialog_OpenPickerFailedMessage"]);
        }
    }

    private async Task ShowOpenChooserAsync()
    {
        ContentDialog dialog = new()
        {
            Title = Loc["Dialog_OpenTitle"],
            Content = Loc["Dialog_OpenContent"],
            PrimaryButtonText = Loc["Dialog_ImageA"],
            SecondaryButtonText = Loc["Dialog_ImageB"],
            CloseButtonText = Loc["Dialog_Cancel"],
            DefaultButton = ContentDialogButton.Primary,
        };

        ContentDialogResult result = await ShowDialogAsync(dialog);
        if (result == ContentDialogResult.Primary)
        {
            await PickAndLoadAsync(true);
        }
        else if (result == ContentDialogResult.Secondary)
        {
            await PickAndLoadAsync(false);
        }
    }

    private void RootGrid_DragOver(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        if (!_dragOverLogged)
        {
            _dragOverLogged = true;
            DiagnosticsLog.Shared.Info("DragOver received storage items");
        }

        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = Loc["Dialog_OpenTitle"];
        e.DragUIOverride.IsCaptionVisible = true;
        e.DragUIOverride.IsContentVisible = true;
        DropOverlay.Visibility = Visibility.Visible;
        e.Handled = true;
    }

    private async void RootGrid_Drop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        _dragOverLogged = false;
        e.Handled = true;

        try
        {
            IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();
            DiagnosticsLog.Shared.Info($"Drop received {items.Count} item(s)");
            await HandleDroppedItemsAsync(items);
        }
        catch (Exception exception)
        {
            DiagnosticsLog.Shared.Error("Drop handling failed", exception);
            await ShowErrorAsync(Loc["Dialog_DropFailed"], Loc["Dialog_DropFailedMessage"]);
        }
    }

    private async Task HandleDroppedItemsAsync(IReadOnlyList<IStorageItem> items)
    {
        HashSet<string> supported = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".bmp",
        };

        List<StorageFile> files = items
            .OfType<StorageFile>()
            .Where(file => supported.Contains(Path.GetExtension(file.Name)))
            .ToList();

        if (files.Count == 0)
        {
            await ShowErrorAsync(Loc["Dialog_UnsupportedTitle"], Loc["Dialog_UnsupportedMessage"]);
            return;
        }

        if (files.Count >= 2)
        {
            await ViewModel.LoadImageAsync(true, files[0]);
            await ViewModel.LoadImageAsync(false, files[1]);
            if (files.Count > 2)
            {
                ViewModel.SetTransientMessage("Message_ExtraFilesIgnored");
            }

            Canvas.Focus(FocusState.Programmatic);
            return;
        }

        StorageFile single = files[0];
        if (ViewModel.ImageA is null)
        {
            await ViewModel.LoadImageAsync(true, single);
        }
        else if (ViewModel.ImageB is null)
        {
            await ViewModel.LoadImageAsync(false, single);
        }
        else
        {
            ContentDialog dialog = new()
            {
                Title = Loc["Dialog_ReplaceTitle"],
                Content = Loc.Format("Dialog_ReplaceContent", single.Name),
                PrimaryButtonText = Loc["Dialog_ReplaceA"],
                SecondaryButtonText = Loc["Dialog_ReplaceB"],
                CloseButtonText = Loc["Dialog_Cancel"],
                DefaultButton = ContentDialogButton.Primary,
            };

            ContentDialogResult result = await ShowDialogAsync(dialog);
            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.LoadImageAsync(true, single);
            }
            else if (result == ContentDialogResult.Secondary)
            {
                await ViewModel.LoadImageAsync(false, single);
            }
        }

        Canvas.Focus(FocusState.Programmatic);
    }

    // ---------------------------------------------------------------------
    // Export
    // ---------------------------------------------------------------------

    private async void ExportDifference_Click(object sender, RoutedEventArgs e) => await ExportAsync(difference: true);

    private async void ExportSideBySide_Click(object sender, RoutedEventArgs e) => await ExportAsync(difference: false);

    private async Task ExportAsync(bool difference)
    {
        DiffResult? diff = ViewModel.CurrentDiff;
        ImageDocument? imageA = ViewModel.ImageA;
        ImageDocument? imageB = ViewModel.ImageB;
        if (imageA is null)
        {
            return;
        }

        if (difference ? diff is null : imageB is null || ViewModel.IsBusy)
        {
            return;
        }

        int width;
        int height;
        if (difference)
        {
            width = diff!.CanvasWidth;
            height = diff.CanvasHeight;
        }
        else
        {
            (width, height) = SideBySideRenderer.GetOutputSize(imageA, imageB!);
            try
            {
                SideBySideRenderer.EnsureSupported(width, height);
            }
            catch (InvalidOperationException)
            {
                await ShowErrorAsync(Loc["Dialog_OutputTooLarge"], Loc["Dialog_OutputTooLargeMessage"]);
                return;
            }
        }

        string title = Loc[difference ? "Dialog_ExportDifferenceTitle" : "Dialog_ExportSideBySideTitle"];
        ContentDialog confirm = new()
        {
            Title = title,
            Content = new TextBlock
            {
                Text = Loc.Format("Dialog_ExportContent", width, height),
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = Loc["Dialog_ChooseLocation"],
            CloseButtonText = Loc["Dialog_Cancel"],
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await ShowDialogAsync(confirm) != ContentDialogResult.Primary)
        {
            return;
        }

        StorageFile? target;
        try
        {
            FileSavePicker picker = new()
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                SuggestedFileName = difference ? "difference-overlay" : "side-by-side",
            };
            picker.FileTypeChoices.Add(Loc["FileType_Png"], new List<string> { ".png" });
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            target = await picker.PickSaveFileAsync();
        }
        catch (Exception exception)
        {
            DiagnosticsLog.Shared.Error("Save picker failed", exception);
            await ShowErrorAsync(Loc["Dialog_SavePickerFailed"], Loc["Dialog_SavePickerFailedMessage"]);
            return;
        }

        if (target is null)
        {
            return;
        }

        ViewModel.SetExporting(true);
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            if (difference)
            {
                await ExportService.ExportDifferenceOverlayAsync(target, diff!, imageA, CancellationToken.None);
            }
            else
            {
                await ExportService.ExportSideBySideAsync(target, imageA, imageB!, CancellationToken.None);
            }

            stopwatch.Stop();
            DiagnosticsLog.Shared.Info(
                $"Export succeeded: kind={(difference ? "difference" : "side-by-side")} size={width}x{height} durationMs={stopwatch.ElapsedMilliseconds}");
            ViewModel.SetTransientMessage("Message_ExportComplete", target.Name, width, height);
        }
        catch (UnauthorizedAccessException exception)
        {
            DiagnosticsLog.Shared.Error("Export denied", exception);
            await ShowErrorAsync(Loc["Dialog_ExportFailed"], Loc["Dialog_ExportAccessDenied"]);
        }
        catch (Exception exception)
        {
            DiagnosticsLog.Shared.Error("Export failed", exception);
            await ShowErrorAsync(Loc["Dialog_ExportFailed"], Loc["Dialog_ExportGeneric"]);
        }
        finally
        {
            ViewModel.SetExporting(false);
        }
    }

    // ---------------------------------------------------------------------
    // Dialogs and help
    // ---------------------------------------------------------------------

    private async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        if (_dialogOpen)
        {
            return ContentDialogResult.None;
        }

        _dialogOpen = true;
        try
        {
            dialog.XamlRoot = RootGrid.XamlRoot;
            return await dialog.ShowAsync();
        }
        catch (Exception exception)
        {
            DiagnosticsLog.Shared.Error("Dialog failed", exception);
            return ContentDialogResult.None;
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private async Task ShowErrorAsync(string title, string message)
    {
        ContentDialog dialog = new()
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = Loc["Dialog_OK"],
            DefaultButton = ContentDialogButton.Close,
        };
        await ShowDialogAsync(dialog);
    }

    private async void Shortcuts_Click(object sender, RoutedEventArgs e)
    {
        ContentDialog dialog = new()
        {
            Title = Loc["Dialog_ShortcutsTitle"],
            Content = new TextBlock { Text = Loc["Dialog_ShortcutsText"], TextWrapping = TextWrapping.Wrap },
            CloseButtonText = Loc["Dialog_Close"],
        };
        await ShowDialogAsync(dialog);
    }

    private async void About_Click(object sender, RoutedEventArgs e)
    {
        string version = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
        ContentDialog dialog = new()
        {
            Title = Loc["Dialog_AboutTitle"],
            Content = new TextBlock { Text = Loc.Format("Dialog_AboutText", version), TextWrapping = TextWrapping.Wrap },
            CloseButtonText = Loc["Dialog_Close"],
        };
        await ShowDialogAsync(dialog);
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private enum AppThemeMode
    {
        System,
        Light,
        Dark,
    }
}
