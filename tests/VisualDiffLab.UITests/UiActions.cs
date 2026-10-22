using System.Diagnostics;
using System.Runtime.InteropServices;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit;
using Application = FlaUI.Core.Application;

namespace VisualDiffLab.UITests;

internal static class UiActions
{
    /// <summary>
    /// UIA can time out transiently while the app is doing large bitmap work. Retry a few
    /// times so the tests fail on real behavior, not on a single busy-automation timeout.
    /// </summary>
    public static AutomationElement? FindFirstWithRetry(
        AutomationElement root,
        Func<ConditionFactory, ConditionBase> selector,
        int attempts = 10)
    {
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                return root.FindFirstDescendant(selector);
            }
            catch (COMException) when (i < attempts - 1)
            {
                Thread.Sleep(200);
            }
            catch (TimeoutException) when (i < attempts - 1)
            {
                Thread.Sleep(200);
            }
        }

        return null;
    }

    public static AutomationElement Find(Window window, string automationId)
    {
        AutomationElement? element = FindFirstWithRetry(window, cf => cf.ByAutomationId(automationId));
        Assert.True(element is not null, $"Element '{automationId}' was not found.");
        return element!;
    }

    public static void Invoke(Window window, string automationId)
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

    public static void SelectMode(Window window, string automationId)
    {
        AutomationElement element = Find(window, automationId);
        Assert.True(
            element.Patterns.SelectionItem.TryGetPattern(out var selection),
            $"{automationId} has no SelectionItem pattern.");
        selection!.Select();
        Thread.Sleep(400);
        Assert.True(IsModeSelected(window, automationId), $"{automationId} was not selected.");
    }

    public static bool IsModeSelected(Window window, string automationId)
    {
        AutomationElement element = Find(window, automationId);
        return element.Patterns.SelectionItem.TryGetPattern(out var selection) && selection.IsSelected;
    }

    public static void SetSlider(Window window, string automationId, double value)
    {
        AutomationElement element = Find(window, automationId);
        Assert.True(element.Patterns.RangeValue.TryGetPattern(out var range), $"{automationId} has no RangeValue pattern.");
        range!.SetValue(value);
        Thread.Sleep(200);
    }

    public static void SelectRadio(Window window, string automationId)
    {
        AutomationElement element = Find(window, automationId);
        if (element.Patterns.SelectionItem.TryGetPattern(out var selection))
        {
            selection.Select();
        }
        else if (element.Patterns.Toggle.TryGetPattern(out var toggle))
        {
            toggle.Toggle();
        }
        else
        {
            element.AsRadioButton().Click();
        }

        Thread.Sleep(500);
    }

    public static void SelectComboItem(Window window, string comboId, string itemName)
    {
        var combo = Find(window, comboId).AsComboBox();
        combo.Expand();
        Thread.Sleep(300);
        var item = combo.Items.FirstOrDefault(candidate => candidate.Name.Contains(itemName, StringComparison.OrdinalIgnoreCase));
        Assert.True(item is not null, $"Combo item '{itemName}' was not found in {comboId}.");
        item!.Select();
        Thread.Sleep(400);
    }

    public static string WaitForText(Window window, string automationId, Func<string, bool> predicate, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        string last = "<not found>";
        while (DateTime.UtcNow < deadline)
        {
            AutomationElement? element = FindFirstWithRetry(window, cf => cf.ByAutomationId(automationId));
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

    public static TResult WaitFor<TResult>(Func<TResult?> action, TimeSpan timeout, string description)
        where TResult : class
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            TResult? result = action();
            if (result is not null)
            {
                return result;
            }

            Thread.Sleep(250);
        }

        throw new TimeoutException($"Timed out waiting for {description}.");
    }

    /// <summary>
    /// Clicks the ContentDialog confirmation button, then fills the native Save As dialog.
    /// Returns once the target file exists.
    /// </summary>
    public static void ChooseExportLocation(
        Application app,
        UIA3Automation automation,
        Window window,
        string targetPath)
    {
        AutomationElement? confirm = WaitFor(
            () => FindFirstWithRetry(window, cf =>
                cf.ByControlType(ControlType.Button).And(cf.ByName("Choose location…"))),
            TimeSpan.FromSeconds(15),
            "export confirmation dialog");
        confirm.AsButton().Invoke();
        Thread.Sleep(800);

        AutomationElement saveDialog = WaitFor(
            () => FindSaveDialog(app, automation, window),
            TimeSpan.FromSeconds(20),
            "native Save As dialog");

        // Navigate the picker to the target directory first: the modern file dialog
        // treats a full path in the name field as a file name only.
        string targetDirectory = Path.GetDirectoryName(targetPath)!;
        string targetFileName = Path.GetFileName(targetPath);
        saveDialog.Focus();
        Thread.Sleep(300);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_L);
        Thread.Sleep(400);
        Keyboard.Type(targetDirectory);
        Thread.Sleep(300);
        Keyboard.Press(VirtualKeyShort.RETURN);
        Thread.Sleep(1500);

        AutomationElement? edit = FindFirstWithRetry(saveDialog, cf => cf.ByAutomationId("1001"))
                                 ?? FindFirstWithRetry(saveDialog, cf => cf.ByControlType(ControlType.Edit));
        Assert.True(edit is not null, "The Save As file name field was not found.");
        edit!.Focus();
        Thread.Sleep(300);
        var textBox = edit.AsTextBox();
        textBox.Text = targetFileName;
        Thread.Sleep(800);
        string entered = textBox.Text;
        if (!string.Equals(entered, targetFileName, StringComparison.OrdinalIgnoreCase))
        {
            textBox.Text = string.Empty;
            Thread.Sleep(200);
            Keyboard.Type(targetFileName);
            Thread.Sleep(700);
            entered = textBox.Text;
        }

        if (!string.Equals(entered, targetFileName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Could not enter the export file name. Field contains: '{entered}'.");
        }

        // Folder enumeration can finish after navigation and auto-select an existing file,
        // overwriting the name. Re-assert it immediately before pressing Save.
        textBox.Text = targetFileName;
        Thread.Sleep(300);
        entered = textBox.Text;
        if (!string.Equals(entered, targetFileName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"The file name changed before saving. Field contains: '{entered}'.");
        }

        AutomationElement? save = FindFirstWithRetry(saveDialog, cf => cf.ByAutomationId("1"))
                                 ?? FindFirstWithRetry(saveDialog, cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Save")))
                                 ?? FindFirstWithRetry(saveDialog, cf => cf.ByControlType(ControlType.Button).And(cf.ByName("保存")));
        Assert.True(save is not null, "The Save button was not found.");
        save!.AsButton().Invoke();
        Thread.Sleep(1200);

        // If the common dialog kept focus in the name field, Enter commits it as well.
        if (FindFirstWithRetry(window, cf => cf.ByControlType(ControlType.Window).And(cf.ByName("Save As"))) is not null)
        {
            edit.Focus();
            Thread.Sleep(200);
            Keyboard.Press(VirtualKeyShort.RETURN);
            Thread.Sleep(800);
        }

        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(targetPath) && new FileInfo(targetPath).Length > 0)
            {
                return;
            }

            Thread.Sleep(300);
        }

        string windows = string.Join(
            " | ",
            window.FindAllDescendants(cf => cf.ByControlType(ControlType.Window))
                .Select(element => $"{element.Name}[{element.ClassName}]"));
        string currentText = string.Empty;
        try
        {
            currentText = edit.AsTextBox().Text;
        }
        catch (Exception)
        {
        }

        throw new TimeoutException(
            $"Export file was not written: {targetPath}. Entered filename: '{entered}'. Current: '{currentText}'. Windows: {windows}");
    }

    private static AutomationElement? FindSaveDialog(Application app, UIA3Automation automation, Window mainWindow)
    {
        // The WinRT file picker is hosted as a child #32770 window inside the WinUI window.
        AutomationElement? hosted = FindFirstWithRetry(mainWindow, cf =>
            cf.ByControlType(ControlType.Window).And(cf.ByName("Save As")));
        if (hosted is not null)
        {
            return hosted;
        }

        // Some Windows versions expose the picker as a separate top-level window.
        foreach (Window topLevel in app.GetAllTopLevelWindows(automation))
        {
            if (topLevel.Properties.NativeWindowHandle.Value != mainWindow.Properties.NativeWindowHandle.Value &&
                topLevel.Title.Contains("Save", StringComparison.OrdinalIgnoreCase))
            {
                return topLevel;
            }
        }

        AutomationElement desktop = automation.GetDesktop();
        foreach (AutomationElement child in desktop.FindAllChildren())
        {
            if (child.ControlType == ControlType.Window &&
                child.Properties.ProcessId.Value == app.ProcessId &&
                (child.Name.Contains("Save", StringComparison.OrdinalIgnoreCase) ||
                 child.Name.Contains("保存", StringComparison.OrdinalIgnoreCase)))
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>
    /// Drives the native Open dialog: click an Open button first, then call this.
    /// Navigates to the folder and selects the file, mimicking a real user.
    /// </summary>
    public static void OpenFileThroughPicker(
        UIA3Automation automation,
        Window window,
        string filePath)
    {
        string directory = Path.GetDirectoryName(filePath)!;
        string fileName = Path.GetFileName(filePath);

        AutomationElement dialog = WaitFor(
            () => FindOpenDialog(automation, window),
            TimeSpan.FromSeconds(20),
            "native Open dialog");

        dialog.Focus();
        Thread.Sleep(300);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_L);
        Thread.Sleep(400);
        Keyboard.Type(directory);
        Thread.Sleep(300);
        Keyboard.Press(VirtualKeyShort.RETURN);
        Thread.Sleep(1500);

        // Prefer selecting the exact file in the list: programmatic name-field edits can
        // leave an auto-selected item (observed opening the 4K fixture instead).
        AutomationElement? listItem = FindFirstWithRetry(dialog, cf =>
                                        cf.ByControlType(ControlType.ListItem).And(cf.ByName(fileName)))
                                    ?? FindFirstWithRetry(dialog, cf =>
                                        cf.ByControlType(ControlType.DataItem).And(cf.ByName(fileName)));
        if (listItem is not null)
        {
            listItem.Click();
            Thread.Sleep(300);
            if (listItem.Patterns.Invoke.TryGetPattern(out var invokeFile))
            {
                invokeFile.Invoke();
            }
            else
            {
                listItem.DoubleClick();
            }

            DateTime itemDeadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < itemDeadline)
            {
                if (FindOpenDialog(automation, window) is null)
                {
                    return;
                }

                Thread.Sleep(250);
            }

            throw new TimeoutException("The Open dialog did not close after invoking the file item.");
        }

        AutomationElement? edit = FindFirstWithRetry(dialog, cf => cf.ByAutomationId("1001"))
                                 ?? FindFirstWithRetry(dialog, cf => cf.ByControlType(ControlType.Edit));
        Assert.True(edit is not null, "The Open dialog file name field was not found.");
        edit!.Focus();
        Thread.Sleep(250);
        var textBox = edit.AsTextBox();
        textBox.Text = fileName;
        Thread.Sleep(600);
        string entered = textBox.Text;
        if (!string.Equals(entered, fileName, StringComparison.OrdinalIgnoreCase))
        {
            textBox.Text = string.Empty;
            Thread.Sleep(200);
            Keyboard.Type(fileName);
            Thread.Sleep(500);
            entered = textBox.Text;
        }

        if (!string.Equals(entered, fileName, StringComparison.OrdinalIgnoreCase))
        {
            // The modern open dialog can keep an auto-selected file; a full path in the
            // name field is accepted by the open dialog and resolves the target explicitly.
            textBox.Text = filePath;
            Thread.Sleep(500);
            entered = textBox.Text;
            if (!string.Equals(entered, filePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Could not enter the open file name. Field contains: '{entered}'.");
            }
        }

        AutomationElement? open = FindFirstWithRetry(dialog, cf => cf.ByAutomationId("1"))
                                 ?? FindFirstWithRetry(dialog, cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Open")))
                                 ?? FindFirstWithRetry(dialog, cf => cf.ByControlType(ControlType.Button).And(cf.ByName("打开")));
        Assert.True(open is not null, "The Open button was not found.");
        open!.AsButton().Invoke();

        // Wait for the dialog to close.
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            if (FindOpenDialog(automation, window) is null)
            {
                return;
            }

            Thread.Sleep(250);
        }

        throw new TimeoutException("The Open dialog did not close after choosing a file.");
    }

    private static AutomationElement? FindOpenDialog(UIA3Automation automation, Window mainWindow)
    {
        AutomationElement? hosted = FindFirstWithRetry(mainWindow, cf =>
            cf.ByControlType(ControlType.Window).And(cf.ByName("Open")));
        if (hosted is not null)
        {
            return hosted;
        }

        // Some builds title the dialog "Open File" or use a localized label.
        foreach (AutomationElement child in automation.GetDesktop().FindAllChildren())
        {
            if (child.ControlType == ControlType.Window &&
                child.Properties.ProcessId.Value == mainWindow.Properties.ProcessId.Value &&
                (child.Name.Contains("Open", StringComparison.OrdinalIgnoreCase) ||
                 child.Name.Contains("打开", StringComparison.OrdinalIgnoreCase)) &&
                !child.Name.Contains("Save", StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    public static void CloseSaveDialogIfOpen(Application app, UIA3Automation automation, Window mainWindow)
    {
        AutomationElement? dialog = FindSaveDialog(app, automation, mainWindow);
        if (dialog is null)
        {
            return;
        }

        AutomationElement? cancel = dialog.FindFirstDescendant(cf => cf.ByAutomationId("2"))
                                   ?? dialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Cancel")));
        if (cancel is not null)
        {
            cancel.AsButton().Invoke();
        }
    }
}
