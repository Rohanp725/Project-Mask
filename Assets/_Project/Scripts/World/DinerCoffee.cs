using Semblance.Core;
using Semblance.Events;
using Semblance.Interaction;
using Semblance.Residents;
using UnityEngine;

namespace Semblance.World
{
    /// <summary>
    /// A cup of Mae's burnt coffee. Buys an hour of the night (you drop at 4 AM instead of 3, two cups at most) and
    /// costs a quarter hour to drink. But coffee at the wrong end of the day makes a man jumpy: nerve drains faster
    /// for the rest of the night. Once per part of the day.
    /// </summary>
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
            clock?.Advance(0.25f);
            bool evening = clock != null && clock.Hour >= 17f;
            if (evening)
            {
                clock.ExtendNight(1f);
                if (Nerve.Instance != null) { Nerve.Instance.CoffeeDrain += 0.06f; Nerve.Instance.Shock(6f); }
                interactor.Hud.ShowMessage($"{server.FirstName} pours you a cup without asking. Burnt and bitter. You'll be awake an hour longer than you should be tonight, and every creak will find you.", 5f);
            }
            else
            {
                Nerve.Instance?.Calm(25f);
                interactor.Hud.ShowMessage($"{server.FirstName} pours you a cup without asking. It's burnt and bitter and the best thing that's happened all day.", 4.5f);
            }
        }
    }
}
