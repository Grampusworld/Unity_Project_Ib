using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Ib.UI
{
    /// <summary>运行时拼 uGUI 的小工具。UI 全在代码里生成，不进 .unity / .prefab。</summary>
    internal static class UIFactory
    {
        private static Font font;
        private static readonly DefaultControls.Resources Res = new DefaultControls.Resources();

        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static RectTransform Stretch(GameObject go)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static GameObject Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch(go);
            go.GetComponent<Image>().color = color;
            return go;
        }

        public static GameObject Empty(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch(go);
            return go;
        }

        public static Text Label(Transform parent, string name, string text, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;
            t.rectTransform.sizeDelta = new Vector2(800f, size + 20f);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>只有文字的按钮：悬停时文字变暖色。</summary>
        public static Button Button(Transform parent, string label, UnityAction onClick, int size = 32)
        {
            GameObject go = DefaultControls.CreateButton(Res);
            go.name = label;
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(420f, size + 24f);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            var text = go.GetComponentInChildren<Text>();
            text.font = Font;
            text.text = label;
            text.fontSize = size;
            text.color = Color.white;

            var button = go.GetComponent<Button>();
            button.targetGraphic = text;
            ColorBlock c = button.colors;
            c.normalColor = new Color(0.85f, 0.85f, 0.85f);
            c.highlightedColor = new Color(1f, 0.88f, 0.62f);
            c.selectedColor = c.normalColor;
            c.pressedColor = new Color(0.6f, 0.55f, 0.45f);
            c.fadeDuration = 0.08f;
            button.colors = c;
            button.onClick.AddListener(onClick);
            return button;
        }

        public static Slider Slider(Transform parent, float min, float max, float value, UnityAction<float> onChange)
        {
            GameObject go = DefaultControls.CreateSlider(Res);
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(420f, 24f);
            var s = go.GetComponent<Slider>();
            s.minValue = min;
            s.maxValue = max;
            s.value = value;
            s.onValueChanged.AddListener(onChange);
            return s;
        }

        /// <summary>竖排容器：子物体按各自 sizeDelta 排，不被拉伸。</summary>
        public static RectTransform Column(Transform parent, string name, Vector2 anchor, Vector2 size, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            return rt;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject);
        }
    }
}
