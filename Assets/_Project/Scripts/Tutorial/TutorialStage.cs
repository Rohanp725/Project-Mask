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
        [SerializeField] private World.Door boilerDoor;
        [SerializeField] private Renderer furnaceGlow;

        private Transform player;
        private FirstPersonController controller;
        private Camera playerCam;
        private PlayerHUD hud;
        private ScreenFader fader;
        private readonly List<Light> lights = new List<Light>();
        private readonly List<Renderer> bulbs = new List<Renderer>();
        private readonly List<AmbientEmitter> hums = new List<AmbientEmitter>();
        private bool photoTaken, breakerPulled, finished, lightsOut;
        private AudioSource chase, rain, drone, heart;
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
            var alma = Resident.FindByName("Alma Reyes");
            if (alma != null)
            {
                // Seated exactly where she was put: the agent would slide her off the chair's collider.
                if (alma.TryGetComponent<ResidentBehaviour>(out var ab)) ab.ForceSeated = true;
                var almaAgent = alma.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (almaAgent != null) almaAgent.enabled = false;
            }
            // Teo waits in the closet with the door held shut until the lights are back: no agent to pull him out.
            var teoStart = Resident.FindByName("Teodoro Baptiste");
            if (teoStart != null)
            {
                var agent = teoStart.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null) agent.enabled = false;
                teoStart.transform.SetPositionAndRotation(A("TeoCloset").position, A("TeoCloset").rotation);
            }
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
            Nerve.ExtraDistress = 0f;
            SceneLoader.MainMenu();
        }

        // ------------------------------------------------------------ helpers

        private void Hint(string text) => hud.SetHint(text, 0f);

        /// <summary>A card you read at your own pace. Waits for any open conversation first, so it never lands under one.</summary>
        private IEnumerator Card(string text)
        {
            yield return new WaitUntil(() => Dialogue.DialogueUI.Instance == null || Dialogue.DialogueUI.Instance.Current == null);
            yield return new WaitUntil(() => NotebookUI.Instance == null || !NotebookUI.Instance.IsOpen);
            hud.ShowReading(GameInput.Format(text));
            yield return new WaitUntil(() => !hud.IsReading);
        }

        private static bool Asked(string displayName, string key) => SaveSystem.Flags.Contains($"asked|{displayName}|{key}");

        private bool Near(string anchor, float radius) => Vector3.Distance(player.position, A(anchor).position) < radius;

        /// <summary>In the long first aisle of the archive, along the north wall.</summary>
        private bool InFirstAisle() => player.position.y < -2.6f && player.position.z > -3.7f;

        private bool LookingAt(Vector3 point, float minDot = 0.5f)
        {
            var to = point - playerCam.transform.position;
            return Vector3.Dot(playerCam.transform.forward, to.normalized) > minDot;
        }

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
            lightsOut = !on;
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
                if (roomLights.Count > 0) GameAudio.PlayAt("tube_on", roomLights[0].transform.position, 0.7f, UnityEngine.Random.Range(0.95f, 1.05f), 30f);
                yield return new WaitForSeconds(0.45f);
            }
            foreach (var h in hums) if (h != null) h.gameObject.SetActive(true);
            if (furnaceGlow != null) SetBulbs(new[] { furnaceGlow }, true);
            lightsOut = false;
        }

        /// <summary>The lights are out: Reyes gets up, crosses to the dispatch set and sits down by the radio.</summary>
        private IEnumerator ReyesToTheRadio()
        {
            var alma = Resident.FindByName("Alma Reyes");
            if (alma == null) yield break;
            alma.TryGetComponent<ResidentBehaviour>(out var behaviour);
            if (behaviour != null) behaviour.ForceSeated = false;
            yield return new WaitForSeconds(1.2f);
            var agent = alma.GetComponent<UnityEngine.AI.NavMeshAgent>();
            var mover = alma.GetComponent<ResidentMover>();
            var target = A("Dispatch");
            // Stand up, walk to the set (the agent stops short of the chair), then sit on it exactly.
            if (agent != null) { agent.enabled = true; if (UnityEngine.AI.NavMesh.SamplePosition(alma.transform.position, out var onMesh, 1f, UnityEngine.AI.NavMesh.AllAreas)) agent.Warp(onMesh.position); }
            if (mover != null) mover.WalkTo(target.position + Vector3.right * 0.6f);
            float t = 0f;
            while (t < 14f && Vector3.Distance(alma.transform.position, target.position) > 0.9f) { t += Time.deltaTime; yield return null; }
            if (agent != null) agent.enabled = false;
            alma.transform.SetPositionAndRotation(target.position, target.rotation);
            if (behaviour != null) behaviour.ForceSeated = true;
        }

        private IEnumerator TypedLine(string line)
        {
            var canvas = UIFactory.Canvas("Tutorial Card", null, 200); // over the fader's black
            var text = UIFactory.Text("Line", canvas.transform, "", 30, new Color(0.92f, 0.89f, 0.8f), TextAlignmentOptions.Center);
            UIFactory.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 80));
            for (int i = 1; i <= line.Length; i++)
            {
                text.text = line.Substring(0, i);
                if (line[i - 1] != ' ') GameAudio.Play(GameAudio.Variant("typewriter_key", 8), 0.45f, UnityEngine.Random.Range(0.96f, 1.04f));
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

        /// <summary>Distant thunder now and then while you're above ground: never close, never on a cue.</summary>
        private IEnumerator Thunder()
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(14f, 22f));
            while (!finished)
            {
                if (player != null && player.position.y > -1f && !HidingSpot.PlayerHidden)
                    GameAudio.Play(UnityEngine.Random.value < 0.5f ? "thunder_1" : "thunder_2", UnityEngine.Random.Range(0.35f, 0.55f), UnityEngine.Random.Range(0.9f, 1.05f), AudioBus.Ambience);
                yield return new WaitForSeconds(UnityEngine.Random.Range(35f, 70f));
            }
        }

        /// <summary>Pipes knocking, a carton shifting, claws two rows over: the basement talking to itself.</summary>
        private IEnumerator BasementNoises()
        {
            var points = new[] { A("DogEast").position, A("DogWest").position, A("Thud").position, A("ChaseFrom").position };
            while (lightsOut)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(7f, 14f));
                if (!lightsOut) break;
                var at = points[UnityEngine.Random.Range(0, points.Length)] + Vector3.up * UnityEngine.Random.Range(0.3f, 1.8f);
                switch (UnityEngine.Random.Range(0, 3))
                {
                    case 0: GameAudio.PlayAt("knock_slow", at, 0.45f, UnityEngine.Random.Range(0.6f, 0.8f), 30f); break; // a pipe
                    case 1: GameAudio.PlayAt("thud", at, 0.5f, UnityEngine.Random.Range(0.7f, 1f), 30f); break;          // a carton
                    default: GameAudio.PlayAt("claws_loop", at, 0.35f, 1f, 24f); break;                                  // something, two rows over
                }
                Nerve.Instance?.Shock(1.5f);
            }
        }

        /// <summary>
        /// The first encounter in the dark: the flashlight stays dead the whole time she's in the aisle, and the
        /// red lamp over the locker flickers out as she comes level with it, then comes back once she's gone.
        /// </summary>
        private IEnumerator NothingToSeeHer(Func<bool> until)
        {
            var lockerFront = A("LockerFront").position;
            Light lamp = null;
            float best = 3.5f;
            foreach (var l in emergencyLights)
            {
                if (l == null) continue;
                float d = Vector3.Distance(l.transform.position, lockerFront);
                if (d < best) { best = d; lamp = l; }
            }
            var fixtures = new List<Renderer>();
            if (lamp != null)
                foreach (var r in lamp.transform.parent.GetComponentsInChildren<Renderer>())
                    if (r.name == "Emergency Lamp" && Vector3.Distance(r.transform.position, lamp.transform.position) < 1f) fixtures.Add(r);
            bool lampOut = false;
            while (!until())
            {
                Flashlight.Instance?.Kill(0.6f);
                if (!lampOut && lamp != null && dog != null && dog.gameObject.activeSelf && Vector3.Distance(dog.transform.position, lockerFront) < 3.2f)
                {
                    lampOut = true;
                    for (int i = 0; i < 7; i++)
                    {
                        lamp.enabled = !lamp.enabled;
                        SetBulbs(fixtures, lamp.enabled);
                        yield return new WaitForSeconds(UnityEngine.Random.Range(0.05f, 0.16f));
                    }
                    lamp.enabled = false;
                    SetBulbs(fixtures, false);
                    GameAudio.PlayAt("flashlight_click", lamp.transform.position, 0.4f, 0.5f, 12f);
                }
                yield return null;
            }
            if (lamp != null && lightsOut)
            {
                yield return new WaitForSeconds(1.5f);
                for (int i = 0; i < 4; i++)
                {
                    lamp.enabled = !lamp.enabled;
                    SetBulbs(fixtures, lamp.enabled);
                    yield return new WaitForSeconds(UnityEngine.Random.Range(0.05f, 0.14f));
                }
                lamp.enabled = true;
                SetBulbs(fixtures, true);
            }
        }

        /// <summary>The chase, heard: her growl right behind you every few seconds, a stinger when she's close.</summary>
        private IEnumerator OnYourHeels(Func<bool> until)
        {
            float nextGrowl = Time.time + 1.5f, nextSting = Time.time + 4f;
            while (!until())
            {
                if (dog != null && dog.gameObject.activeSelf)
                {
                    float d = Vector3.Distance(dog.transform.position, player.position);
                    if (Time.time > nextGrowl)
                    {
                        nextGrowl = Time.time + UnityEngine.Random.Range(2f, 3.5f);
                        GameAudio.PlayAt("growl", dog.transform.position + Vector3.up * 0.4f, 1f, UnityEngine.Random.Range(0.7f, 0.9f), 30f);
                    }
                    if (d < 3.5f && Time.time > nextSting)
                    {
                        nextSting = Time.time + 5f;
                        GameAudio.Play("stinger_1", 0.7f, 0.9f);
                        Nerve.Instance?.Shock(6f);
                    }
                }
                yield return null;
            }
        }

        /// <summary>Whenever the thing would be in the beam, the batteries give out. You never get a clean look.</summary>
        private IEnumerator KeepItDark(Func<bool> until)
        {
            float nextFlicker = 0f;
            while (!until())
            {
                var torch = Flashlight.Instance;
                if (torch != null && dog != null && dog.gameObject.activeSelf && Time.time > nextFlicker && LookingAt(dog.transform.position, 0.45f)
                    && Vector3.Distance(player.position, dog.transform.position) < 14f)
                {
                    torch.Kill(UnityEngine.Random.Range(1.8f, 3.2f));
                    GameAudio.Play("flashlight_click", 0.5f, 0.8f);
                    nextFlicker = Time.time + 0.6f;
                }
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
            StartCoroutine(Thunder());
            yield return TypedLine("Thursday night. The office. Two days before Semblance.");

            // Outside, in the rain, closing on the one lit window.
            StartCoroutine(Dolly(9.5f));
            StartCoroutine(FadeAudio(rain, 0.6f, 2.5f));
            yield return fader.Fade(0f, 2.4f);
            yield return new WaitForSeconds(5.9f);
            yield return fader.Fade(1f, 1.2f);

            // Inside: at the window, looking out at the same rain. (The whole camera object goes, so its screen
            // output goes with it.)
            cinematic.gameObject.SetActive(false);
            playerCam.enabled = true;
            Place(A("Window").position, A("Window").forward);
            StartCoroutine(FadeAudio(rain, 0.22f, 1.5f));
            yield return new WaitForSeconds(0.4f);
            InputLock.Pop(this);
            controller.InputLocked = false;
            yield return fader.Fade(0f, 2.2f);

            // 1. Walking.
            Hint("11:40 PM. Everyone's gone home but you and the sergeant, and the rain's coming in at the window.\n<size=75%>{move} to walk, {look} to look around, {sprint} to hurry. The letter's on your desk, under the lamp.</size>");
            yield return new WaitUntil(() => Near("Desk", 1.9f));

            // 2. Reading.
            Hint("There's a letter on the blotter. Look at it and press {interact} to read it.");
            yield return new WaitUntil(() => SaveSystem.Flags.Contains("seen:tut_letter"));
            yield return new WaitUntil(() => !hud.IsReading);

            // 3. The notebook. The next objective is already waiting when it closes: no dead air.
            Hint("Everything you read, hear or see is written down. Press {notebook} to open your notebook; the letter's in the Case File. {notebook} again closes it.");
            yield return new WaitUntil(() => NotebookUI.Instance != null && NotebookUI.Instance.IsOpen);
            Hint("Sergeant Reyes is at the front desk by the lobby door: out of your office, left along the corridor. Press {interact} on her and ask about the letter, under <b>About what I've found...</b>");
            yield return new WaitUntil(() => !NotebookUI.Instance.IsOpen);

            // 4. Questioning.
            yield return new WaitUntil(() => Asked("Alma Reyes", "tut_letter"));
            Hint("Now ask about Teodoro (<b>About people...</b>), then choose <b>Look them over</b>: what you see goes in the notebook next to what she said.");
            yield return new WaitUntil(() => Asked("Alma Reyes", "About Teodoro Baptiste") && Asked("Alma Reyes", "observe"));
            Hint("Leave the conversation when you're done with her.");
            yield return Card("What people tell you is testimony. What you see for yourself is observation. In the world, hold {focus} on someone to look closer.\n\nFaces are where the lies are. In Semblance, the parish records show how everyone used to look. And a handshake tells you something too. Cold hands with a reason behind them, a walk in from the fields, an hour in a cellar, mean nothing. Cold hands with no reason are worth a note.");

            // 5. The camera. The flash goes off; so do the lights.
            Hint("Reyes wants the evidence board on film: it's on the back wall, right of the clock. Press {camera} to raise the camera, {shoot} to take the photo.");
            yield return new WaitUntil(() => photoTaken);
            yield return new WaitForSeconds(0.35f);
            GameAudio.Play("power_down", 1f);
            GameAudio.Play("stinger_3", 0.5f, 0.7f);
            SetPower(false);
            if (breaker != null) breaker.Armed = true;
            Nerve.Instance?.Shock(8f);
            Hint("<b>The lights are gone.</b> Press {flashlight} for your flashlight.");
            yield return new WaitForSeconds(1.4f);
            hud.ShowMessage("Reyes, from the dark: \"That's the breaker. Basement, boiler room, far end of the archive. I'm not leaving the phones, Klein.\"", 6f);
            StartCoroutine(ReyesToTheRadio());
            yield return new WaitUntil(() => Flashlight.Instance != null && Flashlight.Instance.IsOn);
            yield return new WaitForSeconds(0.6f);

            // 6. Down. Time to read before anything happens.
            Hint("The stairwell door is on the far wall of the corridor, under the red EXIT sign. Take the stairs down to the archive.\n<size=75%>Your nerve drains in the dark and steadies in light. Keep moving.</size>");
            drone = GameAudio.Loop("drone_deep_loop", null, 0f, bus: AudioBus.Ambience);
            yield return new WaitUntil(() => player.position.y < -2.6f);
            StartCoroutine(FadeAudio(drone, 0.35f, 6f));
            Hint("The archive. The boiler room is on the far side of the stacks; there's a plan of them pinned up by the door. Where an aisle's stacked shut with cartons, go round.");

            // 7. The lobby. A carton falls in the dead end to the south: nothing there but the carton. Then claws,
            //    from the long aisle to the north, which is the way you have to go. In that aisle the batteries
            //    give out, the eyes show at the far end, and the locker's alcove is on your right under the red lamp.
            yield return new WaitUntil(() => Near("ArchiveEntry", 2.2f));
            yield return new WaitForSeconds(2.2f);
            GameAudio.PlayAt("thud", A("Thud").position + Vector3.up * 0.5f, 0.9f, 0.8f, 40f);
            Nerve.Instance?.Shock(3f);
            StartCoroutine(BasementNoises());
            yield return new WaitForSeconds(4f);
            GameAudio.PlayAt("claws_loop", A("DogEast").position + Vector3.up * 0.3f, 0.5f, 1f, 30f);
            Nerve.Instance?.Shock(3f);
            yield return new WaitUntil(() => InFirstAisle() && player.position.x > -6.9f);
            Hint("Something's down here with you. Keep your light moving, and keep moving. There's an alcove on the right under a red lamp: a locker.");
            yield return new WaitUntil(() => InFirstAisle() && player.position.x > -5.9f);
            Flashlight.Instance?.Kill(0.5f);
            GameAudio.Play("flashlight_click", 0.6f, 0.8f);
            yield return new WaitForSeconds(0.9f);
            dog.Appear(A("DogEast").position, Vector3.left);
            dog.WalkTo(A("LockerFront").position, 0.55f);
            GameAudio.Play("sting_soft", 0.7f);
            heart = GameAudio.Loop("heartbeat", null, 0.5f, bus: AudioBus.Ambience);
            Nerve.Instance?.Shock(9f);
            // Until she's gone again you get eye-shine and claws and nothing else: the batteries stay dead, and
            // the alcove's red lamp gives out as she reaches it. Nobody gets a look at her in the first aisle.
            StartCoroutine(NothingToSeeHer(() => !dog.gameObject.activeSelf));
            Hint("<b>The batteries are going, and it's coming down the aisle.</b>\n<size=80%>The alcove under the red lamp, on your right. Press {interact} on the locker to get inside.</size>");
            float wait = 0f;
            while (!dog.Arrived && wait < 22f) { wait += Time.deltaTime; yield return null; }
            dog.Stay();
            bool hidden = HidingSpot.PlayerHidden;
            if (hidden)
            {
                hud.SetHint("Hold still.", 0f);
                yield return new WaitForSeconds(1.4f);
                GameAudio.PlayAt("locker_rattle", A("LockerFront").position + Vector3.up, 0.8f, 1f, 20f);
                Nerve.Instance?.Shock(10f);
                yield return new WaitForSeconds(1.5f);
                GameAudio.PlayAt("growl", A("LockerFront").position + Vector3.up * 0.4f, 0.7f, 0.9f, 20f);
                yield return new WaitForSeconds(2.4f);
            }
            else
            {
                GameAudio.PlayAt("growl", dog.transform.position + Vector3.up * 0.4f, 0.8f, 0.85f, 20f);
                Nerve.Instance?.Shock(16f);
                yield return new WaitForSeconds(2f);
            }
            dog.WalkTo(A("DogWest").position, 1.6f);
            wait = 0f;
            while (!dog.Arrived && wait < 14f) { wait += Time.deltaTime; yield return null; }
            dog.Vanish();
            StartCoroutine(FadeAudio(heart, 0f, 3f));
            if (HidingSpot.PlayerHidden)
            {
                Hint("It's gone, back the way you came. Press {interact} to climb out.");
                yield return new WaitUntil(() => !HidingSpot.PlayerHidden);
            }
            Hint("Keep going: east to the end of this aisle, then right. The red lamps mark the turns, and the EXIT sign is over the boiler room door.");

            // 8. It comes back, behind you, as you turn the top of the far block, and you never get the light on it.
            //    The run: down the long corridor beside the cage, along the end wall, up to the EXIT sign. It stops
            //    at the door, because it can't work a handle.
            yield return new WaitUntil(() => (player.position.x > A("ChaseStart").position.x - 0.6f && player.position.z > A("ChaseStart").position.z - 0.6f) || breakerPulled);
            if (!breakerPulled)
            {
                dog.Appear(A("ChaseFrom").position, Vector3.forward);
                dog.Follow(4.5f);
                GameAudio.Play("stinger_2", 0.9f, 0.85f);
                chase = GameAudio.Loop(GameAudio.NextChase(), null, 0.8f, bus: AudioBus.Cue);
                if (heart == null) heart = GameAudio.Loop("heartbeat", null, 0f, bus: AudioBus.Cue);
                StartCoroutine(FadeAudio(heart, 0.9f, 1.2f));
                StartCoroutine(OnYourHeels(() => breakerPulled));
                Nerve.ExtraDistress = 0.95f;
                Nerve.Instance?.Shock(12f);
                Hint("<b>It's behind you.</b> Don't turn around. {sprint} to run: down the long aisle past the cage, along the end wall, up to the EXIT sign. Reset the breaker.");
                StartCoroutine(KeepItDark(() => breakerPulled));
                while (!breakerPulled)
                {
                    var d = dog.transform.position;
                    if (d.x > A("BoilerDoor").position.x - 0.7f && d.z > A("BoilerDoor").position.z - 0.3f) dog.Stay();
                    yield return null;
                }
            }

            // 9. Lights. It's a dog.
            dog.Stay();
            if (chase != null) Destroy(chase.gameObject);
            if (heart != null) { StartCoroutine(FadeAudio(heart, 0f, 1.5f)); Destroy(heart.gameObject, 1.6f); heart = null; }
            StartCoroutine(FadeAudio(drone, 0f, 4f));
            Nerve.ExtraDistress = 0f;
            Hint(null);
            yield return Restore();
            if (!dog.gameObject.activeSelf) dog.Appear(A("BoilerDoor").position, Vector3.forward);
            // If the detective shut the door on her, she scratches at it and it opens from the other side: a nose first.
            if (boilerDoor != null && !boilerDoor.IsOpen)
            {
                GameAudio.PlayAt("claws_loop", boilerDoor.transform.position + Vector3.up * 0.3f, 0.6f, 1f, 20f);
                yield return new WaitForSeconds(1.3f);
                boilerDoor.ForceOpen(true);
                yield return new WaitForSeconds(0.7f);
            }
            dog.WalkTo(A("DogSit").position);
            wait = 0f;
            while (!dog.Arrived && wait < 7f) { wait += Time.deltaTime; yield return null; }
            dog.Sit();
            Nerve.Instance?.Calm(100f); // the fright's over: no heartbeat left over from the chase
            hud.ShowMessage("It's a dog. An old, half-blind dog with grey on her muzzle, and she is very pleased to see you.", 5f);
            yield return new WaitForSeconds(2.2f);
            var teo = Resident.FindByName("Teodoro Baptiste");
            if (closetDoor != null) { closetDoor.SetLocked(false); closetDoor.ForceOpen(true); closetDoor.FixOpen(); } // and it stays open: Teo's standing in it
            if (teo != null)
            {
                var teoAgent = teo.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (teoAgent != null) { teoAgent.enabled = true; if (teoAgent.isOnNavMesh) teoAgent.Warp(teo.transform.position); }
                teo.DoorstepGreeting = "There she is. Biscuit, you old fool, come here. ...Detective. Sorry about the lights. Furnace trips the breaker when it turns cold, and I was up a ladder with a mop in my hand.";
                var mover = teo.GetComponent<ResidentMover>();
                if (mover != null) mover.WalkTo(A("TeoOut").position);
            }
            yield return new WaitForSeconds(2.5f);
            Hint("Talk to Teodoro ({interact}) and ask what he's doing down here: <b>About yourself</b>.");
            yield return new WaitUntil(() => Asked("Teodoro Baptiste", ResidentProfile.TopicSelf));

            // 10. The contradiction. The hint says what to do; the card says why, once the conversation's over.
            Hint("Reyes said Teo clocked out at ten. Teo says he never left. Open your notebook ({notebook}), tick both notes and <b>link</b> them.");
            yield return Card("Two statements that can't both be true. A link that holds is a mark against somebody.\n\nIn Semblance, one of the people you meet won't be a person. It will make a few mistakes. So will everybody else. The job is telling the difference.");
            yield return new WaitUntil(() => Notebook.Instance != null && Notebook.Instance.Contradictions.Count > 0);
            Hint("Linked. Now back upstairs: put what Teo said to Reyes (<b>About what I've found...</b>).");
            yield return new WaitUntil(() => !NotebookUI.Instance.IsOpen);

            // 11. Upstairs, and the ruling.
            yield return new WaitUntil(() => Asked("Alma Reyes", "tut_swap"));
            Hint("She was wrong for an ordinary reason. In the notebook, on <b>Teodoro's page</b>, press <b>ok?</b> beside what Reyes said and rule it <b>Explained</b>.");
            yield return new WaitUntil(() => AnyExplained());
            Hint("An explained contradiction isn't proof of anything. Close the notebook.");
            yield return new WaitUntil(() => NotebookUI.Instance == null || !NotebookUI.Instance.IsOpen);
            yield return new WaitUntil(() => Dialogue.DialogueUI.Instance == null || Dialogue.DialogueUI.Instance.Current == null);
            Hint(null);
            yield return new WaitForSeconds(0.5f);

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
            // Through the mixer: it sets every loop's volume each frame from its own level, so writing the
            // source's volume directly does nothing.
            if (source == null) yield break;
            float from = GameAudio.GetVolume(source);
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                if (source == null) yield break;
                GameAudio.SetVolume(source, Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            if (source != null) GameAudio.SetVolume(source, to);
        }
    }
}
