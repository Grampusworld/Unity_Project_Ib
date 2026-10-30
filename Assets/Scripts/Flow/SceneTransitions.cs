using Ib.Environment;
using Ib.Interaction;
using Ib.UI;
using UnityEngine;

namespace Ib.Flow
{
    /// <summary>
    /// GDD §3.6 第 6–14 步的状态切换，每一步都记检查点。触发方是 Interaction 里的组件：
    /// FamilyPortraitExit、EmptyFrameExit、PowerOutTrigger、FlashlightPickup、MonitorTerminal。
    /// </summary>
    public static class SceneTransitions
    {
        private const float FadeSeconds = 0.6f;

        public static bool Busy { get; private set; }

        /// <summary>第 6 步：画中馆看全家福 → 黑屏 → 回到黑屋，门的位置换成空画框。</summary>
        public static void ReturnToBlackRoom()
        {
            Fade(() =>
            {
                GameFlow.SetState(GameState.BlackRoomReturn);
                Checkpoints.ReachAndTeleport(Checkpoints.BlackRoom); // 传回黑屋会把黑屋重新启用、门解锁

                // 门还在，但锁死：只能走空画框
                MuseumEntrance entrance = Object.FindAnyObjectByType<MuseumEntrance>();
                DoubleDoor door = entrance != null ? entrance.GetComponent<DoubleDoor>() : null;
                if (door != null)
                {
                    door.SetLocked(true);
                }
            });
        }

        /// <summary>第 7 步：穿过空画框 → 黑屏 → 二楼家庭厅，闭馆。</summary>
        public static void EnterAfterHours()
        {
            Fade(() =>
            {
                GameFlow.SetState(GameState.AfterHours);
                Checkpoints.ReachAndTeleport(Checkpoints.AfterHours);
                if (WorldState.BlackRoom != null)
                {
                    WorldState.BlackRoom.SetActive(false);
                }
            });
        }

        /// <summary>第 10 步：楼梯中段断电（不黑屏）。OnPowerOut 同一刻发出，电磁锁门自己打开。</summary>
        public static void CutPower()
        {
            GameFlow.SetState(GameState.PowerOut);
            Checkpoints.Reach(Checkpoints.PowerOut);
        }

        /// <summary>第 12 步：拿起手电，状态仍是 PowerOut。人台和名画人物由 Threats 按 HasFlashlight 出动，天使不动。</summary>
        public static void TakeFlashlight()
        {
            GameFlow.GiveFlashlight();
            if (!GameFlow.HasFlashlight)
            {
                return; // 玩家身上没有手电组件
            }

            GameFlow.ShowHintOnce(HintId.GetOut);
            Checkpoints.Reach(Checkpoints.Flashlight);
        }

        /// <summary>第 14 步：值班室监控屏 → 天使出动（过场以后做，现在直接切）。</summary>
        public static void ActivateAngels()
        {
            GameFlow.SetState(GameState.AngelsActive);
            Checkpoints.Reach(Checkpoints.AngelsActive);
        }

        internal static void Reset() => Busy = false;

        private static void Fade(System.Action atBlack)
        {
            if (Busy)
            {
                return;
            }

            Busy = true;
            GameFlow.SetInputLocked(true);
            Presentation.FadeToBlack(FadeSeconds, () =>
            {
                atBlack();
                Presentation.FadeFromBlack(FadeSeconds, () =>
                {
                    Busy = false;
                    GameFlow.SetInputLocked(false);
                });
            });
        }
    }
}
