using Ib.Flow;
using Ib.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ib.Core
{
    /// <summary>
    /// 人群声 + 环境底噪 + 二楼厕所异响（老大 2026-11-01）。场景里不用摆：
    /// RuntimeInitializeOnLoadMethod 在场景加载后自己生成，素材在 Resources/Audio（Ib 美术，见 Docs/Ambient_Audio.md）。
    /// - CrowdMurmur：只在 NPC_Crowd 显示时循环（WorldState：GameState &lt;= PaintedGallery）。进画中馆 1.5 s 淡入，
    ///   人群消失 / 场景切换 2 s 淡出后停，之后不再响。
    /// - AmbientRoomTone：全程轻声铺底。断电时 3.5 s 交叉淡化到 AmbientRoomTone_PowerOut（素材没有就继续原版）。
    ///   主菜单不播，结局 CG 保留（GDD 要求过场保留环境音），Game Over 2 s 淡出，暂停由 AudioListener.pause 停住。
    /// - ToiletNoise：接完电话后从二楼厕所 CP2_Flashlight_Toilet 播一次（3D、1500 Hz 低通做隔墙感），
    ///   播完把任务提示换成 "Investigate Second Floor"。
    /// 音量层级：底噪 0.28 / 断电版 0.22，人群 0.55，BGM 0.5，心跳 0.25–0.95；追击时底噪再压 20%，让心跳出来。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmbientAudio : MonoBehaviour
    {
        private const float CrowdVolume = 0.55f;
        private const float CrowdFadeIn = 1.5f;
        private const float CrowdFadeOut = 2f;
        private const float RoomToneVolume = 0.28f;
        private const float RoomTonePowerOutVolume = 0.22f;
        private const float RoomToneFadeIn = 2f;
        private const float PowerOutCrossfade = 3.5f;
        private const float GameOverFade = 2f;
        private const float ToiletDelay = 0.7f;
        private const float ToiletBlend = 0.85f;
        private const float ToiletMinDistance = 2.5f;
        private const float ToiletMaxDistance = 35f;
        private const float ToiletLowPass = 1500f;

        private static AmbientAudio instance;

        private AudioSource crowd;
        private AudioSource roomTone;
        private AudioSource powerOutTone;
        private float powerOutMix;          // 0 = 原版，1 = 断电版
        private bool toiletPlayed;

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

            instance = new GameObject("--- AmbientAudio ---").AddComponent<AmbientAudio>();
        }

        private void Awake()
        {
            crowd = MakeLoop(Sfx.CrowdMurmur, 0f);
            roomTone = MakeLoop(Sfx.AmbientRoomTone, 0f);
            powerOutTone = MakeLoop(Sfx.AmbientRoomTonePowerOut, 0f);
            powerOutMix = 0f;
        }

        private AudioSource MakeLoop(string clipName, float volume)
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/" + clipName);
            if (clip == null)
            {
                return null;   // 素材没到：静音降级，不报错
            }

            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = volume;
            source.Play();
            return source;
        }

        private void Update()
        {
            if (GameFlow.IsPaused)
            {
                return;   // 暂停时整体被 AudioListener.pause 停住，包络也原地不动
            }

            float dt = Time.unscaledDeltaTime;
            bool playing = GameFlow.State != GameState.MainMenu;
            bool gameOver = GameOverScreen.IsShowing;

            UpdateCrowd(dt, playing && !gameOver);
            UpdateRoomTone(dt, playing, gameOver);
        }

        private void UpdateCrowd(float dt, bool audible)
        {
            if (crowd == null)
            {
                return;
            }

            bool want = audible
                && GameFlow.State <= GameState.PaintedGallery
                && WorldState.Crowd != null
                && WorldState.Crowd.activeInHierarchy;

            float target = want ? CrowdVolume : 0f;
            float speed = dt / (want ? CrowdFadeIn : CrowdFadeOut);
            crowd.volume = Mathf.MoveTowards(crowd.volume, target * AudioMix.Effects, speed * CrowdVolume);

            if (crowd.volume <= 0.0005f && crowd.isPlaying && !want)
            {
                crowd.Stop();
            }
            else if (want && !crowd.isPlaying)
            {
                crowd.Play();
            }
        }

        private void UpdateRoomTone(float dt, bool audible, bool gameOver)
        {
            bool wantPowerOut = GameFlow.State >= GameState.PowerOut;
            powerOutMix = Mathf.MoveTowards(powerOutMix, wantPowerOut ? 1f : 0f, dt / PowerOutCrossfade);

            // 追击时压 20%，别和心跳抢频段
            float duck = Mathf.Lerp(1f, 0.8f, ChaseHeartbeat.Level);
            float baseVolume = Mathf.Lerp(RoomToneVolume, RoomTonePowerOutVolume, powerOutMix) * duck * AudioMix.Effects;
            float fade = audible && !gameOver ? 1f : 0f;

            SetRoomVolume(roomTone, baseVolume * (1f - powerOutMix) * fade, dt, gameOver ? GameOverFade : RoomToneFadeIn);
            SetRoomVolume(powerOutTone, baseVolume * powerOutMix * fade, dt, gameOver ? GameOverFade : RoomToneFadeIn);
        }

        private static void SetRoomVolume(AudioSource source, float target, float dt, float fadeSeconds)
        {
            if (source == null)
            {
                return;
            }

            float step = fadeSeconds > 0f ? dt / fadeSeconds : 1f;
            source.volume = Mathf.MoveTowards(source.volume, target, step * Mathf.Max(RoomToneVolume, target));
        }

        /// <summary>
        /// 接完电话：0.7 s 后从二楼厕所播一次异响，播完再提示 "Investigate Second Floor"。
        /// 由 SceneTransitions.HangUp 调用（听筒底噪放完、控制还给玩家的那一刻）。
        /// </summary>
        public static void PhoneCallFinished()
        {
            if (instance == null || instance.toiletPlayed)
            {
                return;
            }

            instance.toiletPlayed = true;
            instance.StartCoroutine(instance.ToiletThenHint());
        }

        private System.Collections.IEnumerator ToiletThenHint()
        {
            yield return new WaitForSecondsRealtime(ToiletDelay);
            AudioClip clip = PlayToiletNoise();
            float wait = clip != null ? clip.length : 0f;
            yield return new WaitForSecondsRealtime(wait);
            GameFlow.ShowHintOnce(HintId.InvestigateSecondFloor);
            Debug.Log("[AmbientAudio] 二楼厕所异响播完 → 提示 Investigate Second Floor");
        }

        /// <summary>在 CP2_Flashlight_Toilet（厕所里）放一条 3D 的撞击/水管声，返回剪辑（缺失时 null）。</summary>
        private static AudioClip PlayToiletNoise()
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/ToiletNoise");
            if (clip == null)
            {
                Debug.LogWarning("[AmbientAudio] 找不到 Resources/Audio/ToiletNoise，跳过厕所异响。");
                return null;
            }

            GameObject go = new GameObject("Sfx_ToiletNoise");
            go.transform.position = ToiletPosition();
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = false;
            source.playOnAwake = false;
            source.spatialBlend = ToiletBlend;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = ToiletMinDistance;
            source.maxDistance = ToiletMaxDistance;
            source.dopplerLevel = 0f;
            source.volume = AudioMix.Effects;

            AudioLowPassFilter lowPass = go.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = ToiletLowPass;

            source.Play();
            Destroy(go, clip.length + 0.2f);
            return clip;
        }

        /// <summary>厕所里的声源位置：优先用检查点 CP2_Flashlight_Toilet，找不到就按灰盒常量（× WorldScale）。</summary>
        private static Vector3 ToiletPosition()
        {
            foreach (PlayerSpawnPoint mark in FindObjectsByType<PlayerSpawnPoint>(FindObjectsInactive.Include))
            {
                if (mark.name == "CP2_Flashlight_Toilet")
                {
                    return mark.transform.position + Vector3.up * 1.2f;
                }
            }

            return new Vector3(29.0f, 6.4f, 3.3f) * 1.4f + Vector3.up * 1.2f;
        }
    }
}
