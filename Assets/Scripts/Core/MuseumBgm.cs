using Ib.Environment;
using Ib.Flow;
using Ib.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ib.Core
{
    /// <summary>
    /// 全局背景音乐：Resources/Audio/BGM_Museum.mp3（巴赫 G 弦上的咏叹调，公有领域，见同目录 LICENSE）。
    /// 2D 循环，RuntimeInitializeOnLoadMethod 在场景加载后自己生成，场景里不用摆。数值都在下面常量里：
    /// - MainMenu 不播；BlackRoom 按「出生点离大门的距离 → 0」SmoothStep 从 0 升到 MaxVolume；
    /// - PaintedGallery 及之后保持最大音量；
    /// - 第一次进 BlackRoomReturn：淡出并停，本局（到重新加载场景为止）不再播，状态往回跳也不播。
    /// 总音量走 AudioListener.volume（GameSettings），暂停走 AudioListener.pause，这里都不用管。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MuseumBgm : MonoBehaviour
    {
        private const float MaxVolume = 0.5f;
        private const float FadeOutSeconds = 2.5f;
        /// <summary>每帧平滑的时间常数（1/秒），越大跟得越紧。</summary>
        private const float SmoothSpeed = 3f;

        private static MuseumBgm instance;

        /// <summary>
        /// 0..1：BGM 自己的淡入包络（volume / MaxVolume，未乘音乐音量、未压心跳）。
        /// 人群声用它做同一个起点、同一条淡入（老大 2026-11-02）；MainMenu / 本局 BGM 停掉后为 0。
        /// </summary>
        public static float Envelope => instance != null && !finishedForRun ? Mathf.Clamp01(instance.volume / MaxVolume) : 0f;
        private static bool finishedForRun;

        private AudioSource source;
        private MuseumEntrance entrance;
        private FirstPersonController player;
        private float spawnDistance = -1f;
        private float volume;
        private float fadeFrom;
        private float fadeStart;
        private bool fadingOut;

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

            finishedForRun = false;
            instance = new GameObject("--- MuseumBgm ---").AddComponent<MuseumBgm>();
        }

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.clip = Resources.Load<AudioClip>("Audio/BGM_Museum");
            source.loop = true;
            source.volume = 0f;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        private void Update()
        {
            if (finishedForRun)
            {
                return;
            }

            // 第一次进黑屋回程：开始淡出，播完这轮就本局封麦
            if (!fadingOut && GameFlow.State == GameState.BlackRoomReturn)
            {
                fadingOut = true;
                fadeStart = Time.unscaledTime;
                fadeFrom = volume;
            }

            if (fadingOut)
            {
                volume = fadeFrom * (1f - Mathf.Clamp01((Time.unscaledTime - fadeStart) / FadeOutSeconds));
                source.volume = volume;
                if (volume <= 0.001f)
                {
                    source.Stop();
                    finishedForRun = true;
                }

                return;
            }

            if (GameFlow.State == GameState.MainMenu)
            {
                volume = 0f;
                source.volume = 0f;
                return;
            }

            float target = MaxVolume;
            if (GameFlow.State == GameState.BlackRoom)
            {
                float k = 1f - SpawnProgress(); // 0 = 刚出生，1 = 走到门口
                target = MaxVolume * Mathf.SmoothStep(0f, 1f, k);
            }

            // 每帧平滑，传送 / 状态切换时不跳变
            volume += (target - volume) * (1f - Mathf.Exp(-SmoothSpeed * Time.deltaTime));
            // 被追击时压低约 5 dB，让心跳出来（ChaseHeartbeat.Level 自己带淡入淡出，这里不会跳变）
            source.volume = volume * AudioMix.Music * (1f - 0.45f * ChaseHeartbeat.Level);

            if (!source.isPlaying && volume > 0.01f)
            {
                source.Play();
            }
        }

        /// <summary>玩家现在离大门的距离占出生时距离的比例（1 = 刚出生那么远）。</summary>
        private float SpawnProgress()
        {
            if (entrance == null)
            {
                entrance = FindAnyObjectByType<MuseumEntrance>();
            }

            if (entrance == null)
            {
                return 1f;
            }

            // 用出生点自身的位置量「出生时离门的距离」，不受玩家传送时机影响
            if (spawnDistance < 0f)
            {
                Vector3 spawnPos = entrance.transform.position;
                foreach (PlayerSpawnPoint mark in FindObjectsByType<PlayerSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (mark.name == Checkpoints.BlackRoom)
                    {
                        spawnPos = mark.transform.position;
                        break;
                    }
                }

                spawnDistance = Mathf.Max(0.1f, Vector3.Distance(spawnPos, entrance.transform.position));
            }

            if (player == null)
            {
                player = FindAnyObjectByType<FirstPersonController>();
            }

            if (player == null)
            {
                return 1f;
            }

            return Mathf.Clamp01(Vector3.Distance(player.transform.position, entrance.transform.position) / spawnDistance);
        }
    }
}
