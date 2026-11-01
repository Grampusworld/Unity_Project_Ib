using System.Collections.Generic;
using Ib.Core;
using Ib.Environment;
using Ib.Flow;
using Ib.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Ib.Threats
{
    /// <summary>
    /// 哭泣天使（§4.1 / §4.2）。三个阶段，每个阶段换一套外形：
    /// - Still  静止：双手捂脸站在展台上，什么都不做。
    /// - Peek   偷看：松手回看。不被注视时原地慢慢转身朝向玩家，被注视时不动。
    /// - Attack 攻击：下展台，沿固定路线在一层巡逻；发现玩家后追击，碰到玩家就抓住（jump scare 后黑屏 GAME OVER）。
    ///   玩家还在楼上时不下展台，先瞬移到一楼楼梯脚等着，玩家下楼才开始。
    ///
    /// 注视规则（R4-1）：玩家看着它时它完全不动。"看着" = 在相机视锥内 + 视线没被挡 + 看得清。
    /// 看得清：全馆灯亮着（闭馆后，还没拿到手电）就够了；停电后必须被手电照着，
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

        [Header("楼下等待")]
        [Tooltip("一楼地面的世界高度（判断玩家在不在楼上的基准）。")]
        [SerializeField] private float groundFloorY = 0f;
        [Tooltip("玩家高出一楼地面这么多算还在楼上：天使不下展台，先到楼梯脚等着。")]
        [SerializeField, Min(0f)] private float upstairsMargin = 4f;
        [Tooltip("玩家高出一楼地面不到这个高度就算已下楼：天使恢复正常追击。")]
        [SerializeField, Min(0f)] private float downstairsMargin = 1.5f;
        [Tooltip("楼梯脚等待点用的出生点名（PlayerSpawnPoint）。")]
        [SerializeField] private string stairsSpawnName = "DBG7_Stairs_1F";
        [Tooltip("找不到楼梯脚标记时的兜底坐标（世界）。")]
        [SerializeField] private Vector3 stairsFallback = new Vector3(32.2f, 0f, 32.9f);
        [Tooltip("等待点在楼梯脚东侧多少米（离楼梯口留点距离）。")]
        [SerializeField, Min(0f)] private float stairsWaitOffsetEast = 2f;

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
        private bool waitingDownstairs;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.enabled = false;
            homePosition = transform.position;
            homeRotation = transform.rotation;
        }

        private void OnEnable()
        {
            All.Add(this);
            // 阶段跟着全局状态走（ThreatSchedule.AngelPhase）；Start 时先用 Inspector 里的 phase（调试用），之后每次状态切换覆盖它
            GameFlow.StateChanged += OnStateChanged;
            GameOverScreen.Continued += ResetForRespawn;
        }

        private void OnDisable()
        {
            All.Remove(this);
            GameFlow.StateChanged -= OnStateChanged;
            GameOverScreen.Continued -= ResetForRespawn;
            RefreshFlashlightFlicker();
        }

        private void OnStateChanged(GameState from, GameState to) => SetPhase(ThreatSchedule.AngelPhase(to));

        /// <summary>死亡重生：按当前状态重进一次阶段（Attack 会重新从展台下地，Still/Peek 回展台原位）。</summary>
        private void ResetForRespawn() => SetPhase(ThreatSchedule.AngelPhase(GameFlow.State), true);

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
                waitingDownstairs = false;
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
            waitingDownstairs = false;

            // 玩家还在楼上（监控过场 / CP3 重生）：不下展台，先去一楼楼梯脚等着。
            // 位置在雕塑厅监控画面（Feed 0）的背后，雪花退去时画面里是空的。
            if (PlayerUpstairs() && TryWaitByStairs())
            {
                return;
            }

            // 从展台正前方下地（展台 1.2 m 见方），找最近的导航网格点
            Vector3 start = homePosition + homeRotation * Vector3.forward * stepOffPlinth;
            start.y = groundFloorY;
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

        private bool PlayerUpstairs()
        {
            ResolvePlayer();
            return player != null && player.transform.position.y > groundFloorY + upstairsMargin;
        }

        /// <summary>瞬移到楼梯脚东侧站定，朝向楼梯口。落不到导航网格上就返回 false，退回从展台下地。</summary>
        private bool TryWaitByStairs()
        {
            Vector3 stairs = stairsFallback;
            bool found = false;
            foreach (PlayerSpawnPoint mark in FindObjectsByType<PlayerSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mark.name == stairsSpawnName)
                {
                    stairs = mark.transform.position;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Debug.LogWarning("[WeepingAngel] 场景里没有出生点 " + stairsSpawnName + "，用兜底坐标。", this);
            }

            Vector3 spot = stairs + Vector3.right * stairsWaitOffsetEast;
            spot.y = groundFloorY;

            agent.enabled = true;
            if (!NavMesh.SamplePosition(spot, out NavMeshHit hit, 2f, NavMesh.AllAreas) || !agent.Warp(hit.position))
            {
                agent.enabled = false;
                Debug.LogWarning("[WeepingAngel] 楼梯脚没有导航网格，退回从展台下地。", this);
                return false;
            }

            agent.speed = patrolSpeed;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            waitingDownstairs = true;

            // 朝向楼梯口
            Vector3 to = stairs - transform.position;
            to.y = 0f;
            transform.rotation = Quaternion.LookRotation(to.sqrMagnitude > 0.01f ? to.normalized : Vector3.left);
            return true;
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
            if (!agent.enabled || !agent.isOnNavMesh || GameOverScreen.IsShowing || AngelJumpScare.Playing)
            {
                return;
            }

            // 在楼梯脚等玩家下楼：被不被看都不动，等下到一楼再照常巡逻/追击
            if (waitingDownstairs)
            {
                if (player == null || player.transform.position.y >= groundFloorY + downstairsMargin)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                    return;
                }

                waitingDownstairs = false;
                agent.isStopped = false;
                agent.speed = patrolSpeed;
                patrolIndex = NearestWaypoint();
                patrolDirection = 1;
                GoToWaypoint();
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
                if (flat.magnitude < catchDistance && !ThreatSchedule.NoCatch)
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
            // 遮挡检测借用手电的射线（它会忽略玩家自己），所以手电组件必须在；它一直挂在玩家相机上
            PlayerFlashlight flashlight = PlayerFlashlight.Instance;
            if (playerCamera == null || flashlight == null)
            {
                return false;
            }

            bool needFlashlight = !MuseumLights.On;
            if (needFlashlight && !flashlight.IsLit)
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

                if (!needFlashlight || flashlight.IsIlluminating(p, transform))
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
            Debug.Log("[WeepingAngel] 抓到玩家了 → jump scare → GAME OVER。", this);

            // 先放 jump scare，完了才黑屏 GAME OVER + Continue（GameOverScreen 负责）
            AngelJumpScare.Play(this, () =>
            {
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
            });
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
