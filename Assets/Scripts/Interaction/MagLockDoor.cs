using Ib.Flow;
using UnityEngine;

namespace Ib.Interaction
{
    /// <summary>
    /// 值班室电磁锁门（GDD §3.6 第 10 步）：停电那一刻"咔"地释放，门板沿墙缓缓滑开。
    /// 状态 ≥ PowerOut 时开着，读回更早的检查点时瞬间关上。
    /// Ib Level 挂在 Door_Duty_MagLock 上；slide 是滑开的位移（世界坐标），默认沿 -Z 滑一个门宽。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MagLockDoor : MonoBehaviour
    {
        [SerializeField] private Vector3 slide = new Vector3(0f, 0f, -1.0f);
        [SerializeField, Min(0.01f)] private float duration = 2.5f;

        private Vector3 closed;
        private float t; // 0 = 关，1 = 开
        private bool open;

        public static bool IsOpen(GameState s) => s >= GameState.PowerOut;

        private void Awake() => closed = transform.position;

        private void OnEnable()
        {
            GameFlow.StateChanged += OnStateChanged;
            open = IsOpen(GameFlow.State);
            t = open ? 1f : 0f;
            transform.position = closed + slide * t;
        }

        private void OnDisable() => GameFlow.StateChanged -= OnStateChanged;

        private void OnStateChanged(GameState from, GameState next)
        {
            open = IsOpen(next);
            if (!open)
            {
                t = 0f;
                transform.position = closed;
            }
        }

        private void Update()
        {
            if (!open || t >= 1f)
            {
                return;
            }

            t = Mathf.Min(1f, t + Time.deltaTime / duration);
            transform.position = closed + slide * Mathf.SmoothStep(0f, 1f, t);
        }
    }
}
