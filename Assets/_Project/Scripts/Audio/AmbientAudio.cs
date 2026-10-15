using Semblance.Case;
using Semblance.Core;
using UnityEngine;

namespace Semblance.Audio
{
    /// <summary>
    /// The sound of Semblance: wind, crickets at night (which go quiet when something wrong is near),
    /// room tone indoors, crows by day and owls by night, and a low dread drone that deepens as the mimic
    /// grows aware of you.
    ///
    /// Mixing rules: the beds sit low and move (gusts, lulls), so they never become a wall of noise; anything
    /// the player has to hear (cues, voices) ducks them through <see cref="GameAudio"/>; silence is used on purpose.
    /// </summary>
    public class AmbientAudio : MonoBehaviour
    {
        public static AmbientAudio Instance { get; private set; }

        /// <summary>0 = normal, 1 = everything holds its breath (crickets stop, wind drops). Set by scares.</summary>
        public static float Hush { get; set; }

        private AudioSource wind, crickets, room, drone, deepDrone;
        private float nextBird, lullUntil, nextLull;
        private Transform listener;

        private void Awake() => Instance = this;

        private void Start()
        {
            wind = GameAudio.Loop("wind_loop", null, 0f, bus: AudioBus.Ambience);
            crickets = GameAudio.Loop("crickets_loop", null, 0f, bus: AudioBus.Ambience);
            room = GameAudio.Loop("room_tone_loop", null, 0f, bus: AudioBus.Ambience);
            drone = GameAudio.Loop("drone_loop", null, 0f, bus: AudioBus.Ambience);
            deepDrone = GameAudio.Loop("drone_deep_loop", null, 0f, bus: AudioBus.Ambience);
            nextBird = Time.time + 10f;
            nextLull = Time.time + Random.Range(40f, 90f);
        }

        public static bool IsIndoors(Vector3 position)
        {
            return Physics.Raycast(position + Vector3.up * 0.5f, Vector3.up, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore)
                   && hit.collider.name != "Tree";
        }

        private void Update()
        {
            if (listener == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                listener = cam.transform;
            }
            var clock = GameClock.Instance;
            float hour = clock != null ? clock.Hour : 22f;
            bool night = hour >= 20.5f || hour < 6f;
            bool dusk = hour >= 18.5f && !night;
            bool indoors = IsIndoors(listener.position - Vector3.up * 1.5f);
            bool underground = listener.position.y < -10f;

            // Crickets fall silent around a few people after dark: the mimic, if that's one of its signs this time,
            // and somebody innocent who just keeps very still at night.
            float mimicQuiet = 0f;
            var noise = CaseDirector.Instance != null ? CaseDirector.Instance.Noise : null;
            if (night && noise != null)
                foreach (var r in noise.CricketHush)
                    if (r != null && r.isActiveAndEnabled)
                        mimicQuiet = Mathf.Max(mimicQuiet, 1f - Mathf.InverseLerp(10f, 22f, Vector3.Distance(r.transform.position, listener.position)));

            // Every so often the whole night goes still for a few seconds. It makes the next sound land.
            if (Time.time > nextLull)
            {
                lullUntil = Time.time + Random.Range(4f, 9f);
                nextLull = Time.time + Random.Range(50f, 120f);
            }
            float lull = night && Time.time < lullUntil ? 0.7f : 0f;
            float hush = Mathf.Clamp01(Mathf.Max(Mathf.Max(Hush, mimicQuiet), lull));

            int tier = MimicAwareness.Instance != null ? MimicAwareness.Instance.Tier : 0;
            float t = Time.time;
            // Slow gusts: the wind rises and falls instead of sitting at one level.
            float gust = 0.6f + 0.4f * Mathf.PerlinNoise(t * 0.05f, 0.3f);
            float dt = Time.deltaTime * 0.5f;

            float windLevel = underground ? 0f : (night ? 0.16f : 0.11f) * gust * (indoors ? 0.3f : 1f) * (1f - hush * 0.7f);
            float cricketLevel = underground ? 0f : (night || dusk ? 0.12f : 0f) * (indoors ? 0.2f : 1f) * (1f - hush);
            float roomLevel = indoors || underground ? (underground ? 0.05f : 0.07f) : 0f;
            float droneLevel = (night ? 0.025f + tier * 0.03f : 0.01f) * (underground ? 1.6f : 1f);
            float deepLevel = night && tier >= 2 ? 0.06f + (tier - 2) * 0.06f : 0f;

            Fade(wind, windLevel, dt);
            Fade(crickets, cricketLevel, dt * 2f);
            Fade(room, roomLevel, dt);
            Fade(drone, droneLevel, dt * 0.3f);
            Fade(deepDrone, deepLevel, dt * 0.3f);

            if (Time.time > nextBird && !indoors && !underground)
            {
                nextBird = Time.time + Random.Range(25f, 60f);
                Vector3 pos = listener.position + Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward * Random.Range(30f, 50f) + Vector3.up * 8f;
                if (night && hush < 0.5f)
                    GameAudio.PlayAt("owl_1", pos, 0.4f, Random.Range(0.9f, 1.05f), 70f, AudioBus.World);
                else if (!night)
                    GameAudio.PlayAt(Random.value < 0.5f ? "crow_1" : "crow_2", pos, 0.35f, Random.Range(0.9f, 1.1f), 70f, AudioBus.World);
            }
        }

        private static void Fade(AudioSource source, float target, float speed)
        {
            if (source == null) return;
            GameAudio.SetVolume(source, Mathf.MoveTowards(GameAudio.GetVolume(source), target, speed));
        }
    }
}
