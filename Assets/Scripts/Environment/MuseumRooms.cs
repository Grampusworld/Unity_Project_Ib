using UnityEngine;

namespace Ib.Environment
{
    /// <summary>
    /// 二楼值班室（监控室）在不在里面。二楼两只名画人物靠它判断「玩家躲进监控室就放弃追击」（老大 2026-11-01）。
    /// 不写死坐标、不改场景：运行时从 Walls_2F 的四面墙（Duty_North / Line_West / Ext_South / Ext_West）的
    /// 渲染包围盒量出内墙面，所以重新生成灰盒、改 WorldScale 都跟着走。墙找不到时退回按 1.4 倍灰盒算的常量。
    /// 只看 XZ；调用方自己确认玩家在二楼。
    /// </summary>
    internal static class MuseumRooms
    {
        /// <summary>门框附近也算屋里，避免玩家站在门洞里时判定来回跳。</summary>
        private const float DoorSlack = 0.35f;

        private static Bounds duty;
        private static bool built;

        public static bool InDutyRoom(Vector3 world)
        {
            EnsureBuilt();
            if (duty.size.x <= 0f)
            {
                return false;
            }

            return world.x >= duty.min.x - DoorSlack && world.x <= duty.max.x + DoorSlack
                && world.z >= duty.min.z - DoorSlack && world.z <= duty.max.z + DoorSlack;
        }

        private static void EnsureBuilt()
        {
            if (built)
            {
                return;
            }

            built = true;

            Transform museum = GameObject.Find("Level_Museum")?.transform;
            Transform walls = museum != null ? museum.Find("Walls_2F") : null;
            if (walls == null)
            {
                // 灰盒常量 × WorldScale 1.4：室内 x 9.31–17.99，z 0.21–7.21（2026-11-02 缩到 1/4）
                duty = new Bounds(new Vector3(13.65f, 8.96f, 3.71f), new Vector3(8.68f, 7f, 7.00f));
                return;
            }

            if (!TryBounds(walls, "Duty_North", out Bounds north)
                || !TryBounds(walls, "Line_West", out Bounds east)
                || !TryBounds(walls, "Ext_South", out Bounds south)
                || !(TryBounds(walls, "Duty_West", out Bounds west) || TryBounds(walls, "Ext_West", out west)))
            {
                duty = new Bounds(new Vector3(13.65f, 8.96f, 3.71f), new Vector3(8.68f, 7f, 7.00f));
                return;
            }

            float xMin = west.max.x;
            float xMax = east.min.x;
            float zMin = south.max.z;
            float zMax = north.min.z;
            if (xMax <= xMin + 1f || zMax <= zMin + 1f)
            {
                duty = new Bounds(new Vector3(13.65f, 8.96f, 3.71f), new Vector3(8.68f, 7f, 7.00f));
                return;
            }

            duty = new Bounds(
                new Vector3((xMin + xMax) * 0.5f, north.min.y + 0.5f, (zMin + zMax) * 0.5f),
                new Vector3(xMax - xMin, 1f, zMax - zMin));
            Debug.Log($"[MuseumRooms] 二楼监控室范围 x {xMin:F2}–{xMax:F2} · z {zMin:F2}–{zMax:F2}");
        }

        private static bool TryBounds(Transform walls, string name, out Bounds bounds)
        {
            bounds = default;
            Transform wall = walls.Find(name);
            if (wall == null)
            {
                return false;
            }

            bool any = false;
            foreach (Renderer r in wall.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null)
                {
                    continue;
                }

                if (!any)
                {
                    bounds = r.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            return any;
        }
    }
}

// 2026-11-02 值班室缩到 1/4：房间范围改为按 Duty_West 取。
