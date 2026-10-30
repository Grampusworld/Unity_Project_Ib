using System;
using UnityEngine;
using UnityEngine.UI;

namespace Ib.UI
{
    /// <summary>主菜单和暂停菜单共用的设置页：Master Volume、Mouse Sensitivity。改动立即生效并存盘。</summary>
    internal sealed class SettingsPanel : MonoBehaviour
    {
        private GameObject view;
        private Slider volume;
        private Slider sensitivity;
        private Action onClosed;

        public bool IsOpen => view.activeSelf;

        public static SettingsPanel Create(Transform parent)
        {
            GameObject go = UIFactory.Empty(parent, "Settings");
            var panel = go.AddComponent<SettingsPanel>();
            panel.view = UIFactory.Panel(go.transform, "Dim", new Color(0f, 0f, 0f, 0.85f));

            UIFactory.Label(panel.view.transform, "Title", "Settings", 44, TextAnchor.MiddleCenter, Color.white)
                .rectTransform.anchoredPosition = new Vector2(0f, 260f);

            RectTransform column = UIFactory.Column(panel.view.transform, "Items", new Vector2(0.5f, 0.45f), new Vector2(460f, 360f), 14f);
            Row(column, "Master Volume");
            panel.volume = UIFactory.Slider(column, 0f, 1f, 1f, v => GameSettings.Volume = v);
            Row(column, "Mouse Sensitivity");
            panel.sensitivity = UIFactory.Slider(column, GameSettings.SensitivityMin, GameSettings.SensitivityMax, 0.15f, v => GameSettings.Sensitivity = v);
            UIFactory.Empty(column, "Gap").GetComponent<RectTransform>().sizeDelta = new Vector2(10f, 30f);
            UIFactory.Button(column, "Back", panel.Close);

            panel.view.SetActive(false);
            return panel;
        }

        private static void Row(Transform parent, string text)
        {
            Text t = UIFactory.Label(parent, text, text, 26, TextAnchor.MiddleLeft, new Color(0.85f, 0.85f, 0.85f));
            t.rectTransform.sizeDelta = new Vector2(420f, 36f);
        }

        public void Open(Action closed)
        {
            onClosed = closed;
            volume.SetValueWithoutNotify(GameSettings.Volume);
            sensitivity.SetValueWithoutNotify(GameSettings.Sensitivity);
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
