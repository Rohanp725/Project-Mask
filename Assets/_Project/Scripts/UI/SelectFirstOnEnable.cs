using Semblance.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Semblance.UI
{
    /// <summary>
    /// When a screen opens, give keyboard/gamepad navigation somewhere to start: select its first interactable button
    /// (or a chosen one). Without this, a gamepad can't press anything on a uGUI screen.
    /// </summary>
    public class SelectFirstOnEnable : MonoBehaviour
    {
        public Selectable Preferred;

        private void OnEnable() => StartCoroutine(SelectNextFrame());

        private System.Collections.IEnumerator SelectNextFrame()
        {
            yield return null; // layouts and buttons are built this frame
            Select(gameObject, Preferred);
        }

        public static void Select(GameObject root, Selectable preferred = null)
        {
            var es = EventSystem.current;
            if (es == null || root == null || !root.activeInHierarchy) return;
            var target = preferred != null && preferred.isActiveAndEnabled && preferred.interactable ? preferred : null;
            if (target == null)
                foreach (var s in root.GetComponentsInChildren<Selectable>())
                    if (s.isActiveAndEnabled && s.interactable) { target = s; break; }
            if (target != null) es.SetSelectedGameObject(target.gameObject);
        }
    }

    /// <summary>
    /// A small marker beside whatever is selected, shown while the player is navigating with a gamepad or the arrow
    /// keys (mouse users see hover highlights instead).
    /// </summary>
    public class SelectionMarker : MonoBehaviour
    {
        private static SelectionMarker instance;
        private RectTransform marker;
        private Canvas canvas;

        public static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("Selection Marker");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SelectionMarker>();
        }

        private void Awake()
        {
            instance = this; // first, so building the canvas (which ensures an event system) doesn't make another
            canvas = UIFactory.Canvas("Selection Canvas", transform, 500);
            var text = UIFactory.Text("Marker", canvas.transform, "<b>></b>", 26, UIFactory.Accent, TextAlignmentOptions.MidlineRight);
            marker = text.rectTransform;
            marker.sizeDelta = new Vector2(30, 30);
            marker.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            var es = EventSystem.current;
            var selected = es != null ? es.currentSelectedGameObject : null;
            bool show = selected != null && selected.activeInHierarchy && GameInput.UsingGamepad && InputLock.IsLocked;
            marker.gameObject.SetActive(show);
            if (!show) return;
            var rect = selected.GetComponent<RectTransform>();
            if (rect == null) return;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 leftMiddle = (corners[0] + corners[1]) * 0.5f;
            marker.position = leftMiddle + Vector3.left * 6f;
            marker.pivot = new Vector2(1f, 0.5f);
        }
    }
}
