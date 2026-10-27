using Ib.Player;
using UnityEngine;

namespace Ib.Environment
{
    /// <summary>
    /// 全馆灯光总开关（调试菜单用，以后"停电"状态也可以直接调它）。
    /// 关：场景里所有 Light 组件 enabled = false（手电除外）。
    /// 开：全部 enabled = true，并把还没激活的灯组（比如推门前的 Museum_Deferred）一起激活。
    /// </summary>
    public static class MuseumLights
    {
        public static bool On { get; private set; } = true;

        public static void Toggle() => Set(!On);

        public static void Set(bool on)
        {
            On = on;
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            foreach (Light light in lights)
            {
                if (light.GetComponentInParent<PlayerFlashlight>(true) != null)
                {
                    continue;
                }

                light.enabled = on;
                if (on)
                {
                    // 灯组父物体可能被 MuseumEntrance 关着，一路往上激活；
                    // 但只激活纯灯组 —— 碰到带模型的父物体（比如已经隐藏的黑屋）就停，别把整个房间翻出来
                    for (Transform t = light.transform; t != null; t = t.parent)
                    {
                        if (t.gameObject.activeSelf)
                        {
                            continue;
                        }
                        if (t.GetComponentsInChildren<Renderer>(true).Length > 0)
                        {
                            break;
                        }
                        t.gameObject.SetActive(true);
                    }
                }
            }
        }
    }
}
