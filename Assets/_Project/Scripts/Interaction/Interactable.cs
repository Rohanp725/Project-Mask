using UnityEngine;

namespace Semblance.Interaction
{
    /// <summary>
    /// Base class for anything the player can look at and press E on (signs, doors, residents, evidence).
    /// Needs a collider on this object or a child.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        [SerializeField] private string prompt = "Inspect";

        public virtual string Prompt => prompt;
        public virtual bool CanInteract => isActiveAndEnabled;

        public abstract void Interact(PlayerInteractor interactor);
    }
}
