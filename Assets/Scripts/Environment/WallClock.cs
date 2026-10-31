using Ib.Flow;
using UnityEngine;

namespace Ib.Environment
{
    /// <summary>
    /// 接待大厅的挂钟：画中馆里是傍晚（开馆中），出画以后是深夜。
    /// 跳时间只在玩家看不见它的时候发生（GDD §3.3：不在视野里才变）——视锥 + 遮挡射线，和 MuseumEntrance 同一思路。
    /// 表盘正面朝本地 +Z；指针绕本地 Z 轴转，12 点朝本地 +Y。Ib Level 由「布置展厅细节」生成。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WallClock : MonoBehaviour
    {
        [SerializeField] private Transform hourHand;
        [SerializeField] private Transform minuteHand;
        [Tooltip("开馆时显示的时间（小时，17.7 = 17:42）")]
        [SerializeField] private float openHours = 17.7f;
        [Tooltip("出画以后显示的时间（小时，2.25 = 02:15）")]
        [SerializeField] private float closedHours = 2.25f;

        private Renderer[] renderers;
        private float shown = -1f;
        private readonly Plane[] frustum = new Plane[6];

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            Show(Target());
        }

        private float Target()
        {
            return GameFlow.State >= GameState.AfterHours ? closedHours : openHours;
        }

        private void LateUpdate()
        {
            float target = Target();
            if (Mathf.Approximately(target, shown) || IsSeen())
            {
                return;
            }

            Show(target);
        }

        private void Show(float hours)
        {
            shown = hours;
            float h = Mathf.Repeat(hours, 12f);
            float m = Mathf.Repeat(hours, 1f);
            // 从表盘正面看，绕 +Z 的正角度是逆时针，所以取负
            if (hourHand != null)
            {
                hourHand.localRotation = Quaternion.Euler(0f, 0f, -h * 30f);
            }

            if (minuteHand != null)
            {
                minuteHand.localRotation = Quaternion.Euler(0f, 0f, -m * 360f);
            }
        }

        private bool IsSeen()
        {
            Camera viewer = Camera.main;
            if (viewer == null || renderers.Length == 0)
            {
                return false;
            }

            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers)
            {
                b.Encapsulate(r.bounds);
            }

            GeometryUtility.CalculateFrustumPlanes(viewer, frustum);
            if (!GeometryUtility.TestPlanesAABB(frustum, b))
            {
                return false;
            }

            Vector3 eye = viewer.transform.position;
            Vector3 to = b.center - eye;
            if (!Physics.Raycast(eye, to.normalized, out RaycastHit hit, to.magnitude - 0.05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            return hit.transform.IsChildOf(transform);
        }
    }
}
