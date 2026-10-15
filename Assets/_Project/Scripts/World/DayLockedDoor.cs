using Semblance.Core;
using Semblance.UI;
using UnityEngine;

namespace Semblance.World
{
    /// <summary>
    /// A door that stays locked until a day counted back from the end of the case (Room 3 opens the morning
    /// someone goes missing, whatever the difficulty).
    /// </summary>
    [RequireComponent(typeof(Door))]
    public class DayLockedDoor : MonoBehaviour
    {
        [SerializeField] private int daysBeforeEnd = 2;
        [SerializeField, TextArea] private string unlockToast;

        private void Start()
        {
            var clock = GameClock.Instance;
            if (clock == null) return;
            clock.DayStarted += Check;
            Check(clock.Day);
        }

        private void OnDestroy()
        {
            if (GameClock.Instance != null) GameClock.Instance.DayStarted -= Check;
        }

        private void Check(int day)
        {
            var clock = GameClock.Instance;
            if (clock == null || day < Mathf.Max(2, clock.LastDay - daysBeforeEnd)) return;
            var door = GetComponent<Door>();
            if (door == null) return;
            door.SetLocked(false);
            if (!string.IsNullOrEmpty(unlockToast) && !Save.SaveSystem.Loading)
                PlayerHUD.Instance?.Toast(unlockToast);
            enabled = false;
        }
    }
}
