using UnityEngine;

namespace Semblance.World
{
    /// <summary>The highway runs out of county. A line when you reach the end of it, and nothing past the wall.</summary>
    public class EdgeOfTown : MonoBehaviour
    {
        private float nextLine;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player") || Time.time < nextLine) return;
            nextLine = Time.time + 20f;
            UI.PlayerHUD.Instance?.ShowMessage("The road goes on for miles, and none of them are yours. Everything you came for is inside the sound of the bell.", 5f);
        }
    }
}
