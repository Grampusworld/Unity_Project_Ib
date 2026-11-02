using Ib.Core;
using Ib.Flow;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ib.Interaction
{
    /// <summary>
    /// 配电室门（老大 2026-11-01，2026-11-02 改）。场景里不用挂组件：场景加载后自己生成，
    /// 按节点名找门轴 <c>PowerRoom_DoorHinge</c>（GrayboxBuilder.Wayfinding 生成，生成时 0° = 关）。老场景没有这扇门时静默降级。
    /// 三段：
    /// - 关（0°）：开局一直关着。
    /// - 门缝（15°）：值班室监控过场 SecurityMonitors.Playing 由 true 变 false 那一刻，3 s 缓缓开一条缝 + DoorCreak。
    ///   缝只有约 0.4 m，人过不去，所以门板上挂 PowerRoomDoorPush：按 E「Push」推到全开（工作卡在配电室里面，流程要求进去）。
    /// - 全开（92°）：推开，或拿到工作卡（调试传送 / 读档）。1.5 s + PowerRoomDoorOpen。
    /// 重生 / 读档按当前状态瞬间摆好：HasStaffCard → 全开；State ≥ AngelsActive 且过场不在放 → 门缝（本局推开过就全开）；否则关。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PowerRoomDoor : MonoBehaviour
    {
        private const string HingeName = "PowerRoom_DoorHinge";
        private const string LeafName = "PowerRoom_Door";
        private const string OpenClip = "PowerRoomDoorOpen";
        private const float AjarAngle = 15f;    // 门缝：约 0.4 m（世界），玩家过不去，要推
        private const float OpenAngle = 92f;    // 朝雕塑厅（+Z）外开，贴到门洞西侧
        private const float AjarSeconds = 3f;
        private const float OpenSeconds = 1.5f;
        private const float CreakVolume = 0.8f;
        private const float FindInterval = 0.5f;

        private static PowerRoomDoor instance;

        private Transform hinge;
        private SecurityMonitors monitors;
        private float angle;
        private float fromAngle, toAngle, progress, seconds;
        private bool moving;
        private bool pushed;          // 本局推开过（检查点回到 AngelsActive 之前会清掉）
        private bool wasPlaying;
        private float nextFind;
        private bool warned;

        /// <summary>门轴当前角度（0 = 关，15 = 门缝，92 = 全开），自检用。</summary>
        public float Angle => angle;

        public bool IsOpening => moving;

        public bool IsOpen => !moving && angle >= OpenAngle - 0.5f;

        /// <summary>开着一条缝、没在动：可以推。</summary>
        public bool CanPush => !moving && angle > 0.5f && angle < OpenAngle - 0.5f;

        /// <summary>开门声的声源（素材缺失时为 null）。</summary>
        public AudioSource Creak { get; private set; }

        public Transform Hinge => hinge;

        public static PowerRoomDoor Instance => instance;

        private bool MonitorsPlaying
        {
            get
            {
                if (monitors == null)
                {
                    monitors = FindAnyObjectByType<SecurityMonitors>();
                }

                return monitors != null && monitors.Playing;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive)
            {
                return;
            }

            if (instance != null)
            {
                Destroy(instance.gameObject);
                instance = null;
            }

            instance = new GameObject("--- PowerRoomDoor ---").AddComponent<PowerRoomDoor>();
        }

        private void Awake() => instance = this;

        private void OnEnable() => GameOverScreen.Continued += OnRespawn;

        private void OnDisable() => GameOverScreen.Continued -= OnRespawn;

        /// <summary>按当前状态应该停在的角度（不含动画）。</summary>
        private float StateAngle()
        {
            if (GameFlow.HasStaffCard || (pushed && GameFlow.State >= GameState.AngelsActive))
            {
                return OpenAngle;
            }

            return GameFlow.State >= GameState.AngelsActive && !MonitorsPlaying ? AjarAngle : 0f;
        }

        private void OnRespawn()
        {
            if (GameFlow.State < GameState.AngelsActive)
            {
                pushed = false;
            }

            Snap(StateAngle());
        }

        private void Update()
        {
            if (hinge == null)
            {
                if (Time.unscaledTime < nextFind)
                {
                    return;
                }

                nextFind = Time.unscaledTime + FindInterval;
                GameObject go = GameObject.Find(HingeName);
                if (go == null)
                {
                    if (!warned)
                    {
                        warned = true;
                        Debug.Log("[PowerRoomDoor] 场景里没有 PowerRoom_DoorHinge（灰盒还没重建）→ 跳过，不报错。");
                    }

                    return;
                }

                hinge = go.transform;
                Transform leaf = hinge.Find(LeafName);
                if (leaf != null && leaf.GetComponent<PowerRoomDoorPush>() == null)
                {
                    leaf.gameObject.AddComponent<PowerRoomDoorPush>();
                }

                // 场景加载 / 读档：直接摆到当前状态，不播动画
                wasPlaying = MonitorsPlaying;
                Snap(StateAngle());
            }

            bool playing = MonitorsPlaying;
            bool monitorsJustEnded = wasPlaying && !playing;
            wasPlaying = playing;

            if (!moving)
            {
                float target = StateAngle();
                if (target > angle + 0.5f)
                {
                    if (target >= OpenAngle)
                    {
                        BeginMove(OpenAngle, OpenSeconds, OpenClip);   // 拿到工作卡（调试传送等）
                    }
                    else if (monitorsJustEnded || GameFlow.State >= GameState.AngelsActive)
                    {
                        BeginMove(AjarAngle, AjarSeconds, Sfx.DoorCreak);   // 看完监控：缓缓开一条缝
                    }
                }
            }

            if (!moving)
            {
                return;
            }

            progress = Mathf.Min(1f, progress + Time.deltaTime / seconds);
            angle = Mathf.Lerp(fromAngle, toAngle, Mathf.SmoothStep(0f, 1f, progress));
            Apply(angle);

            if (progress >= 1f)
            {
                moving = false;
                Debug.Log($"[PowerRoomDoor] 到位：{toAngle:F0}°", this);
            }
        }

        /// <summary>玩家按 E 推门：从门缝推到全开（PowerRoomDoorPush 调用）。</summary>
        public void Push()
        {
            if (!CanPush)
            {
                return;
            }

            pushed = true;
            BeginMove(OpenAngle, OpenSeconds, OpenClip);
        }

        /// <summary>兼容旧调用：直接开到全开。</summary>
        public void Open()
        {
            if (moving || IsOpen)
            {
                return;
            }

            pushed = true;
            BeginMove(OpenAngle, OpenSeconds, OpenClip);
        }

        private void BeginMove(float target, float duration, string clip)
        {
            fromAngle = angle;
            toAngle = target;
            progress = 0f;
            seconds = Mathf.Max(0.01f, duration);
            moving = true;
            PlayCreak(clip);
            Debug.Log($"[PowerRoomDoor] 开门 {fromAngle:F0}° → {toAngle:F0}°（{seconds:F1} s）", this);
        }

        private void Snap(float degrees)
        {
            moving = false;
            angle = degrees;
            Apply(angle);
        }

        private void Apply(float degrees)
        {
            if (hinge != null)
            {
                hinge.localRotation = Quaternion.Euler(0f, -degrees, 0f);
            }
        }

        private void PlayCreak(string clipName)
        {
            if (hinge == null)
            {
                return;
            }

            AudioClip clip = Sfx.Load(clipName);
            if (clip == null)
            {
                return;   // 素材没到：静音开门，不报错
            }

            if (Creak != null)
            {
                Destroy(Creak);
            }

            Creak = hinge.gameObject.AddComponent<AudioSource>();
            Creak.clip = clip;
            Creak.loop = false;
            Creak.playOnAwake = false;
            Creak.spatialBlend = 1f;
            Creak.rolloffMode = AudioRolloffMode.Logarithmic;
            Creak.minDistance = 2f;
            Creak.maxDistance = 30f;
            Creak.dopplerLevel = 0f;
            Creak.volume = CreakVolume * AudioMix.Effects;
            Creak.Play();   // 和 hinge 转起来同一帧
        }
    }
}
