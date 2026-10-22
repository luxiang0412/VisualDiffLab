using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;
using Application = FlaUI.Core.Application;

namespace VisualDiffLab.UITests;

public class EmptyStateUiTests
{
    private readonly ITestOutputHelper _output;

    public EmptyStateUiTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void EmptyStateSingleImageAndCorruptFileError_AreVisible()
    {
        string root = TestPaths.RepositoryRoot();
        string exe = TestPaths.AppExecutable(root);

        // Empty state, light and dark.
        TestPaths.KillRunningInstances();
        using (Application app = Application.Launch(new ProcessStartInfo { FileName = exe, Arguments = "--lang en-US", UseShellExecute = false }))
        {
            try
            {
                using UIA3Automation automation = new();
                Window window = app.GetMainWindow(automation, TimeSpan.FromSeconds(30))!;
                window.SetForeground();
                TestPaths.SetTopMost(window, true);
                TestPaths.ForceForeground(window);

                UiActions.WaitForText(
                    window,
                    "StatusText",
                    text => text.Contains("Open or drop", StringComparison.OrdinalIgnoreCase),
                    TimeSpan.FromSeconds(15));
                AutomationElement emptyOpen = UiActions.Find(window, "EmptyOpenAButton");
                Assert.False(emptyOpen.IsOffscreen, "The empty state actions should be visible on first launch.");
                TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-2-empty-light.png");

                UiActions.SelectRadio(window, "ThemeDarkRadio");
                Thread.Sleep(800);
                TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-2-empty-dark.png");
            }
            finally
            {
                CloseQuietly(app);
            }
        }

        // Corrupt image produces a user-facing error dialog, not a crash.
        string corrupt = Path.Combine(root, "artifacts", "acceptance", "corrupt-input.png");
        Directory.CreateDirectory(Path.GetDirectoryName(corrupt)!);
        File.WriteAllBytes(corrupt, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 });

        TestPaths.KillRunningInstances();
        using (Application app = Application.Launch(new ProcessStartInfo
               {
                   FileName = exe,
                   Arguments = $"--a \"{corrupt}\" --lang en-US",
                   UseShellExecute = false,
               }))
        {
            try
            {
                using UIA3Automation automation = new();
                Window window = app.GetMainWindow(automation, TimeSpan.FromSeconds(30))!;
                window.SetForeground();
                TestPaths.SetTopMost(window, true);
                TestPaths.ForceForeground(window);

                AutomationElement title = UiActions.WaitFor(
                    () => UiActions.FindFirstWithRetry(window, cf => cf.ByName("Could not open image")),
                    TimeSpan.FromSeconds(20),
                    "corrupt file error dialog");
                _output.WriteLine($"error dialog title: {title.Name}");
                TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-2-error-corrupt.png");

                AutomationElement? ok = UiActions.FindFirstWithRetry(window, cf =>
                    cf.ByControlType(ControlType.Button).And(cf.ByName("OK")));
                ok?.AsButton().Invoke();
                Thread.Sleep(500);
            }
            finally
            {
                CloseQuietly(app);
            }
        }

        // One image only: usable single-image state with a clear hint.
        TestPaths.KillRunningInstances();
        using (Application app = Application.Launch(new ProcessStartInfo
               {
                   FileName = exe,
                   Arguments = $"--a \"{TestPaths.Image(root, "dashboard-before.png")}\" --lang en-US",
                   UseShellExecute = false,
               }))
        {
            try
            {
                using UIA3Automation automation = new();
                Window window = app.GetMainWindow(automation, TimeSpan.FromSeconds(30))!;
                window.SetForeground();
                TestPaths.SetTopMost(window, true);
                TestPaths.ForceForeground(window);

                UiActions.WaitForText(
                    window,
                    "ImageAInfo",
                    text => text.Contains("dashboard-before.png"),
                    TimeSpan.FromSeconds(20));
                UiActions.WaitForText(
                    window,
                    "StatusText",
                    text => text.Contains("open image B", StringComparison.OrdinalIgnoreCase),
                    TimeSpan.FromSeconds(10));
                TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-2-single-image-hint.png");
            }
            finally
            {
                CloseQuietly(app);
            }
        }
    }

    private static void CloseQuietly(Application app)
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
