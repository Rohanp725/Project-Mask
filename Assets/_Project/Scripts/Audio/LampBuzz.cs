using Semblance.Rendering;
using UnityEngine;

namespace Semblance.Audio
{
    /// <summary>The electrical hum of a lamp, audible only up close and only while it's lit.</summary>
    [RequireComponent(typeof(LampSwitch))]
    public class LampBuzz : MonoBehaviour
    {
        private LampSwitch lamp;
        private AudioSource source;

        private void Start()
        {
            lamp = GetComponent<LampSwitch>();
            var anchor = lamp.Lamp != null ? lamp.Lamp.transform : transform;
            source = GameAudio.Loop("lamp_buzz_loop", anchor, 0f, 6f, AudioBus.Ambience);
        }

        private void Update()
        {
            if (source == null) return;
            bool lit = lamp.IsOn && lamp.Lamp != null && lamp.Lamp.enabled;
            // Out of earshot or unlit, stop the source outright: a silent loop still costs a voice and its filter.
            var listener = Camera.main;
            bool near = listener != null && (listener.transform.position - source.transform.position).sqrMagnitude < source.maxDistance * source.maxDistance * 1.2f;
            bool audible = lit && near;
            if (audible && !source.isPlaying) source.Play();
            else if (!audible && source.isPlaying) source.Stop();
            if (audible) GameAudio.SetVolume(source, 0.12f);
        }
    }
}
