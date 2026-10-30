using Ib.Flow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ib.UI
{
    /// <summary>
    /// Esc 暂停：Resume / Texts / Settings / Quit to Menu。冻结时间由 GameFlow.SetPaused 负责（主菜单、Game Over 时它会拒绝）。
    /// Texts 页列出本局读过的文本，点开用同一个阅读器重读（GDD §6.11.5）。
    /// </summary>
    internal sealed class PauseMenu : MonoBehaviour
    {
        private PresentationRoot root;
        private GameObject view;
        private GameObject mainPage;
        private GameObject textsPage;
        private RectTransform textsList;

        public static PauseMenu Create(Transform parent, PresentationRoot root)
        {
            GameObject go = UIFactory.Empty(parent, "PauseMenu");
            var pause = go.AddComponent<PauseMenu>();
            pause.root = root;
            pause.view = UIFactory.Panel(go.transform, "Dim", new Color(0f, 0f, 0f, 0.75f));

            pause.mainPage = UIFactory.Empty(pause.view.transform, "Main");
            UIFactory.Label(pause.mainPage.transform, "Title", "Paused", 44, TextAnchor.MiddleCenter, Color.white)
                .rectTransform.anchoredPosition = new Vector2(0f, 260f);
            RectTransform column = UIFactory.Column(pause.mainPage.transform, "Items", new Vector2(0.5f, 0.45f), new Vector2(420f, 340f), 12f);
            UIFactory.Button(column, "Resume", pause.Resume);
            UIFactory.Button(column, "Texts", pause.ShowTexts);
            UIFactory.Button(column, "Settings", () => root.Settings.Open(null));
            UIFactory.Button(column, "Quit to Menu", QuitToMenu);

            pause.textsPage = UIFactory.Empty(pause.view.transform, "Texts");
            UIFactory.Label(pause.textsPage.transform, "Title", "Texts", 40, TextAnchor.MiddleCenter, Color.white)
                .rectTransform.anchoredPosition = new Vector2(0f, 372f);
            pause.textsList = UIFactory.Column(pause.textsPage.transform, "List", new Vector2(0.5f, 0.5f), new Vector2(700f, 660f), 2f);
            RectTransform back = UIFactory.Column(pause.textsPage.transform, "Back", new Vector2(0.5f, 0.06f), new Vector2(420f, 60f), 0f);
            UIFactory.Button(back, "Back", pause.ShowMain);

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

        private void ShowTexts()
        {
            mainPage.SetActive(false);
            textsPage.SetActive(true);
            UIFactory.Clear(textsList);

            if (Presentation.ReadTexts.Count == 0)
            {
                Text empty = UIFactory.Label(textsList, "Empty", "Nothing read yet.", 26, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.7f));
                empty.rectTransform.sizeDelta = new Vector2(600f, 40f);
                return;
            }

            foreach (string id in Presentation.ReadTexts)
            {
                var doc = Resources.Load<TextDocument>("Texts/" + id);
                string label = doc != null && !string.IsNullOrEmpty(doc.title) ? doc.title : id;
                Button b = UIFactory.Button(textsList, label, () => root.Reader.Open(id, null), 22);
                ((RectTransform)b.transform).sizeDelta = new Vector2(700f, 32f);
            }
        }

        private static void QuitToMenu()
        {
            GameFlow.SetPaused(false);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
