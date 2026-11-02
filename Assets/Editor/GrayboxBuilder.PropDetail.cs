using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ib.EditorTools
{
    /// <summary>
    /// 细化过的小道具：灭火器、清洁车、"小心地滑"警示牌、二层长廊展柜。
    /// 外轮廓、碰撞体和摆放位置保持原来的尺寸（掩体、动线不变），只是把一块方块拆成真实物件的各个零件。
    /// 尺寸按 GDD 1 倍写，生成末尾整体乘 WorldScale。
    /// </summary>
    public static partial class GrayboxBuilder
    {
        private sealed class PropKit
        {
            public Mesh ExtinguisherBody;   // 灭火器瓶身：圆底 + 圆肩
            public Mesh Triangle;           // 单位正三角（警示标志）
            public Mesh SprayBottle;        // 喷壶瓶身
            public Material CartGrey;       // 清洁车框架（深灰塑料）
            public Material BagVinyl;       // 清洁车垃圾袋（黄色乙烯布）
            public Material Black;          // 黑色塑料 / 橡胶 / 印刷
            public Material White;          // 白色标签 / 文字
            public Material SprayBlue;
            public Material SprayGreen;
            public Material InkGlass;       // 墨水瓶
            public Material WaxRed;         // 火漆
        }

        private static PropKit propKit;

        private static PropKit Props
        {
            get
            {
                if (propKit == null)
                {
                    propKit = LoadPropKit();
                }

                return propKit;
            }
        }

        private static PropKit LoadPropKit()
        {
            EnsureFolder(GeneratedDir);
            PropKit k = new PropKit();

            k.ExtinguisherBody = LatheAsset("Mesh_Extinguisher_Body", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.07f, 0f), new Vector2(0.083f, 0.012f), new Vector2(0.085f, 0.03f),
                new Vector2(0.085f, 0.43f), new Vector2(0.081f, 0.462f), new Vector2(0.068f, 0.49f), new Vector2(0.045f, 0.508f),
                new Vector2(0.022f, 0.515f), new Vector2(0f, 0.516f),
            }, 1f, 1f, 24);

            k.SprayBottle = LatheAsset("Mesh_SprayBottle", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.034f, 0f), new Vector2(0.036f, 0.01f), new Vector2(0.036f, 0.12f),
                new Vector2(0.03f, 0.15f), new Vector2(0.016f, 0.17f), new Vector2(0.014f, 0.19f), new Vector2(0f, 0.19f),
            }, 1f, 0.7f, 16);

            k.Triangle = TriangleAsset("Mesh_WarningTriangle");

            k.CartGrey = Lit("M_Cart_Plastic", null, null, new Color(0.16f, 0.17f, 0.18f), 0.35f, 1f, 0f, false);
            k.BagVinyl = Lit("M_Cart_Bag", null, null, new Color(0.72f, 0.56f, 0.08f), 0.55f, 1f, 0f, false);
            k.Black = Lit("M_Plastic_Black", null, null, new Color(0.025f, 0.025f, 0.028f), 0.4f, 1f, 0f, false);
            k.White = Lit("M_Print_White", null, null, new Color(0.86f, 0.85f, 0.82f), 0.2f, 1f, 0f, false);
            k.SprayBlue = Lit("M_Spray_Blue", null, null, new Color(0.10f, 0.32f, 0.62f), 0.6f, 1f, 0f, false);
            k.SprayGreen = Lit("M_Spray_Green", null, null, new Color(0.18f, 0.48f, 0.22f), 0.6f, 1f, 0f, false);
            k.InkGlass = Lit("M_Ink_Glass", null, null, new Color(0.04f, 0.05f, 0.09f), 0.92f, 1f, 0f, false);
            k.WaxRed = Lit("M_Wax_Red", null, null, new Color(0.45f, 0.04f, 0.04f), 0.5f, 1f, 0f, false);
            return k;
        }

        /// <summary>单位正三角，边长 1，重心在原点，法线朝本地 +Z。</summary>
        private static Mesh TriangleAsset(string name)
        {
            string path = GeneratedDir + "/" + name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                return existing;
            }

            float h = Mathf.Sqrt(3f) * 0.5f;
            Mesh mesh = new Mesh
            {
                name = name,
                vertices = new[] { new Vector3(-0.5f, -h / 3f, 0f), new Vector3(0.5f, -h / 3f, 0f), new Vector3(0f, h * 2f / 3f, 0f) },
                uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f) },
                triangles = new[] { 0, 1, 2 },
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>两点之间的一根圆管（软管、把手、扫帚柄）。</summary>
        private static GameObject Tube(Transform parent, string name, Vector3 a, Vector3 b, float diameter, Material mat)
        {
            Vector3 d = b - a;
            GameObject go = LocalCylinder(parent, name, (a + b) * 0.5f, diameter, d.magnitude, mat);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            return go;
        }

        // ================================================================ 灭火器

        /// <summary>
        /// 挂墙的手提式干粉灭火器：墙上的挂架和箍带、圆肩瓶身、白色标签、阀头（提把 + 压把 + 压力表 + 保险销）、
        /// 沿瓶身垂下的黑色软管和喷嘴，上方一块红底白图的标识牌。本地 +Z 朝外（离墙）。
        /// </summary>
        private static void FireExtinguisher(Transform parent, string name, Vector3 wallPoint, Vector3 normal)
        {
            float yaw = Mathf.Atan2(normal.x, normal.z) * Mathf.Rad2Deg;
            Transform g = NewGroup(parent, name, wallPoint, yaw);
            PropKit k = Props;

            const float baseY = 0.29f;          // 瓶底离地
            const float r = 0.085f;
            const float cz = 0.035f + r;        // 瓶身轴线离墙
            Material metal = Finishes.MetalDark;

            // 挂架：背板 + 托臂 + 底托 + 箍带
            LocalBox(g, "Bracket_Plate", new Vector3(0f, baseY + 0.3f, 0.006f), new Vector3(0.09f, 0.44f, 0.012f), metal);
            LocalBox(g, "Bracket_Arm", new Vector3(0f, baseY + 0.4f, 0.022f), new Vector3(0.06f, 0.03f, 0.035f), metal);
            LocalCylinder(g, "Bracket_Cup", new Vector3(0f, baseY + 0.02f, cz), 2f * r + 0.012f, 0.04f, metal);
            LocalBox(g, "Bracket_CupArm", new Vector3(0f, baseY + 0.02f, 0.02f), new Vector3(0.05f, 0.03f, 0.04f), metal);
            LocalCylinder(g, "Bracket_Strap", new Vector3(0f, baseY + 0.4f, cz), 2f * r + 0.008f, 0.025f, Kit.Chrome);

            // 瓶身（保留名字 Body 和碰撞）
            GameObject body = MeshObject(g, "Body", k.ExtinguisherBody, Finishes.RedPaint, new Vector3(0f, baseY, cz), Vector3.one);
            CapsuleCollider col = body.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.26f, 0f);
            col.height = 0.52f;
            col.radius = r;

            // 标签：一圈白带 + 正面几行黑字 + 红色图标块
            LocalCylinder(g, "Label_Band", new Vector3(0f, baseY + 0.24f, cz), 2f * r + 0.003f, 0.17f, k.White);
            float face = cz + r + 0.0025f;
            LocalBox(g, "Label_Title", new Vector3(0f, baseY + 0.3f, face), new Vector3(0.05f, 0.018f, 0.002f), Finishes.RedPaint);
            for (int i = 0; i < 4; i++)
            {
                LocalBox(g, "Label_Text_" + i, new Vector3(0f, baseY + 0.265f - i * 0.022f, face), new Vector3(0.048f - i * 0.004f, 0.006f, 0.002f), k.Black);
            }

            // 阀头：瓶颈、阀体、提把、压把、压力表、保险销
            float top = baseY + 0.516f;
            LocalCylinder(g, "Neck", new Vector3(0f, top + 0.012f, cz), 0.036f, 0.026f, Kit.Chrome);
            LocalBox(g, "Valve", new Vector3(0f, top + 0.045f, cz), new Vector3(0.042f, 0.045f, 0.06f), Kit.Chrome);
            GameObject carry = LocalBox(g, "Handle_Carry", new Vector3(0f, top + 0.068f, cz - 0.04f), new Vector3(0.026f, 0.01f, 0.12f), k.Black);
            carry.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            GameObject lever = LocalBox(g, "Handle_Lever", new Vector3(0f, top + 0.095f, cz - 0.035f), new Vector3(0.026f, 0.01f, 0.12f), k.Black);
            lever.transform.localRotation = Quaternion.Euler(-24f, 0f, 0f);
            GameObject gauge = LocalCylinder(g, "Gauge", new Vector3(0f, top + 0.05f, cz + 0.04f), 0.034f, 0.012f, Kit.Chrome);
            gauge.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            GameObject dial = LocalCylinder(g, "Gauge_Dial", new Vector3(0f, top + 0.05f, cz + 0.0465f), 0.028f, 0.002f, k.White);
            dial.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            LocalBox(g, "Gauge_Needle", new Vector3(0.003f, top + 0.053f, cz + 0.0478f), new Vector3(0.002f, 0.011f, 0.001f), Finishes.RedPaint);
            GameObject pin = LocalCylinder(g, "SafetyPin_Ring", new Vector3(0.034f, top + 0.055f, cz), 0.024f, 0.004f, Finishes.WarningYellow);
            pin.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Tube(g, "SafetyPin", new Vector3(-0.025f, top + 0.055f, cz), new Vector3(0.03f, top + 0.055f, cz), 0.005f, Kit.Chrome);

            // 软管：阀侧出口 → 绕到瓶身侧面 → 垂到下部卡扣，末端喷嘴
            Vector3 h0 = new Vector3(-0.024f, top + 0.04f, cz + 0.01f);
            Vector3 h1 = new Vector3(-0.07f, top + 0.02f, cz + 0.03f);
            Vector3 h2 = new Vector3(-r - 0.014f, top - 0.06f, cz + 0.025f);
            Vector3 h3 = new Vector3(-r - 0.014f, baseY + 0.16f, cz + 0.02f);
            Tube(g, "Hose_0", h0, h1, 0.016f, k.Black);
            Tube(g, "Hose_1", h1, h2, 0.016f, k.Black);
            Tube(g, "Hose_2", h2, h3, 0.016f, k.Black);
            Tube(g, "Nozzle", h3, h3 + new Vector3(0.004f, -0.08f, 0.004f), 0.028f, k.Black);
            LocalBox(g, "Hose_Clip", new Vector3(-r - 0.006f, baseY + 0.2f, cz + 0.02f), new Vector3(0.02f, 0.02f, 0.03f), Kit.Chrome);

            // 墙上的标识牌：红底、白色灭火器图标、底部一行白字
            float signY = 1.25f;
            LocalBox(g, "Sign", new Vector3(0f, signY, 0.006f), new Vector3(0.2f, 0.2f, 0.012f), Finishes.RedPaint);
            LocalBox(g, "Sign_Icon_Body", new Vector3(-0.02f, signY + 0.02f, 0.0135f), new Vector3(0.04f, 0.09f, 0.002f), k.White);
            LocalBox(g, "Sign_Icon_Head", new Vector3(-0.02f, signY + 0.075f, 0.0135f), new Vector3(0.022f, 0.02f, 0.002f), k.White);
            GameObject flame = MeshObject(g, "Sign_Icon_Flame", k.Triangle, k.White, new Vector3(0.045f, signY + 0.025f, 0.0135f), Vector3.one * 0.045f, false);
            flame.transform.localRotation = Quaternion.identity;
            LocalBox(g, "Sign_Text", new Vector3(0f, signY - 0.07f, 0.0135f), new Vector3(0.15f, 0.02f, 0.002f), k.White);
        }

        // ================================================================ 清洁车

        /// <summary>
        /// 保洁推车：四个万向轮、底层托板、两根立柱撑起的顶层托盘（四周有挡边），
        /// 一端是推把，另一端挂黄色垃圾袋；底层放带挤水器的拖把桶和两卷纸巾，顶层放喷壶、卷纸、抹布，
        /// 拖把插在桶里、扫帚斜靠在车边。外形仍是约 1.0 × 1.1 × 0.5 m，一个盒子碰撞体。
        /// </summary>
        private static void CleaningCart(Transform parent, float x, float y, float z)
        {
            Transform g = NewGroup(parent, "CleaningCart", new Vector3(x, y, z), 0f);
            PropKit k = Props;
            Material frame = k.CartGrey;

            BoxCollider col = g.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.55f, 0f);
            col.size = new Vector3(1.05f, 1.1f, 0.52f);

            // 万向轮：叉架 + 轮子
            for (int i = 0; i < 4; i++)
            {
                float wx = i < 2 ? -0.42f : 0.42f;
                float wz = i % 2 == 0 ? -0.19f : 0.19f;
                LocalBox(g, "Caster_Fork_" + i, new Vector3(wx, 0.1f, wz), new Vector3(0.035f, 0.07f, 0.05f), Finishes.MetalDark);
                GameObject wheel = LocalCylinder(g, "Caster_Wheel_" + i, new Vector3(wx, 0.05f, wz), 0.1f, 0.03f, k.Black);
                wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            // 底层托板（带一圈翻边）
            LocalBox(g, "Deck_Bottom", new Vector3(0f, 0.16f, 0f), new Vector3(1.0f, 0.05f, 0.5f), frame);
            LocalBox(g, "Deck_Lip_F", new Vector3(0f, 0.2f, 0.24f), new Vector3(1.0f, 0.04f, 0.02f), frame);
            LocalBox(g, "Deck_Lip_B", new Vector3(0f, 0.2f, -0.24f), new Vector3(1.0f, 0.04f, 0.02f), frame);

            // 立柱 + 顶层托盘（四周挡边）
            for (int i = 0; i < 4; i++)
            {
                float px = i < 2 ? -0.47f : 0.14f;
                float pz = i % 2 == 0 ? -0.22f : 0.22f;
                LocalBox(g, "Post_" + i, new Vector3(px, 0.56f, pz), new Vector3(0.045f, 0.76f, 0.045f), frame);
            }

            const float trayY = 0.95f;
            LocalBox(g, "Tray", new Vector3(-0.165f, trayY, 0f), new Vector3(0.68f, 0.04f, 0.5f), frame);
            LocalBox(g, "Tray_Rim_F", new Vector3(-0.165f, trayY + 0.04f, 0.24f), new Vector3(0.68f, 0.05f, 0.02f), frame);
            LocalBox(g, "Tray_Rim_B", new Vector3(-0.165f, trayY + 0.04f, -0.24f), new Vector3(0.68f, 0.05f, 0.02f), frame);
            LocalBox(g, "Tray_Rim_L", new Vector3(-0.495f, trayY + 0.04f, 0f), new Vector3(0.02f, 0.05f, 0.5f), frame);
            LocalBox(g, "Tray_Divider", new Vector3(-0.2f, trayY + 0.035f, 0f), new Vector3(0.015f, 0.04f, 0.46f), frame);

            // 推把：两根竖管 + 黑色握把
            for (int s = -1; s <= 1; s += 2)
            {
                Tube(g, "Handle_Post_" + (s < 0 ? "L" : "R"), new Vector3(-0.53f, 0.5f, s * 0.18f), new Vector3(-0.56f, 1.06f, s * 0.18f), 0.026f, Kit.Chrome);
            }

            Tube(g, "Handle_Grip", new Vector3(-0.56f, 1.06f, -0.2f), new Vector3(-0.56f, 1.06f, 0.2f), 0.034f, k.Black);

            // 垃圾袋架 + 黄色垃圾袋（上口撑开、底部收窄下垂）
            LocalBox(g, "BagHoop_F", new Vector3(0.33f, trayY, 0.22f), new Vector3(0.36f, 0.03f, 0.03f), frame);
            LocalBox(g, "BagHoop_B", new Vector3(0.33f, trayY, -0.22f), new Vector3(0.36f, 0.03f, 0.03f), frame);
            LocalBox(g, "BagHoop_End", new Vector3(0.5f, trayY, 0f), new Vector3(0.03f, 0.03f, 0.47f), frame);
            LocalBox(g, "Bag_Top", new Vector3(0.33f, trayY - 0.22f, 0f), new Vector3(0.33f, 0.42f, 0.42f), k.BagVinyl);
            LocalBox(g, "Bag_Bottom", new Vector3(0.33f, 0.375f, 0f), new Vector3(0.3f, 0.3f, 0.38f), k.BagVinyl);
            LocalBox(g, "Bag_Fold", new Vector3(0.33f, trayY - 0.02f, 0f), new Vector3(0.35f, 0.04f, 0.44f), k.BagVinyl);

            // 底层：拖把桶 + 挤水器
            LocalBox(g, "Bucket", new Vector3(-0.27f, 0.32f, 0f), new Vector3(0.36f, 0.27f, 0.32f), Finishes.WarningYellow);
            LocalBox(g, "Bucket_Water", new Vector3(-0.27f, 0.43f, 0f), new Vector3(0.32f, 0.01f, 0.28f), Kit.Partition);
            LocalBox(g, "Wringer_Box", new Vector3(-0.2f, 0.5f, 0f), new Vector3(0.16f, 0.12f, 0.26f), Finishes.WarningYellow);
            LocalBox(g, "Wringer_Plate", new Vector3(-0.2f, 0.51f, 0f), new Vector3(0.12f, 0.1f, 0.22f), k.Black);
            Tube(g, "Wringer_Lever", new Vector3(-0.15f, 0.56f, 0.1f), new Vector3(-0.48f, 0.86f, 0.1f), 0.022f, Kit.Chrome);

            // 拖把：杆子插在桶里，拖把头泡在水里
            Tube(g, "Mop_Handle", new Vector3(-0.33f, 0.4f, -0.08f), new Vector3(-0.2f, 1.45f, -0.14f), 0.026f, Finishes.CrateWood);
            LocalBox(g, "Mop_Head", new Vector3(-0.33f, 0.4f, -0.08f), new Vector3(0.12f, 0.06f, 0.18f), Kit.Linen);

            // 底层另一侧：两卷大纸巾
            for (int i = 0; i < 2; i++)
            {
                GameObject roll = LocalCylinder(g, "PaperRoll_" + i, new Vector3(0.0f, 0.27f, -0.11f + i * 0.22f), 0.2f, 0.17f, Kit.Paper);
                LocalCylinder(roll.transform, "Core", Vector3.zero, 0.3f, 1.02f, Finishes.CrateWood);
            }

            // 顶层：两个喷壶、一卷卫生纸、一块叠好的抹布
            SprayBottle(g, "Spray_0", new Vector3(-0.42f, trayY + 0.02f, -0.12f), 20f, k.SprayBlue);
            SprayBottle(g, "Spray_1", new Vector3(-0.33f, trayY + 0.02f, -0.15f), -35f, k.SprayGreen);
            LocalCylinder(g, "ToiletRoll", new Vector3(-0.38f, trayY + 0.075f, 0.12f), 0.11f, 0.11f, k.White);
            LocalBox(g, "Rag", new Vector3(-0.08f, trayY + 0.03f, 0.05f), new Vector3(0.18f, 0.02f, 0.22f), Kit.Fabrics[1]);

            // 扫帚斜靠在车的袋子一侧
            Tube(g, "Broom_Handle", new Vector3(0.55f, 0.1f, 0.27f), new Vector3(0.53f, 1.35f, 0.15f), 0.024f, Finishes.CrateWood);
            GameObject brush = LocalBox(g, "Broom_Head", new Vector3(0.55f, 0.06f, 0.27f), new Vector3(0.3f, 0.09f, 0.06f), k.Black);
            brush.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }

        private static void SprayBottle(Transform parent, string name, Vector3 basePos, float yaw, Material liquid)
        {
            Transform s = NewGroup(parent, name, basePos, yaw);
            MeshObject(s, "Bottle", Props.SprayBottle, liquid, Vector3.zero, Vector3.one);
            LocalBox(s, "Trigger_Head", new Vector3(0.012f, 0.205f, 0f), new Vector3(0.06f, 0.035f, 0.026f), Props.White);
            LocalBox(s, "Nozzle", new Vector3(0.048f, 0.205f, 0f), new Vector3(0.016f, 0.014f, 0.014f), Props.Black);
            GameObject trigger = LocalBox(s, "Trigger", new Vector3(0.03f, 0.18f, 0f), new Vector3(0.008f, 0.04f, 0.014f), Props.White);
            trigger.transform.localRotation = Quaternion.Euler(0f, 0f, -15f);
            LocalBox(s, "Label", new Vector3(0f, 0.07f, 0.0255f), new Vector3(0.05f, 0.07f, 0.002f), Props.White);
        }

        // ================================================================ "小心地滑"警示牌

        /// <summary>
        /// 黄色 A 字警示牌：两块圆顶面板在顶端铰接，顶上有提手槽、底边有黑色防滑脚；
        /// 两面都印黑色"CAUTION"横条、黑框黄底的警告三角（里面一个滑倒的小人）和两行字。
        /// </summary>
        private static void WetFloorSign(Transform parent, float x, float y, float z, float yaw)
        {
            Transform g = NewGroup(parent, "WetFloorSign", new Vector3(x, y, z), yaw);
            const float hinge = 0.66f;
            LocalCylinder(g, "Hinge", new Vector3(0f, hinge, 0f), 0.025f, 0.26f, Props.Black).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            // 两块面板绕顶部铰链张开 16°；背面那块再转 180°，印刷面始终朝外
            SignPanel(g, "Front", Quaternion.Euler(-16f, 0f, 0f), hinge);
            SignPanel(g, "Back", Quaternion.Euler(-16f, 180f, 0f), hinge);
        }

        private static void SignPanel(Transform parent, string name, Quaternion rotation, float hinge)
        {
            PropKit k = Props;
            Transform p = NewGroup(parent, name);
            p.localPosition = new Vector3(0f, hinge, 0f);
            p.localRotation = rotation;

            const float w = 0.3f;
            const float t = 0.015f;
            Material yellow = Finishes.WarningYellow;

            // 板身（原点在铰链，往下长）+ 圆顶 + 提手槽 + 防滑脚
            GameObject board = AddBox(p, "Board", new Vector3(0f, -0.33f, 0f), new Vector3(w, 0.58f, t), yellow);
            RemoveCollider(board);
            GameObject dome = LocalCylinder(p, "Dome", new Vector3(0f, -0.04f, 0f), w, t, yellow);
            dome.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            LocalBox(p, "Grip_Slot", new Vector3(0f, -0.005f, 0f), new Vector3(0.1f, 0.028f, t + 0.002f), k.Black);
            LocalBox(p, "Foot", new Vector3(0f, -0.615f, 0f), new Vector3(w + 0.01f, 0.025f, t + 0.012f), k.Black);

            // 印刷面在本地 +Z
            float face = t * 0.5f + 0.001f;
            LocalBox(p, "Caution_Bar", new Vector3(0f, -0.12f, face), new Vector3(0.25f, 0.05f, 0.002f), k.Black);
            LocalBox(p, "Caution_Text", new Vector3(0f, -0.12f, face + 0.001f), new Vector3(0.17f, 0.016f, 0.001f), yellow);

            MeshObject(p, "Warning_Outline", k.Triangle, k.Black, new Vector3(0f, -0.29f, face), Vector3.one * 0.19f, false);
            MeshObject(p, "Warning_Fill", k.Triangle, yellow, new Vector3(0f, -0.29f, face + 0.0008f), Vector3.one * 0.15f, false);

            // 滑倒的小人：头、身体、甩开的四肢
            Transform man = NewGroup(p, "Pictogram");
            man.localPosition = new Vector3(0f, -0.3f, face + 0.0016f);
            LocalSphere(man, "Head", new Vector3(0.012f, 0.03f, 0f), new Vector3(0.014f, 0.014f, 0.001f), k.Black);
            Limb(man, "Torso", new Vector2(0.006f, 0.02f), new Vector2(-0.006f, -0.005f), k.Black);
            Limb(man, "Arm_L", new Vector2(0.004f, 0.014f), new Vector2(-0.02f, 0.022f), k.Black);
            Limb(man, "Arm_R", new Vector2(0.004f, 0.014f), new Vector2(0.025f, 0.006f), k.Black);
            Limb(man, "Leg_L", new Vector2(-0.006f, -0.005f), new Vector2(-0.026f, -0.02f), k.Black);
            Limb(man, "Leg_R", new Vector2(-0.006f, -0.005f), new Vector2(0.018f, -0.024f), k.Black);
            LocalBox(man, "Floor_Line", new Vector3(0f, -0.03f, 0f), new Vector3(0.07f, 0.004f, 0.001f), k.Black);

            for (int i = 0; i < 2; i++)
            {
                LocalBox(p, "Text_" + i, new Vector3(0f, -0.46f - i * 0.04f, face), new Vector3(0.2f - i * 0.05f, 0.018f, 0.002f), k.Black);
            }
        }

        private static void Limb(Transform parent, string name, Vector2 a, Vector2 b, Material mat)
        {
            Vector2 d = b - a;
            GameObject go = LocalBox(parent, name, (Vector3)((a + b) * 0.5f), new Vector3(d.magnitude, 0.006f, 0.001f), mat);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        // ================================================================ 二层长廊展柜

        /// <summary>
        /// 长廊展柜（GDD 的 5 组掩体）：1.0 m 高的深色木柜身 + 0.4 m 玻璃罩，里面摆旧信件、速写本、钢笔。
        /// 整体放大 WorldScale 后柜身约 1.4 m，比 1.3 m 的孩子高 —— 不用蹲也能藏住（游戏里没有蹲）。
        /// 细节：两级底座线脚、四角黄铜柱头柱础的壁柱、双层镶板（外框 + 内芯）+ 黄铜锁眼、檐口与黄铜压边、
        /// 正面铜铭牌；玻璃罩每条边都有黄铜框，正面一把小锁；柜内两级丝绒台阶，摆封了火漆的信、
        /// 系丝带的一叠信、立在书架上摊开的速写本、墨水瓶和钢笔，每件前面一张说明卡。
        /// 玻璃罩在 Ignore Raycast 层，不挡视线判定。外轮廓与碰撞体和原来一致（2.9 × 1.4 × 0.95）。
        /// </summary>
        private static void GalleryVitrine(Transform parent, string name, float x, float y, float z, int seed)
        {
            Transform g = NewGroup(parent, name, new Vector3(x, y, z), 0f);
            PropKit k = Props;
            Vector3 size = new Vector3(2.9f, 1.4f, 0.95f);
            const float bodyH = 1.0f;
            float hx = size.x * 0.5f;
            float hz = size.z * 0.5f;

            // ---- 柜身（碰撞）----
            AddBox(g, "Body", new Vector3(0f, bodyH * 0.5f, 0f), new Vector3(size.x, bodyH, size.z), Finishes.Wainscot);

            // 底座：踢脚 + 一道线脚
            LocalBox(g, "Kick", new Vector3(0f, 0.05f, 0f), new Vector3(size.x + 0.04f, 0.1f, size.z + 0.04f), Finishes.Trim);
            LocalBox(g, "Base_Mould", new Vector3(0f, 0.112f, 0f), new Vector3(size.x + 0.025f, 0.025f, size.z + 0.025f), Finishes.Trim);

            // 檐口：中楣 + 压线 + 黄铜压边
            LocalBox(g, "Frieze", new Vector3(0f, 0.915f, 0f), new Vector3(size.x + 0.02f, 0.07f, size.z + 0.02f), Finishes.Trim);
            LocalBox(g, "Cornice", new Vector3(0f, 0.965f, 0f), new Vector3(size.x + 0.05f, 0.03f, size.z + 0.05f), Finishes.Trim);
            // 黄铜压边：和 GlassCase 的 Plinth_Trim 一个道理，顶面必须低于柜身顶面 8 mm。
            // 齐平时 2.9 × 0.95 m 的整片柜台面就是两片同向共面，玩家移动时木纹/黄铜交替闪。
            const float topTrimH = 0.016f;
            const float topTrimDrop = 0.008f;
            LocalBox(g, "Top_Trim", new Vector3(0f, bodyH - topTrimDrop - topTrimH * 0.5f, 0f), new Vector3(size.x + 0.06f, topTrimH, size.z + 0.06f), Finishes.Brass);

            // 四角壁柱 + 黄铜柱头 / 柱础
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 c = new Vector3(sx * (hx - 0.03f), 0f, sz * (hz - 0.03f));
                    LocalBox(g, "Pilaster", c + Vector3.up * 0.5f, new Vector3(0.09f, 0.74f, 0.09f), Finishes.Wainscot);
                    LocalBox(g, "Pilaster_Base", c + Vector3.up * 0.14f, new Vector3(0.1f, 0.03f, 0.1f), Finishes.Brass);
                    LocalBox(g, "Pilaster_Cap", c + Vector3.up * 0.865f, new Vector3(0.1f, 0.03f, 0.1f), Finishes.Brass);
                }
            }

            // 长边：每面三块双层镶板，中间那块有锁眼；正面再加一块铜铭牌
            for (int side = -1; side <= 1; side += 2)
            {
                float fz = side * (hz + 0.008f);
                for (int i = 0; i < 3; i++)
                {
                    float px = (i - 1) * 0.9f;
                    LocalBox(g, "Panel_Frame", new Vector3(px, 0.5f, fz), new Vector3(0.76f, 0.6f, 0.016f), Finishes.Trim);
                    LocalBox(g, "Panel_Field", new Vector3(px, 0.5f, fz + side * 0.008f), new Vector3(0.62f, 0.46f, 0.012f), Finishes.Wainscot);
                    LocalBox(g, "Panel_Bead", new Vector3(px, 0.5f, fz + side * 0.011f), new Vector3(0.56f, 0.4f, 0.006f), Finishes.Trim);
                }

                LocalBox(g, "Keyhole_Plate", new Vector3(0f, 0.82f, fz + side * 0.01f), new Vector3(0.035f, 0.055f, 0.006f), Finishes.Brass);
                LocalBox(g, "Keyhole", new Vector3(0f, 0.818f, fz + side * 0.0135f), new Vector3(0.007f, 0.02f, 0.002f), k.Black);
            }

            LocalBox(g, "NamePlate", new Vector3(0f, 0.915f, hz + 0.022f), new Vector3(0.32f, 0.045f, 0.006f), Finishes.Brass);
            LocalBox(g, "NamePlate_Text", new Vector3(0f, 0.915f, hz + 0.0255f), new Vector3(0.24f, 0.012f, 0.001f), k.Black);

            // 短边：各一块双层镶板
            for (int side = -1; side <= 1; side += 2)
            {
                float fx = side * (hx + 0.008f);
                LocalBox(g, "End_Frame", new Vector3(fx, 0.5f, 0f), new Vector3(0.016f, 0.6f, 0.66f), Finishes.Trim);
                LocalBox(g, "End_Field", new Vector3(fx + side * 0.008f, 0.5f, 0f), new Vector3(0.012f, 0.46f, 0.52f), Finishes.Wainscot);
            }

            // ---- 玻璃罩 + 黄铜框 ----
            GameObject glass = AddBox(g, "Glass", new Vector3(0f, bodyH + 0.2f, 0f), new Vector3(size.x - 0.04f, 0.4f, size.z - 0.04f), Finishes.GlassClear);
            glass.layer = IgnoreRaycastLayer;
            glass.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            float gx = hx - 0.02f;
            float gz = hz - 0.02f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    LocalBox(g, "Edge_V", new Vector3(sx * gx, bodyH + 0.2f, sz * gz), new Vector3(0.022f, 0.4f, 0.022f), Finishes.Brass);
                }
            }

            foreach (float ey in new[] { bodyH + 0.012f, size.y - 0.011f })
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    LocalBox(g, "Edge_X", new Vector3(0f, ey, s * gz), new Vector3(size.x - 0.02f, 0.022f, 0.022f), Finishes.Brass);
                    LocalBox(g, "Edge_Z", new Vector3(s * gx, ey, 0f), new Vector3(0.022f, 0.022f, size.z - 0.02f), Finishes.Brass);
                }
            }

            LocalBox(g, "Edge_Top", new Vector3(0f, size.y - 0.01f, 0f), new Vector3(size.x - 0.04f, 0.02f, 0.02f), Finishes.Brass);
            LocalBox(g, "Glass_Lock", new Vector3(0f, bodyH + 0.035f, gz + 0.016f), new Vector3(0.05f, 0.03f, 0.012f), Finishes.Brass);

            // ---- 柜内陈设 ----
            float floor = bodyH + 0.012f;
            LocalBox(g, "Velvet", new Vector3(0f, bodyH + 0.006f, 0f), new Vector3(size.x - 0.12f, 0.012f, size.z - 0.12f), Finishes.Velvet);
            LocalBox(g, "Velvet_Step_1", new Vector3(0f, floor + 0.035f, -0.18f), new Vector3(size.x - 0.2f, 0.07f, 0.3f), Finishes.Velvet);
            LocalBox(g, "Velvet_Step_2", new Vector3(0f, floor + 0.07f, -0.27f), new Vector3(size.x - 0.2f, 0.14f, 0.13f), Finishes.Velvet);

            // 散放的三封信（前排），最后一封盖着火漆
            for (int i = 0; i < 3; i++)
            {
                Vector3 pos = new Vector3(-1.0f + i * 0.32f + seed * 0.04f, floor + 0.003f + i * 0.002f, 0.13f + (i % 2) * 0.06f);
                GameObject paper = LocalBox(g, "Letter_" + i, pos, new Vector3(0.21f, 0.003f, 0.29f), Kit.Paper);
                paper.transform.localRotation = Quaternion.Euler(0f, -12f + i * 11f + seed * 7f, 0f);
                for (int line = 0; line < 5; line++)
                {
                    LocalBox(paper.transform, "Ink_" + line, new Vector3(0f, 0.6f, 0.3f - line * 0.13f), new Vector3(0.8f, 0.3f, 0.02f), k.InkGlass);
                }
            }

            GameObject envelope = LocalBox(g, "Envelope", new Vector3(-0.35f + seed * 0.03f, floor + 0.006f, 0.2f), new Vector3(0.22f, 0.006f, 0.12f), Kit.Paper);
            envelope.transform.localRotation = Quaternion.Euler(0f, 6f - seed * 4f, 0f);
            LocalCylinder(envelope.transform, "WaxSeal", new Vector3(0f, 1.2f, 0f), 0.14f, 0.6f, k.WaxRed);

            // 第一级台阶：系着丝带的一叠信 + 墨水瓶 + 钢笔
            Transform bundle = NewGroup(g, "LetterBundle", new Vector3(-0.75f, floor + 0.07f, -0.18f), 10f + seed * 9f);
            for (int i = 0; i < 4; i++)
            {
                GameObject sheet = LocalBox(bundle, "Sheet_" + i, new Vector3(0f, 0.004f + i * 0.007f, 0f), new Vector3(0.2f, 0.006f, 0.13f), Kit.Paper);
                sheet.transform.localRotation = Quaternion.Euler(0f, (i - 1.5f) * 3f, 0f);
            }
            LocalBox(bundle, "Ribbon_A", new Vector3(0f, 0.016f, 0f), new Vector3(0.022f, 0.034f, 0.135f), k.WaxRed);
            LocalBox(bundle, "Ribbon_B", new Vector3(0f, 0.016f, 0f), new Vector3(0.205f, 0.034f, 0.022f), k.WaxRed);

            Transform ink = NewGroup(g, "InkWell", new Vector3(0.95f, floor + 0.07f, -0.2f), 0f);
            LocalBox(ink, "Bottle", new Vector3(0f, 0.03f, 0f), new Vector3(0.055f, 0.06f, 0.055f), k.InkGlass);
            LocalCylinder(ink, "Neck", new Vector3(0f, 0.07f, 0f), 0.026f, 0.02f, k.InkGlass);
            LocalCylinder(ink, "Cap", new Vector3(0f, 0.087f, 0f), 0.03f, 0.016f, Finishes.Brass);

            GameObject pen = Tube(g, "Pen", new Vector3(0.68f, floor + 0.078f, -0.13f), new Vector3(0.82f, floor + 0.078f, -0.2f + seed * 0.01f), 0.012f, k.Black);
            LocalCylinder(pen.transform, "Nib", new Vector3(0f, -0.52f, 0f), 0.7f, 0.06f, Finishes.Brass);

            // 第二级台阶：书架上摊开的速写本（封皮 + 两页，中缝凹下去）
            Transform stand = NewGroup(g, "SketchStand", new Vector3(0.15f + seed * 0.05f, floor + 0.14f, -0.27f), 0f);
            stand.localRotation = Quaternion.Euler(28f, 0f, 0f);   // 往后仰，书页朝向柜子正面
            LocalBox(stand, "Stand", new Vector3(0f, 0f, 0f), new Vector3(0.36f, 0.012f, 0.24f), Finishes.Brass);
            LocalBox(stand, "Stand_Lip", new Vector3(0f, 0.012f, 0.115f), new Vector3(0.36f, 0.02f, 0.01f), Finishes.Brass);
            LocalBox(stand, "Cover", new Vector3(0f, 0.01f, 0f), new Vector3(0.34f, 0.008f, 0.23f), Kit.Book);
            GameObject left = LocalBox(stand, "Page_L", new Vector3(-0.08f, 0.02f, 0f), new Vector3(0.16f, 0.008f, 0.21f), Kit.Paper);
            left.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
            GameObject right = LocalBox(stand, "Page_R", new Vector3(0.08f, 0.02f, 0f), new Vector3(0.16f, 0.008f, 0.21f), Kit.Paper);
            right.transform.localRotation = Quaternion.Euler(0f, 0f, 4f);
            for (int i = 0; i < 3; i++)
            {
                // 铅笔速写的几道线
                GameObject stroke = LocalBox(right.transform, "Stroke_" + i, new Vector3(-0.1f + i * 0.1f, 0.6f, 0.1f - i * 0.15f), new Vector3(0.5f, 0.3f, 0.03f), Finishes.MetalDark);
                stroke.transform.localRotation = Quaternion.Euler(0f, 25f - i * 20f, 0f);
            }

            // 说明卡：每件展品前一张，斜立
            float[] cards = { -0.95f, -0.3f, 0.3f, 0.9f };
            for (int i = 0; i < cards.Length; i++)
            {
                GameObject card = LocalBox(g, "Label_Card_" + i, new Vector3(cards[i], floor + 0.02f, 0.33f), new Vector3(0.09f, 0.003f, 0.055f), Finishes.Label);
                card.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
            }
        }
    }
}
