using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Ib.UI
{
    /// <summary>GDD §3.7：新任务出现时屏幕上方淡入一行小字，3 秒后淡出。用游戏时间，暂停时一起停住。</summary>
    internal sealed class HintBar : MonoBehaviour
    {
        private const float Fade = 0.6f;
        private const float Hold = 3f;

        private CanvasGroup group;
        private Text label;
        private HintTable table;
        private Coroutine playing;

        public static HintBar Create(Transform parent)
        {
            GameObject go = UIFactory.Empty(parent, "Hint");
            var bar = go.AddComponent<HintBar>();
            bar.group = go.AddComponent<CanvasGroup>();
            bar.group.alpha = 0f;
            bar.group.blocksRaycasts = false;

            bar.label = UIFactory.Label(go.transform, "Text", "", 30, TextAnchor.MiddleCenter, new Color(0.92f, 0.9f, 0.85f));
            var rt = bar.label.rectTransform;
            rt.anchorMin = new Vector2(0.2f, 1f);
            rt.anchorMax = new Vector2(0.8f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 60f);
            rt.anchoredPosition = new Vector2(0f, -70f);
            bar.label.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            bar.table = Resources.Load<HintTable>("Texts/Hints");
            if (bar.table == null) Debug.LogError("[Presentation] 缺少 Resources/Texts/Hints.asset");
            return bar;
        }

        public void Show(HintId id)
        {
            string line = table != null ? table.Get(id) : null;
            if (string.IsNullOrEmpty(line))
            {
                Debug.LogError($"[Presentation] 提示 {id} 没有文案");
                return;
            }

            label.text = line;
            if (playing != null) StopCoroutine(playing);
            playing = StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            for (float t = group.alpha * Fade; t < Fade; t += Time.deltaTime)
            {
                group.alpha = t / Fade;
                yield return null;
            }

            group.alpha = 1f;
            yield return new WaitForSeconds(Hold);

            for (float t = Fade; t > 0f; t -= Time.deltaTime)
            {
                group.alpha = t / Fade;
                yield return null;
            }

            group.alpha = 0f;
            playing = null;
        }
    }
}
