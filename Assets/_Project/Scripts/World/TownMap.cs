using Semblance.Core;
using Semblance.Interaction;
using UnityEngine;

namespace Semblance.World
{
    /// <summary>
    /// A stack of free county maps by the diner register. Taking one fills in the notebook's Map page: every farm,
    /// who lives there, and where you're standing.
    /// </summary>
    public class TownMap : Interactable
    {
        public const string Flag = "map:found";

        public static bool Found => Save.SaveSystem.Flags.Contains(Flag);

        public override string Prompt => "Take a county map";

        private void Start()
        {
            GameFlow.GameResumed += Refresh;
            Refresh();
        }

        private void OnDestroy() => GameFlow.GameResumed -= Refresh;

        private void Refresh()
        {
            if (this != null && Found) gameObject.SetActive(false);
        }

        public override void Interact(PlayerInteractor interactor)
        {
            Save.SaveSystem.Flags.Add(Flag);
            Audio.GameAudio.Play("page_flip", 0.6f);
            interactor.Hud.ShowReading("A county map, printed for the Founders' Day visitors who never came. Every farm is marked with the family's name in Mae's handwriting.");
            UI.PlayerHUD.Instance?.Toast(GameInput.Format("Map added to your notebook. <b>{notebook}</b>, then <b>Map</b>."));
            gameObject.SetActive(false);
        }
    }
}
