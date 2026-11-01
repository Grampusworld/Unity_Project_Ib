using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Ib.UI
{
    /// <summary>整套 UI 的根：一块 Overlay Canvas，子层从下到上是提示、按键提示、主菜单、暂停、设置、阅读器。Esc 统一在这里分发。</summary>
    internal sealed class PresentationRoot : MonoBehaviour
    {
        /// <summary>
        /// 整套 UI 的统一放大倍数：参考分辨率 = 1920×1080 ÷ UIScale。
        /// 所有尺寸都按缩小后的参考分辨率（约 1477×831）排，不逐个改元素。
        /// </summary>
        public const float UIScale = 1.3f;

        public HintBar Hints { get; private set; }
        public CaptionBar Caption { get; private set; }
        public TextReader Reader { get; private set; }
        public ScreenFader Fader { get; private set; }
        public MainMenu Menu { get; private set; }
        public PauseMenu Pause { get; private set; }
        public SettingsPanel Settings { get; private set; }
        public ConfirmDialog Confirm { get; private set; }
        public EndingCard Ending { get; private set; }

        public static PresentationRoot Create()
        {
            var go = new GameObject("--- Presentation UI ---", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f) / UIScale;
            scaler.matchWidthOrHeight = 0.5f;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            }

            return go.AddComponent<PresentationRoot>();
        }

        private void Awake()
        {
            Hints = HintBar.Create(transform);
            Caption = CaptionBar.Create(transform);
            ControlHints.Create(transform);
            InteractPrompt.Create(transform);
            Menu = MainMenu.Create(transform, this);
            Pause = PauseMenu.Create(transform, this);
            Settings = SettingsPanel.Create(transform);
            Reader = TextReader.Create(transform);
            Confirm = ConfirmDialog.Create(transform);
            Crosshair.Create(transform);
            StaminaBar.Create(transform);
            Fader = ScreenFader.Create(transform); // 盖在所有 UI 之上
            Ending = EndingCard.Create(transform); // 结局字幕在黑幕之上
        }

        private void Start() => GameSettings.ApplyAll();

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

            if (Ending.IsShowing) { }
            else if (Confirm.IsOpen) Confirm.Cancel();
            else if (Reader.IsOpen) Reader.Close();
            else if (Settings.IsOpen) Settings.Close();
            else if (Menu.IsOpen) { }
            else Pause.EscapePressed();
        }
    }
}
