using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 道具模型批量导入：统一导入参数 + 按设计尺寸归一 + 实测报告。
/// 只作用于 Assets/Art/Models/Props，不碰 Angels / Paintings / Statues。
/// 菜单：Tools/Ib/道具模型 · 配置导入
/// 批处理：-executeMethod IbImportProps.Configure
/// </summary>
public static class IbImportProps
{
    private const string PropsDir = "Assets/Art/Models/Props";
    private const string ReportPath = "/tmp/ib_props_report.txt";

    // 设计尺寸（单位 m）：轴 0=X 1=Y(高) 2=Z；0 表示不归一
    private static readonly Dictionary<string, (int axis, float target, string note)> Targets =
        new Dictionary<string, (int, float, string)>
        {
            { "01", (1, 2.20f, "画架总高") },
            { "02", (1, 0.40f, "梅瓶高") },
            { "05", (1, 0.30f, "窑铸雕塑高") },
            { "06", (0, 0.06f, "胸针宽") },
            { "07", (0, 0f, "手镯按原尺寸") },
            { "08", (0, 0f, "吊坠按原尺寸") },
            { "09", (0, 1.80f, "长凳长") },
            { "10", (1, 0.95f, "扶手椅总高") },
            { "11", (1, 0.62f, "边桌总高") },
            { "12", (1, 0f, "画笔罐按原尺寸") },
            { "13", (0, 0.40f, "调色板长") },
            { "14", (0, 0f, "颜料管按原尺寸") },
            { "15", (1, 0.15f, "油壶高") },
            { "16", (0, 0.32f, "画笔长") },
            { "17", (0, 0.60f, "画布卷长") },
            { "18", (1, 0.55f, "折叠凳高") },
            { "19", (0, 0.38f, "画箱宽") },
            { "20", (1, 0f, "沙发按原尺寸") },
            { "21", (1, 0f, "陶罐按原尺寸") },
        };

    [MenuItem("Tools/Ib/道具模型 · 配置导入")]
    public static void Configure()
    {        List<string> models = CollectModels();

        // 第 1 遍：基础导入参数，globalScale 复位为 1
        foreach (string path in models)
        {
            ModelImporter mi = GetImporter(path);
            if (mi == null) continue;
            ApplyBaseSettings(mi);
            mi.globalScale = 1f;
            mi.SaveAndReimport();
        }

        // 第 2 遍：按实测尺寸算归一系数
        foreach (string path in models)
        {
            ModelImporter mi = GetImporter(path);
            if (mi == null) continue;

            string id = Path.GetFileNameWithoutExtension(path).Substring(0, 2);
            if (!Targets.TryGetValue(id, out var t) || t.target <= 0f) continue;

            if (!TryMeasure(path, out Vector3 size))
            {
                Debug.LogWarning("[IbImportProps] 无法测量 " + path);
                continue;
            }

            float measured = size[t.axis];
            if (measured <= 0.0001f)
            {
                Debug.LogWarning("[IbImportProps] 尺寸为零，跳过归一: " + path);
                continue;
            }

            float factor = t.target / measured;
            if (factor < 0.1f || factor > 10f)
            {
                Debug.LogWarning("[IbImportProps] 系数异常 " + factor + "，跳过: " + path);
                continue;
            }

            mi.globalScale = factor;

            // 模型本身没有碰撞体，但道具随后要参与交互/阻挡，这里按包围盒补一个 BoxCollider 开关留待手工处理
            mi.SaveAndReimport();
        }

        AssetDatabase.Refresh();
        Report(models);
    }

    /// <summary>
    /// 修复内嵌贴图：02 青花梅瓶的 fbx 里有一张 2048×1024 的真 albedo，
    /// 但材质引用的绝对路径指向不存在的 .fbm 目录，Unity 无法解析，材质退化为纯色。
    /// 这里把贴图单独导入 Assets/Art/Textures/Props，再建外部材质并重映射到 fbx。
    /// </summary>
    [MenuItem("Tools/Ib/道具模型 · 修复内嵌贴图")]
    public static void WireEmbeddedTextures()
    {
        const string srcTex = "/tmp/embedded_tex/02_blue_white_vase_tex1.png";
        const string texDir = "Assets/Art/Textures/Props";
        const string matDir = "Assets/Art/Materials/Props";
        const string fbxPath = "Assets/Art/Models/Props/02_blue_white_vase.fbx";

        if (!File.Exists(srcTex))
        {
            Debug.LogError("[IbImportProps] 找不到提取出的贴图: " + srcTex);
            return;
        }

        EnsureFolder("Assets/Art/Textures", "Props");
        EnsureFolder("Assets/Art/Materials", "Props");

        string texPath = texDir + "/BlueWhiteVase_Albedo.png";
        File.Copy(srcTex, texPath, true);
        AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null)
        {
            Debug.LogError("[IbImportProps] 贴图导入失败: " + texPath);
            return;
        }

        string matPath = matDir + "/M_Prop_BlueWhiteVase.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(sh) { name = "M_Prop_BlueWhiteVase" };
            AssetDatabase.CreateAsset(mat, matPath);
        }

        mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.85f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        var mi = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (mi == null)
        {
            Debug.LogError("[IbImportProps] 拿不到 ModelImporter: " + fbxPath);
            return;
        }

        var key = new AssetImporter.SourceAssetIdentifier(typeof(Material), "Porcelain_BlueWhite");
        mi.AddRemap(key, mat);
        mi.SaveAndReimport();

        Debug.Log("[IbImportProps] 已把 " + texPath + " 挂到 " + matPath + " 并重映射到 02 梅瓶");
    }

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent))
        {
            string grand = Path.GetDirectoryName(parent).Replace("\\", "/");
            AssetDatabase.CreateFolder(grand, Path.GetFileName(parent));
        }
        if (!AssetDatabase.IsValidFolder(parent + "/" + child))
            AssetDatabase.CreateFolder(parent, child);
    }

    /// <summary>诊断：列出每个 fbx 的全部子资产与材质贴图槽，用来确认内嵌贴图是否被提取。</summary>
    public static void Diagnose()
    {
        const string diagPath = "/tmp/ib_props_diag.txt";
        var sb = new StringBuilder();

        foreach (string path in CollectModels())
        {
            sb.AppendLine("===== " + Path.GetFileName(path) + " =====");

            Object[] subs = AssetDatabase.LoadAllAssetsAtPath(path);
            sb.AppendLine("子资产数: " + subs.Length);
            foreach (Object o in subs)
            {
                if (o == null) continue;
                string extra = "";
                if (o is Texture2D t2) extra = " " + t2.width + "x" + t2.height;
                if (o is Mesh m) extra = " verts=" + m.vertexCount;
                sb.AppendLine("  - [" + o.GetType().Name + "] " + o.name + extra);
            }

            foreach (Object o in subs)
            {
                if (!(o is Material mat)) continue;
                sb.AppendLine("  材质: " + mat.name + "  shader=" + (mat.shader != null ? mat.shader.name : "<null>"));
                Shader sh = mat.shader;
                if (sh == null) continue;
                int n = sh.GetPropertyCount();
                for (int i = 0; i < n; i++)
                {
                    string pname = sh.GetPropertyName(i);
                    if (sh.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture) continue;
                    Texture tex = mat.GetTexture(pname);
                    Color col = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
                    sb.AppendLine("    " + pname + " = " + (tex != null ? tex.name : "<null>"));
                    break;
                }
                if (mat.HasProperty("_BaseColor"))
                {
                    Color c = mat.GetColor("_BaseColor");
                    sb.AppendLine("    _BaseColor = #" + ColorUtility.ToHtmlStringRGB(c));
                }
            }

            sb.AppendLine();
        }

        try
        {
            File.WriteAllText(diagPath, sb.ToString());
            Debug.Log("[IbImportProps] 诊断写入 " + diagPath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[IbImportProps] 诊断写盘失败: " + e.Message + "\n" + sb);
        }
    }

    private static ModelImporter GetImporter(string path)
    {
        var mi = AssetImporter.GetAtPath(path) as ModelImporter;
        if (mi == null) Debug.LogWarning("[IbImportProps] 不是 ModelImporter: " + path);
        return mi;
    }

    private static void ApplyBaseSettings(ModelImporter mi)
    {
        // 单位与坐标：沿用项目现有约定
        mi.useFileScale = true;
        mi.bakeAxisConversion = false;

        // 静态道具：不要相机、灯光、动画、形变
        mi.importCameras = false;
        mi.importLights = false;
        mi.animationType = ModelImporterAnimationType.None;
        mi.importAnimation = false;
        mi.importBlendShapes = false;

        // 材质内嵌为 fbx 子资产，避免 19 个模型在同一目录挤出 Material / Material 1 / Material 2
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
        mi.materialSearch = ModelImporterMaterialSearch.Local;

        // 网格：不压缩、焊接顶点、用 Blender 导出的法线
        mi.meshCompression = ModelImporterMeshCompression.Off;
        mi.weldVertices = true;
        mi.importNormals = ModelImporterNormals.Import;
        mi.isReadable = false;
        // 注意：碰撞体不在这里加。ModelImporter 的 addColliders 未暴露给 C#，
        // 且 Tripo 输出的网格是三角面片，自动生成 Collider 会产生大量无用碰撞体。
        // 需要参与阻挡的道具（画架/长凳/边桌）后续在预制体上手工挂 BoxCollider。
    }

    private static List<string> CollectModels()
    {
        var list = new List<string>();
        if (!AssetDatabase.IsValidFolder(PropsDir))
        {
            Debug.LogError("[IbImportProps] 目录不存在: " + PropsDir);
            return list;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { PropsDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                list.Add(path);
        }

        list.Sort(System.StringComparer.Ordinal);
        return list;
    }

    /// <summary>按 MeshFilter 的 8 个包围盒角点，经 localToWorld 变换后合并，得到真实世界尺寸。</summary>
    private static bool TryMeasure(string path, out Vector3 size)
    {
        size = Vector3.zero;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return false;

        Bounds total = new Bounds();
        bool has = false;

        foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = mf.sharedMesh;
            if (mesh == null) continue;

            Matrix4x4 m = mf.transform.localToWorldMatrix;
            Bounds b = mesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                Vector3 w = m.MultiplyPoint3x4(c);
                if (!has) { total = new Bounds(w, Vector3.zero); has = true; }
                else total.Encapsulate(w);
            }
        }

        if (!has) return false;
        size = total.size;
        return true;
    }

    private static void Report(List<string> models)
    {
        var sb = new StringBuilder();
        sb.AppendLine("道具模型导入报告  " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("模型数: " + models.Count);
        sb.AppendLine();
        sb.AppendLine("编号 | 文件 | 最终尺寸 X×Y×Z (m) | globalScale | 目标 | 顶点 | 三角面 | 材质 | 贴图 | 着色器");

        int totalVerts = 0, totalTris = 0, textured = 0;

        foreach (string path in models)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { sb.AppendLine("!! 加载失败: " + path); continue; }

            TryMeasure(path, out Vector3 size);

            int verts = 0, tris = 0;
            var mats = new HashSet<Material>();
            var shaders = new HashSet<string>();
            bool anyTex = false;

            foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                verts += mf.sharedMesh.vertexCount;
                tris += mf.sharedMesh.triangles.Length / 3;
            }

            foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material mat in r.sharedMaterials)
                {
                    if (mat == null || !mats.Add(mat)) continue;
                    shaders.Add(mat.shader != null ? mat.shader.name : "<null>");
                    foreach (string p in new[] { "_BaseMap", "_MainTex", "_BaseColorMap" })
                        if (mat.HasProperty(p) && mat.GetTexture(p) != null) { anyTex = true; break; }
                }
            }

            totalVerts += verts;
            totalTris += tris;
            if (anyTex) textured++;

            var mi = GetImporter(path);
            float scale = mi != null ? mi.globalScale : 1f;
            string id = Path.GetFileNameWithoutExtension(path).Substring(0, 2);
            string target = Targets.TryGetValue(id, out var t) && t.target > 0f
                ? t.note + " " + t.target.ToString("F2", CultureInfo.InvariantCulture)
                : "不归一";

            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0} | {1} | {2:F3} × {3:F3} × {4:F3} | {5:F3} | {6} | {7} | {8} | {9} | {10} | {11}",
                id, Path.GetFileName(path), size.x, size.y, size.z, scale, target,
                verts, tris, mats.Count, anyTex ? "有" : "无", string.Join(",", shaders)));
        }

        sb.AppendLine();
        sb.AppendLine("合计顶点: " + totalVerts + "   合计三角面: " + totalTris);
        sb.AppendLine("带贴图模型: " + textured + " / " + models.Count);

        try
        {
            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log("[IbImportProps] 报告已写入 " + ReportPath + "\n" + sb);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[IbImportProps] 写报告失败: " + e.Message + "\n" + sb);
        }
    }
}
