using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VisualDiffLab.UITests;

/// <summary>
/// UI Automation geometry is only reliable when the test process is per-monitor DPI aware.
/// The test host otherwise reports virtualized rectangles and mouse input lands elsewhere.
/// </summary>
internal static class UiTestBootstrap
{
    private static bool _initialized;

    [ModuleInitializer]
    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        try
        {
            SetProcessDpiAwarenessContext(new IntPtr(-4)); // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
        }
        catch (Exception)
        {
            // Older hosts may already have a fixed awareness; UI tests then report a
            // clear geometry mismatch instead of silently clicking the wrong place.
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);
}
