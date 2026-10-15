using UnityEngine;

namespace Semblance.Audio
{
    /// <summary>A looping sound that lives at a spot in the world: TV static in an empty motel room, the diner jukebox.</summary>
    public class AmbientEmitter : MonoBehaviour
    {
        [SerializeField] private string clipName;
        [SerializeField] private float volume = 0.1f;
        [SerializeField] private float maxDistance = 8f;

        private AudioSource source;

        private void Start()
        {
            source = GameAudio.Loop(clipName, transform, volume, maxDistance, AudioBus.World);
        }

        private void OnDestroy()
        {
            if (source != null) Destroy(source.gameObject);
        }
    }
}
