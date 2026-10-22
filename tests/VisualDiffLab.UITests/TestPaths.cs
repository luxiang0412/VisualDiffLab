using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace VisualDiffLab.UITests;

internal static class TestPaths
{
    public static string RepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "VisualDiffLab.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException("Repository root (VisualDiffLab.sln) was not found.");
    }

    public static string AppExecutable(string root)
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable("VDL_APP_EXE");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        string configuration = Environment.GetEnvironmentVariable("VDL_APP_CONFIG") ?? "Debug";
        return Path.Combine(
            root,
            "src",
            "VisualDiffLab.App",
            "bin",
            "x64",
            configuration,
            "net10.0-windows10.0.26100.0",
            "win-x64",
            "VisualDiffLab.exe");
    }

    public static string Image(string root, string name) =>
        Path.Combine(root, "assets", "test-images", name);

    public static void KillRunningInstances()
    {
        foreach (Process process in Process.GetProcessesByName("VisualDiffLab"))
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    public static void SaveScreenshot(Window window, string root, string relativePath, bool resetInspectorScroll = true)
    {
        ForceForeground(window);
        IntPtr hwnd = window.Properties.NativeWindowHandle.Value;
        Rectangle bounds = window.BoundingRectangle;
        if (hwnd != IntPtr.Zero)
        {
            // The UIA rectangle includes the invisible DWM resize border; capture only
            // the visible frame so screenshots have no stray desktop pixels.
            if (DwmGetWindowAttribute(hwnd, 9, out NativeRect frame, Marshal.SizeOf<NativeRect>()) == 0 &&
                frame.Right > frame.Left && frame.Bottom > frame.Top)
            {
                bounds = Rectangle.FromLTRB(frame.Left, frame.Top, frame.Right, frame.Bottom);
            }
        }

        // Move keyboard focus off toolbar buttons (which show focus tooltips) and park the
        // pointer over the status bar so hover tooltips also close before capture.
        AutomationElement? canvas = UiActions.FindFirstWithRetry(window, cf => cf.ByAutomationId("ComparisonCanvas"));
        try
        {
            canvas?.Focus();
        }
        catch (Exception)
        {
            // Focus is best-effort for clean evidence.
        }

        if (resetInspectorScroll)
        {
            ScrollInspector(window, 0);
        }

        Mouse.MoveTo(bounds.Left + (bounds.Width / 2), bounds.Top + bounds.Height - 30);
        Keyboard.Press(VirtualKeyShort.F15); // harmless key to dismiss lingering tooltips
        Thread.Sleep(800);

        string path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using Bitmap bitmap = new(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
        bitmap.Save(path, ImageFormat.Png);
        Thread.Sleep(250);
    }

    public static double ScaleForWindow(Window window)
    {
        IntPtr hwnd = window.Properties.NativeWindowHandle.Value;
        return GetDpiForWindow(hwnd) / 96.0;
    }

    public static void ResizeWindow(Window window, int widthDip, int heightDip)
    {
        IntPtr hwnd = window.Properties.NativeWindowHandle.Value;
        double scale = ScaleForWindow(window);
        Rectangle bounds = window.BoundingRectangle;
        MoveWindow(hwnd, bounds.Left, bounds.Top, (int)Math.Round(widthDip * scale), (int)Math.Round(heightDip * scale), true);
    }

    public static void Maximize(Window window)
    {
        IntPtr hwnd = window.Properties.NativeWindowHandle.Value;
        ShowWindow(hwnd, 3); // SW_MAXIMIZE
    }

    public static void Restore(Window window)
    {
        IntPtr hwnd = window.Properties.NativeWindowHandle.Value;
        ShowWindow(hwnd, 9); // SW_RESTORE
    }

    public static void ScrollInspector(Window window, double verticalPercent)
    {
        AutomationElement? inspectorScroll = UiActions.FindFirstWithRetry(window, cf => cf.ByAutomationId("InspectorScrollViewer"));
        if (inspectorScroll is not null && inspectorScroll.Patterns.Scroll.TryGetPattern(out var scrollPattern))
        {
            try
            {
                scrollPattern.SetScrollPercent(-1, verticalPercent);
            }
            catch (Exception)
            {
                // Not scrollable in the current state.
            }
        }

        Thread.Sleep(300);
    }

    public static void SetTopMost(Window window, bool topMost)
    {
        IntPtr hwnd = window.Properties.NativeWindowHandle.Value;
        if (hwnd != IntPtr.Zero)
        {
            SetWindowPos(hwnd, topMost ? new IntPtr(-1) : new IntPtr(-2), 0, 0, 0, 0, 0x0003);
        }
    }

    public static void ForceForeground(Window window)
    {
        IntPtr hwnd = window.Properties.NativeWindowHandle.Value;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        if (IsIconic(hwnd))
        {
            ShowWindow(hwnd, 9); // SW_RESTORE only when minimized; never un-maximize.
        }

        uint foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        uint currentThread = GetCurrentThreadId();
        AttachThreadInput(currentThread, foregroundThread, true);
        try
        {
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
            SetActiveWindow(hwnd);
        }
        finally
        {
            AttachThreadInput(currentThread, foregroundThread, false);
        }

        Thread.Sleep(600);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetActiveWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int command);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern bool MoveWindow(IntPtr hwnd, int x, int y, int width, int height, bool repaint);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out NativeRect value, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
