using Ib.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace Ib.UI
{
    /// <summary>
    /// 右下角按键提示，每行「键帽 + 英文说明」，文案来自 Resources/Texts/Controls.asset。
    /// 需要手电的行等 GameFlow.FlashlightObtained 再显示，不轮询。
    /// 和准星一样跟着 GameFlow.IsAiming：主菜单、暂停、阅读、Game Over 时隐藏。
    /// </summary>
    internal sealed class ControlHints : MonoBehaviour
    {
        private const float RowHeight = 34f;
        private const float KeyColumn = 92f;
        private const int FontSize = 20;
        private static readonly Color TextColor = new Color(0.88f, 0.86f, 0.82f);

        private CanvasGroup group;
        private GameObject[] flashlightRows = new GameObject[0];

        public static ControlHints Create(Transform parent)
        {
            var go = new GameObject("ControlHints", typeof(RectTransform), typeof(CanvasGroup), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-32f, 32f);
            rt.sizeDelta = new Vector2(260f, 3f * RowHeight + 16f);

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.spacing = 8f;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var hints = go.AddComponent<ControlHints>();
            hints.group = go.GetComponent<CanvasGroup>();
            hints.group.blocksRaycasts = false;
            hints.group.interactable = false;
            hints.group.alpha = 0f;

            var table = Resources.Load<ControlHintTable>("Texts/Controls");
            if (table == null || table.lines == null)
            {
                Debug.LogError("[Presentation] 缺少 Resources/Texts/Controls.asset");
                return hints;
            }

            var gated = new System.Collections.Generic.List<GameObject>();
            foreach (ControlHintTable.Line line in table.lines)
            {
                GameObject row = Row(go.transform, line.key, line.action);
                if (line.needsFlashlight)
                {
                    row.SetActive(GameFlow.HasFlashlight);
                    gated.Add(row);
                }
            }

            hints.flashlightRows = gated.ToArray();
            GameFlow.FlashlightObtained += hints.OnFlashlightObtained;
            return hints;
        }

        private void OnDestroy() => GameFlow.FlashlightObtained -= OnFlashlightObtained;

        private void OnFlashlightObtained()
        {
            foreach (GameObject row in flashlightRows) row.SetActive(true);
        }

        private void LateUpdate()
        {
            float alpha = GameFlow.IsAiming ? 1f : 0f;
            if (group.alpha != alpha) group.alpha = alpha;
        }

        internal static GameObject Row(Transform parent, string key, string action)
        {
            var row = new GameObject(action, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            ((RectTransform)row.transform).sizeDelta = new Vector2(260f, RowHeight);

            // 键帽：深色底 + 浅色描边，宽度随字数
            float keyWidth = Mathf.Max(RowHeight, 18f + key.Length * 12f);
            var cap = new GameObject("Key", typeof(RectTransform), typeof(Image));
            cap.transform.SetParent(row.transform, false);
            var capRt = (RectTransform)cap.transform;
            capRt.anchorMin = capRt.anchorMax = capRt.pivot = new Vector2(0f, 0.5f);
            capRt.anchoredPosition = new Vector2(KeyColumn - keyWidth, 0f);
            capRt.sizeDelta = new Vector2(keyWidth, RowHeight);
            var capImage = cap.GetComponent<Image>();
            capImage.color = new Color(0.05f, 0.05f, 0.05f, 0.6f);
            capImage.raycastTarget = false;
            var outline = cap.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.83f, 0.78f, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            Text keyText = UIFactory.Label(cap.transform, "Label", key, FontSize - 2, TextAnchor.MiddleCenter, TextColor);
            UIFactory.Stretch(keyText.gameObject);
            keyText.horizontalOverflow = HorizontalWrapMode.Overflow;

            Text actionText = UIFactory.Label(row.transform, "Action", action, FontSize, TextAnchor.MiddleLeft, TextColor);
            var art = actionText.rectTransform;
            art.anchorMin = art.anchorMax = art.pivot = new Vector2(0f, 0.5f);
            art.anchoredPosition = new Vector2(KeyColumn + 14f, 0f);
            art.sizeDelta = new Vector2(260f - KeyColumn - 14f, RowHeight);
            actionText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            return row;
        }
    }
}
