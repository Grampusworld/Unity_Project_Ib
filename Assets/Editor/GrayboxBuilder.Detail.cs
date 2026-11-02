using System.Collections.Generic;
using UnityEngine;

namespace Ib.EditorTools
{
    /// <summary>
    /// 建筑细节（GDD R3-1 古典学院风）：在已经生成好的墙体上"贴"一层装修，全部由墙体包围盒推出来，
    /// 改了墙 / 门洞常量后重新生成会自动跟着变。
    ///
    /// 每一面朝室内的墙面，按房间配：
    ///   踢脚线 → 护墙板 → 墙裙压条 → 房间色墙面 → 挂镜线 → 两级檐口，大厅再加壁柱；
    ///   有过梁的门洞两侧加门套；大厅天花加井字梁；雕塑厅加四根立柱和楼梯口拱梁；部分房间地面换石材。
    ///
    /// 避免 z-fighting 的规则：每一层的出墙深度都不同（1.2 cm / 2 / 3.5 / 4.5 / 6 / 16 cm），
    /// 上下相邻的两层只共边、朝向相反；碰到画、展板、楼梯等障碍物时，厚的那几层在障碍物前断开。
    /// </summary>
    public static partial class GrayboxBuilder
    {
        // ---- 墙面分层（相对楼层地面，单位 m）----
        private const float BaseboardTop = 0.22f;
        private const float DadoBottom = 0.86f;
        private const float DadoTop = 0.94f;
        private const float CorniceDrop = 0.30f;      // 檐口高度（从天花往下）
        private const float PictureRailDrop = 0.75f;  // 挂镜线离天花

        private const float SkinDepth = 0.012f;
        private const float WainscotDepth = 0.02f;
        private const float PictureRailDepth = 0.03f;
        private const float BaseboardDepth = 0.035f;
        private const float DadoDepth = 0.045f;
        private const float CasingDepth = 0.05f;
        private const float CorniceDepthLow = 0.06f;
        private const float CorniceDepthHigh = 0.16f;
        private const float PilasterDepth = 0.12f;

        private const float ObstacleReach = 0.6f;     // 离墙面这么近的物体才算"贴墙障碍"
        private const float PilasterSpacing = 4.2f;

        private sealed class RoomStyle
        {
            public string Name;
            public bool Wainscot;
            public bool PictureRail;
            public bool Cornice;
            public bool Pilasters;
            public bool Skin = true;
        }

        private struct WallPiece
        {
            public Bounds B;
            public bool IsLintel;
            public string Name;
        }

        private struct Interval
        {
            public float A;
            public float B;

            public Interval(float a, float b)
            {
                A = a;
                B = b;
            }

            public float Length => B - A;
        }

        private static void BuildArchitectureDetail(Transform museum, Palette p)
        {
            Transform root = NewGroup(museum, "Detail");

            List<WallPiece> walls = CollectWallPieces(museum);
            List<Bounds> obstacles = CollectObstacles(museum);

            Transform faces = NewGroup(root, "WallFinish");
            foreach (WallPiece w in walls)
            {
                DressWallPiece(faces, w, walls, obstacles);
            }

            BuildCeilingBeams(NewGroup(root, "Beams"));
            BuildSculptureColonnade(NewGroup(root, "Colonnade"));
            BuildFloorInlays(NewGroup(root, "FloorInlay"));
            BuildVents(NewGroup(root, "Vents"), obstacles);

            MarkStatic(root);
        }

        // ================================================================ 收集

        private static List<WallPiece> CollectWallPieces(Transform museum)
        {
            List<WallPiece> list = new List<WallPiece>();
            foreach (string group in new[] { "Walls_1F", "Walls_2F" })
            {
                Transform g = museum.Find(group);
                if (g == null)
                {
                    continue;
                }

                foreach (MeshRenderer r in g.GetComponentsInChildren<MeshRenderer>())
                {
                    list.Add(new WallPiece { B = r.bounds, IsLintel = r.name.StartsWith("Lintel_"), Name = r.transform.parent.name + "/" + r.name });
                }
            }

            return list;
        }

        /// <summary>贴墙会挡住线条的东西：道具、展板、名画、雕像、楼梯。</summary>
        private static List<Bounds> CollectObstacles(Transform museum)
        {
            List<Bounds> list = new List<Bounds>();
            List<Transform> roots = new List<Transform>();

            Transform stairs = museum.Find("Stairs");
            if (stairs != null)
            {
                roots.Add(stairs);
            }

            foreach (GameObject go in museum.gameObject.scene.GetRootGameObjects())
            {
                if (go.name == "Props_Museum" || go.name == "Artworks")
                {
                    roots.Add(go.transform);
                }
            }

            foreach (Transform t in roots)
            {
                foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
                {
                    // 斜靠在墙上的梯子很细，护墙板不用为它断开（断了反而露出一块空墙）
                    if (r.transform.parent != null && r.transform.parent.name == "Ladder")
                    {
                        continue;
                    }

                    list.Add(r.bounds);
                }
            }

            return list;
        }

        // ================================================================ 房间判定

        private static readonly Dictionary<string, RoomStyle> Styles = new Dictionary<string, RoomStyle>
        {
            { "Reception", new RoomStyle { Name = "Reception", Wainscot = true, PictureRail = true, Cornice = true, Pilasters = true } },
            { "OilHall", new RoomStyle { Name = "OilHall", Wainscot = true, PictureRail = true, Cornice = true, Pilasters = true } },
            { "Sculpture", new RoomStyle { Name = "Sculpture", Wainscot = false, PictureRail = false, Cornice = true, Pilasters = true } },
            { "StairHall", new RoomStyle { Name = "StairHall", Wainscot = true, PictureRail = false, Cornice = true, Pilasters = false } },
            { "PowerRoom", new RoomStyle { Name = "PowerRoom", Skin = false } },
            { "Costume", new RoomStyle { Name = "Costume", Wainscot = true, PictureRail = true, Cornice = true, Pilasters = false } },
            { "Gallery", new RoomStyle { Name = "Gallery", Wainscot = true, PictureRail = true, Cornice = true, Pilasters = true } },
            { "Family", new RoomStyle { Name = "Family", Wainscot = true, PictureRail = true, Cornice = true, Pilasters = false } },
            { "Duty", new RoomStyle { Name = "Duty", Wainscot = false, PictureRail = false, Cornice = false, Pilasters = false } },
            { "Toilet", new RoomStyle { Name = "Toilet", Wainscot = false, PictureRail = false, Cornice = false, Pilasters = false } },
        };

        /// <summary>点所在的房间；楼外、黑屋、二层未开放区返回 null（不装修）。</summary>
        private static RoomStyle RoomAt(float x, float z, bool secondFloor)
        {
            if (x <= 0f || x >= BuildingWidth || z <= 0f || z >= BuildingDepth)
            {
                return null;
            }

            string name;
            if (!secondFloor)
            {
                if (x < WestLineX)
                {
                    name = "OilHall";
                }
                else if (x > EastLineX)
                {
                    name = "Costume";
                }
                else if (z < ReceptionNorthZ)
                {
                    name = "Reception";
                }
                else if (z > StairHallSouthZ)
                {
                    name = "StairHall";
                }
                else if (x > PowerRoomWestX && x < PowerRoomEastX && z < PowerRoomNorthZ)
                {
                    name = "PowerRoom";
                }
                else
                {
                    name = "Sculpture";
                }
            }
            else
            {
                if (x < WestLineX)
                {
                    name = z < DutyNorthZ && x > DutyWestX ? "Duty" : null;
                }
                else if (x > EastLineX)
                {
                    if (z < ToiletNorthZ && x < ToiletEastX)
                    {
                        name = "Toilet";
                    }
                    else if (z > FamilySouthZ && z < FamilyNorthZ)
                    {
                        name = "Family";
                    }
                    else
                    {
                        name = null;
                    }
                }
                else
                {
                    name = z > StairHallSouthZ ? "StairHall" : "Gallery";
                }
            }

            return name != null ? Styles[name] : null;
        }

        // ================================================================ 墙面

        private static void DressWallPiece(Transform parent, WallPiece w, List<WallPiece> walls, List<Bounds> obstacles)
        {
            Bounds b = w.B;
            bool alongX = b.size.x >= b.size.z;
            bool secondFloor = b.min.y > Floor2Y - 0.5f;
            float floorY = secondFloor ? Floor2Y : Floor1Y;
            float height = secondFloor ? Floor2Height : Floor1Height;

            for (int side = -1; side <= 1; side += 2)
            {
                float plane = alongX ? (side < 0 ? b.min.z : b.max.z) : (side < 0 ? b.min.x : b.max.x);
                float a0 = alongX ? b.min.x : b.min.z;
                float a1 = alongX ? b.max.x : b.max.z;

                // 1. 扣掉从这一面伸出去的其他墙（T 形 / L 形交接处），剩下的每一段只属于一个房间
                List<Interval> runs = new List<Interval> { new Interval(a0, a1) };
                foreach (WallPiece q in walls)
                {
                    if (q.B == b)
                    {
                        continue;
                    }

                    float qMin = alongX ? q.B.min.z : q.B.min.x;
                    float qMax = alongX ? q.B.max.z : q.B.max.x;
                    bool reaches = side > 0 ? (qMax > plane + 0.01f && qMin < plane + 0.05f) : (qMin < plane - 0.01f && qMax > plane - 0.05f);
                    if (!reaches || q.B.max.y <= b.min.y + 0.01f || q.B.min.y >= b.max.y - 0.01f)
                    {
                        continue;
                    }

                    runs = Subtract(runs, alongX ? q.B.min.x : q.B.min.z, alongX ? q.B.max.x : q.B.max.z);
                }

                foreach (Interval run in runs)
                {
                    if (run.Length < 0.05f)
                    {
                        continue;
                    }

                    float mid = (run.A + run.B) * 0.5f;
                    float px = alongX ? mid : plane + side * 0.4f;
                    float pz = alongX ? plane + side * 0.4f : mid;
                    RoomStyle style = RoomAt(px, pz, secondFloor);
                    if (style == null)
                    {
                        continue;
                    }

                    Face face = new Face
                    {
                        AlongX = alongX,
                        Side = side,
                        Plane = plane,
                        Run = run,
                        FloorY = floorY,
                        Height = height,
                        PieceMinY = b.min.y,
                        PieceMaxY = b.max.y,
                        IsLintel = w.IsLintel,
                    };

                    DressFace(parent, face, style, obstacles);
                }
            }
        }

        private struct Face
        {
            public bool AlongX;
            public int Side;
            public float Plane;
            public Interval Run;
            public float FloorY;
            public float Height;
            public float PieceMinY;
            public float PieceMaxY;
            public bool IsLintel;
        }

        private static void DressFace(Transform parent, Face f, RoomStyle style, List<Bounds> obstacles)
        {
            float h = f.Height;
            float ceilingRel = h;

            // 房间色墙面（很薄，画框背面压着它也没关系，所以不避障）
            if (style.Skin)
            {
                Material skin = Finishes.RoomSkins.ContainsKey(style.Name) ? Finishes.RoomSkins[style.Name] : Finishes.PlasterBase;
                float top = style.Cornice ? ceilingRel - CorniceDrop : ceilingRel;
                Band(parent, "Skin", f, BaseboardTop, top, SkinDepth, skin, null, 0f);
            }

            Band(parent, "Baseboard", f, 0f, BaseboardTop, BaseboardDepth, Finishes.Trim, obstacles, 0.03f);

            if (style.Wainscot)
            {
                Band(parent, "Wainscot", f, BaseboardTop, DadoBottom, WainscotDepth, Finishes.Wainscot, obstacles, 0.03f);
                Band(parent, "Dado", f, DadoBottom, DadoTop, DadoDepth, Finishes.Trim, obstacles, 0.03f);
            }

            if (style.PictureRail)
            {
                Band(parent, "PictureRail", f, ceilingRel - PictureRailDrop, ceilingRel - PictureRailDrop + 0.05f,
                    PictureRailDepth, Finishes.Gilt, obstacles, 0.03f);
            }

            if (style.Cornice)
            {
                Band(parent, "Cornice_Low", f, ceilingRel - CorniceDrop, ceilingRel, CorniceDepthLow, Finishes.Cornice, null, 0f);
                Band(parent, "Cornice_High", f, ceilingRel - 0.16f, ceilingRel, CorniceDepthHigh, Finishes.Cornice, null, 0f);
            }

            if (f.IsLintel)
            {
                AddCasing(parent, f, style);
            }
            else if (style.Pilasters)
            {
                AddPilasters(parent, f, style, obstacles);
            }
        }

        /// <summary>
        /// 沿墙面的一条水平装饰带。y0/y1 相对楼层地面，会裁到这块墙的高度范围里；
        /// obstacles 非空时在贴墙障碍物前断开（多留 gap）。
        /// </summary>
        private static void Band(Transform parent, string name, Face f, float y0, float y1, float depth, Material mat,
            List<Bounds> obstacles, float gap)
        {
            float yMin = Mathf.Max(f.FloorY + y0, f.PieceMinY);
            float yMax = Mathf.Min(f.FloorY + y1, f.PieceMaxY);
            if (yMax - yMin < 0.005f)
            {
                return;
            }

            List<Interval> runs = new List<Interval> { f.Run };
            if (obstacles != null)
            {
                runs = SubtractObstacles(runs, f, yMin, yMax, obstacles, gap);
            }

            foreach (Interval r in runs)
            {
                if (r.Length < 0.04f)
                {
                    continue;
                }

                AddFaceBox(parent, name, f, r.A, r.B, yMin, yMax, 0f, depth, mat);
            }
        }

        private static List<Interval> SubtractObstacles(List<Interval> runs, Face f, float yMin, float yMax, List<Bounds> obstacles, float gap)
        {
            foreach (Bounds o in obstacles)
            {
                if (o.max.y <= yMin || o.min.y >= yMax)
                {
                    continue;
                }

                float near = f.AlongX ? (f.Side > 0 ? o.min.z - f.Plane : f.Plane - o.max.z) : (f.Side > 0 ? o.min.x - f.Plane : f.Plane - o.max.x);
                float far = f.AlongX ? (f.Side > 0 ? o.max.z - f.Plane : f.Plane - o.min.z) : (f.Side > 0 ? o.max.x - f.Plane : f.Plane - o.min.x);
                if (far < -0.01f || near > ObstacleReach)
                {
                    continue;
                }

                float a = f.AlongX ? o.min.x : o.min.z;
                float b = f.AlongX ? o.max.x : o.max.z;
                runs = Subtract(runs, a - gap, b + gap);
            }

            return runs;
        }

        /// <summary>贴着墙面、向室内伸出 [d0, d1] 的方块。a/b 沿墙方向，y 为世界高度。</summary>
        private static GameObject AddFaceBox(Transform parent, string name, Face f, float a, float b, float yMin, float yMax,
            float d0, float d1, Material mat)
        {
            float p0 = f.Plane + f.Side * d0;
            float p1 = f.Plane + f.Side * d1;
            float lo = Mathf.Min(p0, p1);
            float hi = Mathf.Max(p0, p1);
            GameObject go = f.AlongX
                ? AddSlab(parent, name, a, b, lo, hi, yMin, yMax, mat)
                : AddSlab(parent, name, lo, hi, a, b, yMin, yMax, mat);
            RemoveCollider(go);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>门套：过梁下面就是门洞，两侧竖框 + 顶框，比门洞每边宽 16 cm。</summary>
        private static void AddCasing(Transform parent, Face f, RoomStyle style)
        {
            const float w = 0.16f;
            float top = f.PieceMinY;
            float a = f.Run.A;
            float b = f.Run.B;

            // 只给真正的门洞（过梁两端都是门边）加：过梁这一面被交接墙切短过的不算
            AddFaceBox(parent, "Casing_L", f, a - w, a + 0.005f, f.FloorY, top, 0f, CasingDepth, Finishes.Trim);
            AddFaceBox(parent, "Casing_R", f, b - 0.005f, b + w, f.FloorY, top, 0f, CasingDepth, Finishes.Trim);

            float headTop = Mathf.Min(top + w, f.FloorY + f.Height - (style.Cornice ? CorniceDrop : 0f));
            if (headTop - top > 0.03f)
            {
                AddFaceBox(parent, "Casing_Head", f, a - w, b + w, top, headTop, 0f, CasingDepth, Finishes.Trim);
            }
        }

        /// <summary>壁柱：只放在没有画 / 展板 / 门的整段墙上，间距约 4.2 m，带柱础和柱头。</summary>
        private static void AddPilasters(Transform parent, Face f, RoomStyle style, List<Bounds> obstacles)
        {
            float top = f.FloorY + f.Height - (style.Cornice ? CorniceDrop : 0f);
            List<Interval> free = SubtractObstacles(new List<Interval> { f.Run }, f, f.FloorY, top, obstacles, 0.15f);

            foreach (Interval r in free)
            {
                int n = Mathf.FloorToInt((r.Length + 0.4f) / PilasterSpacing);
                for (int k = 0; k < n; k++)
                {
                    float c = r.A + r.Length * (k + 0.5f) / n;
                    if (c - 0.45f < r.A || c + 0.45f > r.B)
                    {
                        continue;
                    }

                    AddFaceBox(parent, "Pilaster_Base", f, c - 0.31f, c + 0.31f, f.FloorY, f.FloorY + 0.32f, 0f, 0.18f, Finishes.StoneWorn);
                    AddFaceBox(parent, "Pilaster_Shaft", f, c - 0.25f, c + 0.25f, f.FloorY + 0.32f, top - 0.28f, 0f, PilasterDepth, Finishes.Cornice);
                    AddFaceBox(parent, "Pilaster_Capital", f, c - 0.31f, c + 0.31f, top - 0.28f, top, 0f, 0.17f, Finishes.Cornice);
                }
            }
        }

        // ================================================================ 天花梁

        /// <summary>井字梁：顺 X 的梁比顺 Z 的深 2 cm，交叉处底面不共面。</summary>
        private static void BuildCeilingBeams(Transform parent)
        {
            float i = HalfWall;
            AddBeamGrid(parent, "OilHall", i, WestLineX - i, i, BuildingDepth - i, Floor1Height);
            AddBeamGrid(parent, "Reception", WestLineX + i, EastLineX - i, i, ReceptionNorthZ - i, Floor1Height);
            AddBeamGrid(parent, "Costume", EastLineX + i, BuildingWidth - i, i, BuildingDepth - i, Floor1Height);
            AddBeamGrid(parent, "Gallery", WestLineX + i, EastLineX - i, i, StairHallSouthZ - i, RoofY);
            AddBeamGrid(parent, "StairHall_2F", WestLineX + i, EastLineX - i, StairHallSouthZ + i, BuildingDepth - i, RoofY);
            AddBeamGrid(parent, "Duty", DutyWestX + i, WestLineX - i, i, DutyNorthZ - i, RoofY);

            // 木板天花：两层的大厅都一样（梁之间是木板，不再是灰色抹灰）。全家福厅、厕所、雕塑厅、配电间保持抹灰
            Transform panels = NewGroup(parent, "CeilingPanels");
            CeilingPanel(panels, "OilHall", i, WestLineX - i, i, BuildingDepth - i, Floor1Height);
            CeilingPanel(panels, "Reception", WestLineX + i, EastLineX - i, i, ReceptionNorthZ - i, Floor1Height);
            CeilingPanel(panels, "Costume", EastLineX + i, BuildingWidth - i, i, BuildingDepth - i, Floor1Height);
            CeilingPanel(panels, "Gallery", WestLineX + i, EastLineX - i, i, StairHallSouthZ - i, RoofY);
            CeilingPanel(panels, "StairHall_2F", WestLineX + i, EastLineX - i, StairHallSouthZ + i, BuildingDepth - i, RoofY);
            CeilingPanel(panels, "Duty", DutyWestX + i, WestLineX - i, i, DutyNorthZ - i, RoofY);
        }

        /// <summary>2 cm 厚的木板天花，贴在楼板 / 屋顶底面下面（顶面和楼板底面朝向相反，不会闪）。</summary>
        private static void CeilingPanel(Transform parent, string room, float x0, float x1, float z0, float z1, float ceilingY)
        {
            GameObject go = AddSlab(parent, "Ceiling_" + room, x0, x1, z0, z1, ceilingY - 0.02f, ceilingY, Finishes.CeilingWood);
            RemoveCollider(go);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void AddBeamGrid(Transform parent, string room, float x0, float x1, float z0, float z1, float ceilingY)
        {
            const float cell = 3.25f;
            const float width = 0.28f;
            Transform g = NewGroup(parent, "Beams_" + room);

            int nz = Mathf.Max(1, Mathf.RoundToInt((z1 - z0) / cell));
            for (int k = 1; k < nz; k++)
            {
                float z = z0 + (z1 - z0) * k / nz;
                RemoveCollider(AddSlab(g, "BeamX_" + k, x0, x1, z - width * 0.5f, z + width * 0.5f, ceilingY - 0.42f, ceilingY, Finishes.Beam));
            }

            int nx = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / cell));
            for (int k = 1; k < nx; k++)
            {
                float x = x0 + (x1 - x0) * k / nx;
                RemoveCollider(AddSlab(g, "BeamZ_" + k, x - width * 0.5f, x + width * 0.5f, z0, z1, ceilingY - 0.40f, ceilingY, Finishes.Beam));
            }
        }

        // ================================================================ 雕塑厅立柱

        private static void BuildSculptureColonnade(Transform parent)
        {
            float h = Floor1Height;
            float archZ = StairHallSouthZ;
            float half = StairArchWidth * 0.5f;

            // 楼梯口拱梁：横跨 7 m 通高开口，压在两根立柱上
            AddSlab(parent, "Arch_Beam", MainDoorX - half, MainDoorX + half, archZ - HalfWall - 0.05f, archZ + HalfWall + 0.05f,
                h - 0.7f, h, Finishes.Cornice);

            Vector2[] columns =
            {
                new Vector2(MainDoorX - half + 0.3f, archZ - HalfWall - 0.36f),
                new Vector2(MainDoorX + half - 0.3f, archZ - HalfWall - 0.36f),
                new Vector2(14.1f, 12.9f),
                new Vector2(24.9f, 12.9f),
            };

            for (int i = 0; i < columns.Length; i++)
            {
                AddColumn(parent, "Column_" + i, columns[i].x, columns[i].y, Floor1Y, h);
            }
        }

        private static void AddColumn(Transform parent, string name, float x, float z, float floorY, float height)
        {
            Transform g = NewGroup(parent, name);
            AddSlab(g, "Plinth", x - 0.42f, x + 0.42f, z - 0.42f, z + 0.42f, floorY, floorY + 0.3f, Finishes.StoneWorn);
            AddSlab(g, "Capital", x - 0.42f, x + 0.42f, z - 0.42f, z + 0.42f, floorY + height - 0.32f, floorY + height, Finishes.Cornice);

            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "Shaft";
            shaft.transform.SetParent(g, false);
            float shaftH = height - 0.62f;
            shaft.transform.localPosition = new Vector3(x, floorY + 0.3f + shaftH * 0.5f, z);
            shaft.transform.localScale = new Vector3(0.58f, shaftH * 0.5f, 0.58f);
            shaft.GetComponent<MeshRenderer>().sharedMaterial = Finishes.Marble;
        }

        // ================================================================ 地面拼花

        /// <summary>石材地面：5 mm 厚的面层，铺在房间墙面之间；门槛处露出底下的木地板。</summary>
        private static void BuildFloorInlays(Transform parent)
        {
            const float t = 0.006f;
            float i = HalfWall;
            Material m = Finishes.StoneTile;
            RemoveCollider(AddSlab(parent, "Stone_Reception", WestLineX + i, EastLineX - i, i, ReceptionNorthZ - i, Floor1Y, Floor1Y + t, m));
            RemoveCollider(AddSlab(parent, "Stone_Sculpture", WestLineX + i, EastLineX - i, ReceptionNorthZ + i, StairHallSouthZ - i, Floor1Y, Floor1Y + t, m));
            RemoveCollider(AddSlab(parent, "Stone_StairHall", WestLineX + i, EastLineX - i, StairHallSouthZ + i, BuildingDepth - i, Floor1Y, Floor1Y + t, m));
            RemoveCollider(AddSlab(parent, "Stone_Toilet", EastLineX + i, ToiletEastX - i, i, ToiletNorthZ - i, Floor2Y, Floor2Y + t, m));
        }

        // ================================================================ 通风口

        /// <summary>高处的铸铁通风格栅：大厅每面长墙一个，避开画和展板。</summary>
        private static void BuildVents(Transform parent, List<Bounds> obstacles)
        {
            float i = HalfWall;
            // (房间, 面上一点, 法线, 楼层地面, 层高)
            AddVent(parent, "Vent_OilHall_N", new Vector3(2.5f, 0f, BuildingDepth - i), Vector3.back, Floor1Y, Floor1Height, obstacles);
            AddVent(parent, "Vent_Costume_S", new Vector3(33.0f, 0f, i), Vector3.forward, Floor1Y, Floor1Height, obstacles);
            AddVent(parent, "Vent_Reception_E", new Vector3(EastLineX - i, 0f, 1.6f), Vector3.left, Floor1Y, Floor1Height, obstacles);
            AddVent(parent, "Vent_Gallery_W", new Vector3(WestLineX + i, 0f, 1.4f), Vector3.right, Floor2Y, Floor2Height, obstacles);
            AddVent(parent, "Vent_StairHall2F_N", new Vector3(17.0f, 0f, BuildingDepth - i), Vector3.back, Floor2Y, Floor2Height, obstacles);
        }

        private static void AddVent(Transform parent, string name, Vector3 facePoint, Vector3 normal, float floorY, float height,
            List<Bounds> obstacles)
        {
            float y = floorY + height - CorniceDrop - 0.55f;
            Vector3 c = new Vector3(facePoint.x, y, facePoint.z) + normal * 0.02f;
            bool alongX = Mathf.Abs(normal.z) > 0.5f;
            Vector3 size = alongX ? new Vector3(0.7f, 0.35f, 0.04f) : new Vector3(0.04f, 0.35f, 0.7f);
            Bounds vb = new Bounds(c, size + Vector3.one * 0.3f);
            foreach (Bounds o in obstacles)
            {
                if (o.Intersects(vb))
                {
                    return;
                }
            }

            Transform g = NewGroup(parent, name);
            RemoveCollider(AddBox(g, "Frame", c, size, Finishes.MetalDark));
            for (int k = 0; k < 5; k++)
            {
                Vector3 slat = c + normal * 0.025f + Vector3.up * (-0.12f + k * 0.06f);
                Vector3 s = alongX ? new Vector3(0.62f, 0.015f, 0.02f) : new Vector3(0.02f, 0.015f, 0.62f);
                RemoveCollider(AddBox(g, "Slat_" + k, slat, s, Finishes.Brass));
            }
        }

        // ================================================================ 区间工具

        private static List<Interval> Subtract(List<Interval> runs, float a, float b)
        {
            List<Interval> result = new List<Interval>();
            foreach (Interval r in runs)
            {
                if (b <= r.A || a >= r.B)
                {
                    result.Add(r);
                    continue;
                }

                if (a > r.A)
                {
                    result.Add(new Interval(r.A, a));
                }

                if (b < r.B)
                {
                    result.Add(new Interval(b, r.B));
                }
            }

            return result;
        }
    }
}
