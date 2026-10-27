using System.Collections.Generic;
using Ib.Core;
using Ib.Environment;
using Ib.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Ib.Threats
{
    /// <summary>
    /// 哭泣天使（§4.1 / §4.2）。三个阶段，每个阶段换一套外形：
    /// - Still  静止：双手捂脸站在展台上，什么都不做。
    /// - Peek   偷看：松手回看。不被注视时原地慢慢转身朝向玩家，被注视时不动。
    /// - Attack 攻击：下展台，沿固定路线在一层巡逻；发现玩家后追击，碰到玩家就抓住（黑屏 GAME OVER）。
    ///
    /// 注视规则（R4-1）：玩家看着它时它完全不动。"看着" = 在相机视锥内 + 视线没被挡 + 被手电照着。
    /// 手电关着、或者追击时闪烁灭掉的那一拍，都不算看见。
    /// 攻击阶段里连续盯着它超过 3 秒，它会直接进入追击（手电随之开始闪），所以不能靠一直盯着拖住它。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class WeepingAngel : MonoBehaviour
    {
        public enum Phase { Still, Peek, Attack }

        [SerializeField] private Phase phase = Phase.Still;

        [Header("外形（每个阶段一个子物体）")]
        [SerializeField] private GameObject stillVisual;
        [SerializeField] private GameObject peekVisual;
        [SerializeField] private GameObject attackVisual;

        [Header("注视判定")]
        [Tooltip("身上的采样点（本地坐标）：任意一个点被看见就算被注视。")]
        [SerializeField] private Vector3[] samplePoints =
        {
            new Vector3(0f, 1.75f, 0f), new Vector3(0f, 1.25f, 0f), new Vector3(0f, 0.6f, 0f),
            new Vector3(0.3f, 1.2f, 0f), new Vector3(-0.3f, 1.2f, 0f), new Vector3(0f, 0.1f, 0f),
        };

        [Header("偷看")]
        [SerializeField, Min(0f)] private float peekTurnSpeed = 25f;

        [Header("巡逻 / 追击")]
        [SerializeField] private WaypointPath patrolPath;
        [SerializeField, Min(0f)] private float patrolSpeed = 1.8f;
        [SerializeField, Min(0f)] private float chaseSpeed = 4.6f;
        [SerializeField, Min(0f)] private float detectRange = 14f;
        [SerializeField, Range(10f, 360f)] private float detectFov = 140f;
        [Tooltip("贴得这么近时，不用看见也能察觉到玩家。")]
        [SerializeField, Min(0f)] private float senseRange = 3f;
        [Tooltip("丢失视线这么久后放弃追击，回到巡逻路线。")]
        [SerializeField, Min(0f)] private float loseTargetSeconds = 6f;
        [Tooltip("攻击阶段里玩家连续盯着它超过这么久，它就不再老实：手电开始闪，它开始追。")]
        [SerializeField, Min(0f)] private float stareLimitSeconds = 3f;
        [SerializeField, Min(0.1f)] private float catchDistance = 0.9f;
        [SerializeField] private Vector3 eyeOffset = new Vector3(0f, 1.7f, 0f);
        [Tooltip("进入攻击阶段时，从展台中心往正前方走多远落地。")]
        [SerializeField, Min(0f)] private float stepOffPlinth = 1.1f;

        /// <summary>当前阶段。</summary>
        public Phase CurrentPhase => phase;

        /// <summary>这一帧是否被玩家注视着。</summary>
        public bool IsObserved { get; private set; }

        /// <summary>攻击阶段里是否在追玩家。</summary>
        public bool IsChasing { get; private set; }

        public static readonly List<WeepingAngel> All = new List<WeepingAngel>();

        private NavMeshAgent agent;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private int patrolIndex;
        private int patrolDirection = 1;
        private float lastSeenPlayerTime = -999f;
        private float observedSince = -1f;

        private FirstPersonController player;
        private Camera playerCamera;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.enabled = false;
            homePosition = transform.position;
            homeRotation = transform.rotation;
        }

        private void OnEnable() => All.Add(this);

        private void OnDisable()
        {
            All.Remove(this);
            RefreshFlashlightFlicker();
        }

        private void Start()
        {
            SetPhase(phase, true);
        }

        // ================================================================ 阶段切换

        public void SetPhase(Phase next) => SetPhase(next, false);

        private void SetPhase(Phase next, bool force)
        {
            if (next == phase && !force)
            {
                return;
            }

            phase = next;
            IsChasing = false;

            SetVisual(stillVisual, phase == Phase.Still);
            SetVisual(peekVisual, phase == Phase.Peek);
            SetVisual(attackVisual, phase == Phase.Attack);

            if (phase == Phase.Attack)
            {
                StartAttack();
            }
            else
            {
                // 回到展台上的原位；静止阶段连朝向一起复原，偷看阶段保留当前朝向
                agent.enabled = false;
                transform.SetPositionAndRotation(homePosition, phase == Phase.Still ? homeRotation : transform.rotation);
            }

            RefreshFlashlightFlicker();
        }

        private static void SetVisual(GameObject go, bool on)
        {
            if (go != null)
            {
                go.SetActive(on);
            }
        }

        private void StartAttack()
        {
            // 从展台正前方下地（展台 1.2 m 见方），找最近的导航网格点
            Vector3 start = homePosition + homeRotation * Vector3.forward * stepOffPlinth;
            start.y = 0f;
            if (NavMesh.SamplePosition(start, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                start = hit.position;
            }

            agent.enabled = true;
            if (!agent.Warp(start))
            {
                Debug.LogWarning("[WeepingAngel] 附近没有导航网格，先用 Tools/Ib/布置哭泣天使 烘焙一次。", this);
                return;
            }

            agent.speed = patrolSpeed;
            patrolIndex = NearestWaypoint();
            patrolDirection = 1;
            GoToWaypoint();
        }

        // ================================================================ 每帧

        private void Update()
        {
            ResolvePlayer();
            IsObserved = ComputeObserved();
            if (!IsObserved)
            {
                observedSince = -1f;
            }
            else if (observedSince < 0f)
            {
                observedSince = Time.time;
            }

            switch (phase)
            {
                case Phase.Peek:
                    UpdatePeek();
                    break;
                case Phase.Attack:
                    UpdateAttack();
                    break;
            }
        }

        private void UpdatePeek()
        {
            if (IsObserved || player == null)
            {
                return;
            }

            Vector3 to = player.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f)
            {
                return;
            }

            Quaternion target = Quaternion.LookRotation(to);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, peekTurnSpeed * Time.deltaTime);
        }

        private void UpdateAttack()
        {
            if (!agent.enabled || !agent.isOnNavMesh || GameOverScreen.IsShowing)
            {
                return;
            }

            // 被看着：原地定住，连惯性都不留。
            // 但不能一直盯着它耗下去：连续盯满 stareLimitSeconds 秒就转入追击 —— 追击会让手电闪烁，
            // 灭的那一拍不算注视，它就趁机逼近
            if (IsObserved)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;

                if (!IsChasing && Time.time - observedSince >= stareLimitSeconds)
                {
                    lastSeenPlayerTime = Time.time;
                    IsChasing = true;
                    agent.speed = chaseSpeed;
                    RefreshFlashlightFlicker();
                }
                return;
            }

            agent.isStopped = false;

            bool canSee = CanSensePlayer();
            if (canSee)
            {
                lastSeenPlayerTime = Time.time;
            }

            bool chasing = player != null && Time.time - lastSeenPlayerTime < loseTargetSeconds;
            if (chasing != IsChasing)
            {
                IsChasing = chasing;
                RefreshFlashlightFlicker();
                if (!chasing)
                {
                    agent.speed = patrolSpeed;
                    patrolIndex = NearestWaypoint();
                    GoToWaypoint();
                }
            }

            if (IsChasing)
            {
                agent.speed = chaseSpeed;
                agent.SetDestination(player.transform.position);

                Vector3 flat = player.transform.position - transform.position;
                flat.y = 0f;
                if (flat.magnitude < catchDistance)
                {
                    CatchPlayer();
                }
            }
            else if (!agent.pathPending && agent.remainingDistance < 0.4f)
            {
                AdvanceWaypoint();
                GoToWaypoint();
            }
        }

        // ================================================================ 注视

        private bool ComputeObserved()
        {
            PlayerFlashlight flashlight = PlayerFlashlight.Instance;
            if (playerCamera == null || flashlight == null || !flashlight.IsLit)
            {
                return false;
            }

            Vector3 eye = playerCamera.transform.position;
            foreach (Vector3 local in samplePoints)
            {
                Vector3 p = transform.TransformPoint(local);

                Vector3 vp = playerCamera.WorldToViewportPoint(p);
                if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f)
                {
                    continue;
                }

                if (flashlight.Blocked(eye, p, transform))
                {
                    continue;
                }

                if (flashlight.IsIlluminating(p, transform))
                {
                    return true;
                }
            }

            return false;
        }

        private bool CanSensePlayer()
        {
            if (player == null || playerCamera == null)
            {
                return false;
            }

            Vector3 eye = transform.position + eyeOffset;
            Vector3 target = playerCamera.transform.position;
            Vector3 to = target - eye;
            float distance = to.magnitude;

            if (distance <= senseRange)
            {
                return true;
            }

            if (distance > detectRange)
            {
                return false;
            }

            Vector3 flatTo = new Vector3(to.x, 0f, to.z);
            if (Vector3.Angle(transform.forward, flatTo) > detectFov * 0.5f)
            {
                return false;
            }

            PlayerFlashlight flashlight = PlayerFlashlight.Instance;
            return flashlight == null || !flashlight.Blocked(eye, target, transform);
        }

        private void CatchPlayer()
        {
            Debug.Log("[WeepingAngel] 抓到玩家了 → GAME OVER。", this);

            // 黑屏 + Continue；玩家点 Continue 才在检查点重生（GameOverScreen 负责）
            GameOverScreen.Show();

            lastSeenPlayerTime = -999f;
            IsChasing = false;
            RefreshFlashlightFlicker();

            // 回到巡逻起点，免得玩家一重生又贴脸
            if (patrolPath != null && patrolPath.Count > 0)
            {
                agent.Warp(patrolPath.GetPoint(0));
                patrolIndex = 0;
            }
            agent.speed = patrolSpeed;
            GoToWaypoint();
        }

        // ================================================================ 巡逻

        private void GoToWaypoint()
        {
            if (patrolPath == null || patrolPath.Count == 0 || !agent.isOnNavMesh)
            {
                return;
            }

            agent.SetDestination(patrolPath.GetPoint(patrolIndex));
        }

        private void AdvanceWaypoint()
        {
            if (patrolPath == null || patrolPath.Count < 2)
            {
                return;
            }

            if (patrolPath.PingPong)
            {
                if (patrolIndex + patrolDirection >= patrolPath.Count || patrolIndex + patrolDirection < 0)
                {
                    patrolDirection = -patrolDirection;
                }
                patrolIndex += patrolDirection;
            }
            else
            {
                patrolIndex = (patrolIndex + 1) % patrolPath.Count;
            }
        }

        private int NearestWaypoint()
        {
            if (patrolPath == null || patrolPath.Count == 0)
            {
                return 0;
            }

            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < patrolPath.Count; i++)
            {
                float d = (patrolPath.GetPoint(i) - transform.position).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        // ================================================================ 工具

        private void ResolvePlayer()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<FirstPersonController>();
            }

            if (playerCamera == null && player != null)
            {
                playerCamera = player.GetComponentInChildren<Camera>();
            }
        }

        /// <summary>任何一个天使在追，手电就闪。</summary>
        private static void RefreshFlashlightFlicker()
        {
            PlayerFlashlight flashlight = PlayerFlashlight.Instance;
            if (flashlight == null)
            {
                return;
            }

            bool anyChasing = false;
            foreach (WeepingAngel angel in All)
            {
                anyChasing |= angel.IsChasing;
            }
            flashlight.Flickering = anyChasing;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsObserved ? Color.cyan : Color.red;
            foreach (Vector3 local in samplePoints)
            {
                Gizmos.DrawWireSphere(transform.TransformPoint(local), 0.06f);
            }

            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position + eyeOffset, detectRange);
        }
#endif
    }
}
