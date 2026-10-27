using UnityEngine;
using UnityEngine.InputSystem;

namespace Ib.Interaction
{
    /// <summary>
    /// 准星交互：从相机正前方打一条短射线，命中 IInteractable 时显示提示，按 E 触发。
    /// 只读相机朝向，不写相机的任何属性（单一写者原则）。
    /// 提示先用 OnGUI 顶着，正式 UI 做好后替换 DrawPrompt 即可。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField, Min(0.1f)] private float reach = 2.2f;
        [SerializeField, Min(0.01f)] private float probeRadius = 0.08f;
        [SerializeField] private LayerMask mask = ~0;
        [SerializeField] private bool showPrompt = true;

        public IInteractable Current { get; private set; }

        private InputAction interactAction;
        private GUIStyle promptStyle;
        private int promptFontHeight = -1;

        // 提示字号按屏幕高度算：1080p ≈ 35 px（原来固定 22 px 偏小）
        private const float PromptFontRatio = 0.0325f;
        private const int PromptFontMin = 26;

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
        }

        private void OnDisable()
        {
            interactAction.Disable();
            Current = null;
        }

        private void Update()
        {
            Current = FindTarget();

            if (Current != null && interactAction.WasPressedThisFrame())
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

        private void OnGUI()
        {
            if (!showPrompt || Current == null)
            {
                return;
            }

            if (promptStyle == null || promptFontHeight != Screen.height)
            {
                promptStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Max(PromptFontMin, Mathf.RoundToInt(Screen.height * PromptFontRatio)),
                };
                promptStyle.normal.textColor = new Color(1f, 1f, 1f, 0.92f);
                promptFontHeight = Screen.height;
            }

            string text = "[E] " + Current.Prompt;
            float h = promptStyle.fontSize * 1.8f;
            Rect rect = new Rect(0f, Screen.height * 0.5f + h * 0.85f, Screen.width, h);

            // 先画一层深色描影，亮背景下也看得清
            Color tint = promptStyle.normal.textColor;
            promptStyle.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, promptStyle);
            promptStyle.normal.textColor = tint;
            GUI.Label(rect, text, promptStyle);
        }
    }
}
