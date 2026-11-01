using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ib.EditorTools
{
    /// <summary>
    /// 正门钥匙模型（老大 2026-11-01）：一把旧黄铜博物馆钥匙，环形匙柄（内圈 + 十字筋）、领口、圆杆、三齿钥匙头，
    /// 匙柄下面用细绳拴一张纸签，正反两面都印着 "MAIN DOOR / No. 07"（Tools/gen_key_tag.py）。
    /// 全部用代码拼（圆环网格自己生成，其余用内置几何体），存成 Resources/Props/MainDoorKey.prefab，
    /// 抽屉检视（KeyInspect）和正门开锁动画（MainDoorLock）都从这里实例化。
    /// 约定：本地 +Z = 钥匙头方向，原点 = 领口前沿（插进锁孔时停在这里），钥匙所在平面 = YZ，纸签挂在 −Y。
    /// </summary>
    public static class MainDoorKeyBuilder
    {
        private const string PrefabPath = "Assets/Resources/Props/MainDoorKey.prefab";
        private const string ModelDir = "Assets/Art/Models/Key";
        private const string TagTexture = "Assets/Art/Textures/Props/KeyTag_MainDoor.png";

        [MenuItem("Tools/Ib/生成正门钥匙模型")]
        public static GameObject Build()
        {
            EnsureFolder("Assets/Resources/Props");
            EnsureFolder(ModelDir);

            Material brass = Mat("M_Key_Brass", new Color(0.70f, 0.52f, 0.26f), 1f, 0.62f, null);
            Material brassDark = Mat("M_Key_BrassDark", new Color(0.42f, 0.30f, 0.14f), 1f, 0.45f, null);
            Material tag = Mat("M_Key_Tag", Color.white, 0f, 0.12f, TagTex());
            Material paper = Mat("M_Key_TagEdge", new Color(0.80f, 0.73f, 0.58f), 0f, 0.1f, null);
            Material twine = Mat("M_Key_Twine", new Color(0.34f, 0.27f, 0.19f), 0f, 0.15f, null);

            Mesh ring = Torus("KeyBow_Ring", 0.019f, 0.0034f, 40, 12);
            Mesh inner = Torus("KeyBow_Inner", 0.0085f, 0.0018f, 28, 8);

            GameObject root = new GameObject("MainDoorKey");
            Transform key = new GameObject("Key").transform;
            key.SetParent(root.transform, false);

            // 匙柄：外圈 + 内圈 + 十字筋（圆环在 YZ 平面里，轴朝 X）
            Quaternion ringAxis = Quaternion.Euler(0f, 90f, 0f);
            Part(key, "Bow_Ring", ring, new Vector3(0f, 0f, -0.027f), ringAxis, Vector3.one, brass);
            Part(key, "Bow_Inner", inner, new Vector3(0f, 0f, -0.027f), ringAxis, Vector3.one, brass);
            Prim(key, "Bow_Bar_V", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.027f), Quaternion.identity, new Vector3(0.0028f, 0.0175f, 0.0028f), brass);
            Prim(key, "Bow_Bar_H", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.027f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.0028f, 0.0175f, 0.0028f), brass);
            Prim(key, "Bow_Knob", PrimitiveType.Sphere, new Vector3(0f, 0f, -0.027f), Quaternion.identity, Vector3.one * 0.0062f, brassDark);

            // 领口：两道环
            Prim(key, "Collar_A", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.0055f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.012f, 0.0018f, 0.012f), brass);
            Prim(key, "Collar_B", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.001f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.0095f, 0.0014f, 0.0095f), brassDark);
            Prim(key, "Neck", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.0105f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.0058f, 0.0035f, 0.0058f), brass);

            // 杆 + 杆头
            Prim(key, "Shaft", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.031f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.0064f, 0.031f, 0.0064f), brass);
            Prim(key, "Tip", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.062f), Quaternion.identity, Vector3.one * 0.0066f, brass);

            // 钥匙头：三个长短不一的齿（朝 −Y，和纸签同一侧看得见）
            float[] teeth = { 0.016f, 0.010f, 0.014f };
            for (int i = 0; i < teeth.Length; i++)
            {
                float z = 0.045f + i * 0.0055f;
                Prim(key, "Bit_" + i, PrimitiveType.Cube, new Vector3(0f, -teeth[i] * 0.5f - 0.002f, z), Quaternion.identity,
                    new Vector3(0.0026f, teeth[i], 0.0042f), brass);
            }

            // 纸签：挂在匙柄最下面，细绳竖直往下，签子横着（孔在左 = 靠匙柄那头）
            Transform tagRoot = new GameObject("Tag").transform;
            tagRoot.SetParent(root.transform, false);
            const float tagW = 0.052f;
            const float tagH = 0.026f;
            float ringBottom = -0.019f - 0.0034f;
            float holeU = 110f / 1024f;
            float holeZ = -0.027f;
            float tagCenterZ = holeZ - tagW * (holeU - 0.5f);
            float tagCenterY = ringBottom - 0.026f;
            float holeY = tagCenterY;
            Prim(tagRoot, "String", PrimitiveType.Cylinder, new Vector3(0f, (ringBottom + holeY) * 0.5f + 0.002f, holeZ), Quaternion.identity,
                new Vector3(0.0012f, (ringBottom - holeY) * 0.5f + 0.003f, 0.0012f), twine);
            Prim(tagRoot, "String_Knot", PrimitiveType.Sphere, new Vector3(0f, ringBottom + 0.001f, holeZ), Quaternion.identity, Vector3.one * 0.003f, twine);
            Prim(tagRoot, "Card", PrimitiveType.Cube, new Vector3(0f, tagCenterY, tagCenterZ), Quaternion.identity, new Vector3(0.0006f, tagH, tagW), paper);
            // 正反两面：Quad 可见面朝本地 −Z，LookRotation(−n) 让它朝 n；这样两面的字都是正着读的
            Prim(tagRoot, "Face_PosX", PrimitiveType.Quad, new Vector3(0.00035f, tagCenterY, tagCenterZ), Quaternion.LookRotation(Vector3.left, Vector3.up),
                new Vector3(tagW, tagH, 1f), tag);
            Prim(tagRoot, "Face_NegX", PrimitiveType.Quad, new Vector3(-0.00035f, tagCenterY, tagCenterZ), Quaternion.LookRotation(Vector3.right, Vector3.up),
                new Vector3(tagW, tagH, 1f), tag);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[MainDoorKeyBuilder] 已生成 " + PrefabPath);
            return prefab;
        }

        public static GameObject Load()
        {
            GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return p != null ? p : Build();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static Texture2D TagTex()
        {
            TextureImporter ti = AssetImporter.GetAtPath(TagTexture) as TextureImporter;
            if (ti != null && (ti.wrapMode != TextureWrapMode.Clamp || ti.anisoLevel < 4))
            {
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.anisoLevel = 8;
                ti.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(TagTexture);
        }

        private static Material Mat(string name, Color color, float metallic, float smoothness, Texture2D tex)
        {
            string path = ModelDir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }

            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            m.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static GameObject Prim(Transform parent, string name, PrimitiveType type, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            MeshRenderer r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        private static void Part(Transform parent, string name, Mesh mesh, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
        {
            GameObject go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>圆环，轴朝本地 Z（放到 YZ 平面里时再转 90°）。存成网格资源，重复生成时覆盖。</summary>
        private static Mesh Torus(string name, float radius, float tube, int segments, int sides)
        {
            string path = ModelDir + "/" + name + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                Vector3 c = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                for (int j = 0; j <= sides; j++)
                {
                    float b = j * Mathf.PI * 2f / sides;
                    Vector3 n = c * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                    verts.Add(c * radius + n * tube);
                    normals.Add(n);
                    uvs.Add(new Vector2((float)i / segments, (float)j / sides));
                }
            }

            int row = sides + 1;
            for (int i = 0; i < segments; i++)
            {
                for (int j = 0; j < sides; j++)
                {
                    int a = i * row + j;
                    int b = a + row;
                    tris.Add(a); tris.Add(b); tris.Add(a + 1);
                    tris.Add(b); tris.Add(b + 1); tris.Add(a + 1);
                }
            }

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
