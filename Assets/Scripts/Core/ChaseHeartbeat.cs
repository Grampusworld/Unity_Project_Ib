using Ib.Flow;
using Ib.Player;
using Ib.Threats;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ib.Core
{
    /// <summary>
    /// 被追击心跳（老大 2026-11-01）：任何怪物在追玩家（哭泣天使 IsChasing、名画人物 IsChasing）时循环播 Resources/Audio/ChaseHeartbeat，
    /// 2D、无距离衰减；离最近的追击者越近越响、越快（pitch 1.0–1.8 = 60–108 BPM，参数按 Docs/Chase_Audio_Viewmodel.md）。
    /// 开始追击约 1 s 淡入，摆脱追击约 2.5 s 淡出、淡出时心跳同时放慢；结局 / Game Over / 被抓（jump scare）时也淡出。
    /// 暂停时整体静音由 GameFlow 的 AudioListener.pause 负责，这里只是停住淡入淡出。
    /// RuntimeInitializeOnLoadMethod 自己生成，场景里不用摆。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChaseHeartbeat : MonoBehaviour
    {
        private const float FarDistance = 14f;
        private const float NearDistance = 2f;
        private const float MinPitch = 1f;
        private const float MaxPitch = 1.8f;
        private const float MinVolume = 0.25f;
        private const float MaxVolume = 0.95f;
        private const float FadeInSeconds = 1f;
        private const float FadeOutSeconds = 2.5f;
        private const float FastFadeOutSeconds = 0.6f;
        private const float FollowSeconds = 0.4f;

        private static ChaseHeartbeat instance;

        /// <summary>0..1：当前心跳的总体强度（淡入淡出 × 远近），BGM 用它压低自己让心跳出来。</summary>
        public static float Level { get; private set; }

        /// <summary>这一帧是否有怪物在追玩家。</summary>
        public static bool AnyChasing { get; private set; }

        private AudioSource source;
        private FirstPersonController player;
        private float fade;          // 0..1 淡入淡出包络
        private float closeness;     // 0..1 远近（平滑后）
        private float closenessVel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            Level = 0f;
            AnyChasing = false;
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

            Level = 0f;
            instance = new GameObject("--- ChaseHeartbeat ---").AddComponent<ChaseHeartbeat>();
        }

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.clip = Sfx.Load(Sfx.ChaseHeartbeat);
            source.loop = true;
            source.volume = 0f;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
        }

        private void Update()
        {
            if (source.clip == null || GameFlow.IsPaused)
            {
                return;   // 暂停：声音已被 AudioListener.pause 停住，包络也原地不动
            }

            float dt = Time.unscaledDeltaTime;
            float distance = NearestChaserDistance();
            AnyChasing = distance < float.PositiveInfinity;

            bool blocked = GameFlow.State == GameState.Ending || GameOverScreen.IsShowing || AngelJumpScare.Playing;
            bool on = AnyChasing && !blocked;

            if (on)
            {
                float t = Mathf.InverseLerp(FarDistance, NearDistance, distance);
                closeness = Mathf.SmoothDamp(closeness, t, ref closenessVel, FollowSeconds, Mathf.Infinity, dt);
                fade = Mathf.MoveTowards(fade, 1f, dt / FadeInSeconds);
            }
            else
            {
                // 摆脱追击：慢慢淡出、心跳慢慢平复；被抓 / 结局时快一点收掉，别压在尖叫上
                closeness = Mathf.SmoothDamp(closeness, 0f, ref closenessVel, FadeOutSeconds * 0.6f, Mathf.Infinity, dt);
                fade = Mathf.MoveTowards(fade, 0f, dt / (blocked ? FastFadeOutSeconds : FadeOutSeconds));
            }

            float volume = Mathf.Lerp(MinVolume, MaxVolume, closeness * closeness) * fade;
            Level = fade * Mathf.Lerp(0.6f, 1f, closeness);
            source.pitch = Mathf.Lerp(MinPitch, MaxPitch, closeness);
            source.volume = volume * AudioMix.Effects;

            if (fade > 0.001f)
            {
                if (!source.isPlaying)
                {
                    source.Play();
                }
            }
            else if (source.isPlaying)
            {
                source.Stop();
                closeness = 0f;
                closenessVel = 0f;
            }
        }

        /// <summary>离玩家最近的「正在追」的怪物的距离；没人追 = 无穷大。</summary>
        private float NearestChaserDistance()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<FirstPersonController>();
                if (player == null)
                {
                    return float.PositiveInfinity;
                }
            }

            Vector3 p = player.transform.position;
            float best = float.PositiveInfinity;

            foreach (WeepingAngel angel in WeepingAngel.All)
            {
                if (angel != null && angel.IsChasing)
                {
                    best = Mathf.Min(best, Vector3.Distance(p, angel.transform.position));
                }
            }

            foreach (PaintingFigure figure in PaintingFigure.All)
            {
                if (figure != null && figure.IsActive && figure.IsChasing)
                {
                    best = Mathf.Min(best, Vector3.Distance(p, figure.transform.position));
                }
            }

            return best;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                Level = 0f;
                AnyChasing = false;
            }
        }
    }
}
