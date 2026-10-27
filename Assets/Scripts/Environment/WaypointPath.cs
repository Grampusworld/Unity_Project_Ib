using UnityEngine;

namespace Ib.Environment
{
    /// <summary>
    /// 路点路径：子物体按层级顺序就是路点。人台的固定巡回路线用它（§4.1），
    /// 现在只画 Gizmo，以后人偶 AI 直接读 GetPoint()。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaypointPath : MonoBehaviour
    {
        [Tooltip("true = A→B→A 往返；false = 首尾相接的环线。")]
        [SerializeField] private bool pingPong = true;
        [SerializeField] private Color gizmoColor = new Color(0.1f, 0.4f, 0.9f, 1f);

        public bool PingPong => pingPong;

        public int Count => transform.childCount;

        public Vector3 GetPoint(int index)
        {
            return transform.GetChild(index).position;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            int count = transform.childCount;
            if (count == 0)
            {
                return;
            }

            Gizmos.color = gizmoColor;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = GetPoint(i) + Vector3.up * 0.05f;
                Gizmos.DrawWireSphere(p, 0.2f);

                if (i + 1 < count)
                {
                    Gizmos.DrawLine(p, GetPoint(i + 1) + Vector3.up * 0.05f);
                }
            }

            if (!pingPong && count > 2)
            {
                Gizmos.DrawLine(GetPoint(count - 1) + Vector3.up * 0.05f, GetPoint(0) + Vector3.up * 0.05f);
            }
        }
#endif
    }
}
