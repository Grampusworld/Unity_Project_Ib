using Ib.Core;
using Ib.Flow;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ib.Interaction
{
    /// <summary>
    /// 配电室门（老大 2026-11-01）：拿到保安柜里的工作卡（老大口中的「钥匙」）时，
    /// 门板绕 <c>PowerRoom_DoorHinge</c> 平滑打开，同一帧起播 <c>PowerRoomDoorOpen</c>。
    /// 门平时虚掩 26°：玩家倒着退进配电室时不用先停下来开门，门本身也一直看得见；
    /// 拿到卡后 1.5 s 开到 92° 并保持。场景里不用挂组件：场景加载后自己生成，
    /// 按节点名找门轴（由 GrayboxBuilder.Wayfinding 生成）。老场景没有这扇门时静默降级，不报错。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PowerRoomDoor : MonoBehaviour
    {
        private const string HingeName = "PowerRoom_DoorHinge";
        private const string CreakClip = "PowerRoomDoorOpen";
        private const float AjarAngle = 26f;    // 虚掩：门缝约 0.7 m，玩家侧身能过
        private const float OpenAngle = 92f;    // 朝雕塑厅（+Z）外开，贴到门洞西侧
        private const float OpenSeconds = 1.5f;
        private const float CreakVolume = 0.8f;
        private const float FindInterval = 0.5f;

        private static PowerRoomDoor instance;

        private Transform hinge;
        private float angle = AjarAngle;
        private float progress;
        private float nextFind;
        private bool opening;
        private bool opened;
        private bool warned;

        /// <summary>门轴当前角度（0 = 关，92 = 全开），自检用。</summary>
        public float Angle => angle;

        /// <summary>正在播开门动画。</summary>
        public bool IsOpening => opening;

        /// <summary>已经开到最大。</summary>
        public bool IsOpen => opened;

        /// <summary>开门声的声源（素材缺失时为 null）。</summary>
        public AudioSource Creak { get; private set; }

        public Transform Hinge => hinge;

        public static PowerRoomDoor Instance => instance;

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

        private void Awake()
        {
            instance = this;

            // 读档 / 调试直接带着工作卡进场景：门直接给到全开，不补播动画
            if (GameFlow.HasStaffCard)
            {
                angle = OpenAngle;
                opened = true;
            }
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
                Apply(angle);
            }

            // 拿到工作卡（不管是保安柜交互、调试传送还是读档）→ 开门
            if (!opening && !opened && GameFlow.HasStaffCard)
            {
                Open();
            }

            if (!opening)
            {
                return;
            }

            progress = Mathf.Min(1f, progress + Time.deltaTime / OpenSeconds);
            angle = Mathf.Lerp(AjarAngle, OpenAngle, Mathf.SmoothStep(0f, 1f, progress));
            Apply(angle);

            if (progress >= 1f)
            {
                opening = false;
                opened = true;
                Debug.Log($"[PowerRoomDoor] 全开：{OpenAngle:F0}°，用时 {OpenSeconds:F1} s", this);
            }
        }

        /// <summary>开始开门（重复调用无效）。</summary>
        public void Open()
        {
            if (opening || opened)
            {
                return;
            }

            opening = true;
            progress = 0f;
            PlayCreak();
            Debug.Log($"[PowerRoomDoor] 拿到工作卡 → 开门动画开始（{AjarAngle:F0}° → {OpenAngle:F0}°，{OpenSeconds:F1} s）", this);
        }

        private void Apply(float degrees)
        {
            if (hinge != null)
            {
                hinge.localRotation = Quaternion.Euler(0f, -degrees, 0f);
            }
        }

        private void PlayCreak()
        {
            if (hinge == null)
            {
                return;
            }

            AudioClip clip = Sfx.Load(CreakClip);
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
