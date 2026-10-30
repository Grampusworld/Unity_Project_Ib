using Ib.Flow;
using UnityEngine;

namespace Ib.Interaction
{
    /// <summary>
    /// 值班室监控屏（GDD §3.6 第 14 步）：停电后拿到手电时按 E → 天使出动。
    /// Ib Level 挂在 A_MonitorWall（需要 Collider）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonitorTerminal : MonoBehaviour, IInteractable
    {
        public string Prompt => "Check";

        public InteractKind Kind => InteractKind.Device;

        public bool CanInteract => GameFlow.State == GameState.PowerOut && GameFlow.HasFlashlight;

        public void Interact(PlayerInteractor interactor) => SceneTransitions.ActivateAngels();
    }
}
