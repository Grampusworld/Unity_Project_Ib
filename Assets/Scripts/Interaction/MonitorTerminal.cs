using Ib.Flow;
using UnityEngine;

namespace Ib.Interaction
{
    /// <summary>
    /// 值班室监控屏（GDD §3.6 第 14 步）：停电后拿到手电时按 E → 监控过场（SecurityMonitors）→ 天使出动。
    /// 场景里没有 SecurityMonitors 时直接切到天使出动。
    /// Ib Level 挂在 A_MonitorWall（需要 Collider）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonitorTerminal : MonoBehaviour, IInteractable
    {
        private SecurityMonitors monitors;

        public string Prompt => "Check";

        public InteractKind Kind => InteractKind.Device;

        public bool CanInteract => GameFlow.State == GameState.PowerOut && GameFlow.HasFlashlight
            && (Monitors == null || !Monitors.Playing);

        private SecurityMonitors Monitors => monitors != null ? monitors : monitors = FindAnyObjectByType<SecurityMonitors>();

        public void Interact(PlayerInteractor interactor)
        {
            if (Monitors == null)
            {
                SceneTransitions.ActivateAngels();
                return;
            }

            Camera eye = interactor != null ? interactor.GetComponentInChildren<Camera>() : null;
            Monitors.Play(eye != null ? eye : Camera.main);
        }
    }
}
