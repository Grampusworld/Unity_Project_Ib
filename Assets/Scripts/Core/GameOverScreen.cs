using System;
using Ib.Player;
using UnityEngine;

namespace Ib.Core
{
    /// <summary>
    /// 被馆藏抓住后的结束画面：全屏黑底 + GAME OVER + 下方一个 Continue 按钮。
    /// 显示期间暂停世界（timeScale = 0）、屏蔽玩家输入、放出光标；
    /// 点 Continue → 玩家在上一个检查点重生（PlayerSpawner.Respawn），恢复游戏。
    /// 场景里不用预先摆：第一次 Show() 时自己生成。UI 和 DebugMenu 一样先用 OnGUI 顶着。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameOverScreen : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float fadeSeconds = 0.6f;

        public static bool IsShowing => instance != null && instance.showing;

        /// <summary>点 Continue、玩家重生之后触发（天使等借此复位）。</summary>
        public static event Action Continued;

        private static GameOverScreen instance;

        private bool showing;
        private float shownAt;
        private float savedTimeScale = 1f;
        private FirstPersonController player;

        private Texture2D black;
        private GUIStyle titleStyle;
        private GUIStyle buttonStyle;
        private int styleHeight = -1;

        public static void Show()
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<GameOverScreen>();
                if (instance == null)
                {
                    instance = new GameObject("--- GameOver ---").AddComponent<GameOverScreen>();
                }
            }

            instance.Open();
        }

        private void Open()
        {
            if (showing)
            {
                return;
            }

            showing = true;
            shownAt = Time.unscaledTime;
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            player = FindAnyObjectByType<FirstPersonController>();
            if (player != null)
            {
                player.InputBlocked = true;
            }

            PlayerLook.SuspendPointerCapture = true;
            PlayerLook.LockCursor(false);
        }

        private void Continue()
        {
            showing = false;
            Time.timeScale = savedTimeScale;

            PlayerSpawner spawner = FindAnyObjectByType<PlayerSpawner>();
            if (spawner != null)
            {
                spawner.Respawn();
            }

            if (player != null)
            {
                player.InputBlocked = false;
            }

            PlayerLook.SuspendPointerCapture = false;
            PlayerLook.LockCursor(true);

            Continued?.Invoke();
        }

        private void OnGUI()
        {
            if (!showing)
            {
                return;
            }

            EnsureStyles();

            // 先渐黑，再出字和按钮
            float t = fadeSeconds <= 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - shownAt) / fadeSeconds);
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, t);
            GUI.depth = -100;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), black, ScaleMode.StretchToFill);

            if (t >= 1f)
            {
                float s = Screen.height / 1080f;
                GUI.Label(new Rect(0f, Screen.height * 0.36f, Screen.width, 140f * s), "GAME OVER", titleStyle);

                Rect button = new Rect((Screen.width - 260f * s) * 0.5f, Screen.height * 0.58f, 260f * s, 64f * s);
                if (GUI.Button(button, "Continue", buttonStyle))
                {
                    Continue();
                }
            }

            GUI.color = old;
        }

        private void EnsureStyles()
        {
            if (black == null)
            {
                black = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                black.SetPixel(0, 0, Color.black);
                black.Apply();
            }

            if (styleHeight == Screen.height)
            {
                return;
            }

            styleHeight = Screen.height;
            float s = Mathf.Clamp(Screen.height / 1080f, 0.6f, 3f);

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(96f * s),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            titleStyle.normal.textColor = new Color(0.55f, 0.03f, 0.03f);

            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(28f * s) };
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
                if (showing)
                {
                    Time.timeScale = savedTimeScale;
                }
            }
        }
    }
}
