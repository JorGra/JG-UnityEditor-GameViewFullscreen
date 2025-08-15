#if UNITY_EDITOR
using System;
using System.Runtime.InteropServices;

namespace JG.Editor
{
    /// <summary>
    /// Windows-only helpers for per-monitor DPI awareness.
    /// On non-Windows editors, falls back to scale 1.0 and a no-op scope.
    /// </summary>
    internal static class WinDpi
    {
#if UNITY_EDITOR_WIN
        [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll")] private static extern int GetDpiForWindow(IntPtr hWnd); // Win10+
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);
        [DllImport("Shcore.dll")] private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
        [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        private const int MONITOR_DEFAULTTOPRIMARY = 1;
        private const int MDT_EFFECTIVE_DPI = 0;
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = (IntPtr)(-4);

        public static float GetScaleForActiveWindow()
        {
            var hwnd = GetActiveWindow();
            if (hwnd != IntPtr.Zero)
            {
                try
                {
                    int dpi = GetDpiForWindow(hwnd);
                    if (dpi > 0) return dpi / 96f;
                }
                catch { /* best effort */ }

                try
                {
                    var mon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTOPRIMARY);
                    if (mon != IntPtr.Zero && GetDpiForMonitor(mon, MDT_EFFECTIVE_DPI, out var dx, out _) == 0)
                        return dx / 96f;
                }
                catch { /* best effort */ }
            }
            return 1f;
        }

        /// <summary>
        /// Temporarily switch the calling thread to PMv2 DPI awareness.
        /// Restores the previous DPI context on dispose.
        /// </summary>
        public sealed class DpiScope : IDisposable
        {
            private readonly IntPtr _prev;
            public DpiScope()
            {
                try { _prev = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2); }
                catch { _prev = IntPtr.Zero; }
            }
            public void Dispose()
            {
                try { if (_prev != IntPtr.Zero) SetThreadDpiAwarenessContext(_prev); } catch { }
            }
        }
#else
        // macOS/Linux editor: harmless defaults
        public static float GetScaleForActiveWindow() => 1f;
        public sealed class DpiScope : IDisposable { public void Dispose() { } }
#endif
    }
}
#endif
