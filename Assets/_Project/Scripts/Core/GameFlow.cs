using System;
using System.Collections;
using Semblance.Case;
using Semblance.Notes;
using Semblance.Player;
using Semblance.Save;
using UnityEngine;

namespace Semblance.Core
{
    /// <summary>
    /// The shape of a playthrough: arriving, sleeping each night (autosave), collapsing if you stay up past 3 AM,
    /// running out of days, and resuming from a save.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance { get; private set; }

        [SerializeField] private Transform wakeSpot;
        [SerializeField] private Transform player;

        public bool IsSleeping { get; private set; }
        public Transform WakeSpot => wakeSpot;

        /// <summary>A new case is starting (no save loaded). The intro subscribes to this.</summary>
        public static event Action NewGameStarted;
        /// <summary>A saved case was resumed.</summary>
        public static event Action GameResumed;
        /// <summary>The detective has woken up on a new day.</summary>
        public static event Action<int> Woke;
        /// <summary>Night events can run something while the detective sleeps (yield-able).</summary>
        public static Func<IEnumerator> SleepInterruption;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics()
        {
            NewGameStarted = null;
            GameResumed = null;
            Woke = null;
            SleepInterruption = null;
        }

        public void Configure(Transform wake, Transform playerTransform)
        {
            wakeSpot = wake;
            player = playerTransform;
        }

        private void Awake() => Instance = this;

        private void Start()
        {
            var clock = GameClock.Instance;
            clock.LastDay = GameSettings.DayCount;
            clock.Collapsed += Collapse;

            SaveSystem.Register(d => { d.day = clock.Day; d.hour = clock.Hour; d.difficulty = (int)GameSettings.Difficulty; },
                                d => clock.SetTime(d.day, d.hour));
            SaveSystem.Register(d => d.notebook = Notebook.Instance != null ? Notebook.Instance.GetState() : null,
                                d => { if (d.notebook != null) Notebook.Instance?.LoadState(d.notebook); });
            SaveSystem.Register(d => d.film = PhotoCamera.Instance != null ? PhotoCamera.Instance.Film : 0,
                                d => PhotoCamera.Instance?.SetFilm(d.film));

            StartCoroutine(Begin());
        }

        private IEnumerator Begin()
        {
            // Let every system register its save hooks first.
            yield return null;
            if (SceneLoader.ContinueRequested)
            {
                SceneLoader.ConsumeContinue();
                ScreenFader.Instance?.SetBlack(1f);
                if (SaveSystem.Load())
                {
                    EndingDirector.MissingPersonFound = SaveSystem.Flags.Contains("found:missing");
                    PlacePlayerAtWakeSpot();
                    GameResumed?.Invoke();
                    var clock = GameClock.Instance;
                    if (ScreenFader.Instance != null)
                    {
                        yield return ScreenFader.Instance.TitleCard($"DAY {clock.Day}", DateLine(clock.Day));
                        yield return ScreenFader.Instance.Fade(0f, 1.5f);
                    }
                    yield break;
                }
                ScreenFader.Instance?.SetBlack(0f);
                if (!string.IsNullOrEmpty(SaveSystem.LastLoadProblem))
                {
                    UI.PlayerHUD.Instance?.ShowMessage(SaveSystem.LastLoadProblem, 6f);
                    SaveSystem.LastLoadProblem = null;
                }
            }
            NewGameStarted?.Invoke();
        }

        public static string DateLine(int day)
        {
            var date = new DateTime(1983, 10, 14).AddDays(day); // Day 1 = Saturday, Day 2 = Sunday service
            return date.ToString("dddd, MMMM d, yyyy");
        }

        /// <summary>Black out (fear or exhaustion): lose the rest of the night and wake late.</summary>
        public void Collapse()
        {
            if (IsSleeping) return;
            // It found what you found. You don't get to pass out and wake up safe; you get to the bell.
            if (Events.Stalker.IsForced) return;
            if (Events.Stalker.Active != null && Events.Stalker.Active.IsKilling) return;
            // Whatever was hunting you doesn't follow you into the morning.
            if (Events.Stalker.Active != null) Events.Stalker.Active.End();
            StartCoroutine(SleepRoutine(collapsed: true));
        }

        public void Sleep()
        {
            if (!IsSleeping)
                StartCoroutine(SleepRoutine(collapsed: false));
        }

        private IEnumerator SleepRoutine(bool collapsed)
        {
            IsSleeping = true;
            SaveSystem.Flags.Add("slept");
            if (Dialogue.DialogueUI.Instance != null && Dialogue.DialogueUI.Instance.Current != null)
                Dialogue.DialogueUI.Instance.Close();
            World.DetectiveCar.Instance?.ForceExit();
            InputLock.Push(this, null);
            var clock = GameClock.Instance;
            clock.Frozen = true;
            var fader = ScreenFader.Instance;
            if (collapsed)
                UI.PlayerHUD.Instance?.ShowMessage("Your legs give out. The last thing you see is the dark coming up to meet you.", 3f);
            if (fader != null)
                yield return fader.Fade(1f, collapsed ? 2.5f : 1.5f);

            if (!collapsed && SleepInterruption != null)
                yield return SleepInterruption();

            int next = clock.Day + 1;
            if (next > clock.LastDay)
            {
                InputLock.Pop(this);
                IsSleeping = false;
                EndingDirector.Instance?.Play(EndingKind.OutOfTime);
                yield break;
            }

            clock.SetTime(next, collapsed ? 10f : 6.5f);
            PlacePlayerAtWakeSpot();

            if (fader != null)
            {
                string sub = DateLine(next) + (collapsed ? "\n<i>You woke on the motel floor. You don't remember walking back.</i>" : "");
                int left = clock.LastDay - next;
                sub += left == 0 ? "\n<color=#c03020>This is the last day.</color>" : $"\n{left + 1} days left.";
                yield return fader.TitleCard($"DAY {next}", sub, 2.6f);
                yield return fader.Fade(0f, 1.5f);
            }
            clock.Frozen = false;
            InputLock.Pop(this);
            IsSleeping = false;
            Woke?.Invoke(next);
            // Saved after the morning has settled (nerve restored, beats placed), so a Continue matches what you saw.
            SaveSystem.Save();
        }

        public void PlacePlayerAtWakeSpot()
        {
            if (player == null || wakeSpot == null) return;
            var cc = player.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            player.SetPositionAndRotation(wakeSpot.position, wakeSpot.rotation);
            if (cc) cc.enabled = true;
            Physics.SyncTransforms();
        }
    }
}
