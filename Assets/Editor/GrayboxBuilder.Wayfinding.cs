using Ib.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ib.EditorTools
{
    /// <summary>
    /// 导视门牌 + 配电室门/室内。2026-11-01 老大需求：
    ///   · 删掉接待厅两侧的绿色出口牌，换成 "PAINTING GALLERY" / "COSTUME HALL" 美术馆门牌；
    ///   · 雕塑厅东西两个门口上方各挂一块 "SCULPTURE HALL"；
    ///   · 配电室做一扇可动画的单开门（铰链节点 PowerRoom_DoorHinge，门板 PowerRoom_Door），
    ///     门内补配电柜、线槽、警示牌等细节。
    ///
    /// 所有尺寸仍按 1 倍写，最后由 ApplyWorldScale 统一乘 1.4。门牌挂在 Props_Museum 下，不参与静态合批。
    /// </summary>
    public static partial class GrayboxBuilder
    {
        private const string SignFontDir = "Assets/Art/Fonts";
        private const string SignFontPath = SignFontDir + "/Cinzel-Regular.ttf";
        private const string SignTextMaterialPath = FinishDir + "/M_Text3D_SignSerif.mat";

        private static readonly Color SignInk = new Color(0.93f, 0.86f, 0.68f);
        private static readonly Color WarningInk = new Color(0.08f, 0.07f, 0.06f);

        /// <summary>四块厅名门牌。放在建筑细节之后调用，避免被门套/墙面装饰盖住。</summary>
        private static void BuildWayfindingSigns(Transform parent)
        {
            Transform root = NewGroup(parent, "WayfindingSigns");
            float y = Floor1Y + HallDoorHeight + 0.60f;   // 门套顶 3.36 m，牌底留约 10 cm

            // 接待厅：西侧门通油画厅（绘画廊），东侧门通服装厅。牌面朝接待厅。
            HallPlaque(root, "Sign_PaintingGallery",
                new Vector3(WestLineX + HalfWall, y, SouthDoorZ), Vector3.right, "PAINTING GALLERY");
            HallPlaque(root, "Sign_CostumeHall",
                new Vector3(EastLineX - HalfWall, y, SouthDoorZ), Vector3.left, "COSTUME HALL");

            // 雕塑厅：牌挂在东西两侧门洞的外侧墙面，分别从油画厅 / 服装厅看过来。
            HallPlaque(root, "Sign_SculptureHall_W",
                new Vector3(WestLineX - HalfWall, y, MidDoorZ), Vector3.left, "SCULPTURE HALL");
            HallPlaque(root, "Sign_SculptureHall_E",
                new Vector3(EastLineX + HalfWall, y, MidDoorZ), Vector3.right, "SCULPTURE HALL");
        }

        /// <summary>美术馆风格门牌：黄铜外框 + 深色木底 + 米色衬线字。无碰撞体，不挡人。</summary>
        private static void HallPlaque(Transform parent, string name, Vector3 wallPoint, Vector3 normal, string title)
        {
            const float width = 1.92f;
            const float height = 0.62f;

            Transform g = NewGroup(parent, name);
            g.position = wallPoint;
            g.rotation = Quaternion.LookRotation(normal, Vector3.up);

            // 黄铜背板（比木底每边大 7 cm，形成一圈铜边）
            GameObject back = AddBox(g, "BrassBorder", new Vector3(0f, 0f, 0.030f),
                new Vector3(width + 0.14f, height + 0.14f, 0.060f), Finishes.Brass);
            RemoveCollider(back);

            // 深色木底
            GameObject face = AddBox(g, "WoodFace", new Vector3(0f, 0f, 0.064f),
                new Vector3(width, height, 0.036f), Finishes.Wainscot);
            RemoveCollider(face);

            // 内侧细铜线，把木底和字框起来
            GameObject ruleTop = AddBox(g, "InnerRule_Top", new Vector3(0f, height * 0.5f - 0.035f, 0.083f),
                new Vector3(width - 0.10f, 0.010f, 0.006f), Finishes.Brass);
            GameObject ruleBottom = AddBox(g, "InnerRule_Bottom", new Vector3(0f, -height * 0.5f + 0.035f, 0.083f),
                new Vector3(width - 0.10f, 0.010f, 0.006f), Finishes.Brass);
            RemoveCollider(ruleTop);
            RemoveCollider(ruleBottom);

            SignText(g, "Title", new Vector3(0f, 0.015f, 0.090f), title, 0.103f, SignInk);

            // 一块小铜牌脚，藏在牌底，作为墙面上的阴影托
            GameObject foot = AddBox(g, "MountFoot", new Vector3(0f, -height * 0.5f - 0.055f, 0.020f),
                new Vector3(0.22f, 0.05f, 0.040f), Finishes.Brass);
            RemoveCollider(foot);
        }

        /// <summary>
        /// 配电室门：门洞北面、西侧门框为轴。门板挂在 PowerRoom_DoorHinge 下，
        /// 绕本地 Y 轴负方向转 90° 即向雕塑厅（+Z）外开。门板网格节点名 PowerRoom_Door。
        /// </summary>
        private static GameObject BuildPowerRoomDoor(Transform parent, Palette p)
        {
            const float leafWidth = SmallDoorWidth - 0.04f;    // 1.16 m，左右各留 2 cm 门缝
            const float leafHeight = SmallDoorHeight - 0.04f;  // 2.36 m
            const float leafThickness = 0.07f;

            Transform root = NewGroup(parent, "PowerRoomDoor");
            float hingeX = PowerDoorX - SmallDoorWidth * 0.5f + 0.02f;
            float hingeZ = PowerRoomNorthZ + HalfWall;         // 门洞北面，往雕塑厅开

            // 门框：贴在门洞内侧面，和 Detail 的木门套并存，看起来是包了钢框的配电室门
            Transform frame = NewGroup(root, "PowerRoom_DoorFrame");
            AddBox(frame, "Jamb_W", new Vector3(PowerDoorX - SmallDoorWidth * 0.5f + 0.02f, SmallDoorHeight * 0.5f, PowerRoomNorthZ),
                new Vector3(0.04f, SmallDoorHeight, 0.34f), Finishes.MetalDark);
            AddBox(frame, "Jamb_E", new Vector3(PowerDoorX + SmallDoorWidth * 0.5f - 0.02f, SmallDoorHeight * 0.5f, PowerRoomNorthZ),
                new Vector3(0.04f, SmallDoorHeight, 0.34f), Finishes.MetalDark);
            AddBox(frame, "Head", new Vector3(PowerDoorX, SmallDoorHeight - 0.02f, PowerRoomNorthZ),
                new Vector3(SmallDoorWidth, 0.04f, 0.34f), Finishes.MetalDark);
            AddBox(frame, "Threshold", new Vector3(PowerDoorX, 0.015f, PowerRoomNorthZ),
                new Vector3(SmallDoorWidth, 0.03f, 0.34f), Finishes.Brass);

            // 铰链节点：轴心就在门洞西侧、北面。代码只转这个节点即可。
            Transform hinge = NewGroup(root, "PowerRoom_DoorHinge", new Vector3(hingeX, Floor1Y, hingeZ), 0f);

            // 门板：外壳是空节点（缩放 1），门板网格与装饰都挂在下面。
            // 注意不要直接把细节挂到 Cube 上——Cube 的 localScale 会把子节点位置/尺寸再乘一次。
            Transform leaf = NewGroup(hinge, "PowerRoom_Door",
                new Vector3(leafWidth * 0.5f, leafHeight * 0.5f + 0.02f, -leafThickness * 0.5f), 0f);
            AddBox(leaf, "Slab", Vector3.zero,
                new Vector3(leafWidth, leafHeight, leafThickness), Finishes.Plastic);

            // 门板正面（北面）两块凹板 + 踢脚板，凹板用深一号的金属色
            AddBox(leaf, "Panel_Upper", new Vector3(0f, 0.28f, leafThickness * 0.5f + 0.004f),
                new Vector3(leafWidth * 0.68f, 0.86f, 0.008f), Finishes.MetalDark);
            AddBox(leaf, "Panel_Lower", new Vector3(0f, -0.55f, leafThickness * 0.5f + 0.004f),
                new Vector3(leafWidth * 0.68f, 0.62f, 0.008f), Finishes.MetalDark);
            AddBox(leaf, "KickPlate", new Vector3(0f, -leafHeight * 0.5f + 0.18f, leafThickness * 0.5f + 0.009f),
                new Vector3(leafWidth * 0.82f, 0.22f, 0.010f), Finishes.Brass);

            // 合页：三枚，跟着门板转
            for (int i = 0; i < 3; i++)
            {
                AddBox(leaf, "Hinge_" + i, new Vector3(-leafWidth * 0.5f + 0.035f, -0.85f + i * 0.85f, 0.0f),
                    new Vector3(0.07f, 0.20f, 0.10f), Finishes.Brass);
            }

            // 门把手 / 锁芯：靠东侧门边，取 1.09 m（和正门把手同高，7 岁孩子够得到）
            AddBox(leaf, "HandleRose", new Vector3(leafWidth * 0.5f - 0.12f, -0.11f, leafThickness * 0.5f + 0.012f),
                new Vector3(0.08f, 0.16f, 0.030f), Finishes.Brass);
            AddBox(leaf, "HandleLever", new Vector3(leafWidth * 0.5f - 0.22f, -0.11f, leafThickness * 0.5f + 0.050f),
                new Vector3(0.22f, 0.035f, 0.035f), Finishes.Brass);
            AddBox(leaf, "LockCylinder", new Vector3(leafWidth * 0.5f - 0.12f, -0.26f, leafThickness * 0.5f + 0.012f),
                new Vector3(0.06f, 0.09f, 0.030f), Finishes.MetalDark);

            // 门上的小标牌（孩子平视高度）
            SignText(leaf, "DoorLabel", new Vector3(0f, 0.10f, leafThickness * 0.5f + 0.011f),
                "ELECTRICAL", 0.045f, SignInk);
            SignText(leaf, "DoorLabelSub", new Vector3(0f, 0.025f, leafThickness * 0.5f + 0.011f),
                "AUTHORIZED PERSONNEL ONLY", 0.019f, new Color(0.82f, 0.76f, 0.62f));

            // 装饰件的小碰撞体全部去掉，只在整扇门板（空节点）上留一个 BoxCollider，给射线/交互用
            foreach (Collider c in leaf.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(c);
            }
            BoxCollider doorCollider = leaf.gameObject.AddComponent<BoxCollider>();
            doorCollider.size = new Vector3(leafWidth, leafHeight, leafThickness);
            doorCollider.center = Vector3.zero;

            // 门内侧的配电室细节
            BuildPowerRoomInterior(parent);

            return root.gameObject;
        }

        /// <summary>配电室内部：两组配电柜、线槽、导管、绝缘垫、警示牌、水桶。都在墙边，不挡门。</summary>
        private static void BuildPowerRoomInterior(Transform parent)
        {
            Transform power = parent.Find("PowerRoom");
            if (power == null)
            {
                power = NewGroup(parent, "PowerRoom");
            }

            // 西墙一组、东墙一组配电柜（柜门分别朝东 / 朝西，避开北门和南侧保安柜）
            PowerCabinet(power, "Cabinet_W", new Vector3(17.52f, 0f, 10.55f), 90f);
            PowerCabinet(power, "Cabinet_E", new Vector3(21.48f, 0f, 10.55f), -90f);

            // 顶上电缆桥架：横跨房间，两端埋进墙
            AddBox(power, "CableTray", new Vector3(19.5f, 5.55f, 10.1f),
                new Vector3(4.4f, 0.06f, 0.34f), Finishes.MetalDark);
            for (int i = 0; i < 4; i++)
            {
                AddBox(power, "TrayBracket_" + i, new Vector3(17.6f + i * 1.25f, 5.74f, 10.1f),
                    new Vector3(0.04f, 0.34f, 0.04f), Finishes.MetalDark);
            }

            // 从两组柜顶到桥架的导管
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? 17.62f : 21.38f;
                for (int i = 0; i < 2; i++)
                {
                    Cylinder(power, "Conduit_" + side + "_" + i, new Vector3(x + i * 0.18f, 3.85f, 10.55f),
                        0.045f, 3.35f, Finishes.MetalDark, false);
                }

                AddBox(power, "JunctionBox_" + side, new Vector3(x + 0.09f, 5.46f, 10.55f),
                    new Vector3(0.34f, 0.22f, 0.20f), Finishes.MetalDark);
            }

            // 地面绝缘垫 + 墙角水桶
            AddBox(power, "InsulationMat", new Vector3(19.55f, 0.012f, 10.65f),
                new Vector3(1.55f, 0.024f, 0.85f), Finishes.Plastic);
            Cylinder(power, "Bucket", new Vector3(21.15f, 0.22f, 11.45f), 0.32f, 0.44f, Finishes.Plastic, true);

            // 东墙警告牌：黄底黑字，手电照到很醒目。位置避开柜顶导管（z≈14.8），挪到门与柜之间
            Transform sign = NewGroup(power, "DangerSign");
            sign.position = new Vector3(PowerRoomEastX - HalfWall, 2.55f, 11.55f);
            sign.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            GameObject plate = AddBox(sign, "Plate", new Vector3(0f, 0f, 0.035f),
                new Vector3(0.76f, 0.50f, 0.05f), Finishes.WarningYellow);
            RemoveCollider(plate);

            // 细黑边框（四条），中间保留整块黄色底，黑色文字才读得清
            AddBox(sign, "Border_Top", new Vector3(0f, 0.185f, 0.064f),
                new Vector3(0.70f, 0.030f, 0.010f), Finishes.MetalDark);
            AddBox(sign, "Border_Bottom", new Vector3(0f, -0.185f, 0.064f),
                new Vector3(0.70f, 0.030f, 0.010f), Finishes.MetalDark);
            AddBox(sign, "Border_Left", new Vector3(-0.335f, 0f, 0.064f),
                new Vector3(0.030f, 0.34f, 0.010f), Finishes.MetalDark);
            AddBox(sign, "Border_Right", new Vector3(0.335f, 0f, 0.064f),
                new Vector3(0.030f, 0.34f, 0.010f), Finishes.MetalDark);
            SignText(sign, "Text", new Vector3(0f, 0.035f, 0.074f), "DANGER", 0.078f, WarningInk);
            SignText(sign, "TextSub", new Vector3(0f, -0.105f, 0.074f), "HIGH VOLTAGE", 0.038f, WarningInk);
            foreach (Collider c in sign.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }

        /// <summary>一台落地配电柜：深灰柜体、浅色柜门、通风格栅、把手和顶部指示灯。</summary>
        private static void PowerCabinet(Transform parent, string name, Vector3 basePosition, float yaw)
        {
            Transform g = NewGroup(parent, name, basePosition, yaw);
            AddBox(g, "Body", new Vector3(0f, 1.05f, 0f), new Vector3(0.74f, 2.10f, 0.52f), Finishes.MetalDark);
            AddBox(g, "Door", new Vector3(0f, 1.05f, 0.268f), new Vector3(0.62f, 1.88f, 0.018f), Finishes.Plastic);

            // 通风格栅
            for (int i = 0; i < 5; i++)
            {
                AddBox(g, "Vent_" + i, new Vector3(0f, 1.72f - i * 0.055f, 0.280f),
                    new Vector3(0.42f, 0.018f, 0.006f), Finishes.MetalDark);
            }

            // 门把手 + 锁孔
            AddBox(g, "Handle", new Vector3(0.235f, 1.05f, 0.292f), new Vector3(0.035f, 0.16f, 0.030f), Finishes.Brass);
            AddBox(g, "Lock", new Vector3(0.235f, 0.88f, 0.292f), new Vector3(0.045f, 0.07f, 0.020f), Finishes.MetalDark);

            // 柜顶三相指示灯：一红两绿
            GameObject lamp = LocalCylinder(g, "Lamp_Red", new Vector3(-0.18f, 2.19f, 0.10f), 0.05f, 0.05f, Finishes.RedLed);
            lamp.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            GameObject lamp2 = LocalCylinder(g, "Lamp_Green", new Vector3(0.10f, 2.19f, 0.10f), 0.05f, 0.05f, Finishes.ExitSign);
            lamp2.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // 柜脚
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -0.28f : 0.28f;
                float z = i < 2 ? -0.18f : 0.18f;
                AddBox(g, "Foot_" + i, new Vector3(x, 0.03f, z), new Vector3(0.08f, 0.06f, 0.08f), Finishes.MetalDark);
            }
        }

        /// <summary>门牌文字：Cinzel 衬线字（SIL OFL），单独一份 Ib/Text3D 材质，避免改到其它说明牌共用的字体图集。</summary>
        private static void SignText(Transform parent, string name, Vector3 localPos, string text, float capHeight, Color color)
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(SignFontPath);
            if (font == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 找不到门牌字体 " + SignFontPath + "，改用内置字体。");
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // TextMesh 字面朝 −Z

            TextMesh tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.fontSize = 96;
            tm.characterSize = capHeight / 6.5f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.richText = false;

            MeshRenderer r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = SignTextMaterial(font);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.AddComponent<Text3DFont>();
        }

        private static Material SignTextMaterial(Font font)
        {
            EnsureFolder(SignFontDir);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(SignTextMaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Ib/Text3D")) { name = "M_Text3D_SignSerif" };
                AssetDatabase.CreateAsset(mat, SignTextMaterialPath);
            }

            mat.mainTexture = font.material.mainTexture;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
