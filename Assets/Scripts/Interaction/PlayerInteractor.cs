using System;
using Ib.Flow;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ib.Interaction
{
    /// <summary>
    /// 准星交互：从相机正前方打一条细球扫，命中 IInteractable 时描边，按 E 触发。
    /// 只读相机朝向，不写相机的任何属性（单一写者原则）。
    /// 提示文字由 UI 订阅 TargetChanged 自己画，这里不画。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField, Min(0.1f)] private float reach = 2.2f;
        [SerializeField, Min(0.01f)] private float probeRadius = 0.08f;
        [SerializeField] private LayerMask mask = ~0;
        [SerializeField, Min(0.001f)] private float highlightWidth = 0.03f;

        public IInteractable Current { get; private set; }

        /// <summary>当前对准的交互物，没有时为 null。</summary>
        public static IInteractable Target { get; private set; }

        /// <summary>Target 变化时触发一次（含变成 null）。打开菜单、阅读器时 IsAiming 为 false，也会变成 null。</summary>
        public static event Action<IInteractable> TargetChanged;

        private InteractHighlight highlight;

        private InputAction interactAction;

        private void Awake()
        {
            if (cameraTransform == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null)
                {
                    cameraTransform = cam.transform;
                }
            }

            // 输入用代码构建，与 FirstPersonController 一致
            interactAction = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
        }

        private void OnEnable()
        {
            interactAction.Enable();
            highlight ??= new InteractHighlight(highlightWidth);
        }

        private void OnDisable()
        {
            interactAction.Disable();
            SetCurrent(null);
            highlight?.Dispose();
            highlight = null;
        }

        private void Update()
        {
            bool aiming = GameFlow.IsAiming;
            SetCurrent(aiming ? FindTarget() : null);
            highlight?.Show(Current as Component);

            if (aiming && Current != null && interactAction.WasPressedThisFrame())
            {
                Current.Interact(this);
            }
        }

        private IInteractable FindTarget()
        {
            if (cameraTransform == null)
            {
                return null;
            }

            // 用细球扫而不是射线：正对双开门中缝时射线会从缝里穿过去。
            // 起点在玩家胶囊里，球扫会忽略起点就重叠的碰撞体，不会打到自己。
            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            if (!Physics.SphereCast(ray, probeRadius, out RaycastHit hit, reach, mask, QueryTriggerInteraction.Ignore))
            {
                return null;
            }

            IInteractable target = hit.collider.GetComponentInParent<IInteractable>();
            return target != null && target.CanInteract ? target : null;
        }

        private void SetCurrent(IInteractable next)
        {
            Current = next;
            if (next == Target)
            {
                return;
            }

            Target = next;
            TargetChanged?.Invoke(next);
        }
    }
}
