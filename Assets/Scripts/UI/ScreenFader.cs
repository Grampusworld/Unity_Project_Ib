using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Ib.UI
{
    /// <summary>
    /// 全屏黑幕，盖在所有 UI 之上（GDD §3.6 第 6 步「画中馆褪成黑色」等）。
    /// 用 unscaledDeltaTime，暂停或 timeScale=0 时也照常走。新的淡变会打断上一次，被打断的回调不再调用。
    /// 黑幕不挡鼠标，只挡画面。
    /// </summary>
    internal sealed class ScreenFader : MonoBehaviour
    {
        private Image image;
        private Coroutine running;

        public float Alpha => image.color.a;

        public static ScreenFader Create(Transform parent)
        {
            var go = new GameObject("ScreenFader", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            UIFactory.Stretch(go);
            var fader = go.AddComponent<ScreenFader>();
            fader.image = go.GetComponent<Image>();
            fader.image.color = new Color(0f, 0f, 0f, 0f);
            fader.image.raycastTarget = false;
            return fader;
        }

        public void FadeTo(float target, float seconds, Action onDone)
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Run(target, seconds, onDone));
        }

        private IEnumerator Run(float target, float seconds, Action onDone)
        {
            float from = image.color.a;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                Set(Mathf.Lerp(from, target, t / seconds));
                yield return null;
            }

            Set(target);
            running = null;
            onDone?.Invoke();
        }

        private void Set(float a) => image.color = new Color(0f, 0f, 0f, a);
    }
}
