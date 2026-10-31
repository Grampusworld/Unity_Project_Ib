using Ib.Interaction;
using Ib.Threats;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ib.EditorTools
{
    /// <summary>
    /// 后半段（GDD §3.6 第 8、11、13、15–18 步）要的场景接线：电话、正门锁、保安柜工作卡、前台抽屉、手电落地、
    /// 名画人物的禁区（全家福厅）。
    /// 只在已有物体上补组件、补几个小物件，按名字找，可重复执行；不重建场景，手改的东西不受影响。
    /// 生成灰盒的最后一步也会调用它。
    /// </summary>
    public static partial class GrayboxBuilder
    {
        // 手电躺在厕所地上，离门洞 2 m，正对门（1 倍坐标）
        private const float FlashlightFloorX = EastLineX + 2f;

        [MenuItem("Tools/Ib/布置后半段（电话、正门锁、工作卡、抽屉）")]
        private static void SetupSecondHalfMenu()
        {
            SetupSecondHalf();
        }

        internal static void SetupSecondHalf()
        {
            Scene scene = SceneManager.GetActiveScene();
            LoadFinishes();

            Transform props = FindRoot(scene, "Props_Museum");
            if (props == null)
            {
                Debug.LogError("[GrayboxBuilder] 场景里没有 Props_Museum，先生成灰盒。");
                return;
            }

            PlaceFlashlightOnFloor(props);
            WirePhone(props);
            WireDrawer(props);
            UpgradeSculptureCamera(props);
            WireCabinet(props);
            WireMainDoorLock(FindRoot(scene, "MainDoor_Double"));
            WireFigureNoGo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[GrayboxBuilder] 后半段接线完成。");
        }

        private static Transform FindRoot(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == name)
                {
                    return go.transform;
                }
            }

            return null;
        }

        private static Transform Require(Transform root, string name)
        {
            Transform t = root != null ? FindDeep(root, name) : null;
            if (t == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 找不到 " + name + "，跳过。");
            }

            return t;
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        /// <summary>第 11 步：手电从架子上挪到地上，正对门洞（光由 FlashlightPickup 运行时朝门外打）。</summary>
        private static void PlaceFlashlightOnFloor(Transform props)
        {
            Transform flashlight = Require(props, "Pickup_Flashlight");
            if (flashlight == null)
            {
                return;
            }

            BuildFlashlightModel(flashlight);
            // 原点在地面（模型躺在地上）；运行时 FlashlightPickup 会把手电头转向门洞
            flashlight.localPosition = new Vector3(FlashlightFloorX, Floor2Y, ToiletDoorZ);
            flashlight.localRotation = Quaternion.Euler(0f, 20f, 0f);
        }

        /// <summary>
        /// 手电模型：躺着的圆筒，手电头朝本地 +X。尾盖 + 筒身 + 收口 + 灯头 + 黄铜灯圈 + 发光镜片 + 红色按钮。
        /// 生成灰盒时它是一块拉伸的方块，这里换成一组子物体（根物体保留组件和碰撞盒，缩放归 1）。每次都重建 Model，可重复执行。
        /// </summary>
        private static void BuildFlashlightModel(Transform flashlight)
        {
            MeshRenderer oldRenderer = flashlight.GetComponent<MeshRenderer>();
            if (oldRenderer != null)
            {
                Object.DestroyImmediate(oldRenderer);
                Object.DestroyImmediate(flashlight.GetComponent<MeshFilter>());
            }

            flashlight.localScale = Vector3.one;
            Transform old = flashlight.Find("Model");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            const float axis = 0.0325f; // 灯头半径：灯头着地，筒身略微离地
            BoxCollider box = Ensure<BoxCollider>(flashlight.gameObject);
            box.center = new Vector3(0.003f, axis, 0f);
            box.size = new Vector3(0.28f, 0.066f, 0.066f);

            Transform m = NewGroup(flashlight, "Model");
            FlashlightRod(m, "TailCap", -0.135f, -0.12f, 0.045f, axis, Finishes.MetalDark);
            FlashlightRod(m, "Barrel", -0.12f, 0.05f, 0.042f, axis, Finishes.MetalDark);
            FlashlightRod(m, "Collar", 0.05f, 0.07f, 0.052f, axis, Finishes.MetalDark);
            FlashlightRod(m, "Head", 0.07f, 0.13f, 0.065f, axis, Finishes.MetalDark);
            FlashlightRod(m, "Bezel", 0.13f, 0.138f, 0.067f, axis, Finishes.Brass);
            FlashlightRod(m, "Lens", 0.138f, 0.141f, 0.056f, axis, Finishes.LampShade);
            for (int i = 0; i < 3; i++)
            {
                FlashlightRod(m, "Grip_" + i, -0.1f + i * 0.02f, -0.094f + i * 0.02f, 0.045f, axis, Finishes.Brass);
            }
            LocalBox(m, "Switch", new Vector3(0.015f, axis + 0.023f, 0f), new Vector3(0.022f, 0.01f, 0.014f), Finishes.RedPaint);
        }

        /// <summary>沿本地 X 轴的一段圆柱（x0 到 x1），轴心高 axis。</summary>
        private static void FlashlightRod(Transform parent, string name, float x0, float x1, float diameter, float axis, Material mat)
        {
            GameObject go = LocalCylinder(parent, name, new Vector3((x0 + x1) * 0.5f, axis, 0f), diameter, x1 - x0, mat);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        /// <summary>
        /// 雕塑厅的监控：旧版是一块黄色方块（KeyItem 材质），换成和 Cam_Reception 一样的 SecurityCamera 模型，位置和朝向不变。
        /// 已经是新模型（有 Body 子物体）就跳过。
        /// </summary>
        private static void UpgradeSculptureCamera(Transform props)
        {
            Transform cam = Require(props, "SecurityCamera_Sculpture");
            if (cam == null || cam.Find("Body") != null)
            {
                return;
            }

            Transform parent = cam.parent;
            Vector3 mount = cam.localPosition;
            Vector3 forward = parent.InverseTransformDirection(cam.forward);
            int index = cam.GetSiblingIndex();
            Object.DestroyImmediate(cam.gameObject);
            SecurityCamera(parent, "SecurityCamera_Sculpture", mount, mount + forward);
            parent.Find("SecurityCamera_Sculpture").SetSiblingIndex(index);
        }

        /// <summary>第 8 步：前台电话。组里只有无碰撞的小方块，补一个罩住底座和话筒的碰撞盒。</summary>
        private static void WirePhone(Transform props)
        {
            Transform phone = Require(Require(props, "FrontDesk"), "Phone");
            if (phone == null)
            {
                return;
            }

            BoxCollider box = Ensure<BoxCollider>(phone.gameObject);
            box.center = new Vector3(0f, 0.07f, 0f);
            box.size = new Vector3(0.3f, 0.14f, 0.22f);
            Ensure<PhoneRing>(phone.gameObject);
        }

        /// <summary>
        /// 第 16 步：前台抽屉。生成灰盒时它只是一块面板，拉开后整块悬在半空。
        /// 这里换成完整的抽屉：面板 + 把手 + 往柜身里伸进去的屉盒（底、两侧、后板）。屉盒深 0.45 m，比拉开的 0.3 m 长，
        /// 拉到头也还有一截插在柜身里，看起来是从柜子里抽出来的。根物体保留组件，缩放归 1，碰撞盒罩住面板。每次都重建，可重复执行。
        /// </summary>
        // 前台柜身员工那面 z = 0.4；文件夹厚 0.012，中心再往外 0.007。
        // x = ±0.75：抽屉宽 ±0.35、两侧柜门从 ±0.95 开始，便签宽 0.26，正好在中间
        private static readonly Vector3 NotePosT14 = new Vector3(-0.75f, 0.73f, 0.407f);
        private static readonly Vector3 NotePosT15 = new Vector3(0.75f, 0.73f, 0.407f);

        private static void MoveDeskNote(Transform desk, string name, Vector3 localPos)
        {
            Transform note = FindDeep(desk, name);
            if (note == null || note.parent != desk)
            {
                Debug.LogWarning($"[Graybox] {name} 不在 {desk.name} 下，没挪位置");
                return;
            }

            note.localPosition = localPos;
        }

        private static void WireDrawer(Transform props)
        {
            Transform drawer = Require(props, "C_FrontDesk_Drawer");
            if (drawer == null)
            {
                return;
            }

            // 旧场景里 T14/T15 贴在抽屉面板前面（z 0.475）：抽屉一拉就穿模，抽屉碰撞体还挡住准星
            MoveDeskNote(drawer.parent, "Text_T14_Workbook", NotePosT14);
            MoveDeskNote(drawer.parent, "Text_T15_CaseReport", NotePosT15);

            const float w = 0.7f;      // 面板宽
            const float h = 0.22f;     // 面板高
            const float front = 0.06f; // 面板厚（本地 z −0.03..+0.03，背面贴着柜身）
            const float depth = 0.45f; // 屉盒伸进柜身的深度
            const float wall = 0.015f;

            MeshRenderer oldRenderer = drawer.GetComponent<MeshRenderer>();
            if (oldRenderer != null)
            {
                Object.DestroyImmediate(oldRenderer);
                Object.DestroyImmediate(drawer.GetComponent<MeshFilter>());
            }

            drawer.localScale = Vector3.one;
            // 旧把手（生成时在 FrontDesk 下，或者上一版挂到了抽屉下）和上一次生成的屉盒都删掉重建
            for (Transform handle = FindDeep(drawer.parent, "Drawer_Handle"); handle != null; handle = FindDeep(drawer.parent, "Drawer_Handle"))
            {
                Object.DestroyImmediate(handle.gameObject);
            }
            Transform oldModel = drawer.Find("Model");
            if (oldModel != null)
            {
                Object.DestroyImmediate(oldModel.gameObject);
            }

            Transform m = NewGroup(drawer, "Model");
            LocalBox(m, "Front", Vector3.zero, new Vector3(w, h, front), Finishes.Trim);
            LocalBox(drawer, "Drawer_Handle", new Vector3(0f, 0f, front * 0.5f + 0.0125f), new Vector3(0.16f, 0.025f, 0.025f), Finishes.Brass);

            float boxW = w - 0.06f;
            float boxH = h - 0.05f;
            float z0 = -front * 0.5f;          // 面板背面
            float zc = z0 - depth * 0.5f;
            float yBottom = -h * 0.5f + 0.03f;
            LocalBox(m, "Tray_Bottom", new Vector3(0f, yBottom + wall * 0.5f, zc), new Vector3(boxW, wall, depth), Finishes.Wainscot);
            LocalBox(m, "Tray_Side_L", new Vector3(-(boxW - wall) * 0.5f, yBottom + boxH * 0.5f, zc), new Vector3(wall, boxH, depth), Finishes.Wainscot);
            LocalBox(m, "Tray_Side_R", new Vector3((boxW - wall) * 0.5f, yBottom + boxH * 0.5f, zc), new Vector3(wall, boxH, depth), Finishes.Wainscot);
            LocalBox(m, "Tray_Back", new Vector3(0f, yBottom + boxH * 0.5f, z0 - depth + wall * 0.5f), new Vector3(boxW, boxH, wall), Finishes.Wainscot);

            BoxCollider box = Ensure<BoxCollider>(drawer.gameObject);
            box.center = new Vector3(0f, 0f, 0.0125f);
            box.size = new Vector3(w, h, front + 0.025f);

            Ensure<FrontDeskDrawer>(drawer.gameObject);
        }

        /// <summary>第 15 步：配电间保安柜 + 柜门上挂的一张工作卡（拿走后隐藏）。</summary>
        private static void WireCabinet(Transform props)
        {
            Transform cabinet = Require(props, "B_SecurityCabinet");
            if (cabinet == null)
            {
                return;
            }

            Transform old = FindDeep(cabinet.parent, "StaffCard");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            // 柜子正面朝北（门洞方向），卡挂在正面 1.1 m 高、7 岁孩子平视的位置
            float front = ReceptionNorthZ + HalfWall + 0.3f + 0.3f;
            GameObject card = LocalBox(cabinet.parent, "StaffCard", new Vector3(PowerDoorX + 0.3f, Floor1Y + 1.1f, front + 0.006f),
                new Vector3(0.09f, 0.055f, 0.01f), Finishes.Label);

            StaffCardCabinet c = Ensure<StaffCardCabinet>(cabinet.gameObject);
            SetRef(c, "cardVisual", card);
        }

        /// <summary>
        /// 第 13、17 步：正门内侧的锁。一块贴着门板内面的薄碰撞板（MainDoorLock 序章时关掉）+ 黄铜锁片 + 插上才显示的钥匙。
        /// 挂在门的根节点上（不跟门板转，真实馆里这扇门一直锁着）。
        /// </summary>
        private static void WireMainDoorLock(Transform door)
        {
            if (door == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 找不到 MainDoor_Double，跳过正门锁。");
                return;
            }

            Transform old = door.Find("MainDoor_Lock");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            const float thickness = 0.08f;
            float inner = HalfWall - thickness * 0.5f - 0.01f + thickness * 0.5f;   // 门板内（馆内 +Z）表面

            Transform lockRoot = NewGroup(door, "MainDoor_Lock");
            BoxCollider plate = lockRoot.gameObject.AddComponent<BoxCollider>();
            plate.center = new Vector3(0f, MainDoorHeight * 0.5f, inner + 0.12f);
            plate.size = new Vector3(MainDoorWidth, MainDoorHeight, 0.04f);

            GameObject plateVisual = LocalBox(lockRoot, "LockPlate", new Vector3(0f, 1.09f, inner + 0.02f), new Vector3(0.16f, 0.26f, 0.04f), Finishes.Brass);
            GameObject key = LocalBox(lockRoot, "Key_InLock", new Vector3(0f, 1.05f, inner + 0.09f), new Vector3(0.03f, 0.03f, 0.1f), Finishes.Brass);
            key.SetActive(false);

            MainDoorLock lockComp = lockRoot.gameObject.AddComponent<MainDoorLock>();
            SetRef(lockComp, "keyVisual", key);
            SetRef(lockComp, "lockVisual", plateVisual);
        }

        /// <summary>名画人物不进全家福厅（GDD §6.9）。门洞本身留在外面，它们停在门口。</summary>
        private static void WireFigureNoGo()
        {
            float x0 = EastLineX + HalfWall + 0.3f;
            Vector3 center = new Vector3((x0 + BuildingWidth) * 0.5f, Floor2Y + 2f, (FamilySouthZ + FamilyNorthZ) * 0.5f);
            Vector3 size = new Vector3(BuildingWidth - x0, 4f, FamilyNorthZ - FamilySouthZ);
            foreach (PaintingFigure figure in Object.FindObjectsByType<PaintingFigure>(FindObjectsInactive.Include))
            {
                SerializedObject so = new SerializedObject(figure);
                so.FindProperty("noGo").boundsValue = new Bounds(center * WorldScale, size * WorldScale);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
