using Semblance.Residents;
using UnityEngine;

namespace Semblance.Events
{
    public enum ApparitionLook
    {
        /// <summary>A pitch-black silhouette with the exact body shape of the person it copies.</summary>
        Shade,
        /// <summary>Looks exactly like the person (doubles, mirror ghosts).</summary>
        Double,
        /// <summary>The mimic with its face slipping: long arms, broken neck, white eyes (the hunt).</summary>
        HuntForm,
    }

    /// <summary>A figure built from a resident's look, used by night events, doubles, mirror ghosts and the hunt.</summary>
    public class Apparition : MonoBehaviour
    {
        public ResidentAvatar Avatar { get; private set; }
        public ApparitionLook Look { get; private set; }

        public static Apparition Create(Resident model, ApparitionLook look, Vector3 position, Quaternion rotation, int layer = 0)
        {
            var go = new GameObject("Apparition (" + look + ")");
            go.SetActive(false);
            go.transform.SetPositionAndRotation(position, rotation);
            var apparition = go.AddComponent<Apparition>();
            apparition.Look = look;
            var avatar = go.AddComponent<ResidentAvatar>();
            apparition.Avatar = avatar;
            avatar.Rebuilt += apparition.Restyle;
            avatar.SetProfile(model.Profile, null);
            if (model.Avatar.Modifications != null)
                avatar.ApplyRunModifications(model.Avatar.Modifications);
            go.SetActive(true);
            SetLayer(go, layer);
            return apparition;
        }

        public static void SetLayer(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }

        private void Restyle()
        {
            var rig = Avatar.Rig;
            if (rig == null) return;
            if (Look == ApparitionLook.Shade)
            {
                var black = rig.GetMaterial(new Color(0.012f, 0.012f, 0.014f));
                foreach (var r in rig.Root.GetComponentsInChildren<MeshRenderer>(true))
                    r.sharedMaterial = black;
                rig.Tint("PupilL", new Color(0.75f, 0.75f, 0.7f));
                rig.Tint("PupilR", new Color(0.75f, 0.75f, 0.7f));
                rig.Tint("EyeLWhite", new Color(0.012f, 0.012f, 0.014f));
                rig.Tint("EyeRWhite", new Color(0.012f, 0.012f, 0.014f));
            }
            else if (Look == ApparitionLook.HuntForm)
            {
                // Black all over, so the face it borrowed can't be read.
                var black = rig.GetMaterial(new Color(0.02f, 0.018f, 0.02f));
                foreach (var r in rig.Root.GetComponentsInChildren<MeshRenderer>(true))
                    r.sharedMaterial = black;
                rig.Scale("ArmL", new Vector3(1f, 1.55f, 1f));
                rig.Scale("ArmR", new Vector3(1f, 1.55f, 1f));
                rig.Move("Head", new Vector3(0, 0.12f, 0.04f));
                rig.Rotate("NeckPivot", new Vector3(10f, 0, 78f));
                rig.Scale("Mouth", new Vector3(2.4f, 6f, 1f));
                rig.Tint("Mouth", new Color(0.05f, 0.01f, 0.01f));
                rig.Tint("PupilL", Color.white);
                rig.Tint("PupilR", Color.white);
                rig.Scale("PupilL", new Vector3(1.6f, 1.6f, 1f));
                rig.Scale("PupilR", new Vector3(1.6f, 1.6f, 1f));
                rig.Scale("HandL", Vector3.one * 1.5f);
                rig.Scale("HandR", Vector3.one * 1.5f);
            }
            SetLayer(gameObject, gameObject.layer);
        }

        public bool IsSeenBy(Camera cam, float maxDistance, float minDot = 0.82f)
        {
            if (cam == null) return false;
            Vector3 head = transform.position + Vector3.up * 1.5f;
            Vector3 to = head - cam.transform.position;
            if (to.magnitude > maxDistance) return false;
            if (Vector3.Dot(cam.transform.forward, to.normalized) < minDot) return false;
            return !Physics.Linecast(cam.transform.position, head, out var hit, ~0, QueryTriggerInteraction.Ignore)
                   || hit.collider.transform.IsChildOf(transform);
        }

        public void Vanish()
        {
            if (this != null) Destroy(gameObject);
        }
    }
}
