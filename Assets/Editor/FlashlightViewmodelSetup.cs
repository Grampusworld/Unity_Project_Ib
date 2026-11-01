using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Ib.EditorTools
{
    /// <summary>
    /// 第一人称手电筒（Ib 美术 2026-11-01）：Flashlight_Viewmodel.fbx（Tools/build_flashlight_viewmodel.py 生成）
    /// → 外部 URP 材质 + Flashlight_Viewmodel.prefab。
    /// 只含手电本体，不含手（2026-11-01 起）。prefab 约定：根节点原点 = 握持处，灯头朝根的 +Z、
    /// 开关朝 +Y；根下 LightMount 空节点在镜片前 1 mm，旋转为单位（+Z 朝前），代码在这里挂 SpotLight。
    /// 渲染器全部不投影。说明见 Docs/Chase_Audio_Viewmodel.md。可重复执行；prefab 不存在时编辑器启动后自动跑一次。
    /// </summary>
    public static class FlashlightViewmodelSetup
    {
        public const string FbxPath = "Assets/Art/Models/Props/Flashlight_Viewmodel.fbx";
        public const string PrefabPath = "Assets/Art/Models/Props/Flashlight_Viewmodel.prefab";
        private const string MatDir = "Assets/Art/Materials/Props/";

        [MenuItem("Tools/Ib/生成第一人称手电筒 prefab")]
        public static void Build()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null)
            {
                Debug.LogError("[FlashlightViewmodel] 找不到 " + FbxPath);
                return;
            }

            // 1) 材质
            Material metal = Mat("M_FL_Metal", new Color(0.08f, 0.085f, 0.09f), 0.85f, 0.62f);
            Material rubber = Mat("M_FL_Rubber", new Color(0.025f, 0.025f, 0.025f), 0f, 0.15f);
            Material chrome = Mat("M_FL_Chrome", new Color(0.9f, 0.9f, 0.9f), 1f, 0.92f);
            Material lensOn = Mat("M_FL_Lens_On", new Color(1f, 0.93f, 0.78f), 0f, 0.95f, new Color(1f, 0.88f, 0.65f) * 2.2f);
            Mat("M_FL_Lens_Off", new Color(0.55f, 0.55f, 0.52f), 0f, 0.95f);

            // 2) 导入设置 + 材质重映射
            var mi = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.addCollider = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            Remap(mi, "FL_Metal", metal);
            Remap(mi, "FL_Rubber", rubber);
            Remap(mi, "FL_Chrome", chrome);
            Remap(mi, "FL_Lens", lensOn);
            mi.SaveAndReimport();
            fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);

            // 3) 在预览场景里拼 prefab，不碰当前打开的场景
            Scene ps = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Flashlight_Viewmodel");
                SceneManager.MoveGameObjectToScene(root, ps);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, ps);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.name = "Model";
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;

                Transform mount = Find(model.transform, "LightMount");
                Transform button = Find(model.transform, "FL_Button");
                if (mount == null || button == null)
                {
                    Debug.LogError("[FlashlightViewmodel] FBX 里缺 LightMount / FL_Button");
                    return;
                }

                // 灯头 → +Z，开关 → +Y（与 FBX 轴向换算无关，按实际几何摆正）
                Vector3 fwd = mount.position - root.transform.position;
                Vector3 up = Vector3.ProjectOnPlane(Center(button) - root.transform.position, fwd);
                model.transform.rotation = Quaternion.Inverse(Quaternion.LookRotation(fwd.normalized, up.normalized));

                Vector3 mountPos = root.transform.InverseTransformPoint(mount.position);
                Object.DestroyImmediate(mount.gameObject);
                var lm = new GameObject("LightMount").transform;
                lm.SetParent(root.transform, false);
                lm.localPosition = mountPos + new Vector3(0f, 0f, 0.001f);
                lm.localRotation = Quaternion.identity;

                foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.lightProbeUsage = LightProbeUsage.BlendProbes;
                }

                Bounds all = new Bounds(Vector3.zero, Vector3.zero);
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                {
                    all.Encapsulate(r.bounds);
                }
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                {
                    if (r.name.StartsWith("FL_Hand"))
                    {
                        Debug.LogError("[FlashlightViewmodel] prefab 里仍有手部网格 " + r.name);
                        return;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                Debug.Log(string.Format(
                    "[FlashlightViewmodel] {0} prefab={1} LightMount={2} 包围盒 min={3} max={4}（无手部网格）",
                    ok ? "OK" : "FAILED", PrefabPath, lm.localPosition.ToString("F3"), all.min.ToString("F3"),
                    all.max.ToString("F3")));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(ps);
            }
            AssetDatabase.SaveAssets();
        }

        private static Material Mat(string name, Color c, float metal, float smooth, Color? emission = null)
        {
            string path = MatDir + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Smoothness", smooth);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void Remap(ModelImporter mi, string sourceName, Material m)
        {
            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceName), m);
        }

        private static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                Transform f = Find(c, name);
                if (f != null) return f;
            }
            return null;
        }

        private static Vector3 Center(Transform t)
        {
            var r = t.GetComponent<Renderer>();
            return r != null ? r.bounds.center : t.position;
        }
    }

    [InitializeOnLoad]
    internal static class FlashlightViewmodelAutoRun
    {
        static FlashlightViewmodelAutoRun()
        {
            EditorApplication.delayCall += TryRun;
        }

        private static void TryRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                AssetDatabase.LoadAssetAtPath<GameObject>(FlashlightViewmodelSetup.PrefabPath) != null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(FlashlightViewmodelSetup.FbxPath) == null)
            {
                return;
            }
            FlashlightViewmodelSetup.Build();
        }
    }
}
