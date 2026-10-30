using System;
using UnityEngine;
using UnityEngine.UI;

namespace Ib.UI
{
    /// <summary>
    /// 正门开门确认（GDD §3.5 / §7.2）：说明牌同款纸面，一句问题 + 两个按钮。Esc 等于选否。
    /// 打开时由调用方锁控制、放出光标；回调里再决定放不放。
    /// </summary>
    internal sealed class ConfirmDialog : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.16f, 0.13f, 0.1f);

        private GameObject view;
        private Text question;
        private Text yesLabel;
        private Text noLabel;
        private Action onYes;
        private Action onNo;

        public bool IsOpen => view.activeSelf;

        public static ConfirmDialog Create(Transform parent)
        {
            GameObject go = UIFactory.Empty(parent, "Confirm");
            var dialog = go.AddComponent<ConfirmDialog>();

            dialog.view = UIFactory.Panel(go.transform, "Dim", new Color(0f, 0f, 0f, 0.6f));

            GameObject paper = UIFactory.Panel(dialog.view.transform, "Paper", new Color(0.92f, 0.89f, 0.8f));
            var rt = (RectTransform)paper.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(760f, 300f);
            paper.AddComponent<Shadow>().effectDistance = new Vector2(6f, -6f);

            dialog.question = UIFactory.Label(paper.transform, "Question", "", 30, TextAnchor.MiddleCenter, Ink);
            var qrt = dialog.question.rectTransform;
            qrt.anchorMin = qrt.anchorMax = new Vector2(0.5f, 0.5f);
            qrt.sizeDelta = new Vector2(660f, 120f);
            qrt.anchoredPosition = new Vector2(0f, 50f);

            Button yes = UIFactory.Button(paper.transform, "Open", () => dialog.Choose(true), 28);
            Button no = UIFactory.Button(paper.transform, "Don't open", () => dialog.Choose(false), 28);
            Place(yes, -160f);
            Place(no, 160f);
            dialog.yesLabel = yes.GetComponentInChildren<Text>();
            dialog.noLabel = no.GetComponentInChildren<Text>();
            dialog.yesLabel.color = dialog.noLabel.color = Ink;

            dialog.view.SetActive(false);
            return dialog;
        }

        private static void Place(Button b, float x)
        {
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(260f, 52f);
            rt.anchoredPosition = new Vector2(x, -70f);
        }

        public void Open(string text, string yes, string no, Action yesChosen, Action noChosen)
        {
            question.text = text;
            yesLabel.text = yes;
            noLabel.text = no;
            onYes = yesChosen;
            onNo = noChosen;
            view.SetActive(true);
            transform.SetAsLastSibling();
        }

        /// <summary>Esc：等于选否。</summary>
        public void Cancel()
        {
            if (IsOpen) Choose(false);
        }

        private void Choose(bool yes)
        {
            view.SetActive(false);
            Action cb = yes ? onYes : onNo;
            onYes = onNo = null;
            cb?.Invoke();
        }
    }
}
