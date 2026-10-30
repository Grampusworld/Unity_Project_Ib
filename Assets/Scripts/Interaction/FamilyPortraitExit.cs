using Ib.Flow;
using UnityEngine;

namespace Ib.Interaction
{
    /// <summary>
    /// 二楼的《全家福》。画中馆和真实馆是同一个画架：
    /// PaintedGallery 时按 E → 黑屏回到黑屋（GDD §3.6 第 6 步）；
    /// 天使出动后、钥匙已经插在正门锁孔里（没开门）时按 E → 走进那块空白，真结局（§6.9）。
    /// Ib Level 挂在 FamilyPortrait_Easel（需要 Collider）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FamilyPortraitExit : MonoBehaviour, IInteractable
    {
        public string Prompt => "Look";

        public InteractKind Kind => InteractKind.Painting;

        public bool CanInteract => (GameFlow.State == GameState.PaintedGallery || CanEnterBlank) && !SceneTransitions.Busy;

        private static bool CanEnterBlank => GameFlow.State == GameState.AngelsActive && GameFlow.KeyInserted;

        public void Interact(PlayerInteractor interactor)
        {
            if (CanEnterBlank)
            {
                SceneTransitions.TrueEnding();
            }
            else
            {
                SceneTransitions.ReturnToBlackRoom();
            }
        }
    }
}
