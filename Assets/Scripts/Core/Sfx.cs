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
        /// <summary>名画人物破框出场：爆裂 + 碎屑 + 木框闷响 + 展厅混响，2.2 s。</summary>
        public const string GlassBreak = "GlassBreak";
        /// <summary>名画人物从画框里冲出来（伏击）：爆点 + 两层女声尖叫 + 混响，2.35 s（Ib 美术）。</summary>
        public const string PaintingScream = "PaintingScream";
        /// <summary>被追击心跳：1.000 s = 60 BPM 一拍，无缝循环（Ib 美术）。</summary>
        public const string ChaseHeartbeat = "ChaseHeartbeat";
        /// <summary>开馆人群声：24 s 无缝循环，只在 NPC_Crowd 显示时播（Ib 美术）。</summary>
        public const string CrowdMurmur = "CrowdMurmur";
        /// <summary>环境底噪：40 s 无缝循环，全程铺底（Ib 美术）。</summary>
        public const string AmbientRoomTone = "AmbientRoomTone";
        /// <summary>断电后的阴冷底噪：40 s 无缝循环，断电时交叉淡入（Ib 美术）。</summary>
        public const string AmbientRoomTonePowerOut = "AmbientRoomTone_PowerOut";
        /// <summary>二楼厕所异响：3.2 s，接完电话后播一次（Ib 美术）。</summary>
        public const string ToiletNoise = "ToiletNoise";
        /// <summary>监控雪花底噪：3.0 s 无缝循环，随屏幕雪花（_Static）密度调音量和音高（Ib 技术合成）。</summary>
        public const string MonitorStatic = "MonitorStatic";
        public const string PaperFlip = "PaperFlip";
        public const string DoorSlam = "DoorSlam";
        public const string DoorCreak = "DoorCreak";

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
            source.volume = volume * AudioMix.Effects;
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
