using UnityEngine;

namespace Ib.Environment
{
    /// <summary>
    /// 灰盒批注：只在 Scene 视图里显示一段文字，运行时什么都不做。
    /// 由 GrayboxBuilder 生成，用来标房间名和"待灰盒试跑"的默认值，重建时一起覆盖。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrayboxNote : MonoBehaviour
    {
        [SerializeField, TextArea(2, 8)] private string text;
        [Tooltip("待定项用橙色显示，已定的房间标签用灰白色。")]
        [SerializeField] private bool pending;

        public string Text
        {
            get => text;
            set => text = value;
        }

        public bool Pending
        {
            get => pending;
            set => pending = value;
        }

#if UNITY_EDITOR
        private static GUIStyle normalStyle;
        private static GUIStyle pendingStyle;

        private void OnDrawGizmos()
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (normalStyle == null)
            {
                normalStyle = new GUIStyle(UnityEditor.EditorStyles.boldLabel);
                normalStyle.normal.textColor = new Color(0.92f, 0.92f, 0.88f);
                pendingStyle = new GUIStyle(UnityEditor.EditorStyles.boldLabel);
                pendingStyle.normal.textColor = new Color(1f, 0.62f, 0.15f);
            }

            Gizmos.color = pending ? new Color(1f, 0.62f, 0.15f) : new Color(0.7f, 0.7f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, 0.15f);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.3f, text, pending ? pendingStyle : normalStyle);
        }
#endif
    }
}
