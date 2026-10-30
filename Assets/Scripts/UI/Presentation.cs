using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ib.UI
{
    public enum HintId { Explore = 1, AnswerPhone = 2, GetOut = 3, FindKey = 4, GetStaffCard = 5, OpenDrawer = 6 }

    /// <summary>
    /// UI 对外入口（接口见 Docs/Interfaces.md）。场景里不用摆任何东西：每次加载场景自动生成整套 UI。
    /// 订阅 sceneLoaded 的时机（BeforeSceneLoad）晚于 GameFlow（SubsystemRegistration），
    /// 所以 GameFlow 先复位，主菜单再在 Awake 里 RegisterMainMenu，顺序不能反。
    /// </summary>
    public static class Presentation
    {
        private static PresentationRoot root;
        private static readonly List<string> readTexts = new List<string>();

        /// <summary>纸面阅读器是否打开。阅读时不冻结时间。</summary>
        public static bool IsReading => root != null && root.Reader.IsOpen;

        /// <summary>屏幕上方淡入一行，3 秒后淡出。</summary>
        public static void ShowHint(HintId id)
        {
            if (Ready(nameof(ShowHint))) root.Hints.Show(id);
        }

        /// <summary>屏幕中央展开纸面。textId："T1"…"T16"、"T6_1"…"T6_4"。关闭时调 onClosed。</summary>
        public static void OpenText(string textId, Action onClosed = null)
        {
            if (Ready(nameof(OpenText))) root.Reader.Open(textId, onClosed);
        }

        /// <summary>全屏渐黑，seconds 秒后调 onDone。不受 timeScale 影响；新调用会打断旧的（旧回调不再调）。</summary>
        public static void FadeToBlack(float seconds, Action onDone = null)
        {
            if (Ready(nameof(FadeToBlack))) root.Fader.FadeTo(1f, seconds, onDone);
        }

        /// <summary>从黑幕淡回画面，seconds 秒后调 onDone。</summary>
        public static void FadeFromBlack(float seconds, Action onDone = null)
        {
            if (Ready(nameof(FadeFromBlack))) root.Fader.FadeTo(0f, seconds, onDone);
        }

        /// <summary>确认窗是否打开（正门开门确认）。</summary>
        public static bool IsConfirming => root != null && root.Confirm.IsOpen;

        /// <summary>纸面确认窗：一句问题 + 两个按钮，Esc 等于选否。调用方负责锁控制、放光标。</summary>
        public static void Confirm(string question, string yes, string no, Action onYes, Action onNo)
        {
            if (Ready(nameof(Confirm))) root.Confirm.Open(question, yes, no, onYes, onNo);
        }

        /// <summary>结局字幕：全黑底，标题 + 逐段淡入的正文，按任意键（或自动）结束后调 onDone。</summary>
        public static void ShowEnding(string title, string[] lines, Action onDone)
        {
            if (Ready(nameof(ShowEnding))) root.Ending.Show(title, lines, onDone);
        }

        /// <summary>本局读过的文本，按第一次读的顺序。暂停菜单 Texts 页用。</summary>
        internal static IReadOnlyList<string> ReadTexts => readTexts;

        internal static void MarkRead(string textId)
        {
            if (!readTexts.Contains(textId)) readTexts.Add(textId);
        }

        private static bool Ready(string caller)
        {
            if (root != null) return true;
            Debug.LogWarning($"[Presentation] {caller} 调用时 UI 还没生成（场景加载前？）");
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive) return;
            readTexts.Clear();
            root = PresentationRoot.Create();
        }
    }
}
