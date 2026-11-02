using Ib.Core;
using Ib.Flow;
using Ib.Player;
using Ib.UI;
using UnityEngine;

namespace Ib.Interaction
{
    /// <summary>
    /// 二楼全家福厅门（2026-11-02）。门洞在东线 x = 26，门板关着时正好填满门洞、挡人。
    /// - State &lt; AngelsActive：门开着（绕合页向全家福厅 +X 转 90°，贴在门洞南侧）。
    /// - 监控过场（SecurityMonitors.Playing）由 true 变 false 那一刻：0.25 s 猛关 + DoorSlam，之后锁住。
    /// - 锁着时按 E：没钥匙 → LockRattle + 字幕 C_LockedDoor；有钥匙 → KeyClick 开锁，2 s 缓开 + DoorCreak。
    ///   钥匙不消耗（正门坏结局还要用），FamilyDoorUnlocked 不随检查点回滚。
    /// 合页 = 门板南侧竖边，门宽 / 厚度 / 底边都从门板自己的 Renderer 包围盒算（和 MagLockDoor 一样），
    /// 关闭姿态 = 场景摆放姿态。读档 / 重生按当前状态瞬间摆好。
    /// 猛关时如果玩家还站在门扇扫过区（门洞内侧 clearRadius 内、全家福厅一侧），先不关，等他走开再关——
    /// 门板是实体 BoxCollider，转过来会把人推走。
    /// 门上的锁（子物体 Door_Family_Lock，GrayboxBuilder.Batch1102 生成）：没解锁时显示，解锁后隐藏。
    /// 描边打在锁上（IHighlightSource）：门板正好填满门洞、缩在 0.3 m 厚的墙中间，自己的描边壳四周全被墙和门套挡住，
    /// 所以以前关着时对准它看不到高亮（2026-11-02 修）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FamilyRoomDoor : MonoBehaviour, IInteractable, IHighlightSource
    {
        public const string LockName = "Door_Family_Lock";

        [SerializeField, Min(0.01f)] private float openSeconds = 2f;       // 用钥匙开锁后缓缓推开
        [SerializeField, Min(0.01f)] private float slamSeconds = 0.25f;    // 监控过场结束后猛关
        [Tooltip("猛关前玩家离门洞多近要等（米，门洞内侧、全家福厅一侧）")]
        [SerializeField, Min(0f)] private float clearRadius = 2.2f;

        private const float OpenAngle = 90f;
        private const float ClosedTolerance = 0.5f;
        private const float SameFloorHeight = 2f;

        private Transform lockVisual;
        private FirstPersonController player;
        private SecurityMonitors monitors;

        private Vector3 closedPosition;
        private Quaternion closedRotation;
        private Vector3 hinge;      // 合页轴（门板南侧竖边、全家福厅那一面）
        private Vector3 inward;     // 从门墙指向全家福厅（+X）
        private float floorY;

        private float angle;        // 0 = 关，90 = 全开（绕合页往 +X 转）
        private bool moving;
        private bool slamMove;
        private float fromAngle, toAngle, moveTime, moveSeconds;
        private bool wasPlaying;
        private bool slamPending;   // 过场已放完，等玩家离开门扇扫过区再猛关

        private SecurityMonitors Monitors => monitors != null ? monitors : monitors = FindAnyObjectByType<SecurityMonitors>();

        private bool MonitorsPlaying => Monitors != null && Monitors.Playing;

        public string Prompt => GameFlow.HasKey ? "Unlock" : "Locked";

        public InteractKind Kind => InteractKind.Door;

        public bool CanInteract => angle < ClosedTolerance && !moving && !SceneTransitions.Busy;

        public Transform HighlightRoot => lockVisual;

        private void Awake()
        {
            lockVisual = transform.Find(LockName);
            closedPosition = transform.position;
            closedRotation = transform.rotation;
            inward = Vector3.Dot(transform.right, Vector3.right) >= 0f ? transform.right : -transform.right;

            // 门宽、厚度、底边都从门板自己的包围盒算（关着时轴对齐，投影就是本地尺寸 × 世界缩放）
            Vector3 size = transform.lossyScale;
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = GetComponentInChildren<MeshRenderer>();
            }

            if (renderer != null)
            {
                size = renderer.bounds.size;
                floorY = renderer.bounds.min.y;
            }
            else
            {
                floorY = closedPosition.y - transform.lossyScale.y * 0.5f;
            }

            float width = Mathf.Abs(Vector3.Dot(size, transform.forward));
            float thick = Mathf.Abs(Vector3.Dot(size, transform.right));
            float south = Vector3.Dot(transform.forward, Vector3.forward) >= 0f ? 1f : -1f;
            hinge = closedPosition - transform.forward * (width * 0.5f * south) + inward * (thick * 0.5f);
        }

        private void OnEnable()
        {
            GameFlow.StateChanged += OnStateChanged;
            GameOverScreen.Continued += OnRespawn;
        }

        private void OnDisable()
        {
            GameFlow.StateChanged -= OnStateChanged;
            GameOverScreen.Continued -= OnRespawn;
        }

        private void Start()
        {
            // 场景加载 / 读档：按当前状态直接摆好，不做动画
            wasPlaying = MonitorsPlaying;
            SnapToState();
        }

        private void Update()
        {
            ApplyLock();
            UpdateMotion();
            UpdateCutscene();
        }

        private void OnStateChanged(GameState from, GameState to)
        {
            // 结局期间保持当前姿态，不再跟着状态改
            if (to == GameState.Ending)
            {
                return;
            }

            SnapToState();
        }

        private void OnRespawn() => SnapToState();

        /// <summary>锁：没解锁就显示，解锁后隐藏（FamilyDoorUnlocked 不随检查点回滚，读档 / 重生自然一致）。</summary>
        private void ApplyLock()
        {
            bool shown = !GameFlow.FamilyDoorUnlocked;
            if (lockVisual != null && lockVisual.gameObject.activeSelf != shown)
            {
                lockVisual.gameObject.SetActive(shown);
            }
        }

        /// <summary>
        /// 按当前状态瞬间摆好：State &lt; AngelsActive → 开；AngelsActive 且监控在放 → 开（等放完再关）；
        /// AngelsActive 且监控不在放 且没解锁 → 关；已解锁 → 开；Ending 保持当前。
        /// </summary>
        private void SnapToState()
        {
            if (GameFlow.State == GameState.Ending)
            {
                return;
            }

            slamPending = false;
            if (GameFlow.FamilyDoorUnlocked || GameFlow.State < GameState.AngelsActive)
            {
                SnapOpen();
                return;
            }

            // AngelsActive：过场还在放就先开着（UpdateCutscene 会接住"放完"那一刻）
            if (MonitorsPlaying)
            {
                SnapOpen();
            }
            else
            {
                SnapClosed();
            }
        }

        private void UpdateCutscene()
        {
            bool playing = MonitorsPlaying;
            if (wasPlaying && !playing && GameFlow.State == GameState.AngelsActive && !GameFlow.FamilyDoorUnlocked)
            {
                // 监控过场刚放完：门该关了。玩家还在门扇扫过区就等，每帧检查
                slamPending = true;
            }

            wasPlaying = playing;

            if (!slamPending)
            {
                return;
            }

            if (GameFlow.FamilyDoorUnlocked || GameFlow.State != GameState.AngelsActive)
            {
                slamPending = false;
                SnapToState();
                return;
            }

            if (PlayerInSweep())
            {
                return;
            }

            slamPending = false;
            BeginMove(0f, slamSeconds, true);
            Sfx.PlayAt(Sfx.DoorSlam, closedPosition, 1f, 45f);
        }

        /// <summary>玩家胶囊是否在门洞内侧 clearRadius 米内、并且站在全家福厅一侧（同一层才算）。</summary>
        private bool PlayerInSweep()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<FirstPersonController>();
            }

            if (player == null)
            {
                return false;
            }

            Vector3 position = player.transform.position;
            if (Mathf.Abs(position.y - floorY) > SameFloorHeight)
            {
                return false;   // 不在同一层
            }

            Vector3 offset = position - closedPosition;
            if (Vector3.Dot(offset, inward) <= 0f)
            {
                return false;   // 还在走廊一侧，门板扫不到
            }

            offset.y = 0f;
            return offset.magnitude < clearRadius;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (!GameFlow.HasKey)
            {
                Sfx.PlayAt(Sfx.LockRattle, transform.position, 0.9f);
                Captions.Show("C_LockedDoor");
                return;
            }

            Sfx.PlayAt(Sfx.KeyClick, transform.position);
            GameFlow.FamilyDoorUnlocked = true;   // 钥匙不消耗：正门坏结局还要用
            BeginMove(OpenAngle, openSeconds, false);
            Sfx.PlayAt(Sfx.DoorCreak, transform.position, 0.8f, 30f);
        }

        private void BeginMove(float target, float seconds, bool slam)
        {
            if (Mathf.Approximately(angle, target))
            {
                angle = target;
                moving = false;
                Apply();
                return;
            }

            fromAngle = angle;
            toAngle = target;
            moveTime = 0f;
            moveSeconds = Mathf.Max(0.01f, seconds);
            slamMove = slam;
            moving = true;
        }

        private void UpdateMotion()
        {
            if (!moving)
            {
                return;
            }

            moveTime += Time.deltaTime;
            float k = Mathf.Clamp01(moveTime / moveSeconds);
            // 关门是猛的一下（先慢后快），开门是缓缓推开
            k = slamMove ? k * k : Mathf.SmoothStep(0f, 1f, k);
            angle = Mathf.Lerp(fromAngle, toAngle, k);

            if (moveTime >= moveSeconds)
            {
                angle = toAngle;
                moving = false;
            }

            Apply();
        }

        private void SnapOpen()
        {
            moving = false;
            angle = OpenAngle;
            Apply();
        }

        private void SnapClosed()
        {
            moving = false;
            angle = 0f;
            Apply();
        }

        /// <summary>绕合页竖边转，+angle 把门板从门洞方向（本地 +Z）转向全家福厅（+X）。</summary>
        private void Apply()
        {
            Quaternion delta = Quaternion.AngleAxis(angle, Vector3.up);
            transform.rotation = delta * closedRotation;
            transform.position = hinge + delta * (closedPosition - hinge);
        }
    }
}
