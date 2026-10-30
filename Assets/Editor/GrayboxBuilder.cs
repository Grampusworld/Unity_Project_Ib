using System.Collections.Generic;
using Ib.Core;
using Ib.Environment;
using Ib.Interaction;
using Ib.Player;
using Ib.Threats;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Ib.EditorTools
{
    /// <summary>
    /// 美术馆灰盒生成器（GDD §5：两层 40 × 26 m + 南墙外的黑屋，正门双开门连通）。所有尺寸都是常量，重复执行结果一致（覆盖式重建，不做增量）。
    /// 菜单：Tools/Ib/生成灰盒关卡
    /// 命令行：Unity -batchmode -quit -projectPath ... -executeMethod Ib.EditorTools.GrayboxBuilder.BuildFromCommandLine
    ///
    /// 坐标约定：原点 = 美术馆西南角地面；+X 朝东，+Z 朝北（俯视图的"上"）。
    /// 俯视图 SVG 每 15 px = 1 m，换算 X = (px - 40) / 15，Z = 26 - (py - 44) / 15。
    /// 房间边界都写墙中线；墙厚 0.3 m 一半在房间里。
    /// 场景里橙色文字 = 待灰盒试跑的默认值（GrayboxNote），改这里的常量后重新生成。
    /// </summary>
    public static partial class GrayboxBuilder
    {
        private const string ScenePath = "Assets/Scenes/Graybox.unity";

        /// <summary>
        /// 整体放大倍数（2026-10-27）：主角是 1.3 m 的孩子，馆和馆里的一切都按这个倍数放大，"在孩子眼里什么都很大"。
        /// 下面所有尺寸常量和摆放表仍按 GDD 的 1 倍值写（楼体 40 × 26 m）；生成完最后一步把场景整体放大（ApplyWorldScale），
        /// 灯的照射距离和亮度同步补偿。所以实际建成的尺寸 = 常量 × WorldScale。玩家本身不放大。
        /// </summary>
        public const float WorldScale = 1.4f;
        private const string MaterialDir = "Assets/Art/Materials";
        private const string PrefabDir = "Assets/Prefabs";
        private const string PlayerPrefabPath = PrefabDir + "/Player.prefab";
        private const string VolumeProfilePath = "Assets/Settings/GrayboxVolume.asset";

        // ================================================================ 尺寸常量（改这里就能改整个关卡）

        // ---- 楼体 ----
        private const float WallThickness = 0.3f;
        private const float HalfWall = WallThickness * 0.5f;

        private const float BuildingWidth = 40f;   // X
        private const float BuildingDepth = 26f;   // Z

        private const float Floor1Y = 0f;
        private const float Floor1Height = 6.0f;                        // 一层净高（展厅挑高，7 岁视角显得空旷）
        private const float SlabThickness = 0.4f;
        private const float Floor2Y = Floor1Height + SlabThickness;     // 6.4
        private const float Floor2Height = 5.0f;
        private const float RoofY = Floor2Y + Floor2Height;             // 11.4

        // 两条纵向分隔线（两层共用）：西列 0–13，中列 13–26，东列 26–40
        private const float WestLineX = 13f;
        private const float EastLineX = 26f;

        // ---- 一层（中列从南到北：接待厅 → 雕塑厅 → 楼梯厅）----
        private const float ReceptionNorthZ = 9f;      // 接待厅 13 × 9
        private const float StairHallSouthZ = 21f;     // 雕塑厅 13 × 12；楼梯厅 13 × 5（两层同一位置）

        // 配电间：从雕塑厅南墙切出来，门朝北正对天使（倒退进门时正面盯着它）
        private const float PowerRoomWestX = 17f;
        private const float PowerRoomEastX = 22f;
        private const float PowerRoomNorthZ = 12.2f;

        // ---- 二层 ----
        private const float DutyNorthZ = 10.6f;        // 值班室 13 × 10.6（西南角），北面是未开放区
        private const float FamilySouthZ = 8f;         // 全家福厅 14 × 11（东侧中段）
        private const float FamilyNorthZ = 19f;
        private const float ToiletNorthZ = 7f;         // 厕所 7 × 7（东南角）
        private const float ToiletEastX = 33f;

        // ---- 门洞（中心坐标沿墙方向）----
        private const float HallDoorWidth = 2.4f;
        private const float HallDoorHeight = 3.2f;
        private const float SmallDoorWidth = 1.2f;
        private const float SmallDoorHeight = 2.4f;

        private const float MainDoorX = 19.5f;         // 正门（双开），接待厅南墙正中 = 黑屋那道门
        private const float MainDoorWidth = 3.0f;
        private const float MainDoorHeight = 3.6f;
        private const float MainDoorSeam = 0.035f;     // 两扇门之间的缝，接待厅的光从这里漏进黑屋
        private const float MainDoorOpenAngle = 90f;
        private const float MainDoorOpenSeconds = 3f;

        private const float SouthDoorZ = 4.5f;         // 油画厅↔接待厅、接待厅↔服装厅
        private const float MidDoorZ = 15f;            // 油画厅↔雕塑厅、雕塑厅↔服装厅
        private const float StairArchWidth = 7f;       // 雕塑厅↔楼梯厅：大开口，楼梯中段能看到配电间
        private const float PowerDoorX = 19.5f;

        private const float DutyDoorZ = 3.0f;          // 值班室电磁锁门
        private const float LockedDoorZ = 18.5f;       // 未开放区那扇上锁的门
        private const float ToiletDoorZ = 3.3f;
        private const float FamilyDoorZ = 13.5f;
        private const float FamilyDoorWidth = 2.0f;
        private const float FamilyDoorHeight = 3.0f;
        private const float GalleryOpeningWestX = 20f; // 二层楼梯厅南侧 20–26 敞开接名画长廊

        // ---- 楼梯：双跑折返，平台在西端 ----
        // 第一跑沿北半边从东往西上到平台，第二跑沿南半边从西往东上到二层。
        private const int StairRisersPerFlight = 23;   // 放大 1.4 倍后每级 0.195 m，CharacterController 迈得上（stepOffset 0.3）
        private const float StairRise = (Floor2Y - Floor1Y) / (StairRisersPerFlight * 2);   // 0.139（× WorldScale = 0.195）
        private const float StairTread = 0.3f;
        private const float StairLandingEastX = 15.5f;
        private const float StairTopX = StairLandingEastX + (StairRisersPerFlight - 1) * StairTread;   // 19.7
        private const float StairDividerZ = 23.5f;
        private const float StairTreadThickness = 0.35f;
        private const float RailHeight = 1.0f;

        // ---- 黑屋：贴在美术馆南墙外，北墙就是美术馆南墙，那道双开门就是正门（推门直接进接待厅）----
        // 内部 z ∈ [−BlackRoomLength, 0]。馆内无阴影的点光会穿墙，所以黑屋阶段只开带阴影的接待厅灯（见 MuseumEntrance）。
        private const float BlackRoomCenterX = MainDoorX;
        private const float BlackRoomHalfWidth = 5f;
        private const float BlackRoomLength = 16f;
        private const float BlackRoomHeight = 4.5f;
        private const float BlackEaselZ = -14f;        // 画架离门 14 m（"十几米外"）
        // 空气墙窄带：5.2 m 宽（比门洞 3 m 宽），两侧各留 1.2 m 通道，玩家能绕到画布背面看空白边缘的刮痕
        private const float AirBandHalfWidth = 2.6f;
        private const float AirBandBackOffset = 2.0f;   // 画布背面到南侧空气墙：1.3 m，够转身绕过去

        // ---- 灯光 ----
        private const float RoomLightIntensity = 3.5f;    // 10-27 改成"底光 + 每幅画一盏射灯"，底光调暗拉开对比
        private const float RoomLightRange = 14f;

        // ---- 玩家（7 岁身高，§3.1）----
        private const float PlayerHeight = 1.3f;
        private const float PlayerEyeHeight = 1.15f;

        private static readonly Opening MainDoor = new Opening(MainDoorX, MainDoorWidth, MainDoorHeight);
        private static readonly Opening DutyDoor = new Opening(DutyDoorZ, SmallDoorWidth, SmallDoorHeight);
        private static readonly Opening LockedDoor = new Opening(LockedDoorZ, SmallDoorWidth, SmallDoorHeight);

        private sealed class Palette
        {
            public Material Floor;
            public Material Wall;
            public Material Ceiling;
            public Material Stair;
            public Material Prop;
            public Material Key;
            public Material Exhibit;
            public Material Threat;
            public Material Glass;
            public Material Door;
            public Material Black;
            public Material DoorLight;
            public Material SeamGlow;
            public Material Skylight;
        }

        private readonly struct Opening
        {
            public readonly float Center;
            public readonly float Width;
            public readonly float Height;

            public Opening(float center, float width, float height)
            {
                Center = center;
                Width = width;
                Height = height;
            }

            public float Min => Center - Width * 0.5f;

            public float Max => Center + Width * 0.5f;
        }

        [MenuItem("Tools/Ib/生成灰盒关卡")]
        public static void BuildFromMenu()
        {
            bool ok = EditorUtility.DisplayDialog(
                "生成灰盒关卡",
                "将重建 " + ScenePath + "（覆盖已有内容）并重新生成 Player 预制体。\n\n" +
                "手改过的场景内容会丢失，继续？",
                "生成", "取消");

            if (!ok)
            {
                return;
            }

            Build();
            EditorUtility.DisplayDialog("完成", "灰盒关卡已生成：\n" + ScenePath + "\n\nPlay 后 F1–F9 切换检查点。", "好");
        }

        public static void BuildFromCommandLine()
        {
            Build();
        }

        // 不弹确认框的版本，给 MCP / 自动化调用（弹窗会卡住编辑器）
        [MenuItem("Tools/Ib/生成灰盒关卡（无确认）")]
        public static void BuildWithoutPrompt()
        {
            Build();
        }

        private static void Build()
        {
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Art");
            EnsureFolder(MaterialDir);
            EnsureFolder(PrefabDir);

            LoadFinishes();
            Palette p = LoadPalette();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Transform museum = new GameObject("Level_Museum").transform;
            BuildShell(museum, p);
            BuildWalls1F(museum, p);
            BuildWalls2F(museum, p);
            BuildStairs(museum, p);
            MarkStatic(museum);
            BuildStairPowerOutTrigger();

            Transform props = new GameObject("Props_Museum").transform;
            BuildProps1F(props, p);
            BuildProps2F(props, p);

            BuildThreatPlaceholders(p);
            BuildRoomNotes();

            // 名画 / 雕像 / 道具模型 / 博物馆杂项（GrayboxBuilder.Dressing.cs），要在建筑细节之前放，细节会绕开它们
            List<Exhibit> exhibits = PlaceArtworks();
            exhibits.AddRange(PlaceDressing(props, p));
            exhibits.AddRange(DressBlankBoards(props));
            exhibits.AddRange(CollectPanelExhibits(props));

            // 踢脚线 / 护墙板 / 墙裙 / 挂镜线 / 檐口 / 壁柱 / 门套 / 梁 / 柱 / 地面拼花（GrayboxBuilder.Detail.cs）
            BuildArchitectureDetail(museum, p);

            // 画中馆人群站位（白色圆柱，GrayboxBuilder.Crowd.cs）：要等墙、道具、展品都放好，按碰撞找空位
            BuildCrowd(exhibits, museum);
            // 文本收集物 T1–T16（GDD §6.11.2）：放在人群之后，新加的碰撞体不会挤动已有的人群站位
            BuildTextPickups(props, p);
            BuildRopedMannequins(props);
            CheckDoorClearance(museum);

            GameObject blackRoom = BuildBlackRoom(p);
            DoubleDoor mainDoor = BuildMainDoor(p);

            BuildSpawnsAndPlayer(scene);
            GameObject deferredLights = BuildLighting();
            AddExhibitLights(deferredLights.transform, exhibits);
            BuildAccentLights(deferredLights.transform);
            BuildDustMotes(deferredLights);
            BuildMuseumEntrance(mainDoor, blackRoom, deferredLights);
            BuildPostProcessing();
            BuildEnvironmentSettings();

            AuditLightFixtures();
            ApplyWorldScale(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 天使模型、巡逻路线、导航网格、玩家手电：重建场景后都会丢，这里接着补上（它自己会存场景）
            WeepingAngelSetup.Setup();
            // 后半段的电话、正门锁、工作卡、抽屉、手电落地（GrayboxBuilder.SecondHalf.cs，会再存一次场景）
            SetupSecondHalf();

            Debug.Log("[GrayboxBuilder] 完成：" + ScenePath);
        }

        private static Palette LoadPalette()
        {
            return new Palette
            {
                // 墙 / 地 / 天花 / 楼梯换成带程序贴图的材质（GrayboxBuilder.Materials.cs），世界坐标 UV，不随方块拉伸
                Floor = Finishes.Parquet,
                Wall = Finishes.PlasterBase,
                Ceiling = Finishes.Ceiling,
                Stair = Finishes.StoneWorn,
                Prop = GetOrCreateMaterial("M_GB_Prop", new Color(0.30f, 0.28f, 0.25f), 0.2f),
                Key = GetOrCreateMaterial("M_GB_KeyItem", new Color(0.85f, 0.55f, 0.15f), 0.3f),
                Exhibit = GetOrCreateMaterial("M_GB_Exhibit", new Color(0.62f, 0.58f, 0.50f), 0.1f),
                Threat = GetOrCreateMaterial("M_GB_Threat", new Color(0.55f, 0.12f, 0.12f), 0.25f),
                Glass = GetOrCreateMaterial("M_GB_Glass", new Color(0.45f, 0.58f, 0.65f), 0.92f),
                Door = GetOrCreateMaterial("M_GB_Door", new Color(0.26f, 0.17f, 0.10f), 0.3f),
                Black = GetOrCreateMaterial("M_BlackRoom", new Color(0.012f, 0.012f, 0.014f), 0.05f),
                DoorLight = GetOrCreateMaterial("M_GB_DoorLight", Color.white, 0f, 4f),
                SeamGlow = GetOrCreateMaterial("M_GB_SeamGlow", new Color(1f, 0.86f, 0.70f), 0f, 1.0f),
                Skylight = GetOrCreateMaterial("M_GB_Skylight", new Color(0.55f, 0.65f, 0.8f), 0f, 1.5f),
            };
        }

        // ================================================================ 楼体：地面、楼板、屋顶

        private static void BuildShell(Transform parent, Palette p)
        {
            Transform root = NewGroup(parent, "Shell");

            float x0 = -HalfWall;
            float x1 = BuildingWidth + HalfWall;
            float z0 = -HalfWall;
            float z1 = BuildingDepth + HalfWall;

            AddSlab(root, "Floor_1F", x0, x1, z0, z1, Floor1Y - 0.3f, Floor1Y, p.Floor);

            // 层间楼板：下 5 cm 当一层天花，上面当二层地面；楼梯井开洞
            const float ceilingSkin = 0.05f;
            AddSlabWithStairwell(root, "Ceiling_1F", Floor1Height, Floor1Height + ceilingSkin, p.Ceiling);
            AddSlabWithStairwell(root, "Floor_2F", Floor1Height + ceilingSkin, Floor2Y, p.Floor);

            AddSlab(root, "Roof", x0, x1, z0, z1, RoofY, RoofY + 0.3f, p.Ceiling);
        }

        private static void AddSlabWithStairwell(Transform parent, string name, float yMin, float yMax, Material mat)
        {
            Transform root = NewGroup(parent, name);

            float x0 = -HalfWall;
            float x1 = BuildingWidth + HalfWall;
            float z0 = -HalfWall;
            float z1 = BuildingDepth + HalfWall;

            // 洞口 = 楼梯平台 + 两跑楼梯的范围
            float holeX0 = WestLineX + HalfWall;
            float holeX1 = StairTopX;
            float holeZ0 = StairHallSouthZ + HalfWall;
            float holeZ1 = BuildingDepth - HalfWall;

            AddSlab(root, "South", x0, x1, z0, holeZ0, yMin, yMax, mat);
            AddSlab(root, "North", x0, x1, holeZ1, z1, yMin, yMax, mat);
            AddSlab(root, "West", x0, holeX0, holeZ0, holeZ1, yMin, yMax, mat);
            AddSlab(root, "East", holeX1, x1, holeZ0, holeZ1, yMin, yMax, mat);
        }

        // ================================================================ 一层墙体

        private static void BuildWalls1F(Transform parent, Palette p)
        {
            Transform root = NewGroup(parent, "Walls_1F");
            float y = Floor1Y;
            float h = Floor1Height;
            Material m = p.Wall;

            // 外墙
            WallAlongX(root, "Ext_South", 0f, 0f, BuildingWidth, true, true, y, h, m, MainDoor);
            WallAlongX(root, "Ext_North", BuildingDepth, 0f, BuildingWidth, true, true, y, h, m);
            WallAlongZ(root, "Ext_West", 0f, 0f, BuildingDepth, y, h, m);
            WallAlongZ(root, "Ext_East", BuildingWidth, 0f, BuildingDepth, y, h, m);

            // 西线：油画厅 | 接待厅 / 雕塑厅 / 楼梯厅
            WallAlongZ(root, "Line_West", WestLineX, 0f, BuildingDepth, y, h, m,
                new Opening(SouthDoorZ, HallDoorWidth, HallDoorHeight),
                new Opening(MidDoorZ, HallDoorWidth, HallDoorHeight));

            // 东线：接待厅 / 雕塑厅 / 楼梯厅 | 服装厅（服装厅的"入口墙"，不挂衣服）
            WallAlongZ(root, "Line_East", EastLineX, 0f, BuildingDepth, y, h, m,
                new Opening(SouthDoorZ, HallDoorWidth, HallDoorHeight),
                new Opening(MidDoorZ, HallDoorWidth, HallDoorHeight));

            // 接待厅 | 雕塑厅：不开门，去楼梯必须绕油画厅或服装厅再穿过天使
            WallAlongX(root, "Reception_North", ReceptionNorthZ, WestLineX, EastLineX, false, false, y, h, m);

            // 雕塑厅 | 楼梯厅：通高大开口
            WallAlongX(root, "Sculpture_North", StairHallSouthZ, WestLineX, EastLineX, false, false, y, h, m,
                new Opening(MainDoorX, StairArchWidth, h));

            // 配电间
            WallAlongZ(root, "PowerRoom_West", PowerRoomWestX, ReceptionNorthZ, PowerRoomNorthZ, y, h, m);
            WallAlongZ(root, "PowerRoom_East", PowerRoomEastX, ReceptionNorthZ, PowerRoomNorthZ, y, h, m);
            WallAlongX(root, "PowerRoom_North", PowerRoomNorthZ, PowerRoomWestX, PowerRoomEastX, true, true, y, h, m,
                new Opening(PowerDoorX, SmallDoorWidth, SmallDoorHeight));
        }

        // ================================================================ 二层墙体

        private static void BuildWalls2F(Transform parent, Palette p)
        {
            Transform root = NewGroup(parent, "Walls_2F");
            float y = Floor2Y;
            float h = Floor2Height;
            Material m = p.Wall;

            WallAlongX(root, "Ext_South", 0f, 0f, BuildingWidth, true, true, y, h, m);
            WallAlongX(root, "Ext_North", BuildingDepth, 0f, BuildingWidth, true, true, y, h, m);
            WallAlongZ(root, "Ext_West", 0f, 0f, BuildingDepth, y, h, m);
            WallAlongZ(root, "Ext_East", BuildingWidth, 0f, BuildingDepth, y, h, m);

            // 西线：值班室 / 未开放（西北） | 名画长廊 / 楼梯厅
            WallAlongZ(root, "Line_West", WestLineX, 0f, BuildingDepth, y, h, m, DutyDoor, LockedDoor);
            WallAlongX(root, "Duty_North", DutyNorthZ, 0f, WestLineX, false, false, y, h, m);

            // 东线：名画长廊 / 楼梯厅 | 厕所 / 全家福厅 / 未开放（东北）
            WallAlongZ(root, "Line_East", EastLineX, 0f, BuildingDepth, y, h, m,
                new Opening(ToiletDoorZ, SmallDoorWidth, SmallDoorHeight),
                new Opening(FamilyDoorZ, FamilyDoorWidth, FamilyDoorHeight));

            WallAlongX(root, "Toilet_North", ToiletNorthZ, EastLineX, ToiletEastX, false, false, y, h, m);
            WallAlongZ(root, "Toilet_East", ToiletEastX, 0f, FamilySouthZ, y, h, m);
            WallAlongX(root, "Family_South", FamilySouthZ, EastLineX, BuildingWidth, false, false, y, h, m);
            WallAlongX(root, "Family_North", FamilyNorthZ, EastLineX, BuildingWidth, false, false, y, h, m);

            // 楼梯厅 | 名画长廊：西段是楼梯井边的墙，东段敞开，上来右转就是 21 m 直廊
            WallAlongX(root, "StairHall_South", StairHallSouthZ, WestLineX, GalleryOpeningWestX, false, false, y, h, m);
        }

        // ================================================================ 楼梯

        private static void BuildStairs(Transform parent, Palette p)
        {
            Transform root = NewGroup(parent, "Stairs");

            float southZ0 = StairHallSouthZ + HalfWall;    // 21.15
            float northZ1 = BuildingDepth - HalfWall;      // 25.85
            float dividerZ0 = StairDividerZ - 0.1f;
            float dividerZ1 = StairDividerZ + 0.1f;
            float landingY = StairRisersPerFlight * StairRise;   // 2.7

            // 第一跑（北半边，自东向西），实心到地
            Transform flight1 = NewGroup(root, "Flight_1");
            for (int k = 1; k < StairRisersPerFlight; k++)
            {
                float xMax = StairTopX - (k - 1) * StairTread;
                float xMin = StairTopX - k * StairTread;
                AddSlab(flight1, "Step_" + k, xMin, xMax, dividerZ1, northZ1, Floor1Y, Floor1Y + k * StairRise, p.Stair);
            }

            AddSlab(root, "Landing", WestLineX + HalfWall, StairLandingEastX, southZ0, northZ1,
                Floor1Y, Floor1Y + landingY, p.Stair);

            // 第二跑（南半边，自西向东），悬空踏板，下面能走人；南侧临雕塑厅开口，加栏杆
            Transform flight2 = NewGroup(root, "Flight_2");
            for (int k = 1; k < StairRisersPerFlight; k++)
            {
                float xMin = StairLandingEastX + (k - 1) * StairTread;
                float xMax = StairLandingEastX + k * StairTread;
                float top = Floor1Y + landingY + k * StairRise;
                AddSlab(flight2, "Step_" + k, xMin, xMax, southZ0, dividerZ0, top - StairTreadThickness, top, p.Stair);
                // 用户 10-28 手调：栏杆换石材；最上面几段截到二层地面以下，不从楼板里冒出来
                float railTop = Mathf.Min(top + RailHeight, Floor2Y - 0.009f);
                AddSlab(flight2, "Rail_" + k, xMin, xMax, southZ0, southZ0 + 0.08f, top, railTop, Finishes.StoneWorn);
            }

            // 两跑之间的隔墙，顶到二层栏杆高度
            AddSlab(root, "Divider", StairLandingEastX, StairTopX, dividerZ0, dividerZ1, Floor1Y, Floor2Y + RailHeight, p.Wall);

            // 二层井口栏杆（第一跑上方）
            AddSlab(root, "Rail_2F_Well", StairTopX, StairTopX + 0.08f, dividerZ1, northZ1, Floor2Y, Floor2Y + RailHeight, p.Prop);
        }

        // ================================================================ 一层道具占位

        private static void BuildProps1F(Transform parent, Palette p)
        {
            Transform root = NewGroup(parent, "1F");
            float y = Floor1Y;

            // ---- 接待大厅 ----
            Transform reception = NewGroup(root, "Reception");
            // 前台（柜身、台面、抽屉 C、电话、登记簿）见 GrayboxBuilder.Models.cs
            BuildFrontDesk(reception, 19.5f, y, 6.9f);
            AddWallPanel(reception, "Intro_Wall_TheAudience", new Vector3(19.5f, y + 2.4f, ReceptionNorthZ - HalfWall),
                Vector3.back, 5.0f, 2.6f, p.Exhibit);
            AddWallPanel(reception, "Grampus_IDPhoto", new Vector3(23.0f, y + 1.9f, ReceptionNorthZ - HalfWall),
                Vector3.back, 0.5f, 0.65f, p.Exhibit);
            // 正门本身是可开的双开门，见 BuildMainDoor

            // ---- 油画厅：真名画（PlaceArtworks）已经挂满两面长墙；占位板只留手工调整后剩下的两块，藏在真画后面 ----
            Transform oil = NewGroup(root, "OilHall");
            AddWallPanel(oil, "Painting_NightSea", new Vector3(HalfWall, y + 2.64f, 8.94f), Vector3.right, 2.2f, 1.7f, p.Exhibit);
            AddWallPanel(oil, "Painting_BridgeInRain", new Vector3(WestLineX - HalfWall, y + 2.4f, 9.75f), Vector3.left, 2.2f, 1.7f, p.Exhibit);

            // ---- 雕塑厅：四角雕塑 + 监控摄像头 + 假天窗 ----
            Transform sculpture = NewGroup(root, "SculptureHall");
            Vector3 angelPos = SculptureCenter();
            float[,] corners = { { 14.8f, 10.8f }, { 24.2f, 10.8f }, { 14.8f, 19.4f }, { 24.2f, 19.4f } };
            string[] cornerNames = { "TheLanternBearer", "Moai", "Untitled_I", "Untitled_II" };
            for (int i = 0; i < 2; i++)
            {
                Transform s = NewGroup(sculpture, "Sculpture_" + cornerNames[i]);
                AddProp(s, "Plinth", corners[i, 0], y, corners[i, 1], 1.0f, 0.6f, 1.0f, Finishes.StoneWorn);
                // 雕像面朝天使所在的大厅中央（GrayboxBuilder.Models.cs）
                Vector3 toCenter = angelPos - new Vector3(corners[i, 0], y, corners[i, 1]);
                float faceYaw = Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg;
                Vector3 top = new Vector3(corners[i, 0], y + 0.6f, corners[i, 1]);
                // 东南角原来的《跪者》换成了摩艾石像（Artworks 表），这里只留底座
                if (i == 0)
                {
                    LanternBearer(s, top, faceYaw);
                }
            }

            // 北侧两角：底座放大，上面是《蒙娜丽莎》胸像和自由女神像（PlaceArtworks），占位雕塑不要了
            float[,] bigPlinths = { { 14.89f, 18.81f }, { 24.2f, 19.4f } };
            for (int i = 0; i < 2; i++)
            {
                Transform s = NewGroup(sculpture, "Sculpture_" + cornerNames[i + 2]);
                AddProp(s, "Plinth", bigPlinths[i, 0], y - 0.15f, bigPlinths[i, 1], 1.5f, 0.9f, 1.5f, Finishes.StoneWorn);
            }

            GameObject cam = AddBox(sculpture, "SecurityCamera_Sculpture", new Vector3(13.5f, y + 4.4f, 20.5f),
                new Vector3(0.25f, 0.2f, 0.45f), p.Key);
            cam.transform.LookAt(angelPos + Vector3.up * 1.2f);
            RemoveCollider(cam);

            GameObject skylight = AddSlab(sculpture, "Skylight_Fake", 17.5f, 21.5f, 13f, 17f,
                Floor1Height - 0.04f, Floor1Height - 0.005f, p.Skylight);
            RemoveCollider(skylight);

            AddProp(sculpture, "Pickup_CandyWrapper", 24.9f, y, 10.0f, 0.12f, 0.01f, 0.08f, p.Key);

            // ---- 配电间 ----
            Transform power = NewGroup(root, "PowerRoom");
            AddProp(power, "B_SecurityCabinet", PowerDoorX, y, ReceptionNorthZ + HalfWall + 0.3f, 1.4f, 1.8f, 0.6f, p.Key);
            AddProp(power, "Breaker_Panel", PowerRoomEastX - HalfWall - 0.15f, y + 1.0f, 10.8f, 0.3f, 1.2f, 0.9f, p.Prop);

            // ---- 楼梯厅：原来的三幅小稿位置挂了《麦田群鸦》（PlaceArtworks）----

            // ---- 服装厅：三面挂衣 16 件、三组玻璃罩人台、Lily 的小裙子 ----
            Transform costume = NewGroup(root, "CostumeHall");
            BuildGarmentRails(costume, y);

            // 第 3 组往东挪 0.6 m：给接待厅 ↔ 服装厅的门留出 3 m 进深
            float[,] cases = { { 30.5f, 19.3f }, { 34.8f, 11.8f }, { 30.4f, 4.3f } };
            for (int i = 0; i < 3; i++)
            {
                MannequinCase(costume, "GlassCase_Mannequins_" + (i + 1), cases[i, 0], y, cases[i, 1], i);
            }

            LilyCase(costume, 38.1f, y, 2.3f);

            GameObject hopscotch = AddProp(costume, "Pickup_ChalkHopscotch", 31.5f, y, 14.0f, 0.9f, 0.006f, 2.6f, p.Key);
            RemoveCollider(hopscotch);
        }

        // ================================================================ 二层道具占位

        private static void BuildProps2F(Transform parent, Palette p)
        {
            Transform root = NewGroup(parent, "2F");
            float y = Floor2Y;

            // ---- 名画长廊：6 幅名画（2 幅停电后变空框）+ 5 组展柜 ----
            Transform gallery = NewGroup(root, "Gallery");
            float[] westZ = { 16.5f, 12.67f, 8.93f };
            float[] eastZ = { 16.5f, 11.47f, 6.93f };
            for (int i = 0; i < 3; i++)
            {
                string westName = i == 0 ? "Masterpiece_W1_EmptyFrameA" : "Masterpiece_W" + (i + 1);
                string eastName = i == 2 ? "Masterpiece_E3_EmptyFrameB" : "Masterpiece_E" + (i + 1);
                AddWallPanel(gallery, westName, new Vector3(WestLineX + HalfWall, y + 2.2f, westZ[i]),
                    Vector3.right, 1.6f, 2.0f, p.Exhibit);
                AddWallPanel(gallery, eastName, new Vector3(EastLineX - HalfWall, y + 2.2f, eastZ[i]),
                    Vector3.left, 1.6f, 2.0f, p.Exhibit);
            }

            // 第 2 组从 z 14.07 挪到 16.8：原来正挡在全家福厅门口（门 z 12.5–14.5）
            float[,] cases = { { 15.6f, 17.27f }, { 23.2f, 16.8f }, { 15.6f, 11.27f }, { 23.2f, 9.27f }, { 15.6f, 6.47f } };
            for (int i = 0; i < 5; i++)
            {
                GalleryVitrine(gallery, "DisplayCase_Cover_" + (i + 1), cases[i, 0], y, cases[i, 1], i);
            }

            AddProp(gallery, "Pickup_StudioTicket", 23.2f, y + 1.4f, 9.27f, 0.18f, 0.01f, 0.1f, p.Key);   // 展柜玻璃罩顶上
            AddWallPanel(gallery, "Sketch_LilyLooksAtDoor", new Vector3(WestLineX + HalfWall, y + 1.4f, DutyDoorZ + 1.4f),
                Vector3.right, 0.6f, 0.8f, p.Exhibit);
            // 停电（≥ PowerOut）时电磁锁门滑开（Interfaces.md 场景切换与检查点）。门板在 x = 13 的墙里、沿 Z 宽 1.2 m，
            // 往南（−Z）滑整整一个门宽，缩进 z 1.2–2.4 的墙体，门洞全空出来。slide 是世界位移，要乘 WorldScale
            GameObject dutyDoor = AddDoorPanel(gallery, "Door_Duty_MagLock", false, WestLineX, DutyDoor, y, p.Door);
            SetVector3(dutyDoor.AddComponent<MagLockDoor>(), "slide", new Vector3(0f, 0f, -SmallDoorWidth * WorldScale));
            AddDoorPanel(gallery, "Door_Closed_Locked", false, WestLineX, LockedDoor, y, p.Door);

            // ---- 全家福厅：只有两件 ----
            Transform family = NewGroup(root, "FamilyRoom");
            AddEasel(family, "FamilyPortrait_Easel", new Vector3(30.5f, y, FamilyDoorZ), -90f, p.Prop, p.Exhibit);
            // 第 6 步：PaintedGallery 时对全家福按 E 传回黑屋；碰撞体用画架子物体的（交互检测 GetComponentInParent）
            FindDeep(family, "FamilyPortrait_Easel").gameObject.AddComponent<FamilyPortraitExit>();
            AddProp(family, "Pickup_FlowerAndNote", 30.1f, y, FamilyDoorZ + 0.2f, 0.25f, 0.05f, 0.18f, p.Key);
            AddWallPanel(family, "Painting_TheMuseum_ExitFrame", new Vector3(37.0f, y + 0.3f + 0.9f, FamilyNorthZ - HalfWall),
                Vector3.back, 2.5f, 1.8f, p.Exhibit);

            // ---- 值班室 ----
            Transform duty = NewGroup(root, "DutyRoom");
            // 第 14 步：停电且有手电时按 E 查看监控 → AngelsActive；AddProp 的方块自带 BoxCollider
            AddProp(duty, "A_MonitorWall", 6.5f, y + 0.5f, DutyNorthZ - HalfWall - 0.25f, 4.0f, 1.3f, 0.4f, p.Key)
                .AddComponent<MonitorTerminal>();
            AddProp(duty, "DutyDesk", 6.5f, y, 9.4f, 3.6f, 0.75f, 0.9f, p.Prop);
            AddProp(duty, "A_HandoverLog", 5.8f, y + 0.75f, 9.3f, 0.4f, 0.04f, 0.3f, p.Key);

            // ---- 厕所 ----
            Transform toilet = NewGroup(root, "Toilet");
            AddWallPanel(toilet, "Mirror", new Vector3(29.0f, y + 1.45f, ToiletNorthZ - HalfWall), Vector3.back, 3.6f, 1.0f, p.Glass);
            BuildToiletFixtures(toilet, y);   // 隔间、洗手台、小便斗等，见 GrayboxBuilder.Models.cs
            AddProp(toilet, "Shelf", 31.0f, y, HalfWall + 0.25f, 1.2f, 0.9f, 0.5f, p.Prop);
            // 手电：按 E 拿起（GDD §3.6 第 12 步），逻辑在 Gameplay 的 FlashlightPickup，靠自身 BoxCollider 被准星命中。
            // 躺在地上、离门洞 2 m（SetupSecondHalf 会再摆一次，两处保持一致）
            AddProp(toilet, "Pickup_Flashlight", FlashlightFloorX, y, ToiletDoorZ, 0.28f, 0.08f, 0.08f, p.Key)
                .AddComponent<FlashlightPickup>();
        }

        // ================================================================ 怪物占位（无碰撞，只看站位）

        /// <summary>第 10 步：楼梯中段（平台）断电。触发盒盖满整个平台（两跑全宽），平台面往上 2 m。</summary>
        private static void BuildStairPowerOutTrigger()
        {
            Transform root = new GameObject("Triggers_Flow").transform;
            float landingTop = Floor1Y + StairRisersPerFlight * StairRise;
            float x0 = WestLineX + HalfWall;
            float z0 = StairHallSouthZ + HalfWall;
            float z1 = BuildingDepth - HalfWall;
            AddFlowTrigger(root, "Trigger_PowerOut_Stairs", new Vector3((x0 + StairLandingEastX) * 0.5f, landingTop + 1f, (z0 + z1) * 0.5f),
                new Vector3(StairLandingEastX - x0, 2f, z1 - z0), StateTrigger.Step.PowerOut);
        }

        /// <summary>状态触发盒：空物体 + isTrigger BoxCollider（不渲染、不挡人），NavMesh 烘焙忽略它。</summary>
        private static void AddFlowTrigger(Transform parent, string name, Vector3 center, Vector3 size, StateTrigger.Step step)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.AddComponent<BoxCollider>().isTrigger = true;
            go.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            StateTrigger trigger = go.AddComponent<StateTrigger>();
            SerializedObject so = new SerializedObject(trigger);
            so.FindProperty("step").enumValueIndex = (int)step;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildThreatPlaceholders(Palette p)
        {
            Transform root = new GameObject("Threats_Placeholder").transform;

            // 哭泣天使：正中央，背对入口（西门）→ 面朝东
            Transform angel = NewGroup(root, "Threat_WeepingAngel", SculptureCenter(), 90f);
            AddBox(angel, "Plinth", new Vector3(0f, 0.2f, 0f), new Vector3(1.2f, 0.4f, 1.2f), p.Prop);
            AddBox(angel, "Body", new Vector3(0f, 0.4f + 0.95f, 0f), new Vector3(0.6f, 1.9f, 0.5f), p.Threat);
            GameObject face = AddBox(angel, "Face_Forward", new Vector3(0f, 2.0f, 0.3f), new Vector3(0.3f, 0.2f, 0.1f), p.Key);
            RemoveCollider(face);

            // 人偶 ×3：各一条独立往返路线（俯视图蓝色虚线）
            // Top 的 A 点从 x 28.7 东移到 29.45：服装厅西北角新加了一圈绳子（x 28.78），28.78 + 立柱底座半径 0.14 + 人台底座半径 0.21 + 余量 0.3
            AddMannequinPath(root, "Mannequin_Top", new Vector3(29.45f, Floor1Y, 22.8f), new Vector3(35.9f, Floor1Y, 22.8f), p);
            AddMannequinPath(root, "Mannequin_Right", new Vector3(37.3f, Floor1Y, 23.5f), new Vector3(37.3f, Floor1Y, 5.1f), p);
            AddMannequinPath(root, "Mannequin_Bottom", new Vector3(28.7f, Floor1Y, 6.5f), new Vector3(36.1f, Floor1Y, 6.5f), p);

            // 名画人物 ×2：站在各自的空框前（拿起手电后才出现）
            // 挂 PaintingFigure（总监 10-30）：只有出动开关；占位胶囊没有子物体，stillVisual/outVisual 不填
            AddCapsule(root, "PaintingFigure_A", new Vector3(14.2f, Floor2Y, 16.5f), 1.75f, 0.28f, p.Threat, false)
                .AddComponent<PaintingFigure>();
            AddCapsule(root, "PaintingFigure_B", new Vector3(24.8f, Floor2Y, 6.93f), 1.75f, 0.28f, p.Threat, false)
                .AddComponent<PaintingFigure>();
        }

        private static void AddMannequinPath(Transform parent, string name, Vector3 a, Vector3 b, Palette p)
        {
            GameObject pathGO = new GameObject(name + "_Path");
            pathGO.transform.SetParent(parent, false);
            WaypointPath waypoints = pathGO.AddComponent<WaypointPath>();

            GameObject wa = new GameObject("A");
            wa.transform.SetParent(pathGO.transform, false);
            wa.transform.position = a;

            GameObject wb = new GameObject("B");
            wb.transform.SetParent(pathGO.transform, false);
            wb.transform.position = b;

            // 会动的人台：和玻璃罩里的同一款，没有衣服、没有碰撞（只看站位），面朝路线方向
            Vector3 dir = b - a;
            Transform mannequin = Mannequin(parent, name, a, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 1f, Outfit.None, null);
            Ib.Threats.MannequinPatrol patrol = mannequin.gameObject.AddComponent<Ib.Threats.MannequinPatrol>();
            SetRef(patrol, "path", waypoints);
        }

        // ================================================================ 房间标签与待定项（Scene 视图可见）

        private static void BuildRoomNotes()
        {
            Transform root = new GameObject("--- Notes ---").transform;
            float y1 = Floor1Y + 0.1f;
            float y2 = Floor2Y + 0.1f;

            AddNote(root, "Room_Reception", new Vector3(16.0f, y1, 4.5f), "接待大厅 13×9", false);
            AddNote(root, "Room_OilHall", new Vector3(6.5f, y1, 13f), "油画厅 13×26（7 幅）", false);
            AddNote(root, "Room_Sculpture", new Vector3(16.0f, y1, 17.5f), "雕塑厅 13×12（天使正中）", false);
            AddNote(root, "Room_PowerRoom", new Vector3(19.5f, y1, 11.2f), "配电间 5×3.2 · B 保安柜", false);
            AddNote(root, "Room_StairHall_1F", new Vector3(23.0f, y1, 23.5f), "楼梯厅 13×5", false);
            AddNote(root, "Room_Costume", new Vector3(33f, y1, 13f), "服装厅 14×26（挂衣 16）", false);

            AddNote(root, "Room_Gallery", new Vector3(19.5f, y2, 12f), "名画长廊 13×21（展柜 5）", false);
            AddNote(root, "Room_Family", new Vector3(33f, y2, 13.5f), "全家福厅 14×11", false);
            AddNote(root, "Room_Duty", new Vector3(6.5f, y2, 5f), "值班室 13×10.6 · A 监控屏", false);
            AddNote(root, "Room_Toilet", new Vector3(28.5f, y2, 3.5f), "厕所 7×7 · 手电", false);
            AddNote(root, "Room_Closed_NW", new Vector3(6.5f, y2, 18f), "未开放（西北）", false);
            AddNote(root, "Room_Closed_NE", new Vector3(33f, y2, 22.5f), "未开放（东北）", false);
            AddNote(root, "Room_Closed_SE", new Vector3(36.5f, y2, 4f), "未开放（东南）", false);

            AddNote(root, "TODO_StairLayout", new Vector3(14.5f, Floor1Y + 3f, 22.5f),
                "待定：楼梯厅两层都放在北端（二层图为准），\n一层配电间改到雕塑厅南墙", true);
            AddNote(root, "TODO_Skylight", new Vector3(19.5f, Floor1Height - 0.6f, 15f),
                "待定：雕塑厅天窗上方是二层长廊，先用发光板假装", true);
            AddNote(root, "TODO_Connections", new Vector3(26f, y1 + 3.6f, 10f),
                "待定：服装厅西墙开两扇门（接待厅、雕塑厅）", true);
            AddNote(root, "TODO_BackupRoute", new Vector3(19.5f, y1 + 3f, 9.6f),
                "待定：配电间备用后路（§4.3）未做，先看倒退难度", true);
            AddNote(root, "TODO_MonsterValues", new Vector3(31f, y1 + 3f, 22.8f),
                "待定：人偶速度 / 名画人物视野 / 放弃时间（Q3）", true);
            AddNote(root, "TODO_Toilet", new Vector3(32f, y2 + 2.2f, 4.4f),
                "待定：隔间只有隔板，没做门", true);
        }

        // ================================================================ 黑屋（序章 + 出画）

        private static GameObject BuildBlackRoom(Palette p)
        {
            Transform root = new GameObject("Level_BlackRoom").transform;
            Transform shell = NewGroup(root, "Shell");

            float cx = BlackRoomCenterX;
            float westLine = cx - BlackRoomHalfWidth - HalfWall;
            float eastLine = cx + BlackRoomHalfWidth + HalfWall;
            float southLine = -BlackRoomLength - HalfWall;
            float northFace = -HalfWall;   // 美术馆南墙的外表面
            float h = BlackRoomHeight;

            AddSlab(shell, "Floor", westLine - HalfWall, eastLine + HalfWall, southLine - HalfWall, northFace, -0.3f, 0f, p.Black);
            AddSlab(shell, "Ceiling", westLine - HalfWall, eastLine + HalfWall, southLine - HalfWall, northFace, h, h + 0.3f, p.Black);
            WallAlongX(shell, "Wall_South", southLine, westLine, eastLine, true, true, 0f, h, p.Black);
            WallAlongZ(shell, "Wall_West", westLine, southLine, 0f, 0f, h, p.Black);
            WallAlongZ(shell, "Wall_East", eastLine, southLine, 0f, 0f, h, p.Black);

            // 北墙借用美术馆南墙；贴一层 2 cm 黑皮，免得黑屋这一面露出水泥色
            const float skin = 0.02f;
            float innerW = westLine + HalfWall;
            float innerE = eastLine - HalfWall;
            AddSlab(shell, "NorthSkin_West", innerW, MainDoor.Min, northFace - skin, northFace, 0f, h, p.Black);
            AddSlab(shell, "NorthSkin_East", MainDoor.Max, innerE, northFace - skin, northFace, 0f, h, p.Black);
            AddSlab(shell, "NorthSkin_Lintel", MainDoor.Min, MainDoor.Max, northFace - skin, northFace, MainDoorHeight, h, p.Black);
            MarkStatic(shell);

            // 第二次回到黑屋：门的位置换成空画框（默认隐藏，状态切换时打开）
            Transform frame = NewGroup(root, "EmptyFrame_SecondVisit");
            float fw = 2.5f;
            float fh = 1.8f;
            float fb = 0.3f;
            float fz0 = northFace - skin - 0.12f;
            float fz1 = northFace - skin - 0.02f;
            AddSlab(frame, "Frame_Bottom", cx - fw * 0.5f, cx + fw * 0.5f, fz0, fz1, fb - 0.12f, fb, p.Key);
            AddSlab(frame, "Frame_Top", cx - fw * 0.5f, cx + fw * 0.5f, fz0, fz1, fb + fh, fb + fh + 0.12f, p.Key);
            AddSlab(frame, "Frame_Left", cx - fw * 0.5f - 0.12f, cx - fw * 0.5f, fz0, fz1, fb - 0.12f, fb + fh + 0.12f, p.Key);
            AddSlab(frame, "Frame_Right", cx + fw * 0.5f, cx + fw * 0.5f + 0.12f, fz0, fz1, fb - 0.12f, fb + fh + 0.12f, p.Key);
            // 第 7 步：走进空画框 → AfterHours。触发盒盖住框内（框里 + 框前 0.5 m），跟着画框一起隐藏/显示
            AddFlowTrigger(frame, "Trigger_EmptyFrame", new Vector3(cx, fb + fh * 0.5f, (fz0 - 0.5f + fz1) * 0.5f),
                new Vector3(fw, fh, fz1 - fz0 + 0.5f), StateTrigger.Step.EmptyFrame);
            frame.gameObject.SetActive(false);

            // 画架 + 全家福草稿 + 刮刀（画面朝北，正对门；出生时背对门看画）
            // 黑屋这个画架没有后腿（maolin 10-30 手改，玩家绕到画布背后不会被挡）
            AddEasel(root, "FamilyPortraitSketch_Easel", new Vector3(cx, 0f, BlackEaselZ), 0f, p.Prop, p.Exhibit, false);
            GameObject knife = AddBox(root, "PaletteKnife", new Vector3(cx - 0.25f, 0.01f, BlackEaselZ + 0.45f),
                new Vector3(0.04f, 0.015f, 0.26f), p.Key);
            RemoveCollider(knife);

            // 空气墙：画架 ⇄ 门之间 5.2 m 宽通道（灰盒先用硬碰撞，"黑暗变浓 + 阻力"以后做）
            // 两侧各留 1.2 m、画架后方留 1.3 m，玩家能绕到画布背后
            Transform air = NewGroup(root, "AirWalls");
            float bandW = cx - AirBandHalfWidth;
            float bandE = cx + AirBandHalfWidth;
            float backZ = BlackEaselZ - AirBandBackOffset;
            MakeInvisible(AddSlab(air, "AirWall_West", bandW - 0.2f, bandW, -BlackRoomLength, northFace - skin, 0f, 3f, p.Black));
            MakeInvisible(AddSlab(air, "AirWall_East", bandE, bandE + 0.2f, -BlackRoomLength, northFace - skin, 0f, 3f, p.Black));
            MakeInvisible(AddSlab(air, "AirWall_South", bandW, bandE, backZ - 0.2f, backZ, 0f, 3f, p.Black));

            // 灯：画架正上方一盏顶光 + 前方一盏补光；黑屋停用时这两盏一起关。
            // 门缝光不再用灯（灯要么在接待厅打出白圆斑，要么在黑屋里摊成一滩），改成门板边缘的一条自发光几何，见 BuildMainDoor
            Transform lights = NewGroup(root, "Lights_BlackRoom");
            Vector3 canvas = new Vector3(cx, 1.35f, BlackEaselZ + 0.12f);
            AddSpotLight(lights, "Spot_BlackRoom_Easel_Top", new Vector3(cx, 3.4f, BlackEaselZ + 0.45f), canvas,
                new Color(1f, 0.88f, 0.70f), 9f, 5f, 50f, true);
            AddSpotLight(lights, "Spot_BlackRoom_Easel", new Vector3(cx, 3.6f, BlackEaselZ + 2.2f), canvas,
                new Color(1f, 0.86f, 0.68f), 3f, 7f, 32f, true);

            // 两盏灯都要有看得见的灯具（吊杆 + 灯筒），放在 Lights 组外面
            Transform fixtures = NewGroup(root, "Fixtures");
            AddSpotFixture(fixtures, "BlackRoom_Easel_Top", new Vector3(cx, 3.4f, BlackEaselZ + 0.45f), canvas, h);
            AddSpotFixture(fixtures, "BlackRoom_Easel", new Vector3(cx, 3.6f, BlackEaselZ + 2.2f), canvas, h);

            AddNote(root, "Note_BlackRoom", new Vector3(cx, 0.1f, -8f),
                "黑屋 10×16 · 画架 ⇄ 门 14 m · 空气墙 5.2 m · 门 = 美术馆正门", false);
            AddNote(root, "TODO_AirWall", new Vector3(cx, 2.4f, -8f),
                "待定：空气墙目前是硬碰撞，黑暗变浓 + 阻力未做", true);

            return root.gameObject;
        }

        // ================================================================ 正门：双开门（黑屋 ⇄ 接待厅）

        /// <summary>
        /// 两扇门板各挂在门洞外侧边的铰链上，中间留 MainDoorSeam 的缝。门板贴在墙的馆内一侧，向馆内（+Z）开。
        /// 不放进 Level_Museum（那里整体标了静态合批，会动的东西不能在里面）。
        /// </summary>
        private static DoubleDoor BuildMainDoor(Palette p)
        {
            GameObject root = new GameObject("MainDoor_Double");
            root.transform.position = new Vector3(MainDoorX, Floor1Y, 0f);

            const float thickness = 0.08f;
            float hingeZ = HalfWall - thickness * 0.5f - 0.01f;
            float leafW = (MainDoorWidth - MainDoorSeam) * 0.5f;
            float leafH = MainDoorHeight - 0.02f;

            Transform left = NewGroup(root.transform, "Hinge_Left", new Vector3(-MainDoorWidth * 0.5f, 0f, hingeZ), 0f);
            AddBox(left, "Leaf_Left", new Vector3(leafW * 0.5f, 0.01f + leafH * 0.5f, 0f), new Vector3(leafW, leafH, thickness), p.Door);

            // 门缝光：单面薄片，正面朝 −Z（黑屋），URP Lit 默认剔除背面，所以接待厅那侧看不见。
            // 不要用实心 Box —— 两面都会露，从接待厅看就是一条刺眼的白线（已经踩过一次）。
            // 挂在左门铰链上，开门时跟着门板转，不会在门洞里留一条悬空的光条。
            float seamX = leafW + MainDoorSeam * 0.5f;
            GameObject seamCrack = AddQuad(left, "Seam_Crack",
                new Vector3(seamX, 0.01f + leafH * 0.5f, -thickness * 0.15f),
                new Vector2(MainDoorSeam * 0.8f, leafH), p.SeamGlow);
            RemoveCollider(seamCrack);

            Transform right = NewGroup(root.transform, "Hinge_Right", new Vector3(MainDoorWidth * 0.5f, 0f, hingeZ), 0f);
            AddBox(right, "Leaf_Right", new Vector3(-leafW * 0.5f, 0.01f + leafH * 0.5f, 0f), new Vector3(leafW, leafH, thickness), p.Door);

            // 门把手：两扇各一个，左右镜像（都贴在馆内 +Z 面，离中缝 0.25 m，高 1.09 m —— 7 岁孩子能够到）
            GameObject keyholeRight = AddBox(right, "Keyhole_Right",
                new Vector3(-leafW + 0.25f, 1.09f, thickness * 0.5f + 0.03f), new Vector3(0.12f, 0.18f, 0.06f), p.Key);
            RemoveCollider(keyholeRight);
            GameObject keyholeLeft = AddBox(left, "Keyhole_Left",
                new Vector3(leafW - 0.25f, 1.09f, thickness * 0.5f + 0.03f), new Vector3(0.12f, 0.18f, 0.06f), p.Key);
            RemoveCollider(keyholeLeft);

            DoubleDoor door = root.AddComponent<DoubleDoor>();
            SetRef(door, "leftHinge", left);
            SetRef(door, "rightHinge", right);
            SetFloat(door, "openAngle", MainDoorOpenAngle);
            SetFloat(door, "openDuration", MainDoorOpenSeconds);
            return door;
        }

        private static void BuildMuseumEntrance(DoubleDoor door, GameObject blackRoom, GameObject deferredLights)
        {
            MuseumEntrance entrance = door.gameObject.AddComponent<MuseumEntrance>();
            SetRef(entrance, "door", door);
            SetRef(entrance, "blackRoom", blackRoom);
            SetRefArray(entrance, "deferredLights", new List<GameObject> { deferredLights });

            SerializedObject so = new SerializedObject(entrance);
            so.FindProperty("doorwaySize").vector3Value = new Vector3(MainDoorWidth, MainDoorHeight, WallThickness) * WorldScale;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddEasel(Transform parent, string name, Vector3 basePos, float yaw, Material wood, Material canvas,
            bool backLeg = true)
        {
            // 局部坐标里画面朝 +Z
            Transform root = NewGroup(parent, name, basePos, yaw);
            AddBox(root, "Leg_Left", new Vector3(-0.55f, 0.85f, 0.05f), new Vector3(0.05f, 1.7f, 0.05f), wood);
            AddBox(root, "Leg_Right", new Vector3(0.55f, 0.85f, 0.05f), new Vector3(0.05f, 1.7f, 0.05f), wood);
            if (backLeg)
            {
                AddBox(root, "Leg_Back", new Vector3(0f, 0.8f, -0.45f), new Vector3(0.05f, 1.6f, 0.05f), wood);
            }

            AddBox(root, "Ledge", new Vector3(0f, 0.82f, 0.08f), new Vector3(1.3f, 0.04f, 0.1f), wood);
            AddBox(root, "Canvas", new Vector3(0f, 1.35f, 0.1f), new Vector3(FamilyPortraitWidth, 1.0f, 0.04f), canvas);

            // 《全家福》贴在画布正面。Quad 只有 −Z 面可见，转 180° 让它朝 +Z
            GameObject art = AddQuad(root, "FamilyPortrait", new Vector3(0f, 1.35f, 0.122f), new Vector2(FamilyPortraitWidth, 1.0f), FamilyPortraitMaterial());
            art.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            RemoveCollider(art);
        }

        // 原图 16:9，画布按原比例加宽，不裁画面。黑屋和二楼暂时用同一张（maolin 2026-10-30）
        private const float FamilyPortraitWidth = 1.78f;
        private const string FamilyPortraitTexture = "Assets/Art/Textures/Paintings/FamilyPortrait.png";

        private static Material FamilyPortraitMaterial()
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(FamilyPortraitTexture);
            if (tex == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 找不到全家福贴图 " + FamilyPortraitTexture);
            }

            return Lit("M_FamilyPortrait", tex, null, Color.white, 0.1f, 1f, 0f, false);
        }

        // ================================================================ 服装厅西北角：绳子围起来的一堆人台（maolin 10-30 手摆）

        /// <summary>
        /// 5 个不穿衣服、没有脖子的人台堆在服装厅西北角，两段立柱红绳围住（x ≈ 28.78、z ≈ 22.69）。
        /// 静态摆设，不会动，每一块都有碰撞（人走不进去，导航网格按物理碰撞烘焙，会当成障碍）。
        /// 位置从手改场景里 Mannequin_Top 下的子物体换算成世界坐标得来。放在人群之后，不挤动已有站位。
        /// </summary>
        private static void BuildRopedMannequins(Transform props)
        {
            Transform lily = FindDeep(props, "LilyDress_Case");
            if (lily == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 找不到服装厅分组，绳围人台没放");
                return;
            }

            Transform g = NewGroup(lily.parent, "RopedMannequins_NW");
            float y = Floor1Y;
            float[,] spots = { { 27.376f, 24.684f }, { 27.069f, 23.319f }, { 26.72f, 24.37f }, { 27.923f, 25.288f }, { 28.054f, 24.241f } };
            for (int i = 0; i < 5; i++)
            {
                Transform m = Mannequin(g, "Mannequin_Pile_" + (i + 1), new Vector3(spots[i, 0], y, spots[i, 1]), 90f, 1f, Outfit.None, null);
                Transform neck = FindDeep(m, "Neck");
                if (neck != null)
                {
                    Object.DestroyImmediate(neck.gameObject);   // 手摆的这几个没有脖子
                }
            }

            Stanchions(g, "Rope_East", y, new Vector3(28.78f, 0f, 22.58f), new Vector3(28.78f, 0f, 25.28f), 2);
            Stanchions(g, "Rope_South", y, new Vector3(26.257f, 0f, 22.801f), new Vector3(28.752f, 0f, 22.58f), 2);

            // 每一块都补上碰撞（Mannequin / Stanchions 只给立柱留了碰撞）
            foreach (MeshRenderer r in g.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.GetComponent<Collider>() == null)
                {
                    r.gameObject.AddComponent<BoxCollider>();
                }
            }

            MarkStatic(g);
        }

        // ================================================================ 文本收集物（GDD §6.11.2，T1–T16 共 19 个物体）

        /// <summary>
        /// 给物体挂 TextPickup 并填 textId。PlayerInteractor 用细球扫（忽略 Trigger），所以要实心碰撞体；没有就补一个 BoxCollider。
        /// </summary>
        private static void MakeTextPickup(GameObject go, string id)
        {
            if (go == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 文本 " + id + " 找不到要挂的物体");
                return;
            }

            if (go.GetComponent<Collider>() == null)
            {
                go.AddComponent<BoxCollider>();
            }

            SetString(go.AddComponent<TextPickup>(), "textId", id);
        }

        /// <summary>
        /// 手电近距离直照时，浅色纸和浅色台面 / 墙一起过曝成白色，分不出来（maolin 10-30 实测）。
        /// 所以新加的文本都是一块深色底板，上面贴一张偏暗、四周留边的纸：强光下深色边框照样看得见。
        /// 纸面在最薄的那个轴上比底板厚 25%，两面都露出来，不用管朝向。纸没有碰撞，交互打在底板上。
        /// </summary>
        private static GameObject Framed(GameObject board, Material face)
        {
            if (board == null)
            {
                return null;
            }

            Vector3 s = board.transform.localScale;
            int thin = s.x <= s.y && s.x <= s.z ? 0 : (s.y <= s.z ? 1 : 2);
            Vector3 inset = new Vector3(0.86f, 0.86f, 0.86f);
            inset[thin] = 1.25f;
            LocalBox(board.transform, board.name + "_Paper", Vector3.zero, inset, face);
            return board;
        }

        private static GameObject FindChild(Transform root, string name)
        {
            Transform t = FindDeep(root, name);
            return t != null ? t.gameObject : null;
        }

        /// <summary>
        /// 能复用的现有物体直接挂（导言墙、登记簿、交接本、花下便签、Lily 展签），其余新加一张纸 / 一块牌，名字 Text_&lt;id&gt;_&lt;简称&gt;。
        /// 坐标都是放大前的 1 倍值；各房间分组都在原点，所以局部坐标 = 世界坐标（展柜、画架、前台例外，用各自的局部坐标）。
        /// </summary>
        private static void BuildTextPickups(Transform props, Palette p)
        {
            float y1 = Floor1Y;
            float y2 = Floor2Y;
            // 文本统一：深色底板 + 偏暗的纸面（见 Framed）。底板在 board，纸面是它的子物体
            Material board = Lit("M_TextBoard", null, null, new Color(0.10f, 0.08f, 0.07f), 0.1f, 1f, 0f, false);
            Material paper = Lit("M_TextPaper", null, null, new Color(0.60f, 0.55f, 0.44f), 0.05f, 1f, 0f, false);

            // ---- 1F 接待厅 / 前台 ----
            MakeTextPickup(FindChild(props, "Intro_Wall_TheAudience"), "T1");                 // 导言墙本身
            Transform book = FindDeep(props, "Register_Book");
            MakeTextPickup(book != null ? FindChild(book, "Cover") : null, "T4");              // 登记簿皮面（书页没有碰撞，球扫穿过去打到皮面）
            Transform desk = FindDeep(props, "FrontDesk");
            if (desk != null)
            {
                // 前台没有屏幕：便利贴贴在台面上、电话旁边
                MakeTextPickup(Framed(LocalBox(desk, "Text_T5_StickyNote", new Vector3(-0.85f, 1.006f, 0.2f), new Vector3(0.17f, 0.012f, 0.17f), board), paper), "T5");
                // 抽屉（员工那面 z = 0.43）还不会开：两份文件夹露在抽屉面板外，一左一右，避开中间的把手
                MakeTextPickup(Framed(LocalBox(desk, "Text_T14_Workbook", new Vector3(-0.24f, 0.73f, 0.475f), new Vector3(0.26f, 0.22f, 0.012f), board), paper), "T14");
                MakeTextPickup(Framed(LocalBox(desk, "Text_T15_CaseReport", new Vector3(0.24f, 0.73f, 0.475f), new Vector3(0.26f, 0.22f, 0.012f), board), paper), "T15");
            }

            // ---- 1F 配电间：西墙上的检修单（东墙是电闸箱，南墙是保安柜）----
            GameObject cabinet = FindChild(props, "B_SecurityCabinet");
            if (cabinet != null)
            {
                MakeTextPickup(Framed(AddWallPanel(cabinet.transform.parent, "Text_T9_ServiceSheet",
                    new Vector3(PowerRoomWestX + HalfWall, y1 + 1.3f, 10.8f), Vector3.right, 0.42f, 0.56f, board), paper), "T9");
            }

            // ---- 1F 服装厅：Lily 柜的展签 T10（原来去掉了碰撞，这里补回来）----
            MakeTextPickup(FindChild(props, "Label_T10"), "T10");

            // ---- 2F 名画长廊：三页笔记 + 剪报，各在一个展柜，纸贴在柜身镶板外面（玻璃罩顶太高，孩子够不着也看不见）----
            // 第 4 个展柜顶上已经有画室门票，不用。贴哪一面看那一侧有没有画中人站着（±1 = 北 / 南）
            string[] vitrineIds = { "T6_1", "T11", "T6_2", "T6_3" };
            string[] vitrineNames = { "DisplayCase_Cover_1", "DisplayCase_Cover_2", "DisplayCase_Cover_3", "DisplayCase_Cover_5" };
            string[] shortNames = { "StudioNote1", "Clippings", "StudioNote2", "StudioNote3" };
            float[] sides = { 1f, -1f, -1f, 1f };
            for (int i = 0; i < 4; i++)
            {
                Transform v = FindDeep(props, vitrineNames[i]);
                if (v == null)
                {
                    Debug.LogWarning("[GrayboxBuilder] 找不到展柜 " + vitrineNames[i]);
                    continue;
                }

                MakeTextPickup(Framed(LocalBox(v, "Text_" + vitrineIds[i] + "_" + shortNames[i],
                    new Vector3(-0.9f, 0.62f, sides[i] * 0.507f), new Vector3(0.32f, 0.23f, 0.014f), board), paper), vitrineIds[i]);
            }

            // ---- 2F 全家福厅：只加两块小牌 + 《美术馆》下面一个小展台（全厅唯一新增的小东西）----
            GameObject flower = FindChild(props, "Pickup_FlowerAndNote");
            MakeTextPickup(flower, "T12");                                                // 花下的便签
            Transform easel = FindDeep(props, "FamilyPortrait_Easel");
            if (easel != null)
            {
                // 夹在右腿正面、托架下面（画面朝局部 +Z = 世界 −X，朝门口）
                MakeTextPickup(Framed(LocalBox(easel, "Text_T2_PortraitLabel", new Vector3(0.55f, 0.6f, 0.087f), new Vector3(0.3f, 0.21f, 0.014f), board), paper), "T2");
            }

            if (flower != null)
            {
                Transform family = flower.transform.parent;
                // 《美术馆》连防尘布到 x ≈ 38.45，说明牌挂在布的东边
                const float labelX = 38.85f;
                MakeTextPickup(Framed(AddWallPanel(family, "Text_T3_MuseumLabel", new Vector3(labelX, y2 + 1.3f, FamilyNorthZ - HalfWall),
                    Vector3.back, 0.45f, 0.31f, board), paper), "T3");
                AddProp(family, "Text_T6_4_Stand", labelX, y2, FamilyNorthZ - HalfWall - 0.26f, 0.36f, 0.9f, 0.36f, Finishes.Wainscot);
                MakeTextPickup(Framed(AddProp(family, "Text_T6_4_LastPage", labelX, y2 + 0.9f, FamilyNorthZ - HalfWall - 0.26f, 0.3f, 0.014f, 0.34f, board), paper), "T6_4");
            }

            // ---- 2F 值班室 ----
            MakeTextPickup(FindChild(props, "A_HandoverLog"), "T7");                           // 桌上的交接本
            GameObject dutyDesk = FindChild(props, "DutyDesk");
            if (dutyDesk != null)
            {
                Transform duty = dutyDesk.transform.parent;
                // 东墙（门那面）门北边，离门洞够远
                MakeTextPickup(Framed(AddWallPanel(duty, "Text_T8_NightRules", new Vector3(WestLineX - HalfWall, y2 + 1.3f, 6.5f),
                    Vector3.left, 0.5f, 0.65f, board), paper), "T8");
                // 两个文件夹并排在桌面东半边（交接本在西边 x 5.8）
                MakeTextPickup(Framed(AddProp(duty, "Text_T13_StockForm", 7.2f, y2 + 0.75f, 9.3f, 0.3f, 0.03f, 0.22f, board), paper), "T13");
                MakeTextPickup(Framed(AddProp(duty, "Text_T16_DeathRuling", 7.75f, y2 + 0.75f, 9.4f, 0.3f, 0.03f, 0.22f, board), paper), "T16");
            }
        }

        // ================================================================ 出生点、检查点、玩家

        private static void BuildSpawnsAndPlayer(Scene scene)
        {
            Transform root = new GameObject("--- Spawns ---").transform;
            float y1 = Floor1Y + 0.02f;
            float y2 = Floor2Y + 0.02f;

            // 顺序 = F1–F9。CP 是 §3.4 建议的自动检查点位置（待灰盒确定），DBG 只是调试入口。
            List<PlayerSpawnPoint> points = new List<PlayerSpawnPoint>
            {
                AddSpawn(root, "CP0_BlackRoom_Start", new Vector3(BlackRoomCenterX, 0.02f, BlackEaselZ + 2.4f), 180f, true),
                AddSpawn(root, "CP1_ExitPainting_FamilyRoom", new Vector3(37.0f, y2, FamilyNorthZ - 1.4f), 180f, false),
                AddSpawn(root, "CP2_Flashlight_Toilet", new Vector3(29.0f, y2, ToiletDoorZ), -90f, false),
                AddSpawn(root, "CP3_TaskA_DutyRoom", new Vector3(11.5f, y2, DutyDoorZ), 90f, false),
                AddSpawn(root, "CP4_TaskB_PowerRoom", new Vector3(PowerDoorX, y1, 11.0f), 0f, false),
                AddSpawn(root, "CP5_TaskC_FrontDesk", new Vector3(MainDoorX, y1, 5.2f), 180f, false),
                AddSpawn(root, "DBG6_Museum_Entrance", new Vector3(MainDoorX, y1, 1.5f), 0f, false),
                AddSpawn(root, "DBG7_Stairs_1F", new Vector3(23.0f, y1, 23.5f), -90f, false),
                AddSpawn(root, "DBG8_Gallery_2F_Top", new Vector3(23.0f, y2, 20.0f), 180f, false),
            };

            // 停电检查点（Checkpoints.PowerOut）：楼梯平台南半边、第二跑起步处，面朝东（上楼方向）。
            // 不进调试列表，F1–F9 不变
            AddSpawn(root, "CP_PowerOut_Stairs", new Vector3(14.3f, Floor1Y + StairRisersPerFlight * StairRise + 0.02f, 22.3f), 90f, false);

            PlayerSpawnPoint start = points[0];

            GameObject prefabAsset = GetOrCreatePlayerPrefab();
            GameObject playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, scene);
            playerInstance.transform.position = start.transform.position;
            playerInstance.transform.rotation = start.transform.rotation;

            GameObject manager = new GameObject("--- Game ---");
            PlayerSpawner spawner = manager.AddComponent<PlayerSpawner>();
            SetRef(spawner, "player", playerInstance.GetComponent<FirstPersonController>());
            SetRef(spawner, "spawnPoint", start);

            DebugSpawnSwitcher switcher = manager.AddComponent<DebugSpawnSwitcher>();
            SetRef(switcher, "player", playerInstance.GetComponent<FirstPersonController>());
            SetRefArray(switcher, "points", points);

            DebugMenu menu = manager.AddComponent<DebugMenu>();
            SetRef(menu, "player", playerInstance.GetComponent<FirstPersonController>());
        }

        private static PlayerSpawnPoint AddSpawn(Transform parent, string name, Vector3 position, float yaw, bool isDefault)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            PlayerSpawnPoint spawn = go.AddComponent<PlayerSpawnPoint>();
            SetBool(spawn, "isDefault", isDefault);
            return spawn;
        }

        private static GameObject GetOrCreatePlayerPrefab()
        {
            GameObject root = new GameObject("Player");

            CharacterController cc = root.AddComponent<CharacterController>();
            cc.height = PlayerHeight;
            cc.radius = 0.28f;
            cc.center = new Vector3(0f, PlayerHeight * 0.5f, 0f);
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.3f;
            cc.skinWidth = 0.02f;
            cc.minMoveDistance = 0f;

            GameObject cameraRoot = new GameObject("CameraRoot");
            cameraRoot.transform.SetParent(root.transform, false);
            cameraRoot.transform.localPosition = new Vector3(0f, PlayerEyeHeight, 0f);

            GameObject cameraGO = new GameObject("Main Camera");
            cameraGO.tag = "MainCamera";
            cameraGO.transform.SetParent(cameraRoot.transform, false);

            Camera cam = cameraGO.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 90f;   // 馆整体放大后最长对角线约 67 m
            cam.fieldOfView = 68f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.008f, 0.008f, 0.01f, 1f);
            cam.allowHDR = true;

            cameraGO.AddComponent<AudioListener>();

            UniversalAdditionalCameraData cameraData = cameraGO.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;

            GameObject hand = new GameObject("HandAnchor");
            hand.transform.SetParent(cameraGO.transform, false);
            hand.transform.localPosition = new Vector3(0.18f, -0.15f, 0.3f);

            FirstPersonController controller = root.AddComponent<FirstPersonController>();
            PlayerLook look = root.AddComponent<PlayerLook>();
            PlayerInteractor interactor = root.AddComponent<PlayerInteractor>();
            NoclipXRay xray = root.AddComponent<NoclipXRay>();

            SetRef(controller, "cameraRoot", cameraRoot.transform);
            SetRef(look, "cameraTransform", cameraGO.transform);
            SetRef(interactor, "cameraTransform", cameraGO.transform);
            SetFloat(interactor, "reach", 2.5f);   // 东西都变大了，手够得着的距离放宽一点

            // 穿透视图判定"馆内 / 黑屋"的包围盒跟着整体放大
            SerializedObject xs = new SerializedObject(xray);
            xs.FindProperty("museum").boundsValue = new Bounds(new Vector3(20f, 5.7f, 13f) * WorldScale, new Vector3(40f, 11.4f, 26f) * WorldScale);
            xs.FindProperty("blackRoom").boundsValue = new Bounds(new Vector3(19.5f, 2.25f, -8f) * WorldScale, new Vector3(10f, 4.5f, 16f) * WorldScale);
            SerializedProperty grid = xs.FindProperty("gridHeights");
            grid.arraySize = 2;
            grid.GetArrayElementAtIndex(0).floatValue = 0.02f;
            grid.GetArrayElementAtIndex(1).floatValue = Floor2Y * WorldScale + 0.02f;
            xs.ApplyModifiedPropertiesWithoutUndo();

            // 7 岁身高：眼睛 = standHeight + eyeOffset
            SetFloat(controller, "standHeight", PlayerHeight);
            SetFloat(controller, "crouchHeight", 0.9f);
            SetFloat(controller, "eyeOffset", PlayerEyeHeight - PlayerHeight);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);

            return prefab;
        }

        // ================================================================ 灯光（灰盒统一一套，状态切换以后做）

        /// <summary>
        /// 接待厅的灯和门缝灯开阴影，黑屋阶段常亮（光只从门缝漏进黑屋）；
        /// 其余馆内灯放进返回的组里，由 MuseumEntrance 在推门时才打开（无阴影点光会穿墙照亮黑屋）。
        /// 黑屋自己的灯在 BuildBlackRoom 里。
        /// </summary>
        private static GameObject BuildLighting()
        {
            GameObject root = new GameObject("--- Lighting ---");
            Transform t = root.transform;

            // 灯挂在梁下面（梁底在天花下 0.42 m），灯罩模型放进 Level_Museum，别放进推门前要关掉的灯组
            float c1 = Floor1Height - 1.0f;
            float c2 = Floor2Height - 1.0f;
            lampFixtures = NewGroup(GameObject.Find("Level_Museum").transform, "Lamp_Fixtures");

            AddRoomLights(t, "Reception", WestLineX, EastLineX, 0f, ReceptionNorthZ, Floor1Y + c1, true);

            // 这里不放门缝光：原先这门朝南的射灯会在两扇门面上打出一个白圆斑，已删；
            // 后来加的"中缝自发光薄片"从接待厅看会透出一条白线，也删了。黑屋门前只有画架的两盏灯。

            Transform deferred = NewGroup(t, "Museum_Deferred");
            AddRoomLights(deferred, "OilHall", 0f, WestLineX, 0f, BuildingDepth, Floor1Y + c1, false);
            AddRoomLights(deferred, "Sculpture", WestLineX, EastLineX, PowerRoomNorthZ, StairHallSouthZ, Floor1Y + c1, false);
            AddRoomLights(deferred, "PowerRoom", PowerRoomWestX, PowerRoomEastX, ReceptionNorthZ, PowerRoomNorthZ, Floor1Y + 2.6f, false);
            AddRoomLights(deferred, "StairHall_1F", StairTopX, EastLineX, StairHallSouthZ, BuildingDepth, Floor1Y + c1, false, ColdLight, 0);
            AddRoomLights(deferred, "Costume", EastLineX, BuildingWidth, 0f, BuildingDepth, Floor1Y + c1, false);

            AddRoomLights(deferred, "Gallery", WestLineX, EastLineX, 0f, StairHallSouthZ, Floor2Y + c2, false);
            AddRoomLights(deferred, "StairHall_2F", WestLineX, EastLineX, StairHallSouthZ, BuildingDepth, Floor2Y + c2, false, ColdLight, 1);
            AddRoomLights(deferred, "Family", EastLineX, BuildingWidth, FamilySouthZ, FamilyNorthZ, Floor2Y + c2, false);
            AddRoomLights(deferred, "Duty", 0f, WestLineX, 0f, DutyNorthZ, Floor2Y + c2, false);
            AddRoomLights(deferred, "Toilet", EastLineX, ToiletEastX, 0f, ToiletNorthZ, Floor2Y + c2, false);

            // 天窗冷光，把天使单独打出来（手电与注视的"被照亮"判定以后在这上面调）
            AddSpotLight(deferred, "Spot_Skylight_Angel", SculptureCenter() + new Vector3(0f, Floor1Height - 0.2f, -0.8f), SculptureCenter() + Vector3.up,
                new Color(0.70f, 0.80f, 1f), 18f, 11f, 40f, true);

            AddPointLight(deferred, "Light_LilyDress_Pink", new Vector3(38.1f, Floor1Y + 1.68f, 2.3f),
                new Color(1f, 0.55f, 0.72f), 5f, 4f, false, false);

            return deferred.gameObject;
        }

        private static readonly Color WarmLight = new Color(1f, 0.92f, 0.82f);
        private static readonly Color ColdLight = new Color(0.72f, 0.84f, 1f);
        private static Transform lampFixtures;

        private static void AddRoomLights(Transform parent, string room, float x0, float x1, float z0, float z1, float y, bool shadows)
        {
            AddRoomLights(parent, room, x0, x1, z0, z1, y, shadows, WarmLight, -1);
        }

        /// <summary>
        /// 房间均匀铺点光，间距约 6.5 m。只有挨着黑屋的房间开阴影，其余省 Forward+ 的阴影图。
        /// flickerIndex ≥ 0：这一盏挂 LightFlicker（楼梯厅那种接触不良的冷光）。每盏灯配一个吊灯罩。
        /// </summary>
        private static void AddRoomLights(Transform parent, string room, float x0, float x1, float z0, float z1, float y, bool shadows,
            Color color, int flickerIndex)
        {
            const float spacing = 6.5f;
            int nx = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / spacing));
            int nz = Mathf.Max(1, Mathf.RoundToInt((z1 - z0) / spacing));
            float dx = (x1 - x0) / nx;
            float dz = (z1 - z0) / nz;

            Transform group = NewGroup(parent, "Lights_" + room);
            int index = 0;
            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    Vector3 pos = new Vector3(x0 + (i + 0.5f) * dx, y, z0 + (j + 0.5f) * dz);
                    AddPointLight(group, "Light_" + room + "_" + index, pos, color,
                        RoomLightIntensity, RoomLightRange, index == flickerIndex, shadows);
                    AddLampFixture(room + "_" + index, pos, color);
                    index++;
                }
            }
        }

        private static void AddPointLight(Transform parent, string name, Vector3 position,
            Color color, float intensity, float range, bool flicker, bool shadows)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;

            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = 0.9f;
            light.shadowBias = 0.05f;
            light.shadowNormalBias = 0.4f;

            if (flicker)
            {
                go.AddComponent<LightFlicker>();
            }
        }

        private static void AddSpotLight(Transform parent, string name, Vector3 position, Vector3 target,
            Color color, float intensity, float range, float angle, bool shadows)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.LookAt(target);

            Light light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = angle;
            light.innerSpotAngle = angle * 0.6f;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = 0.9f;
            light.shadowBias = 0.05f;
            light.shadowNormalBias = 0.4f;
        }

        // ================================================================ 后处理

        private static void BuildPostProcessing()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);

            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "GrayboxVolume";
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);

                Tonemapping tonemapping = AddOverride<Tonemapping>(profile);
                tonemapping.mode.Override(TonemappingMode.ACES);

                ColorAdjustments color = AddOverride<ColorAdjustments>(profile);
                color.postExposure.Override(0f);
                color.contrast.Override(12f);
                color.saturation.Override(-22f);
                color.colorFilter.Override(new Color(0.94f, 0.96f, 1f));

                Vignette vignette = AddOverride<Vignette>(profile);
                vignette.intensity.Override(0.35f);
                vignette.smoothness.Override(0.55f);

                Bloom bloom = AddOverride<Bloom>(profile);
                bloom.threshold.Override(0.95f);
                bloom.intensity.Override(0.55f);
                bloom.scatter.Override(0.6f);

                FilmGrain grain = AddOverride<FilmGrain>(profile);
                grain.type.Override(FilmGrainLookup.Thin1);
                grain.intensity.Override(0.6f);
                grain.response.Override(0.85f);
            }

            // 亮度相关的两项每次都写回（资产已存在时也生效）；其他参数允许手调，不覆盖
            if (profile.TryGet(out ColorAdjustments adjust))
            {
                adjust.postExposure.Override(0f);
            }

            if (profile.TryGet(out Vignette vig))
            {
                vig.intensity.Override(0.35f);
            }

            EditorUtility.SetDirty(profile);

            GameObject go = new GameObject("Global Volume");
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.profile = profile;
        }

        private static T AddOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component = ScriptableObject.CreateInstance<T>();
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector;
            profile.components.Add(component);
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        // ================================================================ 整体放大

        /// <summary>
        /// 把场景按 WorldScale 绕原点整体放大（玩家、管理对象、后处理、浮尘除外）。
        /// Light 的照射距离不受 Transform 缩放影响，所以每盏灯 range × S、亮度 × S²（距离放大后照度不变）。
        /// 玩家重新摆到放大后的出生点。
        /// </summary>
        private static void ApplyWorldScale(Scene scene)
        {
            const float s = WorldScale;
            var skip = new HashSet<string> { "Player", "--- Game ---", "Global Volume", "--- Atmosphere ---" };
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (skip.Contains(root.name))
                {
                    continue;
                }

                root.transform.position *= s;
                root.transform.localScale *= s;
            }

            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light.GetComponentInParent<FirstPersonController>(true) != null)
                {
                    continue;
                }

                light.range *= s;
                light.intensity *= s * s;
            }

            FirstPersonController player = Object.FindAnyObjectByType<FirstPersonController>();
            GameObject spawn = GameObject.Find("--- Spawns ---/CP0_BlackRoom_Start");
            if (player != null && spawn != null)
            {
                player.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
            }
        }

        /// <summary>每盏灯附近都得有个看得见的灯具（吊灯、射灯筒、出口灯、天窗、灯片……），否则在 Console 里点名。</summary>
        private static void AuditLightFixtures()
        {
            List<Bounds> fixtures = new List<Bounds>();
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                // 只认小物件（灯罩、射灯筒、出口灯牌……），墙和天花板不算；天窗发光板例外
                Bounds b = r.bounds;
                float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (size > 1.2f && r.name != "Skylight_Fake")
                {
                    continue;
                }

                b.Expand(1.0f);   // Expand 是总尺寸，每边 0.5 m
                fixtures.Add(b);
            }

            int missing = 0;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light.GetComponentInParent<FirstPersonController>(true) != null)
                {
                    continue;
                }

                bool found = false;
                foreach (Bounds b in fixtures)
                {
                    if (b.Contains(light.transform.position))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    Debug.LogWarning("[GrayboxBuilder] 灯没有灯具：" + light.name + " @ " + light.transform.position, light);
                    missing++;
                }
            }

            if (missing == 0)
            {
                Debug.Log("[GrayboxBuilder] 灯具检查：每盏灯都有看得见的灯具。");
            }
        }

        // ================================================================ 场景环境

        private static void BuildEnvironmentSettings()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.05f, 0.052f, 0.06f);   // 10-27 略降：恐怖感靠明暗对比
            RenderSettings.skybox = null;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.022f;
            RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.025f);

            RenderSettings.reflectionIntensity = 0.3f;
        }

        // ================================================================ 几何工具

        private static Vector3 SculptureCenter()
        {
            return new Vector3((WestLineX + EastLineX) * 0.5f, Floor1Y, (ReceptionNorthZ + StairHallSouthZ) * 0.5f);
        }

        /// <summary>
        /// 沿 X 走向的墙（中线 z，从 x0 到 x1）。
        /// extend = 在 L 形拐角处多伸半个墙厚把角盖住；T 形交接（另一面墙从这里贯通）不伸，
        /// 否则端面会和贯通墙的表面同向共面，产生 z-fighting。
        /// </summary>
        private static void WallAlongX(Transform parent, string name, float z, float x0, float x1,
            bool extendStart, bool extendEnd, float floorY, float height, Material mat, params Opening[] openings)
        {
            float a = extendStart ? x0 - HalfWall : x0;
            float b = extendEnd ? x1 + HalfWall : x1;
            BuildWallRun(parent, name, true, z, a, b, floorY, height, mat, openings);
        }

        /// <summary>沿 Z 走向的墙：两端各缩进半个墙厚，正好抵在横墙表面上（只共边、不共面）。</summary>
        private static void WallAlongZ(Transform parent, string name, float x, float z0, float z1,
            float floorY, float height, Material mat, params Opening[] openings)
        {
            BuildWallRun(parent, name, false, x, z0 + HalfWall, z1 - HalfWall, floorY, height, mat, openings);
        }

        private static void BuildWallRun(Transform parent, string name, bool alongX, float line, float a, float b,
            float floorY, float height, Material mat, Opening[] openings)
        {
            Transform root = NewGroup(parent, name);
            List<Opening> sorted = new List<Opening>(openings);
            sorted.Sort((l, r) => l.Center.CompareTo(r.Center));

            float top = floorY + height;
            float cursor = a;
            int index = 0;

            foreach (Opening op in sorted)
            {
                if (op.Min < cursor || op.Max > b)
                {
                    Debug.LogError("[GrayboxBuilder] 门洞超出墙段或互相重叠：" + name + " @ " + op.Center);
                    continue;
                }

                if (op.Min > cursor)
                {
                    AddWallPiece(root, "Seg_" + index, alongX, line, cursor, op.Min, floorY, top, mat);
                }

                if (op.Height < height)
                {
                    AddWallPiece(root, "Lintel_" + index, alongX, line, op.Min, op.Max, floorY + op.Height, top, mat);
                }

                cursor = op.Max;
                index++;
            }

            if (b > cursor)
            {
                AddWallPiece(root, "Seg_" + index, alongX, line, cursor, b, floorY, top, mat);
            }
        }

        private static void AddWallPiece(Transform parent, string name, bool alongX, float line,
            float a, float b, float yMin, float yMax, Material mat)
        {
            if (alongX)
            {
                AddSlab(parent, name, a, b, line - HalfWall, line + HalfWall, yMin, yMax, mat);
            }
            else
            {
                AddSlab(parent, name, line - HalfWall, line + HalfWall, a, b, yMin, yMax, mat);
            }
        }

        /// <summary>关着的门板，正好填满门洞（锁着的门、电磁锁门）。</summary>
        private static GameObject AddDoorPanel(Transform parent, string name, bool wallAlongX, float line,
            Opening op, float floorY, Material mat)
        {
            const float t = 0.1f;
            if (wallAlongX)
            {
                return AddSlab(parent, name, op.Min, op.Max, line - t * 0.5f, line + t * 0.5f, floorY, floorY + op.Height, mat);
            }

            return AddSlab(parent, name, line - t * 0.5f, line + t * 0.5f, op.Min, op.Max, floorY, floorY + op.Height, mat);
        }

        /// <summary>贴墙的薄板（画、说明牌、镜子）。facePoint = 墙表面上的中心点，normal 指向房间内。离墙 1 cm 防共面。</summary>
        private static GameObject AddWallPanel(Transform parent, string name, Vector3 facePoint, Vector3 normal,
            float width, float height, Material mat)
        {
            const float depth = 0.06f;
            Vector3 center = facePoint + normal * (depth * 0.5f + 0.01f);
            bool faceAlongX = Mathf.Abs(normal.x) > 0.5f;
            Vector3 size = faceAlongX ? new Vector3(depth, height, width) : new Vector3(width, height, depth);
            return AddBox(parent, name, center, size, mat);
        }

        /// <summary>落地道具：(x, z) 是底面中心，y 是底面高度。</summary>
        private static GameObject AddProp(Transform parent, string name, float x, float y, float z,
            float sizeX, float sizeY, float sizeZ, Material mat)
        {
            return AddBox(parent, name, new Vector3(x, y + sizeY * 0.5f, z), new Vector3(sizeX, sizeY, sizeZ), mat);
        }

        /// <summary>按最小/最大面坐标生成一块立方体，避免手工算中心点出错。</summary>
        private static GameObject AddSlab(Transform parent, string name,
            float xMin, float xMax, float zMin, float zMax, float yMin, float yMax, Material mat)
        {
            Vector3 center = new Vector3((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f, (zMin + zMax) * 0.5f);
            Vector3 size = new Vector3(xMax - xMin, yMax - yMin, zMax - zMin);
            return AddBox(parent, name, center, size, mat);
        }

        /// <summary>
        /// 单面薄片（Quad）。不动朝向：Unity 自带 Quad 的正面法线是 −Z，URP Lit 默认剔除背面，
        /// 所以只有站在 −Z 那一侧（黑屋）才看得见，+Z 那侧（接待厅）完全看不到。门缝光就靠这个。
        /// </summary>
        private static GameObject AddQuad(Transform parent, string name, Vector3 center, Vector2 size, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return go;
        }

        private static GameObject AddBox(Transform parent, string name, Vector3 center, Vector3 size, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            if (WorldUvMaterials.Contains(mat))
            {
                ApplyWorldUv(go);
            }

            return go;
        }

        private static GameObject AddCapsule(Transform parent, string name, Vector3 basePos, float height, float radius,
            Material mat, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = basePos + Vector3.up * (height * 0.5f);
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            if (!keepCollider)
            {
                RemoveCollider(go);
            }

            return go;
        }

        private static void RemoveCollider(GameObject go)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null)
            {
                Object.DestroyImmediate(c);
            }
        }

        /// <summary>只留碰撞体（空气墙）。</summary>
        private static void MakeInvisible(GameObject go)
        {
            Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(go.GetComponent<MeshFilter>());
        }

        private static GrayboxNote AddNote(Transform parent, string name, Vector3 position, string text, bool pending)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            GrayboxNote note = go.AddComponent<GrayboxNote>();
            note.Text = text;
            note.Pending = pending;
            return note;
        }

        private static Transform NewGroup(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Transform NewGroup(Transform parent, string name, Vector3 position, float yaw)
        {
            Transform t = NewGroup(parent, name);
            t.localPosition = position;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        private static void MarkStatic(Transform root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            }
        }

        // ================================================================ 资产工具

        private static Material GetOrCreateMaterial(string name, Color color, float smoothness, float emission = 0f)
        {
            string path = MaterialDir + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            // 已存在就不覆盖：允许之后手动调参而不被重建冲掉
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[GrayboxBuilder] 找不到 URP Lit 着色器，检查渲染管线设置。");
                shader = Shader.Find("Standard");
            }

            Material mat = new Material(shader);
            mat.name = name;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);

            if (emission > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static SerializedProperty FindProperty(SerializedObject so, Object target, string fieldName)
        {
            SerializedProperty property = so.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError("[GrayboxBuilder] 字段不存在：" + target.GetType().Name + "." + fieldName);
            }

            return property;
        }

        private static void SetRef(Object target, string fieldName, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = FindProperty(so, target, fieldName);
            if (property == null)
            {
                return;
            }

            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetRefArray<T>(Object target, string fieldName, List<T> values) where T : Object
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = FindProperty(so, target, fieldName);
            if (property == null)
            {
                return;
            }

            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetFloat(Object target, string fieldName, float value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = FindProperty(so, target, fieldName);
            if (property == null)
            {
                return;
            }

            property.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetVector3(Object target, string fieldName, Vector3 value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = FindProperty(so, target, fieldName);
            if (property == null)
            {
                return;
            }

            property.vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetBool(Object target, string fieldName, bool value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = FindProperty(so, target, fieldName);
            if (property == null)
            {
                return;
            }

            property.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetString(Object target, string fieldName, string value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = FindProperty(so, target, fieldName);
            if (property == null)
            {
                return;
            }

            property.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            string leaf = System.IO.Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void AddToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == ScenePath)
                {
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }

            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
