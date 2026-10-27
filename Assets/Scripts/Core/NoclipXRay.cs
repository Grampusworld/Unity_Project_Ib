using System.Collections.Generic;
using UnityEngine;

namespace Ib.Core
{
    /// <summary>
    /// noclip 外部视图（线框透视）。
    /// 自由飞行飞到美术馆外面（或整个人埋在墙里）时，正常渲染几乎全黑——这是没有天空光和室外光的必然结果。
    /// 这个组件在这种时候叠一层"全部几何体 AABB 的线框"（Garry's Mod / CF 那种穿模视图）：
    /// 无光照、不写深度、永远压在最前面，所以能从任意角度看清楼层、房间、门、道具和边界。
    ///
    /// 实现要点：
    /// - 线框网格第一次需要时才生成，遍历场景里所有 Renderer 的 bounds 拼成一个 MeshTopology.Lines 网格；
    /// - 颜色按材质名分类（门 = 琥珀、展品 = 米白、威胁 = 红、玻璃 = 青、墙体 = 蓝灰），读图能直接认东西；
    /// - 同时把相机背景从近黑抬成墨蓝、farClip 放到 400，避免"外面全是纯黑 + 远处被裁掉"；
    /// - 不改任何场景物件：只用自己生成的一个额外 Renderer，进出都是 enabled 开关，随时可逆。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NoclipXRay : MonoBehaviour
    {
        [Header("判定为「馆内」的包围盒（世界坐标）")]
        [SerializeField] private Bounds museum =
            new Bounds(new Vector3(20f, 5.7f, 13f), new Vector3(40f, 11.4f, 26f));
        [SerializeField] private Bounds blackRoom =
            new Bounds(new Vector3(19.5f, 2.25f, -8f), new Vector3(10f, 4.5f, 16f));

        [Header("外观")]
        [SerializeField] private Color backgroundColor = new Color(0.03f, 0.045f, 0.065f, 1f);
        [SerializeField, Min(0f)] private float buriedProbeRadius = 0.06f;
        [SerializeField, Min(0f)] private float wireAlpha = 0.85f;
        [SerializeField, Min(1f)] private float farClipWhenActive = 400f;

        [Header("地面网格")]
        [SerializeField] private bool drawFloorGrid = true;
        [SerializeField, Min(0.5f)] private float gridStep = 2f;
        [Tooltip("网格所在的 Y（一层地板 / 二层地板）。")]
        [SerializeField] private float[] gridHeights = { 0.02f, 6.42f };

        /// <summary>当前是否处在穿透视图。</summary>
        public bool IsActive { get; private set; }

        private Camera cam;
        private Transform overlay;
        private MeshRenderer overlayRenderer;
        private Mesh wireMesh;
        private Material wireMaterial;

        private CameraClearFlags savedClearFlags;
        private Color savedBackground;
        private float savedFarClip;
        private bool cameraStateSaved;

        private GUIStyle hudStyle;
        private int hudHeight = -1;

        private void Awake()
        {
            cam = GetComponentInChildren<Camera>();
            if (cam == null)
            {
                cam = Camera.main;
            }
        }

        private void Update()
        {
            if (cam == null)
            {
                return;
            }

            Vector3 p = cam.transform.position;
            bool outside = !museum.Contains(p) && !blackRoom.Contains(p);
            bool buried = !outside && IsBuried(p);

            bool want = outside || buried;
            if (want != IsActive)
            {
                SetActive(want);
            }
        }

        public void SetActive(bool active)
        {
            if (IsActive == active)
            {
                return;
            }

            IsActive = active;
            EnsureOverlay();

            if (overlayRenderer != null)
            {
                overlayRenderer.enabled = active;
            }

            if (cam == null)
            {
                return;
            }

            if (active)
            {
                savedClearFlags = cam.clearFlags;
                savedBackground = cam.backgroundColor;
                savedFarClip = cam.farClipPlane;
                cameraStateSaved = true;

                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = backgroundColor;
                cam.farClipPlane = Mathf.Max(savedFarClip, farClipWhenActive);
            }
            else if (cameraStateSaved)
            {
                cam.clearFlags = savedClearFlags;
                cam.backgroundColor = savedBackground;
                cam.farClipPlane = savedFarClip;
                cameraStateSaved = false;
            }
        }

        /// <summary>相机位置是否卡在实体碰撞体里（穿墙时的"埋进去"状态）。</summary>
        private bool IsBuried(Vector3 position)
        {
            Collider[] hits = Physics.OverlapSphere(position, Mathf.Max(0.01f, buriedProbeRadius), ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                // 自己身上的 CharacterController 不算
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        // ---------------------------------------------------------------- 线框网格

        private void EnsureOverlay()
        {
            if (overlayRenderer != null)
            {
                return;
            }

            GameObject go = new GameObject("XRay_Wireframe");
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            overlay = go.transform;

            MeshFilter filter = go.AddComponent<MeshFilter>();
            overlayRenderer = go.AddComponent<MeshRenderer>();
            overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            overlayRenderer.receiveShadows = false;
            overlayRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            overlayRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            overlayRenderer.sharedMaterial = GetWireMaterial();
            overlayRenderer.enabled = false;

            wireMesh = BuildWireMesh();
            filter.sharedMesh = wireMesh;
        }

        private Material GetWireMaterial()
        {
            if (wireMaterial != null)
            {
                return wireMaterial;
            }

            Shader shader = Shader.Find("Ib/XRayLine");
            if (shader == null)
            {
                Debug.LogWarning("[NoclipXRay] 找不到 Ib/XRayLine 着色器，外部视图退化为不可见。", this);
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            wireMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            return wireMaterial;
        }

        private Mesh BuildWireMesh()
        {
            List<Vector3> verts = new List<Vector3>(4096);
            List<Color> colors = new List<Color>(4096);
            List<int> indices = new List<int>(8192);

            Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || r.name == "XRay_Wireframe")
                {
                    continue;
                }

                // 只取真实几何体：线框自己的、玩家身上的都不要
                if (overlay != null && r.transform.IsChildOf(overlay))
                {
                    continue;
                }

                Bounds b = r.bounds;
                Vector3 size = b.size;
                if (size.x < 0.02f && size.y < 0.02f && size.z < 0.02f)
                {
                    continue;
                }

                // 太小的碎件（把手、钥匙孔）按 3 cm 放大，否则线框糊成一团
                Vector3 center = b.center;
                size = Vector3.Max(size, Vector3.one * 0.03f);
                AddBox(verts, colors, indices, center, size, ColorFor(r));
            }

            if (drawFloorGrid)
            {
                for (int i = 0; i < gridHeights.Length; i++)
                {
                    float alpha = i == 0 ? 0.5f : 0.28f;
                    AddGrid(verts, colors, indices, museum, gridHeights[i],
                        new Color(0.05f, 0.17f, 0.22f, wireAlpha * alpha));
                }

                // 楼体轮廓（12 条棱）+ 底部外框加亮，判断方位主要靠这两样
                AddBox(verts, colors, indices, museum.center, museum.size, new Color(0.10f, 0.42f, 0.60f, wireAlpha));
                AddBox(verts, colors, indices, blackRoom.center, blackRoom.size, new Color(0.10f, 0.42f, 0.60f, wireAlpha * 0.9f));

                AddGroundRect(verts, colors, indices, museum, 0.02f, new Color(0.16f, 0.62f, 0.80f, wireAlpha));
            }

            Mesh mesh = new Mesh { name = "XRay_Wireframe", hideFlags = HideFlags.DontSave };
            mesh.indexFormat = verts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2000f);
            return mesh;
        }

        /// <summary>
        /// 按灰盒材质名上色，读图时能直接区分墙体 / 门 / 展品 / 威胁。
        /// 注意：顶点色是**线性**值，数值写大了会被 tonemapping 压成白色，所以这里都取低值保饱和
        /// （例如门的琥珀 = 线性 0.60/0.22/0.03 ≈ 屏幕上的 sRGB 0.80/0.51/0.20）。
        /// </summary>
        private Color ColorFor(Renderer r)
        {
            Material mat = r.sharedMaterial;
            string n = mat != null ? mat.name : string.Empty;

            if (n.Contains("Door"))
            {
                return new Color(0.62f, 0.24f, 0.03f, wireAlpha);
            }

            if (n.Contains("Exhibit"))
            {
                return new Color(0.55f, 0.46f, 0.20f, wireAlpha);
            }

            if (n.Contains("KeyItem"))
            {
                return new Color(0.72f, 0.36f, 0.02f, wireAlpha);
            }

            if (n.Contains("Threat"))
            {
                return new Color(0.75f, 0.05f, 0.04f, wireAlpha);
            }

            if (n.Contains("Glass"))
            {
                return new Color(0.05f, 0.45f, 0.62f, wireAlpha);
            }

            if (n.Contains("BlackRoom"))
            {
                return new Color(0.06f, 0.07f, 0.10f, wireAlpha);
            }

            if (n.Contains("Skylight"))
            {
                return new Color(0.20f, 0.45f, 0.70f, wireAlpha);
            }

            return new Color(0.17f, 0.27f, 0.40f, wireAlpha);
        }

        private void AddBox(List<Vector3> verts, List<Color> colors, List<int> indices,
            Vector3 center, Vector3 size, Color color)
        {
            Bounds b = new Bounds(center, size);
            AddBox(verts, colors, indices, b, color);
        }

        private void AddBox(List<Vector3> verts, List<Color> colors, List<int> indices, Bounds b, Color color)
        {
            Vector3 min = b.min;
            Vector3 max = b.max;

            int start = verts.Count;
            verts.Add(new Vector3(min.x, min.y, min.z));
            verts.Add(new Vector3(max.x, min.y, min.z));
            verts.Add(new Vector3(max.x, min.y, max.z));
            verts.Add(new Vector3(min.x, min.y, max.z));
            verts.Add(new Vector3(min.x, max.y, min.z));
            verts.Add(new Vector3(max.x, max.y, min.z));
            verts.Add(new Vector3(max.x, max.y, max.z));
            verts.Add(new Vector3(min.x, max.y, max.z));

            for (int i = 0; i < 8; i++)
            {
                colors.Add(color);
            }

            int[] edges =
            {
                0, 1, 1, 2, 2, 3, 3, 0,
                4, 5, 5, 6, 6, 7, 7, 4,
                0, 4, 1, 5, 2, 6, 3, 7,
            };

            for (int i = 0; i < edges.Length; i++)
            {
                indices.Add(start + edges[i]);
            }
        }

        private void AddGroundRect(List<Vector3> verts, List<Color> colors, List<int> indices,
            Bounds b, float y, Color color)
        {
            Vector3 min = b.min;
            Vector3 max = b.max;

            int start = verts.Count;
            verts.Add(new Vector3(min.x, y, min.z));
            verts.Add(new Vector3(max.x, y, min.z));
            verts.Add(new Vector3(max.x, y, max.z));
            verts.Add(new Vector3(min.x, y, max.z));

            for (int i = 0; i < 4; i++)
            {
                colors.Add(color);
            }

            indices.Add(start);
            indices.Add(start + 1);
            indices.Add(start + 1);
            indices.Add(start + 2);
            indices.Add(start + 2);
            indices.Add(start + 3);
            indices.Add(start + 3);
            indices.Add(start);
        }

        private void AddGrid(List<Vector3> verts, List<Color> colors, List<int> indices,
            Bounds b, float y, Color color)
        {
            float step = Mathf.Max(0.5f, gridStep);
            Vector3 min = b.min;
            Vector3 max = b.max;

            for (float x = min.x; x <= max.x + 0.01f; x += step)
            {
                int start = verts.Count;
                verts.Add(new Vector3(x, y, min.z));
                verts.Add(new Vector3(x, y, max.z));
                colors.Add(color);
                colors.Add(color);
                indices.Add(start);
                indices.Add(start + 1);
            }

            for (float z = min.z; z <= max.z + 0.01f; z += step)
            {
                int start = verts.Count;
                verts.Add(new Vector3(min.x, y, z));
                verts.Add(new Vector3(max.x, y, z));
                colors.Add(color);
                colors.Add(color);
                indices.Add(start);
                indices.Add(start + 1);
            }
        }

        // ---------------------------------------------------------------- HUD

        private void OnGUI()
        {
            if (!IsActive)
            {
                return;
            }

            if (hudStyle == null || hudHeight != Screen.height)
            {
                hudHeight = Screen.height;
                int size = Mathf.Max(12, Mathf.RoundToInt(Screen.height * 0.014f));
                hudStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = size,
                    alignment = TextAnchor.UpperRight,
                };
                // Linear 空间下 IMGUI 颜色按线性值走，0.10 ≈ 屏幕上的 sRGB 0.35
                hudStyle.normal.textColor = new Color(0.12f, 0.42f, 0.52f, 0.9f);
            }

            Vector3 p = cam.transform.position;
            GUI.Label(new Rect(0f, 8f, Screen.width - 14f, 26f),
                "X-RAY 外部视图   " + p.x.ToString("0.0") + " / " + p.y.ToString("0.0") + " / " + p.z.ToString("0.0"),
                hudStyle);
        }

        private void OnDisable()
        {
            if (IsActive)
            {
                SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (wireMesh != null)
            {
                Destroy(wireMesh);
            }

            if (wireMaterial != null)
            {
                Destroy(wireMaterial);
            }
        }
    }
}
