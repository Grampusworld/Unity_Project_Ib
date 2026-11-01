using System.Collections;
using Ib.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace Ib.UI
{
    /// <summary>
    /// GDD §3.7 任务提示：新提示在屏幕上方淡入、停 3 秒，然后缩小滑到左上角常驻，直到下一条提示把它换掉（老大 2026-11-01）。
    /// 主菜单和结局时隐藏。用游戏时间，暂停时一起停住。
    /// </summary>
    internal sealed class HintBar : MonoBehaviour
    {
        private const float Fade = 0.6f;
        private const float Hold = 3f;
        private const float Slide = 0.8f;

        // 两个停靠位：上方居中（刚出现）和左上角（常驻）
        // 文字框宽度贴合文字，锚点和轴心一起从上方中点移到左上角，滑动过程不跳
        private static readonly Vector2 CentreAnchor = new Vector2(0.5f, 1f), CentrePos = new Vector2(0f, -70f);
        private static readonly Vector2 CornerAnchor = new Vector2(0f, 1f), CornerPos = new Vector2(36f, -28f);
        private const int CentreSize = 30, CornerSize = 22;
        private const float CornerAlpha = 0.85f;

        private CanvasGroup group;
        private Text label;
        private RectTransform rt;
        private HintTable table;
        private Coroutine playing;
        private bool hasHint;

        public static HintBar Create(Transform parent)
        {
            GameObject go = UIFactory.Empty(parent, "Hint");
            var bar = go.AddComponent<HintBar>();
            bar.group = go.AddComponent<CanvasGroup>();
            bar.group.alpha = 0f;
            bar.group.blocksRaycasts = false;

            bar.label = UIFactory.Label(go.transform, "Text", "", CentreSize, TextAnchor.MiddleLeft, new Color(0.92f, 0.9f, 0.85f));
            bar.rt = bar.label.rectTransform;
            bar.label.horizontalOverflow = HorizontalWrapMode.Overflow;
            bar.Place(0f);
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

            if (playing != null) StopCoroutine(playing);
            playing = StartCoroutine(Play(line));
        }

        private void Update()
        {
            // 回主菜单 / 结局时收起，重新开始游戏不带着上一局的提示
            if (hasHint && (GameFlow.State == GameState.MainMenu || GameFlow.State == GameState.Ending))
            {
                if (playing != null) StopCoroutine(playing);
                playing = null;
                hasHint = false;
                group.alpha = 0f;
            }
        }

        private IEnumerator Play(string line)
        {
            hasHint = true;

            // 角落里还挂着上一条：先淡掉
            for (float a = group.alpha; a > 0f; a -= Time.deltaTime / Fade)
            {
                group.alpha = a;
                yield return null;
            }

            label.text = line;
            Place(0f);
            for (float t = 0f; t < Fade; t += Time.deltaTime)
            {
                group.alpha = t / Fade;
                yield return null;
            }

            group.alpha = 1f;
            yield return new WaitForSeconds(Hold);

            for (float t = 0f; t < Slide; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / Slide);
                Place(k);
                group.alpha = Mathf.Lerp(1f, CornerAlpha, k);
                yield return null;
            }

            Place(1f);
            group.alpha = CornerAlpha;
            playing = null;
        }

        /// <summary>k = 0 上方居中，1 左上角。</summary>
        private void Place(float k)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.Lerp(CentreAnchor, CornerAnchor, k);
            rt.anchoredPosition = Vector2.Lerp(CentrePos, CornerPos, k);
            label.fontSize = Mathf.RoundToInt(Mathf.Lerp(CentreSize, CornerSize, k));
            rt.sizeDelta = new Vector2(label.preferredWidth + 8f, 60f);
        }
    }
}
