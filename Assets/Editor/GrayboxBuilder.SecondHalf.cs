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

            flashlight.localPosition = new Vector3(FlashlightFloorX, Floor2Y + flashlight.localScale.y * 0.5f, ToiletDoorZ);
            flashlight.localRotation = Quaternion.Euler(0f, 20f, 0f);
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

        /// <summary>第 16 步：前台抽屉。生成时拆掉了碰撞体，这里补回来；把手挂到抽屉下面，拉开时一起动。</summary>
        private static void WireDrawer(Transform props)
        {
            Transform drawer = Require(props, "C_FrontDesk_Drawer");
            if (drawer == null)
            {
                return;
            }

            Ensure<BoxCollider>(drawer.gameObject);
            Transform handle = FindDeep(drawer.parent, "Drawer_Handle");
            if (handle != null && handle.parent != drawer)
            {
                handle.SetParent(drawer, true);
            }

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

            LocalBox(lockRoot, "LockPlate", new Vector3(0f, 1.09f, inner + 0.02f), new Vector3(0.16f, 0.26f, 0.04f), Finishes.Brass);
            GameObject key = LocalBox(lockRoot, "Key_InLock", new Vector3(0f, 1.05f, inner + 0.09f), new Vector3(0.03f, 0.03f, 0.1f), Finishes.Brass);
            key.SetActive(false);

            MainDoorLock lockComp = lockRoot.gameObject.AddComponent<MainDoorLock>();
            SetRef(lockComp, "keyVisual", key);
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
