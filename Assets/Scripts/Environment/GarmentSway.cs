using Ib.Threats;
using UnityEngine;

namespace Ib.Environment
{
    /// <summary>
    /// 挂衣轻晃（GDD R2-5：不做布料物理，人台经过时衣摆轻轻晃一下，提示"刚有东西经过"）。
    /// 挂在衣服根节点上（原点 = 衣架挂钩），绕挂钩做一个阻尼摆。
    /// 有移动中的人台进入 triggerRadius 就补一点能量；人台走远后慢慢停下。
    /// 调试：组件右键菜单「晃一下」。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GarmentSway : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float triggerRadius = 4.5f;   // 人台路线离挂杆 3–3.7 m（放大后），离得越近晃得越大
        [SerializeField, Range(0f, 30f)] private float maxAngle = 9f;
        [SerializeField, Min(0.1f)] private float frequency = 1.1f;
        [SerializeField, Min(0f)] private float damping = 0.9f;

        private Quaternion rest;
        private float amplitude;
        private float phase;
        private Vector3 axis;

        private void Awake()
        {
            rest = transform.localRotation;
            // 前后晃：绕衣服自身的左右轴（局部 X）
            axis = Vector3.right;
            phase = Random.value * Mathf.PI * 2f;
        }

        [ContextMenu("晃一下")]
        public void Poke()
        {
            amplitude = maxAngle;
        }

        private void Update()
        {
            foreach (MannequinPatrol m in MannequinPatrol.All)
            {
                if (m.CurrentSpeed <= 0.01f)
                {
                    continue;
                }

                Vector3 d = m.transform.position - transform.position;
                d.y = 0f;
                float r = d.magnitude;
                if (r < triggerRadius)
                {
                    float push = (1f - r / triggerRadius) * maxAngle;
                    amplitude = Mathf.Max(amplitude, push);
                }
            }

            if (amplitude < 0.01f)
            {
                if (amplitude > 0f)
                {
                    amplitude = 0f;
                    transform.localRotation = rest;
                }

                return;
            }

            phase += Time.deltaTime * frequency * Mathf.PI * 2f;
            amplitude *= Mathf.Exp(-damping * Time.deltaTime);
            transform.localRotation = rest * Quaternion.AngleAxis(Mathf.Sin(phase) * amplitude, axis);
        }
    }
}
