using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ib.Interaction
{
    /// <summary>
    /// 给当前准星命中的物体套一圈白色轮廓。
    /// 根节点自己有网格就只描它（便签的底板包住纸面，描边落在整张纸外面）；
    /// 根节点是空物体（双开门）才把子网格都描上。
    /// </summary>
    sealed class InteractHighlight
    {
        private const string ShellName = "InteractOutline";

        private readonly float width;
        private readonly List<MeshRenderer> meshes = new List<MeshRenderer>();
        private readonly List<SkinnedMeshRenderer> skins = new List<SkinnedMeshRenderer>();
        private readonly List<GameObject> shells = new List<GameObject>();

        private Material material;
        private Component current;
        private bool shaderMissing;

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
            for (int i = 0; i < meshes.Count; i++)
            {
                MeshRenderer source = meshes[i];
                if (!Visible(source))
                {
                    continue;
                }

                MeshFilter filter = source.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null)
                {
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

        private static bool Visible(Renderer renderer)
        {
            return renderer != null
                && renderer.enabled
                && renderer.gameObject.activeInHierarchy
                && renderer.gameObject.name != ShellName;
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
            shell.shadowCastingMode = ShadowCastingMode.Off;
            shell.receiveShadows = false;
            shell.lightProbeUsage = LightProbeUsage.Off;
            shell.reflectionProbeUsage = ReflectionProbeUsage.Off;
            shell.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            shell.renderingLayerMask = source.renderingLayerMask;
            shell.allowOcclusionWhenDynamic = false;

            int sub = Mathf.Max(1, mesh.subMeshCount);
            var mats = new Material[sub];
            for (int i = 0; i < sub; i++)
            {
                mats[i] = material;
            }

            shell.sharedMaterials = mats;
        }

        private void EnsureMaterial()
        {
            if (material != null)
            {
                return;
            }

            Shader shader = Shader.Find("Ib/InteractOutline");
            if (shader == null)
            {
                if (!shaderMissing)
                {
                    shaderMissing = true;
                    Debug.LogError("[InteractHighlight] 找不到着色器 Ib/InteractOutline");
                }

                return;
            }

            material = new Material(shader)
            {
                name = "M_InteractOutline",
                hideFlags = HideFlags.HideAndDontSave,
            };
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Width", width);
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
        }
    }
}
