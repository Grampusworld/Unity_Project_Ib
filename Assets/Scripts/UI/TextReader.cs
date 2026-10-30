using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ib.UI
{
    /// <summary>
    /// GDD §6.11.5：拾取后屏幕中央展开纸面。E 或 Esc 关闭。
    /// 打开和关闭的同一帧不响应 E，避免按 E 拾取时立刻关掉、关掉时又被拾取物重新打开。
    /// </summary>
    internal sealed class TextReader : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.16f, 0.13f, 0.1f);

        private GameObject view;
        private Text title;
        private Text body;
        private Action onClosed;
        private int openedFrame = -1;
        private int closedFrame = -1;

        public bool IsOpen => view.activeSelf;

        public static TextReader Create(Transform parent)
        {
            GameObject go = UIFactory.Empty(parent, "Reader");
            var reader = go.AddComponent<TextReader>();

            reader.view = UIFactory.Panel(go.transform, "Dim", new Color(0f, 0f, 0f, 0.6f));

            GameObject paper = UIFactory.Panel(reader.view.transform, "Paper", new Color(0.92f, 0.89f, 0.8f));
            var rt = (RectTransform)paper.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(760f, 780f);
            paper.AddComponent<Shadow>().effectDistance = new Vector2(6f, -6f);

            reader.title = UIFactory.Label(paper.transform, "Title", "", 34, TextAnchor.UpperCenter, Ink);
            Inset(reader.title.rectTransform, 50f, 50f, 50f, 670f);

            reader.body = UIFactory.Label(paper.transform, "Body", "", 28, TextAnchor.UpperLeft, Ink);
            reader.body.lineSpacing = 1.15f;
            Inset(reader.body.rectTransform, 60f, 60f, 120f, 70f);

            Text hint = UIFactory.Label(paper.transform, "CloseHint", "[E] / [Esc]  Close", 20, TextAnchor.LowerRight, new Color(0.4f, 0.36f, 0.3f));
            Inset(hint.rectTransform, 36f, 36f, 725f, 20f);

            reader.view.SetActive(false);
            return reader;
        }

        private static void Inset(RectTransform rt, float left, float right, float top, float bottom)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public void Open(string textId, Action closed)
        {
            if (Time.frameCount == closedFrame) return;

            var doc = Resources.Load<TextDocument>("Texts/" + textId);
            if (doc == null)
            {
                Debug.LogError($"[Presentation] 找不到文本 Resources/Texts/{textId}.asset");
                return;
            }

            if (IsOpen) Close();

            title.text = doc.title;
            body.text = doc.body;
            FontStyle style = doc.hand == TextDocument.Hand.Print ? FontStyle.Normal : FontStyle.Italic;
            title.fontStyle = style;
            body.fontStyle = style;

            onClosed = closed;
            openedFrame = Time.frameCount;
            view.SetActive(true);
            view.transform.SetAsLastSibling();
            transform.SetAsLastSibling();
            Presentation.MarkRead(textId);
        }

        public void Close()
        {
            if (!IsOpen) return;
            view.SetActive(false);
            closedFrame = Time.frameCount;
            Action cb = onClosed;
            onClosed = null;
            cb?.Invoke();
        }

        private void Update()
        {
            if (!IsOpen || Time.frameCount == openedFrame) return;
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame) Close();
        }
    }
}
