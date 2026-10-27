using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ib.EditorTools
{
    /// <summary>
    /// 程序建模：人台、礼服、挂衣、玻璃展柜、前台、厕所隔间与洁具。
    /// 回转体（人台躯干、裙子、挂着的衣服）用"车床"网格：给一条 (半径, 高度) 轮廓线绕 Y 轴转一圈，
    /// 截面可以压扁成椭圆（挂衣压得很扁）。网格存成 .asset 放在 Assets/Art/Models/Generated，已存在就复用。
    /// 其余部件用方块 / 圆柱拼。全部由生成器摆放，重新生成可复现。
    /// </summary>
    public static partial class GrayboxBuilder
    {
        private const string GeneratedDir = "Assets/Art/Models/Generated";
        private const int IgnoreRaycastLayer = 2;   // 玻璃：挡人不挡视线判定

        private sealed class ModelKit
        {
            public Mesh DressForm;      // 人台躯干（胯部到脖子）
            public Mesh Gown;           // 落地长裙（穿在人台上）
            public Mesh Coat;           // 及膝外套
            public Mesh HangDress;      // 挂在衣架上的裙子（压扁）
            public Mesh HangCoat;       // 挂在衣架上的大衣（压扁）
            public Mesh ChildDress;     // Lily 的小裙子
            public Mesh Basin;          // 洗手盆
            public Material Linen;      // 人台布面
            public Material Head;       // 人台头
            public Material Porcelain;
            public Material Chrome;
            public Material Partition;  // 厕所隔板
            public Material StallDoor;
            public Material Paper;
            public Material Book;
            public Material[] Fabrics;
            public Material Pink;
            public Material PinkGlow;
            public Material Stone;
            public Mesh Robe;
            public Mesh KneelBase;
            public Mesh KneelTorso;
        }

        private static ModelKit kit;

        private static ModelKit Kit
        {
            get
            {
                if (kit == null)
                {
                    kit = LoadKit();
                }

                return kit;
            }
        }

        private static ModelKit LoadKit()
        {
            EnsureFolder(GeneratedDir);
            ModelKit k = new ModelKit();

            k.DressForm = LatheAsset("Mesh_DressForm", new[]
            {
                new Vector2(0f, 0.74f), new Vector2(0.15f, 0.75f), new Vector2(0.175f, 0.82f), new Vector2(0.16f, 0.92f),
                new Vector2(0.13f, 1.02f), new Vector2(0.15f, 1.12f), new Vector2(0.172f, 1.2f), new Vector2(0.165f, 1.29f),
                new Vector2(0.15f, 1.36f), new Vector2(0.09f, 1.41f), new Vector2(0.055f, 1.43f), new Vector2(0.05f, 1.5f), new Vector2(0f, 1.5f),
            }, 1f, 0.72f);

            k.Gown = LatheAsset("Mesh_Gown", new[]
            {
                new Vector2(0f, 0.03f), new Vector2(0.5f, 0.03f), new Vector2(0.47f, 0.18f), new Vector2(0.38f, 0.45f),
                new Vector2(0.26f, 0.75f), new Vector2(0.17f, 0.95f), new Vector2(0.138f, 1.03f), new Vector2(0.155f, 1.12f),
                new Vector2(0.178f, 1.2f), new Vector2(0.17f, 1.29f), new Vector2(0.158f, 1.36f), new Vector2(0.1f, 1.4f), new Vector2(0f, 1.4f),
            }, 1f, 0.75f);

            k.Coat = LatheAsset("Mesh_Coat", new[]
            {
                new Vector2(0f, 0.45f), new Vector2(0.27f, 0.45f), new Vector2(0.25f, 0.7f), new Vector2(0.2f, 0.95f),
                new Vector2(0.165f, 1.03f), new Vector2(0.18f, 1.15f), new Vector2(0.19f, 1.26f), new Vector2(0.185f, 1.36f),
                new Vector2(0.12f, 1.42f), new Vector2(0.075f, 1.47f), new Vector2(0f, 1.47f),
            }, 1f, 0.78f);

            // 挂衣：原点在衣架挂钩处（顶部），往下长
            k.HangDress = LatheAsset("Mesh_HangDress", new[]
            {
                new Vector2(0f, -1.25f), new Vector2(0.34f, -1.25f), new Vector2(0.3f, -1.0f), new Vector2(0.21f, -0.62f),
                new Vector2(0.15f, -0.45f), new Vector2(0.17f, -0.3f), new Vector2(0.2f, -0.14f), new Vector2(0.17f, -0.05f), new Vector2(0f, -0.03f),
            }, 1f, 0.32f);

            k.HangCoat = LatheAsset("Mesh_HangCoat", new[]
            {
                new Vector2(0f, -1.05f), new Vector2(0.25f, -1.05f), new Vector2(0.24f, -0.6f), new Vector2(0.2f, -0.42f),
                new Vector2(0.21f, -0.28f), new Vector2(0.225f, -0.12f), new Vector2(0.2f, -0.05f), new Vector2(0.06f, -0.01f), new Vector2(0f, -0.01f),
            }, 1f, 0.38f);

            // 童装：和成人人台同一套坐标（整体缩到 0.62 倍用），蓬裙及膝
            k.ChildDress = LatheAsset("Mesh_ChildDress", new[]
            {
                new Vector2(0f, 0.45f), new Vector2(0.4f, 0.45f), new Vector2(0.37f, 0.58f), new Vector2(0.28f, 0.78f),
                new Vector2(0.17f, 0.95f), new Vector2(0.138f, 1.03f), new Vector2(0.158f, 1.14f), new Vector2(0.178f, 1.22f),
                new Vector2(0.17f, 1.3f), new Vector2(0.15f, 1.37f), new Vector2(0.1f, 1.41f), new Vector2(0f, 1.41f),
            }, 1f, 0.8f);

            k.Basin = LatheAsset("Mesh_Basin", new[]
            {
                new Vector2(0f, -0.12f), new Vector2(0.1f, -0.12f), new Vector2(0.17f, -0.06f), new Vector2(0.2f, 0f),
                new Vector2(0.22f, 0f), new Vector2(0.22f, 0.02f), new Vector2(0.18f, 0.02f), new Vector2(0.15f, -0.04f), new Vector2(0f, -0.07f),
            }, 1f, 0.75f);

            k.Linen = Lit("M_Mannequin_Linen", Finishes.Canvas.mainTexture as Texture2D, null, new Color(0.62f, 0.57f, 0.48f), 0.1f, 0.5f, 0f, false);
            k.Head = Lit("M_Mannequin_Head", null, null, new Color(0.78f, 0.75f, 0.69f), 0.35f, 1f, 0f, false);
            k.Porcelain = Lit("M_Porcelain", null, null, new Color(0.86f, 0.86f, 0.83f), 0.85f, 1f, 0f, false);
            k.Chrome = Lit("M_Chrome", null, null, new Color(0.75f, 0.75f, 0.76f), 0.85f, 1f, 1f, false);
            k.Partition = Lit("M_Toilet_Partition", null, null, new Color(0.30f, 0.33f, 0.31f), 0.45f, 1f, 0f, false);
            k.StallDoor = Lit("M_Toilet_StallDoor", null, null, new Color(0.25f, 0.28f, 0.27f), 0.45f, 1f, 0f, false);
            k.Paper = Lit("M_Paper_Old", null, null, new Color(0.78f, 0.73f, 0.60f), 0.05f, 1f, 0f, false);
            k.Book = Lit("M_Book_Leather", null, null, new Color(0.25f, 0.10f, 0.07f), 0.3f, 1f, 0f, false);
            k.Pink = Lit("M_Fabric_LilyPink", null, null, new Color(0.85f, 0.55f, 0.62f), 0.25f, 1f, 0f, false);
            k.PinkGlow = Emissive("M_Light_Pink", new Color(1f, 0.55f, 0.72f), 2.5f);
            k.Stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/M_Stone_Angel.mat") ?? Finishes.StoneWorn;

            // 雕像：披长袍的立像、跪像（下半身 + 前倾的上半身）
            k.Robe = LatheAsset("Mesh_Statue_Robe", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.34f, 0f), new Vector2(0.32f, 0.15f), new Vector2(0.25f, 0.6f),
                new Vector2(0.18f, 0.95f), new Vector2(0.19f, 1.15f), new Vector2(0.21f, 1.32f), new Vector2(0.2f, 1.42f),
                new Vector2(0.11f, 1.48f), new Vector2(0.06f, 1.52f), new Vector2(0f, 1.53f),
            }, 1f, 0.8f);
            k.KneelBase = LatheAsset("Mesh_Statue_KneelBase", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.4f, 0f), new Vector2(0.38f, 0.18f), new Vector2(0.3f, 0.4f),
                new Vector2(0.22f, 0.55f), new Vector2(0f, 0.6f),
            }, 1f, 1.25f);
            k.KneelTorso = LatheAsset("Mesh_Statue_KneelTorso", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.2f, 0f), new Vector2(0.18f, 0.2f), new Vector2(0.2f, 0.38f),
                new Vector2(0.19f, 0.5f), new Vector2(0.1f, 0.57f), new Vector2(0.05f, 0.6f), new Vector2(0f, 0.61f),
            }, 1f, 0.8f);

            // 年代感、压暗的布料色：酒红、墨蓝、黑、象牙、橄榄、芥末、灰紫、深棕
            Color[] fabric =
            {
                new Color(0.36f, 0.06f, 0.08f), new Color(0.10f, 0.13f, 0.25f), new Color(0.06f, 0.06f, 0.07f),
                new Color(0.70f, 0.66f, 0.56f), new Color(0.26f, 0.30f, 0.16f), new Color(0.55f, 0.43f, 0.15f),
                new Color(0.32f, 0.26f, 0.34f), new Color(0.28f, 0.18f, 0.11f),
            };
            k.Fabrics = new Material[fabric.Length];
            for (int i = 0; i < fabric.Length; i++)
            {
                k.Fabrics[i] = Lit("M_Fabric_" + i, null, null, fabric[i], 0.2f, 1f, 0f, false);
            }

            return k;
        }

        // ================================================================ 车床网格

        /// <summary>轮廓线 (半径, 高度) 绕 Y 轴旋转一圈；sx / sz 把截面压成椭圆。首尾半径为 0 的点自然封口。</summary>
        private static Mesh LatheAsset(string name, Vector2[] profile, float sx, float sz, int segments = 28)
        {
            string path = GeneratedDir + "/" + name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                return existing;
            }

            int rings = profile.Length;
            int cols = segments + 1;
            Vector3[] verts = new Vector3[rings * cols];
            Vector2[] uv = new Vector2[verts.Length];
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < cols; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    verts[r * cols + s] = new Vector3(Mathf.Cos(a) * profile[r].x * sx, profile[r].y, Mathf.Sin(a) * profile[r].x * sz);
                    uv[r * cols + s] = new Vector2(s / (float)segments * 2f, profile[r].y * 2f);
                }
            }

            List<int> tris = new List<int>();
            for (int r = 0; r < rings - 1; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = r * cols + s;
                    int b = a + cols;
                    // 轮廓从下往上时法线朝外；轮廓从上往下（挂衣）时翻过来
                    if (profile[r + 1].y >= profile[r].y)
                    {
                        tris.Add(a); tris.Add(b); tris.Add(a + 1);
                        tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
                    }
                    else
                    {
                        tris.Add(a); tris.Add(a + 1); tris.Add(b);
                        tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
                    }
                }
            }

            Mesh mesh = new Mesh { name = name, vertices = verts, uv = uv, triangles = tris.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos, Vector3 localScale, bool shadows = true)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        private static GameObject LocalCylinder(Transform parent, string name, Vector3 localCenter, float diameter, float height, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            RemoveCollider(go);
            return go;
        }

        private static GameObject LocalSphere(Transform parent, string name, Vector3 localCenter, Vector3 size, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            RemoveCollider(go);
            return go;
        }

        private static GameObject LocalBox(Transform parent, string name, Vector3 localCenter, Vector3 size, Material mat)
        {
            GameObject go = AddBox(parent, name, localCenter, size, mat);
            RemoveCollider(go);
            return go;
        }

        // ================================================================ 人台

        private enum Outfit
        {
            None,
            Gown,
            Coat,
            Child,
        }

        /// <summary>
        /// 人台：圆盘底座 + 立杆 + 布面躯干 + 细脖子 + 蛋形无脸头。origin 在底座中心，yaw = 正面朝向（+Z 为正面）。
        /// </summary>
        private static Transform Mannequin(Transform parent, string name, Vector3 origin, float yaw, float scale, Outfit outfit, Material fabric)
        {
            Transform g = NewGroup(parent, name, origin, yaw);
            g.localScale = Vector3.one * scale;
            ModelKit k = Kit;

            LocalCylinder(g, "Base", new Vector3(0f, 0.015f, 0f), 0.42f, 0.03f, Finishes.MetalDark);
            LocalCylinder(g, "Pole", new Vector3(0f, 0.4f, 0f), 0.035f, 0.76f, Finishes.Brass);
            MeshObject(g, "Torso", k.DressForm, k.Linen, Vector3.zero, Vector3.one);
            LocalCylinder(g, "Neck", new Vector3(0f, 1.52f, 0f), 0.07f, 0.1f, k.Head);
            LocalSphere(g, "Head", new Vector3(0f, 1.66f, 0.01f), new Vector3(0.17f, 0.24f, 0.19f), k.Head);

            switch (outfit)
            {
                case Outfit.Gown:
                    MeshObject(g, "Gown", k.Gown, fabric, Vector3.zero, new Vector3(1.04f, 1f, 1.04f));
                    break;
                case Outfit.Coat:
                    MeshObject(g, "Coat", k.Coat, fabric, Vector3.zero, new Vector3(1.05f, 1f, 1.05f));
                    // 袖子：从肩头斜垂下来
                    for (int side = -1; side <= 1; side += 2)
                    {
                        GameObject sleeve = LocalCylinder(g, side < 0 ? "Sleeve_L" : "Sleeve_R",
                            new Vector3(side * 0.21f, 1.12f, 0f), 0.09f, 0.52f, fabric);
                        sleeve.transform.localRotation = Quaternion.Euler(0f, 0f, side * 8f);
                    }

                    break;
                case Outfit.Child:
                    MeshObject(g, "Dress", k.ChildDress, fabric, Vector3.zero, Vector3.one);
                    break;
            }

            return g;
        }

        // ================================================================ 服装厅

        /// <summary>
        /// 三面墙的挂衣（GDD：上墙 4、右墙 8、下墙 4，左侧入口墙留空）：墙上一根黄铜挂杆 + 托架，
        /// 每件衣服挂在衣架上（裙子 / 大衣交替，颜色轮换），每件带一个薄碰撞盒，人走不进衣服里。
        /// </summary>
        private static void BuildGarmentRails(Transform parent, float floorY)
        {
            Transform root = NewGroup(parent, "Garments");
            const float railY = 2.25f;
            const float off = 0.38f;
            float[] rowX = { 29.4f, 32.2f, 35.0f, 37.8f };
            float[] eastZ = { 24.07f, 21.13f, 18.2f, 15.27f, 12.33f, 9.4f, 6.47f, 3.53f };

            float northZ = BuildingDepth - HalfWall - off;
            float southZ = HalfWall + off;
            float eastX = BuildingWidth - HalfWall - off;

            Rail(root, "Rail_N", new Vector3(28.6f, floorY + railY, northZ), new Vector3(38.6f, floorY + railY, northZ), Vector3.back);
            Rail(root, "Rail_S", new Vector3(28.6f, floorY + railY, southZ), new Vector3(38.6f, floorY + railY, southZ), Vector3.forward);
            Rail(root, "Rail_E", new Vector3(eastX, floorY + railY, 2.8f), new Vector3(eastX, floorY + railY, 24.8f), Vector3.left);

            int n = 0;
            for (int i = 0; i < rowX.Length; i++)
            {
                HangingGarment(root, "Garment_N" + (i + 1), new Vector3(rowX[i], floorY + railY, northZ), 180f, n++);
                HangingGarment(root, "Garment_S" + (i + 1), new Vector3(rowX[i], floorY + railY, southZ), 0f, n++);
            }

            for (int i = 0; i < eastZ.Length; i++)
            {
                HangingGarment(root, "Garment_E" + (i + 1), new Vector3(eastX, floorY + railY, eastZ[i]), -90f, n++);
            }
        }

        private static void Rail(Transform parent, string name, Vector3 a, Vector3 b, Vector3 normal)
        {
            Transform g = NewGroup(parent, name);
            Vector3 mid = (a + b) * 0.5f;
            GameObject rod = Cylinder(g, "Rod", mid, 0.035f, Vector3.Distance(a, b), Finishes.Brass, false);
            rod.transform.rotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);

            // 托架：每 2.5 m 一个，从墙伸出来
            int count = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(a, b) / 2.5f) + 1);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)(count - 1));
                Vector3 wall = p - normal * 0.38f;
                Vector3 c = (p + wall) * 0.5f;
                Vector3 size = Abs(normal) * 0.38f + new Vector3(0.03f, 0.03f, 0.03f) - Abs(normal) * 0.03f;
                RemoveCollider(AddBox(g, "Bracket_" + i, c, size, Finishes.Brass));
            }
        }

        private static void HangingGarment(Transform parent, string name, Vector3 hook, float yaw, int index)
        {
            ModelKit k = Kit;
            Transform g = NewGroup(parent, name, hook, yaw);
            Material fabric = k.Fabrics[index % k.Fabrics.Length];
            bool coat = index % 3 == 1;

            // 衣架：挂钩 + 横杆
            LocalBox(g, "Hook", new Vector3(0f, 0.04f, 0f), new Vector3(0.012f, 0.08f, 0.012f), Finishes.MetalDark);
            LocalBox(g, "HangerBar", new Vector3(0f, -0.02f, 0f), new Vector3(0.44f, 0.02f, 0.02f), Finishes.CrateWood);

            MeshObject(g, "Body", coat ? k.HangCoat : k.HangDress, fabric, new Vector3(0f, -0.02f, 0f), Vector3.one);
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject sleeve = LocalCylinder(g, side < 0 ? "Sleeve_L" : "Sleeve_R",
                    new Vector3(side * 0.22f, -0.36f, 0f), 0.085f, coat ? 0.58f : 0.42f, fabric);
                sleeve.transform.localRotation = Quaternion.Euler(0f, 0f, side * 10f);
                sleeve.transform.localScale = new Vector3(0.085f, (coat ? 0.58f : 0.42f) * 0.5f, 0.06f);
            }

            if (coat)
            {
                // 翻领
                LocalBox(g, "Collar", new Vector3(0f, -0.09f, 0.075f), new Vector3(0.2f, 0.08f, 0.02f), fabric);
            }

            g.gameObject.AddComponent<Ib.Environment.GarmentSway>();

            BoxCollider box = g.gameObject.AddComponent<BoxCollider>();
            float length = coat ? 1.05f : 1.25f;
            box.center = new Vector3(0f, -length * 0.5f, 0f);
            box.size = new Vector3(0.6f, length, 0.25f);
        }

        /// <summary>玻璃罩人台（每组 3 个，呈三角站位朝外）：深色木底座 + 透明玻璃罩 + 黄铜棱线。</summary>
        private static void MannequinCase(Transform parent, string name, float x, float y, float z, int colorSeed)
        {
            const float size = 2.3f;
            const float plinthH = 0.28f;
            Transform g = GlassCase(parent, name, new Vector3(x, y, z), new Vector3(size, 2.4f, size), plinthH);

            Vector3[] spots = { new Vector3(-0.55f, 0f, 0.45f), new Vector3(0.55f, 0f, 0.45f), new Vector3(0f, 0f, -0.5f) };
            float[] yaws = { -20f, 20f, 0f };
            for (int i = 0; i < 3; i++)
            {
                Outfit o = (i + colorSeed) % 3 == 2 ? Outfit.Coat : Outfit.Gown;
                Material m = Kit.Fabrics[(colorSeed * 3 + i) % Kit.Fabrics.Length];
                Mannequin(g, "Mannequin_" + (i + 1), new Vector3(spots[i].x, plinthH, spots[i].z), yaws[i], 1f, o, m);
            }
        }

        /// <summary>Lily 的小裙子：东南角独立玻璃柜，童装人台（全场唯一的粉色光由 BuildLighting 的粉灯负责）。</summary>
        private static void LilyCase(Transform parent, float x, float y, float z)
        {
            Transform g = GlassCase(parent, "LilyDress_Case", new Vector3(x, y, z), new Vector3(1.4f, 1.8f, 1.4f), 0.3f);
            Mannequin(g, "ChildForm", new Vector3(0f, 0.3f, 0f), -135f, 0.62f, Outfit.Child, Kit.Pink);
            // 柜顶内侧的粉色灯片 —— 粉灯（BuildLighting 的 Light_LilyDress_Pink）就挂在它下面
            GameObject puck = LocalCylinder(g, "PinkLamp", new Vector3(0f, 1.765f, 0f), 0.34f, 0.025f, Kit.PinkGlow);
            puck.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            // 展签 T10
            RemoveCollider(AddBox(g, "Label_T10", new Vector3(0f, 0.18f, -0.705f), new Vector3(0.3f, 0.12f, 0.01f), Finishes.Label));
        }

        /// <summary>
        /// 通用玻璃柜：底座（有碰撞）+ 玻璃罩（Ignore Raycast 层：挡人走、不挡视线射线，不投影）+ 12 根棱线。
        /// 返回柜子根节点（局部原点在底面中心）。
        /// </summary>
        private static Transform GlassCase(Transform parent, string name, Vector3 baseCenter, Vector3 size, float plinthH)
        {
            Transform g = NewGroup(parent, name, baseCenter, 0f);
            AddBox(g, "Plinth", new Vector3(0f, plinthH * 0.5f, 0f), new Vector3(size.x, plinthH, size.z), Finishes.Wainscot);
            LocalBox(g, "Plinth_Trim", new Vector3(0f, plinthH - 0.02f, 0f), new Vector3(size.x + 0.03f, 0.04f, size.z + 0.03f), Finishes.Brass);

            float gh = size.y - plinthH;
            GameObject glass = AddBox(g, "Glass", new Vector3(0f, plinthH + gh * 0.5f, 0f), new Vector3(size.x - 0.02f, gh, size.z - 0.02f), Finishes.GlassClear);
            glass.layer = IgnoreRaycastLayer;
            glass.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            const float e = 0.025f;
            float hx = size.x * 0.5f;
            float hz = size.z * 0.5f;
            float y0 = plinthH;
            float y1 = size.y;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    LocalBox(g, "Edge_V", new Vector3(sx * hx, (y0 + y1) * 0.5f, sz * hz), new Vector3(e, y1 - y0, e), Finishes.Brass);
                }

                LocalBox(g, "Edge_TopX", new Vector3(0f, y1, sx * hz), new Vector3(size.x, e, e), Finishes.Brass);
                LocalBox(g, "Edge_TopZ", new Vector3(sx * hx, y1, 0f), new Vector3(e, e, size.z), Finishes.Brass);
            }

            return g;
        }

        // ================================================================ 二层长廊展柜

        /// <summary>
        /// 长廊展柜（GDD 的 5 组掩体）：1.0 m 高的深色木柜身 + 0.4 m 玻璃罩，里面摆旧信件、速写本、钢笔。
        /// 整体放大 WorldScale 后柜身约 1.4 m，比 1.3 m 的孩子高 —— 不用蹲也能藏住（游戏里没有蹲）。
        /// 玻璃罩在 Ignore Raycast 层，不挡视线判定。
        /// </summary>
        private static void GalleryVitrine(Transform parent, string name, float x, float y, float z, int seed)
        {
            Transform g = NewGroup(parent, name, new Vector3(x, y, z), 0f);
            Vector3 size = new Vector3(2.9f, 1.4f, 0.95f);
            const float bodyH = 1.0f;

            AddBox(g, "Body", new Vector3(0f, bodyH * 0.5f, 0f), new Vector3(size.x, bodyH, size.z), Finishes.Wainscot);
            LocalBox(g, "Kick", new Vector3(0f, 0.05f, 0f), new Vector3(size.x + 0.02f, 0.1f, size.z + 0.02f), Finishes.Trim);
            LocalBox(g, "Top_Trim", new Vector3(0f, bodyH - 0.015f, 0f), new Vector3(size.x + 0.04f, 0.03f, size.z + 0.04f), Finishes.Brass);
            // 柜身正反两面各三块凸起的镶板
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    LocalBox(g, "Panel", new Vector3((i - 1) * 0.92f, 0.56f, side * (size.z * 0.5f + 0.008f)), new Vector3(0.78f, 0.62f, 0.016f), Finishes.Trim);
                }
            }

            GameObject glass = AddBox(g, "Glass", new Vector3(0f, bodyH + 0.2f, 0f), new Vector3(size.x - 0.04f, 0.4f, size.z - 0.04f), Finishes.GlassClear);
            glass.layer = IgnoreRaycastLayer;
            glass.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    LocalBox(g, "Edge_V", new Vector3(sx * (size.x * 0.5f - 0.02f), bodyH + 0.2f, sz * (size.z * 0.5f - 0.02f)), new Vector3(0.02f, 0.4f, 0.02f), Finishes.Brass);
                }
            }

            LocalBox(g, "Edge_Top", new Vector3(0f, size.y - 0.01f, 0f), new Vector3(size.x - 0.04f, 0.02f, 0.02f), Finishes.Brass);

            // 展品：深色丝绒垫 + 几张发黄的信纸 + 一本速写本 + 一支钢笔
            LocalBox(g, "Velvet", new Vector3(0f, bodyH + 0.006f, 0f), new Vector3(size.x - 0.12f, 0.012f, size.z - 0.12f), Finishes.Velvet);
            for (int i = 0; i < 3; i++)
            {
                GameObject paper = LocalBox(g, "Letter_" + i, new Vector3(-0.9f + i * 0.35f + seed * 0.05f, bodyH + 0.014f + i * 0.002f, -0.05f + (i % 2) * 0.1f),
                    new Vector3(0.21f, 0.003f, 0.29f), Kit.Paper);
                paper.transform.localRotation = Quaternion.Euler(0f, -12f + i * 11f + seed * 7f, 0f);
            }

            GameObject book = LocalBox(g, "Sketchbook", new Vector3(0.55f, bodyH + 0.03f, 0.05f), new Vector3(0.32f, 0.04f, 0.24f), Kit.Book);
            book.transform.localRotation = Quaternion.Euler(0f, 8f - seed * 5f, 0f);
            GameObject pen = LocalCylinder(g, "Pen", new Vector3(1.05f, bodyH + 0.02f, -0.1f), 0.012f, 0.14f, Finishes.MetalDark);
            pen.transform.localRotation = Quaternion.Euler(90f, 30f + seed * 10f, 0f);
        }

        // ================================================================ 接待厅前台

        /// <summary>
        /// 前台：深色木柜身（访客那面三块凸镶板）+ 石材台面（往外挑出）+ 踢脚 + 黄铜压条；
        /// 员工那面是抽屉（任务 C 的 C_FrontDesk_Drawer 保留原名）；台面上电话、摊开的登记簿。
        /// </summary>
        private static void BuildFrontDesk(Transform parent, float x, float y, float z)
        {
            Transform g = NewGroup(parent, "FrontDesk", new Vector3(x, y, z), 0f);
            const float w = 4.8f;
            const float d = 0.8f;
            const float h = 1.0f;

            AddBox(g, "Body", new Vector3(0f, (h - 0.05f) * 0.5f, 0f), new Vector3(w, h - 0.05f, d), Finishes.Wainscot);
            LocalBox(g, "Top", new Vector3(0f, h - 0.025f, -0.04f), new Vector3(w + 0.12f, 0.05f, d + 0.12f), Finishes.StoneWorn);
            LocalBox(g, "Kick", new Vector3(0f, 0.06f, 0f), new Vector3(w + 0.02f, 0.12f, d + 0.02f), Finishes.Trim);
            LocalBox(g, "BrassLine", new Vector3(0f, h - 0.09f, -d * 0.5f - 0.006f), new Vector3(w, 0.025f, 0.012f), Finishes.Brass);
            for (int i = 0; i < 5; i++)
            {
                LocalBox(g, "Panel_" + i, new Vector3((i - 2) * 0.93f, 0.48f, -d * 0.5f - 0.01f), new Vector3(0.78f, 0.58f, 0.02f), Finishes.Trim);
            }

            // 员工侧：抽屉（任务道具）+ 两侧柜门
            RemoveCollider(AddBox(g, "C_FrontDesk_Drawer", new Vector3(0f, 0.73f, d * 0.5f + 0.03f), new Vector3(0.7f, 0.22f, 0.06f), Finishes.Trim));
            LocalBox(g, "Drawer_Handle", new Vector3(0f, 0.73f, d * 0.5f + 0.07f), new Vector3(0.16f, 0.025f, 0.025f), Finishes.Brass);
            for (int side = -1; side <= 1; side += 2)
            {
                LocalBox(g, "CabinetDoor", new Vector3(side * 1.4f, 0.45f, d * 0.5f + 0.008f), new Vector3(0.9f, 0.62f, 0.016f), Finishes.Trim);
            }

            // 电话：底座 + 话筒
            Transform phone = NewGroup(g, "Phone", new Vector3(-1.3f, h, 0f), 15f);
            LocalBox(phone, "Base", new Vector3(0f, 0.04f, 0f), new Vector3(0.22f, 0.08f, 0.18f), Finishes.MetalDark);
            LocalBox(phone, "Handset", new Vector3(0f, 0.1f, 0f), new Vector3(0.26f, 0.04f, 0.06f), Finishes.MetalDark);
            LocalBox(phone, "Dial", new Vector3(0f, 0.081f, -0.05f), new Vector3(0.08f, 0.004f, 0.06f), Finishes.Label);

            // 登记簿：摊开的两页 + 皮面
            Transform book = NewGroup(g, "Register_Book", new Vector3(1.2f, h, 0f), -8f);
            LocalBox(book, "Cover", new Vector3(0f, 0.008f, 0f), new Vector3(0.46f, 0.016f, 0.32f), Kit.Book);
            GameObject left = LocalBox(book, "Page_L", new Vector3(-0.11f, 0.024f, 0f), new Vector3(0.21f, 0.012f, 0.29f), Kit.Paper);
            left.transform.localRotation = Quaternion.Euler(0f, 0f, -3f);
            GameObject right = LocalBox(book, "Page_R", new Vector3(0.11f, 0.024f, 0f), new Vector3(0.21f, 0.012f, 0.29f), Kit.Paper);
            right.transform.localRotation = Quaternion.Euler(0f, 0f, 3f);
            GameObject pen = LocalCylinder(book, "Pen", new Vector3(0.28f, 0.03f, 0.05f), 0.012f, 0.14f, Finishes.MetalDark);
            pen.transform.localRotation = Quaternion.Euler(90f, 20f, 0f);
        }

        // ================================================================ 厕所

        /// <summary>
        /// 厕所（玩家不进隔间，只要看着合理）：东墙一排 3 个隔间（2.0 m 高隔板、离地 15 cm、门关着带把手和红绿指示），
        /// 整排用一个实心碰撞盒封住；北墙镜子下面洗手台 3 个盆；南墙两个小便斗；烘手机、纸巾盒、垃圾桶、地漏、
        /// 拖把桶 + "暂停使用"牌。手电（南墙架子上）从门口能直接看到，不挡。
        /// </summary>
        private static void BuildToiletFixtures(Transform parent, float y)
        {
            ModelKit k = Kit;
            Transform g = NewGroup(parent, "Fixtures");
            float x0 = 30.85f;                          // 隔间正面
            float x1 = ToiletEastX - HalfWall;          // 东墙面
            float z0 = 1.15f;
            float z1 = ToiletNorthZ - HalfWall;
            const float gap = 0.15f;
            const float top = 2.0f;
            float stallW = (z1 - z0) / 3f;

            Transform stalls = NewGroup(g, "Stalls");
            for (int i = 0; i < 3; i++)
            {
                float a = z0 + i * stallW;
                float b = a + stallW;
                // 正面：门两边的窄板 + 门
                LocalBox(stalls, "Front_A_" + i, new Vector3(x0, y + (gap + top) * 0.5f, a + 0.09f), new Vector3(0.04f, top - gap, 0.18f), k.Partition);
                LocalBox(stalls, "Front_B_" + i, new Vector3(x0, y + (gap + top) * 0.5f, b - 0.09f), new Vector3(0.04f, top - gap, 0.18f), k.Partition);
                float dz = (a + b) * 0.5f;
                float dw = stallW - 0.4f;
                LocalBox(stalls, "Door_" + i, new Vector3(x0 - 0.01f, y + (gap + top - 0.05f) * 0.5f, dz), new Vector3(0.035f, top - gap - 0.05f, dw), k.StallDoor);
                LocalBox(stalls, "Handle_" + i, new Vector3(x0 - 0.05f, y + 1.0f, dz + dw * 0.35f), new Vector3(0.03f, 0.03f, 0.1f), k.Chrome);
                Material indicator = i == 1 ? Finishes.RedLed : Finishes.ExitSign;   // 中间那间"有人"
                LocalBox(stalls, "Indicator_" + i, new Vector3(x0 - 0.035f, y + 1.15f, dz + dw * 0.35f), new Vector3(0.01f, 0.025f, 0.05f), indicator);
                // 隔间之间的隔板
                if (i > 0)
                {
                    LocalBox(stalls, "Divider_" + i, new Vector3((x0 + x1) * 0.5f, y + (gap + top) * 0.5f, a), new Vector3(x1 - x0, top - gap, 0.03f), k.Partition);
                }
            }

            LocalBox(stalls, "EndPanel", new Vector3((x0 + x1) * 0.5f, y + (gap + top) * 0.5f, z0), new Vector3(x1 - x0, top - gap, 0.04f), k.Partition);
            LocalBox(stalls, "TopRail", new Vector3(x0, y + top + 0.02f, (z0 + z1) * 0.5f), new Vector3(0.05f, 0.04f, z1 - z0), k.Chrome);
            // 第三间门上贴"暂停使用"
            LocalBox(stalls, "Sign_OutOfOrder", new Vector3(x0 - 0.03f, y + 1.5f, z0 + stallW * 2.5f), new Vector3(0.005f, 0.2f, 0.28f), Finishes.WarningYellow);

            // 整排隔间一个实心碰撞盒（玩家进不去，也不需要进）
            GameObject seal = new GameObject("Stalls_Collider");
            seal.transform.SetParent(stalls, false);
            BoxCollider sb = seal.AddComponent<BoxCollider>();
            sb.center = new Vector3((x0 - 0.03f + x1) * 0.5f, y + top * 0.5f, (z0 + z1) * 0.5f);
            sb.size = new Vector3(x1 - x0 + 0.03f, top, z1 - z0);

            // 洗手台：镜子（北墙）下面，台面 0.8 m
            Transform vanity = NewGroup(g, "Vanity");
            float vz = ToiletNorthZ - HalfWall - 0.28f;
            AddBox(vanity, "Cabinet", new Vector3(29.0f, y + 0.37f, vz), new Vector3(3.2f, 0.74f, 0.5f), k.Partition);
            LocalBox(vanity, "Counter", new Vector3(29.0f, y + 0.77f, vz - 0.02f), new Vector3(3.3f, 0.06f, 0.58f), Finishes.StoneWorn);
            for (int i = 0; i < 3; i++)
            {
                float bx = 28.0f + i * 1.0f;
                MeshObject(vanity, "Basin_" + i, k.Basin, k.Porcelain, new Vector3(bx, y + 0.8f, vz - 0.04f), Vector3.one, false);
                LocalBox(vanity, "Tap_" + i, new Vector3(bx, y + 0.88f, vz + 0.17f), new Vector3(0.03f, 0.12f, 0.03f), k.Chrome);
                LocalBox(vanity, "Spout_" + i, new Vector3(bx, y + 0.93f, vz + 0.12f), new Vector3(0.025f, 0.025f, 0.11f), k.Chrome);
            }

            // 南墙小便斗 ×2（门口东侧，避开手电架子）
            for (int i = 0; i < 2; i++)
            {
                float ux = 27.4f + i * 1.1f;
                Transform u = NewGroup(g, "Urinal_" + i, new Vector3(ux, y, HalfWall), 0f);
                AddBox(u, "Bowl", new Vector3(0f, 0.75f, 0.17f), new Vector3(0.42f, 0.62f, 0.32f), k.Porcelain);
                LocalBox(u, "Lip", new Vector3(0f, 0.52f, 0.3f), new Vector3(0.36f, 0.1f, 0.08f), k.Porcelain);
                LocalBox(u, "Pipe", new Vector3(0f, 1.25f, 0.04f), new Vector3(0.04f, 0.38f, 0.04f), k.Chrome);
                if (i == 0)
                {
                    LocalBox(u, "Divider", new Vector3(0.55f, 0.95f, 0.25f), new Vector3(0.03f, 0.9f, 0.45f), k.Partition);
                }
            }

            // 西墙：烘手机 + 纸巾盒 + 垃圾桶
            float wx = EastLineX + HalfWall;
            RemoveCollider(AddBox(g, "HandDryer", new Vector3(wx + 0.12f, y + 1.15f, 5.4f), new Vector3(0.22f, 0.3f, 0.28f), k.Chrome));
            RemoveCollider(AddBox(g, "TowelDispenser", new Vector3(wx + 0.07f, y + 1.25f, 6.1f), new Vector3(0.12f, 0.38f, 0.3f), Finishes.Plastic));
            Cylinder(g, "Bin", new Vector3(wx + 0.25f, y + 0.25f, 6.15f), 0.32f, 0.5f, Finishes.MetalDark, true);

            // 地漏 + 拖把桶 + 小心地滑
            RemoveCollider(AddBox(g, "FloorDrain", new Vector3(28.6f, y + 0.008f, 3.6f), new Vector3(0.18f, 0.004f, 0.18f), Finishes.MetalDark));
            Cylinder(g, "MopBucket", new Vector3(30.3f, y + 0.17f, 1.6f), 0.36f, 0.32f, Finishes.WarningYellow, true);
            GameObject mop = Cylinder(g, "Mop", new Vector3(30.32f, y + 0.85f, 1.65f), 0.03f, 1.3f, Finishes.CrateWood, false);
            mop.transform.rotation = Quaternion.Euler(8f, 0f, -10f);
            WetFloorSign(g, 29.7f, y, 2.4f, -60f);

            // 天花通风口
            float ceil = RoofY;
            RemoveCollider(AddBox(g, "CeilingVent", new Vector3(29.5f, ceil - 0.01f, 3.5f), new Vector3(0.6f, 0.02f, 0.6f), Finishes.MetalDark));
        }

        // ================================================================ 雕塑厅角落雕像

        /// <summary>
        /// 《提灯人》：披长袍的立像，一只手举着一盏灯（灯里有极暗的暖光，像还亮着）。
        /// 名为 Body 的网格保留下来，射灯（CollectPanelExhibits）按它找位置。
        /// </summary>
        private static void LanternBearer(Transform parent, Vector3 basePos, float yaw)
        {
            ModelKit k = Kit;
            Transform g = NewGroup(parent, "Statue", basePos, yaw);
            MeshObject(g, "Body", k.Robe, k.Stone, Vector3.zero, Vector3.one);
            LocalSphere(g, "Head", new Vector3(0f, 1.66f, 0.02f), new Vector3(0.2f, 0.25f, 0.22f), k.Stone);
            LocalSphere(g, "Hood", new Vector3(0f, 1.69f, -0.03f), new Vector3(0.26f, 0.28f, 0.27f), k.Stone);
            GameObject arm = LocalCylinder(g, "Arm_Raised", new Vector3(0.2f, 1.55f, 0.16f), 0.1f, 0.62f, k.Stone);
            arm.transform.localRotation = Quaternion.Euler(35f, 0f, -20f);   // 往前上方举，手在 (0.31, 1.79, 0.33)
            GameObject arm2 = LocalCylinder(g, "Arm_Down", new Vector3(-0.2f, 1.12f, 0.05f), 0.1f, 0.58f, k.Stone);
            arm2.transform.localRotation = Quaternion.Euler(10f, 0f, 8f);
            // 灯挂在手下面：提环在手里，灯身垂下来
            LocalBox(g, "Lantern_Ring", new Vector3(0.31f, 1.77f, 0.34f), new Vector3(0.05f, 0.07f, 0.02f), k.Stone);
            LocalBox(g, "Lantern", new Vector3(0.31f, 1.64f, 0.34f), new Vector3(0.14f, 0.18f, 0.14f), k.Stone);
            LocalBox(g, "Lantern_Glow", new Vector3(0.31f, 1.64f, 0.34f), new Vector3(0.1f, 0.12f, 0.15f), Finishes.LampShade);
        }

        /// <summary>《跪者》：跪在地上，上身前倾，头埋进手里。</summary>
        private static void KneelingFigure(Transform parent, Vector3 basePos, float yaw)
        {
            ModelKit k = Kit;
            Transform g = NewGroup(parent, "Statue", basePos, yaw);
            MeshObject(g, "Body", k.KneelBase, k.Stone, Vector3.zero, Vector3.one);
            GameObject torso = MeshObject(g, "Torso", k.KneelTorso, k.Stone, new Vector3(0f, 0.5f, -0.05f), Vector3.one);
            torso.transform.localRotation = Quaternion.Euler(32f, 0f, 0f);
            LocalSphere(g, "Head", new Vector3(0f, 0.98f, 0.33f), new Vector3(0.19f, 0.23f, 0.21f), k.Stone);
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject arm = LocalCylinder(g, side < 0 ? "Arm_L" : "Arm_R", new Vector3(side * 0.13f, 0.86f, 0.3f), 0.09f, 0.45f, k.Stone);
                arm.transform.localRotation = Quaternion.Euler(-60f, 0f, side * -12f);
            }
        }

        // ================================================================ 门前通道检查

        /// <summary>
        /// 每个门洞两侧留 2.5 m 宽、3 m 深的空区；有带碰撞体的布置物伸进来就在 Console 里警告（不自动挪）。
        /// </summary>
        private static void CheckDoorClearance(Transform museum)
        {
            List<WallPiece> walls = CollectWallPieces(museum);
            List<Collider> props = new List<Collider>();
            foreach (string root in new[] { "Props_Museum", "Artworks" })
            {
                GameObject go = GameObject.Find(root);
                if (go != null)
                {
                    props.AddRange(go.GetComponentsInChildren<Collider>());
                }
            }

            Physics.SyncTransforms();
            int warnings = 0;
            foreach (WallPiece w in walls)
            {
                if (!w.IsLintel)
                {
                    continue;
                }

                Bounds b = w.B;
                bool alongX = b.size.x >= b.size.z;
                bool second = b.min.y > Floor2Y - 0.5f;
                float floorY = second ? Floor2Y : Floor1Y;
                Vector3 c = b.center;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 n = alongX ? new Vector3(0f, 0f, side) : new Vector3(side, 0f, 0f);
                    float face = alongX ? (side < 0 ? b.min.z : b.max.z) : (side < 0 ? b.min.x : b.max.x);
                    Vector3 center = alongX ? new Vector3(c.x, floorY + 1f, face + side * 1.5f) : new Vector3(face + side * 1.5f, floorY + 1f, c.z);
                    Vector3 size = alongX ? new Vector3(2.5f, 1.6f, 3f) : new Vector3(3f, 1.6f, 2.5f);
                    if (RoomAt(center.x, center.z, second) == null)
                    {
                        continue;
                    }

                    // 锁死的门（未开放区）不走人，不检查；配电间只有 5×3.2 m，保安柜就该在门里（GDD），也不算
                    if (second && !alongX && Mathf.Abs(c.z - LockedDoorZ) < 0.1f)
                    {
                        continue;
                    }

                    Bounds zone = new Bounds(center, size);
                    foreach (Collider col in props)
                    {
                        Vector3 cs = col.bounds.size;
                        bool flat = Mathf.Min(cs.x, cs.z) < 0.1f;   // 贴墙的画、展板不挡路
                        if (flat || col.name == "B_SecurityCabinet")
                        {
                            continue;
                        }

                        if (col.enabled && col.gameObject.activeInHierarchy && col.bounds.Intersects(zone) && !col.name.StartsWith("Door_"))
                        {
                            Debug.LogWarning("[GrayboxBuilder] 门前通道被占：" + w.Name + "（" + (n.x + n.z > 0 ? "+" : "−") + (alongX ? "Z" : "X") + " 侧）← " + col.transform.parent.name + "/" + col.name, col);
                            warnings++;
                        }
                    }
                }
            }

            if (warnings == 0)
            {
                Debug.Log("[GrayboxBuilder] 门前通道检查：全部畅通。");
            }
        }
    }
}
