using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Semblance.Core
{
    /// <summary>Full-screen fades and title cards ("DAY 2  -  TUESDAY").</summary>
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        private Image black;
        private TextMeshProUGUI title;
        private TextMeshProUGUI subtitle;

        public bool IsBusy { get; private set; }

        private void Awake()
        {
            Instance = this;
            var canvas = UIFactory.Canvas("Fader Canvas", transform, 150);
            black = UIFactory.Panel("Black", canvas.transform, new Color(0, 0, 0, 0), false);
            UIFactory.Stretch(black.rectTransform);
            title = UIFactory.Text("Title", canvas.transform, "", 44, UIFactory.Light, TextAlignmentOptions.Center);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1000, 70));
            subtitle = UIFactory.Text("Subtitle", canvas.transform, "", 20, new Color(1, 1, 1, 0.6f), TextAlignmentOptions.Top);
            UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(900, 120));
            title.alpha = 0;
            subtitle.alpha = 0;
        }

        public void SetBlack(float alpha) => black.color = new Color(0, 0, 0, alpha);

        public IEnumerator Fade(float to, float duration)
        {
            IsBusy = true;
            float from = black.color.a;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                SetBlack(Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            SetBlack(to);
            IsBusy = false;
        }

        public IEnumerator TitleCard(string text, string sub, float hold = 2.2f)
        {
            title.text = text;
            subtitle.text = sub;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.8f)
            {
                title.alpha = t;
                subtitle.alpha = t * 0.7f;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(hold);
            for (float t = 1; t > 0f; t -= Time.unscaledDeltaTime / 0.8f)
            {
                title.alpha = t;
                subtitle.alpha = t * 0.7f;
                yield return null;
            }
            title.alpha = 0;
            subtitle.alpha = 0;
        }
    }
}
