using Ib.Flow;
using Ib.UI;
using UnityEngine;

namespace Ib.Interaction
{
    /// <summary>
    /// 只能"看一看"的画（2026-11-02 黑屋全家福草稿）：按 E 在屏幕下方出一句独白，不推镜头、不锁输入。
    /// 挂在画的父节点上（黑屋是 FamilyPortraitSketch_Easel，子物体 Canvas 自带 BoxCollider），
    /// PlayerInteractor 命中碰撞体后用 GetComponentInParent 找到它。
    /// nextTime 只防连按（0.5 s），看完字幕可以立刻走动。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InspectCaption : MonoBehaviour, IInteractable
    {
        [SerializeField] private string captionId = "C_BlackRoomPortrait";
        [SerializeField, Min(0.5f)] private float seconds = 4f;

        private float nextTime;

        public string Prompt => "Look";

        public InteractKind Kind => InteractKind.Painting;

        public bool CanInteract => !Presentation.IsReading && !SceneTransitions.Busy && Time.unscaledTime >= nextTime;

        public void Interact(PlayerInteractor interactor)
        {
            Captions.Show(captionId, seconds);
            nextTime = Time.unscaledTime + 0.5f;
        }
    }
}
