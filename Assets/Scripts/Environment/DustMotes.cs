using UnityEngine;

namespace Ib.Environment
{
    /// <summary>
    /// 空气里的白色浮尘。运行时在本物体上搭一个粒子系统，发射盒子每帧跟着主相机走，
    /// 粒子在世界空间里慢慢飘（玩家走动时尘埃不会跟着人跑）。
    /// 材质用受光的 Particles/Lit：只在有光的地方看得见，射灯的光柱里最明显，暗处自然隐掉。
    /// gate 物体没激活时（推门前，馆内灯组还关着）不发射，黑屋里看不到浮尘。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DustMotes : MonoBehaviour
    {
        [SerializeField] private Material moteMaterial;
        [Tooltip("这个物体激活时才有浮尘（一般是 Museum_Deferred 灯组）。留空 = 一直有。")]
        [SerializeField] private GameObject gate;

        [Header("密度与范围")]
        [SerializeField, Range(50, 3000)] private int maxParticles = 900;
        [SerializeField] private Vector3 volumeSize = new Vector3(14f, 7f, 14f);
        [SerializeField, Min(1f)] private float lifetime = 14f;

        [Header("外观")]
        [SerializeField, Min(0.001f)] private float sizeMin = 0.012f;
        [SerializeField, Min(0.001f)] private float sizeMax = 0.04f;
        [SerializeField, Range(0f, 1f)] private float alphaMin = 0.25f;
        [SerializeField, Range(0f, 1f)] private float alphaMax = 0.8f;

        [Header("漂浮")]
        [SerializeField, Min(0f)] private float driftSpeed = 0.03f;
        [SerializeField, Min(0f)] private float turbulence = 0.05f;
        [SerializeField, Min(0.01f)] private float turbulenceFrequency = 0.25f;
        [SerializeField] private float gravity = -0.002f;   // 负值 = 极慢地往上飘

        private ParticleSystem system;
        private Transform viewer;

        public int MaxParticles
        {
            get => maxParticles;
            set
            {
                maxParticles = Mathf.Clamp(value, 50, 3000);
                Configure();
            }
        }

        private void Awake()
        {
            system = GetComponent<ParticleSystem>();
            if (system == null)
            {
                system = gameObject.AddComponent<ParticleSystem>();
            }

            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Configure();
        }

        private void OnValidate()
        {
            if (system != null)
            {
                Configure();
            }
        }

        private void Configure()
        {
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.prewarm = true;
            if (!system.isPlaying)
            {
                main.duration = lifetime;   // 播放中改 duration 会报错
            }
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, driftSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, alphaMin), new Color(1f, 0.97f, 0.92f, alphaMax));
            main.gravityModifier = gravity;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = maxParticles / lifetime;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = volumeSize;
            shape.randomDirectionAmount = 1f;

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = turbulence;
            noise.frequency = turbulenceFrequency;
            noise.scrollSpeed = 0.05f;
            noise.octaveCount = 2;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            // 出生和消失都淡入淡出，避免尘点突然冒出来
            ParticleSystem.ColorOverLifetimeModule col = system.colorOverLifetime;
            col.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = fade;

            ParticleSystemRenderer r = system.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = moteMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.minParticleSize = 0f;
            r.maxParticleSize = 0.05f;
        }

        private void LateUpdate()
        {
            bool open = gate == null || gate.activeInHierarchy;
            if (!open)
            {
                if (system.isPlaying || system.particleCount > 0)
                {
                    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                return;
            }

            if (viewer == null)
            {
                Camera cam = Camera.main;
                if (cam == null)
                {
                    return;
                }

                viewer = cam.transform;
            }

            // 只移动发射盒；已经发出的粒子在世界空间里，不会跟着动
            transform.position = viewer.position;

            if (!system.isPlaying)
            {
                system.Play(true);
            }
        }
    }
}
