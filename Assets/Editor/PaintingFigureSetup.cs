using Ib.Threats;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Ib.EditorTools
{
    /// <summary>
    /// 二楼名画人物换成 Girl 模型（Assets/Art/Models/Girl/Girl.fbx，由 Blender/Unity_Project_Ib/Girl.blend 导出）。
    /// 可重复执行：重新生成灰盒之后再跑一次。
    /// 占位胶囊的根节点是非等比缩放的，这里把根缩放归一、拆掉胶囊网格，换成同尺寸的 CapsuleCollider + Girl 外形子物体。
    /// 模型自带的动作做成循环的 Animator，PaintingFigure 按移动速度控制播放（站着不动时定格）。
    /// </summary>
    public static class PaintingFigureSetup
    {
        private const string ModelPath = "Assets/Art/Models/Girl/Girl.fbx";
        private const string ControllerPath = "Assets/Art/Models/Girl/Girl.controller";
        private const float FigureHeight = 1.75f;   // 和原占位胶囊一样高
        private const float FigureRadius = 0.28f;
        private const float StandingHeight = 1.65f; // 模型站直（绑定姿势）时的身高，决定整体缩放
        private const float ModelYawFix = 0f;       // Blender 导出的 FBX 正面已经是 +Z

        [MenuItem("Tools/Ib/二楼名画人物换成 Girl 模型")]
        public static void Setup()
        {
            ConfigureImporter();
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("二楼名画人物", "找不到 " + ModelPath, "好");
                return;
            }

            AnimationClip clip;
            RuntimeAnimatorController controller = BuildController(out clip);
            Avatar avatar = AssetDatabase.LoadAssetAtPath<Avatar>(ModelPath);
            Measure(asset, clip, out float restHeight, out float crawlFloor);
            float scale = restHeight > 0.01f ? StandingHeight / restHeight : 1f;
            Debug.Log($"[PaintingFigureSetup] 站姿高 {restHeight:F2} m，爬行最低点 {crawlFloor:F2} m，缩放 {scale:F3}");
            PaintingFigure[] figures = Object.FindObjectsByType<PaintingFigure>(FindObjectsInactive.Include);
            if (figures.Length == 0)
            {
                EditorUtility.DisplayDialog("二楼名画人物", "场景里没有 PaintingFigure，先生成灰盒关卡。", "好");
                return;
            }

            foreach (PaintingFigure figure in figures)
            {
                GameObject go = figure.gameObject;

                // 根缩放归一（胶囊是靠缩放撑出 1.75 m 的），位置不变：根仍在身体中心
                go.transform.localScale = Vector3.one;
                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                if (mr != null) Object.DestroyImmediate(mr);
                MeshFilter mf = go.GetComponent<MeshFilter>();
                if (mf != null) Object.DestroyImmediate(mf);

                CapsuleCollider cap = go.GetComponent<CapsuleCollider>();
                if (cap == null) cap = go.AddComponent<CapsuleCollider>();
                cap.center = Vector3.zero;
                cap.height = FigureHeight;
                cap.radius = FigureRadius;

                // 预先挂一个禁用的 NavMeshAgent：PaintingFigure.Awake 里没有时会 AddComponent，
                // 而运行中添加的 Agent 默认是启用的，会在第一帧把站在画框里（离导航网格有距离）的人物
                // 吸到最近的导航网格上（实测会掉到一楼/楼外）。场景里先放一个禁用的，Awake 就只改参数不重建。
                NavMeshAgent preAgent = go.GetComponent<NavMeshAgent>();
                if (preAgent == null) preAgent = go.AddComponent<NavMeshAgent>();
                preAgent.enabled = false;

                Transform old = go.transform.Find("Visual_Girl");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                GameObject visual = new GameObject("Visual_Girl");
                visual.transform.SetParent(go.transform, false);
                visual.transform.localPosition = new Vector3(0f, -FigureHeight * 0.5f, 0f);
                visual.transform.localRotation = Quaternion.Euler(0f, ModelYawFix, 0f);

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, visual.transform);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;

                // 缩放按站姿身高；高度按爬行姿势的最低点贴地（Visual_Girl 原点就在胶囊底 = 地面）。
                // 不能用 Renderer.bounds：蒙皮网格的包围盒不跟随动作，之前就是这样把她抬到了半空。
                visual.transform.localScale = Vector3.one * scale;
                instance.transform.localPosition = new Vector3(0f, -crawlFloor, 0f);

                foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
                }

                Animator anim = instance.GetComponent<Animator>();
                if (anim == null) anim = instance.AddComponent<Animator>();
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (anim.avatar == null) anim.avatar = avatar;

                EditorUtility.SetDirty(go);
            }

            EditorSceneManager.MarkSceneDirty(figures[0].gameObject.scene);
            EditorSceneManager.SaveScene(figures[0].gameObject.scene);
            Debug.Log("[PaintingFigureSetup] 二楼名画人物已换成 Girl 模型：" + figures.Length + " 个。");
        }

        /// <summary>
        /// 用一个临时实例实测：绑定姿势的身高，以及爬行动作里网格最低点的平均高度（模型原点为 0）。
        /// 蒙皮网格要 BakeMesh 才是真实顶点位置。
        /// </summary>
        private static void Measure(GameObject asset, AnimationClip clip, out float restHeight, out float crawlFloor)
        {
            GameObject tmp = Object.Instantiate(asset);
            tmp.hideFlags = HideFlags.HideAndDontSave;
            tmp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            tmp.transform.localScale = Vector3.one;
            try
            {
                MinMaxY(tmp, out float lo, out float hi);
                restHeight = hi - lo;
                crawlFloor = lo;
                if (clip != null)
                {
                    const int samples = 12;
                    float sum = 0f;
                    for (int i = 0; i < samples; i++)
                    {
                        clip.SampleAnimation(tmp, clip.length * i / samples);
                        MinMaxY(tmp, out lo, out hi);
                        sum += lo;
                    }
                    crawlFloor = sum / samples;
                }
            }
            finally
            {
                Object.DestroyImmediate(tmp);
            }
        }

        private static void MinMaxY(GameObject root, out float lo, out float hi)
        {
            lo = float.MaxValue;
            hi = float.MinValue;
            Mesh baked = new Mesh();
            foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.BakeMesh(baked, false);
                Matrix4x4 m = smr.transform.localToWorldMatrix;
                foreach (Vector3 v in baked.vertices)
                {
                    float y = m.MultiplyPoint3x4(v).y;
                    if (y < lo) lo = y;
                    if (y > hi) hi = y;
                }
            }
            Object.DestroyImmediate(baked);
            if (lo > hi) { lo = 0f; hi = 0f; }
        }

        /// <summary>Generic 骨骼 + 动作循环；材质用 FBX 里嵌的贴图。</summary>
        private static void ConfigureImporter()
        {
            ModelImporter imp = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (imp == null) return;

            bool dirty = false;
            // Girl.blend 里自带 Blender 的摄像机和灯光：导进来会抢走玩家画面，必须关掉
            if (imp.importCameras) { imp.importCameras = false; dirty = true; }
            if (imp.importLights) { imp.importLights = false; dirty = true; }
            if (imp.animationType != ModelImporterAnimationType.Generic)
            {
                imp.animationType = ModelImporterAnimationType.Generic;
                dirty = true;
            }
            // Generic 骨骼要有 Avatar，动作才稳定驱动骨骼
            if (imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                dirty = true;
            }

            // 重新导出后动作的 take 名会变，旧的片段设置要丢掉，否则 Unity 找不到动作
            ModelImporterClipAnimation[] clips = imp.clipAnimations;
            string[] takes = System.Array.ConvertAll(imp.importedTakeInfos, t => t.name);
            if (clips != null && System.Array.Exists(clips, c => System.Array.IndexOf(takes, c.takeName) < 0))
            {
                clips = null;
                dirty = true;
            }
            if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;
            foreach (ModelImporterClipAnimation c in clips)
            {
                if (!c.loopTime) { c.loopTime = true; dirty = true; }
            }

            if (dirty)
            {
                imp.clipAnimations = clips;
                imp.SaveAndReimport();
            }
        }

        private static RuntimeAnimatorController BuildController(out AnimationClip clip)
        {
            clip = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            {
                if (o is AnimationClip c && !c.name.StartsWith("__preview__"))
                {
                    clip = c;
                    break;
                }
            }

            if (clip == null)
            {
                Debug.LogWarning("[PaintingFigureSetup] Girl.fbx 里没有动作，模型保持绑定姿势。");
                return null;
            }

            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ctrl.layers[0].stateMachine.AddState("Move").motion = clip;
            AssetDatabase.SaveAssets();
            return ctrl;
        }
    }
}
