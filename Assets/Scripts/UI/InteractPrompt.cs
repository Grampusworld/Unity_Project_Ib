using Ib.Flow;
using Ib.Interaction;
using UnityEngine;

namespace Ib.UI
{
    /// <summary>
    /// 屏幕下方中间的交互提示「E  Interact」，文案来自 Resources/Texts/Interact.asset（ControlHintTable 取第一行），
    /// 样式复用右下角 ControlHints 的键帽行。
    /// 显隐由 PlayerInteractor.TargetChanged 驱动（不轮询目标）；IsAiming 为 false（菜单、暂停、阅读器、确认窗）时整体隐藏。
    /// </summary>
    internal sealed class InteractPrompt : MonoBehaviour
    {
        private const float Width = 260f;
        private const float Bottom = 120f;

        private CanvasGroup group;
        private bool hasTarget;

        public static InteractPrompt Create(Transform parent)
        {
            var go = new GameObject("InteractPrompt", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, Bottom);
            rt.sizeDelta = new Vector2(Width, 34f);

            var prompt = go.AddComponent<InteractPrompt>();
            prompt.group = go.GetComponent<CanvasGroup>();
            prompt.group.blocksRaycasts = false;
            prompt.group.interactable = false;
            prompt.group.alpha = 0f;

            var table = Resources.Load<ControlHintTable>("Texts/Interact");
            if (table == null || table.lines == null || table.lines.Length == 0)
            {
                Debug.LogError("[Presentation] 缺少 Resources/Texts/Interact.asset");
                return prompt;
            }

            ControlHintTable.Line line = table.lines[0];
            GameObject row = ControlHints.Row(go.transform, line.key, line.action);
            // 260 宽的行里「E 键帽 + Interact」视觉上正好落在中间，直接居中
            var rowRt = (RectTransform)row.transform;
            rowRt.anchorMin = rowRt.anchorMax = rowRt.pivot = new Vector2(0.5f, 0.5f);
            rowRt.anchoredPosition = Vector2.zero;

            PlayerInteractor.TargetChanged += prompt.OnTargetChanged;
            return prompt;
        }

        private void OnDestroy() => PlayerInteractor.TargetChanged -= OnTargetChanged;

        private void OnTargetChanged(IInteractable target) => hasTarget = target != null;

        private void LateUpdate()
        {
            float alpha = hasTarget && GameFlow.IsAiming ? 1f : 0f;
            if (group.alpha != alpha) group.alpha = alpha;
        }
    }
}
