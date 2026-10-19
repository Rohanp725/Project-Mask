using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Semblance.Audio;
using Semblance.Core;
using Semblance.Events;
using Semblance.Notes;
using Semblance.Player;
using Semblance.Residents;
using Semblance.Save;
using Semblance.UI;
using TMPro;
using UnityEngine;

namespace Semblance.Tutorial
{
    /// <summary>
    /// Night Shift: Thursday, October 13, 1983, two nights before Semblance. The office, a letter, the sergeant,
    /// a photograph, and then the lights go out and the breaker is in the basement. Every mechanic in the game is
    /// taught once, in order, by something that would happen anyway. Nothing here is saved.
    /// </summary>
    public class TutorialStage : MonoBehaviour
    {
        public static TutorialStage Instance { get; private set; }

        [SerializeField] private Transform[] anchors;
        [SerializeField] private Camera cinematic;
        [SerializeField] private Transform[] lightRoots;
        [SerializeField] private Light[] emergencyLights;
        [SerializeField] private TutorialDog dog;
        [SerializeField] private TutorialBreaker breaker;
        [SerializeField] private World.Door closetDoor;
        [SerializeField] private Renderer furnaceGlow;

        private Transform player;
        private FirstPersonController controller;
        private Camera playerCam;
        private PlayerHUD hud;
        private ScreenFader fader;
        private readonly List<Light> lights = new List<Light>();
        private readonly List<Renderer> bulbs = new List<Renderer>();
        private readonly List<AmbientEmitter> hums = new List<AmbientEmitter>();
        private bool photoTaken, breakerPulled, notebookWasOpen, finished;
        private AudioSource chase, rain;
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private Transform A(string name) => anchors.First(t => t != null && t.name == "A:" + name);

        private void Awake() => Instance = this;

        private void Start()
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            player = p.transform;
            controller = p.GetComponent<FirstPersonController>();
            playerCam = p.GetComponentInChildren<Camera>(true);
            hud = PlayerHUD.Instance;
            fader = ScreenFader.Instance;

            GameSettings.TutorialSeen = true; // from now on the game opens on the menu
            GameClock.ShowDay = false;
            var clock = GameClock.Instance;
            if (clock != null) { clock.SetTime(-1, 23.67f); clock.Frozen = true; } // twenty to twelve, all night
            NotebookUI.CaseIntroOverride = "<size=110%><b>Night shift</b></size>\nA letter on the desk, postmarked Semblance, and the sheriff's note under it: <i>look into this</i>. Everything you read, hear and see goes in here, filed by the person it concerns. Statements are what people tell you. Observations are what you see for yourself. When the two don't agree, link them.";
            PhotoCamera.Instance?.SetFilm(3);
            PhotoCamera.PhotoTaken += OnPhoto;
            if (breaker != null) breaker.Pulled += () => breakerPulled = true;

            foreach (var root in lightRoots)
            {
                if (root == null) continue;
                foreach (var l in root.GetComponentsInChildren<Light>(true))
                    if (Array.IndexOf(emergencyLights, l) < 0) lights.Add(l);
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    if (r.name == "Bulb" || r.name == "Fixture" || r.name == "Tube" || r.name == "Lamp Bulb") bulbs.Add(r);
                hums.AddRange(root.GetComponentsInChildren<AmbientEmitter>(true));
            }
            foreach (var l in emergencyLights) if (l != null) l.enabled = false;
            if (dog != null) dog.Vanish();
            StartCoroutine(Run());
        }

        private void OnDestroy()
        {
            PhotoCamera.PhotoTaken -= OnPhoto;
            if (Instance == this) Instance = null;
        }

        private void OnPhoto(List<Resident> _) => photoTaken = true;

        /// <summary>From the pause menu: straight to the title screen.</summary>
        public void Skip()
        {
            if (finished) return;
            finished = true;
            StopAllCoroutines();
            GameSettings.TutorialSeen = true;
            SceneLoader.MainMenu();
        }

        // ------------------------------------------------------------ helpers

        private void Hint(string text) => hud.SetHint(text, 0f);

        private IEnumerator Card(string text)
        {
            yield return new WaitForSeconds(0.2f);
            hud.ShowReading(GameInput.Format(text));
            yield return new WaitUntil(() => !hud.IsReading);
        }

        private static bool Asked(string displayName, string key) => SaveSystem.Flags.Contains($"asked|{displayName}|{key}");

        private bool Near(string anchor, float radius) => Vector3.Distance(player.position, A(anchor).position) < radius;

        private void Place(Vector3 position, Vector3 facing)
        {
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.ProjectOnPlane(facing, Vector3.up)));
            var root = player.Find("Camera Root");
            if (root != null) root.localRotation = Quaternion.identity;
            cc.enabled = true;
            Physics.SyncTransforms();
        }

        private void SetPower(bool on)
        {
            foreach (var l in lights) if (l != null) l.enabled = on;
            SetBulbs(bulbs, on);
            foreach (var l in emergencyLights) if (l != null) l.enabled = !on;
            foreach (var h in hums) if (h != null) h.gameObject.SetActive(on);
            if (furnaceGlow != null) SetBulbs(new[] { furnaceGlow }, on);
        }

        private static void SetBulbs(IEnumerable<Renderer> renderers, bool on)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor(EmissionId, Color.black);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (on) r.SetPropertyBlock(null); else r.SetPropertyBlock(block);
            }
        }

        /// <summary>The building coming back: room by room, from the panel outward, each with a thump of the tubes.</summary>
        private IEnumerator Restore()
        {
            foreach (var l in emergencyLights) if (l != null) l.enabled = false;
            for (int i = lightRoots.Length - 1; i >= 0; i--)
            {
                var root = lightRoots[i];
                if (root == null) continue;
                var roomLights = root.GetComponentsInChildren<Light>(true).Where(l => lights.Contains(l)).ToList();
                var roomBulbs = root.GetComponentsInChildren<Renderer>(true).Where(r => bulbs.Contains(r)).ToList();
                // A flicker before they hold.
                for (int k = 0; k < 2; k++)
                {
                    foreach (var l in roomLights) l.enabled = true;
                    SetBulbs(roomBulbs, true);
                    yield return new WaitForSeconds(0.06f);
                    foreach (var l in roomLights) l.enabled = false;
                    SetBulbs(roomBulbs, false);
                    yield return new WaitForSeconds(0.09f);
                }
                foreach (var l in roomLights) l.enabled = true;
                SetBulbs(roomBulbs, true);
                if (roomLights.Count > 0) GameAudio.PlayAt("flashlight_click", roomLights[0].transform.position, 0.5f, 0.6f, 30f);
                yield return new WaitForSeconds(0.45f);
            }
            foreach (var h in hums) if (h != null) h.gameObject.SetActive(true);
            if (furnaceGlow != null) SetBulbs(new[] { furnaceGlow }, true);
        }

        private IEnumerator TypedLine(string line)
        {
            var canvas = UIFactory.Canvas("Tutorial Card", null, 200); // over the fader's black
            var text = UIFactory.Text("Line", canvas.transform, "", 30, new Color(0.92f, 0.89f, 0.8f), TextAlignmentOptions.Center);
            UIFactory.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 80));
            for (int i = 1; i <= line.Length; i++)
            {
                text.text = line.Substring(0, i);
                if (line[i - 1] != ' ') GameAudio.Play("typewriter_key", 0.45f, UnityEngine.Random.Range(0.92f, 1.08f));
                yield return new WaitForSecondsRealtime(line[i - 1] == '.' ? 0.42f : 0.055f);
            }
            yield return new WaitForSecondsRealtime(1.5f);
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.8f) { text.alpha = t; yield return null; }
            Destroy(canvas.gameObject);
        }

        private IEnumerator Dolly(float seconds)
        {
            Vector3 from = A("CamStart").position, to = A("CamEnd").position;
            Vector3 look = A("WindowLook").position;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                var pos = Vector3.Lerp(from, to, k);
                pos.y += Mathf.Sin(t * 0.7f) * 0.03f; // held by a hand, not a rail
                cinematic.transform.position = pos;
                cinematic.transform.rotation = Quaternion.LookRotation(look - pos);
                yield return null;
            }
        }

        // ------------------------------------------------------------ the night

        private IEnumerator Run()
        {
            fader.SetBlack(1f);
            controller.InputLocked = true;
            InputLock.Push(this, null);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            hud.SetHint(null);
            playerCam.enabled = false;
            cinematic.gameObject.SetActive(true);
            cinematic.enabled = true;
            cinematic.clearFlags = CameraClearFlags.SolidColor;
            cinematic.backgroundColor = playerCam.backgroundColor;
            cinematic.transform.position = A("CamStart").position;
            cinematic.transform.rotation = Quaternion.LookRotation(A("WindowLook").position - A("CamStart").position);
            yield return null;

            rain = GameAudio.Loop("rain_loop", null, 0f, bus: AudioBus.Ambience);
            yield return TypedLine("Thursday night. The office. Two days before Semblance.");

            // Outside, in the rain, closing on the one lit window.
            StartCoroutine(Dolly(9.5f));
            StartCoroutine(FadeAudio(rain, 0.55f, 2.5f));
            yield return fader.Fade(0f, 2.4f);
            yield return new WaitForSeconds(5.9f);
            yield return fader.Fade(1f, 1.2f);

            // Inside: at the window, looking out at the same rain. (The whole camera object goes, so its screen
            // output goes with it.)
            cinematic.gameObject.SetActive(false);
            playerCam.enabled = true;
            Place(A("Window").position, A("Window").forward);
            StartCoroutine(FadeAudio(rain, 0.16f, 1.5f));
            yield return new WaitForSeconds(0.4f);
            InputLock.Pop(this);
            controller.InputLocked = false;
            yield return fader.Fade(0f, 2.2f);

            // 1. Walking.
            Hint("11:40 PM. Everyone's gone home but you and the sergeant.\n<size=75%>{move} to walk, {look} to look around, {sprint} to hurry. Your desk is the one with the lamp still on.</size>");
            yield return new WaitUntil(() => Near("Desk", 1.7f));

            // 2. Reading.
            Hint("There's a letter on the blotter. Look at it and press {interact} to read it.");
            yield return new WaitUntil(() => SaveSystem.Flags.Contains("seen:tut_letter"));
            yield return new WaitUntil(() => !hud.IsReading);
            yield return new WaitForSeconds(0.6f);

            // 3. The notebook.
            Hint("Press {notebook} to open your notebook. The letter's already in the Case File. Close it when you've had a look.");
            yield return new WaitUntil(() => NotebookUI.Instance != null && NotebookUI.Instance.IsOpen);
            notebookWasOpen = true;
            yield return new WaitUntil(() => !NotebookUI.Instance.IsOpen);
            yield return Card("Everything you read, hear or see goes in the notebook, filed by the person it concerns. Time stops while it's open. There's a page for your own notes, too.");

            // 4. Questioning.
            Hint("Sergeant Reyes is at the front desk. Press {interact} on her and ask about the letter (About what I've found...).");
            yield return new WaitUntil(() => Asked("Alma Reyes", "tut_letter"));
            Hint("Ask her about Teodoro (About people...). Then choose <b>Look them over</b>.");
            yield return new WaitUntil(() => Asked("Alma Reyes", "About Teodoro Baptiste") && Asked("Alma Reyes", "observe"));
            yield return new WaitUntil(() => Dialogue.DialogueUI.Instance == null || Dialogue.DialogueUI.Instance.Current == null);
            yield return Card("What people tell you is testimony. What you see for yourself is observation. In the world, hold {focus} on someone to look closer. Faces are where the lies are: the records in Semblance will show you how everyone used to look.");

            // 5. The camera. The flash goes off; so do the lights.
            Hint("Reyes wants the evidence board on film. Stand in front of it, press {camera} to raise the camera, {shoot} to take the photo.");
            yield return new WaitUntil(() => photoTaken);
            yield return new WaitForSeconds(1.1f);
            GameAudio.Play("power_down", 0.9f);
            SetPower(false);
            if (breaker != null) breaker.Armed = true;
            Nerve.Instance?.Shock(6f);
            yield return new WaitForSeconds(1.6f);
            hud.ShowMessage("Reyes, from the dark: \"That's the breaker. Basement, boiler room, far end of the archive. I'm not leaving the phones, Klein.\"", 6f);
            Hint("Press {flashlight} for your flashlight.");
            yield return new WaitUntil(() => Flashlight.Instance != null && Flashlight.Instance.IsOn);
            yield return new WaitForSeconds(0.8f);

            // 6. Down.
            Hint("The stairwell door is on the far wall, under the exit sign. Take the stairs down to the archive.");
            yield return new WaitUntil(() => player.position.y < -2.6f);
            yield return new WaitForSeconds(0.5f);
            yield return Card("Your nerve drains in the dark and steadies in the light. Keep the beam on what's in front of you. Lose all of it and you black out, and the night goes on without you.\n\nThe breaker is at the far end of the central aisle.");
            Hint("Follow the central aisle to the boiler room.");

            // 7. The aisle. Something falls, something walks.
            yield return new WaitUntil(() => Near("ArchiveEntry", 2.5f) || player.position.x > -6f);
            GameAudio.PlayAt("thud", A("DogEast").position + Vector3.up * 0.5f, 0.8f, 0.8f, 40f);
            yield return new WaitForSeconds(2f);
            yield return new WaitUntil(() => player.position.x > -3.2f);
            dog.Appear(A("DogEast").position, Vector3.left);
            dog.WalkTo(A("LockerFront").position);
            Nerve.Instance?.Shock(5f);
            Hint("<b>Something's coming down the aisle.</b> The locker on your right: press {interact} to hide.");
            float wait = 0f;
            while (!dog.Arrived && wait < 14f) { wait += Time.deltaTime; yield return null; }
            dog.Stay();
            bool hidden = HidingSpot.PlayerHidden;
            if (hidden) hud.SetHint("Hold still.", 0f);
            Nerve.Instance?.Shock(hidden ? 8f : 14f);
            yield return new WaitForSeconds(hidden ? 3.2f : 1.6f);
            dog.WalkTo(A("DogWest").position);
            wait = 0f;
            while (!dog.Arrived && wait < 12f) { wait += Time.deltaTime; yield return null; }
            dog.Vanish();
            if (HidingSpot.PlayerHidden)
            {
                Hint("It's gone. Press {interact} to climb out.");
                yield return new WaitUntil(() => !HidingSpot.PlayerHidden);
            }
            yield return new WaitForSeconds(0.5f);
            Hint("Keep going. The boiler room is at the end of the aisle.");

            // 8. It comes back, behind you.
            yield return new WaitUntil(() => player.position.x > A("ChaseStart").position.x || breakerPulled);
            if (!breakerPulled)
            {
                dog.Appear(A("DogWest").position, Vector3.right);
                dog.Follow(4.2f);
                chase = GameAudio.Loop("chase_loop", null, 0.3f, bus: AudioBus.Ambience);
                Rendering.PS1ScreenEffect.Distress = 0.35f;
                Nerve.Instance?.Shock(10f);
                Hint("<b>It's behind you.</b> Don't stop. {sprint} to run. Reset the breaker in the boiler room.");
                yield return new WaitUntil(() => breakerPulled);
            }

            // 9. Lights. It's a dog.
            dog.Stay();
            if (chase != null) Destroy(chase.gameObject);
            Rendering.PS1ScreenEffect.Distress = 0f;
            Hint(null);
            yield return Restore();
            if (!dog.gameObject.activeSelf) dog.Appear(A("BoilerDoor").position, Vector3.right);
            dog.WalkTo(A("DogSit").position);
            wait = 0f;
            while (!dog.Arrived && wait < 7f) { wait += Time.deltaTime; yield return null; }
            dog.Sit();
            Nerve.Instance?.Calm(45f);
            hud.ShowMessage("It's a dog. An old, half-blind dog with grey on her muzzle, and she is very pleased to see you.", 5f);
            yield return new WaitForSeconds(2.2f);
            var teo = Resident.FindByName("Teodoro Baptiste");
            if (closetDoor != null) closetDoor.ForceOpen(true);
            if (teo != null)
            {
                teo.DoorstepGreeting = "There she is. Biscuit, you old fool, come here. ...Detective. Sorry about the lights. Furnace trips the breaker when it turns cold, and I was up a ladder with a mop in my hand.";
                var mover = teo.GetComponent<ResidentMover>();
                if (mover != null) mover.WalkTo(A("TeoOut").position);
            }
            yield return new WaitForSeconds(2.5f);
            Hint("Talk to Teodoro ({interact}). Ask what he's doing down here (About yourself).");
            yield return new WaitUntil(() => Asked("Teodoro Baptiste", ResidentProfile.TopicSelf));
            yield return new WaitUntil(() => Dialogue.DialogueUI.Instance == null || Dialogue.DialogueUI.Instance.Current == null);

            // 10. The contradiction.
            yield return Card("Reyes said Teo clocked out at ten. Teo says he's been down here all night. Two statements that can't both be true.");
            Hint("Open your notebook ({notebook}), tick both of those notes, and link them.");
            yield return new WaitUntil(() => Notebook.Instance != null && Notebook.Instance.Contradictions.Count > 0);
            yield return new WaitUntil(() => NotebookUI.Instance == null || !NotebookUI.Instance.IsOpen);
            yield return Card("A link that holds is a mark against somebody. But people are wrong for ordinary reasons all the time, and a contradiction that's been explained isn't proof of anything.\n\nIn Semblance, one of the people you meet won't be a person. It will make a few mistakes. So will everybody else. The job is telling the difference.");

            // 11. Upstairs, and the ruling.
            Hint("Back upstairs. Put what Teo said to Reyes (About what I've found...).");
            yield return new WaitUntil(() => Asked("Alma Reyes", "tut_swap"));
            yield return new WaitUntil(() => Dialogue.DialogueUI.Instance == null || Dialogue.DialogueUI.Instance.Current == null);
            Hint("In the notebook, find Reyes's note about Teo (on Teodoro's page), press <b>ok?</b> and rule it <b>Explained</b>.");
            yield return new WaitUntil(() => AnyExplained());
            yield return new WaitUntil(() => NotebookUI.Instance == null || !NotebookUI.Instance.IsOpen);
            Hint(null);
            yield return new WaitForSeconds(0.6f);

            // 12. The folder.
            hud.ShowMessage("Reyes slides a folder across the counter without looking up from the phones. \"Semblance. Sheriff wants somebody to go up and look. You leave Saturday. Get some sleep, Klein.\"", 7f);
            yield return new WaitForSeconds(6.5f);
            yield return Card("That's the job. Talk to everyone, write everything down, look at faces, keep a light on. And when you're sure, ring the bell.");
            finished = true;
            InputLock.Push(this, null);
            controller.InputLocked = true;
            StartCoroutine(FadeAudio(rain, 0f, 2f));
            yield return fader.Fade(1f, 2.6f);
            GameSettings.TutorialSeen = true;
            SceneLoader.MainMenu();
        }

        private static bool AnyExplained()
        {
            var nb = Notebook.Instance;
            if (nb == null) return false;
            foreach (var subject in nb.Subjects)
                foreach (var e in nb.EntriesFor(subject))
                    if (e.verdict == (int)Verdict.Explained) return true;
            return false;
        }

        private static IEnumerator FadeAudio(AudioSource source, float to, float seconds)
        {
            if (source == null) yield break;
            float from = source.volume;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                if (source == null) yield break;
                source.volume = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }
            if (source != null) source.volume = to;
        }
    }
}
