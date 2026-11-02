using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ib.Interaction
{
    /// <summary>
    /// 给当前准星命中的物体套一圈白色轮廓。
    /// 根节点自己有网格就只描它（便签的底板包住纸面，描边落在整张纸外面）；
    /// 根节点是空物体（双开门）才把子网格都描上。
    /// 有厚度的网格沿法线挤出、只画背面；单面 Quad（Lily 肖像的四层画布）没有厚度可挤，
    /// 改成沿画布矩形四周生成四条白边、稍微压在画框前面，否则白边会被画框挡住（2026-11-02 修复门口画不高亮）。
    /// </summary>
    sealed class InteractHighlight
    {
        private const string ShellName = "InteractOutline";

        // 平面描边：四条白边压在画布前方这么多米（画框凸出画布约 1 cm，要盖过框沿），
        // 以及比最大平面小这么多的层不描（眼珠那两小片）
        private const float FlatFrontGap = 0.025f;
        private const float FlatMinShare = 0.2f;

        private readonly float width;
        private readonly List<MeshRenderer> meshes = new List<MeshRenderer>();
        private readonly List<SkinnedMeshRenderer> skins = new List<SkinnedMeshRenderer>();
        private readonly List<GameObject> shells = new List<GameObject>();
        private readonly List<Mesh> flatMeshes = new List<Mesh>();

        private Material material;
        private Material flatMaterial;
        private Component current;
        private bool shaderMissing;
        private bool flatShaderMissing;

        public InteractHighlight(float width)
        {
            this.width = width;
        }

        public void Show(Component target)
        {
            if (ReferenceEquals(target, current))
            {
                return;
            }

            ClearShells();
            current = target;
            if (target == null)
            {
                return;
            }

            EnsureMaterial();
            if (material == null)
            {
                return;
            }

            Collect(target);

            // 同一个交互物里常有多层平面（画布 / 眼白 / 虹膜）。只给最大的一层描边，
            // 否则虹膜那两小片的底板会在眼洞里露出一圈白。
            float maxFlatArea = 0f;
            for (int i = 0; i < meshes.Count; i++)
            {
                MeshRenderer source = meshes[i];
                Mesh mesh = MeshOf(source);
                if (mesh != null && IsFlat(mesh))
                {
                    maxFlatArea = Mathf.Max(maxFlatArea, FlatArea(source, mesh));
                }
            }

            for (int i = 0; i < meshes.Count; i++)
            {
                MeshRenderer source = meshes[i];
                if (!Visible(source))
                {
                    continue;
                }

                Mesh mesh = MeshOf(source);
                if (mesh == null)
                {
                    continue;
                }

                if (IsFlat(mesh))
                {
                    if (flatMaterial == null || FlatArea(source, mesh) < maxFlatArea * FlatMinShare)
                    {
                        continue;
                    }

                    MeshRenderer flatShell = CreateShell(source).AddComponent<MeshRenderer>();
                    flatShell.gameObject.AddComponent<MeshFilter>();
                    ApplyFlat(flatShell, source, mesh);
                    continue;
                }

                MeshRenderer shell = CreateShell(source).AddComponent<MeshRenderer>();
                shell.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                Apply(shell, source, mesh);
            }

            for (int i = 0; i < skins.Count; i++)
            {
                SkinnedMeshRenderer source = skins[i];
                if (!Visible(source) || source.sharedMesh == null)
                {
                    continue;
                }

                SkinnedMeshRenderer shell = CreateShell(source).AddComponent<SkinnedMeshRenderer>();
                shell.sharedMesh = source.sharedMesh;
                shell.bones = source.bones;
                shell.rootBone = source.rootBone;
                shell.localBounds = source.localBounds;
                shell.updateWhenOffscreen = true;
                Apply(shell, source, source.sharedMesh);
            }
        }

        public void Dispose()
        {
            ClearShells();
            current = null;
            if (material != null)
            {
                Object.Destroy(material);
                material = null;
            }

            if (flatMaterial != null)
            {
                Object.Destroy(flatMaterial);
                flatMaterial = null;
            }
        }

        private void Collect(Component target)
        {
            GameObject go = target.gameObject;
            go.GetComponents(meshes);
            go.GetComponents(skins);
            if (meshes.Count > 0 || skins.Count > 0)
            {
                return;
            }

            go.GetComponentsInChildren(false, meshes);
            go.GetComponentsInChildren(false, skins);
        }

        private static Mesh MeshOf(MeshRenderer source)
        {
            MeshFilter filter = source.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        private static bool Visible(Renderer renderer)
        {
            return renderer != null
                && renderer.enabled
                && renderer.gameObject.activeInHierarchy
                && renderer.gameObject.name != ShellName;
        }

        /// <summary>网格在某一轴上几乎没有厚度 = 单面片（Quad / 纸面），不能用挤出法描边。</summary>
        private static bool IsFlat(Mesh mesh)
        {
            Vector3 size = mesh.bounds.size;
            float max = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            float min = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            return max > 1e-5f && min <= max * 0.001f;
        }

        private static float FlatArea(Renderer source, Mesh mesh)
        {
            Vector3 lossy = source.transform.lossyScale;
            Vector3 size = mesh.bounds.size;
            size = new Vector3(Mathf.Abs(size.x * lossy.x), Mathf.Abs(size.y * lossy.y), Mathf.Abs(size.z * lossy.z));
            float max = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            float min = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            float mid = size.x + size.y + size.z - max - min;
            return max * mid;
        }

        private GameObject CreateShell(Renderer source)
        {
            var ghost = new GameObject(ShellName);
            ghost.transform.SetParent(source.transform, false);
            ghost.transform.localPosition = Vector3.zero;
            ghost.transform.localRotation = Quaternion.identity;
            ghost.transform.localScale = Vector3.one;
            ghost.layer = source.gameObject.layer;
            shells.Add(ghost);
            return ghost;
        }

        private void Apply(Renderer shell, Renderer source, Mesh mesh)
        {
            ApplyCommon(shell, source);

            int sub = Mathf.Max(1, mesh.subMeshCount);
            var mats = new Material[sub];
            for (int i = 0; i < sub; i++)
            {
                mats[i] = material;
            }

            shell.sharedMaterials = mats;
        }

        /// <summary>
        /// 平面（Quad）没有厚度可挤，就沿画布矩形四周生成四条白边，稍微压在画框前面。
        /// 四条边只占画布外侧 width 米，不会盖住画面本身。
        /// </summary>
        private void ApplyFlat(MeshRenderer shell, MeshRenderer source, Mesh mesh)
        {
            ApplyCommon(shell, source);
            shell.sharedMaterials = new[] { flatMaterial };

            Mesh border = BuildBorder(mesh, source.transform.lossyScale);
            flatMeshes.Add(border);
            shell.GetComponent<MeshFilter>().sharedMesh = border;

            shell.transform.localPosition = Vector3.zero;
            shell.transform.localRotation = Quaternion.identity;
            shell.transform.localScale = Vector3.one;
        }

        /// <summary>在网格的平面上生成"回"字形四条边：上、下、左、右各一条 Quad，宽度 = width（世界米）。</summary>
        private Mesh BuildBorder(Mesh mesh, Vector3 lossy)
        {
            Bounds bounds = mesh.bounds;
            Vector3 extents = bounds.extents;

            // 最薄的那一轴是法线方向，另外两轴张成画面
            int normalAxis = 0;
            if (extents[1] < extents[normalAxis]) normalAxis = 1;
            if (extents[2] < extents[normalAxis]) normalAxis = 2;
            int axis1 = (normalAxis + 1) % 3;
            int axis2 = (normalAxis + 2) % 3;

            Vector3 normal = mesh.normals.Length > 0 ? mesh.normals[0] : Vector3.back;
            if (normal.sqrMagnitude < 1e-8f)
            {
                normal = Vector3.back;
            }

            normal.Normalize();
            Vector3 front = Vector3.zero;
            front[normalAxis] = normal[normalAxis] >= 0f ? 1f : -1f;

            float normalScale = Mathf.Max(1e-4f, Mathf.Abs(lossy[normalAxis]));
            Vector3 origin = bounds.center + front * (FlatFrontGap / normalScale);

            float grow1 = width / Mathf.Max(1e-4f, Mathf.Abs(lossy[axis1]));
            float grow2 = width / Mathf.Max(1e-4f, Mathf.Abs(lossy[axis2]));
            float e1 = extents[axis1];
            float e2 = extents[axis2];

            var vertices = new List<Vector3>(16);
            var triangles = new List<int>(24);
            AddBorderQuad(vertices, triangles, origin, axis1, axis2, -e1 - grow1, e2, e1 + grow1, e2 + grow2);       // 上
            AddBorderQuad(vertices, triangles, origin, axis1, axis2, -e1 - grow1, -e2 - grow2, e1 + grow1, -e2);      // 下
            AddBorderQuad(vertices, triangles, origin, axis1, axis2, -e1 - grow1, -e2, -e1, e2);                      // 左
            AddBorderQuad(vertices, triangles, origin, axis1, axis2, e1, -e2, e1 + grow1, e2);                        // 右

            var border = new Mesh { name = "InteractOutlineBorder" };
            border.SetVertices(vertices);
            border.SetTriangles(triangles, 0);
            border.RecalculateBounds();
            return border;
        }

        private static void AddBorderQuad(List<Vector3> vertices, List<int> triangles,
            Vector3 origin, int axis1, int axis2, float u0, float v0, float u1, float v1)
        {
            int start = vertices.Count;
            vertices.Add(origin + Axis(axis1, u0) + Axis(axis2, v0));
            vertices.Add(origin + Axis(axis1, u0) + Axis(axis2, v1));
            vertices.Add(origin + Axis(axis1, u1) + Axis(axis2, v1));
            vertices.Add(origin + Axis(axis1, u1) + Axis(axis2, v0));
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static Vector3 Axis(int axis, float value)
        {
            return axis == 0 ? new Vector3(value, 0f, 0f)
                : axis == 1 ? new Vector3(0f, value, 0f)
                : new Vector3(0f, 0f, value);
        }

        private static void ApplyCommon(Renderer shell, Renderer source)
        {
            shell.shadowCastingMode = ShadowCastingMode.Off;
            shell.receiveShadows = false;
            shell.lightProbeUsage = LightProbeUsage.Off;
            shell.reflectionProbeUsage = ReflectionProbeUsage.Off;
            shell.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            shell.renderingLayerMask = source.renderingLayerMask;
            shell.allowOcclusionWhenDynamic = false;
        }

        private void EnsureMaterial()
        {
            if (material == null)
            {
                Shader shader = Shader.Find("Ib/InteractOutline");
                if (shader != null)
                {
                    material = new Material(shader)
                    {
                        name = "M_InteractOutline",
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                    material.SetColor("_Color", Color.white);
                    material.SetFloat("_Width", width);
                }
                else if (!shaderMissing)
                {
                    shaderMissing = true;
                    Debug.LogError("[InteractHighlight] 找不到着色器 Ib/InteractOutline");
                }
            }

            if (flatMaterial == null)
            {
                Shader shader = Shader.Find("Ib/InteractOutlineFlat");
                if (shader != null)
                {
                    flatMaterial = new Material(shader)
                    {
                        name = "M_InteractOutlineFlat",
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                    flatMaterial.SetColor("_Color", Color.white);
                }
                else if (!flatShaderMissing)
                {
                    flatShaderMissing = true;
                    Debug.LogError("[InteractHighlight] 找不到着色器 Ib/InteractOutlineFlat");
                }
            }
        }

        private void ClearShells()
        {
            for (int i = 0; i < shells.Count; i++)
            {
                if (shells[i] != null)
                {
                    Object.Destroy(shells[i]);
                }
            }

            shells.Clear();

            for (int i = 0; i < flatMeshes.Count; i++)
            {
                if (flatMeshes[i] != null)
                {
                    Object.Destroy(flatMeshes[i]);
                }
            }

            flatMeshes.Clear();
        }
    }
}
