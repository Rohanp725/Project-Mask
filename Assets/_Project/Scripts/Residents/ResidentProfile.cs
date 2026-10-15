using System.Collections.Generic;
using UnityEngine;

namespace Semblance.Residents
{
    /// <summary>
    /// Everything canonical about one resident: who they are, how they look, their oddities and what they say.
    /// The case generator layers per-run changes (tells, red herrings) on top of this at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Semblance/Resident Profile", fileName = "Resident")]
    public class ResidentProfile : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Resident";
        public string occupation;
        public int age = 40;
        [Tooltip("Possessive pronoun used in records and notes: his / her / their.")]
        public string possessive = "their";
        public string home;
        [TextArea(2, 6)] public string bio;

        [Header("Looks")]
        public ResidentAppearance appearance = new ResidentAppearance();
        public List<FeatureInstance> features = new List<FeatureInstance>();

        [Header("Routine")]
        [Tooltip("Where they are in the Morning, Afternoon, Dusk and Night (ScheduleSpot ids).")]
        public string[] schedule = new string[4];

        [Header("Dialogue")]
        public string[] greetings = { "Evening." };
        [TextArea(2, 5)] public string aboutSelf;
        [TextArea(2, 5)] public string aboutTown;
        [TextArea(2, 5)] public string seenAnythingStrange;
        [Tooltip("Their account of Founders' Day. Everyone in town remembers it the same way: rain, Mae's cherry pie, Crane's sermon.")]
        [TextArea(2, 5)] public string foundersDay;
        [Tooltip("Gossip about other residents. This is how the player learns what's 'normal' for each person.")]
        public List<DialogueTopic> gossip = new List<DialogueTopic>();

        public const string TopicSelf = "About yourself";
        public const string TopicTown = "About Semblance";
        public const string TopicStrange = "Seen anything strange?";
        public const string TopicFoundersDay = "Founders' Day";

        public IEnumerable<DialogueTopic> AllTopics()
        {
            yield return new DialogueTopic(TopicSelf, aboutSelf);
            yield return new DialogueTopic(TopicTown, aboutTown);
            yield return new DialogueTopic(TopicStrange, seenAnythingStrange);
            yield return new DialogueTopic(TopicFoundersDay, foundersDay);
            foreach (var topic in gossip)
                yield return topic;
        }
    }
}
