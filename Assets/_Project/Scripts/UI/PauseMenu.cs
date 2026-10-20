using Semblance.Audio;
using Semblance.Core;
using Semblance.Notes;
using TMPro;
using UnityEngine;

namespace Semblance.UI
{
    /// <summary>Esc with nothing else open. Time stops while it's up.</summary>
    public class PauseMenu : MonoBehaviour
    {
        private GameObject root;
        private GameObject main;
        private GameObject settings;
        private TextMeshProUGUI objective;

        private void Awake()
        {
            Build();
            root.SetActive(false);
        }

        private void Start() => UIRoot.PauseRequested += Open;

        private void OnDestroy() => UIRoot.PauseRequested -= Open;

        public void Open()
        {
            if (root.activeSelf) return;
            GameAudio.Play("ui_page", 0.6f);
            root.SetActive(true);
            main.SetActive(true);
            settings.SetActive(false);
            InputLock.Push(this, Back);
            SelectFirstOnEnable.Select(main);
            var clock = GameClock.Instance;
            if (objective != null)
                objective.text = "Find which of the sixteen isn't who they seem. Prove it, then ring the church bell."
                    + (clock != null ? $"  <size=80%>(Day {clock.Day} of {clock.LastDay})</size>" : "");
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        private void Back()
        {
            if (settings.activeSelf)
            {
                settings.SetActive(false);
                main.SetActive(true);
                return;
            }
            Close();
        }

        public void Close()
        {
            if (root.activeSelf) GameAudio.Play("ui_page", 0.5f, 0.95f);
            root.SetActive(false);
            InputLock.Pop(this);
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        private void Build()
        {
            var canvas = UIFactory.Canvas("Pause Canvas", transform, 100);
            var dim = UIFactory.Panel("Dim", canvas.transform, new Color(0, 0, 0, 0.8f));
            UIFactory.Stretch(dim.rectTransform);
            root = dim.gameObject;

            main = UIFactory.Rect("Main", dim.transform).gameObject;
            UIFactory.Stretch((RectTransform)main.transform);
            var title = UIFactory.Text("Title", main.transform, "PAUSED", 40, UIFactory.Light, TextAlignmentOptions.Center);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(500, 60));
            var save = Save.SaveSystem.Peek();
            bool tutorial = Tutorial.TutorialStage.Instance != null;
            var note = UIFactory.Text("Note", main.transform, tutorial ? "<size=80%>Night Shift: the tutorial. Nothing here is saved; skip it whenever you like.</size>"
                : save != null ? $"<size=80%>The game saves each night when you sleep in Room 6. Last save: Day {save.day}.</size>" : "<size=80%>The game saves each night when you sleep in Room 6.</size>", 16, new Color(1, 1, 1, 0.5f), TextAlignmentOptions.Center);
            UIFactory.Place(note.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(700, 30));

            float y = 60;
            void Item(string label, System.Action action)
            {
                var b = UIFactory.Button(label, main.transform, label, 22, UIFactory.Light, new Color(1, 1, 1, 0.08f), () => { GameAudio.Play("ui_click", 0.7f); action(); });
                UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(320, 46));
                y -= 56;
            }
            objective = UIFactory.Text("Objective", main.transform, "", 17, new Color(1f, 0.95f, 0.85f, 0.85f), TextAlignmentOptions.Center);
            UIFactory.Place(objective.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 92), new Vector2(760, 30));
            y = 40;
            Item("Resume", Close);
            Item("Notebook", () => { Close(); NotebookUI.Instance?.Open(); });
            if (tutorial) Item("Skip tutorial", () => { Close(); Tutorial.TutorialStage.Instance?.Skip(); });
            Item("Settings", () => { main.SetActive(false); settings.SetActive(true); });
            Item("Quit to main menu", () => { Close(); SceneLoader.MainMenu(); });
            Item("Quit game", () => { Application.Quit(); });

            settings = SettingsPanel.Build(dim.transform, () => { settings.SetActive(false); main.SetActive(true); });
            settings.SetActive(false);
        }
    }
}
