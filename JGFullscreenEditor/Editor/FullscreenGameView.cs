#if UNITY_EDITOR

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace JG.Editor
{
    public static class FullscreenGameView
    {
        static readonly Type GameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
        static readonly MethodInfo SetSizeProperty = GameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.NonPublic);

        static EditorWindow instance;

        // Update the shortcut to F12
        [MenuItem("Window/General/Game (Fullscreen) _F12", priority = 2)]
        [MenuItem("Window/General/Game (Fullscreen) _F12", priority = 2)]
        public static void Toggle()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("You can only enter fullscreen mode while the game is running.");
                return;
            }

            if (GameViewType == null)
            {
                Debug.LogError("GameView type not found.");
                return;
            }

            if (instance != null)
            {
                WindowsTaskbar.Show();

                instance.Focus();
                instance.Close();
                instance = null;

                EditorApplication.delayCall += ApplyToolbarPatches;
            }
            else
            {
                WindowsTaskbar.Hide();

                instance = (EditorWindow)ScriptableObject.CreateInstance(GameViewType);

                // *** Get DPI scale and compute logical (unscaled) size for coordinates ***
                float dpiScale = 1f;

                dpiScale = WinDpi.GetScaleForActiveWindow();
                int logicalW = Mathf.RoundToInt(Screen.currentResolution.width / dpiScale);
                int logicalH = Mathf.RoundToInt(Screen.currentResolution.height / dpiScale);

                // Select a size that matches the window's client area.
                // Easiest & robust: Free Aspect (usually index 0).
                if (SetSizeProperty != null)
                {
                    try
                    {
                        // Use Free Aspect to ensure no cropping regardless of window client size.
                        SetSizeProperty.Invoke(instance, new object[] { 0, null });
                    }
                    catch { /* ignore */ }
                }

                var fullscreenRect = new Rect(0, 0, logicalW, logicalH);

                // *** Create the popup while the thread is PMv2 DPI-aware (prevents OS upscaling) ***
                using (new WinDpi.DpiScope())
                {
                    instance.ShowPopup();
                }

                // *** Use logical sizes everywhere for the EditorWindow ***
                instance.minSize = new Vector2(logicalW, logicalH);
                instance.maxSize = instance.minSize;
                instance.position = fullscreenRect;

                instance.Focus();

                EditorApplication.delayCall += ApplyToolbarPatches;
            }
        }


        private static void ApplyToolbarPatches()
        {
            // Apply the patches here, now that Unity had a frame to finalize the view
            GameViewToolbarHiderAlternative.ToggleAlternateToolbarRemoval();
        }


        [MenuItem("Window/LayoutShortcuts/Default", false, 2)]
        static void DefaultLayout()
        {
            EditorApplication.ExecuteMenuItem("Window/Layouts/Default");
        }
    }

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
        public static void Hide() {}  // no-op on macOS/Linux
        public static void Show() {}
#endif
    }
}
#endif
