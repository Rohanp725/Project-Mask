using Semblance.Interaction;

namespace Semblance.Archive
{
    /// <summary>The filing cabinet in the church. Opens the parish records.</summary>
    public class ArchiveCabinet : Interactable
    {
        public override string Prompt => "Read the parish records";

        public override void Interact(PlayerInteractor interactor)
        {
            if (ArchiveUI.Instance != null)
                ArchiveUI.Instance.Open();
        }
    }
}
