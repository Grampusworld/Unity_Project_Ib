using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Ib.Environment
{
    /// <summary>
    /// 画框里的"窗"（GDD §3.2 第五段，R4-3）：一台门洞摄像机站在另一头（partner）与玩家相对画框对称的位置，
    /// 渲染到 RenderTexture，本 Quad 用 Ib/PaintingPortal 按屏幕坐标取样 —— 透过黑屋的空画框能看见真实馆，
    /// 回头看全家福厅北墙那个画框又能看见黑屋。两头各挂一个，互为 partner。
    /// 约定：Quad 的本地 +Z（forward）= 穿过去的方向，可见面朝 −Z（Unity Quad 默认）。
    /// 只在本 Quad 渲染、离得够近时才开摄像机；Carry() 把玩家从这头搬到那头（穿过画框）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class PaintingPortal : MonoBehaviour
    {
        [SerializeField] private PaintingPortal partner;
        [SerializeField, Min(1f)] private float activeDistance = 25f;
        [Tooltip("门洞贴图相对屏幕的分辨率")]
        [SerializeField, Range(0.25f, 1f)] private float resolutionScale = 0.75f;
        [SerializeField, Min(0f)] private float clipOffset = 0.02f;
        [Tooltip("门洞画面的颜色：看真实馆用冷月光色，回看黑屋用中性色")]
        [SerializeField] private Color tint = new Color(0.62f, 0.74f, 0.95f);
        [SerializeField, Min(0f)] private float exposure = 2.2f;

        private static readonly int PortalTex = Shader.PropertyToID("_PortalTex");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly Matrix4x4 Turn = Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f));

        private Renderer surface;
        private Camera portalCam;
        private RenderTexture texture;

        public PaintingPortal Partner => partner;

        private void Awake()
        {
            surface = GetComponent<Renderer>();
            surface.material.SetColor(TintId, tint);
            surface.material.SetFloat(ExposureId, exposure);
            GameObject go = new GameObject("PortalCamera");
            go.transform.SetParent(transform, false);
            portalCam = go.AddComponent<Camera>();
            portalCam.enabled = false;
            UniversalAdditionalCameraData data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;
        }

        private void OnDisable()
        {
            if (portalCam != null)
            {
                portalCam.enabled = false;
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

        /// <summary>本 Quad 平面（不带缩放）→ 另一头平面并转 180°：这头前面的点落到那头后面。</summary>
        private Matrix4x4 Through()
        {
            Matrix4x4 here = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Matrix4x4 there = Matrix4x4.TRS(partner.transform.position, partner.transform.rotation, Vector3.one);
            return there * Turn * here.inverse;
        }

        private void LateUpdate()
        {
            Camera main = Camera.main;
            bool active = partner != null && main != null && main != portalCam
                && surface.isVisible
                && Vector3.Distance(main.transform.position, transform.position) < activeDistance
                // 只从正面看（背面就是墙）
                && Vector3.Dot(main.transform.position - transform.position, transform.forward) < 0f;
            portalCam.enabled = active;
            if (!active)
            {
                return;
            }

            EnsureTexture(main);

            Matrix4x4 m = Through();
            Matrix4x4 world = m * main.transform.localToWorldMatrix;

            portalCam.CopyFrom(main);
            portalCam.targetTexture = texture;
            portalCam.depth = main.depth - 1f;
            portalCam.transform.SetPositionAndRotation(world.GetColumn(3), world.rotation);
            portalCam.ResetWorldToCameraMatrix();

            // 斜近裁面放在那头的画框平面上：墙和画框本身不挡视线，法线朝那头的房间（−forward）
            Transform far = partner.transform;
            Vector3 normal = -far.forward;
            Vector4 clip = CameraSpacePlane(portalCam.worldToCameraMatrix, far.position + normal * clipOffset, normal);
            portalCam.projectionMatrix = main.projectionMatrix;
            portalCam.projectionMatrix = portalCam.CalculateObliqueMatrix(clip);
        }

        /// <summary>
        /// 穿过画框：按同一套变换把玩家位置、朝向搬到那头。玩家碰不到画框平面（后面是门），
        /// 所以把离平面的距离翻到那头的房间里，并至少留 minAhead 米。返回落点（脚底）和朝向 yaw。
        /// </summary>
        public void Carry(Vector3 feet, float yaw, float minAhead, out Vector3 toFeet, out float toYaw)
        {
            Matrix4x4 here = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Vector3 local = here.inverse.MultiplyPoint3x4(feet);
            // 穿过平面：这头前面是 z<0；Turn 把 z 翻号，那头的房间也在它自己的 z<0 一侧，所以这里取正
            local.z = Mathf.Max(minAhead, Mathf.Abs(local.z));
            Matrix4x4 there = Matrix4x4.TRS(partner.transform.position, partner.transform.rotation, Vector3.one);
            toFeet = (there * Turn).MultiplyPoint3x4(local);

            Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 mapped = Through().MultiplyVector(fwd);
            toYaw = Mathf.Atan2(mapped.x, mapped.z) * Mathf.Rad2Deg;
        }

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

            texture = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR) { name = "PaintingPortal" };
            surface.material.SetTexture(PortalTex, texture);
        }

        private static Vector4 CameraSpacePlane(Matrix4x4 worldToCamera, Vector3 point, Vector3 normal)
        {
            Vector3 p = worldToCamera.MultiplyPoint(point);
            Vector3 n = worldToCamera.MultiplyVector(normal).normalized;
            return new Vector4(n.x, n.y, n.z, -Vector3.Dot(p, n));
        }
    }
}
