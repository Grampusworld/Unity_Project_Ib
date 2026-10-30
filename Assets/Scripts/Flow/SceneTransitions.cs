using Ib.Environment;
using Ib.Interaction;
using Ib.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ib.Flow
{
    /// <summary>
    /// GDD §3.6 第 6–18 步的状态切换和任务进度，关键节点记检查点。触发方是 Interaction 里的组件：
    /// FamilyPortraitExit、StateTrigger、FlashlightPickup、MonitorTerminal、PhoneRing、MainDoorLock、
    /// StaffCardCabinet、FrontDeskDrawer。结局是灰盒版：锁控制 + 渐黑 + 字幕，完了重新加载场景回主菜单。
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

        /// <summary>第 8 步：电话开始响（PhoneRing 在出画几秒后调用）。</summary>
        public static void PhoneRinging()
        {
            GameFlow.ShowHintOnce(HintId.AnswerPhone);
        }

        /// <summary>第 8 步：接起电话。听筒里只有底噪，听完（HangUp）才放开控制。</summary>
        public static void AnswerPhone()
        {
            GameFlow.PhoneAnswered = true;
            Busy = true;
            GameFlow.SetInputLocked(true);
        }

        public static void HangUp()
        {
            Busy = false;
            GameFlow.SetInputLocked(false);
        }

        /// <summary>第 13 步：推了锁着的正门。「找到打开主门的钥匙」只在拿到手电（逃离开始）以后给（§3.7 顺序固定）。</summary>
        public static void TriedLockedMainDoor()
        {
            if (GameFlow.HasFlashlight)
            {
                GameFlow.ShowHintOnce(HintId.FindKey);
            }
        }

        /// <summary>第 15 步：配电间保安柜里拿到工作卡。</summary>
        public static void TakeStaffCard()
        {
            GameFlow.HasStaffCard = true;
            GameFlow.ShowHintOnce(HintId.OpenDrawer);
            Checkpoints.Reach(Checkpoints.TaskB);
        }

        /// <summary>第 16 步：前台抽屉里拿到钥匙。之后不再给任何提示。</summary>
        public static void TakeKey()
        {
            GameFlow.HasKey = true;
            Checkpoints.Reach(Checkpoints.TaskC);
        }

        /// <summary>第 17 步：钥匙已插，再按一次 → 确认窗。</summary>
        public static void AskOpenMainDoor()
        {
            GameFlow.SetInputLocked(true);
            Presentation.Confirm("Are you sure you want to open the door?", "Open", "Don't open",
                BadEnding,
                () => GameFlow.SetInputLocked(false));
        }

        /// <summary>第 18 步 · 坏结局：确认开门。</summary>
        public static void BadEnding()
        {
            Ending("Temporary Storage 07", new[]
            {
                "Behind the door there is no street. Only crates, stacked to the ceiling.",
                "The number on the door matches the number on the key.",
                "The collection catches up. He is catalogued, and placed with the rest.",
                "Friday. Lily counts the crates. She stops in front of the new piece for a while, then walks on.",
                "The storage light goes out.",
            });
        }

        /// <summary>第 18 步 · 真结局：钥匙留在锁孔里，回到全家福前，走进那块空白。</summary>
        public static void TrueEnding()
        {
            Ending("Family Portrait", new[]
            {
                "The key is still in the lock. The door could open at any time.",
                "He steps into the blank. This time, it lets him in.",
                "The portrait has four people now.",
                "Across the room, the blurred man in The Gallery slowly turns his head.",
                "Friday morning. Lily touches the canvas. Her fingers stop.",
                "It is wet.",
            });
        }

        private static void Ending(string title, string[] lines)
        {
            if (Busy)
            {
                return;
            }

            // 先切到 Ending：馆藏全部停下，渐黑途中不会被抓
            Busy = true;
            GameFlow.SetInputLocked(true);
            GameFlow.SetState(GameState.Ending);
            Presentation.FadeToBlack(FadeSeconds * 3f, () =>
                Presentation.ShowEnding(title, lines, () =>
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex)));
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
