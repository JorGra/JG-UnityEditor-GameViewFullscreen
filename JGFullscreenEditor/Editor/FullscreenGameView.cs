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

        // Windows API imports (64-bit safe)
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        // Set Constantant
        private const int GWL_STYLE = -16;
        private const uint WS_POPUP = 0x80000000;
        private const uint WS_VISIBLE = 0x10000000;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_SHOWWINDOW = 0x0040;

        // Store original window style & position
        static IntPtr originalStyle;
        static Rect originalPosition;
        static IntPtr hwndInstance;

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
                if (hwndInstance != IntPtr.Zero)
                {
                    SetWindowLongPtr(hwndInstance, GWL_STYLE, originalStyle); // restore style
                    SetWindowPos(hwndInstance, HWND_NOTOPMOST,
                        (int)originalPosition.x, (int)originalPosition.y,
                        (int)originalPosition.width, (int)originalPosition.height,
                        SWP_SHOWWINDOW);
                }
                hwndInstance = IntPtr.Zero;

                instance.Close();
                instance = null;

                EditorApplication.delayCall += ApplyToolbarPatches;
            }
            else
            {
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
                instance.position = fullscreenRect;
                originalPosition = instance.position;                
                instance.Focus();

                // Native fullscreen hack
                EditorApplication.delayCall += () =>
                {
                    hwndInstance = FindWindow(null, instance.titleContent.text);
                    if (hwndInstance != IntPtr.Zero)
                    {
                        originalStyle = GetWindowLongPtr(hwndInstance, GWL_STYLE);
                        IntPtr newStyle = new IntPtr((originalStyle.ToInt64() & ~0x00C00000L) | WS_POPUP | WS_VISIBLE);
                        SetWindowLongPtr(hwndInstance, GWL_STYLE, newStyle);
                        SetWindowPos(hwndInstance, HWND_TOPMOST, 0, 0, monitorWidth, monitorHeight, SWP_SHOWWINDOW);
                    }

                    ApplyToolbarPatches();
                };

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

}
#endif
