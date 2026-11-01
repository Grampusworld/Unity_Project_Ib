using Ib.Flow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ib.UI
{
    /// <summary>
    /// Esc 暂停：Resume / Texts / Settings / Quit to Menu。冻结时间由 GameFlow.SetPaused 负责（主菜单、Game Over 时它会拒绝）。
    /// Texts 页：左侧约 1/4 列出本局读过的文本，右侧约 3/4 的纸面显示选中那份的全文（GDD §6.11.5）。
    /// </summary>
    internal sealed class PauseMenu : MonoBehaviour
    {
        private GameObject view;
        private GameObject mainPage;
        private GameObject textsPage;
        private RectTransform textsList;
        private Text docTitle;
        private Text textsHeading;
        private Text docBody;
        private ScrollRect docScroll;
        private Button selectedEntry;

        private static readonly Color Ink = new Color(0.16f, 0.13f, 0.1f);
        private static readonly Color EntryNormal = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color EntrySelected = new Color(1f, 0.88f, 0.62f);

        public static PauseMenu Create(Transform parent, PresentationRoot root)
        {
            GameObject go = UIFactory.Empty(parent, "PauseMenu");
            var pause = go.AddComponent<PauseMenu>();
            pause.view = UIFactory.Panel(go.transform, "Dim", new Color(0f, 0f, 0f, 0.75f));

            pause.mainPage = UIFactory.Empty(pause.view.transform, "Main");
            UIFactory.Label(pause.mainPage.transform, "Title", "Paused", 44, TextAnchor.MiddleCenter, Color.white)
                .rectTransform.anchoredPosition = new Vector2(0f, 260f);
            RectTransform column = UIFactory.Column(pause.mainPage.transform, "Items", new Vector2(0.5f, 0.45f), new Vector2(420f, 340f), 12f);
            UIFactory.Button(column, "Resume", pause.Resume);
            UIFactory.Button(column, "Texts", pause.ShowTexts);
            UIFactory.Button(column, "Settings", () => root.Settings.Open(null));
            UIFactory.Button(column, "Quit to Menu", QuitToMenu);

            pause.BuildTextsPage();

            pause.view.SetActive(false);
            return pause;
        }

        public bool IsOpen => view.activeSelf;

        /// <summary>Esc：Texts 页先回主页，主页关掉暂停，没开就打开。</summary>
        public void EscapePressed()
        {
            if (!IsOpen) Open();
            else if (textsPage.activeSelf) ShowMain();
            else Resume();
        }

        private void Open()
        {
            GameFlow.SetPaused(true);
            if (!GameFlow.IsPaused) return; // 主菜单或 Game Over 期间不暂停
            view.SetActive(true);
            ShowMain();
        }

        private void Resume()
        {
            view.SetActive(false);
            GameFlow.SetPaused(false);
        }

        private void ShowMain()
        {
            mainPage.SetActive(true);
            textsPage.SetActive(false);
        }

        private void BuildTextsPage()
        {
            textsPage = UIFactory.Empty(view.transform, "Texts");
            Text heading = UIFactory.Label(textsPage.transform, "Title", "Texts", 40, TextAnchor.MiddleCenter, Color.white);
            textsHeading = heading;
            Place(heading.rectTransform, new Vector2(0f, 0.9f), new Vector2(1f, 0.97f));

            // 左 1/4：已收集列表（可滚动）
            GameObject listPanel = UIFactory.Panel(textsPage.transform, "ListPanel", new Color(1f, 1f, 1f, 0.04f));
            Place((RectTransform)listPanel.transform, new Vector2(0.04f, 0.13f), new Vector2(0.26f, 0.88f));
            textsList = Scroll(listPanel.transform, "List", 16f, out _);
            var layout = textsList.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = 4f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            textsList.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 右 3/4：纸面，显示选中文本
            GameObject paper = UIFactory.Panel(textsPage.transform, "Paper", new Color(0.92f, 0.89f, 0.8f));
            Place((RectTransform)paper.transform, new Vector2(0.28f, 0.13f), new Vector2(0.96f, 0.88f));
            paper.AddComponent<Shadow>().effectDistance = new Vector2(6f, -6f);

            docTitle = UIFactory.Label(paper.transform, "DocTitle", "", 34, TextAnchor.UpperCenter, Ink);
            var tr = docTitle.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(50f, -90f);
            tr.offsetMax = new Vector2(-50f, -36f);

            GameObject bodyArea = UIFactory.Empty(paper.transform, "BodyArea");
            var br = (RectTransform)bodyArea.transform;
            br.offsetMin = new Vector2(60f, 40f);
            br.offsetMax = new Vector2(-60f, -110f);
            RectTransform content = Scroll(bodyArea.transform, "Body", 30f, out docScroll);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            docBody = content.gameObject.AddComponent<Text>();
            docBody.font = UIFactory.Font;
            docBody.fontSize = 28;
            docBody.lineSpacing = 1.15f;
            docBody.color = Ink;
            docBody.alignment = TextAnchor.UpperLeft;
            docBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            docBody.verticalOverflow = VerticalWrapMode.Overflow;
            docBody.raycastTarget = true; // 滚轮要落在内容上才能滚

            RectTransform back = UIFactory.Column(textsPage.transform, "Back", new Vector2(0.5f, 0.06f), new Vector2(420f, 60f), 0f);
            UIFactory.Button(back, "Back", ShowMain);
        }

        private static void Place(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>parent 内铺满的竖向滚动区，返回顶部对齐、宽度随视口的 content。</summary>
        private static RectTransform Scroll(Transform parent, string name, float wheel, out ScrollRect scroll)
        {
            GameObject viewport = UIFactory.Empty(parent, name + "Viewport");
            viewport.AddComponent<RectMask2D>();
            GameObject content = UIFactory.Empty(viewport.transform, name);
            var rt = (RectTransform)content.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            scroll = viewport.AddComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = rt;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = wheel;
            return rt;
        }

        private void ShowTexts()
        {
            mainPage.SetActive(false);
            textsPage.SetActive(true);
            UIFactory.Clear(textsList);
            selectedEntry = null;
            ShowDocument(null);
            textsHeading.text = $"Texts  {Presentation.ReadTexts.Count} / {TextCatalog.Count}";

            if (Presentation.ReadTexts.Count == 0)
            {
                Text empty = UIFactory.Label(textsList, "Empty", "Nothing read yet.", 24, TextAnchor.MiddleLeft, new Color(0.7f, 0.7f, 0.7f));
                empty.rectTransform.sizeDelta = new Vector2(300f, 40f);
                return;
            }

            Button first = null;
            // 按收集编号排（不在编号表里的排最后），每条前面带编号
            var ids = new System.Collections.Generic.List<string>(Presentation.ReadTexts);
            ids.Sort((a, b) => SortKey(a).CompareTo(SortKey(b)));
            foreach (string id in ids)
            {
                var doc = Resources.Load<TextDocument>("Texts/" + id);
                string label = doc != null && !string.IsNullOrEmpty(doc.title) ? doc.title : id;
                int n = TextCatalog.Number(id);
                if (n > 0)
                {
                    label = $"{n:00}  {label}";
                }

                Button b = null;
                b = UIFactory.Button(textsList, label, () => Select(b, doc), 22);
                ((RectTransform)b.transform).sizeDelta = new Vector2(300f, 40f);
                Text t = b.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                t.rectTransform.offsetMin = new Vector2(10f, 0f);
                if (first == null)
                {
                    first = b;
                    Select(b, doc);
                }
            }
        }

        private static int SortKey(string id)
        {
            int n = TextCatalog.Number(id);
            return n > 0 ? n : int.MaxValue;
        }

        private void Select(Button entry, TextDocument doc)
        {
            if (selectedEntry != null) SetEntryColor(selectedEntry, EntryNormal);
            selectedEntry = entry;
            SetEntryColor(entry, EntrySelected);
            ShowDocument(doc);
        }

        private static void SetEntryColor(Button b, Color normal)
        {
            ColorBlock c = b.colors;
            c.normalColor = normal;
            c.selectedColor = normal;
            b.colors = c;
        }

        private void ShowDocument(TextDocument doc)
        {
            docTitle.text = doc != null ? doc.title : "";
            docBody.text = doc != null ? doc.body : "";
            FontStyle style = doc != null && doc.hand != TextDocument.Hand.Print ? FontStyle.Italic : FontStyle.Normal;
            docTitle.fontStyle = style;
            docBody.fontStyle = style;
            docScroll.verticalNormalizedPosition = 1f;
        }

        private static void QuitToMenu()
        {
            GameFlow.SetPaused(false);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
