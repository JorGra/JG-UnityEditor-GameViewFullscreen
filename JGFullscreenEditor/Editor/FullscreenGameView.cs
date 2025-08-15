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
        static readonly PropertyInfo ShowToolbarProperty = GameViewType.GetProperty("showToolbar", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly MethodInfo SetSizeProperty = GameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly object False = false;

        static EditorWindow instance;

        // Update the shortcut to F12
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

                var gameViewSizesInstance = GetGameViewSizesInstance();
                int monitorWidth = (int)(Screen.currentResolution.width / EditorGUIUtility.pixelsPerPoint);
                int monitorHeight = (int)(Screen.currentResolution.height / EditorGUIUtility.pixelsPerPoint);

                if (SetSizeProperty != null)
                {
                    int sizeIndex = FindResolutionSizeIndex(monitorWidth, monitorHeight, gameViewSizesInstance);
                    SetSizeProperty.Invoke(instance, new object[] { sizeIndex, null });
                }

                var desktopResolution = new Vector2(monitorWidth, monitorHeight);
                var fullscreenRect = new Rect(Vector2.zero, desktopResolution);
                instance.ShowPopup();

                instance.minSize = new Vector2(Screen.currentResolution.width, Screen.currentResolution.height);
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
        private static object GetGameViewSizesInstance()
        {
            var sizesType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            var singleType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instanceProp = singleType.GetProperty("instance");
            return instanceProp.GetValue(null, null);
        }

        private static int FindResolutionSizeIndex(int width, int height, object gameViewSizesInstance)
        {
            var groupType = gameViewSizesInstance.GetType().GetMethod("GetGroup");
            var currentGroup = groupType.Invoke(gameViewSizesInstance, new object[] { (int)GameViewType.GetMethod("GetCurrentGameViewSizeGroupType").Invoke(instance, null) });

            var getBuiltinCount = currentGroup.GetType().GetMethod("GetBuiltinCount");
            var getCustomCount = currentGroup.GetType().GetMethod("GetCustomCount");
            var getGameViewSize = currentGroup.GetType().GetMethod("GetGameViewSize");

            int totalSizes = (int)getBuiltinCount.Invoke(currentGroup, null) + (int)getCustomCount.Invoke(currentGroup, null);

            for (int i = 0; i < totalSizes; i++)
            {
                var size = getGameViewSize.Invoke(currentGroup, new object[] { i });
                var widthProp = size.GetType().GetProperty("width");
                var heightProp = size.GetType().GetProperty("height");

                int w = (int)widthProp.GetValue(size, null);
                int h = (int)heightProp.GetValue(size, null);

                if (w == width && h == height)
                {
                    return i;
                }
            }

            Debug.LogWarning("Resolution not found. Defaulting to index 0.");
            return 0;
        }

        [MenuItem("Window/LayoutShortcuts/Default", false, 2)]
        static void DefaultLayout()
        {
            EditorApplication.ExecuteMenuItem("Window/Layouts/Default");
        }
    }

    public static class WindowsTaskbar
    {
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern int ShowWindow(IntPtr hWnd, int nCmdShow);

        public static void Hide()
        {
            IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);
            if (taskbarHandle != IntPtr.Zero)
                ShowWindow(taskbarHandle, SW_HIDE);
        }

        public static void Show()
        {
            IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);
            if (taskbarHandle != IntPtr.Zero)
                ShowWindow(taskbarHandle, SW_SHOW);
        }
    }
}
#endif
