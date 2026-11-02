using UnityEngine;

namespace Ib.Interaction
{
    /// <summary>
    /// 配电室门板上的交互（运行时由 PowerRoomDoor 挂到 PowerRoom_Door 上，门板自带 BoxCollider）。
    /// 门开着一条缝时按 E「Push」推到全开；关着 / 全开 / 正在动时不可交互。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PowerRoomDoorPush : MonoBehaviour, IInteractable
    {
        public string Prompt => "Push";

        public InteractKind Kind => InteractKind.Door;

        public bool CanInteract => PowerRoomDoor.Instance != null && PowerRoomDoor.Instance.CanPush;

        public void Interact(PlayerInteractor interactor)
        {
            if (PowerRoomDoor.Instance != null)
            {
                PowerRoomDoor.Instance.Push();
            }
        }
    }
}
