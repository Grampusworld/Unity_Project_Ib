using Ib.Environment;
using Ib.Flow;
using Ib.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ib.Core
{
    /// <summary>
    /// 灰盒调试：数字键 1–9 传送到列表里第 1–9 个出生点 / 检查点。
    /// 列表由 GrayboxBuilder 按顺序写好；只在编辑器和 Development Build 里生效，正式包里是空壳。
    /// 注意：F1 留给调试菜单（DebugMenu），所以这里用主键盘数字键，别改回 F1–F9。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebugSpawnSwitcher : MonoBehaviour
    {
        [SerializeField] private FirstPersonController player;
        [SerializeField] private PlayerSpawnPoint[] points = new PlayerSpawnPoint[0];

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly Key[] Keys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
        };

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (player == null)
            {
                player = FindAnyObjectByType<FirstPersonController>();
                if (player == null)
                {
                    return;
                }
            }

            // F10：全馆断电 / 来电（水晶灯熄灭后会开始摇晃）
            if (keyboard[Key.F10].wasPressedThisFrame)
            {
                MuseumLights.Toggle();
                Debug.Log("[DebugSpawnSwitcher] 全馆灯光 → " + (MuseumLights.On ? "开" : "关"));
                return;
            }

            int count = Mathf.Min(points.Length, Keys.Length);
            for (int i = 0; i < count; i++)
            {
                if (points[i] != null && keyboard[Keys[i]].wasPressedThisFrame)
                {
                    Go(points[i]);
                    return;
                }
            }
        }

        private void Go(PlayerSpawnPoint target)
        {
            // 传回黑屋时要先把序章状态恢复（黑屋可能已被大门关闭停用）
            MuseumEntrance entrance = FindAnyObjectByType<MuseumEntrance>();
            if (entrance != null)
            {
                entrance.PrepareTeleport(target.SpawnPosition);
            }

            player.Teleport(target.SpawnPosition, target.SpawnRotation);

            // PlayerLook 自己记着 yaw，不重置的话下一帧会把朝向转回去
            PlayerLook look = player.GetComponent<PlayerLook>();
            if (look != null)
            {
                look.ResetLook(target.SpawnRotation.eulerAngles.y);
            }

            ApplyState(target.name);
            Debug.Log("[DebugSpawnSwitcher] → " + target.name + "（" + GameFlow.State + "）", target);
        }

        /// <summary>
        /// 按检查点把全局状态拨到对应阶段，并记成当前检查点（死了从这里重来）。
        /// DBG 点只在还停在黑屋时拨到画中馆，其余保持当前状态。主菜单期间不动状态。
        /// CP4 / CP5 顺带给工作卡 / 钥匙。往回传不会收走手电和道具、也不会重新来电（F10 手动开灯）。
        /// </summary>
        private static void ApplyState(string pointName)
        {
            if (GameFlow.State == GameState.MainMenu)
            {
                return;
            }

            switch (pointName)
            {
                case Checkpoints.BlackRoom:
                    GameFlow.SetState(GameState.BlackRoom);
                    break;
                case Checkpoints.AfterHours:
                    GameFlow.SetState(GameState.AfterHours);
                    break;
                case Checkpoints.Flashlight:
                    GameFlow.PhoneAnswered = true;
                    GameFlow.SetState(GameState.PowerOut);
                    GameFlow.GiveFlashlight();
                    break;
                case Checkpoints.AngelsActive:
                case Checkpoints.TaskB:
                case Checkpoints.TaskC:
                    GameFlow.PhoneAnswered = true;
                    GameFlow.SetState(GameState.AngelsActive);
                    GameFlow.GiveFlashlight();
                    GameFlow.HasStaffCard |= pointName != Checkpoints.AngelsActive;
                    GameFlow.HasKey |= pointName == Checkpoints.TaskC;
                    break;
                default:
                    if (GameFlow.State == GameState.BlackRoom)
                    {
                        GameFlow.SetState(GameState.PaintedGallery);
                    }

                    break;
            }

            Checkpoints.Reach(pointName);
        }
#endif
    }
}
