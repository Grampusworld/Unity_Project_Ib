using Ib.Core;
using Ib.Environment;
using Ib.Interaction;
using Ib.Player;
using Ib.Threats;
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

        /// <summary>
        /// 第 6 步：画中馆看全家福 → 黑屏 → 回到黑屋，门的位置换成空画框。
        /// fadeOutSeconds：渐黑用时（全家福推镜头用慢的）；atBlack：全黑后、传送前先调用（推镜头在这里把相机放回原位）。
        /// </summary>
        public static void ReturnToBlackRoom(float fadeOutSeconds = FadeSeconds, System.Action atBlack = null)
        {
            Fade(fadeOutSeconds, () =>
            {
                atBlack?.Invoke();
                ArriveInBlackRoom();
            });
        }

        /// <summary>
        /// 第 6 步的无缝版（FamilyPortraitExit 用）：调用方自己锁控制、遮住画面（只剩那幅画），这里只切状态、
        /// 传回黑屋出生点、锁门。调用方随后把玩家摆到与草稿相对的位置，再把画面放开。
        /// </summary>
        public static void ReturnToBlackRoomSeamless() => ArriveInBlackRoom();

        private static void ArriveInBlackRoom()
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
        }

        /// <summary>
        /// 第 6b / 7 步：穿过空画框 → 二楼全家福厅，闭馆。
        /// 有门洞（PaintingPortal）时：冷白光一闪，玩家按门洞的变换直接落到北墙画框前（朝向跟着转），
        /// 黑屋先留着（北墙画框还是门洞，等玩家背过去才换成《美术馆》并停用黑屋，见 TheGalleryFrame）。
        /// 没有门洞时退回原来的黑屏传送。
        /// </summary>
        public static void EnterAfterHours(PaintingPortal through = null)
        {
            if (through != null && through.Partner != null)
            {
                CarryThrough(through);
                return;
            }

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

        private static void CarryThrough(PaintingPortal through)
        {
            if (Busy)
            {
                return;
            }

            FirstPersonController player = Object.FindAnyObjectByType<FirstPersonController>();
            if (player == null)
            {
                return;
            }

            Busy = true;
            GameFlow.SetInputLocked(true);
            through.Carry(player.transform.position, player.transform.eulerAngles.y, 0.7f, out Vector3 feet, out float yaw);

            ScreenVeil.Flash(FlashColor, 1f);
            GameFlow.SetState(GameState.AfterHours);
            Checkpoints.Reach(Checkpoints.AfterHours);
            player.Teleport(feet, Quaternion.Euler(0f, yaw, 0f));
            PlayerLook look = player.GetComponent<PlayerLook>();
            if (look != null)
            {
                look.ResetLook(yaw);
            }

            player.StartCoroutine(FadeFlash());
        }

        private static readonly Color FlashColor = new Color(0.82f, 0.9f, 1f);

        private static System.Collections.IEnumerator FadeFlash()
        {
            const float seconds = 0.7f;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                ScreenVeil.Flash(FlashColor, 1f - t / seconds);
                yield return null;
            }

            ScreenVeil.Flash(FlashColor, 0f);
            Busy = false;
            GameFlow.SetInputLocked(false);
        }

        /// <summary>第 10 步：楼梯中段断电（不黑屏）。OnPowerOut 同一刻发出，电磁锁门自己打开。</summary>
        public static void CutPower()
        {
            GameFlow.SetState(GameState.PowerOut);
            Checkpoints.Reach(Checkpoints.PowerOut);
            // 停电后不再提示 "Get the Flash"：接完电话已经提示 Investigate Second Floor
        }

        /// <summary>第 12 步：拿起手电，状态仍是 PowerOut。人台和名画人物由 Threats 按 HasFlashlight 出动，天使不动。</summary>
        public static void TakeFlashlight()
        {
            GameFlow.GiveFlashlight();
            if (!GameFlow.HasFlashlight)
            {
                return; // 玩家身上没有手电组件
            }

            GameFlow.ShowHintOnce(HintId.CheckMonitorRoom);
            Checkpoints.Reach(Checkpoints.Flashlight);
        }

        /// <summary>第 14 步：值班室监控屏 → 天使出动（过场以后做，现在直接切）。</summary>
        public static void ActivateAngels()
        {
            GameFlow.SetState(GameState.AngelsActive);
            Checkpoints.Reach(Checkpoints.AngelsActive);
            GameFlow.ShowHintOnce(HintId.GetOut); // 拿手电时改成提示去值班室，看完监控再提示逃出去
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

            // 接完电话：0.7 s 后二楼厕所异响，播完把提示换成 "Investigate Second Floor"
            AmbientAudio.PhoneCallFinished();
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

        /// <summary>
        /// 第 18 步 · 坏结局：正门锁的单次交互已经放完插钥匙动画（MainDoorLock.Unlock 调进来），这里直接放 CG
        /// （BadEndingCutscene：门缓缓打开 → 推进库房 07 → 转身见天使 → 重击硬切黑），黑屏后接同一张结局字幕。
        /// 场景里缺门 / 库房时退回原来的渐黑。
        /// </summary>
        public static void BadEnding()
        {
            if (!BeginEnding())
            {
                return;
            }

            if (!BadEndingCutscene.Play(ShowBadEndingCard))
            {
                Presentation.FadeToBlack(FadeSeconds * 3f, ShowBadEndingCard);
            }
        }

        private static void ShowBadEndingCard()
        {
            ShowEndingCard("Temporary Storage 07", new[]
            {
                "Behind the door there is no street. Only crates, stacked to the ceiling.",
                "The number on the door matches the number on the key.",
                "The collection catches up. He is catalogued, and placed with the rest.",
                "Friday. Lily counts the crates. She stops in front of the new piece for a while, then walks on.",
                "The storage light goes out.",
            });
        }

        /// <summary>
        /// 第 18 步 · 真结局：钥匙还在手里（证明他能离开），但没去开正门 —— 回到全家福前，走进那块空白。
        /// 老大 2026-11-02：大门改成单次交互直接坏结局后，真结局不能再要求"钥匙已插进锁孔"，否则永远走不到。
        /// </summary>
        public static void TrueEnding(float fadeOutSeconds = FadeSeconds * 3f)
        {
            Ending(fadeOutSeconds, "Family Portrait", new[]
            {
                "The key is in his pocket. The door could open at any time.",
                "He steps into the blank. This time, it lets him in.",
                "The portrait has four people now.",
                "Across the room, the blurred man in The Gallery slowly turns his head.",
                "Friday morning. Lily touches the canvas. Her fingers stop.",
                "It is wet.",
            }, trueEndCG: true);
        }

        private static void Ending(float fadeOutSeconds, string title, string[] lines, bool trueEndCG = false)
        {
            if (!BeginEnding())
            {
                return;
            }

            // 真结局：黑屏后先放 CG（TrueEndingCG，4 张静帧 ~29 s），CG 渐黑后再出字幕
            Presentation.FadeToBlack(fadeOutSeconds, trueEndCG
                ? () => TrueEndingCG.Play(() => ShowEndingCard(title, lines))
                : () => ShowEndingCard(title, lines));
        }

        /// <summary>先切到 Ending：馆藏全部停下，渐黑 / CG 途中不会被抓。</summary>
        private static bool BeginEnding()
        {
            if (Busy)
            {
                return false;
            }

            Busy = true;
            ThreatSchedule.Paused = true;   // 名画人物 / 人台不看 Ending 状态，靠这个停住、不再抓人
            GameFlow.SetInputLocked(true);
            GameFlow.SetState(GameState.Ending);
            return true;
        }

        /// <summary>黑屏已经到位以后：结局字幕，完了重新加载场景回主菜单。</summary>
        private static void ShowEndingCard(string title, string[] lines)
        {
            Presentation.ShowEnding(title, lines, () =>
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex));
        }

        internal static void Reset() => Busy = false;

        /// <summary>自己管锁控制的过场（出画拉回）占住 Busy，期间别的切换不进来。</summary>
        internal static void Hold(bool on) => Busy = on;

        private static void Fade(System.Action atBlack) => Fade(FadeSeconds, atBlack);

        private static void Fade(float fadeOutSeconds, System.Action atBlack)
        {
            if (Busy)
            {
                return;
            }

            Busy = true;
            GameFlow.SetInputLocked(true);
            Presentation.FadeToBlack(fadeOutSeconds, () =>
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
