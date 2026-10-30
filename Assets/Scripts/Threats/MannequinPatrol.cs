using System.Collections.Generic;
using Ib.Core;
using Ib.Environment;
using Ib.Flow;
using UnityEngine;

namespace Ib.Threats
{
    /// <summary>
    /// 会动的人台（GDD §5 服装厅）：沿自己的 WaypointPath 匀速往返，不转向玩家、不加速。
    /// 默认不动 —— 按 GDD 要等"拿起手电"那一刻才出动，条件见 ThreatSchedule.CollectionOut。
    /// 死亡重生（GameOverScreen.Continued）时回到原位，再按当前状态重设开关。
    /// 现在只负责移动和朝向；"碰到就被收走"的判定以后在这里加。
    /// 调试：Inspector 里勾 startActive，或组件右键菜单「切换移动」。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MannequinPatrol : MonoBehaviour
    {
        private static readonly List<MannequinPatrol> all = new List<MannequinPatrol>();

        /// <summary>场景里所有人台（挂衣用它来判断"刚有东西经过"）。</summary>
        public static IReadOnlyList<MannequinPatrol> All => all;

        [SerializeField] private WaypointPath path;
        [SerializeField, Min(0.05f)] private float speed = 0.7f;
        [SerializeField, Min(0f)] private float turnSpeed = 180f;
        [SerializeField] private bool startActive;

        public bool IsActive { get; private set; }

        /// <summary>这一帧的水平速度（m/s），挂衣按它决定晃多大。</summary>
        public float CurrentSpeed { get; private set; }

        private int target = 1;
        private int step = 1;
        private Vector3 homePosition;
        private Quaternion homeRotation;

        private void Awake()
        {
            homePosition = transform.position;
            homeRotation = transform.rotation;
        }

        public static void SetAllActive(bool active)
        {
            foreach (MannequinPatrol m in all)
            {
                m.IsActive = active;
            }
        }

        private void OnEnable()
        {
            all.Add(this);
            GameFlow.StateChanged += OnStateChanged;
            GameFlow.FlashlightObtained += Refresh;
            GameOverScreen.Continued += ResetForRespawn;
            Refresh();
        }

        private void OnDisable()
        {
            all.Remove(this);
            GameFlow.StateChanged -= OnStateChanged;
            GameFlow.FlashlightObtained -= Refresh;
            GameOverScreen.Continued -= ResetForRespawn;
        }

        private void OnStateChanged(GameState from, GameState to) => Refresh();

        private void Refresh() => IsActive = startActive || ThreatSchedule.CollectionOut;

        private void ResetForRespawn()
        {
            transform.SetPositionAndRotation(homePosition, homeRotation);
            target = 1;
            step = 1;
            CurrentSpeed = 0f;
            Refresh();
        }

        [ContextMenu("切换移动")]
        private void ToggleActive()
        {
            IsActive = !IsActive;
        }

        private void Update()
        {
            CurrentSpeed = 0f;
            if (!IsActive || path == null || path.Count < 2)
            {
                return;
            }

            Vector3 goal = path.GetPoint(target);
            goal.y = transform.position.y;
            Vector3 to = goal - transform.position;
            float dist = to.magnitude;
            if (dist < 0.05f)
            {
                AdvanceTarget();
                return;
            }

            Vector3 dir = to / dist;
            float move = Mathf.Min(dist, speed * Time.deltaTime);
            transform.position += dir * move;
            CurrentSpeed = move / Mathf.Max(Time.deltaTime, 0.0001f);

            Quaternion face = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, face, turnSpeed * Time.deltaTime);
        }

        private void AdvanceTarget()
        {
            int next = target + step;
            if (next < 0 || next >= path.Count)
            {
                if (path.PingPong)
                {
                    step = -step;
                    next = target + step;
                }
                else
                {
                    next = 0;
                }
            }

            target = next;
        }
    }
}
