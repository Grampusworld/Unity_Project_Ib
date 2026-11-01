using System.Collections;
using Ib.Core;
using Ib.Flow;
using Ib.Threats;
using UnityEngine;

namespace Ib.Interaction
{
    /// <summary>
    /// 值班室六格监控（GDD §4.4，全片唯一的强制过场）。feeds[0] 是正中那格：雕塑厅，对着哭泣天使；其余五格是别的厅。
    /// 每台摄像机渲染到自己的 RenderTexture，屏幕材质 Ib/MonitorScreen 加夜视灰度、扫描线和雪花（_Static）。
    /// 只在玩家走近值班室时才渲染，而且按 feedFps 轮流刷新（正中那格过场里每帧刷新）。
    /// 过场（MonitorTerminal 调 Play）：锁控制 → 镜头慢慢推向正中那格，越近雪花越密 → 满屏雪花时天使出动（去一楼楼梯脚等） →
    /// 雪花退去，雕塑厅空了 → 镜头拉回 → 身后走廊里一声石头挪动 → 放开控制。
    /// Ib Level 由「布置展厅细节」生成。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SecurityMonitors : MonoBehaviour
    {
        [SerializeField] private Camera[] feeds = new Camera[0];
        [SerializeField] private Renderer[] screens = new Renderer[0];
        [SerializeField, Min(1f)] private float feedFps = 5f;
        [SerializeField, Min(1f)] private float activeDistance = 16f;
        [SerializeField, Range(0f, 1f)] private float idleStatic = 0.02f;
        [Tooltip("夜视增益：停电后馆里很暗，画面要提亮很多才看得见")]
        [SerializeField, Min(1f)] private float gain = 14f;

        [Header("过场")]
        [SerializeField, Min(0.1f)] private float pushSeconds = 5f;
        [SerializeField, Min(0.1f)] private float standOff = 0.45f;
        [SerializeField, Min(0f)] private float fullStaticHold = 0.7f;
        [SerializeField, Min(0.1f)] private float clearSeconds = 0.9f;
        [SerializeField, Min(0f)] private float emptyHold = 1.6f;
        [SerializeField, Min(0.1f)] private float pullSeconds = 2f;

        private static readonly int StaticId = Shader.PropertyToID("_Static");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int GainId = Shader.PropertyToID("_Gain");

        private Material[] materials;
        private float nextRefresh;
        private int cursor;
        private bool centreEveryFrame;
        private AudioSource hiss;      // 雪花底噪：过场第一帧起就跟着画面响（老大 2026-11-01）

        /// <summary>过场进行中（MonitorTerminal 用它挡住重复触发）。</summary>
        public bool Playing { get; private set; }

        private void Awake()
        {
            int n = Mathf.Min(feeds.Length, screens.Length);
            materials = new Material[n];
            for (int i = 0; i < n; i++)
            {
                int w = i == 0 ? 512 : 256;
                // 半精度浮点：暗处的细节不会被 8 位量化掉，提亮以后不起色带
                RenderTexture rt = new RenderTexture(w, w * 3 / 4, 16, RenderTextureFormat.ARGBHalf) { name = "Feed_" + i };
                feeds[i].targetTexture = rt;
                feeds[i].enabled = false;
                materials[i] = screens[i].material;
                materials[i].SetTexture(MainTexId, rt);
                materials[i].SetFloat(StaticId, idleStatic);
                materials[i].SetFloat(GainId, gain);
            }
        }

        private void Update()
        {
            for (int i = 0; i < materials.Length; i++)
            {
                feeds[i].enabled = false;
            }

            Camera main = Camera.main;
            if (materials.Length == 0 || main == null
                || (!Playing && Vector3.Distance(main.transform.position, screens[0].bounds.center) > activeDistance))
            {
                return;
            }

            if (centreEveryFrame)
            {
                feeds[0].enabled = true;
            }

            // 轮流刷新：每 1/feedFps 秒把所有画面各刷一次，分摊到连续几帧
            if (Time.time >= nextRefresh)
            {
                feeds[cursor].enabled = true;
                cursor++;
                if (cursor >= materials.Length)
                {
                    cursor = 0;
                    nextRefresh = Time.time + 1f / feedFps;
                }
            }
        }

        public void Play(Camera eye)
        {
            if (!Playing)
            {
                StartCoroutine(Cutscene(eye != null ? eye.transform : null));
            }
        }

        private IEnumerator Cutscene(Transform cam)
        {
            Playing = true;
            centreEveryFrame = true;
            ThreatSchedule.Paused = true;   // 强制过场里不能被抓
            GameFlow.SetInputLocked(true);

            // 音画同步（老大 2026-11-01）：过场第一帧就起雪花底噪，音量/音高跟着 _Static 走，
            // 不再是「动画静音、结尾才响一声石头声」。
            StartHiss();
            if (screens.Length > 0)
            {
                Sfx.PlayAt(Sfx.KeyClick, screens[0].bounds.center, 0.35f, 8f);   // 按下监控的开关声
            }

            Vector3 localPos = cam != null ? cam.localPosition : Vector3.zero;
            Quaternion localRot = cam != null ? cam.localRotation : Quaternion.identity;
            Vector3 fromPos = cam != null ? cam.position : Vector3.zero;
            Quaternion fromRot = cam != null ? cam.rotation : Quaternion.identity;
            Vector3 focus = screens[0].bounds.center;
            Vector3 toPos = focus - (focus - fromPos).normalized * standOff;
            Quaternion toRot = Quaternion.LookRotation(focus - toPos, Vector3.up);

            // 推近：镜头和镜子一样拍不到画中人，他越靠近屏幕，画面越坏
            for (float t = 0f; t < pushSeconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / pushSeconds);
                if (cam != null)
                {
                    cam.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, k), Quaternion.Slerp(fromRot, toRot, k));
                }

                SetStatic(Mathf.Lerp(idleStatic, 1f, k * k), Mathf.Lerp(idleStatic, 0.35f, k));
                yield return null;
            }

            SetStatic(1f, 0.35f);
            if (screens.Length > 0)
            {
                Sfx.PlayAt(Sfx.PowerClunk, screens[0].bounds.center, 0.4f, 10f);   // 满屏雪花那一下的闷响
            }

            yield return new WaitForSeconds(fullStaticHold * 0.5f);

            // 满屏雪花的这一瞬，摄像头的注视断了：天使出动。
            // 玩家在二楼值班室，天使按 WeepingAngel 的楼下等待逻辑去一楼楼梯脚 —— 雪花退去时画面里是空的。
            SceneTransitions.ActivateAngels();
            yield return new WaitForSeconds(fullStaticHold * 0.5f);

            for (float t = 0f; t < clearSeconds; t += Time.deltaTime)
            {
                SetStatic(Mathf.Lerp(1f, idleStatic, t / clearSeconds), 0.35f);
                yield return null;
            }

            SetStatic(idleStatic, idleStatic);
            yield return new WaitForSeconds(emptyHold);

            // 拉回
            Vector3 backPos = cam != null ? cam.position : Vector3.zero;
            Quaternion backRot = cam != null ? cam.rotation : Quaternion.identity;
            for (float t = 0f; t < pullSeconds && cam != null; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / pullSeconds);
                cam.SetPositionAndRotation(Vector3.Lerp(backPos, fromPos, k), Quaternion.Slerp(backRot, fromRot, k));
                yield return null;
            }

            if (cam != null)
            {
                cam.localPosition = localPos;
                cam.localRotation = localRot;

                // 身后的走廊里有什么动了
                Vector3 behind = cam.position - Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized * 7f;
                Sfx.PlayAt(Sfx.StoneScrape, behind, 0.8f, 20f);
            }

            centreEveryFrame = false;
            ThreatSchedule.Paused = false;
            GameFlow.SetInputLocked(false);
            Playing = false;

            // 雪花退干净以后底噪跟着淡出，不要一刀切
            StartCoroutine(FadeHiss(0.5f));
        }

        /// <summary>过场第一帧起循环播雪花底噪（素材缺失时静音，不报错）。</summary>
        private void StartHiss()
        {
            if (hiss != null)
            {
                hiss.Stop();
                Destroy(hiss);
                hiss = null;
            }

            AudioClip clip = Sfx.Load(Sfx.MonitorStatic);
            if (clip == null)
            {
                return;
            }

            hiss = gameObject.AddComponent<AudioSource>();
            hiss.clip = clip;
            hiss.loop = true;
            hiss.playOnAwake = false;
            hiss.spatialBlend = 0.65f;   // 从屏幕那儿来，但别被距离衰减吃没
            hiss.rolloffMode = AudioRolloffMode.Linear;
            hiss.minDistance = 2f;
            hiss.maxDistance = 14f;
            hiss.dopplerLevel = 0f;
            hiss.volume = 0f;
            hiss.pitch = 0.9f;
            hiss.Play();
        }

        private System.Collections.IEnumerator FadeHiss(float seconds)
        {
            if (hiss == null)
            {
                yield break;
            }

            float from = hiss.volume;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                if (hiss == null)
                {
                    yield break;
                }

                hiss.volume = Mathf.Lerp(from, 0f, t / seconds);
                yield return null;
            }

            if (hiss != null)
            {
                hiss.Stop();
                Destroy(hiss);
                hiss = null;
            }
        }

        private void SetStatic(float centre, float others)
        {
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i].SetFloat(StaticId, i == 0 ? centre : others);
            }

            if (hiss != null)
            {
                // 雪花越密，底噪越响、音高越高：耳听和画面是同一根曲线
                hiss.volume = Mathf.Clamp01(0.05f + 0.45f * centre) * AudioMix.Effects;
                hiss.pitch = 0.9f + 0.22f * centre;
            }
        }
    }
}
