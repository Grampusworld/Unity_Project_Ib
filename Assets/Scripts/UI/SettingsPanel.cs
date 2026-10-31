using System;
using Ib.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ib.UI
{
    /// <summary>
    /// 主菜单和暂停菜单共用的设置页：Master / Music / Effects Volume、Mouse Sensitivity。改动立即生效并存盘。
    /// 暂停时 AudioListener.pause 让场景全静音，拖音量条听不出变化，所以松手时放一声忽略暂停的试听音。
    /// </summary>
    internal sealed class SettingsPanel : MonoBehaviour
    {
        private const string PreviewClip = Sfx.KeyClick;

        private GameObject view;
        private Slider volume;
        private Slider music;
        private Slider effects;
        private Slider sensitivity;
        private Text volumeValue;
        private Text musicValue;
        private Text effectsValue;
        private Action onClosed;
        private AudioSource preview;

        public bool IsOpen => view.activeSelf;

        public static SettingsPanel Create(Transform parent)
        {
            GameObject go = UIFactory.Empty(parent, "Settings");
            var panel = go.AddComponent<SettingsPanel>();
            panel.view = UIFactory.Panel(go.transform, "Dim", new Color(0f, 0f, 0f, 0.85f));

            UIFactory.Label(panel.view.transform, "Title", "Settings", 44, TextAnchor.MiddleCenter, Color.white)
                .rectTransform.anchoredPosition = new Vector2(0f, 300f);

            RectTransform column = UIFactory.Column(panel.view.transform, "Items", new Vector2(0.5f, 0.45f), new Vector2(460f, 520f), 10f);

            panel.volumeValue = Row(column, "Master Volume");
            panel.volume = UIFactory.Slider(column, 0f, 1f, 1f, v =>
            {
                GameSettings.Volume = v;
                panel.volumeValue.text = Percent(v);
            });
            panel.PreviewOnRelease(panel.volume);

            panel.musicValue = Row(column, "Music Volume");
            panel.music = UIFactory.Slider(column, 0f, 1f, 1f, v =>
            {
                GameSettings.MusicVolume = v;
                panel.musicValue.text = Percent(v);
            });

            panel.effectsValue = Row(column, "Effects Volume");
            panel.effects = UIFactory.Slider(column, 0f, 1f, 1f, v =>
            {
                GameSettings.EffectsVolume = v;
                panel.effectsValue.text = Percent(v);
            });
            panel.PreviewOnRelease(panel.effects);

            Row(column, "Mouse Sensitivity");
            panel.sensitivity = UIFactory.Slider(column, GameSettings.SensitivityMin, GameSettings.SensitivityMax, 0.15f, v => GameSettings.Sensitivity = v);
            UIFactory.Empty(column, "Gap").GetComponent<RectTransform>().sizeDelta = new Vector2(10f, 24f);
            UIFactory.Button(column, "Back", panel.Close);

            panel.view.SetActive(false);
            return panel;
        }

        /// <summary>标题在左、百分比在右的一行；返回右侧数值文字（灵敏度那行不用）。</summary>
        private static Text Row(Transform parent, string text)
        {
            Text t = UIFactory.Label(parent, text, text, 26, TextAnchor.MiddleLeft, new Color(0.85f, 0.85f, 0.85f));
            t.rectTransform.sizeDelta = new Vector2(420f, 36f);
            Text value = UIFactory.Label(t.transform, "Value", "", 22, TextAnchor.MiddleRight, new Color(1f, 0.88f, 0.62f, 0.85f));
            UIFactory.Stretch(value.gameObject);
            return value;
        }

        private static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";

        private void PreviewOnRelease(Slider slider)
        {
            var trigger = slider.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            entry.callback.AddListener(_ => PlayPreview());
            trigger.triggers.Add(entry);
        }

        private void PlayPreview()
        {
            AudioClip clip = Sfx.Load(PreviewClip);
            if (clip == null) return;

            if (preview == null)
            {
                preview = gameObject.AddComponent<AudioSource>();
                preview.playOnAwake = false;
                preview.spatialBlend = 0f;
                preview.ignoreListenerPause = true; // 暂停时也要听得到
            }

            preview.volume = 0.8f * AudioMix.Effects;
            preview.PlayOneShot(clip);
        }

        public void Open(Action closed)
        {
            onClosed = closed;
            volume.SetValueWithoutNotify(GameSettings.Volume);
            music.SetValueWithoutNotify(GameSettings.MusicVolume);
            effects.SetValueWithoutNotify(GameSettings.EffectsVolume);
            sensitivity.SetValueWithoutNotify(GameSettings.Sensitivity);
            volumeValue.text = Percent(volume.value);
            musicValue.text = Percent(music.value);
            effectsValue.text = Percent(effects.value);
            view.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Close()
        {
            if (!IsOpen) return;
            view.SetActive(false);
            PlayerPrefs.Save();
            Action cb = onClosed;
            onClosed = null;
            cb?.Invoke();
        }
    }
}
