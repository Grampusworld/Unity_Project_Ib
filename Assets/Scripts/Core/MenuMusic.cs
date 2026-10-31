using Ib.Flow;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ib.Core
{
    /// <summary>
    /// 主菜单音乐：Resources/Audio/MenuLullaby.wav（勃拉姆斯《摇篮曲》八音盒版，Tools/gen_lullaby.py 合成，公有领域）。
    /// 2D 循环，场景加载后自己生成。只在 MainMenu 播；点开始（离开 MainMenu）后淡出、停掉，之后交给 MuseumBgm。
    /// 总音量走 AudioListener.volume（GameSettings）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuMusic : MonoBehaviour
    {
        private const float Volume = 0.45f;
        private const float FadeInSeconds = 2f;
        private const float FadeOutSeconds = 2.5f;

        private static MenuMusic instance;

        private AudioSource source;
        private bool fadingOut;
        private float fadeFrom;

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
            }

            instance = new GameObject("--- MenuMusic ---").AddComponent<MenuMusic>();
        }

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.clip = Resources.Load<AudioClip>("Audio/MenuLullaby");
            source.loop = true;
            source.volume = 0f;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        private void Update()
        {
            if (source.clip == null)
            {
                return;
            }

            if (!fadingOut && GameFlow.State != GameState.MainMenu)
            {
                // 没有主菜单的场景（直接开玩）一开始就不在 MainMenu：从来没响过，直接收工
                if (!source.isPlaying)
                {
                    Destroy(gameObject);
                    return;
                }

                fadingOut = true;
                fadeFrom = source.volume;
            }

            if (fadingOut)
            {
                source.volume = Mathf.MoveTowards(source.volume, 0f, fadeFrom / FadeOutSeconds * Time.unscaledDeltaTime);
                if (source.volume <= 0.001f)
                {
                    Destroy(gameObject);
                }

                return;
            }

            if (!source.isPlaying)
            {
                source.Play();
            }

            float target = Volume * AudioMix.Music;
            source.volume = Mathf.MoveTowards(source.volume, target, Volume / FadeInSeconds * Time.unscaledDeltaTime);
        }
    }
}
