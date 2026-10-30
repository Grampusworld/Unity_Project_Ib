using Ib.Environment;
using UnityEngine;

namespace Ib.Flow
{
    /// <summary>
    /// 每个状态下场景该是什么样（GDD §5.3），只管不属于任何 bot 组件的几件事：
    /// NPC 人群、黑屋第二次的空画框、停电灯光。按生成器里的物体名找（名字改了要同步这里）。
    /// 天使阶段、人台出动由 Ib Threats 按 StateChanged + HasFlashlight 自己算（ThreatSchedule）。
    /// </summary>
    internal static class WorldState
    {
        private const string CrowdName = "NPC_Crowd";
        private const string FrameName = "EmptyFrame_SecondVisit";
        private const string BlackRoomName = "Level_BlackRoom";

        private static GameObject crowd;
        private static GameObject frame;
        private static GameObject blackRoom;

        internal static GameObject BlackRoom => blackRoom;

        /// <summary>NPC 人群根节点。交互球扫要穿过人群（全家福前站着 Grampus）。</summary>
        internal static GameObject Crowd => crowd;

        internal static void Cache()
        {
            crowd = frame = blackRoom = null;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                switch (t.name)
                {
                    case CrowdName: crowd = t.gameObject; break;
                    case FrameName: frame = t.gameObject; break;
                    case BlackRoomName: blackRoom = t.gameObject; break;
                }
            }
        }

        internal static void Apply(GameState s)
        {
            if (crowd != null)
            {
                crowd.SetActive(s <= GameState.PaintedGallery);
            }

            if (frame != null)
            {
                frame.SetActive(s == GameState.BlackRoomReturn);
            }

            // 停电后不再来电；只有调试读回 AfterHours 时才重新开灯
            if (s >= GameState.PowerOut && s != GameState.Ending && MuseumLights.On)
            {
                MuseumLights.Set(false);
            }
            else if (s == GameState.AfterHours && !MuseumLights.On)
            {
                MuseumLights.Set(true);
            }
        }
    }
}
