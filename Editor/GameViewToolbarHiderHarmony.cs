#if UNITY_EDITOR
using HarmonyLib;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace JG.Editor
{
    /// <summary>
    /// Hides the built-in GameView toolbar by patching:
    /// - UnityEditor.GameView.DoToolbarGUI (skip drawing)
    /// - UnityEditor.GameView.GetViewInWindow (expand rect to occupy toolbar space)
    /// </summary>
    [InitializeOnLoad]
    public static class GameViewToolbarHider
    {
        private const string HarmonyId = "com.jg.editor.gameviewtoolbarhider";
        private static readonly Harmony s_harmony = new Harmony(HarmonyId);

        private static bool s_isHidden;
        private const float ToolbarHeight = 20f; // Adjust if Unity version differs

        static GameViewToolbarHider() { /* no auto-patching on load */ }

        /// <summary>Show/hide the GameView toolbar by applying/removing Harmony patches.</summary>
        public static void SetHidden(bool hidden)
        {
            if (hidden == s_isHidden)
                return;

            s_isHidden = hidden;

            if (s_isHidden)
                ApplyPatches();
            else
                RemovePatches();
        }

        private static void ApplyPatches()
        {
            var gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null)
            {
                Debug.LogError("[GameViewToolbarHider] UnityEditor.GameView type not found.");
                return;
            }

            var doToolbarGUIMethod = gameViewType.GetMethod("DoToolbarGUI", BindingFlags.Instance | BindingFlags.NonPublic);
            if (doToolbarGUIMethod != null)
            {
                s_harmony.Patch(doToolbarGUIMethod,
                    prefix: new HarmonyMethod(typeof(GameViewToolbarHider), nameof(DoToolbarGUIPrefix)));
            }

            var getViewInWindowMethod = gameViewType.GetMethod("GetViewInWindow", BindingFlags.Instance | BindingFlags.NonPublic);
            if (getViewInWindowMethod != null)
            {
                s_harmony.Patch(getViewInWindowMethod,
                    postfix: new HarmonyMethod(typeof(GameViewToolbarHider), nameof(GetViewInWindowPostfix)));
            }
        }

        private static void RemovePatches()
        {
            s_harmony.UnpatchAll(HarmonyId);
        }

        // Skip drawing toolbar buttons entirely.
        private static bool DoToolbarGUIPrefix() => false;

        // Expand the view rect upward into where the toolbar would be.
        private static void GetViewInWindowPostfix(ref Rect __result)
        {
            __result = new Rect(
                __result.x,
                __result.y - ToolbarHeight,
                __result.width,
                __result.height + ToolbarHeight
            );
        }
    }
}
#endif
