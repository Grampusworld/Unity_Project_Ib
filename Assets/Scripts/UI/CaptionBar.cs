using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Ib.UI
{
    /// <summary>
    /// 内心独白字幕：屏幕下半区 1/4 高度处（离底边 25%）居中一行到两行，淡入、停留、淡出，结束后回调。
    /// 新的一条会打断旧的（旧回调不再调）。用不受 timeScale 影响的时间。
    /// </summary>
    internal sealed class CaptionBar : MonoBehaviour
    {
        private const float Fade = 0.6f;

        private CanvasGroup group;
        private Text label;
        private Coroutine playing;

        public static CaptionBar Create(Transform parent)
        {
            GameObject go = UIFactory.Empty(parent, "Caption");
            var bar = go.AddComponent<CaptionBar>();
            bar.group = go.AddComponent<CanvasGroup>();
            bar.group.alpha = 0f;
            bar.group.blocksRaycasts = false;
            bar.group.interactable = false;

            bar.label = UIFactory.Label(go.transform, "Text", "", 28, TextAnchor.MiddleCenter, new Color(0.93f, 0.91f, 0.86f));
            var rt = bar.label.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.25f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(1100f, 110f);
            bar.label.lineSpacing = 1.1f;
            bar.label.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.85f);
            return bar;
        }

        public void Show(string text, float seconds, Action onDone)
        {
            if (playing != null) StopCoroutine(playing);
            playing = StartCoroutine(Play(text, seconds, onDone));
        }

        private IEnumerator Play(string text, float seconds, Action onDone)
        {
            label.text = text;
            transform.SetAsLastSibling();
            for (float t = 0f; t < Fade; t += Time.unscaledDeltaTime)
            {
                group.alpha = t / Fade;
                yield return null;
            }

            group.alpha = 1f;
            yield return new WaitForSecondsRealtime(seconds);
            for (float t = 0f; t < Fade; t += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - t / Fade;
                yield return null;
            }

            group.alpha = 0f;
            playing = null;
            onDone?.Invoke();
        }
    }
}
