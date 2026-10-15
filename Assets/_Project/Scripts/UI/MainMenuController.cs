using System.Collections;
using Semblance.Audio;
using Semblance.Core;
using Semblance.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Semblance.UI
{
    /// <summary>The title screen: a foggy road at night, the welcome sign, a music box, and the case file.</summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Transform cameraRig;

        private GameObject main;
        private GameObject difficulty;
        private GameObject settings;
        private TextMeshProUGUI title;
        private Vector3 cameraStart;

        private void Start()
        {
            InputLock.Push(this, () => { if (settings.activeSelf || difficulty.activeSelf) ShowMain(); });
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Build();
            gameObject.AddComponent<VhsOverlay>();
            GameAudio.Loop("music_theme", null, 0.55f);
            GameAudio.Loop("wind_loop", null, 0.25f);
            if (cameraRig != null) cameraStart = cameraRig.position;
            StartCoroutine(TypeTitle());
        }

        private void OnDestroy() => InputLock.Pop(this);

        private void Update()
        {
            if (cameraRig != null)
                cameraRig.position = cameraStart + new Vector3(Mathf.Sin(Time.time * 0.11f) * 0.4f, Mathf.Sin(Time.time * 0.17f) * 0.08f, Time.time * 0.08f % 6f);
            if (GameInput.Pressed(GameInput.Cancel))
                InputLock.EscapeTop();
        }

        private IEnumerator TypeTitle()
        {
            const string text = "SEMBLANCE";
            for (int i = 0; i <= text.Length; i++)
            {
                title.text = text.Substring(0, i);
                if (i > 0) GameAudio.Play("typewriter_key", 0.5f, Random.Range(0.9f, 1.1f));
                yield return new WaitForSeconds(0.16f);
            }
        }

        private void ShowMain()
        {
            main.SetActive(true);
            difficulty.SetActive(false);
            settings.SetActive(false);
        }

        private void Build()
        {
            var canvas = UIFactory.Canvas("Menu Canvas", transform, 50);
            title = UIFactory.Text("Title", canvas.transform, "", 92, UIFactory.Light, TextAlignmentOptions.Center);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(1000, 120));
            var subtitle = UIFactory.Text("Subtitle", canvas.transform, "<i>\"One of us is not one of us.\"</i>", 22, new Color(1, 1, 1, 0.55f), TextAlignmentOptions.Center);
            UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -185), new Vector2(900, 40));

            main = UIFactory.Rect("Main", canvas.transform).gameObject;
            UIFactory.Stretch((RectTransform)main.transform);
            float y = 10;
            void Item(Transform parent, string label, System.Action action, ref float yy)
            {
                var b = UIFactory.Button(label, parent, label, 24, UIFactory.Light, new Color(0, 0, 0, 0.45f), () => { GameAudio.Play("typewriter_key", 0.6f); action(); });
                UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, yy), new Vector2(360, 50));
                yy -= 60;
            }

            var save = SaveSystem.Peek();
            if (save != null)
                Item(main.transform, $"Continue  <size=70%>(Day {save.day}, {(Difficulty)save.difficulty})</size>", () => { GameSettings.Difficulty = (Difficulty)save.difficulty; SceneLoader.Continue(); }, ref y);
            Item(main.transform, "New case", () => { main.SetActive(false); difficulty.SetActive(true); }, ref y);
            Item(main.transform, "Settings", () => { main.SetActive(false); settings.SetActive(true); }, ref y);
            Item(main.transform, "Quit", Application.Quit, ref y);

            var credit = UIFactory.Text("Credit", canvas.transform, "Headphones recommended. Play in the dark.", 15, new Color(1, 1, 1, 0.35f), TextAlignmentOptions.Bottom);
            UIFactory.Place(credit.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(800, 30));

            difficulty = UIFactory.Panel("Difficulty", canvas.transform, new Color(0.05f, 0.045f, 0.04f, 0.95f)).gameObject;
            UIFactory.Place((RectTransform)difficulty.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(760, 400));
            var dTitle = UIFactory.Text("Title", difficulty.transform, "HOW CAREFUL IS THE THING YOU'RE HUNTING?", 22, UIFactory.Light, TextAlignmentOptions.Top);
            UIFactory.Place(dTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(700, 40));
            float dy = 90;
            void Choice(Difficulty d, string label, string blurb)
            {
                Button b = null;
                bool armed = false;
                string text = $"<b>{label}</b>\n<size=70%>{blurb}</size>";
                b = UIFactory.Button(d.ToString(), difficulty.transform, text, 22, UIFactory.Light, new Color(1, 1, 1, 0.06f), () =>
                {
                    // Starting a case replaces the saved one: ask once.
                    if (SaveSystem.HasSave && !armed)
                    {
                        armed = true;
                        b.GetComponentInChildren<TextMeshProUGUI>().text = "<b>Start a new case?</b>\n<size=70%>Your saved case will be lost. Choose again to confirm.</size>";
                        return;
                    }
                    GameSettings.Difficulty = d;
                    GameSettings.Save();
                    SceneLoader.NewGame();
                });
                UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, dy), new Vector2(680, 78));
                dy -= 90;
            }
            Choice(Difficulty.Easy, "Careless  <size=70%>(Easy)</size>", "6 days. It makes 4 mistakes. Fewer red herrings. You're told what you see.");
            Choice(Difficulty.Normal, "Careful  <size=70%>(Normal, recommended)</size>", "5 days. It makes 3 mistakes. You're told what you see.");
            Choice(Difficulty.Hard, "Patient  <size=70%>(Hard)</size>", "4 days. It makes 2 mistakes. More red herrings. You're NOT told what you see: you spot every detail yourself.");
            var back = UIFactory.Button("Back", difficulty.transform, "Back", 18, UIFactory.Light, UIFactory.Red, ShowMain);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(160, 36));
            difficulty.SetActive(false);

            settings = SettingsPanel.Build(canvas.transform, ShowMain);
            settings.SetActive(false);
            main.AddComponent<SelectFirstOnEnable>();
            difficulty.AddComponent<SelectFirstOnEnable>();
            settings.AddComponent<SelectFirstOnEnable>();
        }
    }
}
