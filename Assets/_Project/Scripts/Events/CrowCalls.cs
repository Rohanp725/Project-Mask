using UnityEngine;

namespace Semblance.Events
{
    /// <summary>Crows over something in a field: a call every few seconds, heard a long way off.</summary>
    public class CrowCalls : MonoBehaviour
    {
        private float next;

        private void Update()
        {
            if (Time.time < next) return;
            next = Time.time + Random.Range(3.5f, 8f);
            var pos = transform.position + new Vector3(Random.Range(-4f, 4f), Random.Range(1f, 4f), Random.Range(-4f, 4f));
            Audio.GameAudio.PlayAt(Random.value < 0.5f ? "crow_1" : "crow_2", pos, 0.5f, Random.Range(0.85f, 1.1f), 90f, Audio.AudioBus.World);
        }
    }
}
