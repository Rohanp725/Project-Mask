using UnityEngine;

namespace Semblance.Core
{
    /// <summary>
    /// Runs before everything else when the town loads: clears static state left over from a previous visit
    /// (menus, events, flags), so starting a new case or going back to the menu always starts clean.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class SceneBoot : MonoBehaviour
    {
        private void Awake()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            InputLock.ResetStatics();
            UIRoot.ResetStatics();
            GameFlow.ResetStatics();
            Save.SaveSystem.ResetStatics();
            Dialogue.DialogueUI.ResetStatics();
            Player.PhotoCamera.ResetStatics();
            Notes.SightingLog.ResetStatics();
            Notes.NotebookUI.CaseFileSections.Clear();
            Archive.ArchiveUI.ResetStatics();
            Case.EndingDirector.ResetStatics();
            Events.Stalker.ResetStatics();
            Events.HidingSpot.ResetStatics();
            Events.EventDirector.ForcedHuntPending = false;
            Audio.AmbientAudio.Hush = 0f;
            Rendering.DayNightCycle.DarknessBoost = 0f;
            Rendering.DayNightCycle.HuntDarkness = 0f;
            Rendering.LampSwitch.ForceOn = false;
            GameClock.RateScale = 1f;
            GameClock.ShowDay = true;
            Notes.NotebookUI.CaseIntroOverride = null;
            Rendering.PS1ScreenEffect.Distress = 0f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Audio.GameAudio.WarmClips();
        }
    }
}
