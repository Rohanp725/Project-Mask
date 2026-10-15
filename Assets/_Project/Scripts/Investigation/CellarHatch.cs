using System.Collections;
using Semblance.Core;
using Semblance.Interaction;
using UnityEngine;

namespace Semblance.Investigation
{
    /// <summary>A trapdoor (or ladder) that takes the detective between the surface and a cellar or crypt below.</summary>
    public class CellarHatch : Interactable
    {
        [SerializeField] private Transform destination;
        [SerializeField] private string hatchPrompt = "Open the cellar hatch";
        [SerializeField] private bool goingDown = true;

        [SerializeField] private GameObject padlockVisual;
        [SerializeField] private GameObject openPadlockVisual;
        private bool padlocked;
        public bool GoesDown => goingDown;
        public Transform Destination => destination;

        public override string Prompt => padlocked ? "Try the hatch" : hatchPrompt;

        /// <summary>A new brass padlock: some cellars are shut until late in the case.</summary>
        public void SetPadlock(bool on)
        {
            // Taken off: the lock is left lying open in the dirt beside the hatch.
            if (!on && padlocked) ShowOpenPadlock();
            padlocked = on;
            if (padlockVisual != null) padlockVisual.SetActive(on);
        }

        /// <summary>This hatch was padlocked and somebody has taken the lock off.</summary>
        public void ShowOpenPadlock()
        {
            if (padlockVisual != null) padlockVisual.SetActive(false);
            if (openPadlockVisual != null) openPadlockVisual.SetActive(true);
        }

        public void Configure(Transform target, string promptText, bool down)
        {
            destination = target;
            hatchPrompt = promptText;
            goingDown = down;
        }

        public override void Interact(PlayerInteractor interactor)
        {
            if (padlocked)
            {
                interactor.Hud.ShowMessage("A brand-new brass padlock on an old hatch. The hasp is bright. Somebody wants this shut.", 4f);
                return;
            }
            if (destination != null)
                StartCoroutine(Travel(interactor.transform));
        }

        private IEnumerator Travel(Transform player)
        {
            InputLock.Push(this, null);
            Audio.GameAudio.Play(goingDown ? "hatch_open" : "door_creak_1", 0.7f);
            var fader = ScreenFader.Instance;
            if (fader != null) yield return fader.Fade(1f, 0.6f);
            var cc = player.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            player.SetPositionAndRotation(destination.position, destination.rotation);
            if (cc) cc.enabled = true;
            Physics.SyncTransforms();
            if (goingDown) Events.Nerve.Instance?.Shock(4f);
            yield return new WaitForSeconds(0.3f);
            if (fader != null) yield return fader.Fade(0f, 0.6f);
            InputLock.Pop(this);
        }
    }
}
