using UnityEngine;

namespace Ib.Environment
{
    /// <summary>
    /// 水晶吊灯：断电（MuseumLights 关）后灯泡熄灭，整盏灯绕吊链顶端开始缓慢摇晃 ——
    /// 小角度（2–4°）、周期 4–6 s，隔一会儿补一点力，所以永远停不下来；两盏灯相位错开。
    /// 来电后灯泡亮回来，摆动在几秒内收住。
    /// 摆到最远端（速度换向）时吊链嘎吱一声：Resources/Audio/ChandelierCreak_*（CC0 录音，见同目录 LICENSES.md），
    /// 每次随机挑一段、随机音高，3D 声音约 15 m 外听不见。只在断电后摆起来时响。
    /// 挂在吊灯根节点上（原点 = 天花上的吊点）。
    /// 调试：F10 切换全馆灯光，或组件右键「断电 / 来电」。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChandelierSway : MonoBehaviour
    {
        [SerializeField] private Renderer[] bulbs = new Renderer[0];
        [SerializeField] private Material bulbOn;
        [SerializeField] private Material bulbOff;
        [SerializeField] private Light lamp;

        [Header("摇晃")]
        [SerializeField, Range(0f, 10f)] private float angleMin = 2f;
        [SerializeField, Range(0f, 10f)] private float angleMax = 4f;
        [SerializeField, Min(0.5f)] private float periodMin = 4f;
        [SerializeField, Min(0.5f)] private float periodMax = 6f;
        [SerializeField, Min(0.5f)] private float nudgeIntervalMin = 5f;
        [SerializeField, Min(0.5f)] private float nudgeIntervalMax = 11f;
        [SerializeField, Min(0f)] private float settleSpeed = 0.6f;
        [Tooltip("吱呀声的音源；不填就在运行时自己加一个。")]
        [SerializeField] private AudioSource creak;
        [SerializeField, Min(1f)] private float creakDistance = 15f;
        [SerializeField, Range(0f, 1f)] private float creakVolume = 0.7f;

        private Quaternion rest;
        private bool swaying;
        private float amplitude;
        private float targetAmplitude;
        private float period;
        private float phase;
        private float nudgeTimer;
        private Vector3 axis;
        private float lastSin;
        private float lastDelta;
        private AudioClip[] creakClips;

        private void Awake()
        {
            rest = transform.localRotation;
            // 每盏灯的相位、轴向、周期都不一样
            Random.State saved = Random.state;
            Random.InitState(transform.position.GetHashCode());   // 每盏灯位置不同，种子不同
            phase = Random.value * Mathf.PI * 2f;
            float a = Random.value * Mathf.PI;
            axis = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            period = Random.Range(periodMin, periodMax);
            Random.state = saved;

            creakClips = new[] { Resources.Load<AudioClip>("Audio/ChandelierCreak_0"), Resources.Load<AudioClip>("Audio/ChandelierCreak_1") };
            if (creak == null)
            {
                creak = gameObject.AddComponent<AudioSource>();
                creak.playOnAwake = false;
                creak.spatialBlend = 1f;
                creak.rolloffMode = AudioRolloffMode.Linear;
                creak.minDistance = 2f;
                creak.maxDistance = creakDistance;
                creak.dopplerLevel = 0f;
            }
        }

        private void OnEnable()
        {
            MuseumLights.Changed += OnPowerChanged;
        }

        private void OnDisable()
        {
            MuseumLights.Changed -= OnPowerChanged;
        }

        [ContextMenu("断电")]
        private void DebugPowerOff()
        {
            OnPowerChanged(false);
        }

        [ContextMenu("来电")]
        private void DebugPowerOn()
        {
            OnPowerChanged(true);
        }

        private void OnPowerChanged(bool on)
        {
            foreach (Renderer r in bulbs)
            {
                if (r != null)
                {
                    r.sharedMaterial = on ? bulbOn : bulbOff;
                }
            }

            if (lamp != null)
            {
                lamp.enabled = on;
            }

            swaying = !on;
            if (swaying)
            {
                // 断电那一下：电流一断、灯一晃
                targetAmplitude = Random.Range(angleMin, angleMax);
                amplitude = Mathf.Max(amplitude, targetAmplitude * 0.6f);
                nudgeTimer = Random.Range(nudgeIntervalMin, nudgeIntervalMax);
            }
        }

        private void Update()
        {
            if (swaying)
            {
                nudgeTimer -= Time.deltaTime;
                if (nudgeTimer <= 0f)
                {
                    nudgeTimer = Random.Range(nudgeIntervalMin, nudgeIntervalMax);
                    targetAmplitude = Random.Range(angleMin, angleMax);
                }

                amplitude = Mathf.MoveTowards(amplitude, targetAmplitude, Time.deltaTime * 0.8f);
            }
            else
            {
                amplitude = Mathf.MoveTowards(amplitude, 0f, Time.deltaTime * settleSpeed);
            }

            if (amplitude <= 0.001f)
            {
                transform.localRotation = rest;
                return;
            }

            phase += Time.deltaTime * Mathf.PI * 2f / period;
            float s = Mathf.Sin(phase);
            // axis 是父节点空间里的水平轴（父节点不旋转），绕吊点前后摆
            transform.localRotation = Quaternion.AngleAxis(s * amplitude, axis) * rest;

            // 摆到最远端（速度换向）时嘎吱一声；只在断电摆起来时，来电后收摆的那几秒不响
            float delta = s - lastSin;
            if (swaying && delta * lastDelta < 0f && amplitude > 0.5f)
            {
                Creak();
            }

            lastDelta = delta;
            lastSin = s;
        }

        private void Creak()
        {
            AudioClip clip = creakClips[Random.Range(0, creakClips.Length)];
            if (creak == null || clip == null)
            {
                return;
            }

            creak.pitch = Random.Range(0.85f, 1.15f);
            creak.PlayOneShot(clip, creakVolume * Random.Range(0.7f, 1f));
        }
    }
}
