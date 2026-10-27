using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ib.Player
{
    /// <summary>
    /// 第一人称移动核心。只负责"位移与体型"，不碰相机旋转。
    /// 相机位置由本组件唯一写入（蹲伏高度 + 头部晃动），旋转交给 PlayerLook，
    /// 两者写的是不同通道，避免同帧互相覆盖。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("移动速度")]
        [SerializeField, Min(0f)] private float walkSpeed = 3.2f;
        [SerializeField, Min(0f)] private float sprintSpeed = 5.4f;
        [SerializeField, Min(0f)] private float crouchSpeed = 1.5f;
        [SerializeField, Min(0f)] private float acceleration = 16f;
        [SerializeField, Range(0f, 1f)] private float airControl = 0.35f;

        [Header("跳跃与重力")]
        [SerializeField, Min(0f)] private float jumpHeight = 1.05f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float groundStick = -2f;

        [Header("体型")]
        [SerializeField, Min(0.5f)] private float standHeight = 1.8f;
        [SerializeField, Min(0.5f)] private float crouchHeight = 1.05f;
        [SerializeField] private float eyeOffset = -0.18f;
        [SerializeField, Min(0.1f)] private float crouchLerpSpeed = 10f;

        [Header("引用")]
        [SerializeField] private Transform cameraRoot;

        [Header("头部晃动")]
        [SerializeField] private bool headBobEnabled = true;
        [SerializeField, Min(0f)] private float bobFrequency = 9f;
        [SerializeField, Min(0f)] private float bobVertical = 0.035f;
        [SerializeField, Min(0f)] private float bobLateral = 0.02f;

        [Header("脚步")]
        [SerializeField, Min(0.1f)] private float stepDistance = 2.1f;

        [Header("调试：自由飞行")]
        [SerializeField, Min(0.5f)] private float flySpeed = 6f;
        [SerializeField, Min(1f)] private float flyBoostMultiplier = 3f;

        /// <summary>每走满 stepDistance 触发一次，用于接脚步声 / 心跳 / 怪物听觉。</summary>
        public event Action OnFootstep;
        /// <summary>蹲伏状态切换时触发（参数为"当前是否蹲着"）。</summary>
        public event Action<bool> OnCrouchChanged;

        public bool IsGrounded { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsMoving => HorizontalSpeed > 0.15f;

        public float HorizontalSpeed
        {
            get
            {
                Vector3 v = velocity;
                v.y = 0f;
                return v.magnitude;
            }
        }

        public CharacterController Controller => controller;

        /// <summary>自由飞行（穿墙、无重力）。由调试菜单切换，正常游戏里永远是 false。</summary>
        public bool FreeFly { get; private set; }

        /// <summary>飞行速度（m/s），调试菜单里可调。</summary>
        public float FlySpeed
        {
            get => flySpeed;
            set => flySpeed = Mathf.Max(0.5f, value);
        }

        /// <summary>菜单打开时置 true：丢弃移动 / 跳跃 / 蹲伏输入，但保留重力与相机，人物原地站住。</summary>
        public bool InputBlocked { get; set; }

        private CharacterController controller;
        private Transform eye;
        private InputAction moveAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction crouchAction;

        private Vector3 velocity;
        private Vector3 horizontalVelocity;
        private float bobPhase;
        private float distanceSinceStep;
        private float currentHeight;
        private Vector3 lastSafePosition;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (cameraRoot == null)
            {
                cameraRoot = transform.Find("CameraRoot");
            }

            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                eye = cam.transform;
            }

            currentHeight = standHeight;
            lastSafePosition = transform.position;
            CreateActions();
        }

        private void OnEnable()
        {
            moveAction.Enable();
            jumpAction.Enable();
            sprintAction.Enable();
            crouchAction.Enable();
        }

        private void OnDisable()
        {
            moveAction.Disable();
            jumpAction.Disable();
            sprintAction.Disable();
            crouchAction.Disable();
        }

        // 输入用代码构建，不依赖 .inputactions 资产的引用接线。
        // 好处：预制体拷到别的项目不会因为资产丢失而静默失效。
        // 代价：以后要做按键重绑定 UI，需要改成 InputActionAsset。
        private void CreateActions()
        {
            moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            jumpAction = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            sprintAction = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            crouchAction = new InputAction("Crouch", InputActionType.Button, "<Keyboard>/leftCtrl");
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (FreeFly)
            {
                UpdateFreeFly(dt);
                return;
            }

            if (!controller.enabled)
            {
                return;
            }

            UpdateCrouch(dt);

            Vector3 wish = ReadMoveDirection();
            float speed = ResolveSpeed();

            Vector3 target = wish * speed;
            float blend = 1f - Mathf.Exp(-acceleration * (IsGrounded ? 1f : airControl) * dt);
            horizontalVelocity = Vector3.Lerp(horizontalVelocity, target, blend);

            ApplyVertical(dt);

            velocity = new Vector3(horizontalVelocity.x, velocity.y, horizontalVelocity.z);
            controller.Move(velocity * dt);

            // isGrounded 在 Move 之后才可靠
            IsGrounded = controller.isGrounded;

            UpdateFootsteps(dt);
            UpdateCameraPosition(dt);
        }

        private Vector3 ReadMoveDirection()
        {
            Vector2 raw = ReadMoveInput();
            if (raw.sqrMagnitude > 1f)
            {
                raw.Normalize();
            }

            Vector3 dir = transform.right * raw.x + transform.forward * raw.y;
            dir.y = 0f;
            return dir.sqrMagnitude > 1f ? dir.normalized : dir;
        }

        /// <summary>菜单打开时返回零向量，人物不接受移动输入。</summary>
        private Vector2 ReadMoveInput()
        {
            return InputBlocked ? Vector2.zero : moveAction.ReadValue<Vector2>();
        }

        private float ResolveSpeed()
        {
            bool sprinting = !InputBlocked && sprintAction.IsPressed() && !IsCrouching && moveAction.ReadValue<Vector2>().y > 0.1f;
            IsSprinting = sprinting;

            if (IsCrouching)
            {
                return crouchSpeed;
            }

            return sprinting ? sprintSpeed : walkSpeed;
        }

        private void ApplyVertical(float dt)
        {
            if (IsGrounded)
            {
                // 贴地，避免下坡时反复进入"离地一帧"
                if (velocity.y < 0f)
                {
                    velocity.y = groundStick;
                }

                if (jumpAction.WasPressedThisFrame() && !IsCrouching && !InputBlocked)
                {
                    velocity.y = Mathf.Sqrt(-2f * gravity * jumpHeight);
                }
            }
            else
            {
                velocity.y += gravity * dt;
            }
        }

        private void UpdateCrouch(float dt)
        {
            bool wantsCrouch = !InputBlocked && crouchAction.IsPressed();

            // 头顶有东西时，松开也站不起来
            if (IsCrouching && !wantsCrouch && !HasHeadroom())
            {
                wantsCrouch = true;
            }

            float targetHeight = wantsCrouch ? crouchHeight : standHeight;
            currentHeight = Mathf.MoveTowards(currentHeight, targetHeight, crouchLerpSpeed * dt);

            controller.height = currentHeight;
            controller.center = new Vector3(0f, currentHeight * 0.5f, 0f);

            bool nowCrouching = wantsCrouch;
            if (nowCrouching != IsCrouching)
            {
                IsCrouching = nowCrouching;
                OnCrouchChanged?.Invoke(IsCrouching);
            }
        }

        private bool HasHeadroom()
        {
            float radius = Mathf.Max(0.05f, controller.radius - 0.03f);
            Vector3 bottom = transform.position + Vector3.up * (radius + 0.02f);
            Vector3 top = transform.position + Vector3.up * (standHeight - radius);

            Collider[] hits = Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                // 排除自己身上的碰撞体，否则永远站不起来
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private void UpdateFootsteps(float dt)
        {
            if (!IsGrounded || !IsMoving)
            {
                distanceSinceStep = Mathf.Min(distanceSinceStep, stepDistance * 0.9f);
                return;
            }

            distanceSinceStep += HorizontalSpeed * dt;
            if (distanceSinceStep >= stepDistance)
            {
                distanceSinceStep = 0f;
                OnFootstep?.Invoke();
            }
        }

        private void UpdateCameraPosition(float dt)
        {
            if (cameraRoot == null)
            {
                return;
            }

            float lateral = 0f;
            float vertical = 0f;

            if (headBobEnabled && IsGrounded && IsMoving)
            {
                float speedFactor = Mathf.Clamp01(HorizontalSpeed / walkSpeed);
                bobPhase += dt * bobFrequency * speedFactor;
                lateral = Mathf.Cos(bobPhase * 0.5f) * bobLateral * speedFactor;
                vertical = Mathf.Sin(bobPhase) * bobVertical * speedFactor;
            }
            else
            {
                // 停下时把相位收回到 0，避免下次起步从半空中开始
                bobPhase = Mathf.Lerp(bobPhase, 0f, 1f - Mathf.Exp(-8f * dt));
            }

            Vector3 local = new Vector3(lateral, currentHeight + eyeOffset + vertical, 0f);
            cameraRoot.localPosition = local;
        }

        /// <summary>传送时调用：处理 CharacterController 与速度残留。</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = wasEnabled;

            velocity = Vector3.zero;
            horizontalVelocity = Vector3.zero;
            currentHeight = standHeight;
            controller.height = standHeight;
            controller.center = new Vector3(0f, standHeight * 0.5f, 0f);
            bobPhase = 0f;
            distanceSinceStep = 0f;
            lastSafePosition = position;
        }

        // ---------------------------------------------------------------- 自由飞行（调试）

        /// <summary>
        /// 开关自由飞行。开启时直接停用 CharacterController——胶囊碰撞没了，所以能穿墙、下落。
        /// 关闭时会找一个不卡几何体的落点再重新启用胶囊，避免卡进墙里出不来。
        /// </summary>
        public void SetFreeFly(bool value)
        {
            if (FreeFly == value)
            {
                return;
            }

            FreeFly = value;

            if (value)
            {
                lastSafePosition = transform.position;
                velocity = Vector3.zero;
                horizontalVelocity = Vector3.zero;
                currentHeight = standHeight;
                IsGrounded = false;
                IsSprinting = false;
                controller.enabled = false;

                if (cameraRoot != null)
                {
                    cameraRoot.localPosition = new Vector3(0f, currentHeight + eyeOffset, 0f);
                }

                return;
            }

            // 退出飞行：CharacterController 还关着，这时候做重叠检测不会打到自己
            Vector3 target = FindClearPosition(transform.position);
            controller.height = standHeight;
            controller.center = new Vector3(0f, standHeight * 0.5f, 0f);
            transform.position = target;
            controller.enabled = true;

            velocity = Vector3.zero;
            horizontalVelocity = Vector3.zero;
            IsGrounded = false;
            distanceSinceStep = 0f;
        }

        /// <summary>相机朝向 + 世界上下，无加速度、无碰撞、无重力。</summary>
        private void UpdateFreeFly(float dt)
        {
            if (InputBlocked)
            {
                return;
            }

            Vector2 raw = moveAction.ReadValue<Vector2>();
            Vector3 forward = eye != null ? eye.forward : transform.forward;
            Vector3 right = eye != null ? eye.right : transform.right;

            Vector3 dir = forward * raw.y + right * raw.x;
            if (jumpAction.IsPressed())
            {
                dir += Vector3.up;
            }

            if (crouchAction.IsPressed())
            {
                dir -= Vector3.up;
            }

            if (dir.sqrMagnitude > 1f)
            {
                dir.Normalize();
            }

            float speed = flySpeed * (sprintAction.IsPressed() ? flyBoostMultiplier : 1f);
            transform.position += dir * (speed * dt);

            velocity = Vector3.zero;
            horizontalVelocity = Vector3.zero;
            IsGrounded = false;
            IsSprinting = false;
        }

        private Vector3 FindClearPosition(Vector3 position)
        {
            float radius = Mathf.Max(0.05f, controller.radius - 0.02f);
            if (IsCapsuleClear(position, radius))
            {
                return position;
            }

            // 悬在半空：往下找地面，脚落在命中点上方一点
            float probeStart = position.y + 1.5f;
            if (Physics.Raycast(new Vector3(position.x, probeStart, position.z), Vector3.down,
                    out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore))
            {
                Vector3 grounded = new Vector3(position.x, hit.point.y + 0.05f, position.z);
                if (IsCapsuleClear(grounded, radius))
                {
                    return grounded;
                }
            }

            // 卡在墙 / 地板里就一级一级往上抬，找最近的空位
            for (float lift = 0.5f; lift <= 4f; lift += 0.5f)
            {
                Vector3 candidate = position + Vector3.up * lift;
                if (IsCapsuleClear(candidate, radius))
                {
                    return candidate;
                }
            }

            Debug.LogWarning("[FirstPersonController] 退出自由飞行时附近没有空位，退回进入飞行前的位置。", this);
            return lastSafePosition;
        }

        private bool IsCapsuleClear(Vector3 feet, float radius)
        {
            Vector3 bottom = feet + Vector3.up * (radius + 0.02f);
            Vector3 top = feet + Vector3.up * (standHeight - radius);
            return !Physics.CheckCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore);
        }
    }
}
