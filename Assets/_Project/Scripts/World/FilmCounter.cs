using Semblance.Core;
using Semblance.Interaction;
using Semblance.Player;
using Semblance.Residents;
using UnityEngine;

namespace Semblance.World
{
    /// <summary>The general store counter: buy a pack of film from Otis, once per day, if he's behind the counter.</summary>
    public class FilmCounter : Interactable
    {
        [SerializeField] private int packSize = 8;
        [SerializeField] private string clerkName = "Otis Pruitt";

        private int lastDayBought = -1;

        public override string Prompt => "Buy film and batteries";

        public override void Interact(PlayerInteractor interactor)
        {
            var clerk = Resident.FindByName(clerkName);
            if (clerk == null || Vector3.Distance(clerk.transform.position, transform.position) > 5f)
            {
                interactor.Hud.ShowMessage("Nobody's behind the counter. The register is locked.", 4f);
                return;
            }
            int day = GameClock.Instance != null ? GameClock.Instance.Day : 0;
            if (day == lastDayBought || Save.SaveSystem.Flags.Contains("film:" + day))
            {
                interactor.Hud.ShowMessage("\"That's all the film I got till the truck comes tomorrow.\"", 4f);
                return;
            }
            lastDayBought = day;
            Save.SaveSystem.Flags.Add("film:" + day);
            PhotoCamera.Instance?.AddFilm(packSize);
            Flashlight.Instance?.AddBattery(100f);
            interactor.Hud.ShowMessage($"\"Th-that's ${packSize * 0.75f + 1.5f:0.00}. Don't go pointing it at folks without asking.\"\n<i>+{packSize} film, fresh flashlight batteries.</i>", 5f);
            Audio.GameAudio.Play("register", 0.7f);
        }
    }
}
