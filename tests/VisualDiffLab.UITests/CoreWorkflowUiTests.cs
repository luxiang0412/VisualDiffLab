using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Drawing;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;
using Application = FlaUI.Core.Application;

namespace VisualDiffLab.UITests;

public class CoreWorkflowUiTests
{
    private readonly ITestOutputHelper _output;

    public CoreWorkflowUiTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CoreWorkflow_CanBeOperatedThroughUiAutomation()
    {
        string root = TestPaths.RepositoryRoot();
        string exe = TestPaths.AppExecutable(root);
        Assert.True(File.Exists(exe), $"App executable not found: {exe}");

        TestPaths.KillRunningInstances();
        using Application app = Application.Launch(new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"--a \"{TestPaths.Image(root, "dashboard-before.png")}\" --b \"{TestPaths.Image(root, "dashboard-after.png")}\" --lang en-US",
            UseShellExecute = false,
        });

        try
        {
            using UIA3Automation automation = new();
            Window window = app.GetMainWindow(automation, TimeSpan.FromSeconds(30))!;
            window.SetForeground();
            TestPaths.SetTopMost(window, true);
            TestPaths.ForceForeground(window);

            string initialDifferent = WaitForText(
                window,
                "StatDifferentValue",
                text => text != "—" && text != "Comparing…",
                TimeSpan.FromSeconds(40));
            _output.WriteLine($"initial different pixels: {initialDifferent}");
            Assert.NotEqual("0", initialDifferent.Replace(",", string.Empty));

            AutomationElement swap = Find(window, "SwapButton");
            Assert.True(swap.IsEnabled, "Swap should be enabled after loading two images.");
            AutomationElement export = Find(window, "ExportButton");
            Assert.True(export.IsEnabled, "Export should be enabled after loading two images.");

            // Side-by-side, fitted
            Invoke(window, "FitButton");
            TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-2-side-by-side-light.png");

            // Wipe
            SelectMode(window, "ModeWipe");
            Invoke(window, "FitButton");
            WaitForText(window, "ThresholdValueText", text => text == "16", TimeSpan.FromSeconds(5));
            TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-2-wipe-light.png");

            // Overlay 65%
            SelectMode(window, "ModeOverlay");
            SetSlider(window, "OverlayOpacitySlider", 65);
            WaitForText(window, "OverlayOpacityValueText", text => text == "65%", TimeSpan.FromSeconds(5));
            TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-2-overlay-light.png");

            // Heatmap
            SelectMode(window, "ModeHeatmap");
            Invoke(window, "FitButton");
            TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-2-heatmap-light.png");

            // Threshold changes recompute and stats update.
            string beforeThreshold = WaitForText(window, "StatDifferentValue", text => text != "—", TimeSpan.FromSeconds(10));
            SetSlider(window, "ThresholdSlider", 64);
            string afterThreshold = WaitForText(
                window,
                "StatDifferentValue",
                text => text != "—" && text != "Comparing…" && text != beforeThreshold,
                TimeSpan.FromSeconds(30));
            int beforeValue = ParseCount(beforeThreshold);
            int afterValue = ParseCount(afterThreshold);
            _output.WriteLine($"threshold 16 => {beforeValue}, threshold 64 => {afterValue}");
            Assert.True(afterValue < beforeValue, "A higher threshold must reduce the number of different pixels.");
            TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-2-threshold-64.png");
            SetSlider(window, "ThresholdSlider", 16);
            WaitForText(window, "StatDifferentValue", text => ParseCount(text) == beforeValue, TimeSpan.FromSeconds(30));

            // Keyboard shortcuts on the canvas (digits switch modes).
            AutomationElement canvas = Find(window, "ComparisonCanvas");
            Rectangle canvasRect = canvas.BoundingRectangle;
            TestPaths.ForceForeground(window);
            Mouse.Click(new Point(canvasRect.Left + (canvasRect.Width / 2), canvasRect.Top + (canvasRect.Height / 2)), MouseButton.Left);
            Thread.Sleep(400);
            Keyboard.Type("2");
            Thread.Sleep(500);
            Assert.True(IsModeSelected(window, "ModeWipe"), "Key '2' should select Wipe when the canvas is focused.");
            Keyboard.Type("4");
            Thread.Sleep(500);
            Assert.True(IsModeSelected(window, "ModeHeatmap"), "Key '4' should select Heatmap when the canvas is focused.");

            // Ignore region: enter region mode and drag a rectangle over the dynamic avatar
            // area (right-hand column, about 74% down the image). Using the known fit
            // transform keeps this deterministic.
            Invoke(window, "FitButton");
            Thread.Sleep(300);
            Invoke(window, "RegionModeButton");
            Thread.Sleep(400);
            Assert.Contains("Region mode", Find(window, "StatusText").Name);

            double scale = TestPaths.ScaleForWindow(window);
            Rectangle fittedCanvas = Find(window, "ComparisonCanvas").BoundingRectangle;
            (double imageLeft, double imageTop, double imageWidth, double imageHeight) = FitImageRect(fittedCanvas, scale, 1920, 1080);
            double x1 = imageLeft + (imageWidth * 0.79);
            double y1 = imageTop + (imageHeight * 0.68);
            double x2 = imageLeft + (imageWidth * 0.93);
            double y2 = imageTop + (imageHeight * 0.80);
            _output.WriteLine($"drag region: ({x1:0},{y1:0}) -> ({x2:0},{y2:0})");
            Drag(new Point((int)x1, (int)y1), new Point((int)x2, (int)y2));

            string ignored = WaitForText(
                window,
                "StatIgnoredValue",
                text => text != "—" && text != "0" && text != "Comparing…",
                TimeSpan.FromSeconds(30));
            _output.WriteLine($"ignored pixels after region: {ignored}");
            Assert.NotEqual("0", ignored.Replace(",", string.Empty));
            Invoke(window, "RegionModeButton");
            Thread.Sleep(500);
            TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-3-ignore-region.png");

            // Undo the region (Ctrl+Z) and verify ignored pixels return to zero.
            TestPaths.ForceForeground(window);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            WaitForText(window, "StatIgnoredValue", text => text == "0", TimeSpan.FromSeconds(30));
            _output.WriteLine("region undo verified");

            // Re-add a region to keep the export evidence meaningful.
            Invoke(window, "RegionModeButton");
            Thread.Sleep(300);
            Drag(new Point((int)x1, (int)y1), new Point((int)x2, (int)y2));
            WaitForText(
                window,
                "StatIgnoredValue",
                text => text != "—" && text != "0" && text != "Comparing…",
                TimeSpan.FromSeconds(30));
            Invoke(window, "RegionModeButton");
            Thread.Sleep(300);

            string ignoredWithRegion = WaitForText(
                window,
                "StatIgnoredValue",
                text => text != "—" && text != "0" && text != "Comparing…",
                TimeSpan.FromSeconds(30));
            _output.WriteLine($"ignored pixels before zoom checks: {ignoredWithRegion}");

            // Region coordinates must survive 100% zoom, pan and fit.
            Mouse.Click(new Point(canvasRect.Left + (canvasRect.Width / 2), canvasRect.Top + (canvasRect.Height / 2)), MouseButton.Left);
            Keyboard.Type("0");
            Thread.Sleep(900);
            Assert.Equal(ignoredWithRegion, Find(window, "StatIgnoredValue").Name);
            TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-3-region-100-percent.png");

            SpaceDown();
            Thread.Sleep(200);
            Mouse.MoveTo(canvasRect.Left + (canvasRect.Width / 2), canvasRect.Top + (canvasRect.Height / 2));
            Mouse.Down(MouseButton.Left);
            Mouse.MoveTo(canvasRect.Left + (canvasRect.Width / 2) + 120, canvasRect.Top + (canvasRect.Height / 2) + 80);
            Thread.Sleep(400);
            Mouse.Up(MouseButton.Left);
            SpaceUp();
            Thread.Sleep(500);
            Assert.Equal(ignoredWithRegion, Find(window, "StatIgnoredValue").Name);
            TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-3-region-panned.png");

            Keyboard.Type("f");
            Thread.Sleep(700);
            Assert.Equal(ignoredWithRegion, Find(window, "StatIgnoredValue").Name);

            // Esc exits region mode from the canvas.
            Invoke(window, "RegionModeButton");
            Thread.Sleep(400);
            Assert.Contains("Region mode", Find(window, "StatusText").Name);
            Keyboard.Press(VirtualKeyShort.ESCAPE);
            Thread.Sleep(500);
            Assert.DoesNotContain("Region mode", Find(window, "StatusText").Name);

            // Wipe arrows move the split line (canvas focused).
            SelectMode(window, "ModeWipe");
            Invoke(window, "FitButton");
            Mouse.Click(new Point(canvasRect.Left + (canvasRect.Width / 2), canvasRect.Top + (canvasRect.Height / 2)), MouseButton.Left);
            Thread.Sleep(300);
            Keyboard.Press(VirtualKeyShort.LEFT);
            Keyboard.Press(VirtualKeyShort.LEFT);
            WaitForText(window, "WipePositionValueText", text => text == "48%", TimeSpan.FromSeconds(5));

            // Rapid threshold changes must settle on the newest result, never a stale one.
            // Baselines with the ignore region present (the region changes the counts).
            SetSlider(window, "ThresholdSlider", 64);
            string highBaseline = WaitForStableText(window, "StatDifferentValue", TimeSpan.FromSeconds(30));
            SetSlider(window, "ThresholdSlider", 16);
            string lowBaseline = WaitForStableText(window, "StatDifferentValue", TimeSpan.FromSeconds(30));
            _output.WriteLine($"with region: 16 => {lowBaseline}, 64 => {highBaseline}");

            int targetHigh = ParseCount(highBaseline);
            AutomationElement thresholdElement = Find(window, "ThresholdSlider");
            Assert.True(thresholdElement.Patterns.RangeValue.TryGetPattern(out var thresholdRange));
            foreach (double threshold in new double[] { 10, 20, 30, 40, 50, 64 })
            {
                thresholdRange!.SetValue(threshold);
                Thread.Sleep(25);
            }

            WaitForText(window, "StatDifferentValue", text => ParseCount(text) == targetHigh, TimeSpan.FromSeconds(30));
            _output.WriteLine("rapid threshold changes settled on the newest result");
            SetSlider(window, "ThresholdSlider", 16);
            WaitForText(window, "StatDifferentValue", text => text == lowBaseline, TimeSpan.FromSeconds(30));

            // Dark theme screenshots.
            UiActions.SelectRadio(window, "ThemeDarkRadio");
            Thread.Sleep(900);
            SelectMode(window, "ModeSideBySide");
            Invoke(window, "FitButton");
            TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-2-side-by-side-dark.png");
            SelectMode(window, "ModeHeatmap");
            Invoke(window, "FitButton");
            TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-2-heatmap-dark.png");
            UiActions.SelectRadio(window, "ThemeLightRadio");
            Thread.Sleep(800);

            // Minimum window size + maximize/restore.
            TestPaths.ResizeWindow(window, 1024, 640);
            Thread.Sleep(900);
            Rectangle narrow = window.BoundingRectangle;
            _output.WriteLine($"narrow window physical size: {narrow.Width}x{narrow.Height}");
            Assert.True(narrow.Width >= (int)(1024 * scale) - 2, "Minimum window width is enforced.");
            TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-4-narrow-window.png");

            TestPaths.Maximize(window);
            Thread.Sleep(900);
            TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-4-maximized.png");
            TestPaths.Restore(window);
            Thread.Sleep(700);

            _output.WriteLine("UI workflow test completed");
        }
        finally
        {
            try
            {
                app.Close();
                if (!app.HasExited)
                {
                    app.Kill();
                }
            }
            catch (Exception)
            {
                TestPaths.KillRunningInstances();
            }

            TestPaths.KillRunningInstances();
        }
    }

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);

    private static void SpaceDown() => keybd_event(0x20, 0, 0, UIntPtr.Zero);

    private static void SpaceUp() => keybd_event(0x20, 0, 0x0002, UIntPtr.Zero);

    private static (double Left, double Top, double Width, double Height) FitImageRect(
        Rectangle canvas,
        double scale,
        int imageWidth,
        int imageHeight)
    {
        double viewportWidth = canvas.Width / scale;
        double viewportHeight = canvas.Height / scale;
        const double padding = 24.0;
        double zoom = Math.Min(
            Math.Max(1, viewportWidth - (padding * 2)) / imageWidth,
            Math.Max(1, viewportHeight - (padding * 2)) / imageHeight);
        double width = imageWidth * zoom;
        double height = imageHeight * zoom;
        double left = canvas.Left + (((viewportWidth - width) / 2) * scale);
        double top = canvas.Top + (((viewportHeight - height) / 2) * scale);
        return (left, top, width * scale, height * scale);
    }

    private static void Drag(Point from, Point to)
    {
        Mouse.MoveTo((int)from.X, (int)from.Y);
        Thread.Sleep(150);
        Mouse.Down(MouseButton.Left);
        Thread.Sleep(150);
        Mouse.MoveTo((int)to.X, (int)to.Y);
        Thread.Sleep(450);
        Mouse.Up(MouseButton.Left);
        Thread.Sleep(500);
    }

    private static AutomationElement Find(Window window, string automationId)
    {
        AutomationElement? element = UiActions.FindFirstWithRetry(window, cf => cf.ByAutomationId(automationId));
        Assert.True(element is not null, $"Element '{automationId}' was not found.");
        return element!;
    }

    private static void Invoke(Window window, string automationId)
    {
        AutomationElement element = Find(window, automationId);
        if (element.Patterns.Invoke.TryGetPattern(out var invoke))
        {
            invoke.Invoke();
            return;
        }

        if (element.Patterns.Toggle.TryGetPattern(out var toggle))
        {
            toggle.Toggle();
            return;
        }

        element.Click();
    }

    private static void SelectMode(Window window, string automationId)
    {
        AutomationElement element = Find(window, automationId);
        Assert.True(element.Patterns.SelectionItem.TryGetPattern(out var selection), $"{automationId} has no SelectionItem pattern.");
        selection!.Select();
        Thread.Sleep(500);
        Assert.True(IsModeSelected(window, automationId), $"{automationId} was not selected.");
    }

    private static bool IsModeSelected(Window window, string automationId)
    {
        AutomationElement element = Find(window, automationId);
        return element.Patterns.SelectionItem.TryGetPattern(out var selection) && selection.IsSelected;
    }

    private static void SetSlider(Window window, string automationId, double value)
    {
        AutomationElement element = Find(window, automationId);
        Assert.True(element.Patterns.RangeValue.TryGetPattern(out var range), $"{automationId} has no RangeValue pattern.");
        range!.SetValue(value);
        Thread.Sleep(200);
    }

    private static void SelectComboItem(Window window, string comboId, string itemName)
    {
        AutomationElement comboElement = Find(window, comboId);
        var combo = comboElement.AsComboBox();
        combo.Expand();
        Thread.Sleep(400);
        var item = combo.Items.FirstOrDefault(candidate => candidate.Name.Contains(itemName, StringComparison.OrdinalIgnoreCase));
        Assert.True(item is not null, $"Combo item '{itemName}' was not found in {comboId}.");
        item!.Select();
        combo.Collapse();
        Thread.Sleep(500);
    }

    private static string WaitForText(Window window, string automationId, Func<string, bool> predicate, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        string last = "<not found>";
        while (DateTime.UtcNow < deadline)
        {
            AutomationElement? element = UiActions.FindFirstWithRetry(window, cf => cf.ByAutomationId(automationId));
            if (element is not null)
            {
                last = element.Name;
                if (predicate(last))
                {
                    return last;
                }
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException($"Timed out waiting for '{automationId}'. Last value: '{last}'.");
    }

    private static string WaitForStableText(Window window, string automationId, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        string last = "<not found>";
        DateTime lastChange = DateTime.UtcNow;
        while (DateTime.UtcNow < deadline)
        {
            AutomationElement? element = UiActions.FindFirstWithRetry(window, cf => cf.ByAutomationId(automationId));
            string current = element?.Name ?? "<not found>";
            if (current != last)
            {
                last = current;
                lastChange = DateTime.UtcNow;
            }
            else if (DateTime.UtcNow - lastChange > TimeSpan.FromMilliseconds(800) &&
                     last != "—" &&
                     last != "Comparing…")
            {
                return last;
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException($"Timed out waiting for '{automationId}' to stabilize. Last value: '{last}'.");
    }

    private static int ParseCount(string text) =>
        int.TryParse(text.Replace(",", string.Empty).Replace("%", string.Empty), out int value) ? value : 0;
}
