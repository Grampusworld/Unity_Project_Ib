using Ib.Flow;
using UnityEngine;

namespace Ib.Interaction
{
    /// <summary>
    /// 厕所里的手电（GDD §3.6 第 11–12 步）：按 E 拿起，同一刻馆藏出动。
    /// 只在停电后（PowerOut）能拿；拿起后物体隐藏，状态不变（馆藏出动由 Threats 按 HasFlashlight 自己算）。
    /// Ib Level 在生成器里挂上它（需要 Collider）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlashlightPickup : MonoBehaviour, IInteractable
    {
        public string Prompt => "Take";

        public InteractKind Kind => InteractKind.Pickup;

        public bool CanInteract => !GameFlow.HasFlashlight && GameFlow.State == GameState.PowerOut;

        public void Interact(PlayerInteractor interactor)
        {
            SceneTransitions.TakeFlashlight();
            if (GameFlow.HasFlashlight)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
