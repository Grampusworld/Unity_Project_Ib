using UnityEngine;

namespace Ib.Environment
{
    /// <summary>
    /// "看不见才变"的通用检测（GDD §3.3 / R4-2）：包围盒先过视锥（GeometryUtility.TestPlanesAABB），
    /// 再对包围盒上的一组采样点做射线遮挡；任何一点在屏幕内、且射线没被别的东西挡住，就算看见。
    /// 不用 Renderer.isVisible（阴影投射、Scene 视图都会把它算成"看见"）。
    /// 挂钟、大门、监控那几份各写各的，新代码（出画画框、布展变化）统一走这里。
    /// </summary>
    public static class Unseen
    {
        private static readonly Plane[] Frustum = new Plane[6];
        private static readonly Vector3[] Samples = new Vector3[27];

        /// <summary>
        /// self 是被检测的物体本身：射线打到它（或它的子物体）也算看见。
        /// bounds 取 self 下所有 Renderer 的包围盒；没有 Renderer 时用 fallback。
        /// </summary>
        public static bool IsVisible(Camera viewer, Transform self, Bounds bounds, int occluders = ~0)
        {
            if (viewer == null)
            {
                return false;
            }

            GeometryUtility.CalculateFrustumPlanes(viewer, Frustum);
            if (!GeometryUtility.TestPlanesAABB(Frustum, bounds))
            {
                return false;
            }

            // 3×3×3 网格：角点、棱中点、面中心、体中心，往里收 5%，免得采样点正好落在墙面里
            int n = 0;
            Vector3 ext = bounds.extents * 0.95f;
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        Samples[n++] = bounds.center + Vector3.Scale(ext, new Vector3(x, y, z));
                    }
                }
            }

            Vector3 eye = viewer.transform.position;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = Samples[i];
                Vector3 vp = viewer.WorldToViewportPoint(p);
                if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f)
                {
                    continue;
                }

                Vector3 dir = p - eye;
                float dist = dir.magnitude;
                if (dist < 0.01f)
                {
                    return true;
                }

                if (!Physics.Raycast(eye, dir / dist, out RaycastHit hit, dist - 0.05f, occluders, QueryTriggerInteraction.Ignore))
                {
                    return true;
                }

                if (self != null && hit.collider.transform.IsChildOf(self))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>self 下所有 Renderer 的包围盒（含未激活的不算）。没有 Renderer 返回 false。</summary>
        public static bool TryGetBounds(Transform self, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer r in self.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled)
                {
                    continue;
                }

                if (!any)
                {
                    bounds = r.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            return any;
        }
    }
}
