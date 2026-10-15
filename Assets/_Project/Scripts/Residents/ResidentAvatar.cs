using System;
using System.Collections.Generic;
using UnityEngine;

namespace Semblance.Residents
{
    /// <summary>
    /// Builds a resident's body from their profile plus this run's modifications (tells, red herrings).
    /// The generated rig isn't saved with the scene; it's rebuilt whenever the object is enabled or changed.
    /// </summary>
    [ExecuteAlways]
    public class ResidentAvatar : MonoBehaviour
    {
        [SerializeField] private ResidentProfile profile;
        [SerializeField] private Shader shader;

        public ResidentProfile Profile => profile;
        public AvatarRig Rig { get; private set; }
        public RunModifications Modifications { get; private set; }

        /// <summary>The details this resident actually has right now (profile + run changes).</summary>
        public IReadOnlyList<FeatureInstance> ActiveFeatures => activeFeatures;

        public event Action Rebuilt;

        private readonly List<FeatureInstance> activeFeatures = new List<FeatureInstance>();

        private void OnEnable() => Build();

        private void OnDisable()
        {
            if (Rig != null && Rig.Root != null)
                AvatarRig.DestroySafe(Rig.Root.gameObject);
            Rig = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled)
                    Build();
            };
        }
#endif

        public void SetProfile(ResidentProfile newProfile, Shader newShader)
        {
            profile = newProfile;
            shader = newShader;
            Build();
        }

        public void ApplyRunModifications(RunModifications modifications)
        {
            Modifications = modifications;
            Build();
        }

        public void Build()
        {
            var old = transform.Find("Rig");
            if (old != null)
                AvatarRig.DestroySafe(old.gameObject);
            Rig = null;
            if (profile == null)
                return;
            if (shader == null)
                shader = Shader.Find("Semblance/PS1 Lit");

            CollectFeatures();
            Rig = AvatarRig.Create(transform, profile.appearance, shader);
            foreach (var feature in activeFeatures)
            {
                var definition = FeatureCatalog.Get(feature.id);
                if (definition == null)
                    Debug.LogWarning($"[Semblance] {profile.displayName}: unknown feature '{feature.id}'", this);
                else
                    definition.Apply?.Invoke(Rig, feature);
            }

            foreach (var t in Rig.Root.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            Rebuilt?.Invoke();
        }

        private void CollectFeatures()
        {
            activeFeatures.Clear();
            foreach (var feature in profile.features)
            {
                if (feature == null || string.IsNullOrEmpty(feature.id))
                    continue;
                if (Modifications != null && Modifications.RemovedFeatureIds.Contains(feature.id))
                    continue;
                var copy = feature.Clone();
                if (Modifications != null && Modifications.SideOverrides.TryGetValue(feature.id, out var side))
                    copy.side = side;
                activeFeatures.Add(copy);
            }
            if (Modifications != null)
                activeFeatures.AddRange(Modifications.AddedFeatures);
        }

        /// <summary>
        /// Camera flash: ordinary eyes catch red-eye; the mimic's eyes flare pure white. Call again with false to restore.
        /// </summary>
        public void SetFlashEyes(bool on, bool anomalous)
        {
            if (Rig == null) return;
            foreach (var name in new[] { "PupilL", "PupilR" })
            {
                var pupil = Rig.Part(name);
                if (pupil == null || !pupil.TryGetComponent<MeshRenderer>(out var r)) continue;
                if (on)
                {
                    flashRestore[name] = r.sharedMaterial;
                    r.sharedMaterial = Rig.GetMaterial(anomalous ? new Color(1f, 1f, 1f) : new Color(0.55f, 0.06f, 0.05f));
                    if (anomalous) pupil.localScale *= 2.2f;
                }
                else if (flashRestore.TryGetValue(name, out var m))
                {
                    if (r.sharedMaterial == Rig.GetMaterial(new Color(1f, 1f, 1f))) pupil.localScale /= 2.2f;
                    r.sharedMaterial = m;
                }
            }
            // The camera catches what the eye doesn't: the face doesn't hold still.
            if (on && anomalous && Rig.Part("PhotoSmear") == null)
                Rig.Box("PhotoSmear", "Head", new Vector3(0.05f, 0.16f, 0.01f), new Vector3(0.22f, 0.25f, 0.23f), Rig.Look.skin * 1.15f, new Vector3(0, 0, 9f));
            if (!on) { Rig.Remove("PhotoSmear"); flashRestore.Clear(); }
        }

        private readonly Dictionary<string, Material> flashRestore = new Dictionary<string, Material>();

        public BehaviourFlags GetBehaviourFlags()
        {
            var flags = BehaviourFlags.None;
            foreach (var feature in activeFeatures)
            {
                var definition = FeatureCatalog.Get(feature.id);
                if (definition != null)
                    flags |= definition.Behaviour;
            }
            return flags;
        }

        public SpeechFlags GetSpeechFlags()
        {
            var flags = SpeechFlags.None;
            foreach (var feature in activeFeatures)
            {
                var definition = FeatureCatalog.Get(feature.id);
                if (definition != null)
                    flags |= definition.Speech;
            }
            return flags;
        }
    }
}
