using Ib.Core;
using Ib.Player;
using UnityEngine;

namespace Ib.UI
{
    /// <summary>设置项：主音量、音乐/音效音量、鼠标灵敏度。存 PlayerPrefs，没存过就不覆盖 Player 预制体上的值。</summary>
    internal static class GameSettings
    {
        private const string VolumeKey = "ib.volume";
        private const string MusicVolumeKey = "ib.volume.music";
        private const string EffectsVolumeKey = "ib.volume.effects";
        private const string SensitivityKey = "ib.sensitivity";
        public const float SensitivityMin = 0.03f;
        public const float SensitivityMax = 0.6f;
        private const float SensitivityDefault = 0.15f; // PlayerLook 的默认值

        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, 1f);
            set
            {
                PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
                AudioListener.volume = Volume;
            }
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
            set
            {
                PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
                AudioMix.Music = MusicVolume;
            }
        }

        public static float EffectsVolume
        {
            get => PlayerPrefs.GetFloat(EffectsVolumeKey, 1f);
            set
            {
                PlayerPrefs.SetFloat(EffectsVolumeKey, Mathf.Clamp01(value));
                AudioMix.Effects = EffectsVolume;
            }
        }

        public static float Sensitivity
        {
            get
            {
                if (PlayerPrefs.HasKey(SensitivityKey)) return PlayerPrefs.GetFloat(SensitivityKey);
                PlayerLook look = Object.FindAnyObjectByType<PlayerLook>();
                return look != null ? look.Sensitivity : SensitivityDefault;
            }
            set
            {
                PlayerPrefs.SetFloat(SensitivityKey, Mathf.Clamp(value, SensitivityMin, SensitivityMax));
                ApplySensitivity();
            }
        }

        public static void ApplyAll()
        {
            AudioListener.volume = Volume;
            AudioMix.Music = MusicVolume;
            AudioMix.Effects = EffectsVolume;
            ApplySensitivity();
        }

        private static void ApplySensitivity()
        {
            if (!PlayerPrefs.HasKey(SensitivityKey)) return;
            float v = PlayerPrefs.GetFloat(SensitivityKey);
            PlayerLook look = Object.FindAnyObjectByType<PlayerLook>();
            if (look != null) look.Sensitivity = v;
        }
    }
}
