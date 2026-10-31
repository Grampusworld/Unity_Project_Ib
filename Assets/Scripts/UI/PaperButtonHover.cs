using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ib.UI
{
    /// <summary>
    /// 纸面上的文字按钮的悬停效果：底色浮出一层淡墨、文字略放大、下方划出一道墨线。
    /// 用不受暂停影响的时间（确认窗、暂停菜单打开时游戏在暂停里）。
    /// UIFactory.Button 统一用 <see cref="Attach"/> 挂上（主菜单 / 暂停 / 设置都有）；ConfirmDialog 的纸面按钮再用 <see cref="Apply"/> 加墨色底。
    /// </summary>
    internal sealed class PaperButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float HoverScale = 1.06f;
        private const float Speed = 10f; // 1/秒，大约 0.1 秒到位

        private Text label;
        private Image underline;
        private bool hover;
        private float k;

        public static void Apply(Button button, Color ink)
        {
            Image bg = button.GetComponent<Image>();
            bg.color = Color.white; // 实际颜色全由下面的 ColorBlock 给
            button.targetGraphic = bg;
            ColorBlock c = button.colors;
            c.normalColor = new Color(ink.r, ink.g, ink.b, 0f);
            c.highlightedColor = new Color(ink.r, ink.g, ink.b, 0.12f);
            c.selectedColor = c.normalColor;
            c.pressedColor = new Color(ink.r, ink.g, ink.b, 0.24f);
            c.colorMultiplier = 1f;
            c.fadeDuration = 0.1f;
            button.colors = c;

            Attach(button, ink);
        }

        /// <summary>只加悬停动效（文字放大 + 墨线划出），不改按钮配色。重复调用只更新墨线颜色。</summary>
        public static void Attach(Button button, Color lineColor)
        {
            var hoverFx = button.GetComponent<PaperButtonHover>();
            if (hoverFx == null)
            {
                hoverFx = button.gameObject.AddComponent<PaperButtonHover>();
                hoverFx.label = button.GetComponentInChildren<Text>();

                var line = new GameObject("Underline", typeof(RectTransform), typeof(Image));
                line.transform.SetParent(button.transform, false);
                var rt = (RectTransform)line.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 8f);
                hoverFx.underline = line.GetComponent<Image>();
                hoverFx.underline.raycastTarget = false;
            }

            hoverFx.underline.color = lineColor;
            hoverFx.Refresh();
        }

        public void OnPointerEnter(PointerEventData eventData) => hover = true;

        public void OnPointerExit(PointerEventData eventData) => hover = false;

        private void OnDisable()
        {
            hover = false;
            k = 0f;
            Refresh();
        }

        private void Update()
        {
            float target = hover ? 1f : 0f;
            if (Mathf.Approximately(k, target))
            {
                return;
            }

            k = Mathf.MoveTowards(k, target, Time.unscaledDeltaTime * Speed);
            Refresh();
        }

        private void Refresh()
        {
            float e = Mathf.SmoothStep(0f, 1f, k);
            if (label != null)
            {
                label.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, HoverScale, e);
            }

            if (underline != null)
            {
                float width = label != null ? Mathf.Min(label.preferredWidth, label.rectTransform.rect.width) : 120f;
                RectTransform ur = underline.rectTransform;
                bool left = label != null && (label.alignment == TextAnchor.MiddleLeft || label.alignment == TextAnchor.UpperLeft || label.alignment == TextAnchor.LowerLeft);
                if (left)
                {
                    ur.anchorMin = ur.anchorMax = ur.pivot = new Vector2(0f, 0f);
                    ur.anchoredPosition = new Vector2(label.rectTransform.offsetMin.x, 8f);
                }
                else
                {
                    ur.anchorMin = ur.anchorMax = ur.pivot = new Vector2(0.5f, 0f);
                    ur.anchoredPosition = new Vector2(0f, 8f);
                }

                ur.sizeDelta = new Vector2(width * e, 2f);
                Color c = underline.color;
                c.a = 0.85f * e;
                underline.color = c;
            }
        }
    }
}
