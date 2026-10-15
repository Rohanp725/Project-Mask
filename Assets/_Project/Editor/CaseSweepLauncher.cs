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

        static CaseSweepLauncher()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
                SessionState.SetBool(Flag, false);
                Application.runInBackground = true;
                new GameObject("Case Sweep").AddComponent<Case.CaseSweep>();
            };
        }

        [MenuItem("Semblance/Run Overnight Case Sweep")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(TownBuilder.ScenePath);
            SessionState.SetBool(Flag, true);
            EditorApplication.isPlaying = true;
        }
    }
}
