using Semblance.Core;
using Semblance.Interaction;
using UnityEngine;

namespace Semblance.World
{
    /// <summary>
    /// The welcome sign's population. 212 souls; then something painted 213, counting itself; from the third day it
    /// paints 214. Nobody new has come to Semblance except you.
    /// </summary>
    public class SignPopulation : MonoBehaviour
    {
        [SerializeField] private WorldText oldNumber;
        [SerializeField] private WorldText newNumber;
        [SerializeField] private InspectableObject board;

        public void Configure(WorldText faded, WorldText fresh, InspectableObject inspect)
        {
            oldNumber = faded;
            newNumber = fresh;
            board = inspect;
        }

        private void Start()
        {
            if (GameClock.Instance != null)
            {
                GameClock.Instance.DayStarted += Apply;
                Apply(GameClock.Instance.Day);
            }
        }

        private void Apply(int day)
        {
            int population = day >= 2 ? 214 : 213;
            newNumber.Set(population.ToString());
            oldNumber.Set((population - 1).ToString());
            if (board == null || population <= 213) return;
            string text = "Painted over again: 213 crossed out, 214 written over it in the same red. The paint is still tacky. You count back through every face you've met here, and the only new one in Semblance is yours.";
            var taken = Case.StoryBeats.Instance != null ? Case.StoryBeats.Instance.Missing : null;
            if (day >= 3 && taken != null)
                text += $" Nobody has taken {taken.FirstName} off. Whatever paints this sign doesn't count the dead. It counts what it has.";
            board.SetDescription(text);
        }
    }
}
