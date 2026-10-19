using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Semblance.EditorTools
{
    /// <summary>
    /// Menu: Semblance > Run Overnight Case Sweep. Opens the town, enters Play mode, runs 600 generated cases
    /// (200 per difficulty) without freezing the editor, writes Logs/CaseSweepReport.txt and exits Play mode.
    /// </summary>
    [InitializeOnLoad]
    public static class CaseSweepLauncher
    {
        private const string Flag = "Semblance.RunCaseSweep";
        private const string RestoreFlag = "Semblance.RestorePlayFromMenu";

        static CaseSweepLauncher()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
                SessionState.SetBool(Flag, false);
                Application.runInBackground = true;
                new GameObject("Case Sweep").AddComponent<Case.CaseSweep>();
            };
            // The sweep runs in the town; once it's done, Play goes back to starting on the title screen.
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(RestoreFlag, false)) return;
                SessionState.SetBool(RestoreFlag, false);
                var menu = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuBuilder.ScenePath);
                if (menu != null) EditorSceneManager.playModeStartScene = menu;
            };
        }

        [MenuItem("Semblance/Run Overnight Case Sweep")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(TownBuilder.ScenePath);
            // Play normally starts on the title screen; the sweep needs the open town.
            if (EditorSceneManager.playModeStartScene != null) { EditorSceneManager.playModeStartScene = null; SessionState.SetBool(RestoreFlag, true); }
            SessionState.SetBool(Flag, true);
            EditorApplication.isPlaying = true;
        }
    }
}
