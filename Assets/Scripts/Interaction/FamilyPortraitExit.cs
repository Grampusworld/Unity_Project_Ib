using Ib.Flow;
using UnityEngine;

namespace Ib.Interaction
{
    /// <summary>
    /// 画中馆二楼的《全家福》（GDD §3.6 第 6 步）：PaintedGallery 时按 E → 黑屏回到黑屋。
    /// 唯一能交互的画。Ib Level 挂在 FamilyPortrait_Easel（需要 Collider）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FamilyPortraitExit : MonoBehaviour, IInteractable
    {
        public string Prompt => "Look";

        public InteractKind Kind => InteractKind.Painting;

        public bool CanInteract => GameFlow.State == GameState.PaintedGallery && !SceneTransitions.Busy;

        public void Interact(PlayerInteractor interactor) => SceneTransitions.ReturnToBlackRoom();
    }
}
