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
    /// 二楼名画人物（场景里的 PaintingFigure_A / PaintingFigure_B）。
    /// 拿到手电才行动（ThreatSchedule.CollectionOut）。激活后不再等玩家靠近画框（老大 2026-11-01）：
    /// 只要玩家在二楼、且不在值班室（监控室）里，两只就撞碎画框冲出来全速追，同一帧播 PaintingScream。
    /// 玩家一进监控室就放弃追击，走回自己的画框、画框复原、人藏回画里伪装；玩家再回到二楼其它地方，
    /// 等 reengageDelay 之后立刻再冲出来追（门口来回走不会反复进进出出）。回框途中玩家又出来，直接转身追，不卡死。
    /// 回框到画前时玩家正看着就先站着等（不当着玩家的面凭空消失），等太久也会藏回去。
    /// 只管二楼：玩家下了楼梯（不在这层楼面上）就放弃，走回画框（二楼导航网格不含楼梯）。
    /// 进不了全家福厅（noGo，GDD §6.9「它们停在门口，没有跟进来」）：玩家躲进去，它们就回去装回画里；
    /// 蹲在展柜后面也不再是掩体 —— 它们在二楼就是一路追到抓住为止，唯一的安全区是监控室。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaintingFigure : MonoBehaviour
    {
        public static readonly List<PaintingFigure> All = new List<PaintingFigure>();

        private enum Mode
        {
            Idle,
            Return,
            Hidden,
            Chase,
        }

        [SerializeField] private GameObject stillVisual;
        [SerializeField] private GameObject outVisual;

        [Header("破框出场")]
        [Tooltip("画框处的破框站点（菜单 Tools/Ib/布置名画人物破框 自动绑定）。为空时按最近距离自动找。")]
        [SerializeField] private PaintingBreakout breakout;
        [Tooltip("破框前的错开延迟（每只自己的出场时刻，场景里 0.35 / 0.85）。到点后破框、尖叫、站出来是同一帧。")]
        [SerializeField, Min(0f)] private float breakoutDelay = 0.3f;
        [Tooltip("没绑站点时，在这个半径里找最近的破框站点。")]
        [SerializeField, Min(1f)] private float breakoutSearchRadius = 6f;

        [Header("追击")]
        [SerializeField, Min(0.5f)] private float chaseSpeed = 3.6f;
        [SerializeField, Min(0.2f)] private float catchDistance = 0.9f;

        [Header("回框伪装 / 再冲出来")]
        [Tooltip("不追的时候走回画框的速度。")]
        [SerializeField, Min(0.5f)] private float roamSpeed = 2.0f;
        [Tooltip("刚藏回去之后至少等这么久才会再冲出来，避免藏进去的同一刻又冲出来。")]
        [SerializeField, Min(0f)] private float ambushCooldown = 1.5f;
        [Tooltip("玩家离开监控室后，至少在外面待这么久才会再冲出来（门口来回走不会反复进进出出）。")]
        [SerializeField, Min(0f)] private float reengageDelay = 0.8f;
        [Tooltip("不许进入的区域（世界坐标，只看 XZ）：全家福厅。由「布置后半段」写入，大小为 0 时不限制。")]
        [SerializeField] private Bounds noGo;

        public bool IsActive { get; private set; }

        /// <summary>正在追玩家（心跳、BGM 压低看这个）。</summary>
        public bool IsChasing => mode == Mode.Chase;

        /// <summary>最近一次破框时尖叫开始 / 人出现的 Time.time（自检用：两者必须相等）。</summary>
        public float LastScreamTime { get; private set; } = -1f;
        public float LastPresentTime { get; private set; } = -1f;

        private float ReengageDelay => reengageDelay > 0.01f ? reengageDelay : 0.8f;

        private Mode mode;
        private float outsideMonitor;   // 玩家离开监控室、能交战了多久
        private float returnStarted;
        private float stuckTime;
        private float hiddenSince;
        private float returnRepath;
        private float floorY;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private NavMeshAgent agent;
        private FirstPersonController player;
        private float eyeHeight;
        private bool warnedOffMesh;
        private Animator animator;
        private Transform girlVisual;
        private readonly RaycastHit[] groundHits = new RaycastHit[8];
        private readonly List<Renderer> bodyRenderers = new List<Renderer>();
        private readonly List<Collider> bodyColliders = new List<Collider>();
        private bool present;
        private float spawnAt = -1f;

        private void Awake()
        {
            homePosition = transform.position;
            homeRotation = transform.rotation;

            // 占位胶囊生成时拆了碰撞体。补回来，玩家才撞得上，射线也能打到它自己（判定时会跳过）。
            if (GetComponent<Collider>() == null)
            {
                gameObject.AddComponent<CapsuleCollider>();
            }

            // 身高：占位胶囊看自身网格；换成 Girl 模型后根上没有网格，看胶囊碰撞体（根在身体中心）
            Renderer rend = GetComponent<Renderer>();
            CapsuleCollider body = GetComponent<CapsuleCollider>();
            float halfHeight = rend != null ? rend.bounds.extents.y
                : body != null ? body.height * 0.5f * transform.lossyScale.y : 1.1f;
            eyeHeight = halfHeight * 0.72f;
            floorY = transform.position.y - halfHeight;
            animator = GetComponentInChildren<Animator>(true);
            girlVisual = transform.Find("Visual_Girl");

            bodyRenderers.Clear();
            bodyRenderers.AddRange(GetComponentsInChildren<Renderer>(true));
            bodyColliders.Clear();
            bodyColliders.AddRange(GetComponentsInChildren<Collider>(true));

            agent = GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                agent = gameObject.AddComponent<NavMeshAgent>();
            }

            agent.radius = 0.32f;
            agent.height = 1.6f;
            // NavMeshAgent 的 baseOffset 会再乘一次根节点缩放（灰盒整体放大了 WorldScale），所以这里要除回去，
            // 否则枢轴被抬高 halfHeight × (缩放 − 1)，人物悬空。
            agent.baseOffset = halfHeight / Mathf.Max(0.01f, transform.lossyScale.y);
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
            spawnAt = -1f;
            outsideMonitor = 0f;
            transform.SetPositionAndRotation(homePosition, homeRotation);
            StopAgent();

            Refresh();
            if (IsActive)
            {
                // 读档重来：已经出过场了，直接藏回画里等玩家再上二楼
                Hide();
            }
        }

        private void Refresh()
        {
            bool wantOut = ThreatSchedule.CollectionOut;
            if (wantOut)
            {
                if (!IsActive)
                {
                    IsActive = true;

                    // 激活时先老老实实待在画里（画框完好）；玩家一上二楼（监控室除外）再破框冲出来
                    spawnAt = -1f;
                    outsideMonitor = 0f;
                    transform.SetPositionAndRotation(homePosition, homeRotation);
                    SetPresent(false);
                    mode = Mode.Hidden;
                    hiddenSince = Time.time;
                    StopAgent();
                    if (breakout == null)
                    {
                        breakout = FindBreakout();
                    }

                    if (breakout != null)
                    {
                        breakout.Restore();
                    }
                }
            }
            else
            {
                // 手电之前：画是画，人是「不存在」的（不可见、不可交互、不占导航）
                IsActive = false;
                CancelBreakout();
                SetPresent(false);
                mode = Mode.Idle;
                outsideMonitor = 0f;
                StopAgent();

                // 完整画和金框复原，下次拿到手电（读档重来）还能再破一次
                if (breakout != null)
                {
                    breakout.Restore();
                }
            }

            if (stillVisual != null)
            {
                stillVisual.SetActive(!wantOut);
            }

            if (outVisual != null)
            {
                outVisual.SetActive(wantOut);
            }
        }

        /// <summary>
        /// 出场 / 不在场。不在场 = 不可见、不可交互、不占导航（渲染器、碰撞体、agent 全关）。
        /// 拿到手电之前、以及破框之后那半秒，玩家看不到它。
        /// </summary>
        private void SetPresent(bool on)
        {
            present = on;

            for (int i = 0; i < bodyRenderers.Count; i++)
            {
                if (bodyRenderers[i] != null)
                {
                    bodyRenderers[i].enabled = on;
                }
            }

            for (int i = 0; i < bodyColliders.Count; i++)
            {
                if (bodyColliders[i] != null)
                {
                    bodyColliders[i].enabled = on;
                }
            }

            if (animator != null)
            {
                animator.enabled = on;
            }

            if (!on)
            {
                StopAgent();
            }
        }

        private void StopAgent()
        {
            if (agent == null || !agent.enabled)
            {
                return;
            }

            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }

            agent.enabled = false;
        }

        private void CancelBreakout()
        {
            if (spawnAt >= 0f)
            {
                spawnAt = -1f;
            }
        }

        /// <summary>没绑站点时就近找一个（画框就在原位旁边）。</summary>
        private PaintingBreakout FindBreakout()
        {
            PaintingBreakout[] all = FindObjectsByType<PaintingBreakout>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            PaintingBreakout best = null;
            float bestDist = breakoutSearchRadius * breakoutSearchRadius;
            for (int i = 0; i < all.Length; i++)
            {
                float dx = all[i].transform.position.x - transform.position.x;
                float dz = all[i].transform.position.z - transform.position.z;
                float d = dx * dx + dz * dz;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = all[i];
                }
            }

            return best;
        }

        /// <summary>Girl 模型的动作跟着移动速度播：站着不动就定格（名画人物不该有待机小动作）。</summary>
        private void LateUpdate()
        {
            // jump scare 期间整尊被搬到镜头前，不能再被拉回地面
            if (AngelJumpScare.Playing)
            {
                if (animator != null)
                {
                    animator.speed = 0f;
                }

                return;
            }

            SnapVisualToGround();
            if (animator == null)
            {
                return;
            }

            float v = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
            animator.speed = IsActive && !ThreatSchedule.Paused ? Mathf.Clamp(v / roamSpeed, 0f, 1.8f) : 0f;
        }

        /// <summary>
        /// Girl 外形贴地：从身体中心往下打射线找楼面，把 Visual_Girl（原点 = 爬行姿势最低点）放到楼面上。
        /// 不依赖 agent 的 baseOffset 换算，楼梯、坡面上也贴得住。
        /// </summary>
        private void SnapVisualToGround()
        {
            if (girlVisual == null)
            {
                return;
            }

            Vector3 origin = transform.position + Vector3.up * 0.3f;
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            float groundY = 0f;
            for (int i = 0; i < n; i++)
            {
                Transform t = groundHits[i].transform;
                if (t == transform || t.IsChildOf(transform) || groundHits[i].distance >= best)
                {
                    continue;
                }

                best = groundHits[i].distance;
                groundY = groundHits[i].point.y;
            }

            if (best == float.MaxValue)
            {
                return;
            }

            Vector3 p = girlVisual.position;
            girlVisual.position = new Vector3(p.x, groundY, p.z);
        }

        private void Update()
        {
            if (!IsActive || GameOverScreen.IsShowing || ThreatSchedule.Paused || AngelJumpScare.Playing)
            {
                if (ThreatSchedule.Paused && agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                }

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

            Vector3 feet = player.transform.position;
            bool onThisFloor = OnThisFloor(feet);
            bool inMonitor = onThisFloor && MuseumRooms.InDutyRoom(feet);
            bool inNoGo = InNoGo(feet);

            // 不在可交战区域：监控室里、下了二楼、或者躲进全家福厅 → 放弃追击，回画框装回去
            if (!onThisFloor || inMonitor || inNoGo)
            {
                outsideMonitor = 0f;
                if (spawnAt >= 0f)
                {
                    spawnAt = -1f;   // 还没站出来：画框复原、人直接留在画里
                    Hide();
                }
                else if (mode == Mode.Chase)
                {
                    BeginRetreat();
                }
                else if (mode == Mode.Return)
                {
                    UpdateReturn();
                }
                else if (mode == Mode.Idle)
                {
                    StartReturn();
                }

                return;
            }

            outsideMonitor += Time.deltaTime;

            // 藏在画里：玩家一上二楼（监控室除外）就破框冲出来。
            // 每只先等自己的 breakoutDelay（0.35 / 0.85，错开出场），到点后破框 + 尖叫 + 站出来同帧。
            if (mode == Mode.Hidden)
            {
                if (spawnAt < 0f)
                {
                    if (Time.time - hiddenSince >= Mathf.Max(ambushCooldown, 0.01f) && outsideMonitor >= ReengageDelay)
                    {
                        spawnAt = Time.time + Mathf.Max(0f, breakoutDelay);
                    }

                    return;
                }

                if (Time.time >= spawnAt)
                {
                    Ambush();
                }

                return;
            }

            // 回框途中玩家又出来了：也要先连续在外面待够 reengageDelay 才转身追，
            // 否则玩家在监控室门口来回走会让它反复「回框 / 追击」
            if (mode == Mode.Return)
            {
                if (outsideMonitor < ReengageDelay)
                {
                    UpdateReturn();
                    return;
                }

                mode = Mode.Chase;
            }

            if (!present)
            {
                return;
            }

            EnsureAgent();
            mode = Mode.Chase;
            GoTo(feet, chaseSpeed);
            if (FlatDistance(feet) <= catchDistance && !ThreatSchedule.NoCatch)
            {
                Catch();
            }
        }

        // ── 回框伪装 / 再冲出来 ─────────────────────────────────────────────────

        /// <summary>玩家躲进监控室：放弃追击、回画框；还在框里没站出来的直接收回去。</summary>
        private void BeginRetreat()
        {
            if (spawnAt >= 0f || !present)
            {
                spawnAt = -1f;
                Hide();
                return;
            }

            StartReturn();
        }

        private void StartReturn()
        {
            mode = Mode.Return;
            returnStarted = Time.time;
            stuckTime = 0f;
            returnRepath = 0f;
            GoTo(homePosition, roamSpeed);
        }

        private void UpdateReturn()
        {
            bool atHome = FlatDistance(homePosition) <= 1.2f;
            bool giveUp = Time.time - returnStarted > 5f;   // 走不过去（门关着 / 被挡住）也别一直卡在回框路上
            if (!atHome)
            {
                // 定期重新寻路；卡死（走不到原位）时也走下面的「没人看着就藏回去」
                returnRepath -= Time.deltaTime;
                if (returnRepath <= 0f)
                {
                    returnRepath = 0.5f;
                    GoTo(homePosition, roamSpeed);
                }

                if (!Arrived(homePosition) && !giveUp)
                {
                    return;
                }
            }
            else if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, homeRotation, 240f * Time.deltaTime);
            }

            // 到了（或者走不过去了）：玩家正看着就站着等，不当面消失；等太久（5 s）也不再干等
            if (!PlayerSeesMe() || giveUp)
            {
                Hide();
            }
        }

        /// <summary>藏回画里：人不在场、画框和画布复原，回到原位朝向。</summary>
        private void Hide()
        {
            SetPresent(false);
            transform.SetPositionAndRotation(homePosition, homeRotation);
            mode = Mode.Hidden;
            hiddenSince = Time.time;
            if (breakout == null)
            {
                breakout = FindBreakout();
            }

            if (breakout != null)
            {
                breakout.Restore();
            }
        }

        /// <summary>撞碎画框冲出来：碎框 + 尖叫 + 人站出来 + 开始追，全部同一帧（老大 2026-11-01）。</summary>
        private void Ambush()
        {
            if (breakout == null)
            {
                breakout = FindBreakout();
            }

            Vector3 at = breakout != null ? breakout.transform.position : transform.position;
            if (breakout != null)
            {
                breakout.Restore();
                breakout.Break(0.3f, true);   // 尖叫里已经有爆裂声，玻璃声压到 0.3（Ib 美术建议）；immediate = 同帧换碎框
            }

            spawnAt = -1f;
            LastScreamTime = Time.time;
            PlayScream(at);        // 第 0 秒就到峰值的尖叫，和破框同一帧
            SetPresent(true);      // 同一帧站出来：不再等 0.3–1 s 才出现
            LastPresentTime = Time.time;

            Vector3 face = player.transform.position - homePosition;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(face);
            }

            mode = Mode.Chase;
            Debug.Log($"[PaintingFigure] {name} 破框 / 尖叫 / 出现同帧：scream t={LastScreamTime:F3} shown t={LastPresentTime:F3} Δ={(LastPresentTime - LastScreamTime) * 1000f:F1} ms", this);
        }

        private void PlayScream(Vector3 position)
        {
            AudioClip clip = Sfx.Load(Sfx.PaintingScream);
            if (clip == null)
            {
                Sfx.PlayAt(Sfx.GlassBreak, position, 1f, 30f);   // 尖叫素材缺失时至少有个响
                return;
            }

            GameObject go = new GameObject("Sfx_" + Sfx.PaintingScream);
            go.transform.position = position;
            AudioSource source = Sfx.Attach(go, clip, false, 1f, 30f);
            source.spatialBlend = 0.7f;   // 太 3D 会被距离衰减吃掉惊吓感
            source.minDistance = 4f;
            source.pitch = Random.Range(0.95f, 1.05f);
            source.Play();
            Destroy(go, clip.length / source.pitch + 0.2f);
        }

        /// <summary>玩家相机能看见这个人（在视野里且没有墙挡着）。</summary>
        private bool PlayerSeesMe()
        {
            Camera cam = Camera.main;
            if (player != null && (cam == null || !cam.transform.IsChildOf(player.transform)))
            {
                cam = player.GetComponentInChildren<Camera>();
            }

            if (cam == null || !OnThisFloor(player.transform.position))
            {
                return false;
            }

            Vector3 chest = transform.position + Vector3.up * eyeHeight * 0.6f;
            Vector3 vp = cam.WorldToViewportPoint(chest);
            if (vp.z <= 0f || vp.x < -0.1f || vp.x > 1.1f || vp.y < -0.1f || vp.y > 1.1f)
            {
                return false;
            }

            return ClearLine(chest, cam.transform.position);
        }

        /// <summary>from → to 之间除了自己和玩家没有别的东西挡着。</summary>
        private bool ClearLine(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float dist = d.magnitude;
            if (dist < 0.05f)
            {
                return true;
            }

            RaycastHit[] hits = Physics.RaycastAll(from, d / dist, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Transform hit = hits[i].transform;
                if (hit == transform || hit.IsChildOf(transform) || (player != null && hit.IsChildOf(player.transform)))
                {
                    continue;
                }

                return false;
            }

            return true;
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
        /// 网格的枢轴在胶囊中心。Warp 给导航网格上的点，agent 自己按 baseOffset 抬到枢轴高度。
        /// </summary>
        private void PlaceOnMesh(Vector3 around)
        {
            // 先采样再启用 Agent：场景缺二楼导航网格时，启用一个不在网格上的 Agent 会把整尊人
            // 瞬移到最近的网格（实测掉到一楼/楼外）。采不到就保持禁用，走下面的直线兜底。
            if (agent.enabled)
            {
                agent.enabled = false;
            }

            Vector3 probe = around;
            RaycastHit ground;
            if (Physics.Raycast(around + Vector3.up * 3f, Vector3.down, out ground, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && ground.transform != transform
                && !ground.transform.IsChildOf(transform))
            {
                probe = ground.point;
            }

            if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
            {
                if (!warnedOffMesh)
                {
                    warnedOffMesh = true;
                    Debug.LogWarning("[PaintingFigure] 脚下没有二楼导航网格，改为直线走。", this);
                }

                return;
            }

            agent.enabled = true;
            agent.Warp(hit.position);   // Warp 收的是导航网格上的点，baseOffset 由 agent 自己加
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
            StopAgent();

            Debug.Log("[PaintingFigure] 抓到玩家了 → jump scare。", this);

            // 跟哭泣天使同一套硬惊吓：锁输入、贴脸推近、尖叫 → 收尾再读档。
            // 它是趴着从画框里爬出来的（脸朝下），走正脸特写：转过来、纯黑背景、只看脸。
            // 没有 Girl 模型（占位胶囊）时 head 为空，PlayFace 自己退回包围盒算法。
            Transform head = girlVisual != null ? FindBone(girlVisual, "Head") : null;
            AngelJumpScare.PlayFace(transform, head, Sfx.AngelScream, GameOverScreen.Show);
        }

        private static Transform FindBone(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }
    }
}
