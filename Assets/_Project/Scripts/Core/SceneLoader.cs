using UnityEngine;
using UnityEngine.SceneManagement;

namespace Semblance.Core
{
    /// <summary>Scene flow: main menu, new game, continue.</summary>
    public static class SceneLoader
    {
        public const string MenuScene = "MainMenu";
        public const string TownScene = "Semblance_Town";

        /// <summary>True when the town should load from the save file instead of starting fresh.</summary>
        public static bool ContinueRequested { get; private set; }

        /// <summary>A case to replay exactly (0 = a new random one). Consumed when the case is generated.</summary>
        public static int ReplaySeed { get; set; }

        public static void NewGame()
        {
            Save.SaveSystem.DeleteSave();
            ContinueRequested = false;
            ReplaySeed = 0;
            Load(TownScene);
        }

        /// <summary>Start the same case again from the beginning (after a wrong accusation, or running out of time).</summary>
        public static void Replay(int seed)
        {
            Save.SaveSystem.DeleteSave();
            ContinueRequested = false;
            ReplaySeed = seed;
            Load(TownScene);
        }

        public static void Continue()
        {
            ContinueRequested = Save.SaveSystem.HasSave;
            Load(TownScene);
        }

        public static void MainMenu()
        {
            ContinueRequested = false;
            Load(Application.CanStreamedLevelBeLoaded(MenuScene) ? MenuScene : TownScene);
        }

        public static void ConsumeContinue() => ContinueRequested = false;

        private static bool loading;

        private static void Load(string scene)
        {
            if (loading) return;
            InputLockReset();
            Time.timeScale = 1f;
            loading = true;
            // The town is a big scene: read it in the background behind a black "Loading" card instead of freezing
            // the menu on the last frame.
            var runner = new GameObject("Scene Loader").AddComponent<LoadRunner>();
            Object.DontDestroyOnLoad(runner.gameObject);
            runner.StartCoroutine(runner.Run(scene));
        }

        private class LoadRunner : MonoBehaviour
        {
            public System.Collections.IEnumerator Run(string scene)
            {
                var canvasGO = new GameObject("Loading Canvas", typeof(Canvas));
                canvasGO.transform.SetParent(transform, false);
                var canvas = canvasGO.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32000;
                var black = new GameObject("Black", typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
                black.transform.SetParent(canvasGO.transform, false);
                black.color = Color.black;
                UIFactory.Stretch(black.rectTransform);
                var label = new GameObject("Label", typeof(TMPro.TextMeshProUGUI)).GetComponent<TMPro.TextMeshProUGUI>();
                label.transform.SetParent(canvasGO.transform, false);
                if (UIFactory.Font != null) label.font = UIFactory.Font;
                label.text = "Loading...";
                label.fontSize = 28;
                label.color = new Color(0.75f, 0.72f, 0.65f);
                label.alignment = TMPro.TextAlignmentOptions.BottomRight;
                UIFactory.Stretch(label.rectTransform, new Vector2(40, 30), new Vector2(-40, -30));
                yield return null; // show the card before the heavy work starts

                var op = SceneManager.LoadSceneAsync(scene);
                if (op == null)
                {
                    loading = false;
                    Destroy(gameObject);
                    yield break;
                }
                while (!op.isDone) yield return null;
                yield return null; // let the new scene build its UI under the card
                loading = false;
                Destroy(gameObject);
            }

            private void OnDestroy() => loading = false;
        }

        private static void InputLockReset()
        {
            while (InputLock.IsLocked)
                InputLock.Pop(InputLock.Top);
        }
    }
}
