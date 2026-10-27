using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ib.EditorTools
{
    /// <summary>
    /// 编辑用的「透视」视图：只影响 Scene 视图，不改场景、不影响游戏运行。
    /// 原理是 SceneVisibilityManager（等同于 Hierarchy 里的小眼睛），隐藏状态存在 Library 里，不进场景文件。
    ///
    /// - 自动隐藏朝向镜头的外墙：镜头在楼外哪一侧，那一侧的外墙就隐藏，绕着楼转会自动切换（像模拟人生的「墙壁降下」）；
    /// - 隐藏屋顶 / 天花：从上往下俯视时看得到房间布局；
    /// - 只看一层：隐藏所有底部高于一层天花的物体（二层墙体、楼板、道具），方便编辑一层。
    /// 只会显示 / 隐藏这三类物体，你在 Hierarchy 里手动藏起来的其它东西不受影响。
    /// </summary>
    [InitializeOnLoad]
    public static class SceneXRayView
    {
        private const string MenuRoot = "Tools/Ib/编辑视图/";
        private const string PrefWalls = "Ib.XRay.AutoHideWalls";
        private const string PrefRoof = "Ib.XRay.HideRoof";
        private const string PrefFirstFloor = "Ib.XRay.FirstFloorOnly";

        // 一层天花底面 = 6.0（见 GrayboxBuilder.Floor1Height），底部在它以上的都算二层及以上
        private const float UpperFloorY = 5.99f;

        private static readonly HashSet<GameObject> hiddenByUs = new HashSet<GameObject>();
        private static string lastSignature = "";

        static SceneXRayView()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.hierarchyChanged += () => { lastSignature = ""; boundsCache.Clear(); };
        }

        private static bool AutoHideWalls
        {
            get => EditorPrefs.GetBool(PrefWalls, false);
            set => EditorPrefs.SetBool(PrefWalls, value);
        }

        private static bool HideRoof
        {
            get => EditorPrefs.GetBool(PrefRoof, false);
            set => EditorPrefs.SetBool(PrefRoof, value);
        }

        private static bool FirstFloorOnly
        {
            get => EditorPrefs.GetBool(PrefFirstFloor, false);
            set => EditorPrefs.SetBool(PrefFirstFloor, value);
        }

        // ================================================================ 菜单

        [MenuItem(MenuRoot + "自动隐藏朝向镜头的外墙")]
        private static void ToggleWalls() { AutoHideWalls = !AutoHideWalls; Refresh(); }

        [MenuItem(MenuRoot + "自动隐藏朝向镜头的外墙", true)]
        private static bool ToggleWallsCheck() { Menu.SetChecked(MenuRoot + "自动隐藏朝向镜头的外墙", AutoHideWalls); return true; }

        [MenuItem(MenuRoot + "隐藏屋顶和天花")]
        private static void ToggleRoof() { HideRoof = !HideRoof; Refresh(); }

        [MenuItem(MenuRoot + "隐藏屋顶和天花", true)]
        private static bool ToggleRoofCheck() { Menu.SetChecked(MenuRoot + "隐藏屋顶和天花", HideRoof); return true; }

        [MenuItem(MenuRoot + "只看一层")]
        private static void ToggleFirstFloor() { FirstFloorOnly = !FirstFloorOnly; Refresh(); }

        [MenuItem(MenuRoot + "只看一层", true)]
        private static bool ToggleFirstFloorCheck() { Menu.SetChecked(MenuRoot + "只看一层", FirstFloorOnly); return true; }

        [MenuItem(MenuRoot + "全部恢复显示")]
        private static void ShowAll()
        {
            AutoHideWalls = false;
            HideRoof = false;
            FirstFloorOnly = false;
            Refresh();
        }

        // ================================================================ 核心

        private static void OnSceneGUI(SceneView view)
        {
            if (!AutoHideWalls && !HideRoof && !FirstFloorOnly && hiddenByUs.Count == 0)
                return;
            if (Event.current.type != EventType.Repaint || view != SceneView.lastActiveSceneView)
                return;
            Apply(view.camera.transform.position, false);
        }

        private static void Refresh()
        {
            lastSignature = "";
            boundsCache.Clear();
            SceneView view = SceneView.lastActiveSceneView;
            Vector3 cam = view != null ? view.camera.transform.position : Vector3.up * 1000f;
            Apply(cam, true);
            SceneView.RepaintAll();
        }

        private static void Apply(Vector3 cameraPos, bool force)
        {
            var wanted = new HashSet<GameObject>();
            var managed = new HashSet<GameObject>();

            foreach (Renderer r in AllRenderers())
            {
                GameObject go = r.gameObject;
                string path = PathOf(go.transform);

                bool isExteriorWall = IsExteriorWall(path, out Transform buildingRoot);
                bool isRoof = IsRoof(path);
                bool isUpper = r.bounds.min.y > UpperFloorY;

                if (!isExteriorWall && !isRoof && !isUpper)
                    continue;
                managed.Add(go);

                if (isRoof && HideRoof)
                    wanted.Add(go);
                if (isUpper && FirstFloorOnly)
                    wanted.Add(go);
                if (isExteriorWall && AutoHideWalls && FacesCamera(r.bounds, buildingRoot, cameraPos))
                    wanted.Add(go);
            }

            // 只有隐藏集合变了才去碰 SceneVisibilityManager，绕楼拖镜头时不会每帧刷新
            string signature = Signature(wanted);
            if (!force && signature == lastSignature)
                return;
            lastSignature = signature;

            SceneVisibilityManager svm = SceneVisibilityManager.instance;
            foreach (GameObject go in managed)
            {
                bool hide = wanted.Contains(go);
                bool hidden = svm.IsHidden(go, false);
                if (hide && !hidden) svm.Hide(go, false);
                else if (!hide && hidden) svm.Show(go, false);
            }

            hiddenByUs.Clear();
            hiddenByUs.UnionWith(wanted);
        }

        /// <summary>美术馆外墙（Walls_1F / Walls_2F 下的 Ext_*）和黑屋的外墙（Shell/Wall_*）。</summary>
        private static bool IsExteriorWall(string path, out Transform buildingRoot)
        {
            buildingRoot = null;
            if (path.StartsWith("Level_Museum/Walls_") && path.Contains("/Ext_"))
                buildingRoot = FindRoot("Level_Museum");
            else if (path.StartsWith("Level_BlackRoom/Shell/Wall_"))
                buildingRoot = FindRoot("Level_BlackRoom");
            return buildingRoot != null;
        }

        private static bool IsRoof(string path)
        {
            return path.StartsWith("Level_Museum/Shell/Roof")
                || path.StartsWith("Level_Museum/Shell/Ceiling_")
                || path.StartsWith("Level_BlackRoom/Shell/Ceiling");
        }

        /// <summary>墙的外法线 = 墙最薄的水平轴，方向背离所在建筑的中心；镜头在外侧就隐藏。</summary>
        private static bool FacesCamera(Bounds wall, Transform buildingRoot, Vector3 cameraPos)
        {
            Vector3 center = BuildingBounds(buildingRoot).center;
            Vector3 outward = wall.size.x < wall.size.z
                ? new Vector3(Mathf.Sign(wall.center.x - center.x), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(wall.center.z - center.z));
            return Vector3.Dot(cameraPos - wall.center, outward) > 0f;
        }

        private static readonly Dictionary<Transform, Bounds> boundsCache = new Dictionary<Transform, Bounds>();

        private static Bounds BuildingBounds(Transform root)
        {
            if (boundsCache.TryGetValue(root, out Bounds b))
                return b;
            Renderer[] rs = root.GetComponentsInChildren<Renderer>();
            b = rs.Length > 0 ? rs[0].bounds : new Bounds(root.position, Vector3.zero);
            foreach (Renderer r in rs)
                b.Encapsulate(r.bounds);
            boundsCache[root] = b;
            return b;
        }

        // ================================================================ 工具

        private static IEnumerable<Renderer> AllRenderers()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                        yield return r;
            }
        }

        private static Transform FindRoot(string name)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.name == name) return root.transform;
            }
            return null;
        }

        private static string PathOf(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        private static string Signature(HashSet<GameObject> set)
        {
            var ids = new List<int>(set.Count);
            foreach (GameObject go in set) ids.Add(go.GetHashCode());
            ids.Sort();
            return string.Join(",", ids);
        }
    }
}
