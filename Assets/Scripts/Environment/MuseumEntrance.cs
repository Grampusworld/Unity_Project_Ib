using Ib.Flow;
using Ib.Interaction;
using Ib.UI;
using UnityEngine;

namespace Ib.Environment
{
    /// <summary>
    /// 序章大门：黑屋 → 画中馆的无缝衔接（GDD §3.2 第 1 步）。
    /// 1. 黑屋阶段只开接待厅的灯（带阴影），其余馆内灯先关，避免无阴影点光穿墙照亮黑屋；门缝光是一条自发光几何，不受开关影响；
    /// 2. 玩家按 E 推门 → 馆内灯全开，门缓缓打开，不锁控制；
    /// 3. 门完全打开、玩家走进馆内后，每帧检查门是否还在视野里（视锥 + 射线遮挡，R4-2 同一思路）；
    ///    一旦看不见：门瞬间关死、上锁，黑屋整块停用 —— 通道关闭。
    /// 门的 +Z（本物体的 forward）= 美术馆里面。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MuseumEntrance : MonoBehaviour
    {
        [SerializeField] private DoubleDoor door;
        [SerializeField] private GameObject blackRoom;
        [Tooltip("黑屋阶段要关掉的馆内灯组（推门时打开）。")]
        [SerializeField] private GameObject[] deferredLights = new GameObject[0];
        [Tooltip("门洞尺寸：宽、高、墙厚。用于视野检测。")]
        [SerializeField] private Vector3 doorwaySize = new Vector3(3f, 3.6f, 0.3f);
        [Tooltip("玩家离门平面多远（向馆内）才算进了美术馆。")]
        [SerializeField, Min(0f)] private float insideDistance = 1.0f;
        [SerializeField] private LayerMask occluders = ~0;

        public bool IsSealed { get; private set; }

        private Camera viewer;
        private bool museumLightsOn = true;
        private Plane[] frustum = new Plane[6];
        private readonly Vector3[] samples = new Vector3[32];

        private void Awake()
        {
            SetMuseumLights(false);
        }

        private void OnEnable()
        {
            if (door != null)
            {
                door.OpeningStarted += OnDoorOpening;
            }
        }

        private void OnDisable()
        {
            if (door != null)
            {
                door.OpeningStarted -= OnDoorOpening;
            }
        }

        private void Update()
        {
            // 结局 CG 会重新打开这扇门，不能再按序章逻辑关门
            if (IsSealed || door == null || GameFlow.State == GameState.Ending)
            {
                return;
            }

            if (viewer == null)
            {
                viewer = Camera.main;
                if (viewer == null)
                {
                    return;
                }
            }

            bool inside = IsInsideMuseum(viewer.transform.position);

            // 直接传送进馆（调试检查点）时也要有灯
            if (inside && !museumLightsOn)
            {
                SetMuseumLights(true);
            }

            if (inside && door.IsOpen && !IsDoorVisible())
            {
                Seal();
            }
        }

        /// <summary>调试传送前调用：传回黑屋时把序章状态恢复（黑屋启用、门可推、馆灯关）。</summary>
        public void PrepareTeleport(Vector3 destination)
        {
            if (IsInsideMuseum(destination))
            {
                SetMuseumLights(true);
                return;
            }

            IsSealed = false;
            if (blackRoom != null)
            {
                blackRoom.SetActive(true);
            }

            if (door != null)
            {
                door.CloseInstant();
                door.SetLocked(false);
            }

            SetMuseumLights(false);
        }

        private void OnDoorOpening()
        {
            // 结局 CG 开门：停电后不再来电
            if (IsSealed || GameFlow.State == GameState.Ending)
            {
                return;
            }

            SetMuseumLights(true);

            // 第 4 步：推门 = 进入画中馆（全家福只在 PaintedGallery 时能交互）
            if (GameFlow.State == GameState.BlackRoom)
            {
                GameFlow.SetState(GameState.PaintedGallery);
                GameFlow.ShowHintOnce(HintId.Explore);
            }
        }

        private void Seal()
        {
            IsSealed = true;
            door.CloseInstant();
            door.SetLocked(true);

            if (blackRoom != null)
            {
                blackRoom.SetActive(false);
            }

            Debug.Log("[MuseumEntrance] 大门已关闭，黑屋通道关闭。", this);
        }

        private bool IsInsideMuseum(Vector3 position)
        {
            return Vector3.Dot(position - transform.position, transform.forward) > insideDistance;
        }

        private void SetMuseumLights(bool on)
        {
            museumLightsOn = on;
            foreach (GameObject group in deferredLights)
            {
                if (group != null)
                {
                    group.SetActive(on);
                }
            }
        }

        // ---------------------------------------------------------------- 视野检测

        private bool IsDoorVisible()
        {
            Bounds bounds = new Bounds(transform.position + transform.up * (doorwaySize.y * 0.5f), doorwaySize);
            Renderer[] renderers = door.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }

            GeometryUtility.CalculateFrustumPlanes(viewer, frustum);
            if (!GeometryUtility.TestPlanesAABB(frustum, bounds))
            {
                return false;
            }

            int count = CollectSamples(renderers);
            Vector3 eye = viewer.transform.position;

            for (int i = 0; i < count; i++)
            {
                Vector3 p = samples[i];
                Vector3 vp = viewer.WorldToViewportPoint(p);
                if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f)
                {
                    continue;
                }

                Vector3 dir = p - eye;
                float dist = dir.magnitude;
                if (dist < 0.01f)
                {
                    return true;
                }

                if (!Physics.Raycast(eye, dir / dist, out RaycastHit hit, dist - 0.05f, occluders, QueryTriggerInteraction.Ignore))
                {
                    return true;
                }

                // 打到门板本身也算看见门
                if (hit.collider.transform.IsChildOf(door.transform))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>门洞平面 3×3 网格 + 每块门板的中心和四角。</summary>
        private int CollectSamples(Renderer[] renderers)
        {
            int n = 0;
            Vector3 origin = transform.position;
            for (int ix = 0; ix < 3; ix++)
            {
                for (int iy = 0; iy < 3; iy++)
                {
                    float x = (ix * 0.5f - 0.5f) * doorwaySize.x * 0.95f;
                    float y = (0.025f + iy * 0.475f) * doorwaySize.y;
                    samples[n++] = origin + transform.right * x + transform.up * y;
                }
            }

            foreach (Renderer r in renderers)
            {
                if (n + 5 > samples.Length)
                {
                    break;
                }

                Transform t = r.transform;
                samples[n++] = t.position;
                samples[n++] = t.TransformPoint(new Vector3(-0.45f, -0.45f, 0f));
                samples[n++] = t.TransformPoint(new Vector3(0.45f, -0.45f, 0f));
                samples[n++] = t.TransformPoint(new Vector3(-0.45f, 0.45f, 0f));
                samples[n++] = t.TransformPoint(new Vector3(0.45f, 0.45f, 0f));
            }

            return n;
        }
    }
}
