using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ib.UI
{
    /// <summary>
    /// 结局字幕（灰盒版，GDD §6.9）：全黑底，标题先出，正文一段一段淡入。
    /// 全部出完后按任意键（或等几秒）结束，调 onDone。盖在黑幕之上。
    /// </summary>
    internal sealed class EndingCard : MonoBehaviour
    {
        private const float LineFade = 1.2f;
        private const float LineHold = 2.4f;
        private const float AutoClose = 8f;

        private GameObject view;
        private Text title;
        private Text body;
        private Text prompt;

        public bool IsShowing => view.activeSelf;

        public static EndingCard Create(Transform parent)
        {
            GameObject go = UIFactory.Empty(parent, "Ending");
            var card = go.AddComponent<EndingCard>();
            card.view = UIFactory.Panel(go.transform, "Black", Color.black);

            card.title = UIFactory.Label(card.view.transform, "Title", "", 40, TextAnchor.MiddleCenter, new Color(0.92f, 0.89f, 0.8f));
            var trt = card.title.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(1100f, 60f);
            trt.anchoredPosition = new Vector2(0f, 230f);

            card.body = UIFactory.Label(card.view.transform, "Body", "", 26, TextAnchor.UpperCenter, new Color(0.8f, 0.78f, 0.72f));
            card.body.lineSpacing = 1.3f;
            card.body.supportRichText = true;
            var brt = card.body.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(1000f, 400f);
            brt.anchoredPosition = new Vector2(0f, -20f);

            card.prompt = UIFactory.Label(card.view.transform, "Prompt", "Press any key", 20, TextAnchor.LowerCenter, new Color(0.5f, 0.48f, 0.44f));
            var prt = card.prompt.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(0f, 60f);

            card.view.SetActive(false);
            return card;
        }

        public void Show(string heading, string[] lines, Action onDone)
        {
            StopAllCoroutines();
            view.SetActive(true);
            transform.SetAsLastSibling();
            StartCoroutine(Play(heading, lines, onDone));
        }

        private IEnumerator Play(string heading, string[] lines, Action onDone)
        {
            title.text = heading;
            body.text = "";
            prompt.enabled = false;
            yield return Fade(title, LineFade);
            yield return new WaitForSecondsRealtime(LineHold * 0.5f);

            string shown = "";
            foreach (string line in lines)
            {
                // 新的一行先透明，再淡入；之前的行保持不透明
                string previous = shown;
                for (float t = 0f; t < LineFade; t += Time.unscaledDeltaTime)
                {
                    int a = Mathf.RoundToInt(Mathf.Clamp01(t / LineFade) * 255f);
                    body.text = previous + "<color=#CCC7B8" + a.ToString("X2") + ">" + line + "</color>\n\n";
                    yield return null;
                }

                shown = previous + line + "\n\n";
                body.text = shown;
                yield return new WaitForSecondsRealtime(LineHold);
            }

            prompt.enabled = true;
            float until = Time.unscaledTime + AutoClose;
            while (Time.unscaledTime < until && !AnyKey())
            {
                yield return null;
            }

            onDone?.Invoke();
        }

        private static IEnumerator Fade(Graphic g, float seconds)
        {
            Color c = g.color;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                c.a = t / seconds;
                g.color = c;
                yield return null;
            }

            c.a = 1f;
            g.color = c;
        }

        private static bool AnyKey()
        {
            return (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
        }
    }
}
