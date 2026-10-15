using Semblance.Core;
using Semblance.Events;
using Semblance.Interaction;
using Semblance.Residents;
using UnityEngine;

namespace Semblance.World
{
    /// <summary>A cup of Mae's burnt coffee. Steadies the nerves; costs a quarter hour. Once per part of the day.</summary>
    public class DinerCoffee : Interactable
    {
        private string lastServed;

        public override string Prompt => "Ask for a cup of coffee";

        public override void Interact(PlayerInteractor interactor)
        {
            var server = Resident.FindByName(Case.CastNames.Mae);
            var ada = Resident.FindByName(Case.CastNames.Ada);
            if (server == null || !server.isActiveAndEnabled || Vector3.Distance(server.transform.position, transform.position) > 6f)
                server = ada != null && ada.isActiveAndEnabled && Vector3.Distance(ada.transform.position, transform.position) < 6f ? ada : null;
            if (server == null)
            {
                interactor.Hud.ShowMessage("The pot's cold and there's nobody behind the counter.", 3.5f);
                return;
            }
            var clock = GameClock.Instance;
            string key = clock != null ? $"coffee:{clock.Day}|{clock.Phase}" : "";
            if (key == lastServed || Save.SaveSystem.Flags.Contains(key))
            {
                interactor.Hud.ShowMessage($"\"Another? Hon, you're shaking already.\" {server.FirstName} pushes a glass of water at you instead.", 4f);
                return;
            }
            lastServed = key;
            Save.SaveSystem.Flags.Add(key);
            Audio.GameAudio.Play("coffee_pour", 0.6f);
            Nerve.Instance?.Calm(35f);
            clock?.Advance(0.25f);
            interactor.Hud.ShowMessage($"{server.FirstName} pours you a cup without asking. It's burnt and bitter and the best thing that's happened all day.", 4.5f);
        }
    }
}
