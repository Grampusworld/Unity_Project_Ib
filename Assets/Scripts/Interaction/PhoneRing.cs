using Ib.Core;
using Ib.Flow;
using UnityEngine;

namespace Ib.Interaction
{
    /// <summary>
    /// 前台电话（GDD §3.6 第 8 步）：出画（AfterHours）几秒后开始响，同时提示「前往接待大厅接电话」。
    /// 按 E 接起：铃停，听筒里只有线路底噪，几秒后挂断。接过以后楼梯中段才会断电（StateTrigger）。
    /// Ib Level 挂在 FrontDesk/Phone（需要 Collider，布置后半段时补）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PhoneRing : MonoBehaviour, IInteractable
    {
        [SerializeField, Min(0f)] private float ringDelay = 5f;
        [Tooltip("铃声能传多远（米）。全馆都要听得见。")]
        [SerializeField, Min(1f)] private float ringDistance = 70f;

        private AudioSource bell;
        private AudioSource line;
        private float ringAt = -1f;
        private bool onCall;

        public string Prompt => "Answer";

        public InteractKind Kind => InteractKind.Device;

        public bool CanInteract => IsRinging && !SceneTransitions.Busy;

        private bool IsRinging => bell != null && bell.isPlaying;

        private void Awake()
        {
            bell = Sfx.Attach(gameObject, Sfx.Load(Sfx.PhoneRing), true, 1f, ringDistance);
            line = Sfx.Attach(gameObject, Sfx.Load(Sfx.LineHiss), false, 0.45f, 8f);
            // 铃声要穿过楼板传到二楼：只做一半 3D，远处也听得见方向
            bell.spatialBlend = 0.6f;
        }

        private void OnEnable() => GameFlow.StateChanged += OnStateChanged;

        private void OnDisable() => GameFlow.StateChanged -= OnStateChanged;

        private void OnStateChanged(GameState from, GameState to)
        {
            if (to == GameState.AfterHours && !GameFlow.PhoneAnswered)
            {
                ringAt = Time.time + ringDelay;
            }
            else if (to != GameState.AfterHours)
            {
                ringAt = -1f;
                bell.Stop();
            }
        }

        private void Update()
        {
            // 暂停时声音由 GameFlow 整体静音（AudioListener.pause），这里也别把"底噪放完"误判成通话结束
            if (GameFlow.IsPaused)
            {
                return;
            }

            // 底噪放完（最后一下是挂断声）才把控制还给玩家
            if (onCall && !line.isPlaying)
            {
                onCall = false;
                SceneTransitions.HangUp();
            }

            if (ringAt < 0f || Time.time < ringAt)
            {
                return;
            }

            ringAt = -1f;
            if (!GameFlow.PhoneAnswered && GameFlow.State == GameState.AfterHours)
            {
                bell.Play();
                SceneTransitions.PhoneRinging();
            }
        }

        public void Interact(PlayerInteractor interactor)
        {
            bell.Stop();
            line.Play();
            onCall = true;
            SceneTransitions.AnswerPhone();
        }
    }
}
