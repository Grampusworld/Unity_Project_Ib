using System.Collections.Generic;
using Ib.Core;
using Ib.Flow;
using Ib.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Ib.Threats
{
    /// <summary>
    /// 二楼名画人物（场景里的 PaintingFigure_A / PaintingFigure_B）。
    /// 拿到手电才行动（ThreatSchedule.CollectionOut）。出动后在二楼游荡、往玩家所在的区域靠拢；看见玩家就全速追，直到抓住。
    /// 只管二楼：玩家下了楼梯（不在这层楼面上）就看不见，追到楼梯口为止（二楼导航网格不含楼梯）。
    /// 进不了全家福厅（noGo，GDD §6.9「它们停在门口，没有跟进来」）：玩家躲进去，它们追到门口就回去游荡。
    /// 蹲在二楼展柜（DisplayCase）后面时视线被柜身挡住，不会因此发现玩家；
    /// 已经在追的话，视线断了也不追踪当前位置，只走到玩家消失的那一点。
    /// 站着时展柜不算掩体（柜身比眼睛高，但玩法上只有蹲下才躲得住）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaintingFigure : MonoBehaviour
    {
        public static readonly List<PaintingFigure> All = new List<PaintingFigure>();

        private enum Mode
        {
            Idle,
            Roam,
            Chase,
            Search,
        }

        [SerializeField] private GameObject stillVisual;
        [SerializeField] private GameObject outVisual;

        [Header("追击")]
        [SerializeField, Min(0.5f)] private float chaseSpeed = 3.6f;
        [SerializeField, Min(1f)] private float viewRange = 14f;
        [SerializeField, Range(10f, 180f)] private float viewAngle = 120f;
        [SerializeField, Min(0.2f)] private float catchDistance = 0.9f;
        [SerializeField, Min(0.2f)] private float closeSense = 1.6f;

        [Header("游荡")]
        [SerializeField, Min(0.5f)] private float roamSpeed = 2.0f;
        [Tooltip("游荡时在玩家（不在二楼时是原位）周围多大范围里挑下一个点。")]
        [SerializeField, Min(1f)] private float roamRadius = 8f;
        [Tooltip("不许进入的区域（世界坐标，只看 XZ）：全家福厅。由「布置后半段」写入，大小为 0 时不限制。")]
        [SerializeField] private Bounds noGo;

        public bool IsActive { get; private set; }

        /// <summary>正在追玩家，或正在赶往玩家消失的地点。</summary>
        public bool IsChasing => mode == Mode.Chase || mode == Mode.Search;

        private Mode mode;
        private Vector3 lastSeen;
        private Vector3 roamTarget;
        private float stuckTime;
        private NavMeshPath probePath;
        private float floorY;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private NavMeshAgent agent;
        private FirstPersonController player;
        private float eyeHeight;
        private bool warnedOffMesh;

        private void Awake()
        {
            homePosition = transform.position;
            homeRotation = transform.rotation;

            // 占位胶囊生成时拆了碰撞体。补回来，玩家才撞得上，射线也能打到它自己（判定时会跳过）。
            if (GetComponent<Collider>() == null)
            {
                gameObject.AddComponent<CapsuleCollider>();
            }

            Renderer rend = GetComponent<Renderer>();
            eyeHeight = rend != null ? rend.bounds.extents.y * 0.72f : 0.9f;
            floorY = rend != null ? rend.bounds.min.y : transform.position.y - 1.1f;

            agent = GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                agent = gameObject.AddComponent<NavMeshAgent>();
            }

            agent.radius = 0.32f;
            agent.height = 1.6f;
            agent.baseOffset = rend != null ? rend.bounds.extents.y : 1.1f;
            agent.speed = chaseSpeed;
            agent.acceleration = 16f;
            agent.angularSpeed = 420f;
            agent.stoppingDistance = 0.35f;
            agent.autoBraking = false;
            agent.enabled = false;
        }

        private void OnEnable()
        {
            All.Add(this);
            GameFlow.StateChanged += OnStateChanged;
            GameFlow.FlashlightObtained += Refresh;
            GameOverScreen.Continued += ResetForRespawn;
            Refresh();
        }

        private void OnDisable()
        {
            All.Remove(this);
            GameFlow.StateChanged -= OnStateChanged;
            GameFlow.FlashlightObtained -= Refresh;
            GameOverScreen.Continued -= ResetForRespawn;
            if (agent != null)
            {
                agent.enabled = false;
            }
        }

        private void OnStateChanged(GameState from, GameState to) => Refresh();

        private void ResetForRespawn()
        {
            mode = Mode.Idle;
            transform.SetPositionAndRotation(homePosition, homeRotation);
            if (agent != null)
            {
                PlaceOnMesh(homePosition);
                if (agent.isOnNavMesh)
                {
                    agent.ResetPath();
                }
            }

            Refresh();
        }

        private void Refresh()
        {
            IsActive = ThreatSchedule.CollectionOut;
            if (stillVisual != null)
            {
                stillVisual.SetActive(!IsActive);
            }

            if (outVisual != null)
            {
                outVisual.SetActive(IsActive);
            }

            if (!IsActive)
            {
                mode = Mode.Idle;
                if (agent != null && agent.enabled)
                {
                    agent.ResetPath();
                    agent.enabled = false;
                }
            }
        }

        private void Update()
        {
            if (!IsActive || GameOverScreen.IsShowing)
            {
                return;
            }

            if (player == null)
            {
                player = FindAnyObjectByType<FirstPersonController>();
                if (player == null)
                {
                    return;
                }
            }

            EnsureAgent();

            bool seen = OnThisFloor(player.transform.position) && !InNoGo(player.transform.position) && CanSeePlayer();
            if (seen)
            {
                lastSeen = player.transform.position;
                mode = Mode.Chase;
                GoTo(player.transform.position, chaseSpeed);
                if (FlatDistance(player.transform.position) <= catchDistance)
                {
                    Catch();
                }

                return;
            }

            switch (mode)
            {
                case Mode.Chase:
                    // 视线断在这一帧：不再跟玩家的新位置，只去他刚才站的地方（楼梯上 / 全家福厅门口也只到网格边上）
                    mode = Mode.Search;
                    GoTo(lastSeen, chaseSpeed);
                    break;
                case Mode.Search:
                    GoTo(lastSeen, chaseSpeed);
                    if (Arrived(lastSeen))
                    {
                        StartRoam();
                    }

                    break;
                case Mode.Roam:
                    GoTo(roamTarget, roamSpeed);
                    if (Arrived(roamTarget))
                    {
                        StartRoam();
                    }

                    break;
                default:
                    StartRoam();
                    break;
            }
        }

        private void StartRoam()
        {
            mode = Mode.Roam;
            stuckTime = 0f;
            roamTarget = PickRoamPoint();
        }

        /// <summary>玩家在二楼（且不在全家福厅里）时围着玩家挑点，否则围着原位挑点。挑不到就回原位。</summary>
        private Vector3 PickRoamPoint()
        {
            Vector3 p = player.transform.position;
            Vector3 center = OnThisFloor(p) && !InNoGo(p) ? p : homePosition;
            for (int i = 0; i < 12; i++)
            {
                Vector2 r = Random.insideUnitCircle * roamRadius;
                Vector3 candidate = new Vector3(center.x + r.x, floorY, center.z + r.y);
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas)
                    && Mathf.Abs(hit.position.y - floorY) < 0.6f
                    && !InNoGo(hit.position)
                    && Reachable(hit.position))
                {
                    return hit.position;
                }
            }

            return homePosition;
        }

        /// <summary>二楼网格里还有走不进去的块（锁着的未开放区）：只挑走得到的点。</summary>
        private bool Reachable(Vector3 point)
        {
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                return true;
            }

            probePath ??= new NavMeshPath();
            return NavMesh.CalculatePath(agent.nextPosition, point, NavMesh.AllAreas, probePath)
                && probePath.status == NavMeshPathStatus.PathComplete;
        }

        private bool OnThisFloor(Vector3 feet) => Mathf.Abs(feet.y - floorY) < 1.5f;

        private bool InNoGo(Vector3 world)
        {
            return noGo.size.x > 0f
                && world.x >= noGo.min.x && world.x <= noGo.max.x
                && world.z >= noGo.min.z && world.z <= noGo.max.z;
        }

        /// <summary>到了，或者路只能走到这儿（目标在楼梯上、网格外）。</summary>
        private bool Arrived(Vector3 target)
        {
            if (agent.enabled && agent.isOnNavMesh)
            {
                if (agent.pathPending)
                {
                    return false;
                }

                // 卡住（只能走到网格边上、目标在楼梯或禁区里）超过 1 秒也算到了，换下一个点
                stuckTime = agent.velocity.sqrMagnitude < 0.01f ? stuckTime + Time.deltaTime : 0f;
                return !agent.hasPath || stuckTime > 1f || agent.remainingDistance <= agent.stoppingDistance + 0.4f;
            }

            return FlatDistance(target) <= 0.75f;
        }

        private void EnsureAgent()
        {
            if (agent.enabled && agent.isOnNavMesh)
            {
                return;
            }

            PlaceOnMesh(transform.position);
        }

        /// <summary>
        /// 网格的枢轴在胶囊中心。Warp 要给的是枢轴位置，所以在导航点上再抬一个 baseOffset，脚才贴着楼面。
        /// </summary>
        private void PlaceOnMesh(Vector3 around)
        {
            agent.enabled = true;
            Vector3 probe = around;
            RaycastHit ground;
            if (Physics.Raycast(around + Vector3.up * 3f, Vector3.down, out ground, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && ground.transform != transform
                && !ground.transform.IsChildOf(transform))
            {
                probe = ground.point;
            }

            if (NavMesh.SamplePosition(probe, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position + Vector3.up * agent.baseOffset);
            }
            else if (!warnedOffMesh)
            {
                warnedOffMesh = true;
                Debug.LogWarning("[PaintingFigure] 脚下没有二楼导航网格，改为直线走。", this);
            }
        }

        private void GoTo(Vector3 world, float speed)
        {
            agent.speed = speed;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(world);
                return;
            }

            Vector3 to = world - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist < 0.05f)
            {
                return;
            }

            Vector3 dir = to / dist;
            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, agent.radius + 0.35f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(player.transform))
            {
                dir = Vector3.Cross(Vector3.up, dir);
            }

            transform.position += dir * Mathf.Min(dist, speed * Time.deltaTime);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), agent.angularSpeed * Time.deltaTime);
        }

        private bool CanSeePlayer()
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 target = player.GetComponentInChildren<Camera>() != null
                ? player.GetComponentInChildren<Camera>().transform.position
                : player.transform.position + Vector3.up * 1.1f;

            Vector3 to = target - eye;
            float distance = to.magnitude;
            if (distance < 0.05f)
            {
                return true;
            }

            Vector3 flat = new Vector3(to.x, 0f, to.z);
            bool inView = distance <= closeSense
                || (distance <= viewRange && Vector3.Angle(transform.forward, flat) <= viewAngle * 0.5f);
            if (!inView)
            {
                return false;
            }

            RaycastHit[] hits = Physics.RaycastAll(eye, to / distance, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Transform hit = hits[i].transform;
                if (hit == transform || hit.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.IsChildOf(player.transform))
                {
                    return true;
                }

                // 蹲下才把展柜当掩体；站着时从柜顶被看见
                if (IsDisplayCase(hit))
                {
                    if (player.IsCrouching)
                    {
                        return false;
                    }

                    continue;
                }

                return false;
            }

            return true;
        }

        private static bool IsDisplayCase(Transform t)
        {
            while (t != null)
            {
                if (t.name.StartsWith("DisplayCase"))
                {
                    return true;
                }

                t = t.parent;
            }

            return false;
        }

        private float FlatDistance(Vector3 world)
        {
            Vector3 a = transform.position;
            a.y = 0f;
            world.y = 0f;
            return Vector3.Distance(a, world);
        }

        private void Catch()
        {
            mode = Mode.Idle;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }

            Debug.Log("[PaintingFigure] 抓到玩家了 → GAME OVER。", this);
            GameOverScreen.Show();
        }
    }
}
