using Semblance.Interaction;

namespace Semblance.Case
{
    /// <summary>The church bell rope. Ringing it gathers the town for an accusation.</summary>
    public class BellRope : Interactable
    {
        public override string Prompt => "Ring the bell (make your accusation)";

        public override void Interact(PlayerInteractor interactor)
        {
            Audio.GameAudio.Play("bell_toll", 0.8f);
            // The bell brings the whole town running. Whatever was hunting you can't be seen like that:
            // it slips away and walks back in with everyone else, wearing its face (so it can be named).
            if (Events.Stalker.Active != null)
            {
                Events.Stalker.Active.End();
                interactor.Hud.ShowMessage("Something crashes away through the corn as the bell goes. Lanterns are coming up the road.", 4f);
            }
            AccusationUI.Instance?.Open();
        }
    }
}
