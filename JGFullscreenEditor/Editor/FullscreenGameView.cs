#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace JG.Editor
{
    /// <summary>
    /// Toggles a borderless, popup GameView at the current monitor resolution.
    /// - F12 while the editor is Playing.
    /// - Hides the Windows taskbar (no-op on macOS/Linux).
    /// - Makes the popup PMv2 DPI-aware to avoid OS upscaling.
    /// - Hides the GameView toolbar via Harmony patch while fullscreen is active.
    /// </summary>
    public static class FullscreenGameView
    {
        private const string MenuPath = "Window/General/Game (Fullscreen) _F12";

        private static readonly Type s_GameViewType =
            Type.GetType("UnityEditor.GameView,UnityEditor");

        // Non-public: int SizeSelectionCallback(int index, object userData)
        private static readonly MethodInfo s_SizeSelectionCallback =
            s_GameViewType?.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.NonPublic);

        private static EditorWindow s_fullscreenWindow;

        [MenuItem(MenuPath, priority = 2)]
        public static void Toggle()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("You can only enter fullscreen mode while the game is running.");
                return;
            }

            if (s_fullscreenWindow != null)
            {
                ExitFullscreen();
            }
            else
            {
                EnterFullscreen();
            }
        }

        private static void EnterFullscreen()
        {
            if (s_GameViewType == null)
            {
                Debug.LogError("UnityEditor.GameView type not found.");
                return;
            }

            WindowsTaskbar.Hide();

            // Create a new GameView window instance.
            s_fullscreenWindow = (EditorWindow)ScriptableObject.CreateInstance(s_GameViewType);

            // Determine logical size (pixels / DPI scale) for correct EditorWindow coordinates.
            float dpiScale = WinDpi.GetScaleForActiveWindow();
            int logicalW = Mathf.RoundToInt(Screen.currentResolution.width / dpiScale);
            int logicalH = Mathf.RoundToInt(Screen.currentResolution.height / dpiScale);
            var fullscreenRect = new Rect(0, 0, logicalW, logicalH);

            // Force "Free Aspect" (index 0 is typically Free Aspect) so the view matches our window client area.
            if (s_SizeSelectionCallback != null)
            {
                try { s_SizeSelectionCallback.Invoke(s_fullscreenWindow, new object[] { 0, null }); } catch { /* best effort */ }
            }

            // Show popup while thread is PMv2 DPI-aware to avoid blurry upscaling.
            using (new WinDpi.DpiScope())
            {
                s_fullscreenWindow.ShowPopup();
            }

            // Lock the window to the exact fullscreen client size.
            s_fullscreenWindow.minSize = new Vector2(logicalW, logicalH);
            s_fullscreenWindow.maxSize = s_fullscreenWindow.minSize;
            s_fullscreenWindow.position = fullscreenRect;
            s_fullscreenWindow.Focus();

            // Apply toolbar-hiding patches on the next editor tick (after Unity finalizes the view).
            EditorApplication.delayCall += () => GameViewToolbarHider.SetHidden(true);
        }

        private static void ExitFullscreen()
        {
            // Remove toolbar patches on the next editor tick.
            EditorApplication.delayCall += () => GameViewToolbarHider.SetHidden(false);

            WindowsTaskbar.Show();

            if (s_fullscreenWindow != null)
            {
                s_fullscreenWindow.Focus();
                s_fullscreenWindow.Close();
                s_fullscreenWindow = null;
            }
        }

        [MenuItem("Window/LayoutShortcuts/Default", false, 2)]
        private static void ResetToDefaultLayout()
        {
            EditorApplication.ExecuteMenuItem("Window/Layouts/Default");
        }
    }

    /// <summary>
    /// Helper to hide/show the Windows taskbar while in popup fullscreen.
    /// No-op on macOS/Linux.
    /// </summary>
    public static class WindowsTaskbar
    {
#if UNITY_EDITOR_WIN
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        [DllImport("user32.dll")] private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll")] private static extern int ShowWindow(IntPtr hWnd, int nCmdShow);

        public static void Hide()
        {
            var h = FindWindow("Shell_TrayWnd", null);
            if (h != IntPtr.Zero) ShowWindow(h, SW_HIDE);
        }

        public static void Show()
        {
            var h = FindWindow("Shell_TrayWnd", null);
            if (h != IntPtr.Zero) ShowWindow(h, SW_SHOW);
        }
#else
        public static void Hide() { /* no-op */ }
        public static void Show() { /* no-op */ }
#endif
    }
}
#endif
