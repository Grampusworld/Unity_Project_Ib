using Ib.Core;
using UnityEngine;

namespace Ib.Flow
{
    /// <summary>
    /// 检查点（GDD §3.4：只有检查点，无手动存档）。每次状态切换处由 SceneTransitions 记录：
    /// 重生点（PlayerSpawnPoint 物体名）+ 当时的状态。手电不回收（死亡只会发生在拿手电之后）。
    /// 死亡后 GameOverScreen 先 PlayerSpawner.Respawn()（落到 CurrentCheckpoint），
    /// 再发 Continued —— 这里接着把世界恢复成记录时的样子（怪物归位由 Threats 自己订阅 Continued）。
    /// </summary>
    public static class Checkpoints
    {
        public const string BlackRoom = "CP0_BlackRoom_Start";
        public const string AfterHours = "CP1_ExitPainting_FamilyRoom";
        public const string PowerOut = "CP_PowerOut_Stairs";
        public const string Flashlight = "CP2_Flashlight_Toilet";
        public const string AngelsActive = "CP3_TaskA_DutyRoom";

        public static string CurrentName { get; private set; }

        private static GameState savedState;

        /// <summary>按当前状态记一个检查点。找不到同名重生点时只记状态，重生位置沿用上一个。</summary>
        public static void Reach(string spawnName)
        {
            PlayerSpawnPoint point = Find(spawnName);
            if (point != null)
            {
                PlayerSpawner.CurrentCheckpoint = point;
            }
            else
            {
                Debug.LogWarning("[Checkpoints] 场景里没有重生点 " + spawnName + "，只记录状态。");
            }

            CurrentName = spawnName;
            savedState = GameFlow.State;
        }

        /// <summary>记检查点并立刻传送过去（场景切换用）。</summary>
        public static void ReachAndTeleport(string spawnName)
        {
            Reach(spawnName);
            PlayerSpawner spawner = Object.FindAnyObjectByType<PlayerSpawner>();
            if (spawner != null)
            {
                spawner.Respawn();
            }
        }

        internal static void Reset()
        {
            CurrentName = null;
            PlayerSpawner.CurrentCheckpoint = null;
        }

        internal static void Restore()
        {
            if (CurrentName == null)
            {
                return;
            }

            GameFlow.SetState(savedState);
            WorldState.Apply(savedState);
        }

        private static PlayerSpawnPoint Find(string name)
        {
            foreach (PlayerSpawnPoint p in Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsInactive.Include))
            {
                if (p.name == name)
                {
                    return p;
                }
            }

            return null;
        }
    }
}
