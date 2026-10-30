using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ib.EditorTools
{
    /// <summary>
    /// 画中馆的人群站位占位（2026-10-28）：纯白圆柱，带碰撞，固定站位、面朝展品（GDD：人山人海、固定站位、不看你）。
    /// 规则：
    ///   - 每件展品（画、展板、雕像、展台、首饰柜、玻璃罩人台、挂衣、Lily 的裙子）旁边 1–2 个；
    ///   - 站在隔离绳外、离墙上的画至少 2 m（放大后约 3 m 以上）；
    ///   - 哭泣天使周围 4.5 m 内一个都没有，也没有人围观它；
    ///   - 不挡门前通道、楼梯口、人台和天使的巡逻线、二层修复角的隔离区；
    ///   - 身高 1.32–1.75 m（放大后 1.85–2.45 m），都比 1.3 m 的主角高，高矮不一；
    ///   - 随机数按展品名字做种子，重新生成站位不变。
    /// 全部放在 NPC_Crowd 下按房间分组，以后状态切换（闭馆后人群消失）直接关这个根节点。
    /// </summary>
    public static partial class GrayboxBuilder
    {
        private const float NpcMinHeight = 1.32f;
        private const float NpcMaxHeight = 1.75f;
        private const float AngelClearRadius = 4.5f;

        private struct Segment
        {
            public Vector2 A;
            public Vector2 B;
            public float Clearance;

            public Segment(float ax, float az, float bx, float bz, float clearance)
            {
                A = new Vector2(ax, az);
                B = new Vector2(bx, bz);
                Clearance = clearance;
            }
        }

        private struct Zone
        {
            public Rect R;
            public bool SecondFloor;

            public Zone(float x0, float x1, float z0, float z1, bool second)
            {
                R = Rect.MinMaxRect(x0, z0, x1, z1);
                SecondFloor = second;
            }
        }

        private static void BuildCrowd(List<Exhibit> exhibits, Transform museum)
        {
            Physics.SyncTransforms();
            Transform root = new GameObject("NPC_Crowd").transform;
            Material white = Lit("M_NPC_White", null, null, new Color(0.93f, 0.93f, 0.92f), 0.15f, 1f, 0f, false);

            List<Exhibit> all = new List<Exhibit>(exhibits);
            all.AddRange(CostumeExhibits());

            List<Segment> lines = BlockedLines();
            List<Zone> zones = BlockedZones(museum);
            List<Vector3> placed = new List<Vector3>();
            Dictionary<string, Transform> groups = new Dictionary<string, Transform>();
            int total = 0;
            int skipped = 0;

            foreach (Exhibit e in all)
            {
                bool second = e.B.center.y > Floor2Y - 0.5f;
                float floorY = second ? Floor2Y : Floor1Y;
                Vector3 probe = e.Normal != Vector3.zero ? e.B.center + e.Normal * 0.6f : e.B.center;
                RoomStyle room = RoomAt(probe.x, probe.z, second);
                // 全家福厅只站一个人（Grampus），在循环外面单独放
                if (room == null || room.Name == "Family")
                {
                    continue;
                }

                uint h = Fnv(e.Name);
                int count = e.Name.StartsWith("Garments_") ? 1 : 1 + (int)(h % 2);
                if (e.Normal != Vector3.zero && WallWidth(e) > 4f)
                {
                    count = 2;
                }

                System.Random rng = new System.Random((int)(h & 0x7fffffff));
                if (!groups.TryGetValue(room.Name + (second ? "_2F" : "_1F"), out Transform group))
                {
                    group = NewGroup(root, room.Name + (second ? "_2F" : "_1F"));
                    groups[room.Name + (second ? "_2F" : "_1F")] = group;
                }

                for (int j = 0; j < count; j++)
                {
                    float height = NpcMinHeight + (float)rng.NextDouble() * (NpcMaxHeight - NpcMinHeight);
                    float radius = 0.21f + (float)rng.NextDouble() * 0.05f;
                    if (TryFindSpot(e, j, count, rng, floorY, second, room, radius, height, lines, zones, placed, out Vector3 spot))
                    {
                        placed.Add(spot);
                        Npc(group, "NPC_" + e.Name + "_" + j, spot, e.B.center, height, radius, white);
                        total++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
            }

            // 全家福厅：只有一个人 —— Grampus，站在全家福画架前、面朝画、背对门口（不进门前通道）
            Transform family = NewGroup(root, "Family_2F");
            Vector3 easel = new Vector3(30.5f, Floor2Y, FamilyDoorZ);
            Vector3 grampusFoot = new Vector3(29.75f, Floor2Y, FamilyDoorZ);
            Npc(family, "NPC_Grampus_FamilyPortrait", grampusFoot, easel + Vector3.up, 1.7f, 0.24f, white);
            total++;

            Debug.Log("[GrayboxBuilder] 人群站位：" + total + " 个" + (skipped > 0 ? "（" + skipped + " 个找不到合适位置，跳过）" : "") + "。");
        }

        private static void Npc(Transform parent, string name, Vector3 foot, Vector3 lookAt, float height, float radius, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = foot + Vector3.up * (height * 0.5f);
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            Vector3 look = lookAt - foot;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f)
            {
                go.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            }

            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // ---------------------------------------------------------------- 找位置

        private static bool TryFindSpot(Exhibit e, int j, int count, System.Random rng, float floorY, bool second, RoomStyle room,
            float radius, float height, List<Segment> lines, List<Zone> zones, List<Vector3> placed, out Vector3 spot)
        {
            if (e.Normal != Vector3.zero)
            {
                Vector3 along = Vector3.Cross(Vector3.up, e.Normal).normalized;
                float width = WallWidth(e);
                bool garment = e.Name.StartsWith("Garments_");
                float baseDist = garment ? 1.4f : 2.3f + (float)rng.NextDouble() * 0.6f;
                float lateral = count == 1 ? ((float)rng.NextDouble() - 0.5f) * 0.4f * width : (j == 0 ? -1f : 1f) * Mathf.Min(0.3f * width, 1.6f);
                Vector3 wallCenter = new Vector3(e.B.center.x, floorY, e.B.center.z);
                float[] dd = { 0f, 0.4f, 0.8f, -0.3f, 1.3f, 1.8f };
                float[] ll = { 0f, 0.6f, -0.6f, 1.2f, -1.2f, 1.8f, -1.8f };
                foreach (float d in dd)
                {
                    foreach (float l in ll)
                    {
                        Vector3 c = wallCenter + e.Normal * (baseDist + d) + along * (lateral + l);
                        if (SpotOk(c, floorY, second, room, radius, height, lines, zones, placed))
                        {
                            spot = c;
                            return true;
                        }
                    }
                }
            }
            else
            {
                Vector3 center = new Vector3(e.B.center.x, floorY, e.B.center.z);
                Vector3 toRoom = RoomCenterFor(center, second) - center;
                toRoom.y = 0f;
                if (toRoom.sqrMagnitude < 0.01f)
                {
                    toRoom = Vector3.back;
                }

                float baseYaw = Mathf.Atan2(toRoom.x, toRoom.z) * Mathf.Rad2Deg;
                float start = count == 1 ? 0f : (j == 0 ? -35f : 35f);
                float r0 = Mathf.Max(e.B.extents.x, e.B.extents.z) + 1.0f + (float)rng.NextDouble() * 0.3f;
                float[] yawTry = { 0f, 20f, -20f, 45f, -45f, 70f, -70f, 110f, -110f, 150f, -150f, 180f };
                float[] rTry = { 0f, 0.4f, 0.9f, 1.5f, 2.2f };   // 角落里的雕像四周挤，允许站远一点
                foreach (float dr in rTry)
                {
                    foreach (float dy in yawTry)
                    {
                        float yaw = (baseYaw + start + dy) * Mathf.Deg2Rad;
                        Vector3 c = center + new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * (r0 + dr);
                        if (SpotOk(c, floorY, second, room, radius, height, lines, zones, placed))
                        {
                            spot = c;
                            return true;
                        }
                    }
                }
            }

            spot = Vector3.zero;
            return false;
        }

        private static bool SpotOk(Vector3 c, float floorY, bool second, RoomStyle room, float radius, float height,
            List<Segment> lines, List<Zone> zones, List<Vector3> placed)
        {
            // 同一个房间里（别穿墙站到隔壁去）
            RoomStyle here = RoomAt(c.x, c.z, second);
            if (here == null || here.Name != room.Name)
            {
                return false;
            }

            // 天使周围一圈空着
            if (!second && Vector2.Distance(new Vector2(c.x, c.z), new Vector2(SculptureCenter().x, SculptureCenter().z)) < AngelClearRadius)
            {
                return false;
            }

            Vector2 p = new Vector2(c.x, c.z);
            if (!second)
            {
                foreach (Segment s in lines)
                {
                    if (DistanceToSegment(p, s.A, s.B) < s.Clearance + radius)
                    {
                        return false;
                    }
                }
            }

            foreach (Zone z in zones)
            {
                if (z.SecondFloor == second && z.R.Contains(p))
                {
                    return false;
                }
            }

            foreach (Vector3 o in placed)
            {
                if (Mathf.Abs(o.y - c.y) < 1f && Vector2.Distance(p, new Vector2(o.x, o.z)) < 0.75f)
                {
                    return false;
                }
            }

            // 脚下有地，身体不碰任何东西（墙、展柜、长凳、绿植、隔离栏立柱……）
            if (!Physics.Raycast(c + Vector3.up * 0.8f, Vector3.down, out RaycastHit floor, 1.2f, ~0, QueryTriggerInteraction.Ignore)
                || Mathf.Abs(floor.point.y - floorY) > 0.15f)
            {
                return false;
            }

            Vector3 a = c + Vector3.up * (radius + 0.06f);
            Vector3 b = c + Vector3.up * (height - radius);
            return !Physics.CheckCapsule(a, b, radius + 0.05f, ~0, QueryTriggerInteraction.Ignore);
        }

        // ---------------------------------------------------------------- 禁区

        /// <summary>一层：天使巡逻环线（离线 1 m）、三个人台的往返路线（离线 1.3 m）。</summary>
        private static List<Segment> BlockedLines()
        {
            List<Segment> list = new List<Segment>();
            Vector3[] pts = WeepingAngelSetup.PatrolPoints;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 a = pts[i];
                Vector3 b = pts[(i + 1) % pts.Length];
                list.Add(new Segment(a.x, a.z, b.x, b.z, 1.0f));
            }

            list.Add(new Segment(28.7f, 22.8f, 35.9f, 22.8f, 1.3f));
            list.Add(new Segment(37.3f, 23.5f, 37.3f, 5.1f, 1.3f));
            list.Add(new Segment(28.7f, 6.5f, 36.1f, 6.5f, 1.3f));
            return list;
        }

        /// <summary>门前通道（每个门洞两侧 2.5 × 3 m，再放宽 0.4 m）、楼梯上下口、雕塑厅楼梯口拱门、二层修复角。</summary>
        private static List<Zone> BlockedZones(Transform museum)
        {
            List<Zone> list = new List<Zone>();
            foreach (WallPiece w in CollectWallPieces(museum))
            {
                if (!w.IsLintel)
                {
                    continue;
                }

                Bounds b = w.B;
                bool alongX = b.size.x >= b.size.z;
                bool second = b.min.y > Floor2Y - 0.5f;
                const float m = 0.4f;
                if (alongX)
                {
                    list.Add(new Zone(b.center.x - 1.25f - m, b.center.x + 1.25f + m, b.min.z - 3f - m, b.max.z + 3f + m, second));
                }
                else
                {
                    list.Add(new Zone(b.min.x - 3f - m, b.max.x + 3f + m, b.center.z - 1.25f - m, b.center.z + 1.25f + m, second));
                }
            }

            // 雕塑厅 → 楼梯厅的 7 m 拱门两侧
            float half = StairArchWidth * 0.5f;
            list.Add(new Zone(MainDoorX - half, MainDoorX + half, StairHallSouthZ - 2.5f, StairHallSouthZ + 2.5f, false));
            // 一层楼梯脚（第一跑东端）、二层楼梯口（第二跑东端）
            list.Add(new Zone(StairTopX - 0.5f, StairTopX + 2.0f, StairDividerZ - 0.2f, BuildingDepth, false));
            list.Add(new Zone(StairTopX - 0.5f, StairTopX + 2.5f, StairHallSouthZ, StairDividerZ + 0.2f, true));
            // 二层修复角的隔离区
            list.Add(new Zone(22.6f, BuildingWidth, 23.4f, BuildingDepth, true));
            return list;
        }

        // ---------------------------------------------------------------- 服装厅的展品（这些没有射灯，不在 exhibits 列表里）

        private static List<Exhibit> CostumeExhibits()
        {
            List<Exhibit> list = new List<Exhibit>();
            for (int i = 1; i <= 3; i++)
            {
                GameObject c = GameObject.Find("GlassCase_Mannequins_" + i);
                if (c != null)
                {
                    list.Add(new Exhibit { Name = c.name, B = WorldBounds(c), Normal = Vector3.zero });
                }
            }

            GameObject lily = GameObject.Find("LilyDress_Case");
            if (lily != null)
            {
                list.Add(new Exhibit { Name = lily.name, B = WorldBounds(lily), Normal = Vector3.zero });
            }

            // 挂衣：每两件一组，一个人在看
            string[][] pairs =
            {
                new[] { "Garment_N1", "Garment_N2" }, new[] { "Garment_N3", "Garment_N4" },
                new[] { "Garment_S1", "Garment_S2" }, new[] { "Garment_S3", "Garment_S4" },
                new[] { "Garment_E1", "Garment_E2" }, new[] { "Garment_E3", "Garment_E4" },
                new[] { "Garment_E5", "Garment_E6" }, new[] { "Garment_E7", "Garment_E8" },
            };
            foreach (string[] pair in pairs)
            {
                GameObject a = GameObject.Find(pair[0]);
                GameObject b = GameObject.Find(pair[1]);
                if (a == null || b == null)
                {
                    continue;
                }

                Bounds bounds = WorldBounds(a);
                bounds.Encapsulate(WorldBounds(b));
                Vector3 normal = pair[0].Contains("_N") ? Vector3.back : pair[0].Contains("_S") ? Vector3.forward : Vector3.left;
                list.Add(new Exhibit { Name = "Garments_" + pair[0].Substring(8) + pair[1].Substring(9), B = bounds, Normal = normal });
            }

            return list;
        }

        // ---------------------------------------------------------------- 工具

        private static float WallWidth(Exhibit e)
        {
            Vector3 along = Vector3.Cross(Vector3.up, e.Normal);
            return Mathf.Abs(Vector3.Dot(e.B.size, new Vector3(Mathf.Abs(along.x), 0f, Mathf.Abs(along.z))));
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>稳定的字符串哈希（FNV-1a），给每件展品一个固定的随机种子。</summary>
        private static uint Fnv(string s)
        {
            uint h = 2166136261;
            foreach (char ch in s)
            {
                h ^= ch;
                h *= 16777619;
            }

            return h;
        }
    }
}
