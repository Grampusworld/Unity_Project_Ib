using UnityEngine;

namespace Ib.Player
{
    /// <summary>
    /// 冲刺体力。FirstPersonController 算出"本帧是否真的在冲刺"后调用 <see cref="SetSprinting"/>，
    /// 由这里管消耗/恢复；FirstPersonController 在决定能不能冲刺时读 <see cref="CanSprint"/>。
    /// 耗尽后要恢复到 resumeThreshold 才能再冲刺，避免贴着 0 反复点冲刺键。
    /// UI（StaminaBar）读 <see cref="Ratio"/> / <see cref="IsDraining"/> 画体力条。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStamina : MonoBehaviour
    {
        [Header("体力（冲刺消耗）")]
        [SerializeField, Min(0.1f)] private float maxStamina = 3.75f;     // 满体力能连续冲刺的秒数（原来的 3/4）
        [SerializeField, Min(0.1f)] private float recoverSeconds = 3f;    // 不冲刺时，从空回满大约要多久
        [SerializeField, Range(0f, 1f)] private float resumeThreshold = 0.2f; // 耗尽后要回到这个比例才能再冲刺
        [SerializeField, Min(0f)] private float recoverDelay = 0.4f;      // 松开冲刺键后，延迟这么久才开始回体力

        /// <summary>当前体力（秒），[0, Max]。</summary>
        public float Current { get; private set; }

        public float Max => maxStamina;

        /// <summary>0~1，给体力条用。</summary>
        public float Ratio => maxStamina > 0f ? Current / maxStamina : 0f;

        /// <summary>本帧是否正在冲刺消耗体力（体力条显隐用）。</summary>
        public bool IsDraining { get; private set; }

        /// <summary>耗尽后会卡在 false，直到体力回到 resumeThreshold 以上。</summary>
        public bool CanSprint => !exhausted || infinite;

        /// <summary>无限体力（调试菜单用）：不消耗、不回退，体力条恒满。</summary>
        public bool Infinite
        {
            get => infinite;
            set
            {
                infinite = value;
                if (value)
                {
                    Current = maxStamina;
                    exhausted = false;
                    IsDraining = false;
                    recoverTimer = 0f;
                }
            }
        }

        private bool exhausted;
        private bool infinite;
        private float recoverTimer;

        private void Awake()
        {
            Current = maxStamina;
        }

        /// <summary>FirstPersonController 每帧调用一次，传入"本帧实际是否在冲刺"。</summary>
        public void SetSprinting(bool sprinting)
        {
            if (infinite)
            {
                Current = maxStamina;
                exhausted = false;
                IsDraining = false;
                recoverTimer = 0f;
                return;
            }

            float dt = Time.deltaTime;
            IsDraining = sprinting && Current > 0f;

            if (sprinting)
            {
                Current = Mathf.Max(0f, Current - dt);
                recoverTimer = recoverDelay;
                if (Current <= 0f)
                {
                    exhausted = true;
                }

                return;
            }

            if (recoverTimer > 0f)
            {
                recoverTimer -= dt;
                return;
            }

            if (Current < maxStamina)
            {
                Current = Mathf.Min(maxStamina, Current + dt * (maxStamina / recoverSeconds));
            }

            if (exhausted && Current >= maxStamina * resumeThreshold)
            {
                exhausted = false;
            }
        }
    }
}
