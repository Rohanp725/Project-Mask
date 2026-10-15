using Semblance.Case;
using Semblance.Events;
using Semblance.Interaction;
using Semblance.Notes;
using Semblance.Save;
using UnityEngine;

namespace Semblance.Investigation
{
    /// <summary>
    /// Something worth examining. The first look files it in the notebook (under a person or the Case File)
    /// and counts toward any side mystery it belongs to.
    /// </summary>
    public class EvidenceItem : Interactable
    {
        [SerializeField] private string id;
        [SerializeField] private string title = "Evidence";
        [SerializeField, TextArea(2, 6)] private string text;
        [SerializeField] private string subject;
        [SerializeField] private string customPrompt;
        [SerializeField] private float nerveShock;
        [SerializeField] private float awareness;

        public string Id => id;
        public bool Found => SaveSystem.Flags.Contains("evidence:" + id);
        public override string Prompt => string.IsNullOrEmpty(customPrompt) ? "Examine " + title.ToLowerInvariant() : customPrompt;

        public void AppendText(string more) => text += more;
        public void ReplaceText(string token, string value) => text = text.Replace(token, value);

        public void Configure(string evidenceId, string evidenceTitle, string description, string subjectName = null,
            string customPrompt = null, float shock = 0f, float awarenessRaise = 0f)
        {
            id = evidenceId;
            title = evidenceTitle;
            text = description;
            subject = subjectName;
            this.customPrompt = customPrompt;
            nerveShock = shock;
            awareness = awarenessRaise;
        }

        public override void Interact(PlayerInteractor interactor)
        {
            interactor.Hud.ShowMessage($"<b>{title}</b>\n{text}", 7f);
            if (Found) return;
            SaveSystem.Flags.Add("evidence:" + id);
            Notebook.Instance?.Record(NoteKind.Evidence, string.IsNullOrEmpty(subject) ? Notebook.CaseSubject : subject, title, text, null, "evidence|" + id);
            if (nerveShock > 0) Nerve.Instance?.Shock(nerveShock);
            if (awareness > 0) MimicAwareness.Instance?.Raise(awareness, "evidence");
            if (nerveShock >= 10) Audio.GameAudio.Play("sting_soft", 0.5f);
            SideMysteries.OnClueFound(id);
            if (id == "bell_note" || id == "wendell_tally")
            {
                EvidencePlacer.UnlockDobbs();
                interactor.Hud.Toast("Whatever Wendell barred his door against, you could go and ask him now.");
            }
        }
    }
}
