using Semblance.Case;
using Semblance.Events;
using Semblance.Interaction;
using Semblance.Notes;
using Semblance.Residents;
using Semblance.Save;
using UnityEngine;

namespace Semblance.Investigation
{
    /// <summary>
    /// The real resident, kept alive in the dark so the mimic can keep studying the face it's wearing.
    /// Finding them is proof, and unlocks the best ending if you then accuse correctly.
    /// </summary>
    public class MissingPerson : Interactable
    {
        private Resident original;
        private int spoken;

        private static readonly string[] Lines =
        {
            "\"...is it day or night? It turns the bulb off when it leaves. It likes me to wait in the dark.\"",
            "\"It sits there where you're standing and it looks at me. For hours. It copies how I hold my mouth.\"",
            "\"It wears me. It practised my face on me.\"",
            "\"Don't let it know you found me. Please. Ring the bell. Say my name, but say it about <i>it</i>.\"",
        };

        public override string Prompt => original != null ? $"Kneel by {original.FirstName}" : "Kneel";

        public void Configure(Resident realResident) => original = realResident;

        public override void Interact(PlayerInteractor interactor)
        {
            if (original == null) return;
            bool first = !EndingDirector.MissingPersonFound;
            interactor.Hud.ShowReading($"<b>{original.DisplayName}</b>, the real one, thin and grey and blinking at the light.\n{Lines[Mathf.Min(spoken, Lines.Length - 1)]}");
            spoken++;
            if (!first) return;
            EndingDirector.MissingPersonFound = true;
            SaveSystem.Flags.Add("found:missing");
            Nerve.Instance?.Shock(20f);
            MimicAwareness.Instance?.VictimFound();
            SaveSystem.Flags.Add("hunt:forced");
            Audio.GameAudio.Play("stinger_3", 0.7f);
            Notebook.Instance?.Record(NoteKind.Evidence, original.DisplayName, "The real " + original.FirstName,
                $"I found {original.DisplayName} alive in the dark under {CaseDirector.Instance.HidingPlace.Replace("Cellar:", "the ").Replace("Crypt", "the church")}{(CaseDirector.Instance.HidingPlace.StartsWith("Cellar:") ? " farmhouse" : "")}, starved and terrified. The {original.FirstName} walking around town is <b>not</b> {original.FirstName}.",
                null, "evidence|missing_person");
            SaveSystem.Save();
        }
    }
}
