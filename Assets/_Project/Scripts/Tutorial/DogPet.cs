using Semblance.Interaction;
using UnityEngine;

namespace Semblance.Tutorial
{
    /// <summary>Biscuit, once the lights are back: look at her and press E to make a fuss of her.</summary>
    [RequireComponent(typeof(TutorialDog))]
    public class DogPet : Interactable
    {
        private static readonly string[] Lines =
        {
            "You crouch and scratch behind her ears. She leans her whole weight against your shin.",
            "Her tail goes so hard her back end goes with it.",
            "She licks your wrist, then sneezes, offended by something on it.",
            "You rub the grey on her muzzle. She shuts her eyes.",
            "She rolls half onto her side and waits, in case there's more.",
        };

        private TutorialDog dog;
        private int next;
        private float readyAt;

        private void Awake() => dog = GetComponent<TutorialDog>();

        public override string Prompt => "Pet Biscuit";
        public override bool CanInteract => base.CanInteract && dog != null && dog.Happy && Time.time >= readyAt;

        public override void Interact(PlayerInteractor interactor)
        {
            readyAt = Time.time + 1.2f;
            dog.Petted(interactor.transform.position);
            interactor.Hud.ShowMessage(Lines[next], 3.5f);
            next = (next + 1) % Lines.Length;
            Events.Nerve.Instance?.Calm(8f);
        }
    }
}
