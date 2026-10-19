using Semblance.Interaction;
using UnityEngine;

namespace Semblance.Case
{
    /// <summary>
    /// The church bell rope. Ringing it gathers the town for an accusation. The first pull asks you to be sure;
    /// the second rings it. Coming near it the first time says what it's for.
    /// </summary>
    public class BellRope : Interactable
    {
        public static BellRope Instance { get; private set; }

        private float armedUntil = -1f;
        private bool explained;
        private Transform player;

        public override string Prompt => Time.time < armedUntil ? "Ring it (you'll have to name someone)" : "Ring the bell: call the town to hear your accusation";

        private void Awake() => Instance = this;

        private void Update()
        {
            if (explained) return;
            if (player == null) { var cam = Camera.main; if (cam == null) return; player = cam.transform.root; }
            if (Vector3.Distance(player.position, transform.position) > 5f) return;
            explained = true;
            Save.SaveSystem.Flags.Add("seen:bellrope");
            UI.PlayerHUD.Instance?.ShowReading("The bell rope hangs just inside the door. Ring it and every door in Semblance opens: the whole town comes up the road to the church, and you stand in front of them and name the one that isn't one of them.\n\nYou get one accusation. Ring it when you're sure, or when it's coming for you and you've no choice.");
            Notes.Notebook.Instance?.Record(Notes.NoteKind.Event, Notes.Notebook.CaseSubject, "The bell",
                "The church bell calls the town. When I ring it, I name the one that isn't one of them. One chance.", null, "bell_explained");
        }

        public override void Interact(PlayerInteractor interactor)
        {
            if (Time.time >= armedUntil)
            {
                armedUntil = Time.time + 10f;
                interactor.Hud.ShowMessage("Your hand's on the rope. Ring it and the whole town comes, and you'll have to name someone. Pull again if you're sure.", 5f);
                return;
            }
            armedUntil = -1f;
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

        /// <summary>"north-west, 140 m": which way the bell is from here.</summary>
        public static string DirectionHint(Vector3 from)
        {
            if (Instance == null) return "The church is on the town square, across from the diner.";
            var to = Instance.transform.position - from;
            float dist = new Vector2(to.x, to.z).magnitude;
            float angle = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg; // 0 = north (+z), 90 = east
            string[] names = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
            int i = Mathf.RoundToInt(((angle + 360f) % 360f) / 45f) % 8;
            return $"The church bell: {names[i]}, {Mathf.RoundToInt(dist)} m.";
        }
    }
}
