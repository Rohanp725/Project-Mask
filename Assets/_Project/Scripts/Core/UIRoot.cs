using System;
using Semblance.Notes;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Semblance.Core
{
    /// <summary>
    /// Global keys: Esc closes the topmost screen (or opens the pause menu), Tab toggles the notebook.
    /// Other systems hook in through the static events.
    /// </summary>
    public class UIRoot : MonoBehaviour
    {
        /// <summary>Called when Esc is pressed with nothing open (the pause menu subscribes).</summary>
        public static event Action PauseRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics() => PauseRequested = null;

        private void LateUpdate()
        {
            if (GameInput.Pressed(GameInput.Cancel))
            {
                if (!InputLock.EscapeTop())
                    PauseRequested?.Invoke();
                return;
            }

            if (GameInput.Pressed(GameInput.Notebook) && NotebookUI.Instance != null)
            {
                if (NotebookUI.Instance.IsOpen)
                    NotebookUI.Instance.Close();
                else if (!InputLock.IsLocked || InputLock.Top is Dialogue.DialogueUI)
                    NotebookUI.Instance.Open(); // the notebook comes out mid-conversation too
            }
        }
    }
}
