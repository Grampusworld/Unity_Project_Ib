using UnityEngine;
using UnityEngine.InputSystem;

namespace Ib.Player
{
    /// <summary>
    /// 鼠标视角。yaw 写在角色根节点，pitch 写在相机自身，
    /// 与 FirstPersonController 的职责严格分开（那边只写相机 localPosition）。
    /// 默认 -100 执行序，保证移动用到的 transform.forward 拿到的是本帧的新朝向。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class PlayerLook : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private Transform cameraTransform;

        [Header("灵敏度")]
        [Tooltip("每像素旋转角度（度）。0.15 约等于 2400 像素转一圈。")]
        [SerializeField, Min(0.001f)] private float sensitivity = 0.15f;
        [SerializeField] private bool invertY;
        [SerializeField, Range(-89f, 0f)] private float pitchMin = -85f;
        [SerializeField, Range(0f, 89f)] private float pitchMax = 85f;

        [Header("光标")]
        [SerializeField] private bool lockCursorOnStart = true;

        public float Sensitivity
        {
            get => sensitivity;
            set => sensitivity = Mathf.Max(0.001f, value);
        }

        public bool IsCursorLocked => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>调试菜单打开时置 true：不要再把光标自动锁回去（否则点菜单会顺手锁屏）。</summary>
        public static bool SuspendPointerCapture { get; set; }

        private InputAction lookAction;
        private float yaw;
        private float pitch;

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

            lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
            yaw = transform.eulerAngles.y;
        }

        private void OnEnable()
        {
            lookAction.Enable();
            if (lockCursorOnStart)
            {
                LockCursor(true);
            }
        }

        private void OnDisable()
        {
            lookAction.Disable();
            LockCursor(false);
        }

        private void Update()
        {
            if (!IsCursorLocked)
            {
                // 编辑器里按 Esc 释放光标，左键点回游戏重新锁定（调试菜单开着时不抢）
                if (!SuspendPointerCapture && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    LockCursor(true);
                }

                return;
            }

            Vector2 delta = lookAction.ReadValue<Vector2>();
            if (delta.sqrMagnitude <= 0f)
            {
                return;
            }

            yaw += delta.x * sensitivity;
            pitch += (invertY ? delta.y : -delta.y) * sensitivity;
            pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        public void ResetLook(float yawDegrees = 0f)
        {
            yaw = yawDegrees;
            pitch = 0f;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.identity;
            }
        }

        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
