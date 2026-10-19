using UnityEditor;
using UnityEditor.SceneManagement;

namespace Semblance.EditorTools
{
    /// <summary>Pressing Play always starts on the title screen, whatever scene is open in the editor.</summary>
    public static class PlayFromMenu
    {
        [InitializeOnLoadMethod]
        private static void Apply()
        {
            var menu = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuBuilder.ScenePath);
            if (menu != null) EditorSceneManager.playModeStartScene = menu;
        }

        [MenuItem("Semblance/Play From Open Scene Instead")]
        private static void Toggle()
        {
            if (EditorSceneManager.playModeStartScene != null) EditorSceneManager.playModeStartScene = null;
            else Apply();
            UnityEngine.Debug.Log("[Semblance] Play starts from " + (EditorSceneManager.playModeStartScene != null ? "the main menu" : "the open scene"));
        }
    }
}
