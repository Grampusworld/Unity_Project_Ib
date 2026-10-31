using System.Collections.Generic;
using UnityEngine;

namespace Ib.Core
{
    /// <summary>
    /// 音效：剪辑放在 Resources/Audio（Tools/gen_sfx.py 合成），按名字加载、缓存。
    /// 一次性的在世界里临时生成一个 3D AudioSource 播完就删；循环的（电话铃）挂在物体上由调用方管。
    /// </summary>
    public static class Sfx
    {
        public const string PhoneRing = "PhoneRing";
        public const string LineHiss = "LineHiss";
        public const string PowerClunk = "PowerClunk";
        public const string KeyClick = "KeyClick";
        public const string LockRattle = "LockRattle";
        public const string StoneScrape = "StoneScrape";
        public const string AngelScream = "AngelScream";
        public const string AngelScreamRec = "AngelScreamRec";

        private static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        public static AudioClip Load(string name)
        {
            if (cache.TryGetValue(name, out AudioClip clip) && clip != null)
            {
                return clip;
            }

            clip = Resources.Load<AudioClip>("Audio/" + name);
            if (clip == null)
            {
                Debug.LogWarning("[Sfx] 找不到 Resources/Audio/" + name);
            }

            cache[name] = clip;
            return clip;
        }

        /// <summary>在 position 放一次。maxDistance 是听得见的最远距离（线性衰减）。</summary>
        public static void PlayAt(string name, Vector3 position, float volume = 1f, float maxDistance = 30f)
        {
            AudioClip clip = Load(name);
            if (clip == null)
            {
                return;
            }

            GameObject go = new GameObject("Sfx_" + name);
            go.transform.position = position;
            AudioSource source = Attach(go, clip, false, volume, maxDistance);
            source.Play();
            Object.Destroy(go, clip.length + 0.1f);
        }

        /// <summary>2D 一次性播放（UI / jump scare），听得到就播完，与位置无关。</summary>
        public static void PlayUI(string name, float volume)
        {
            AudioClip clip = Load(name);
            if (clip == null)
            {
                return;
            }

            GameObject go = new GameObject("SfxUI_" + name);
            AudioSource source = Attach(go, clip, false, volume, 30f);
            source.spatialBlend = 0f;
            source.Play();
            Object.Destroy(go, clip.length + 0.1f);
        }

        /// <summary>在 host 上加一个 3D AudioSource（不自动播放）。</summary>
        public static AudioSource Attach(GameObject host, AudioClip clip, bool loop, float volume, float maxDistance)
        {
            AudioSource source = host.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.volume = volume;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.5f;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
            return source;
        }
    }
}
