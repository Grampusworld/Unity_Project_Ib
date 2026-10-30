using System;
using System.Collections.Generic;
using Ib.Core;
using Ib.Player;
using Ib.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ib.Flow
{
    /// <summary>全局游戏状态，顺序见 GDD §3.6。</summary>
    public enum GameState
    {
        MainMenu,
        BlackRoom,
        PaintedGallery,
        BlackRoomReturn,
        AfterHours,
        PowerOut,
        AngelsActive,
        Ending,
    }

    /// <summary>
    /// 全局状态机入口（接口见 Docs/Interfaces.md）。
    /// 每次加载场景都从 MainMenu 开始；Quit to Menu 直接重新加载场景即可。
    /// 暂停冻结的 Time.timeScale 只在这里写（Game Over 仍由 GameOverScreen 自管，两者互斥）。
    /// </summary>
    public static class GameFlow
    {
        public static GameState State { get; private set; } = GameState.MainMenu;
        public static bool IsPaused { get; private set; }

        /// <summary>
        /// 准星、交互描边、按 E 只在正常游玩时有效。
        /// 主菜单、暂停、Game Over、读纸面都盖住画面中心，这几段不算瞄准。
        /// </summary>
        public static bool IsAiming =>
            State != GameState.MainMenu
            && !IsPaused
            && !GameOverScreen.IsShowing
            && !Presentation.IsReading;

        /// <summary>(from, to)</summary>
        public static event Action<GameState, GameState> StateChanged;

        /// <summary>第 10 步楼梯中段全馆断电，触发一次。</summary>
        public static event Action OnPowerOut;

        /// <summary>主角已拿到手电（F 能开关）。</summary>
        public static bool HasFlashlight { get; private set; }

        /// <summary>拿到手电时触发一次。晚订阅的先看 HasFlashlight。</summary>
        public static event Action FlashlightObtained;

        private static readonly HashSet<HintId> shownHints = new HashSet<HintId>();
        private static bool menuRegistered;
        private static float pausedTimeScale = 1f;

        /// <summary>主菜单 UI 在 Awake 里调用。没有主菜单的场景会跳过 MainMenu 直接开玩。</summary>
        public static void RegisterMainMenu() => menuRegistered = true;

        /// <summary>主菜单淡出结束后由 Presentation 调用：放开控制，进入黑屋。</summary>
        public static void StartNewGame()
        {
            if (State != GameState.MainMenu)
            {
                return;
            }

            SetInputLocked(false);
            BeginBlackRoom();
        }

        private static void BeginBlackRoom()
        {
            SetState(GameState.BlackRoom);
            Checkpoints.Reach(Checkpoints.BlackRoom);
        }


        /// <summary>暂停菜单开关由 Presentation 调用。主菜单、Game Over 期间忽略。</summary>
        public static void SetPaused(bool paused)
        {
            if (paused == IsPaused || State == GameState.MainMenu || GameOverScreen.IsShowing)
            {
                return;
            }

            IsPaused = paused;
            if (paused)
            {
                pausedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = pausedTimeScale;
            }

            SetInputLocked(paused);
        }

        /// <summary>任务提示每条每局只发一次（GDD §3.7）。</summary>
        public static void ShowHintOnce(HintId id)
        {
            if (shownHints.Add(id))
            {
                Presentation.ShowHint(id);
            }
        }

        /// <summary>给主角手电：厕所拾取物和调试菜单都走这里。</summary>
        public static void GiveFlashlight()
        {
            PlayerFlashlight flashlight = PlayerFlashlight.Instance;
            if (HasFlashlight || flashlight == null)
            {
                return;
            }

            flashlight.Give();
            HasFlashlight = true;
            FlashlightObtained?.Invoke();
        }

        public static void SetState(GameState next)
        {
            if (next == State)
            {
                return;
            }

            GameState from = State;
            State = next;
            WorldState.Apply(next);
            StateChanged?.Invoke(from, next);

            if (next == GameState.PowerOut)
            {
                OnPowerOut?.Invoke();
            }
        }

        internal static void SetInputLocked(bool locked)
        {
            FirstPersonController player = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
            if (player != null)
            {
                player.InputBlocked = locked;
            }

            // 光标放出来时视角自然停转（PlayerLook 只在锁定光标时转头）
            PlayerLook.SuspendPointerCapture = locked;
            PlayerLook.LockCursor(!locked);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            GameOverScreen.Continued -= Checkpoints.Restore;
            GameOverScreen.Continued += Checkpoints.Restore;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive)
            {
                return;
            }

            // 关掉"进入 Play 时重载域"也要干净：每次加载都复位
            State = GameState.MainMenu;
            IsPaused = false;
            Time.timeScale = 1f;
            menuRegistered = false;
            shownHints.Clear();
            HasFlashlight = false;
            Checkpoints.Reset();
            SceneTransitions.Reset();
            new GameObject("--- GameFlow ---").AddComponent<GameFlowBoot>();
        }

        /// <summary>等场景里所有 Awake 跑完（主菜单已登记）再决定是否锁在主菜单。</summary>
        private sealed class GameFlowBoot : MonoBehaviour
        {
            private void Start()
            {
                WorldState.Cache();
                if (menuRegistered)
                {
                    SetInputLocked(true);
                }
                else
                {
                    Debug.Log("[GameFlow] 场景里没有主菜单，跳过 MainMenu 直接开始。");
                    BeginBlackRoom();
                }

                Destroy(gameObject);
            }
        }
    }
}
