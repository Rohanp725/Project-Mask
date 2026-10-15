using Semblance.Core;
using Semblance.Interaction;

namespace Semblance.World
{
    /// <summary>The bed in Room 6. Sleeping ends the day and saves the game (only from evening on).</summary>
    public class MotelBed : Interactable
    {
        private const float EarliestSleep = 19.5f;

        public override string Prompt
        {
            get
            {
                var clock = GameClock.Instance;
                bool early = clock != null && clock.Day == 1 && clock.Hour < 21.5f && clock.Hour >= 6f;
                return clock != null && !early && (clock.Hour >= EarliestSleep || clock.Hour < 6f) ? "Sleep until morning (saves)" : "Lie down";
            }
        }

        public override void Interact(PlayerInteractor interactor)
        {
            var clock0 = GameClock.Instance;
            if (clock0 != null && clock0.Day == 1 && clock0.Hour < 21.5f)
            {
                interactor.Hud.ShowMessage("The town's still awake. There's work to do before you sleep.", 3.5f);
                return;
            }
            if (Events.Stalker.IsHunting)
            {
                interactor.Hud.ShowMessage("Not with that thing outside. You'd never wake up.", 3f);
                return;
            }
            var clock = GameClock.Instance;
            if (clock != null && clock.Hour < EarliestSleep && clock.Hour >= 6f)
            {
                interactor.Hud.ShowMessage($"You're not tired. It's only {GameClock.FormatHour(clock.Hour)}, and there's still daylight to work with.", 4f);
                return;
            }
            GameFlow.Instance?.Sleep();
        }
    }
}
