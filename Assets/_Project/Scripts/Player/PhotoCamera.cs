using System;
using System.Collections.Generic;
using System.Linq;
using Semblance.Core;
using Semblance.Notes;
using Semblance.Residents;
using Semblance.UI;
using Semblance.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Semblance.Player
{
    /// <summary>
    /// The detective's instant camera. Q raises it, left click takes a photo (uses film), Q again lowers it.
    /// Photos are filed in the notebook with who's in them and what's visible. The flash gives ordinary
    /// people red-eye; the mimic's eyes come out pure white.
    /// </summary>
    public class PhotoCamera : MonoBehaviour
    {
        public static PhotoCamera Instance { get; private set; }

        [SerializeField] private Camera viewCamera;
        [SerializeField] private int film = 12;
        [SerializeField] private float subjectRange = 16f;
        [SerializeField] private int photoWidth = 480;
        [SerializeField] private int photoHeight = 360;

        public int Film => film;
        public bool IsRaised { get; private set; }

        /// <summary>Raised after a photo is taken, with every resident in it.</summary>
        public static event Action<List<Resident>> PhotoTaken;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics() => PhotoTaken = null;

        private Camera photoCamera;
        private RenderTexture photoTarget;
        private GameObject viewfinder;
        private TextMeshProUGUI filmLabel;
        private Image flashOverlay;
        private float flashAlpha;
        private float cooldownUntil;

        private void Awake()
        {
            Instance = this;
            if (viewCamera == null)
                viewCamera = GetComponentInChildren<Camera>();
            BuildUI();
        }

        public void AddFilm(int amount)
        {
            film += amount;
            UpdateLabel();
        }

        public void SetFilm(int amount)
        {
            film = amount;
            UpdateLabel();
        }

        private void Update()
        {
            if (InputLock.IsLocked)
            {
                if (IsRaised) SetRaised(false);
            }
            else
            {
                if (GameInput.Pressed(GameInput.Camera))
                    SetRaised(!IsRaised);
                if (IsRaised && GameInput.Pressed(GameInput.Shoot) && Cursor.lockState == CursorLockMode.Locked && Time.time > cooldownUntil)
                    TakePhoto();
            }

            if (flashAlpha > 0f)
            {
                flashAlpha = Mathf.Max(0f, flashAlpha - Time.deltaTime * 2.5f);
                flashOverlay.color = new Color(1, 1, 1, flashAlpha);
            }
        }

        private void SetRaised(bool raised)
        {
            IsRaised = raised;
            viewfinder.SetActive(raised);
            UpdateLabel();
            Audio.GameAudio.Play(raised ? "camera_raise" : "camera_lower", 0.5f);
        }

        private void UpdateLabel()
        {
            if (filmLabel != null)
                filmLabel.text = film > 0 ? $"FILM {film:00}" : "<color=#c03020>NO FILM</color>";
        }

        // ------------------------------------------------------------ Taking a photo

        public void TakePhoto()
        {
            cooldownUntil = Time.time + 1.2f;
            if (film <= 0)
            {
                PlayerHUD.Instance?.Toast("Out of film. Otis sells more at the general store.");
                Audio.GameAudio.Play("camera_empty", 0.6f);
                return;
            }
            film--;
            UpdateLabel();

            var subjects = FindSubjects();
            var texture = Capture(subjects);
            flashAlpha = 0.9f;
            Audio.GameAudio.Play("camera_shutter", 0.9f);

            var notebook = Notebook.Instance;
            if (notebook != null)
            {
                int index = notebook.AddPhoto(texture);
                var clock = GameClock.Instance;
                string where = LocationMarker.Describe(transform.position);
                string stampKey = $"photo|{index}";
                if (subjects.Count == 0)
                    notebook.Record(NoteKind.Photo, Notebook.CaseSubject, "Photograph", $"Taken {where}.", null, stampKey, index);
                foreach (var r in subjects)
                {
                    var details = r.GetObservationLines();
                    string text = $"Taken {where}." + (details.Count > 0 && GameSettings.ListObservedDetails ? " In the photo: " + string.Join(" ", details.Select(r.Named)) : "");
                    if (Case.CaseDirector.Instance != null && Case.CaseDirector.Instance.FlaresInPhotos(r))
                        text += $" {r.FirstName}'s eyes flared white in the print.";
                    notebook.Record(NoteKind.Photo, r.DisplayName, "Photograph", text, null, $"{stampKey}|{r.DisplayName}", index);
                }
            }
            PlayerHUD.Instance?.Toast(subjects.Count > 0 ? $"Photographed {string.Join(", ", subjects.Select(s => s.FirstName))}." : "Photo taken.");
            PhotoTaken?.Invoke(subjects);
        }

        private List<Resident> FindSubjects()
        {
            var list = new List<Resident>();
            foreach (var r in Resident.All)
            {
                var head = r.Avatar.Rig?.Part("Head");
                if (head == null) continue;
                Vector3 target = head.position + Vector3.up * 0.15f;
                float distance = Vector3.Distance(viewCamera.transform.position, target);
                if (distance > subjectRange) continue;
                var vp = viewCamera.WorldToViewportPoint(target);
                if (vp.z <= 0 || vp.x < 0.08f || vp.x > 0.92f || vp.y < 0.05f || vp.y > 0.95f) continue;
                if (Physics.Linecast(viewCamera.transform.position, target, out var hit, ~0, QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponentInParent<Resident>() != r)
                    continue;
                list.Add(r);
            }
            return list;
        }

        private Texture2D Capture(List<Resident> subjects)
        {
            if (photoCamera == null)
            {
                var go = new GameObject("Photo Camera");
                go.transform.SetParent(viewCamera.transform, false);
                photoCamera = go.AddComponent<Camera>();
                photoCamera.enabled = false;
                photoTarget = new RenderTexture(photoWidth, photoHeight, 24) { filterMode = FilterMode.Point };
            }
            photoCamera.CopyFrom(viewCamera);
            photoCamera.targetTexture = photoTarget;

            // The flash.
            var flashGO = new GameObject("Flash");
            flashGO.transform.SetParent(viewCamera.transform, false);
            var flash = flashGO.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.range = 14f;
            flash.intensity = 9f;
            flash.color = new Color(1f, 0.97f, 0.9f);

            var director = Case.CaseDirector.Instance;
            foreach (var r in subjects)
                r.Avatar.SetFlashEyes(true, director != null && director.FlaresInPhotos(r));

            photoCamera.Render();
            var tex = Archive.PortraitStudio.ReadTarget(photoTarget);

            foreach (var r in subjects)
                r.Avatar.SetFlashEyes(false, false);
            Destroy(flashGO);

            Develop(tex);
            return tex;
        }

        /// <summary>Instant-film look: warm cast, lifted blacks, vignette, grain.</summary>
        private static void Develop(Texture2D tex)
        {
            // Integer maths on 32-bit pixels: this runs on the shutter frame, so keep it quick.
            var rng = new System.Random(Environment.TickCount);
            var pixels = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            for (int y = 0; y < h; y++)
            {
                float dy = (y / (float)h - 0.5f) * 2f;
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    var c = pixels[i];
                    float dx = (x / (float)w - 0.5f) * 2f;
                    float vignette = Mathf.Lerp(1.05f, 0.45f, Mathf.Clamp01((dx * dx + dy * dy) * 0.55f)) * 0.9f;
                    int grain = rng.Next(-8, 9); // about +-0.03
                    pixels[i] = new Color32(
                        Develop1(c.r * 1.06f * vignette + 15f + grain),
                        Develop1(c.g * 0.98f * vignette + 13f + grain),
                        Develop1(c.b * 0.86f * vignette + 13f + grain), 255);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
        }

        private static byte Develop1(float v) => (byte)(v < 0f ? 0f : v > 255f ? 255f : v);

        // ------------------------------------------------------------ Viewfinder

        private void BuildUI()
        {
            var canvas = UIFactory.Canvas("Camera Canvas", transform, 15);
            viewfinder = UIFactory.Rect("Viewfinder", canvas.transform).gameObject;
            UIFactory.Stretch((RectTransform)viewfinder.transform);
            var c = new Color(1, 1, 1, 0.75f);
            foreach (var (anchor, sx, sy) in new[] { (new Vector2(0.15f, 0.15f), 1, 1), (new Vector2(0.85f, 0.15f), -1, 1), (new Vector2(0.15f, 0.85f), 1, -1), (new Vector2(0.85f, 0.85f), -1, -1) })
            {
                var h = UIFactory.Panel("Corner H", viewfinder.transform, c, false);
                UIFactory.Place(h.rectTransform, anchor, new Vector2(sx > 0 ? 0 : 1, 0.5f), Vector2.zero, new Vector2(40, 3));
                var v = UIFactory.Panel("Corner V", viewfinder.transform, c, false);
                UIFactory.Place(v.rectTransform, anchor, new Vector2(0.5f, sy > 0 ? 0 : 1), Vector2.zero, new Vector2(3, 40));
            }
            filmLabel = UIFactory.Text("Film", viewfinder.transform, "", 18, c, TextAlignmentOptions.BottomRight);
            UIFactory.Place(filmLabel.rectTransform, new Vector2(0.85f, 0.15f), new Vector2(1, 1), new Vector2(0, -10), new Vector2(200, 30));
            var help = UIFactory.Text("Help", viewfinder.transform, "LEFT CLICK  take photo     Q  lower camera     RIGHT CLICK  zoom", 13, new Color(1, 1, 1, 0.5f), TextAlignmentOptions.Bottom);
            UIFactory.Place(help.rectTransform, new Vector2(0.5f, 0.15f), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(700, 24));
            viewfinder.SetActive(false);

            flashOverlay = UIFactory.Panel("Flash", canvas.transform, new Color(1, 1, 1, 0), false);
            UIFactory.Stretch(flashOverlay.rectTransform);
            UpdateLabel();
        }
    }
}
