using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using VisualDiffLab.Core.Export;
using VisualDiffLab.Core.Models;
using VisualDiffLab.Imaging;
using Xunit;
using Xunit.Abstractions;
using Application = FlaUI.Core.Application;

namespace VisualDiffLab.UITests;

public class ExportUiTests
{
    private readonly ITestOutputHelper _output;

    public ExportUiTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ExportDifferenceAndSideBySidePnGs_ThroughTheUi()
    {
        string root = TestPaths.RepositoryRoot();
        string exe = TestPaths.AppExecutable(root);
        string differencePath = Path.Combine(root, "artifacts", "acceptance", "exports", "difference", "difference-overlay.png");
        string sideBySidePath = Path.Combine(root, "artifacts", "acceptance", "exports", "side-by-side", "side-by-side.png");
        Directory.CreateDirectory(Path.GetDirectoryName(differencePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(sideBySidePath)!);
        foreach (string path in new[] { differencePath, sideBySidePath })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

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

            UiActions.WaitForText(window, "StatDifferentValue", text => text != "—" && text != "Comparing…", TimeSpan.FromSeconds(40));

            UiActions.Invoke(window, "ExportDifferenceButton");
            UiActions.ChooseExportLocation(app, automation, window, differencePath);
            _output.WriteLine($"difference export written: {new FileInfo(differencePath).Length:N0} bytes");
            UiActions.WaitForText(window, "StatusText", text => text.Contains("Export complete"), TimeSpan.FromSeconds(20));
            TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-3-export-difference.png");

            ImageDocument difference = await ImageLoader.LoadAsync(differencePath);
            Assert.Equal(1920, difference.Width);
            Assert.Equal(1080, difference.Height);
            int changedPixels = 0;
            for (int i = 0; i < difference.Pixels.Length; i += 4)
            {
                // The fixed magenta diff color is composited over the A base at 85%, so the
                // result varies with the underlying pixel but stays in this tight range.
                byte b = difference.Pixels[i];
                byte g = difference.Pixels[i + 1];
                byte r = difference.Pixels[i + 2];
                if (r >= 185 && g <= 60 && b is >= 110 and <= 180)
                {
                    changedPixels++;
                }
            }

            _output.WriteLine($"changed-color pixels in exported overlay: {changedPixels:N0}");
            Assert.True(changedPixels > 10_000, "The exported difference overlay must contain fixed diff-colored pixels.");

            UiActions.Invoke(window, "ExportSideBySideButton");
            UiActions.ChooseExportLocation(app, automation, window, sideBySidePath);
            _output.WriteLine($"side-by-side export written: {new FileInfo(sideBySidePath).Length:N0} bytes");
            UiActions.WaitForText(window, "StatusText", text => text.Contains("Export complete"), TimeSpan.FromSeconds(20));

            ImageDocument sideBySide = await ImageLoader.LoadAsync(sideBySidePath);
            Assert.Equal(1920 + 16 + 1920, sideBySide.Width);
            Assert.Equal(32 + 1080, sideBySide.Height);
            Assert.Equal(255, sideBySide.Pixels[3]);
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

    [Fact]
    public async Task SizeMismatch_ReportsAddedAndRemovedPixels()
    {
        string root = TestPaths.RepositoryRoot();
        string exe = TestPaths.AppExecutable(root);
        string differencePath = Path.Combine(root, "artifacts", "acceptance", "exports", "size-mismatch", "difference-size-mismatch.png");
        Directory.CreateDirectory(Path.GetDirectoryName(differencePath)!);
        if (File.Exists(differencePath))
        {
            File.Delete(differencePath);
        }

        TestPaths.KillRunningInstances();
        using Application app = Application.Launch(new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"--a \"{TestPaths.Image(root, "layout-before.png")}\" --b \"{TestPaths.Image(root, "layout-after.png")}\" --lang en-US",
            UseShellExecute = false,
        });

        try
        {
            using UIA3Automation automation = new();
            Window window = app.GetMainWindow(automation, TimeSpan.FromSeconds(30))!;
            window.SetForeground();
            TestPaths.SetTopMost(window, true);
            TestPaths.ForceForeground(window);

            UiActions.WaitForText(window, "StatDifferentValue", text => text != "—" && text != "Comparing…", TimeSpan.FromSeconds(40));
            string coverage = UiActions.WaitForText(
                window,
                "StatCoverageValue",
                text => text.Contains("added", StringComparison.OrdinalIgnoreCase) && text.Contains("missing", StringComparison.OrdinalIgnoreCase),
                TimeSpan.FromSeconds(10));
            _output.WriteLine($"coverage: {coverage}");

            UiActions.SelectMode(window, "ModeHeatmap");
            UiActions.Invoke(window, "FitButton");
            TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-3-size-mismatch.png");

            UiActions.Invoke(window, "ExportDifferenceButton");
            UiActions.ChooseExportLocation(app, automation, window, differencePath);

            ImageDocument difference = await ImageLoader.LoadAsync(differencePath);
            Assert.Equal(1720, difference.Width);
            Assert.Equal(1080, difference.Height);

            int added = 0;
            int removed = 0;
            for (int i = 0; i < difference.Pixels.Length; i += 4)
            {
                byte b = difference.Pixels[i];
                byte g = difference.Pixels[i + 1];
                byte r = difference.Pixels[i + 2];
                if (b == 221 && g == 142 && r == 4)
                {
                    added++;
                }
                else if (b == 0 && g == 157 && r == 217)
                {
                    removed++;
                }
            }

            _output.WriteLine($"added-color pixels: {added:N0}, removed-color pixels: {removed:N0}");
            Assert.True(added > 1_000, "Area present only in B must use the fixed added color.");
            Assert.Equal(0, removed);
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
}
