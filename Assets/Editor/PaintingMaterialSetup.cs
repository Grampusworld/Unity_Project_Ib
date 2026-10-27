using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ib.EditorTools
{
    /// <summary>
    /// 名画 FBX 的材质修复。
    /// Blender 里画布贴图是打包进 .blend 的、画框颜色是程序化节点，FBX 都带不过来，导进 Unity 是一片白。
    /// 这里在 Unity 端按材质名建好 URP 材质（画布贴 Assets/Art/Textures/Paintings/*_Canvas.png），
    /// 再用 ModelImporter 的材质重映射挂到每个 Painting_*.fbx 上。重新导出 FBX 后再点一次菜单即可。
    /// </summary>
    public static class PaintingMaterialSetup
    {
        private const string ModelDir = "Assets/Art/Models/Paintings";
        private const string TextureDir = "Assets/Art/Textures/Paintings";
        private const string MaterialDir = "Assets/Art/Materials/Paintings";

        [MenuItem("Tools/Ib/修复名画材质")]
        public static void Setup()
        {
            if (!AssetDatabase.IsValidFolder(MaterialDir))
            {
                AssetDatabase.CreateFolder("Assets/Art/Materials", "Paintings");
            }

            int fixedCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(path);

                foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (sub is Material source)
                    {
                        Material target = GetMaterial(source.name);
                        if (target != null)
                        {
                            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), target);
                        }
                    }
                }

                importer.SaveAndReimport();
                fixedCount++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[PaintingMaterialSetup] 已修复 " + fixedCount + " 幅名画的材质。");
        }

        private static Material GetMaterial(string name)
        {
            string path = MaterialDir + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            }

            if (name.StartsWith("M_Canvas_"))
            {
                string painting = name.Substring("M_Canvas_".Length);
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureDir + "/" + painting + "_Canvas.png");
                if (tex == null)
                {
                    Debug.LogWarning("[PaintingMaterialSetup] 找不到画布贴图 " + painting + "_Canvas.png");
                }
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", UnityEngine.Color.white);
                Set(mat, 0f, 0.22f);
            }
            else
            {
                switch (name)
                {
                    case "M_Frame_Gilt":        Tint(mat, 0.58f, 0.42f, 0.17f); Set(mat, 0.85f, 0.55f); break;
                    case "M_Frame_GiltPatina":  Tint(mat, 0.42f, 0.33f, 0.16f); Set(mat, 0.75f, 0.45f); break;
                    case "M_Frame_GoldTrim":    Tint(mat, 0.62f, 0.45f, 0.18f); Set(mat, 0.9f, 0.6f); break;
                    case "M_Frame_CarvedWood":  Tint(mat, 0.24f, 0.10f, 0.04f); Set(mat, 0f, 0.4f); break;
                    case "M_Frame_DarkWood":    Tint(mat, 0.08f, 0.045f, 0.025f); Set(mat, 0f, 0.3f); break;
                    case "M_Backing":           Tint(mat, 0.12f, 0.09f, 0.06f); Set(mat, 0f, 0.1f); break;
                    default: return null;
                }
            }

            if (created)
            {
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        private static void Tint(Material mat, float r, float g, float b)
        {
            mat.SetColor("_BaseColor", new UnityEngine.Color(r, g, b));
        }

        private static void Set(Material mat, float metallic, float smoothness)
        {
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
        }
    }
}
