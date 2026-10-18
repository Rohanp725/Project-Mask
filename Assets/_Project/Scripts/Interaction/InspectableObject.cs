using UnityEngine;

namespace Semblance.Interaction
{
    /// <summary>Shows a short description on the HUD when inspected.</summary>
    public class InspectableObject : Interactable
    {
        [SerializeField, TextArea(2, 6)] private string description = "Nothing out of the ordinary.";
        [SerializeField] private float messageDuration = 5f;
        [Tooltip("Reading this unlocks questions about it in conversation (ClueTopics).")]
        [SerializeField] private string clueKey;

        public void SetDescription(string text) => description = text;
        public void SetClue(string key) => clueKey = key;

        public override void Interact(PlayerInteractor interactor)
        {
            // A small handling sound: paper, cloth, a lid. Enough that looking at something feels like touching it.
            Audio.GameAudio.Play("page_flip", 0.22f, Random.Range(0.8f, 1.05f), Audio.AudioBus.World);
            interactor.Hud.ShowReading(description);
            if (!string.IsNullOrEmpty(clueKey) && Save.SaveSystem.Flags.Add("seen:" + clueKey))
                UI.PlayerHUD.Instance?.Toast("Something to ask about.");
        }
    }
}
