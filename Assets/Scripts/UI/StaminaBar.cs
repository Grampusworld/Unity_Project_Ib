using Ib.Flow;
using Ib.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Ib.UI
{
    /// <summary>
    /// 屏幕下方中间的体力条：平时不显示，冲刺消耗或体力没满时淡入，回满后淡出。
    /// 读 PlayerStamina（场景加载后再找，玩家可能比 UI 晚生成），不写它。
    /// </summary>
    internal sealed class StaminaBar : MonoBehaviour
    {
        private const float Width = 220f;
        private const float Height = 10f;
        private const float Bottom = 46f;
        private const float FadeSpeed = 4f; // 每秒淡入/淡出的 alpha 变化量

        private CanvasGroup group;
        private RectTransform fill;
        private PlayerStamina stamina;

        public static StaminaBar Create(Transform parent)
        {
            GameObject go = UIFactory.Empty(parent, "StaminaBar");
            var bar = go.AddComponent<StaminaBar>();

            bar.group = go.AddComponent<CanvasGroup>();
            bar.group.alpha = 0f;
            bar.group.blocksRaycasts = false;
            bar.group.interactable = false;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(Width, Height);
            rt.anchoredPosition = new Vector2(0f, Bottom);

            UIFactory.Panel(go.transform, "Back", new Color(0.08f, 0.07f, 0.06f, 0.55f));

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(go.transform, false);
            UIFactory.Stretch(fillGo);

            // 不用 Image.Type.Filled：没有 sprite 的 Image 会忽略 fillAmount，永远画满。改为按比例缩右锚点。
            var fillImage = fillGo.GetComponent<Image>();
            fillImage.color = new Color(0.86f, 0.74f, 0.45f, 0.95f);
            fillImage.raycastTarget = false;
            bar.fill = (RectTransform)fillGo.transform;

            return bar;
        }

        private void LateUpdate()
        {
            if (stamina == null)
            {
                stamina = FindAnyObjectByType<PlayerStamina>();
                if (stamina == null)
                {
                    group.alpha = 0f;
                    return;
                }
            }

            fill.anchorMax = new Vector2(Mathf.Clamp01(stamina.Ratio), 1f);

            bool show = GameFlow.IsAiming && (stamina.IsDraining || stamina.Ratio < 0.999f);
            float target = show ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, FadeSpeed * Time.deltaTime);
        }
    }
}
