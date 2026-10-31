using Ib.Core;
using Ib.Flow;
using Ib.Player;
using UnityEngine;

namespace Ib.Interaction
{
    /// <summary>
    /// 值班室电磁锁门（GDD §3.6 第 10 步）：停电"咔"地释放，门以靠墙一侧竖边为轴向值班室（−X）内转 90° 打开。
    /// 拿到手电后玩家走进值班室 → 门猛地关上（砰）；监控过场结束 → 吱呀着再缓缓打开。
    /// 合页边、门宽从门板自己的 Renderer 包围盒算，不依赖生成器传参；关着时的位置和朝向不变。
    /// 读档/重生：状态 &lt; PowerOut 瞬间关，PowerOut 及以上瞬间开着（PowerOut 允许再关一次）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MagLockDoor : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float openSeconds = 2.5f;          // 停电开门
        [SerializeField, Min(0.01f)] private float slamSeconds = 0.15f;         // 玩家进屋后猛地关上
        [SerializeField, Min(0.01f)] private float cutsceneOpenSeconds = 3f;    // 监控过场结束后开门
        [SerializeField, Min(0f)] private float enterDistance = 2.2f;           // 玩家离门墙多深算"进了值班室"（> 门宽 1.68，猛关时门板不会扫过玩家）

        private const float OpenAngle = 90f;
        private const float SameFloorHeight = 2f;

        private FirstPersonController player;
        private SecurityMonitors monitors;

        private Vector3 closedPosition;
        private Quaternion closedRotation;
        private Vector3 hinge;      // 合页轴（靠墙一侧竖边、门板朝室内那面）
        private Vector3 inward;     // 从门墙指向值班室
        private float floorY;

        private float angle;        // 0 = 关，90 = 全开（绕合页往 -X 转）
        private bool moving;
        private bool slamMove;
        private float fromAngle, toAngle, moveTime, moveSeconds;
        private bool slammed;       // 已经关过一次（b），等过场结束再开
        private bool wasPlaying;

        private SecurityMonitors Monitors => monitors != null ? monitors : monitors = FindAnyObjectByType<SecurityMonitors>();

        private void Awake()
        {
            closedPosition = transform.position;
            closedRotation = transform.rotation;
            inward = -transform.right;

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
            // 场景加载时直接按当前状态摆好（读档 / 重生）
            slammed = false;
            if (GameFlow.State >= GameState.PowerOut)
            {
                SnapOpen();
            }
            else
            {
                SnapClosed();
            }
        }

        private void Update()
        {
            UpdateMotion();
            UpdatePlayer();
            UpdateCutscene();
        }

        private void OnStateChanged(GameState from, GameState next)
        {
            if (next < GameState.PowerOut)
            {
                slammed = false;
                SnapClosed();
                return;
            }

            if (next == GameState.PowerOut)
            {
                slammed = false;
                if (from < GameState.PowerOut)
                {
                    // 断电那一刻锁释放的"咔"（GDD §3.6 第 10 步：玩家在楼梯中段远远听见），门缓缓向里开
                    Sfx.PlayAt(Sfx.PowerClunk, closedPosition, 1f, 45f);
                    BeginMove(OpenAngle, openSeconds, false);
                }
                else
                {
                    SnapOpen();
                }

                return;
            }

            // AngelsActive 及以后：过场还在放就等它结束（d），否则瞬间开着（读档 / 重生）
            if (Monitors != null && Monitors.Playing)
            {
                return;
            }

            slammed = false;
            SnapOpen();
        }

        private void OnRespawn()
        {
            // 死回 PowerOut 检查点时按"开着"处理，并允许进屋再关一次
            slammed = false;
            if (GameFlow.State >= GameState.PowerOut)
            {
                SnapOpen();
            }
            else
            {
                SnapClosed();
            }
        }

        private void UpdatePlayer()
        {
            if (slammed || GameFlow.State != GameState.PowerOut || !GameFlow.HasFlashlight || GameOverScreen.IsShowing)
            {
                return;
            }

            if (player == null)
            {
                player = FindAnyObjectByType<FirstPersonController>();
            }

            if (player == null || angle < OpenAngle - 0.5f)
            {
                return;
            }

            Vector3 position = player.transform.position;
            if (Mathf.Abs(position.y - floorY) > SameFloorHeight)
            {
                return; // 不在同一层
            }

            if (Vector3.Dot(position - closedPosition, inward) < enterDistance)
            {
                return; // 还没走进值班室
            }

            slammed = true;
            BeginMove(0f, slamSeconds, true);
            Sfx.PlayAt(Sfx.DoorSlam, closedPosition, 1f, 45f);
        }

        private void UpdateCutscene()
        {
            SecurityMonitors m = Monitors;
            bool playing = m != null && m.Playing;
            if (wasPlaying && !playing)
            {
                // 监控过场放完：门缓缓打开，合页吱呀一声
                if (GameFlow.State >= GameState.PowerOut && angle < OpenAngle - 0.5f)
                {
                    slammed = false;
                    BeginMove(OpenAngle, cutsceneOpenSeconds, false);
                    Sfx.PlayAt(Sfx.DoorCreak, closedPosition, 0.8f, 30f);
                }
            }

            wasPlaying = playing;
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

        private void Apply()
        {
            Quaternion delta = Quaternion.AngleAxis(-angle, Vector3.up);
            transform.rotation = delta * closedRotation;
            transform.position = hinge + delta * (closedPosition - hinge);
        }
    }
}
