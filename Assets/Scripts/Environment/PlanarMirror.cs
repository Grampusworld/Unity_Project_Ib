using Ib.Flow;
using Ib.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ib.Environment
{
    /// <summary>
    /// 平面镜（GDD §9 第 12 项，R4-5）：一台镜面摄像机按主相机关于镜面的对称位置渲染到 RenderTexture，
    /// 镜面材质（Ib/PlanarMirror）按屏幕坐标取样。只在玩家附近、镜子在视野里时才渲染。
    /// 倒影规则（§5.3）：镜子在每个状态都照出房间；Ted 的身体（TedBody 层）只在画中馆（PaintedGallery）照得出来，
    /// 出画以后他没有倒影。挂在镜面 Quad 上（Quad 的可见面朝本地 −Z）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class PlanarMirror : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float activeDistance = 14f;
        [Tooltip("反射贴图相对屏幕的分辨率")]
        [SerializeField, Range(0.25f, 1f)] private float resolutionScale = 0.6f;
        [SerializeField, Min(0f)] private float clipOffset = 0.02f;

        private static readonly int ReflectionTex = Shader.PropertyToID("_ReflectionTex");

        private Renderer surface;
        private Camera mirrorCam;
        private RenderTexture texture;

        private void Awake()
        {
            surface = GetComponent<Renderer>();
            GameObject go = new GameObject("MirrorCamera");
            go.transform.SetParent(transform, false);
            mirrorCam = go.AddComponent<Camera>();
            mirrorCam.enabled = false;
            mirrorCam.depth = -10f;
            UniversalAdditionalCameraData data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            if (mirrorCam != null)
            {
                mirrorCam.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }
        }

        // 对称矩阵会把三角形的绕序反过来，镜面摄像机渲染时要反转剔除
        private void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam == mirrorCam)
            {
                GL.invertCulling = true;
            }
        }

        private void OnEndCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam == mirrorCam)
            {
                GL.invertCulling = false;
            }
        }

        private void LateUpdate()
        {
            Camera main = Camera.main;
            bool active = main != null
                && surface.isVisible
                && Vector3.Distance(main.transform.position, transform.position) < activeDistance;
            mirrorCam.enabled = active;
            if (!active)
            {
                return;
            }

            EnsureTexture(main);

            Vector3 normal = -transform.forward;
            Vector3 point = transform.position;
            Vector4 plane = new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, point));
            Matrix4x4 reflect = Reflection(plane);

            mirrorCam.CopyFrom(main);
            mirrorCam.targetTexture = texture;
            mirrorCam.depth = main.depth - 1f;
            mirrorCam.cullingMask = main.cullingMask | (TedVisible ? TedBody.LayerMask : 0);
            mirrorCam.transform.SetPositionAndRotation(reflect.MultiplyPoint(main.transform.position), main.transform.rotation);
            mirrorCam.worldToCameraMatrix = main.worldToCameraMatrix * reflect;

            // 斜近裁面：镜子后面的东西不进倒影
            Vector4 clip = CameraSpacePlane(mirrorCam.worldToCameraMatrix, point + normal * clipOffset, normal);
            mirrorCam.projectionMatrix = main.CalculateObliqueMatrix(clip);
        }

        /// <summary>画中馆里一切都是画，Ted 照得出来；出画以后没有倒影（§5.3、§6.6）。</summary>
        private static bool TedVisible => GameFlow.State == GameState.PaintedGallery;

        private void EnsureTexture(Camera main)
        {
            int w = Mathf.Max(64, Mathf.RoundToInt(main.pixelWidth * resolutionScale));
            int h = Mathf.Max(64, Mathf.RoundToInt(main.pixelHeight * resolutionScale));
            if (texture != null && texture.width == w && texture.height == h)
            {
                return;
            }

            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }

            texture = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR) { name = "MirrorReflection" };
            surface.material.SetTexture(ReflectionTex, texture);
        }

        private static Matrix4x4 Reflection(Vector4 p)
        {
            Matrix4x4 m = Matrix4x4.identity;
            m.m00 = 1f - 2f * p.x * p.x;
            m.m01 = -2f * p.x * p.y;
            m.m02 = -2f * p.x * p.z;
            m.m03 = -2f * p.w * p.x;
            m.m10 = -2f * p.y * p.x;
            m.m11 = 1f - 2f * p.y * p.y;
            m.m12 = -2f * p.y * p.z;
            m.m13 = -2f * p.w * p.y;
            m.m20 = -2f * p.z * p.x;
            m.m21 = -2f * p.z * p.y;
            m.m22 = 1f - 2f * p.z * p.z;
            m.m23 = -2f * p.w * p.z;
            return m;
        }

        private static Vector4 CameraSpacePlane(Matrix4x4 worldToCamera, Vector3 point, Vector3 normal)
        {
            Vector3 p = worldToCamera.MultiplyPoint(point);
            Vector3 n = worldToCamera.MultiplyVector(normal).normalized;
            return new Vector4(n.x, n.y, n.z, -Vector3.Dot(p, n));
        }
    }
}
