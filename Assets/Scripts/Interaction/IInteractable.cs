namespace Ib.Interaction
{
    /// <summary>
    /// 可按 E 交互的物体。实现者挂在带 Collider 的物体或它的父节点上，
    /// PlayerInteractor 用 GetComponentInParent 找到它。
    /// </summary>
    public interface IInteractable
    {
        /// <summary>准星对准时的提示文字（例如"推开"）。</summary>
        string Prompt { get; }

        /// <summary>当前能不能交互；false 时不显示提示，按 E 也不响应。</summary>
        bool CanInteract { get; }

        void Interact(PlayerInteractor interactor);
    }
}
