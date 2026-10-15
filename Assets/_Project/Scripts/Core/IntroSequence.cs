using System.Collections;
using Semblance.Audio;
using Semblance.Residents;
using Semblance.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Semblance.Core
{
    /// <summary>
    /// The opening of a new case: the letter (on a worn VHS tape), then the drive into Semblance at dusk,
    /// ending in the Pale Moon Motel's lot. Space skips a line, Esc skips the lot.
    /// </summary>
    public class IntroSequence : MonoBehaviour
    {
        [SerializeField] private Transform player;

        public static bool Playing { get; private set; }

        private Canvas canvas;
        private TextMeshProUGUI text;
        private bool skipAll;

        public void Configure(Transform playerTransform) => player = playerTransform;

        private void Start()
        {
            GameFlow.NewGameStarted += Begin;
        }

        private void OnDestroy() => GameFlow.NewGameStarted -= Begin;

        private void Begin() => StartCoroutine(Run());

        private IEnumerator Run()
        {
            Playing = true;
            InputLock.Push(this, () => skipLine = true);
            var clock = GameClock.Instance;
            if (clock != null) clock.Frozen = true;
            ScreenFader.Instance?.SetBlack(1f);
            Build();
            var vhs = gameObject.AddComponent<VhsOverlay>();
            var hiss = GameAudio.Loop("static_loop", null, 0.18f);

            string[] lines =
            {
                "OCTOBER 14, 1983.\nBarlow County Investigations. Det. Klein's desk.",
                "The letter came without a return address.\nOne line, in pencil, pressed hard enough to tear the paper:",
                "<size=130%><i>\"One of us is not one of us.\"</i></size>",
                "Postmarked SEMBLANCE. Population 212.\nNobody at the agency had ever heard of it.",
                "On the back, smaller: <i>\"Most of us live out past the county line. It's one of the sixteen who live inside the sound of the bell.\"</i>",
                "You drove out the next morning.",
            };
            foreach (var line in lines)
            {
                if (skipAll) break;
                yield return Type(line);
            }
            text.text = "";

            if (!skipAll)
                yield return Drive(hiss);

            if (hiss) Destroy(hiss.gameObject);
            Destroy(vhs);
            canvas.gameObject.SetActive(false);
            Arrive();
            if (clock != null) clock.Frozen = false;
            InputLock.Pop(this);
            Playing = false;
            if (ScreenFader.Instance != null)
            {
                yield return ScreenFader.Instance.TitleCard("DAY 1", GameFlow.DateLine(1) + "\n<i>The Pale Moon Motel, Semblance</i>", 2f);
                yield return ScreenFader.Instance.Fade(0f, 2f);
            }
            UI.PlayerHUD.Instance?.ShowMessage($"One of these sixteen people isn't human. When you can prove which, ring the church bell.\n<size=80%>You have {GameSettings.DayCount} days.</size>", 7f);
            TutorialDirector.Instance?.Begin();
        }

        private bool skipLine;
        private float skipHeld;

        private void Update()
        {
            // Hold Esc / B to skip the whole intro; a tap only skips a line.
            if (!Playing) return;
            skipHeld = GameInput.Held(GameInput.Cancel) ? skipHeld + Time.unscaledDeltaTime : 0f;
            if (skipHeld > 1.1f) skipAll = true;
        }

        private IEnumerator Type(string line)
        {
            text.text = line;
            text.maxVisibleCharacters = 0;
            yield return null;
            float shown = 0;
            int total = text.textInfo.characterCount;
            while (shown < total && !skipAll)
            {
                int before = (int)shown;
                shown += Time.deltaTime * 32f;
                text.maxVisibleCharacters = (int)shown;
                if ((int)shown != before && (int)shown % 2 == 0) GameAudio.Play("typewriter_key", 0.35f, Random.Range(0.9f, 1.1f));
                if (GameInput.Pressed(GameInput.Advance) || skipLine) { shown = total; skipLine = false; }
                yield return null;
                total = text.textInfo.characterCount;
            }
            text.maxVisibleCharacters = 99999;
            float wait = 0;
            while (wait < 2.4f && !skipAll)
            {
                wait += Time.deltaTime;
                if ((GameInput.Pressed(GameInput.Advance) || skipLine) && wait > 0.2f) { skipLine = false; break; }
                yield return null;
            }
        }

        /// <summary>Riding into town in the car: the player's own camera, carried up the highway.</summary>
        private IEnumerator Drive(AudioSource hiss)
        {
            if (player == null) yield break;
            var cc = player.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            var car = BuildCar();
            Vector3 from = new Vector3(1.8f, 0.05f, -278f), to = new Vector3(1.8f, 0.05f, -214f);
            player.SetPositionAndRotation(from, Quaternion.identity);
            car.SetParent(player, false);
            var engine = GameAudio.Loop("drone_deep_loop", null, 0.1f);
            if (ScreenFader.Instance != null) StartCoroutine(ScreenFader.Instance.Fade(0f, 2f));
            const float duration = 15f;
            for (float t = 0; t < duration && !skipAll; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                player.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(t * 11f) * 0.01f;
                if (hiss) GameAudio.SetVolume(hiss, Mathf.Lerp(0.18f, 0.04f, k));
                if (t > duration - 4f && text.text == "") text.text = "<size=70%>The motel's sign was the only light for miles.</size>";
                yield return null;
            }
            if (ScreenFader.Instance != null) yield return ScreenFader.Instance.Fade(1f, 1.2f);
            if (engine) Destroy(engine.gameObject);
            Destroy(car.gameObject);
            text.text = "";
            if (cc) cc.enabled = true;
        }

        private Transform BuildCar()
        {
            var shader = Shader.Find("Semblance/PS1 Lit");
            Material Mat(Color c)
            {
                var m = new Material(shader);
                m.SetColor("_BaseColor", c);
                return m;
            }
            var root = new GameObject("Detective's Car").transform;
            var body = Mat(new Color(0.15f, 0.17f, 0.2f));
            var dash = Mat(new Color(0.07f, 0.06f, 0.06f));
            void Part(string name, Vector3 pos, Vector3 size, Material m, Vector3 euler = default)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(root, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(euler);
                go.transform.localScale = size;
                go.GetComponent<Renderer>().sharedMaterial = m;
            }
            Part("Dashboard", new Vector3(0.2f, 0.98f, 0.8f), new Vector3(1.9f, 0.25f, 0.5f), dash);
            Part("Wheel", new Vector3(-0.35f, 1.25f, 0.5f), new Vector3(0.38f, 0.38f, 0.04f), dash, new Vector3(-25f, 0, 0));
            Part("Hood", new Vector3(0.2f, 0.95f, 2.0f), new Vector3(1.8f, 0.12f, 2f), body, new Vector3(4f, 0, 0));
            Part("Pillar L", new Vector3(-0.85f, 1.55f, 1.1f), new Vector3(0.05f, 0.9f, 0.05f), body, new Vector3(-28f, 0, 0));
            Part("Pillar R", new Vector3(1.25f, 1.55f, 1.1f), new Vector3(0.05f, 0.9f, 0.05f), body, new Vector3(-28f, 0, 0));
            var lightGO = new GameObject("Headlights");
            lightGO.transform.SetParent(root, false);
            lightGO.transform.localPosition = new Vector3(0.2f, 0.9f, 3f);
            lightGO.transform.localRotation = Quaternion.Euler(8f, 0, 0);
            var l = lightGO.AddComponent<Light>();
            l.type = LightType.Spot;
            l.range = 35f;
            l.spotAngle = 60f;
            l.intensity = 6f;
            l.color = new Color(1f, 0.92f, 0.75f);
            return root;
        }

        private void Arrive()
        {
            var lot = ScheduleSpot.Find("Motel:Lot");
            if (player == null || lot == null) return;
            var cc = player.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            player.SetPositionAndRotation(lot.transform.position + Vector3.up * 0.05f, Quaternion.identity);
            if (cc) cc.enabled = true;
            Physics.SyncTransforms();
        }

        private void Build()
        {
            canvas = UIFactory.Canvas("Intro Canvas", transform, 160);
            text = UIFactory.Text("Line", canvas.transform, "", 28, UIFactory.Light, TextAlignmentOptions.Center);
            UIFactory.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 300));
            var hint = UIFactory.Text("Skip", canvas.transform, GameInput.Format("{advance}: next line     hold {cancel}: skip intro"), 17, new Color(1, 1, 1, 0.55f), TextAlignmentOptions.BottomRight);
            UIFactory.Place(hint.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 20), new Vector2(400, 24));
        }
    }
}
