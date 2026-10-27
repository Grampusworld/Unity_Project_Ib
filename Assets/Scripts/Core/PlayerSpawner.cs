using Ib.Environment;
using Ib.Player;
using UnityEngine;

namespace Ib.Core
{
    /// <summary>
    /// 把玩家放到出生点。挂在场景的管理对象上（GameManager / --- Game ---）。
    /// 死亡重生调 Respawn()：传到最近的检查点（没有就用默认出生点），不用关心 CharacterController 和黑屋状态。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] private FirstPersonController player;
        [SerializeField] private PlayerSpawnPoint spawnPoint;
        [SerializeField] private bool snapOnStart = true;

        /// <summary>
        /// 最近一次到达的检查点。检查点逻辑还没做，现在一直是 null → 退回默认出生点。
        /// 以后检查点触发器直接给它赋值即可。
        /// </summary>
        public static PlayerSpawnPoint CurrentCheckpoint { get; set; }

        private void Start()
        {
            if (snapOnStart)
            {
                Respawn();
            }
        }

        public void Respawn()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<FirstPersonController>();
            }

            if (player == null)
            {
                Debug.LogWarning("[PlayerSpawner] 场景里没有 FirstPersonController，无法传送。", this);
                return;
            }

            PlayerSpawnPoint target = CurrentCheckpoint != null ? CurrentCheckpoint
                : spawnPoint != null ? spawnPoint : PlayerSpawnPoint.Default;
            if (target == null)
            {
                Debug.LogWarning("[PlayerSpawner] 没有找到 PlayerSpawnPoint，保持在当前位置。", this);
                return;
            }

            // 重生点在黑屋时，黑屋可能已经被大门整块停用（没有地板碰撞）—— 先恢复序章状态再传送，
            // 否则一落地就掉出地图
            MuseumEntrance entrance = FindAnyObjectByType<MuseumEntrance>();
            if (entrance != null)
            {
                entrance.PrepareTeleport(target.SpawnPosition);
            }

            player.Teleport(target.SpawnPosition, target.SpawnRotation);

            PlayerLook look = player.GetComponent<PlayerLook>();
            if (look != null)
            {
                look.ResetLook(target.SpawnRotation.eulerAngles.y);
            }
        }
    }
}
