using System.Collections;
using Ib.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace Ib.UI
{
    /// <summary>
    /// GDD §3.2：主菜单浮在黑屋画面上，没有单独的菜单场景，不设"继续游戏"。
    /// 点 Start 后菜单项逐个淡出（不是整块消失），淡完调 GameFlow.StartNewGame 交出控制权。
    /// </summary>
    internal sealed class MainMenu : MonoBehaviour
    {
        private const float ItemFade = 0.5f;
        private const float Stagger = 0.3f;

        private PresentationRoot root;
        private GameObject view;
        private CanvasGroup[] items;

        public bool IsOpen => view.activeSelf;

        public static MainMenu Create(Transform parent, PresentationRoot root)
        {
            GameObject go = UIFactory.Empty(parent, "MainMenu");
            var menu = go.AddComponent<MainMenu>();
            menu.root = root;
            menu.view = go;

            RectTransform column = UIFactory.Column(go.transform, "Items", new Vector2(0.5f, 0.28f), new Vector2(420f, 260f), 12f);
            Button start = UIFactory.Button(column, "Start", menu.OnStart);
            Button settings = UIFactory.Button(column, "Settings", () => root.Settings.Open(null));
            Button quit = UIFactory.Button(column, "Quit", Quit);

            menu.items = new[] { Group(start), Group(settings), Group(quit) };
            GameFlow.RegisterMainMenu();
            return menu;
        }

        private static CanvasGroup Group(Button b) => b.gameObject.AddComponent<CanvasGroup>();

        private void OnStart()
        {
            foreach (CanvasGroup g in items) g.interactable = false;
            StartCoroutine(FadeOutThenStart());
        }

        private IEnumerator FadeOutThenStart()
        {
            float total = ItemFade + Stagger * (items.Length - 1);
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    items[i].alpha = 1f - Mathf.Clamp01((t - i * Stagger) / ItemFade);
                }

                yield return null;
            }

            view.SetActive(false);
            GameSettings.ApplyAll();
            GameFlow.StartNewGame();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
