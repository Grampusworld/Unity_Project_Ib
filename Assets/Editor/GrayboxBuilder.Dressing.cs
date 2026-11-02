using System.Collections.Generic;
using Ib.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ib.EditorTools
{
    /// <summary>
    /// 布置：名画 / 雕像（原来是在场景里手摆的，2026-10-27 按当时场景里的位置写成表，重新生成也不会丢）、
    /// Assets/Art/Models/Props 的道具模型、博物馆杂项（隔离栏、灭火器、出口灯、监控、清洁车、木箱、梯子、说明牌）、
    /// 每件展品一盏射灯、吊灯罩、浮尘。
    ///
    /// 剧情约束（GDD §5）：全家福厅只有画架和《美术馆》两件，不加东西；天使周围 4 m 不放东西；
    /// 门洞、楼梯、人偶路线、长廊 5 个展柜之间的掩体通道、黑屋都不放东西；未开放区保持关闭。
    /// 东西尽量贴墙、放角落，房间中间留空（上一轮要求"更空旷"）。
    /// </summary>
    public static partial class GrayboxBuilder
    {
        private const string PropModelDir = "Assets/Art/Models/Props/";

        private struct Exhibit
        {
            public string Name;
            public Bounds B;
            public Vector3 Normal;    // 挂墙展品朝向室内的方向；独立展品为 0
            public bool Shadows;
        }

        private static readonly List<Vector3> ExitSignLights = new List<Vector3>();

        // ================================================================ 名画与雕像

        private struct ArtSpot
        {
            public string Name;
            public string Asset;
            public Vector3 Position;
            public float Yaw;
            public float Scale;
            public int LabelSide;     // 说明牌在画的右边 +1 / 左边 −1 / 不挂 0（面对画时的左右）

            public ArtSpot(string name, string asset, float x, float y, float z, float yaw, float scale, int labelSide)
            {
                Name = name;
                Asset = asset;
                Position = new Vector3(x, y, z);
                Yaw = yaw;
                Scale = scale;
                LabelSide = labelSide;
            }
        }

        private static readonly ArtSpot[] Artworks =
        {
            // 一层油画厅 · 西墙
            new ArtSpot("Painting_GirlWithPearlEarring", "Paintings/Painting_GirlWithPearlEarring", 0.150f, 2.860f, 2.960f, 90f, 5.000f, 1),
            new ArtSpot("Painting_LadyWithYellowRoses (1)", "Paintings/Painting_LadyWithYellowRoses", 0.220f, 2.897f, 8.829f, 90f, 3.212f, 1),
            new ArtSpot("Painting_LastSupper", "Paintings/Painting_LastSupper", 0.150f, 2.680f, 18.580f, 90f, 3.000f, 1),
            // 一层油画厅 · 东墙、北墙
            new ArtSpot("Painting_Springtime", "Paintings/Painting_Springtime", 12.780f, 2.891f, 9.809f, 270f, 2.110f, 1),
            new ArtSpot("Painting_Sunflowers", "Paintings/Painting_Sunflowers", 12.780f, 3.110f, 21.060f, 270f, 3.287f, 1),
            new ArtSpot("Painting_WheatfieldWithCrows", "Paintings/Painting_WheatfieldWithCrows", 7.032f, 3.050f, 25.870f, 180f, 2.725f, 1),
            // 一层楼梯厅东墙（占满整面墙，不挂说明牌）
            new ArtSpot("Painting_WheatfieldWithCrows (1)", "Paintings/Painting_WheatfieldWithCrows", 25.850f, 2.600f, 23.360f, 270f, 3.019f, 0),
            // 二层名画长廊东墙
            new ArtSpot("Painting_LadyWithYellowRoses", "Paintings/Painting_LadyWithYellowRoses", 25.780f, 9.053f, 16.481f, 270f, 3.212f, -1),   // 右边是全家福厅的门
            new ArtSpot("Painting_MonaLisa", "Paintings/Painting_MonaLisa", 25.780f, 9.000f, 11.548f, 270f, 1.982f, 1),
            // 雕塑厅北侧两角的雕像（放在放大的底座上）
            new ArtSpot("MonaLisaStatue", "Statues/MonaLisaStatue", 15.006f, 0.758f, 18.828f, 120.8f, 0.800f, 0),
            new ArtSpot("StatueOfLiberty", "Statues/StatueOfLiberty", 24.127f, 0.738f, 19.266f, 0f, 1.600f, 0),
            // 雕塑厅东南角：复活节岛摩艾石像（Blender 程序化建模 Statues/build_moai.py），站在原《跪者》的底座上，面朝大厅中央的天使
            new ArtSpot("Moai", "Statues/Moai", 24.2f, 0.6f, 10.8f, -48.2f, 1.9f, 0),
        };

        private static List<Exhibit> PlaceArtworks()
        {
            List<Exhibit> exhibits = new List<Exhibit>();
            Transform root = new GameObject("Artworks").transform;

            foreach (ArtSpot a in Artworks)
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/" + a.Asset + ".fbx");
                if (asset == null)
                {
                    Debug.LogWarning("[GrayboxBuilder] 找不到模型 " + a.Asset);
                    continue;
                }

                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(asset, root);
                go.name = a.Name;
                go.transform.position = a.Position;
                go.transform.rotation = Quaternion.Euler(0f, a.Yaw, 0f);
                go.transform.localScale = Vector3.one * a.Scale;

                bool isStatue = a.Asset.StartsWith("Statues/");
                Bounds b = WorldBounds(go);
                exhibits.Add(new Exhibit { Name = a.Name, B = b, Normal = isStatue ? Vector3.zero : go.transform.forward, Shadows = isStatue });

                if (isStatue)
                {
                    AddLocalBoxCollider(go);
                }
                else if (a.LabelSide != 0)
                {
                    AddWallLabel(root, a.Name, go.transform, b, a.LabelSide);
                }
            }

            return exhibits;
        }

        /// <summary>说明牌：画框外侧 30 cm、离地 1.3 m（7 岁孩子抬头就能看到）。</summary>
        private static void AddWallLabel(Transform parent, string name, Transform painting, Bounds b, int side)
        {
            Vector3 n = painting.forward;
            Vector3 right = Vector3.Cross(Vector3.up, -n) * side;
            float half = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(right.x), 0f, Mathf.Abs(right.z))));
            float floorY = painting.position.y > Floor2Y ? Floor2Y : Floor1Y;
            Vector3 c = painting.position + right * (half + 0.3f) + n * 0.012f;
            c.y = floorY + 1.3f;
            Vector3 size = Mathf.Abs(n.x) > 0.5f ? new Vector3(0.015f, 0.22f, 0.32f) : new Vector3(0.32f, 0.22f, 0.015f);
            RemoveCollider(AddBox(parent, "Label_" + name, c, size, Finishes.Label));
        }

        /// <summary>手工调整后藏到真画后面的占位板不算展品；其余挂墙占位板和角落占位雕塑各配一盏射灯。</summary>
        private static List<Exhibit> CollectPanelExhibits(Transform props)
        {
            var wanted = new Dictionary<string, Vector3>
            {
                { "Intro_Wall_TheAudience", Vector3.back },
                { "Grampus_IDPhoto", Vector3.back },
                { "Masterpiece_W1_EmptyFrameA", Vector3.right },
                { "Masterpiece_W2", Vector3.right },
                { "Masterpiece_W3", Vector3.right },
                { "Masterpiece_E1", Vector3.left },
                { "Masterpiece_E2", Vector3.left },
                { "Masterpiece_E3_EmptyFrameB", Vector3.left },
                { "Sketch_LilyLooksAtDoor", Vector3.right },
                { "Painting_TheMuseum_ExitFrame", Vector3.back },
                { "Canvas", Vector3.left },     // 全家福画架
                { "Body", Vector3.zero },       // 角落占位雕塑
            };

            List<Bounds> real = new List<Bounds>();
            GameObject art = GameObject.Find("Artworks");
            if (art != null)
            {
                foreach (Renderer r in art.GetComponentsInChildren<Renderer>())
                {
                    real.Add(r.bounds);
                }
            }

            List<Exhibit> list = new List<Exhibit>();
            foreach (MeshRenderer r in props.GetComponentsInChildren<MeshRenderer>())   // 返回的是数组，循环里删物体没问题
            {
                if (!wanted.TryGetValue(r.name, out Vector3 normal))
                {
                    continue;
                }

                if (r.name == "Canvas" && r.transform.parent.name != "FamilyPortrait_Easel")
                {
                    continue;
                }

                // 角落雕像：Body 在 Sculpture_xxx/Statue 下面；清洁车、灭火器、监控也叫 Body，跳过
                Transform owner = r.transform.parent;
                if (r.name == "Body")
                {
                    if (owner.name == "Statue" && owner.parent != null)
                    {
                        owner = owner.parent;
                    }

                    if (!owner.name.StartsWith("Sculpture_"))
                    {
                        continue;
                    }
                }

                bool hidden = false;
                Bounds probe = r.bounds;
                probe.Expand(0.05f);   // 占位板贴在画背后，只是"碰到"，放宽一点
                foreach (Bounds rb in real)
                {
                    if (rb.Intersects(probe))
                    {
                        hidden = true;
                        break;
                    }
                }

                if (!hidden)
                {
                    Bounds eb = r.bounds;
                    if (r.name == "Body")
                    {
                        eb = WorldBounds(owner.gameObject);   // 整座雕像连底座
                    }

                    list.Add(new Exhibit { Name = owner.name + "_" + r.name, B = eb, Normal = normal });
                }
                else
                {
                    // 被真画盖住的占位板会从画框边上露一截（《蒙娜丽莎》下面那块），直接去掉
                    Object.DestroyImmediate(r.gameObject);
                }
            }

            return list;
        }

        // ================================================================ 道具模型 + 博物馆杂项

        private static List<Exhibit> PlaceDressing(Transform props, Palette p)
        {
            List<Exhibit> exhibits = new List<Exhibit>();
            ExitSignLights.Clear();
            Transform root = NewGroup(props, "Dressing");
            float y1 = Floor1Y;
            float y2 = Floor2Y;

            // ---- 接待厅：两侧墙角各一组休息座（门两边），北墙两角各一个展台 ----
            Transform rec = NewGroup(root, "Reception");
            Model(rec, "20_sofa", 14.75f, y1, 0.78f, 180f, 1f, true);
            Model(rec, "11_round_side_table", 15.75f, y1, 0.55f, 0f, 1f, true);
            Model(rec, "21_terracotta_jug", 15.75f, y1 + 0.62f, 0.55f, 30f, 1f, false);
            Model(rec, "10_armchair", 16.6f, y1, 0.92f, 200f, 1f, true);
            Model(rec, "10_armchair", 23.4f, y1, 0.92f, 160f, 1f, true);
            Model(rec, "11_round_side_table", 24.3f, y1, 0.55f, 0f, 1f, true);
            Model(rec, "12_brush_jar", 24.3f, y1 + 0.62f, 0.55f, 0f, 1f, false);
            exhibits.Add(Pedestal(rec, "Pedestal_Vase", 13.75f, y1, 8.25f, "02_blue_white_vase", 1.3f));   // 用户 10-28 手调：梅瓶放大 1.3 倍
            exhibits.Add(Pedestal(rec, "Pedestal_GlassSculpture", 25.25f, y1, 8.25f, "05_cast_glass_sculpture"));

            // ---- 油画厅：三张长凳对着大画，大画前拉隔离绳 ----
            Transform oil = NewGroup(root, "OilHall");
            // 用户 10-28 手调：长凳挪到南墙下（门两边各一张，放大 1.1 倍），中间第三张删掉
            Model(oil, "09_museum_bench", 4.34f, y1, 0.58f, -2f, 1.1f, true);
            Model(oil, "09_museum_bench", 8.545f, y1, 0.549f, 181.9f, 1.1f, true);
            DressOilHall(oil);   // 水晶吊灯、红地毯、绿植、语音导览架、导言牌（GrayboxBuilder.OilHall.cs）
            Stanchions(oil, "Rope_GirlWithPearl", y1, new Vector3(1.25f, 0f, 1.0f), new Vector3(1.25f, 0f, 4.9f), 3);
            Stanchions(oil, "Rope_Lady", y1, new Vector3(1.25f, 0f, 7.0f), new Vector3(1.25f, 0f, 10.65f), 3);
            Stanchions(oil, "Rope_LastSupper", y1, new Vector3(1.25f, 0f, 13.2f), new Vector3(1.25f, 0f, 24.0f), 5);

            // ---- 服装厅：西墙三个首饰小展柜（矮，7 岁孩子能看见里面）----
            Transform costume = NewGroup(root, "CostumeHall");
            exhibits.Add(Vitrine(costume, "Vitrine_Brooch", 26.6f, y1, 8.0f, "06_gold_cameo_brooch", 25f));
            exhibits.Add(Vitrine(costume, "Vitrine_Bangle", 26.6f, y1, 10.6f, "07_gold_onyx_bangle", 0f));
            exhibits.Add(Vitrine(costume, "Vitrine_Pendant", 26.6f, y1, 20.0f, "08_gold_coral_pendant", -15f));

            // ---- 楼梯底下：保洁推车 + 小心地滑（开幕夜还没收走）----
            Transform stair = NewGroup(root, "StairHall_1F");
            CleaningCart(stair, 16.4f, y1, 22.2f);
            WetFloorSign(stair, 17.7f, y1, 21.9f, 25f);

            // ---- 二层长廊：南端一张长凳；名画前拉隔离绳；锁着的门旁边堆着木箱和梯子 ----
            Transform gallery = NewGroup(root, "Gallery");
            Model(gallery, "09_museum_bench", 19.5f, y2, 3.2f, 0f, 1f, true);
            // 隔离绳只拉在画正前方、贴墙平行，不横过门前（全家福厅门 z 12.5–14.5）；《黄玫瑰夫人》前面已经有展柜，不再拉绳
            Stanchions(gallery, "Rope_MonaLisa", y2, new Vector3(24.85f, 0f, 10.3f), new Vector3(24.85f, 0f, 12.2f), 2);
            Crate(gallery, "Crate_A", 13.85f, y2, 20.2f, 0.8f, 0f);
            Crate(gallery, "Crate_B", 14.75f, y2, 20.35f, 0.6f, 12f);
            Crate(gallery, "Crate_C", 13.9f, y2 + 0.8f, 20.2f, 0.55f, -8f);
            Ladder(gallery, "Ladder", 16.6f, y2, StairHallSouthZ - HalfWall, 3.0f);

            // ---- 二层楼梯口东北角：没收拾完的修复角（画架、颜料、松节油、防尘布），用隔离绳围着 ----
            Transform studio = NewGroup(root, "StairHall_2F_Restoration");
            // 楼梯加长后井口栏杆到 x 22.1，修复角整体让到 22.8 以东
            RemoveCollider(AddSlab(studio, "DropCloth", 22.8f, 25.75f, 23.7f, 25.75f, y2, y2 + 0.008f, Finishes.Canvas));
            GameObject easel = Model(studio, "01_artist_easel", 24.2f, y2, 25.0f, 200f, 1f, true);
            Model(studio, "18_folding_stool", 23.5f, y2, 24.5f, 20f, 1f, true);
            Model(studio, "13_palette", 23.5f, y2 + 0.55f, 24.5f, 40f, 1f, false);
            Model(studio, "19_paint_box", 25.2f, y2, 24.1f, 15f, 1f, false);
            Model(studio, "16_brushes", 25.2f, y2 + 0.145f, 24.1f, 70f, 1f, false);
            Model(studio, "14_paint_tubes", 24.8f, y2, 23.85f, 110f, 1f, false);
            Model(studio, "15_turpentine_can", 25.5f, y2, 25.4f, 0f, 1f, false);
            Model(studio, "12_brush_jar", 24.9f, y2, 25.5f, 0f, 1f, false);
            Model(studio, "17_rolled_canvas", 23.3f, y2, 25.5f, 0f, 1f, false);
            Stanchions(studio, "Rope_Restoration", y2, new Vector3(22.7f, 0f, 23.45f), new Vector3(25.7f, 0f, 23.45f), 3);
            exhibits.Add(new Exhibit { Name = "Restoration_Easel", B = WorldBounds(easel), Normal = Vector3.zero });

            // ---- 值班室：桌前一把扶手椅 ----
            Model(NewGroup(root, "DutyRoom"), "10_armchair", 11.7f, y2, 1.45f, 180f, 1f, true);

            // ---- 灭火器 ----
            Transform safety = NewGroup(root, "Safety");
            FireExtinguisher(safety, "Extinguisher_Reception_W", new Vector3(WestLineX + HalfWall, y1, 2.6f), Vector3.right);
            FireExtinguisher(safety, "Extinguisher_Reception_E", new Vector3(EastLineX - HalfWall, y1, 6.6f), Vector3.left);
            FireExtinguisher(safety, "Extinguisher_Costume_W", new Vector3(EastLineX + HalfWall, y1, 12.5f), Vector3.right);
            FireExtinguisher(safety, "Extinguisher_Gallery_W", new Vector3(WestLineX + HalfWall, y2, 5.8f), Vector3.right);

            // ---- 出口指示灯（绿）：正门里侧、二层厕所门 ----
            // 接待厅两侧门原来的绿色出口牌已删，改成厅名门牌，见 GrayboxBuilder.Wayfinding.cs
            ExitSign(safety, "Exit_MainDoor", new Vector3(MainDoorX, y1 + MainDoorHeight + 0.45f, HalfWall), Vector3.forward);
            ExitSign(safety, "Exit_Gallery_Toilet", new Vector3(EastLineX - HalfWall, y2 + SmallDoorHeight + 0.45f, ToiletDoorZ), Vector3.left);

            // ---- 监控摄像头（带红色指示灯），对着各厅中间 ----
            Transform cams = NewGroup(root, "SecurityCameras");
            SecurityCamera(cams, "Cam_Reception", new Vector3(25.55f, y1 + 5.0f, 8.55f), new Vector3(19.5f, y1 + 1f, 1.5f));
            SecurityCamera(cams, "Cam_OilHall", new Vector3(0.45f, y1 + 5.0f, 25.45f), new Vector3(6.5f, y1 + 1f, 13f));
            SecurityCamera(cams, "Cam_Costume", new Vector3(39.55f, y1 + 5.0f, 0.45f), new Vector3(33f, y1 + 1f, 13f));
            SecurityCamera(cams, "Cam_Gallery", new Vector3(13.45f, y2 + 4.0f, 0.45f), new Vector3(19.5f, y2 + 1f, 10f));

            return exhibits;
        }

        // ================================================================ 单件

        /// <summary>放一个道具模型：底部贴到 y，按 yaw 转（模型正面朝 −Z），可选按包围盒加 BoxCollider。</summary>
        private static GameObject Model(Transform parent, string file, float x, float y, float z, float yaw, float scale, bool collider)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PropModelDir + file + ".fbx");
            if (asset == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 找不到道具模型 " + file);
                return new GameObject(file + "_Missing");
            }

            // 保留 FBX 根节点自带的旋转和缩放（有的模型根上是 −90° + ×100 的 Blender 坐标换算）
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.position = new Vector3(x, y, z);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * asset.transform.localRotation;
            go.transform.localScale = asset.transform.localScale * scale;

            Bounds b = WorldBounds(go);
            go.transform.position += Vector3.up * (y - b.min.y);

            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = ShadowCastingMode.On;
            }

            if (collider)
            {
                AddLocalBoxCollider(go);
            }

            return go;
        }

        /// <summary>石材展台 + 顶上一件小展品。</summary>
        private static Exhibit Pedestal(Transform parent, string name, float x, float y, float z, string model, float modelScale = 1f)
        {
            Transform g = NewGroup(parent, name);
            AddProp(g, "Base", x, y, z, 0.6f, 0.1f, 0.6f, Finishes.StoneWorn);
            AddProp(g, "Column", x, y + 0.1f, z, 0.46f, 0.8f, 0.46f, Finishes.StoneWorn);
            AddProp(g, "Top", x, y + 0.9f, z, 0.56f, 0.1f, 0.56f, Finishes.StoneWorn);
            GameObject item = Model(g, model, x, y + 1.0f, z, 0f, modelScale, false);
            Bounds b = WorldBounds(item);
            return new Exhibit { Name = name, B = b, Normal = Vector3.zero };
        }

        /// <summary>首饰柜：木柜身 + 丝绒垫 + 玻璃罩（玻璃罩没有碰撞，不挡视线判定）。</summary>
        private static Exhibit Vitrine(Transform parent, string name, float x, float y, float z, string jewel, float yaw)
        {
            Transform g = NewGroup(parent, name);
            AddProp(g, "Cabinet", x, y, z, 0.55f, 0.85f, 0.55f, Finishes.Wainscot);
            RemoveCollider(AddProp(g, "Velvet", x, y + 0.85f, z, 0.45f, 0.015f, 0.45f, Finishes.Velvet));
            RemoveCollider(AddProp(g, "GlassCover", x, y + 0.85f, z, 0.55f, 0.32f, 0.55f, Finishes.GlassClear));
            GameObject item = Model(g, jewel, x, y + 0.865f, z, yaw, 1.6f, false);
            return new Exhibit { Name = name, B = WorldBounds(item), Normal = Vector3.zero };
        }

        /// <summary>黄铜立柱 + 红丝绒绳（立柱有碰撞，绳子没有）。</summary>
        private static void Stanchions(Transform parent, string name, float floorY, Vector3 from, Vector3 to, int posts)
        {
            Transform g = NewGroup(parent, name);
            Vector3 prev = Vector3.zero;
            for (int i = 0; i < posts; i++)
            {
                Vector3 c = Vector3.Lerp(from, to, posts == 1 ? 0f : i / (float)(posts - 1));
                c.y = floorY;
                Cylinder(g, "Base_" + i, c + Vector3.up * 0.015f, 0.28f, 0.03f, Finishes.Brass, false);
                Cylinder(g, "Pole_" + i, c + Vector3.up * 0.48f, 0.05f, 0.9f, Finishes.Brass, true);
                GameObject knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                knob.name = "Knob_" + i;
                knob.transform.SetParent(g, false);
                knob.transform.position = c + Vector3.up * 0.95f;
                knob.transform.localScale = Vector3.one * 0.085f;
                knob.GetComponent<MeshRenderer>().sharedMaterial = Finishes.Brass;
                RemoveCollider(knob);

                if (i > 0)
                {
                    Vector3 a = prev + Vector3.up * 0.86f;
                    Vector3 b = c + Vector3.up * 0.86f;
                    GameObject rope = AddBox(g, "Rope_" + i, (a + b) * 0.5f - Vector3.up * 0.06f,
                        new Vector3(0.035f, 0.035f, Vector3.Distance(a, b)), Finishes.Velvet);
                    rope.transform.rotation = Quaternion.LookRotation(b - a);
                    RemoveCollider(rope);
                }

                prev = c;
            }
        }

        private static GameObject Cylinder(Transform parent, string name, Vector3 center, float diameter, float height, Material mat, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!collider)
            {
                RemoveCollider(go);
            }

            return go;
        }

        private static void ExitSign(Transform parent, string name, Vector3 wallPoint, Vector3 normal)
        {
            Transform g = NewGroup(parent, name);
            RemoveCollider(AddBox(g, "Housing", wallPoint + normal * 0.045f, Abs(normal) * 0.07f + Perp(normal) * 0.52f + Vector3.up * 0.2f, Finishes.MetalDark));
            RemoveCollider(AddBox(g, "Panel", wallPoint + normal * 0.083f, Abs(normal) * 0.006f + Perp(normal) * 0.46f + Vector3.up * 0.15f, Finishes.ExitSign));
            ExitSignLights.Add(wallPoint + normal * 0.3f);
        }

        private static void SecurityCamera(Transform parent, string name, Vector3 mount, Vector3 target)
        {
            Transform g = NewGroup(parent, name, mount, 0f);
            Vector3 dir = target - mount;
            dir.y = 0f;
            g.rotation = Quaternion.LookRotation(dir.normalized);
            GameObject body = AddBox(g, "Body", new Vector3(0f, -0.12f, 0.12f), new Vector3(0.16f, 0.14f, 0.34f), Finishes.MetalDark);
            body.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
            AddBox(g, "Arm", new Vector3(0f, -0.04f, 0f), new Vector3(0.04f, 0.1f, 0.04f), Finishes.MetalDark);
            GameObject led = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            led.name = "Led";
            led.transform.SetParent(body.transform, false);
            led.transform.localPosition = new Vector3(0.3f, 0.3f, 0.52f);
            led.transform.localScale = new Vector3(0.12f, 0.14f, 0.06f);
            led.GetComponent<MeshRenderer>().sharedMaterial = Finishes.RedLed;
            foreach (Collider c in g.GetComponentsInChildren<Collider>())
            {
                Object.DestroyImmediate(c);
            }
        }

        private static void Crate(Transform parent, string name, float x, float y, float z, float size, float yaw)
        {
            Transform g = NewGroup(parent, name, new Vector3(x, y, z), yaw);
            AddBox(g, "Box", new Vector3(0f, size * 0.5f, 0f), Vector3.one * size, Finishes.CrateWood);
            // 两道钢带
            for (int i = 0; i < 2; i++)
            {
                float off = (i == 0 ? -0.3f : 0.3f) * size;
                RemoveCollider(AddBox(g, "Strap_" + i, new Vector3(off, size * 0.5f, 0f), new Vector3(0.04f, size + 0.01f, size + 0.01f), Finishes.MetalDark));
            }
        }

        /// <summary>
        /// 靠墙的直梯：梯脚着地、梯子顶端正好抵在墙面上（墙面在梯脚的 +Z 方向，wallZ = 墙面 z）。
        /// 绕 X 轴转 +lean 时梯子顶端朝 +Z 倒；梯框厚 7 cm，所以梯脚再多退半个厚度。
        /// </summary>
        private static void Ladder(Transform parent, string name, float x, float y, float wallZ, float height)
        {
            const float footOut = 0.8f;
            const float halfDepth = 0.035f;
            float lean = Mathf.Asin(footOut / height) * Mathf.Rad2Deg;
            Transform g = NewGroup(parent, name, new Vector3(x, y, wallZ - footOut - halfDepth), 0f);
            g.rotation = Quaternion.Euler(lean, 0f, 0f);
            RemoveCollider(AddBox(g, "Rail_L", new Vector3(-0.22f, height * 0.5f, 0f), new Vector3(0.05f, height, 0.07f), Finishes.MetalDark));
            RemoveCollider(AddBox(g, "Rail_R", new Vector3(0.22f, height * 0.5f, 0f), new Vector3(0.05f, height, 0.07f), Finishes.MetalDark));
            for (int i = 1; i < 10; i++)
            {
                RemoveCollider(AddBox(g, "Rung_" + i, new Vector3(0f, i * height / 10f, 0f), new Vector3(0.44f, 0.03f, 0.05f), Finishes.MetalDark));
            }
        }

        // ================================================================ 灯

        /// <summary>
        /// 每件展品一盏暖色射灯（挂墙的从前上方斜打，独立的从头顶偏前方打），亮度按距离平方给，
        /// 让画面照度大致一样，明显亮过底光 —— 光一圈一圈落在画上，画与画之间是暗的。
        /// 太宽的画（《最后的晚餐》11 m）拆成几盏并排。
        /// </summary>
        private static void AddExhibitLights(Transform parent, List<Exhibit> exhibits)
        {
            Transform g = NewGroup(parent, "Exhibit_Spots");
            Color warm = new Color(1f, 0.86f, 0.66f);

            foreach (Exhibit e in exhibits)
            {
                bool second = e.B.center.y > Floor2Y;
                float lightY = (second ? Floor2Y + Floor2Height : Floor1Y + Floor1Height) - 0.55f;

                float ceilingY = second ? RoofY : Floor1Height;
                float floorY = second ? Floor2Y : Floor1Y;

                // 小展柜 / 展台：吊杆射灯正好挂在头顶上方，垂直往下打（灯具看得见，光不是凭空来的）
                if (e.Name.StartsWith("Vitrine_") || e.Name.StartsWith("Pedestal_"))
                {
                    Vector3 above = new Vector3(e.B.center.x, floorY + 2.9f, e.B.center.z);
                    AimSpot(g, "Spot_" + e.Name, above, e.B.center, e.B.extents.magnitude + 0.25f, warm, e.Shadows, ceilingY, 2.5f);
                    continue;
                }

                if (e.Normal == Vector3.zero)
                {
                    Vector3 toRoom = RoomCenterFor(e.B.center, second) - e.B.center;
                    toRoom.y = 0f;
                    Vector3 pos = new Vector3(e.B.center.x, lightY, e.B.center.z) + toRoom.normalized * 0.9f;
                    AimSpot(g, "Spot_" + e.Name, pos, e.B.center, e.B.extents.magnitude + 0.2f, warm, e.Shadows, ceilingY);
                    continue;
                }

                Vector3 along = Vector3.Cross(Vector3.up, e.Normal);
                float width = Mathf.Abs(Vector3.Dot(e.B.size, new Vector3(Mathf.Abs(along.x), 0f, Mathf.Abs(along.z))));
                int pieces = Mathf.Max(1, Mathf.CeilToInt(width / 3.5f));
                float pieceW = width / pieces;
                for (int k = 0; k < pieces; k++)
                {
                    Vector3 target = e.B.center + along * ((k + 0.5f) * pieceW - width * 0.5f);
                    float out1 = Mathf.Clamp(0.45f * (lightY - target.y), 1.0f, 2.2f);
                    Vector3 pos = target + e.Normal * out1;
                    pos.y = lightY;
                    float radius = 0.5f * Mathf.Max(pieceW, e.B.size.y) + 0.15f;
                    AimSpot(g, "Spot_" + e.Name + (pieces > 1 ? "_" + k : ""), pos, target, radius, warm, e.Shadows, ceilingY);
                }
            }
        }

        private static void AimSpot(Transform parent, string name, Vector3 pos, Vector3 target, float radius, Color color, bool shadows, float ceilingY,
            float boost = 1f)
        {
            float dist = Vector3.Distance(pos, target);
            float angle = Mathf.Clamp(2f * Mathf.Atan(radius / dist) * Mathf.Rad2Deg * 1.1f, 20f, 120f);
            float intensity = Mathf.Clamp(2.2f * dist * dist * boost, 4f, 90f);
            AddSpotLight(parent, name, pos, target, color, intensity, dist * 1.8f, angle, shadows);
            AddSpotFixture(lampFixtures, name, pos, target, ceilingY);
        }

        /// <summary>
        /// 射灯灯具：一根吊杆从天花垂下来 + 一个朝着目标的灯筒 + 筒口一片暖色发光灯片。灯（Light）就在灯片的位置。
        /// </summary>
        private static void AddSpotFixture(Transform parent, string name, Vector3 pos, Vector3 target, float ceilingY)
        {
            if (parent == null)
            {
                return;
            }

            Transform g = NewGroup(parent, "Fixture_" + name);
            Vector3 dir = (target - pos).normalized;
            Quaternion along = Quaternion.FromToRotation(Vector3.up, dir);

            GameObject can = Cylinder(g, "Can", pos - dir * 0.12f, 0.15f, 0.22f, Finishes.MetalDark, false);
            can.transform.rotation = along;
            GameObject lens = Cylinder(g, "Lens", pos - dir * 0.005f, 0.12f, 0.01f, Finishes.LampShade, false);
            lens.transform.rotation = along;
            lens.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            AddLampGlow(g.gameObject, lens.GetComponent<Renderer>());

            Vector3 back = pos - dir * 0.24f;
            if (ceilingY - back.y > 0.02f)
            {
                RemoveCollider(AddSlab(g, "Rod", back.x - 0.012f, back.x + 0.012f, back.z - 0.012f, back.z + 0.012f, back.y, ceilingY, Finishes.MetalDark));
            }
        }

        private static Vector3 RoomCenterFor(Vector3 p, bool second)
        {
            RoomStyle r = RoomAt(p.x, p.z, second);
            string n = r != null ? r.Name : "";
            switch (n)
            {
                case "Reception": return new Vector3(19.5f, p.y, 4.5f);
                case "Sculpture": return SculptureCenter();
                case "Costume": return new Vector3(33f, p.y, 13f);
                case "OilHall": return new Vector3(6.5f, p.y, 13f);
                case "Gallery": return new Vector3(19.5f, p.y, 10.5f);
                case "StairHall": return new Vector3(22f, p.y, 22.3f);
                default: return p + Vector3.back;
            }
        }

        /// <summary>出口灯的绿色微光。</summary>
        private static void BuildAccentLights(Transform parent)
        {
            Transform g = NewGroup(parent, "Exit_Glow");
            for (int i = 0; i < ExitSignLights.Count; i++)
            {
                AddPointLight(g, "ExitGlow_" + i, ExitSignLights[i], new Color(0.2f, 1f, 0.45f), 0.8f, 2.2f, false, false);
            }
        }

        /// <summary>吊灯：一根吊杆 + 一个发光灯罩，挂在每盏底光的位置上。</summary>
        private static void AddLampFixture(string name, Vector3 pos, Color color)
        {
            if (lampFixtures == null)
            {
                return;
            }

            float ceiling = pos.y > Floor2Y ? RoofY : Floor1Height;
            Transform g = NewGroup(lampFixtures, "Lamp_" + name);
            float rodTop = ceiling;
            float rodBottom = pos.y + 0.1f;
            RemoveCollider(AddSlab(g, "Rod", pos.x - 0.012f, pos.x + 0.012f, pos.z - 0.012f, pos.z + 0.012f, rodBottom, rodTop, Finishes.Brass));
            GameObject shade = Cylinder(g, "Shade", pos + Vector3.up * 0.1f, 0.46f, 0.14f, Finishes.LampShade, false);
            shade.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            GameObject cap = Cylinder(g, "Cap", pos + Vector3.up * 0.19f, 0.2f, 0.04f, Finishes.Brass, false);
            cap.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            AddLampGlow(g.gameObject, shade.GetComponent<Renderer>());
        }

        /// <summary>断电时灯罩 / 镜片一起熄灭（LampGlow 监听 MuseumLights）。</summary>
        private static void AddLampGlow(GameObject owner, Renderer glow)
        {
            Ib.Environment.LampGlow lg = owner.AddComponent<Ib.Environment.LampGlow>();
            SetRefArray(lg, "glows", new List<Renderer> { glow });
            SetRef(lg, "onMaterial", Finishes.LampShade);
            SetRef(lg, "offMaterial", OilKit.BulbOff);
        }

        // ================================================================ 浮尘

        private static void BuildDustMotes(GameObject gate)
        {
            GameObject go = new GameObject("--- Atmosphere ---");
            DustMotes dust = go.AddComponent<DustMotes>();
            SetRef(dust, "moteMaterial", Finishes.DustMote);
            SetRef(dust, "gate", gate);
        }

        // ================================================================ 空白展板换成画

        /// <summary>
        /// 二层长廊几块空白占位板：W2 / W3 换成馆里还没挂出来的两幅名画模型（《呐喊》《拿破仑翻越阿尔卑斯山》）；
        /// W1 / E3（停电后会变空框的两幅）、Grampus 证件照、Lily 看门的速写用程序画出来的画布 + 画框；
        /// 全家福厅北墙的《美术馆》按 GDD 是"盖着布的画"：画框 + 一块垂下来的布。
        /// 返回新挂的名画模型（要配射灯）；保留下来的板子仍由 CollectPanelExhibits 配灯。
        /// </summary>
        private static List<Exhibit> DressBlankBoards(Transform props)
        {
            List<Exhibit> exhibits = new List<Exhibit>();
            Transform art = GameObject.Find("Artworks").transform;
            Material gilt = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Paintings/M_Frame_GiltPatina.mat") ?? Finishes.Gilt;
            Material darkFrame = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Paintings/M_Frame_DarkWood.mat") ?? Finishes.Trim;

            exhibits.AddRange(ReplaceWithPainting(props, art, "Masterpiece_W2", "Painting_Scream", Vector3.right));
            exhibits.AddRange(ReplaceWithPainting(props, art, "Masterpiece_W3", "Painting_NapoleonCrossingAlps", Vector3.right));
            // 用户 10-28 手调：《拿破仑》的画布往外推 3.5 cm（放大后约 5 cm），不让画框边压住画面
            Transform napoleonCanvas = FindDeep(art, "Painting_NapoleonCrossingAlps_Canvas");
            if (napoleonCanvas != null)
            {
                napoleonCanvas.position += Vector3.right * 0.035f;
            }

            PaintBoard(props, "Masterpiece_W1_EmptyFrameA", Finishes.CanvasPortrait, gilt, Vector3.right, 0.14f);
            PaintBoard(props, "Masterpiece_E3_EmptyFrameB", Finishes.CanvasLandscape, gilt, Vector3.left, 0.14f);
            PaintBoard(props, "Grampus_IDPhoto", Finishes.CanvasPortrait, darkFrame, Vector3.back, 0.05f);
            PaintBoard(props, "Sketch_LilyLooksAtDoor", Finishes.CanvasSketch, darkFrame, Vector3.right, 0.05f);

            Transform exit = FindDeep(props, "Painting_TheMuseum_ExitFrame");
            if (exit != null)
            {
                PaintBoard(props, "Painting_TheMuseum_ExitFrame", Finishes.CanvasLandscape, gilt, Vector3.back, 0.14f);
                Bounds b = exit.GetComponent<Renderer>().bounds;
                Transform cloth = NewGroup(exit.parent, "DustCloth");
                // 布比画框大一圈，下摆多垂 0.25 m，正面离墙 0.12 m（盖住画框）
                // 画挂在北墙、正面朝 −Z，所以布在 b.min.z 再往外
                RemoveCollider(AddBox(cloth, "Cloth", new Vector3(b.center.x, b.center.y - 0.1f, b.min.z - 0.06f),
                    new Vector3(b.size.x + 0.4f, b.size.y + 0.5f, 0.04f), Finishes.Canvas));
                RemoveCollider(AddBox(cloth, "Cloth_Fold", new Vector3(b.center.x - 0.4f, b.min.y - 0.32f, b.min.z - 0.09f),
                    new Vector3(0.9f, 0.12f, 0.05f), Finishes.Canvas));
            }

            return exhibits;
        }

        private static IEnumerable<Exhibit> ReplaceWithPainting(Transform props, Transform art, string board, string painting, Vector3 normal)
        {
            Transform t = FindDeep(props, board);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Paintings/" + painting + ".fbx");
            if (t == null || asset == null)
            {
                yield break;
            }

            Bounds b = t.GetComponent<Renderer>().bounds;
            Vector3 wall = b.center - normal * (b.size.x < b.size.z ? b.size.x : b.size.z) * 0.5f - normal * 0.01f;
            Object.DestroyImmediate(t.gameObject);

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(asset, art);
            go.name = board + "_" + painting.Replace("Painting_", "");
            // 和 Artworks 表里的名画同一种摆法：原点在画框背面中心，正面朝 normal
            go.transform.position = wall;
            go.transform.rotation = Quaternion.LookRotation(normal, Vector3.up);
            go.transform.localScale = Vector3.one;

            // 按板子的大小缩放：画框高约 2.3 m（板子 2.0 m 加一圈框），宽不超过 2.6 m
            Bounds pb = WorldBounds(go);
            float width = Mathf.Max(pb.size.x, pb.size.z);
            float k = Mathf.Min(2.3f / Mathf.Max(pb.size.y, 0.01f), 2.6f / Mathf.Max(width, 0.01f));
            go.transform.localScale = Vector3.one * k;
            pb = WorldBounds(go);
            go.transform.position += Vector3.up * (b.center.y - pb.center.y);

            yield return new Exhibit { Name = go.name, B = WorldBounds(go), Normal = normal };
        }

        /// <summary>板子正面换成程序画布，四周加一圈画框（frame = 框宽）。</summary>
        private static void PaintBoard(Transform props, string board, Material canvas, Material frameMat, Vector3 normal, float frame)
        {
            Transform t = FindDeep(props, board);
            if (t == null)
            {
                return;
            }

            MeshRenderer r = t.GetComponent<MeshRenderer>();
            r.sharedMaterial = canvas;
            Bounds b = r.bounds;
            bool alongX = Mathf.Abs(normal.z) > 0.5f;   // 画面沿 X 展开（挂在南北墙上）
            float w = alongX ? b.size.x : b.size.z;
            float h = b.size.y;
            float depth = 0.09f;
            Vector3 face = b.center + normal * (alongX ? b.extents.z : b.extents.x);
            Vector3 side = alongX ? Vector3.right : Vector3.forward;
            Transform g = NewGroup(t.parent, board + "_Frame");

            Vector3 Size(float along, float up) => side * along + Vector3.up * up + Abs(normal) * depth;
            Vector3 c0 = face - normal * (depth * 0.5f - 0.02f);
            RemoveCollider(AddBox(g, "Top", c0 + Vector3.up * (h * 0.5f + frame * 0.5f), Size(w + frame * 2f, frame), frameMat));
            RemoveCollider(AddBox(g, "Bottom", c0 - Vector3.up * (h * 0.5f + frame * 0.5f), Size(w + frame * 2f, frame), frameMat));
            RemoveCollider(AddBox(g, "Left", c0 - side * (w * 0.5f + frame * 0.5f), Size(frame, h), frameMat));
            RemoveCollider(AddBox(g, "Right", c0 + side * (w * 0.5f + frame * 0.5f), Size(frame, h), frameMat));
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }

        // ================================================================ 工具

        private static Bounds WorldBounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0)
            {
                return new Bounds(go.transform.position, Vector3.zero);
            }

            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs)
            {
                b.Encapsulate(r.bounds);
            }

            return b;
        }

        /// <summary>按网格在根节点局部空间的包围盒加一个 BoxCollider（模型自己没有碰撞体）。</summary>
        private static void AddLocalBoxCollider(GameObject go)
        {
            Matrix4x4 toRoot = go.transform.worldToLocalMatrix;
            bool first = true;
            Bounds local = new Bounds();
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null)
                {
                    continue;
                }

                Bounds mb = mf.sharedMesh.bounds;
                Matrix4x4 m = toRoot * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x, (i & 2) == 0 ? mb.min.y : mb.max.y, (i & 4) == 0 ? mb.min.z : mb.max.z);
                    Vector3 v = m.MultiplyPoint3x4(c);
                    if (first)
                    {
                        local = new Bounds(v, Vector3.zero);
                        first = false;
                    }
                    else
                    {
                        local.Encapsulate(v);
                    }
                }
            }

            if (first)
            {
                return;
            }

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.center = local.center;
            box.size = local.size;
        }

        private static Vector3 Abs(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }

        /// <summary>水平面内与法线垂直的方向（取绝对值，用来拼尺寸）。</summary>
        private static Vector3 Perp(Vector3 normal)
        {
            return Abs(Vector3.Cross(Vector3.up, normal));
        }
    }
}
