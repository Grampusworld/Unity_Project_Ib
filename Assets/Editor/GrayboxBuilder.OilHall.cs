using System.Collections.Generic;
using Ib.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ib.EditorTools
{
    /// <summary>
    /// 一楼油画厅（2026-10-28）：两盏水晶吊灯（断电后摇晃，见 ChandelierSway）、中轴红地毯、大型绿植、
    /// 入口处的语音导览架和本单元导言牌（游戏内文字用英文）。
    /// 尺寸按 GDD 1 倍写，生成末尾整体乘 WorldScale。
    /// </summary>
    public static partial class GrayboxBuilder
    {
        private sealed class OilHallKit
        {
            public Mesh Ring;          // 黄铜环（圆环）
            public Mesh Link;          // 吊链的一节
            public Mesh Prism;         // 水晶坠（双锥）
            public Mesh Canopy;        // 吸顶盘
            public Mesh Stem;          // 灯柱（车床花瓶柱）
            public Mesh Bobeche;       // 烛台托盘
            public Mesh Finial;        // 底部尖顶
            public Mesh Pot;           // 花盆
            public Mesh Leaf;          // 双面叶片
            public Material Crystal;
            public Material BulbOn;
            public Material BulbOff;
            public Material Candle;
            public Material Carpet;
            public Material CarpetBorder;
            public Material PotGlaze;
            public Material Soil;
            public Material LeafGreen;
            public Material Bark;
            public Material PanelRed;
            public Material DeviceBlack;
            public Material LedGreen;
        }

        private static OilHallKit oilKit;

        private static OilHallKit OilKit => oilKit ?? (oilKit = LoadOilHallKit());

        private static OilHallKit LoadOilHallKit()
        {
            EnsureFolder(GeneratedDir);
            OilHallKit k = new OilHallKit();

            k.Ring = TorusAsset("Mesh_Ring", 1f, 0.03f, 40, 8);
            k.Link = TorusAsset("Mesh_ChainLink", 0.022f, 0.006f, 12, 6);
            k.Prism = PrismAsset("Mesh_CrystalPrism");
            k.Leaf = LeafAsset("Mesh_Leaf");

            k.Canopy = LatheAsset("Mesh_Chandelier_Canopy", new[]
            {
                new Vector2(0f, -0.13f), new Vector2(0.05f, -0.13f), new Vector2(0.12f, -0.1f), new Vector2(0.2f, -0.05f),
                new Vector2(0.24f, -0.01f), new Vector2(0.25f, 0f), new Vector2(0f, 0f),
            }, 1f, 1f, 32);
            k.Stem = LatheAsset("Mesh_Chandelier_Stem", new[]
            {
                new Vector2(0f, -1.15f), new Vector2(0.03f, -1.15f), new Vector2(0.09f, -1.08f), new Vector2(0.11f, -0.98f),
                new Vector2(0.06f, -0.9f), new Vector2(0.04f, -0.78f), new Vector2(0.08f, -0.62f), new Vector2(0.1f, -0.5f),
                new Vector2(0.05f, -0.38f), new Vector2(0.03f, -0.25f), new Vector2(0.05f, -0.12f), new Vector2(0.03f, 0f), new Vector2(0f, 0f),
            }, 1f, 1f, 24);
            k.Bobeche = LatheAsset("Mesh_Chandelier_Bobeche", new[]
            {
                new Vector2(0f, -0.02f), new Vector2(0.015f, -0.02f), new Vector2(0.05f, 0f), new Vector2(0.06f, 0.012f), new Vector2(0f, 0.008f),
            }, 1f, 1f, 16);
            k.Finial = LatheAsset("Mesh_Chandelier_Finial", new[]
            {
                new Vector2(0f, -0.3f), new Vector2(0.025f, -0.24f), new Vector2(0.06f, -0.12f), new Vector2(0.12f, -0.04f), new Vector2(0.13f, 0f), new Vector2(0f, 0f),
            }, 1f, 1f, 24);
            k.Pot = LatheAsset("Mesh_PlantPot", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.22f, 0f), new Vector2(0.24f, 0.04f), new Vector2(0.3f, 0.45f),
                new Vector2(0.34f, 0.52f), new Vector2(0.35f, 0.56f), new Vector2(0.31f, 0.57f), new Vector2(0.29f, 0.5f), new Vector2(0f, 0.5f),
            }, 1f, 1f, 28);

            k.Crystal = TransparentLit("M_Crystal", new Color(0.92f, 0.96f, 1f, 0.42f), 0.98f);
            k.BulbOn = Emissive("M_Bulb_On", new Color(1f, 0.82f, 0.55f), 3.2f);
            k.BulbOff = Lit("M_Bulb_Off", null, null, new Color(0.45f, 0.43f, 0.4f), 0.85f, 1f, 0f, false);
            k.Candle = Lit("M_Candle_Sleeve", null, null, new Color(0.88f, 0.85f, 0.78f), 0.3f, 1f, 0f, false);
            k.Carpet = Lit("M_Carpet_Red", Finishes.Canvas.mainTexture as Texture2D, null, new Color(0.36f, 0.05f, 0.06f), 0.04f, 0.6f);
            k.CarpetBorder = Lit("M_Carpet_Gold", Finishes.Canvas.mainTexture as Texture2D, null, new Color(0.52f, 0.38f, 0.14f), 0.08f, 0.6f);
            k.PotGlaze = Lit("M_Pot_Glaze", null, null, new Color(0.10f, 0.14f, 0.13f), 0.7f, 1f, 0f, false);
            k.Soil = Lit("M_Soil", null, null, new Color(0.09f, 0.07f, 0.05f), 0.05f, 1f, 0f, false);
            k.LeafGreen = Lit("M_Leaf", null, null, new Color(0.10f, 0.22f, 0.08f), 0.45f, 1f, 0f, false);
            k.Bark = Lit("M_Bark", null, null, new Color(0.20f, 0.14f, 0.09f), 0.1f, 1f, 0f, false);
            k.PanelRed = Lit("M_Panel_Oxblood", null, null, new Color(0.22f, 0.05f, 0.05f), 0.25f, 1f, 0f, false);
            k.DeviceBlack = Lit("M_Device_Black", null, null, new Color(0.04f, 0.04f, 0.045f), 0.6f, 1f, 0f, false);
            k.LedGreen = Emissive("M_Led_Green", new Color(0.2f, 1f, 0.35f), 3f);
            return k;
        }

        // ================================================================ 入口：油画厅布置

        /// <summary>油画厅：中轴红地毯、5 盆大型绿植、入口（接待厅那扇门）两侧的语音导览架和导言牌、两盏水晶吊灯。</summary>
        private static void DressOilHall(Transform oil)
        {
            OilHallKit k = OilKit;
            float y = Floor1Y;

            // ---- 中轴红地毯：金边，离南墙长凳和北墙都留出距离；不进隔离绳区 ----
            Transform carpet = NewGroup(oil, "Carpet_Runner");
            const float cx = WestLineX * 0.5f;
            const float halfW = 1.2f;
            const float z0 = 1.7f;
            const float z1 = 24.3f;
            Slab(carpet, "Runner", cx - halfW, cx + halfW, z0, z1, y, y + 0.012f, k.Carpet);
            Slab(carpet, "Border_W", cx - halfW, cx - halfW + 0.12f, z0, z1, y + 0.012f, y + 0.014f, k.CarpetBorder);
            Slab(carpet, "Border_E", cx + halfW - 0.12f, cx + halfW, z0, z1, y + 0.012f, y + 0.014f, k.CarpetBorder);
            Slab(carpet, "Border_S", cx - halfW + 0.12f, cx + halfW - 0.12f, z0, z0 + 0.12f, y + 0.012f, y + 0.014f, k.CarpetBorder);
            Slab(carpet, "Border_N", cx - halfW + 0.12f, cx + halfW - 0.12f, z1 - 0.12f, z1, y + 0.012f, y + 0.014f, k.CarpetBorder);

            // ---- 大型绿植：四角里的三角 + 中门两侧（门前通道外）----
            Transform plants = NewGroup(oil, "Plants");
            float e = WestLineX - HalfWall - 0.55f;
            Plant(plants, "Plant_NW", new Vector3(HalfWall + 0.55f, y, BuildingDepth - HalfWall - 0.55f), 11);
            Plant(plants, "Plant_NE", new Vector3(e, y, BuildingDepth - HalfWall - 0.55f), 23);
            Plant(plants, "Plant_SE", new Vector3(e, y, HalfWall + 0.55f), 37);
            Plant(plants, "Plant_MidDoor_S", new Vector3(e, y, MidDoorZ - 2.55f), 41);
            Plant(plants, "Plant_MidDoor_N", new Vector3(e, y, MidDoorZ + 2.55f), 53);

            // ---- 入口：接待厅那扇门（东墙 z = 4.5）南边语音导览架、北边导言牌 ----
            float wallX = WestLineX - HalfWall;
            AudioGuideRack(oil, new Vector3(wallX, y, 2.35f));
            IntroPanel(oil, new Vector3(wallX, y, 7.0f));

            // ---- 两盏水晶吊灯：挂在中轴梁的交点下面 ----
            Transform chandeliers = NewGroup(oil, "Chandeliers");
            float beamBottom = Floor1Height - 0.42f;
            Chandelier(chandeliers, "Chandelier_S", new Vector3(cx, beamBottom, 9.8f));
            Chandelier(chandeliers, "Chandelier_N", new Vector3(cx, beamBottom, 16.2f));
        }

        private static GameObject Slab(Transform parent, string name, float x0, float x1, float z0, float z1, float y0, float y1, Material mat)
        {
            GameObject go = AddSlab(parent, name, x0, x1, z0, z1, y0, y1, mat);
            RemoveCollider(go);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        // ================================================================ 水晶吊灯

        /// <summary>
        /// 水晶吊灯（吊点 = anchor，往下 2.5 m）：吸顶盘 → 吊链 → 花瓶灯柱 → 上下两层黄铜环和弯臂 →
        /// 每支臂端一只烛托 + 白色烛套 + 灯泡（暖光自发光）→ 臂端垂坠、层间水晶串、下环一圈水晶帘、底部尖顶和长坠。
        /// 黄铜件、烛套、水晶分别合并成一个网格（每盏 3 个 + 灯泡），灯泡单独留着给断电时换材质。
        /// 中间一盏暖色点光（Light 在灯泡圈中间，看得见的灯具里）。
        /// </summary>
        private static void Chandelier(Transform parent, string name, Vector3 anchor)
        {
            OilHallKit k = OilKit;
            Transform root = NewGroup(parent, name, anchor, 0f);
            Transform brass = NewGroup(root, "_brass");
            Transform crystals = NewGroup(root, "_crystal");
            Transform candles = NewGroup(root, "_candle");
            List<Renderer> bulbs = new List<Renderer>();

            MeshObject(brass, "Canopy", k.Canopy, Finishes.Brass, Vector3.zero, Vector3.one);

            // 吊链：每节 4.4 cm，相邻两节转 90°
            const float chainTop = -0.12f;
            const float chainBottom = -1.25f;
            int links = Mathf.RoundToInt((chainTop - chainBottom) / 0.036f);
            for (int i = 0; i < links; i++)
            {
                GameObject link = MeshObject(brass, "Link_" + i, k.Link, Finishes.Brass, new Vector3(0f, chainTop - 0.018f - i * 0.036f, 0f), new Vector3(1f, 1f, 1.6f));
                link.transform.localRotation = Quaternion.Euler(90f, i % 2 == 0 ? 0f : 90f, 0f);
            }

            MeshObject(brass, "Stem", k.Stem, Finishes.Brass, new Vector3(0f, chainBottom, 0f), Vector3.one);

            // 两层：上层 6 臂 r 0.38，下层 10 臂 r 0.72
            float[] tierY = { -1.78f, -2.12f };
            float[] tierR = { 0.38f, 0.72f };
            int[] tierN = { 6, 10 };
            for (int t = 0; t < 2; t++)
            {
                float ty = tierY[t];
                float r = tierR[t];
                MeshObject(brass, "Ring_" + t, k.Ring, Finishes.Brass, new Vector3(0f, ty - 0.02f, 0f), new Vector3(r, 1f, r));
                Vector3 prev = Vector3.zero;
                for (int i = 0; i < tierN[t]; i++)
                {
                    float a = (i + (t == 0 ? 0.5f : 0f)) / tierN[t] * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 tip = dir * r + new Vector3(0f, ty, 0f);

                    // 弯臂：从灯柱伸出、往下弯再翘起来（三段）
                    Vector3 p0 = new Vector3(0f, ty + 0.06f, 0f) + dir * 0.06f;
                    Vector3 p1 = dir * (r * 0.55f) + new Vector3(0f, ty - 0.08f, 0f);
                    Vector3 p2 = tip + new Vector3(0f, -0.03f, 0f);
                    Vector3 p3 = tip + new Vector3(0f, 0.09f, 0f);
                    Rod(brass, "Arm", p0, p1, 0.022f);
                    Rod(brass, "Arm", p1, p2, 0.022f);
                    Rod(brass, "Arm", p2, p3, 0.02f);

                    MeshObject(brass, "Bobeche", k.Bobeche, Finishes.Brass, p3, Vector3.one);
                    GameObject sleeve = LocalCylinder(candles, "Candle", p3 + new Vector3(0f, 0.07f, 0f), 0.028f, 0.13f, k.Candle);
                    GameObject bulb = LocalSphere(root, "Bulb_" + t + "_" + i, p3 + new Vector3(0f, 0.165f, 0f), new Vector3(0.042f, 0.07f, 0.042f), k.BulbOn);
                    bulb.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                    bulbs.Add(bulb.GetComponent<Renderer>());

                    // 臂端垂坠：一颗小珠 + 一根长坠
                    Crystal(crystals, p3 + new Vector3(0f, -0.04f, 0f), 0.022f, 0.035f);
                    Crystal(crystals, p3 + new Vector3(0f, -0.12f, 0f), 0.028f, 0.09f);

                    // 层间水晶串：相邻两臂之间下垂的弧，5 颗
                    if (i > 0 || t == 1)
                    {
                        Vector3 from = i > 0 ? prev : new Vector3(Mathf.Cos((tierN[t] - 1f) / tierN[t] * Mathf.PI * 2f), 0f, Mathf.Sin((tierN[t] - 1f) / tierN[t] * Mathf.PI * 2f)) * r + new Vector3(0f, ty, 0f);
                        for (int b = 1; b <= 5; b++)
                        {
                            float u = b / 6f;
                            Vector3 bead = Vector3.Lerp(from, tip, u) + new Vector3(0f, -0.1f * Mathf.Sin(u * Mathf.PI) - 0.03f, 0f);
                            Crystal(crystals, bead, 0.018f, 0.022f);
                        }
                    }

                    prev = tip;
                }
            }

            // 下环一圈水晶帘（24 根长坠）+ 底部尖顶和一簇长坠
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 0.62f, -2.26f - (i % 2) * 0.05f, Mathf.Sin(a) * 0.62f);
                Crystal(crystals, p, 0.022f, 0.11f + (i % 3) * 0.02f);
            }

            MeshObject(brass, "Finial", k.Finial, Finishes.Brass, new Vector3(0f, chainBottom - 1.15f, 0f), Vector3.one);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                Crystal(crystals, new Vector3(Mathf.Cos(a) * 0.11f, -2.55f, Mathf.Sin(a) * 0.11f), 0.03f, 0.16f);
            }

            Crystal(crystals, new Vector3(0f, -2.75f, 0f), 0.045f, 0.2f);

            // 合并：黄铜 / 烛套 / 水晶各一个网格
            MergeChildren(brass, name + "_Brass", Finishes.Brass, true);
            MergeChildren(candles, name + "_Candles", k.Candle, true);
            MergeChildren(crystals, name + "_Crystals", k.Crystal, false);

            // 灯：在上层那圈灯泡中间（光从灯泡来）
            GameObject lightGO = new GameObject("Lamp");
            lightGO.transform.SetParent(root, false);
            lightGO.transform.localPosition = new Vector3(0f, -1.55f, 0f);
            Light lamp = lightGO.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.84f, 0.62f);
            lamp.intensity = 7f;
            lamp.range = 9f;
            lamp.shadows = LightShadows.None;

            ChandelierSway sway = root.gameObject.AddComponent<ChandelierSway>();
            SetRefArray(sway, "bulbs", bulbs);
            SetRef(sway, "bulbOn", k.BulbOn);
            SetRef(sway, "bulbOff", k.BulbOff);
            SetRef(sway, "lamp", lamp);
        }

        private static void Rod(Transform parent, string name, Vector3 a, Vector3 b, float diameter)
        {
            GameObject rod = LocalCylinder(parent, name, (a + b) * 0.5f, diameter, Vector3.Distance(a, b), Finishes.Brass);
            rod.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
        }

        private static void Crystal(Transform parent, Vector3 top, float width, float length)
        {
            MeshObject(parent, "Crystal", OilKit.Prism, OilKit.Crystal, top - new Vector3(0f, length * 0.5f, 0f), new Vector3(width, length, width), false);
        }

        /// <summary>把 group 下所有网格按 group 父节点的局部空间合并成一个，删掉零件。</summary>
        private static void MergeChildren(Transform group, string meshName, Material mat, bool shadows)
        {
            Transform owner = group.parent;
            List<CombineInstance> parts = new List<CombineInstance>();
            foreach (MeshFilter mf in group.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null)
                {
                    continue;
                }

                parts.Add(new CombineInstance { mesh = mf.sharedMesh, transform = owner.worldToLocalMatrix * mf.transform.localToWorldMatrix });
            }

            Mesh merged = new Mesh { name = meshName, indexFormat = IndexFormat.UInt32 };
            merged.CombineMeshes(parts.ToArray(), true, true);
            merged.RecalculateBounds();

            Object.DestroyImmediate(group.gameObject);
            GameObject go = new GameObject(meshName);
            go.transform.SetParent(owner, false);
            go.AddComponent<MeshFilter>().sharedMesh = merged;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        // ================================================================ 绿植

        /// <summary>
        /// 大型盆栽（橡皮树 / 琴叶榕一类）：深色釉面花盆 + 土 + 三根枝干 + 40 片左右的叶子（种子固定，重新生成长得一样）。
        /// 叶子合并成一个网格；花盆带碰撞。总高约 2.1 m（放大后近 3 m）。
        /// </summary>
        private static void Plant(Transform parent, string name, Vector3 basePos, int seed)
        {
            OilHallKit k = OilKit;
            Transform g = NewGroup(parent, name, basePos, seed * 37f % 360f);
            MeshObject(g, "Pot", k.Pot, k.PotGlaze, Vector3.zero, Vector3.one);
            LocalCylinder(g, "Soil", new Vector3(0f, 0.5f, 0f), 0.56f, 0.02f, k.Soil);
            BoxCollider col = g.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.5f, 0f);
            col.size = new Vector3(0.7f, 1.0f, 0.7f);

            System.Random rng = new System.Random(seed);
            Transform leaves = NewGroup(g, "_leaves");
            for (int s = 0; s < 3; s++)
            {
                float a = s / 3f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.6f;
                Vector3 lean = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (0.12f + (float)rng.NextDouble() * 0.1f);
                float h = 1.35f + (float)rng.NextDouble() * 0.5f;
                Vector3 bottom = new Vector3(0f, 0.5f, 0f);
                Vector3 topP = bottom + new Vector3(lean.x, h, lean.z);
                Rod(g, "Stem_" + s, bottom, topP, 0.035f);
                g.Find("Stem_" + s).GetComponent<MeshRenderer>().sharedMaterial = k.Bark;

                // 叶子沿枝干上半段螺旋长出，越往上越密
                for (int i = 0; i < 14; i++)
                {
                    float u = 0.35f + 0.65f * (i / 13f);
                    Vector3 at = Vector3.Lerp(bottom, topP, u);
                    float yaw = i * 137.5f + s * 40f + (float)rng.NextDouble() * 20f;
                    float pitch = 20f + (float)rng.NextDouble() * 45f - u * 15f;
                    float size = 0.75f + (float)rng.NextDouble() * 0.45f;
                    GameObject leaf = MeshObject(leaves, "Leaf", k.Leaf, k.LeafGreen, at, Vector3.one * size, true);
                    leaf.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(pitch, 0f, 0f);
                }
            }

            MergeChildren(leaves, name + "_Leaves", k.LeafGreen, true);
        }

        // ================================================================ 语音导览架、导言牌

        /// <summary>靠墙的语音导览架：木柜 + 两排充电插槽里的黑色导览机（绿色充电灯）+ 顶上英文牌子。</summary>
        private static void AudioGuideRack(Transform parent, Vector3 wallPoint)
        {
            OilHallKit k = OilKit;
            // 墙在 +X（东墙），架子朝 −X（朝厅里）
            Transform g = NewGroup(parent, "AudioGuide_Rack", wallPoint, -90f);
            AddBox(g, "Cabinet", new Vector3(0f, 0.55f, 0.17f), new Vector3(0.9f, 1.1f, 0.34f), Finishes.Wainscot);
            LocalBox(g, "Top", new Vector3(0f, 1.12f, 0.17f), new Vector3(0.96f, 0.04f, 0.38f), Finishes.StoneWorn);
            LocalBox(g, "Kick", new Vector3(0f, 0.05f, 0.17f), new Vector3(0.92f, 0.1f, 0.36f), Finishes.Trim);
            for (int row = 0; row < 2; row++)
            {
                float sy = 0.72f + row * 0.26f;
                LocalBox(g, "Shelf_" + row, new Vector3(0f, sy - 0.11f, 0.35f), new Vector3(0.84f, 0.02f, 0.03f), Finishes.Brass);
                for (int i = 0; i < 5; i++)
                {
                    float x = -0.32f + i * 0.16f;
                    LocalBox(g, "Guide_" + row + "_" + i, new Vector3(x, sy, 0.345f), new Vector3(0.06f, 0.2f, 0.03f), k.DeviceBlack);
                    LocalBox(g, "Screen_" + row + "_" + i, new Vector3(x, sy + 0.04f, 0.361f), new Vector3(0.045f, 0.06f, 0.003f), Finishes.GlassClear);
                    LocalBox(g, "Led_" + row + "_" + i, new Vector3(x, sy - 0.08f, 0.361f), new Vector3(0.008f, 0.008f, 0.004f), k.LedGreen);
                }
            }

            // 顶上的牌子
            LocalBox(g, "Sign_Post_L", new Vector3(-0.4f, 1.3f, 0.05f), new Vector3(0.025f, 0.36f, 0.025f), Finishes.Brass);
            LocalBox(g, "Sign_Post_R", new Vector3(0.4f, 1.3f, 0.05f), new Vector3(0.025f, 0.36f, 0.025f), Finishes.Brass);
            LocalBox(g, "Sign", new Vector3(0f, 1.48f, 0.05f), new Vector3(0.92f, 0.26f, 0.03f), k.PanelRed);
            Label3D(g, "Text_Title", new Vector3(0f, 1.52f, 0.068f), "AUDIO GUIDE", 0.07f, new Color(0.9f, 0.78f, 0.5f));
            Label3D(g, "Text_Sub", new Vector3(0f, 1.42f, 0.068f), "Please take one  ·  Free of charge", 0.03f, new Color(0.85f, 0.8f, 0.7f));
        }

        /// <summary>本单元导言牌：深红底板 + 做旧描金框 + 英文标题和导言（游戏内文字统一英文）。</summary>
        private static void IntroPanel(Transform parent, Vector3 wallPoint)
        {
            OilHallKit k = OilKit;
            Transform g = NewGroup(parent, "Section_IntroPanel", wallPoint, -90f);
            const float w = 1.7f;
            const float h = 2.1f;
            const float cy = 2.15f;
            AddBox(g, "Board", new Vector3(0f, cy, 0.025f), new Vector3(w, h, 0.05f), k.PanelRed);
            Material gilt = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Paintings/M_Frame_GiltPatina.mat") ?? Finishes.Gilt;
            LocalBox(g, "Frame_T", new Vector3(0f, cy + h * 0.5f + 0.04f, 0.035f), new Vector3(w + 0.16f, 0.08f, 0.07f), gilt);
            LocalBox(g, "Frame_B", new Vector3(0f, cy - h * 0.5f - 0.04f, 0.035f), new Vector3(w + 0.16f, 0.08f, 0.07f), gilt);
            LocalBox(g, "Frame_L", new Vector3(-w * 0.5f - 0.04f, cy, 0.035f), new Vector3(0.08f, h, 0.07f), gilt);
            LocalBox(g, "Frame_R", new Vector3(w * 0.5f + 0.04f, cy, 0.035f), new Vector3(0.08f, h, 0.07f), gilt);

            Color gold = new Color(0.92f, 0.8f, 0.52f);
            Color cream = new Color(0.86f, 0.82f, 0.72f);
            float zf = 0.053f;
            Label3D(g, "Text_Gallery", new Vector3(0f, cy + 0.8f, zf), "GALLERY  I", 0.05f, gold);
            Label3D(g, "Text_Title", new Vector3(0f, cy + 0.6f, zf), "THE OIL PAINTINGS", 0.1f, gold);
            Label3D(g, "Text_Rule", new Vector3(0f, cy + 0.44f, zf), "—————", 0.05f, gold);
            string[] body =
            {
                "Portraits, saints and quiet fields —",
                "the paintings Grampus returned to",
                "again and again, hung together",
                "for five nights only.",
                "",
                "Look closely. Every face here",
                "was painted from memory.",
            };
            for (int i = 0; i < body.Length; i++)
            {
                Label3D(g, "Text_Body_" + i, new Vector3(0f, cy + 0.26f - i * 0.12f, zf), body[i], 0.045f, cream);
            }

            Label3D(g, "Text_Footer", new Vector3(0f, cy - 0.92f, zf), "Please do not touch the paintings.", 0.035f, cream);
        }

        /// <summary>
        /// 3D 文字（内置字体，TextMesh）：capHeight = 大写字母高度（米）。正面朝本地 +Z（牌子朝外的方向）。
        /// </summary>
        private static void Label3D(Transform parent, string name, Vector3 localPos, string text, float capHeight, Color color)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            // TextMesh 的字面朝 −Z，转 180° 让它朝外
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.fontSize = 96;
            tm.characterSize = capHeight / 6.5f;   // fontSize 96 时字母高约 6.5 × characterSize
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            // 不用字体自带材质（GUI/Text Shader 是 ZTest Always，字会穿墙）：换成带深度测试的 Ib/Text3D
            MeshRenderer r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = Text3DMaterial(font);
            r.shadowCastingMode = ShadowCastingMode.Off;
            go.AddComponent<Text3DFont>();
        }

        private static Material Text3DMaterial(Font font)
        {
            string path = FinishDir + "/M_Text3D.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Ib/Text3D")) { name = "M_Text3D" };
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.mainTexture = font.material.mainTexture;
            return mat;
        }

        // ================================================================ 网格生成

        /// <summary>圆环：大半径 major、管半径 minor，环面在 XZ 平面。</summary>
        private static Mesh TorusAsset(string name, float major, float minor, int segU, int segV)
        {
            string path = GeneratedDir + "/" + name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                return existing;
            }

            List<Vector3> v = new List<Vector3>();
            List<int> tris = new List<int>();
            for (int i = 0; i <= segU; i++)
            {
                float a = i / (float)segU * Mathf.PI * 2f;
                Vector3 c = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int j = 0; j <= segV; j++)
                {
                    float b = j / (float)segV * Mathf.PI * 2f;
                    v.Add(c * (major + Mathf.Cos(b) * minor) + Vector3.up * Mathf.Sin(b) * minor);
                }
            }

            for (int i = 0; i < segU; i++)
            {
                for (int j = 0; j < segV; j++)
                {
                    int a = i * (segV + 1) + j;
                    int b = a + segV + 1;
                    tris.Add(a); tris.Add(a + 1); tris.Add(b);
                    tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
                }
            }

            Mesh m = new Mesh { name = name, vertices = v.ToArray(), triangles = tris.ToArray() };
            m.RecalculateNormals();
            m.RecalculateBounds();
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>水晶坠：八面切割的双锥（单位高 1、宽 1，中心在原点）。法线按面算，棱角分明。</summary>
        private static Mesh PrismAsset(string name)
        {
            string path = GeneratedDir + "/" + name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                return existing;
            }

            const int sides = 8;
            Vector3 top = new Vector3(0f, 0.5f, 0f);
            Vector3 bottom = new Vector3(0f, -0.5f, 0f);
            List<Vector3> v = new List<Vector3>();
            List<int> t = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                // 腰线偏上（像吊坠）
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0.2f, Mathf.Sin(a0) * 0.5f);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0.2f, Mathf.Sin(a1) * 0.5f);
                int n = v.Count;
                v.Add(top); v.Add(p1); v.Add(p0);
                t.Add(n); t.Add(n + 1); t.Add(n + 2);
                n = v.Count;
                v.Add(bottom); v.Add(p0); v.Add(p1);
                t.Add(n); t.Add(n + 1); t.Add(n + 2);
            }

            Mesh m = new Mesh { name = name, vertices = v.ToArray(), triangles = t.ToArray() };
            m.RecalculateNormals();
            m.RecalculateBounds();
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>双面叶片：根部在原点，沿 +Z 长 0.32 m、宽 0.14 m，中脉微微拱起、叶尖下垂。</summary>
        private static Mesh LeafAsset(string name)
        {
            string path = GeneratedDir + "/" + name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                return existing;
            }

            const int seg = 8;
            const float length = 0.32f;
            const float width = 0.14f;
            List<Vector3> v = new List<Vector3>();
            List<int> t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float u = i / (float)seg;
                float half = Mathf.Sin(u * Mathf.PI) * width * 0.5f * (1f - 0.25f * u);
                float z = u * length;
                float y = Mathf.Sin(u * Mathf.PI) * 0.015f - u * u * 0.06f;
                v.Add(new Vector3(-half, y - 0.006f, z));
                v.Add(new Vector3(0f, y + 0.008f, z));
                v.Add(new Vector3(half, y - 0.006f, z));
            }

            // 背面用一份独立的顶点（法线朝下），不然正反两面共享顶点、法线互相抵消
            int back = v.Count;
            v.AddRange(v.ToArray());
            for (int i = 0; i < seg; i++)
            {
                int a = i * 3;
                int b = a + 3;
                // 正面
                t.Add(a); t.Add(b); t.Add(a + 1); t.Add(a + 1); t.Add(b); t.Add(b + 1);
                t.Add(a + 1); t.Add(b + 1); t.Add(a + 2); t.Add(a + 2); t.Add(b + 1); t.Add(b + 2);
                // 背面（反向绕序）
                int c = back + a;
                int d = back + b;
                t.Add(c); t.Add(c + 1); t.Add(d); t.Add(c + 1); t.Add(d + 1); t.Add(d);
                t.Add(c + 1); t.Add(c + 2); t.Add(d + 1); t.Add(c + 2); t.Add(d + 2); t.Add(d + 1);
            }

            Mesh m = new Mesh { name = name, vertices = v.ToArray(), triangles = t.ToArray() };
            m.RecalculateNormals();
            m.RecalculateBounds();
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
