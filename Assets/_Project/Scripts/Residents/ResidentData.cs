using System;
using System.Collections.Generic;
using UnityEngine;

namespace Semblance.Residents
{
    public enum FeatureSide { None, Left, Right }

    /// <summary>What role a detail plays in the mystery.</summary>
    public enum FeatureKind
    {
        /// <summary>A normal Semblance oddity: consistent, explained, part of the noise.</summary>
        Trait,
        /// <summary>Looks suspicious but has an innocent explanation.</summary>
        RedHerring,
        /// <summary>A mistake the mimic makes. Only ever applied to the mimic.</summary>
        Tell,
    }

    public enum FeatureCategory { Eyes, Brows, Nose, Mouth, Ears, Head, Skin, Body, Hands, Behaviour, Speech }

    public enum HairStyle { None, Short, Long, Bun, Combover, Wild }
    public enum HatStyle { None, Cap, Fedora, Bonnet }

    /// <summary>One detail on one resident, e.g. "missing finger, right hand".</summary>
    [Serializable]
    public class FeatureInstance
    {
        public string id;
        public FeatureSide side;
        [Tooltip("How the resident explains it, if asked. Empty for details they don't acknowledge.")]
        [TextArea(1, 3)] public string explanation;
        [Tooltip("Whether this detail shows in the resident's old archive photo (used by the records later).")]
        public bool inArchive = true;

        public FeatureInstance() { }

        public FeatureInstance(string id, FeatureSide side = FeatureSide.None, string explanation = null, bool inArchive = true)
        {
            this.id = id;
            this.side = side;
            this.explanation = explanation;
            this.inArchive = inArchive;
        }

        public FeatureInstance Clone() => new FeatureInstance(id, side, explanation, inArchive);
    }

    [Serializable]
    public class ResidentAppearance
    {
        [Range(0.8f, 1.2f)] public float height = 1f;
        [Range(0.7f, 1.4f)] public float build = 1f;
        [Range(0.8f, 1.3f)] public float headSize = 1f;
        public Color skin = new Color(0.82f, 0.66f, 0.55f);
        public Color hairColor = new Color(0.25f, 0.18f, 0.12f);
        public Color eyeColor = new Color(0.25f, 0.18f, 0.1f);
        public Color shirt = new Color(0.45f, 0.42f, 0.38f);
        public Color pants = new Color(0.22f, 0.24f, 0.3f);
        public Color shoes = new Color(0.12f, 0.09f, 0.07f);
        public HairStyle hair = HairStyle.Short;
        public HatStyle hat = HatStyle.None;
        public Color hatColor = new Color(0.3f, 0.25f, 0.2f);
        public bool glasses;
        public bool beard;
        public bool mustache;
        public bool apron;
        public bool overalls;
        public bool clericalCollar;
    }

    [Serializable]
    public class DialogueTopic
    {
        public string title;
        [TextArea(2, 5)] public string response;
        /// <summary>What the line claims, for checking contradictions (set at runtime, never saved in the profile).</summary>
        [NonSerialized] public string fact;
        /// <summary>What this line explains away, if it's a neighbour's account (run-only gossip).</summary>
        [NonSerialized] public string explains;

        public DialogueTopic() { }
        public DialogueTopic(string title, string response) { this.title = title; this.response = response; }
    }

    /// <summary>Changes the case generator made to a resident for this run (tells and red herrings).</summary>
    public class RunModifications
    {
        public readonly List<FeatureInstance> AddedFeatures = new List<FeatureInstance>();
        public readonly HashSet<string> RemovedFeatureIds = new HashSet<string>();
        public readonly Dictionary<string, FeatureSide> SideOverrides = new Dictionary<string, FeatureSide>();
        public readonly Dictionary<string, string> TopicOverrides = new Dictionary<string, string>();
        /// <summary>Gossip this resident has for this run only ("About X"), e.g. backing up an innocent's excuse.</summary>
        public readonly List<DialogueTopic> ExtraGossip = new List<DialogueTopic>();
        public string GreetingOverride;
        public readonly List<string> Notes = new List<string>(); // debug description of what was applied
    }
}
