using System.Runtime.InteropServices;

namespace VisualDiffLab.App.Services;

/// <summary>
/// Enforces a minimum window size with WM_GETMINMAXINFO. WinUI has no managed API for
/// this; the values are in DIPs and scaled for the current monitor DPI.
/// </summary>
internal sealed class WindowMinSize : IDisposable
{
    private const uint WmGetMinMaxInfo = 0x0024;
    private const uint WmDpiChanged = 0x02E0;

    private readonly IntPtr _hwnd;
    private readonly int _minWidth;
    private readonly int _minHeight;
    private readonly SubclassProc _proc;
    private bool _disposed;

    public WindowMinSize(IntPtr hwnd, int minWidth, int minHeight)
    {
        _hwnd = hwnd;
        _minWidth = minWidth;
        _minHeight = minHeight;
        _proc = OnSubclass;
        if (!SetWindowSubclass(hwnd, _proc, UIntPtr.Zero, IntPtr.Zero))
        {
            throw new InvalidOperationException("Failed to install the window sizing subclass.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        RemoveWindowSubclass(_hwnd, _proc, UIntPtr.Zero);
    }

    private IntPtr OnSubclass(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam, UIntPtr id, IntPtr data)
    {
        if (message == WmGetMinMaxInfo && lParam != IntPtr.Zero)
        {
            MinMaxInfo info = Marshal.PtrToStructure<MinMaxInfo>(lParam);
            double scale = Math.Max(1, GetDpiForWindow(hWnd)) / 96.0;
            info.MinTrackSize.X = (int)Math.Ceiling(_minWidth * scale);
            info.MinTrackSize.Y = (int)Math.Ceiling(_minHeight * scale);
            Marshal.StructureToPtr(info, lParam, false);
            return IntPtr.Zero;
        }

        if (message == WmDpiChanged && lParam != IntPtr.Zero)
        {
            // Let the default handler apply the suggested DPI-scaled window rect.
            return DefSubclassProc(hWnd, message, wParam, lParam);
        }

        return DefSubclassProc(hWnd, message, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr SubclassProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam, UIntPtr id, IntPtr data);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc proc, UIntPtr id, IntPtr data);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc proc, UIntPtr id);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);
}
