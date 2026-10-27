using System.Collections.Generic;
using UnityEngine;

namespace Ib.Core
{
    /// <summary>
    /// 出生点标记。挂在空物体上即可，场景里可以有多个（以后做多入口 / 检查点）。
    /// 场景里第一个被启用的出生点作为默认出生点。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSpawnPoint : MonoBehaviour
    {
        [SerializeField] private bool isDefault = true;
        [SerializeField] private Color gizmoColor = new Color(0.25f, 0.95f, 0.6f, 1f);

        private static readonly List<PlayerSpawnPoint> Registry = new List<PlayerSpawnPoint>();

        /// <summary>默认出生点；若没有标 isDefault 的，退化为第一个注册的。</summary>
        public static PlayerSpawnPoint Default
        {
            get
            {
                PruneDead();
                for (int i = 0; i < Registry.Count; i++)
                {
                    if (Registry[i].isDefault)
                    {
                        return Registry[i];
                    }
                }

                return Registry.Count > 0 ? Registry[0] : null;
            }
        }

        public Vector3 SpawnPosition => transform.position;

        public Quaternion SpawnRotation => transform.rotation;

        private void OnEnable()
        {
            if (!Registry.Contains(this))
            {
                Registry.Add(this);
            }
        }

        private void OnDisable()
        {
            Registry.Remove(this);
        }

        private static void PruneDead()
        {
            for (int i = Registry.Count - 1; i >= 0; i--)
            {
                if (Registry[i] == null)
                {
                    Registry.RemoveAt(i);
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Vector3 basePos = transform.position;

            // 身体
            Gizmos.DrawWireCube(basePos + Vector3.up * 0.9f, new Vector3(0.7f, 1.8f, 0.4f));
            // 朝向
            Gizmos.DrawLine(basePos + Vector3.up * 1.6f, basePos + Vector3.up * 1.6f + transform.forward * 1.2f);
            // 地面上的定位圈，方便俯视找点
            Vector3 right = transform.right * 0.5f;
            Gizmos.DrawLine(basePos + right, basePos - right);
            Gizmos.DrawLine(basePos + transform.forward * 0.5f, basePos - transform.forward * 0.5f);
        }
#endif
    }
}
