using Ib.Core;
using Ib.Flow;
using UnityEngine;

namespace Ib.Interaction
{
    /// <summary>
    /// 配电间保安柜（GDD §3.5 任务 B / §3.6 第 15 步）：天使出动以后按 E 取工作卡 → 提示「打开前台抽屉」。
    /// 卡片模型（可选）拿走后隐藏。Ib Level 挂在 B_SecurityCabinet（自带 BoxCollider）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StaffCardCabinet : MonoBehaviour, IInteractable
    {
        [SerializeField] private GameObject cardVisual;

        public string Prompt => "Take card";

        public InteractKind Kind => InteractKind.Pickup;

        public bool CanInteract => GameFlow.State == GameState.AngelsActive && !GameFlow.HasStaffCard;

        private void OnEnable()
        {
            GameFlow.StateChanged += OnStateChanged;
            Apply();
        }

        private void OnDisable() => GameFlow.StateChanged -= OnStateChanged;

        private void OnStateChanged(GameState from, GameState to) => Apply();

        private void Apply()
        {
            if (cardVisual != null)
            {
                cardVisual.SetActive(!GameFlow.HasStaffCard);
            }
        }

        public void Interact(PlayerInteractor interactor)
        {
            Sfx.PlayAt(Sfx.KeyClick, transform.position, 0.6f);
            SceneTransitions.TakeStaffCard();
            Apply();
        }
    }
}
