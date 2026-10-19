using UnityEngine;

namespace Semblance.Player
{
    /// <summary>
    /// The detective's body: seen in mirrors and from outside the window, never by the player's own camera
    /// (it sits on the PlayerBody layer, which that camera culls). A long coat, hands, a hat, and a face with
    /// nothing on it you could pick out of a crowd. This walks it: a bob with the stride, arms that swing, a head
    /// that follows the eyes.
    /// </summary>
    public class PlayerBody : MonoBehaviour
    {
        private CharacterController controller;
        private Transform armL, armR, head, cameraRoot;
        private float stride;

        private void Start()
        {
            controller = GetComponentInParent<CharacterController>();
            var coat = transform.Find("Coat");
            if (coat != null) { armL = coat.Find("Arm L"); armR = coat.Find("Arm R"); }
            head = transform.Find("Head");
            if (transform.parent != null) cameraRoot = transform.parent.Find("Camera Root");
        }

        private void LateUpdate()
        {
            float speed = controller != null && controller.enabled ? Vector3.ProjectOnPlane(controller.velocity, Vector3.up).magnitude : 0f;
            float walking = Mathf.Clamp01(speed / 2f);
            if (speed > 0.1f) stride += Time.deltaTime * speed * 3.2f;
            float swing = Mathf.Sin(stride) * walking * 26f;
            if (armL != null) armL.localRotation = Quaternion.Euler(swing, 0, 0);
            if (armR != null) armR.localRotation = Quaternion.Euler(-swing, 0, 0);
            transform.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(stride)) * 0.02f * walking, 0);
            if (head != null && cameraRoot != null)
            {
                float pitch = Mathf.DeltaAngle(0f, cameraRoot.localEulerAngles.x);
                head.localRotation = Quaternion.Euler(pitch * 0.6f, 0, 0);
            }
        }
    }
}
