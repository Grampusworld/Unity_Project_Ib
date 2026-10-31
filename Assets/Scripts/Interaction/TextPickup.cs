using Ib.Core;
using Ib.Flow;
using Ib.UI;
using UnityEngine;

namespace Ib.Interaction
{
    /// <summary>
    /// 文本收集物（GDD §6.11）：按 E 展开纸面，读的时候锁移动和视角，不冻结时间。
    /// 物体不消失，可以反复读。Ib Level 在生成器里挂上它并填 textId。
    /// 可读阶段（§6.11.2「可拿到的阶段」）等对应状态做出来再加，现在任何时候都能读。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TextPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private string textId = "T1";

        // T7 交接本读完 → 提示「到配电间取工作卡」（§3.7 第 5 条）
        private const string HandoverTextId = "T7";

        // 阅读器按 E 关闭的那一帧，PlayerInteractor 也会看到 E；这一帧不许再打开
        private int closedFrame = -1;

        public string TextId => textId;

        public string Prompt => "Read";

        public InteractKind Kind => InteractKind.Text;

        public bool CanInteract => !Presentation.IsReading && Time.frameCount != closedFrame;

        public void Interact(PlayerInteractor interactor)
        {
            Sfx.PlayUI(Sfx.PaperFlip, 0.25f);
            GameFlow.SetInputLocked(true);
            Presentation.OpenText(textId, OnClosed);

            // UI 没生成时 OpenText 只打警告，别把玩家锁死
            if (!Presentation.IsReading)
            {
                GameFlow.SetInputLocked(false);
            }
        }

        private void OnClosed()
        {
            closedFrame = Time.frameCount;
            if (!GameFlow.IsPaused)
            {
                GameFlow.SetInputLocked(false);
            }

            if (textId == HandoverTextId)
            {
                GameFlow.ShowHintOnce(HintId.GetStaffCard);
            }
        }
    }
}
