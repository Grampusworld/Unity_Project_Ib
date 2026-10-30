using Ib.Environment;
using Ib.Player;
using Ib.Threats;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Ib.EditorTools
{
    /// <summary>
    /// 把哭泣天使布置进灰盒场景（可重复执行，重新生成灰盒之后再跑一次就行）：
    /// 1. 雕塑厅正中央展台（Threats_Placeholder/Threat_WeepingAngel/Plinth）上放天使，隐藏红色占位方块；
    ///    三个阶段的外形 = Angel_Still / Angel_Attack1 / Angel_Attack2 三个 FBX；
    /// 2. 一层巡逻路线（环线，经过雕塑厅 → 油画厅 → 接待厅 → 服装厅 → 楼梯厅）；
    /// 3. 只覆盖一层的 NavMeshSurface，烘焙并存成资源；
    /// 4. 玩家 Prefab 的相机上挂 PlayerFlashlight（默认没有手电，调试菜单里给）。
    /// </summary>
    public static class WeepingAngelSetup
    {
        private const string ModelDir = "Assets/Art/Models/Angels/";
        private const string StoneMaterialPath = "Assets/Art/Materials/M_Stone_Angel.mat";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        private const string NavMeshDataPath = "Assets/Scenes/Graybox_NavMesh_1F.asset";

        /// <summary>一层巡逻环线（按 1 倍坐标写，用时乘 WorldScale）。人群站位也要避开这条线。</summary>
        public static readonly Vector3[] PatrolPoints =
        {
            new Vector3(15.5f, 0f, 17.5f),   // 雕塑厅西北
            new Vector3(11.5f, 0f, 15f),     // 雕塑厅 → 油画厅门
            new Vector3(6.5f, 0f, 21f),      // 油画厅北
            new Vector3(6.5f, 0f, 5f),       // 油画厅南
            new Vector3(14.5f, 0f, 4.5f),    // 油画厅 → 接待厅门
            new Vector3(19.5f, 0f, 5.5f),    // 接待厅中
            new Vector3(27.5f, 0f, 4.5f),    // 接待厅 → 服装厅门
            new Vector3(33f, 0f, 4f),        // 服装厅南
            new Vector3(33f, 0f, 21.5f),     // 服装厅北
            new Vector3(24.5f, 0f, 15f),     // 服装厅 → 雕塑厅门
            new Vector3(19.5f, 0f, 23f),     // 楼梯厅
        };

        private const float PlinthTop = 0.4f;   // GrayboxBuilder 的展台：1.2 × 0.4 × 1.2

        // 模型统一 1.9 m 高（导出时定的），再整体放大到 1.2 倍
        private const float ModelScale = 1.2f;
        private const float AngelHeight = 1.9f * ModelScale;

        // 模型在 FBX 里正面朝哪（绕 Y 的修正角）；Blender 导出的 FBX 正面已经是 +Z，不用转
        private const float ModelYawFix = 0f;

        [MenuItem("Tools/Ib/布置哭泣天使")]
        public static void Setup()
        {
            GameObject placeholder = GameObject.Find("Threats_Placeholder/Threat_WeepingAngel");
            if (placeholder == null)
            {
                EditorUtility.DisplayDialog("布置哭泣天使", "场景里找不到 Threats_Placeholder/Threat_WeepingAngel，先生成灰盒关卡。", "好");
                return;
            }

            Material stone = GetStoneMaterial();

            // ---- 1. 占位方块藏起来，展台留着 ----
            foreach (string n in new[] { "Body", "Face_Forward" })
            {
                Transform t = placeholder.transform.Find(n);
                if (t != null)
                {
                    t.gameObject.SetActive(false);
                }
            }

            Transform old = placeholder.transform.Find("WeepingAngel");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            GameObject angelGO = new GameObject("WeepingAngel");
            angelGO.transform.SetParent(placeholder.transform, false);
            angelGO.transform.localPosition = new Vector3(0f, PlinthTop, 0f);
            angelGO.transform.localRotation = Quaternion.identity;

            GameObject still = AddVisual(angelGO.transform, "Visual_Still", "Angel_Still", stone);
            GameObject peek = AddVisual(angelGO.transform, "Visual_Peek", "Angel_Attack1", stone);
            GameObject attack = AddVisual(angelGO.transform, "Visual_Attack", "Angel_Attack2", stone);

            // 身体碰撞：挡玩家，也让射线能打到它（注视判定会忽略它自己）
            CapsuleCollider body = angelGO.AddComponent<CapsuleCollider>();
            body.center = new Vector3(0f, AngelHeight * 0.5f, 0f);
            body.height = AngelHeight;
            body.radius = 0.35f * ModelScale;

            NavMeshModifier modifier = angelGO.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;

            NavMeshAgent agent = angelGO.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f * ModelScale;
            agent.height = AngelHeight;
            agent.acceleration = 30f;
            agent.angularSpeed = 540f;
            agent.stoppingDistance = 0.2f;
            agent.autoBraking = false;
            agent.enabled = false;

            WeepingAngel angel = angelGO.AddComponent<WeepingAngel>();
            WaypointPath path = BuildPatrolPath();

            SerializedObject so = new SerializedObject(angel);
            so.FindProperty("stillVisual").objectReferenceValue = still;
            so.FindProperty("peekVisual").objectReferenceValue = peek;
            so.FindProperty("attackVisual").objectReferenceValue = attack;
            so.FindProperty("patrolPath").objectReferenceValue = path;
            so.FindProperty("eyeOffset").vector3Value = new Vector3(0f, AngelHeight - 0.2f, 0f);
            SerializedProperty samples = so.FindProperty("samplePoints");
            float[] heights = { 0.92f, 0.66f, 0.32f, 0.63f, 0.63f, 0.05f };
            float[] sides = { 0f, 0f, 0f, 0.3f, -0.3f, 0f };
            samples.arraySize = heights.Length;
            for (int i = 0; i < heights.Length; i++)
            {
                samples.GetArrayElementAtIndex(i).vector3Value = new Vector3(sides[i] * ModelScale, heights[i] * AngelHeight, 0f);
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // 编辑器里只显示静止外形
            peek.SetActive(false);
            attack.SetActive(false);

            // ---- 3. 导航网格 ----
            BakeNavMesh();

            // ---- 4. 手电 ----
            AddFlashlightToPlayerPrefab();

            EditorSceneManager.MarkSceneDirty(angelGO.scene);
            EditorSceneManager.SaveScene(angelGO.scene);
            Selection.activeGameObject = angelGO;
            Debug.Log("[WeepingAngelSetup] 哭泣天使已布置到雕塑厅展台，巡逻路线和导航网格已生成。");
        }

        private static GameObject AddVisual(Transform parent, string name, string model, Material stone)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + model + ".fbx");
            if (asset == null)
            {
                Debug.LogError("[WeepingAngelSetup] 找不到模型 " + ModelDir + model + ".fbx");
                GameObject empty = new GameObject(name);
                empty.transform.SetParent(parent, false);
                return empty;
            }

            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localRotation = Quaternion.Euler(0f, ModelYawFix, 0f);
            root.transform.localScale = Vector3.one * ModelScale;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
            instance.transform.localPosition = Vector3.zero;
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = stone;
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            // 贴地：有的 FBX 网格自带竖直偏移（Angel_Attack1 底部离原点 0.26 m），按实际包围盒把脚放到根节点高度
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                foreach (Renderer r in renderers)
                {
                    b.Encapsulate(r.bounds);
                }
                float lift = (b.min.y - parent.position.y) / ModelScale;
                instance.transform.localPosition -= new Vector3(0f, lift, 0f);
            }
            return root;
        }

        private static Material GetStoneMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(StoneMaterialPath);
            if (mat != null)
            {
                return mat;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader) { name = "M_Stone_Angel" };
            mat.SetColor("_BaseColor", new Color(0.62f, 0.60f, 0.55f));
            mat.SetFloat("_Smoothness", 0.18f);
            AssetDatabase.CreateAsset(mat, StoneMaterialPath);
            return mat;
        }

        // ================================================================ 巡逻路线

        private static WaypointPath BuildPatrolPath()
        {
            GameObject root = GameObject.Find("Threats_Placeholder");
            Transform existing = root.transform.Find("WeepingAngel_Patrol");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            GameObject pathGO = new GameObject("WeepingAngel_Patrol");
            pathGO.transform.SetParent(root.transform, false);
            WaypointPath path = pathGO.AddComponent<WaypointPath>();

            SerializedObject so = new SerializedObject(path);
            so.FindProperty("pingPong").boolValue = false;
            so.FindProperty("gizmoColor").colorValue = new Color(0.9f, 0.2f, 0.2f, 1f);
            so.ApplyModifiedPropertiesWithoutUndo();

            // 一层环线（坐标见 GrayboxBuilder 尺寸常量）：雕塑厅西 → 油画厅 → 接待厅 → 服装厅 → 雕塑厅东 → 楼梯厅
            Vector3[] points = PatrolPoints;

            for (int i = 0; i < points.Length; i++)
            {
                GameObject wp = new GameObject("P" + i);
                wp.transform.SetParent(pathGO.transform, false);
                wp.transform.position = points[i] * GrayboxBuilder.WorldScale;   // 坐标按 1 倍写，馆整体放大了
            }

            return path;
        }

        // ================================================================ 导航网格

        private static void BakeNavMesh()
        {
            GameObject go = GameObject.Find("--- NavMesh ---");
            if (go == null)
            {
                go = new GameObject("--- NavMesh ---");
            }

            NavMeshSurface surface = go.GetComponent<NavMeshSurface>();
            if (surface == null)
            {
                surface = go.AddComponent<NavMeshSurface>();
            }

            // 只烘一层：盒子罩住整栋楼的一层，上沿在一层天花（6.0）以下
            surface.collectObjects = CollectObjects.Volume;
            go.transform.position = Vector3.zero;
            surface.center = new Vector3(20f, 2.6f, 13f) * GrayboxBuilder.WorldScale;
            surface.size = new Vector3(41f, 5.6f, 27f) * GrayboxBuilder.WorldScale;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;

            surface.BuildNavMesh();

            NavMeshData data = surface.navMeshData;
            if (data != null)
            {
                if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshDataPath) != null)
                {
                    AssetDatabase.DeleteAsset(NavMeshDataPath);
                }
                AssetDatabase.CreateAsset(data, NavMeshDataPath);
                surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshDataPath);
                EditorUtility.SetDirty(surface);
            }
        }

        // ================================================================ 手电

        private static void AddFlashlightToPlayerPrefab()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Camera cam = contents.GetComponentInChildren<Camera>(true);
                if (cam != null)
                {
                    PlayerFlashlight flashlight = cam.GetComponent<PlayerFlashlight>();
                    if (flashlight == null)
                    {
                        flashlight = cam.gameObject.AddComponent<PlayerFlashlight>();
                    }

                    // Prefab 里存的是序列化值，脚本默认值改了也要同步过去
                    SerializedObject so = new SerializedObject(flashlight);
                    so.FindProperty("intensity").floatValue = 55f;
                    so.FindProperty("range").floatValue = 26f;
                    so.FindProperty("spotAngle").floatValue = 52f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
