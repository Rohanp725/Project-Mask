using System;
using Semblance.Interaction;
using UnityEngine;

namespace Semblance.Tutorial
{
    /// <summary>The breaker panel in the boiler room. Nothing to do until the power goes.</summary>
    public class TutorialBreaker : Interactable
    {
        public bool Armed { get; set; }
        public event Action Pulled;

        public override string Prompt => Armed ? "Reset the breaker" : "Look at the panel";

        public override void Interact(PlayerInteractor interactor)
        {
            if (!Armed)
            {
                interactor.Hud.ShowReading("The breaker panel. Every switch where it should be. Somebody has written FURNACE on the big one in grease pencil, and underneath, smaller: DON'T.");
                return;
            }
            Armed = false;
            Audio.GameAudio.PlayAt("breaker_reset", transform.position, 0.9f, 1f, 30f);
            Pulled?.Invoke();
        }
    }
}
