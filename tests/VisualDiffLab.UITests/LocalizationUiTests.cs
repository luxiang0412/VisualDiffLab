using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;
using Application = FlaUI.Core.Application;

namespace VisualDiffLab.UITests;

public class LocalizationUiTests
{
    private readonly ITestOutputHelper _output;

    public LocalizationUiTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void LanguageSwitch_EnglishAndChinese_AreAppliedImmediately()
    {
        string root = TestPaths.RepositoryRoot();
        string exe = TestPaths.AppExecutable(root);
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
            Assert.Equal("Open image A", UiActions.Find(window, "OpenAButton").Name);

            // The language selector sits below the fold; scroll to it before selecting.
            TestPaths.ScrollInspector(window, 100);
            UiActions.SelectRadio(window, "LanguageChineseRadio");

            UiActions.WaitForText(window, "OpenAButton", text => text == "打开图像 A", TimeSpan.FromSeconds(10));
            string summary = UiActions.WaitForText(
                window,
                "DiffSummaryText",
                text => text.Contains("个差异像素", StringComparison.Ordinal),
                TimeSpan.FromSeconds(10));
            _output.WriteLine($"Chinese summary: {summary}");
            Assert.Contains("差异像素", summary);
            AutomationElement themeSystem = UiActions.Find(window, "ThemeSystemRadio");
            Assert.Equal("跟随系统", themeSystem.Name);
            Assert.True(themeSystem.Patterns.SelectionItem.TryGetPattern(out var themeSelection) && themeSelection.IsSelected);

            // Secondary commands and export flyout are localized in code; verify them too.
            UiActions.Invoke(window, "MoreButton");
            UiActions.WaitForText(window, "ClearButton", text => text == "清空比较", TimeSpan.FromSeconds(10));
            FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
            Thread.Sleep(300);
            UiActions.Invoke(window, "ExportButton");
            AutomationElement? exportItem = UiActions.WaitFor(
                () => UiActions.FindFirstWithRetry(window, cf => cf.ByAutomationId("ExportDifferenceItem")),
                TimeSpan.FromSeconds(10),
                "localized export flyout");
            Assert.Contains("差异叠加", exportItem.Name);
            FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
            Thread.Sleep(300);

            TestPaths.ScrollInspector(window, 100);
            TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-4-localization-chinese.png", resetInspectorScroll: false);

            // Switch back to English through the same selector.
            UiActions.SelectRadio(window, "LanguageEnglishRadio");
            UiActions.WaitForText(window, "OpenAButton", text => text == "Open image A", TimeSpan.FromSeconds(10));
            string englishSummary = UiActions.WaitForText(
                window,
                "DiffSummaryText",
                text => text.Contains("different pixels", StringComparison.Ordinal),
                TimeSpan.FromSeconds(10));
            _output.WriteLine($"English summary: {englishSummary}");
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
    public void ChineseSystem_EmptyStateAndError_AreLocalized()
    {
        string root = TestPaths.RepositoryRoot();
        string exe = TestPaths.AppExecutable(root);

        // Empty state.
        TestPaths.KillRunningInstances();
        using (Application app = Application.Launch(new ProcessStartInfo
               {
                   FileName = exe,
                   Arguments = "--lang zh-CN",
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
                    "StatusText",
                    text => text.Contains("打开或拖入", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(15));
                Assert.Equal("打开图像 A", UiActions.Find(window, "EmptyOpenAButton").Name);
                TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-2-empty-zh.png");
            }
            finally
            {
                CloseQuietly(app);
            }
        }

        // Corrupt file error dialog.
        string corrupt = Path.Combine(root, "artifacts", "test-input", "corrupt-zh.png");
        Directory.CreateDirectory(Path.GetDirectoryName(corrupt)!);
        File.WriteAllBytes(corrupt, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 9, 8, 7, 6 });

        TestPaths.KillRunningInstances();
        using (Application app = Application.Launch(new ProcessStartInfo
               {
                   FileName = exe,
                   Arguments = $"--a \"{corrupt}\" --lang zh-CN",
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

                UiActions.WaitFor(
                    () => UiActions.FindFirstWithRetry(window, cf => cf.ByName("无法打开图像")),
                    TimeSpan.FromSeconds(20),
                    "localized corrupt-file dialog");
                Assert.Contains(
                    window.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text)),
                    element => element.Name.Contains("无法解码", StringComparison.Ordinal));
                TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-2-error-corrupt-zh.png");

                AutomationElement? ok = UiActions.FindFirstWithRetry(window, cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName("确定")));
                ok?.AsButton().Invoke();
                Thread.Sleep(400);
            }
            finally
            {
                CloseQuietly(app);
            }
        }

        // Single-image hint.
        TestPaths.KillRunningInstances();
        using (Application app = Application.Launch(new ProcessStartInfo
               {
                   FileName = exe,
                   Arguments = $"--a \"{TestPaths.Image(root, "dashboard-before.png")}\" --lang zh-CN",
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

                string status = UiActions.WaitForText(
                    window,
                    "StatusText",
                    text => text.Contains("已载入图像 A", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(20));
                _output.WriteLine($"Chinese single-image status: {status}");
                Assert.Contains("打开图像 B", status);
                TestPaths.SaveScreenshot(window, root, "artifacts/acceptance/phase-2-single-image-zh.png");
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
