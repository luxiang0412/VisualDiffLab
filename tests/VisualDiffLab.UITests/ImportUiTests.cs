using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;
using Application = FlaUI.Core.Application;

namespace VisualDiffLab.UITests;

public class ImportUiTests
{
    private readonly ITestOutputHelper _output;

    public ImportUiTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ImportThroughFilePicker_SwapAndClear_Work()
    {
        string root = TestPaths.RepositoryRoot();
        string exe = TestPaths.AppExecutable(root);
        // Use an isolated folder with only the two files under test. The native open
        // dialog remembers a selected item and can auto-complete to similarly named
        // fixtures (for example the 4K variant) in the shared fixture folder.
        string inputDirectory = Path.Combine(root, "artifacts", "test-input");
        Directory.CreateDirectory(inputDirectory);
        string before = Path.Combine(inputDirectory, "import-before.png");
        string after = Path.Combine(inputDirectory, "import-after.png");
        File.Copy(TestPaths.Image(root, "dashboard-before.png"), before, overwrite: true);
        File.Copy(TestPaths.Image(root, "dashboard-after.png"), after, overwrite: true);

        TestPaths.KillRunningInstances();
        using Application app = Application.Launch(new ProcessStartInfo
        {
            FileName = exe,
            Arguments = "--lang en-US",
            UseShellExecute = false,
        });

        try
        {
            using UIA3Automation automation = new();
            Window window = app.GetMainWindow(automation, TimeSpan.FromSeconds(30))!;
            window.SetForeground();
            TestPaths.SetTopMost(window, true);
            TestPaths.ForceForeground(window);

            UiActions.WaitForText(window, "StatusText", text => text.Contains("Open or drop"), TimeSpan.FromSeconds(15));

            // Import A through the real Open dialog.
            UiActions.Invoke(window, "EmptyOpenAButton");
            UiActions.OpenFileThroughPicker(automation, window, before);
            UiActions.WaitForText(
                window,
                "ImageAInfo",
                text => text.Contains("import-before.png"),
                TimeSpan.FromSeconds(20));
            _output.WriteLine("image A imported through the file picker");

            // Import B through the toolbar button.
            UiActions.Invoke(window, "OpenBButton");
            UiActions.OpenFileThroughPicker(automation, window, after);
            UiActions.WaitForText(
                window,
                "ImageBInfo",
                text => text.Contains("import-after.png"),
                TimeSpan.FromSeconds(20));
            string different = UiActions.WaitForText(
                window,
                "StatDifferentValue",
                text => text != "—" && text != "Comparing…",
                TimeSpan.FromSeconds(30));
            _output.WriteLine($"imported pair computed {different} different pixels");
            TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-2-file-picker-import.png");

            // Swap A and B.
            UiActions.Invoke(window, "SwapButton");
            UiActions.WaitForText(
                window,
                "ImageAInfo",
                text => text.Contains("import-after.png"),
                TimeSpan.FromSeconds(10));
            UiActions.WaitForText(
                window,
                "ImageBInfo",
                text => text.Contains("import-before.png"),
                TimeSpan.FromSeconds(10));
            _output.WriteLine("swap verified through inspector image labels");

            // Clear via the command bar overflow menu.
            UiActions.Invoke(window, "MoreButton");
            Thread.Sleep(500);
            AutomationElement? clear = UiActions.WaitFor(
                () => UiActions.FindFirstWithRetry(window, cf => cf.ByAutomationId("ClearButton")),
                TimeSpan.FromSeconds(10),
                "clear command");
            clear.AsButton().Invoke();
            Thread.Sleep(700);

            UiActions.WaitForText(window, "ImageAInfo", text => text == "Not loaded", TimeSpan.FromSeconds(10));
            UiActions.WaitForText(window, "ImageBInfo", text => text == "Not loaded", TimeSpan.FromSeconds(10));
            UiActions.WaitForText(window, "StatusText", text => text.Contains("Open or drop"), TimeSpan.FromSeconds(10));
            _output.WriteLine("clear verified");
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
