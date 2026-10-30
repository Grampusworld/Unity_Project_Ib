using System;
using UnityEngine;

namespace Ib.Interaction
{
    /// <summary>
    /// 双开门。两扇门板各挂在一个铰链节点下（铰链 = 门洞外侧边）。
    /// 门板在铰链的局部 +X（左扇）/ −X（右扇）方向展开，于是左扇转 −openAngle、右扇转 +openAngle
    /// 都会朝铰链的局部 +Z 打开（序章大门：+Z = 美术馆里面）。
    /// 开门是缓动动画，不锁玩家控制；关门只有瞬间关上（在玩家看不见时调用）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoubleDoor : MonoBehaviour, IInteractable
    {
        private enum State
        {
            Closed,
            Opening,
            Open,
        }

        [SerializeField] private Transform leftHinge;
        [SerializeField] private Transform rightHinge;
        [SerializeField, Range(0f, 175f)] private float openAngle = 90f;
        [SerializeField, Min(0.1f)] private float openDuration = 3f;
        [SerializeField] private bool locked;
        [SerializeField] private string prompt = "推开";

        /// <summary>开始开门（动画第一帧）。</summary>
        public event Action OpeningStarted;
        /// <summary>完全打开。</summary>
        public event Action Opened;

        public bool IsOpen => state == State.Open;
        public bool IsClosed => state == State.Closed;
        public bool IsLocked => locked;

        public string Prompt => prompt;

        public InteractKind Kind => InteractKind.Door;
        public bool CanInteract => !locked && state == State.Closed;

        private State state = State.Closed;
        private float progress;

        public void Interact(PlayerInteractor interactor)
        {
            Open();
        }

        public void Open()
        {
            if (locked || state != State.Closed)
            {
                return;
            }

            state = State.Opening;
            progress = 0f;
            OpeningStarted?.Invoke();
        }

        /// <summary>瞬间关上。只在玩家看不见门的时候调用，没有关门动画。</summary>
        public void CloseInstant()
        {
            state = State.Closed;
            progress = 0f;
            Apply(0f);
        }

        public void SetLocked(bool value)
        {
            locked = value;
        }

        private void Update()
        {
            if (state != State.Opening)
            {
                return;
            }

            progress = Mathf.Min(1f, progress + Time.deltaTime / openDuration);
            Apply(Mathf.SmoothStep(0f, 1f, progress));

            if (progress >= 1f)
            {
                state = State.Open;
                Opened?.Invoke();
            }
        }

        private void Apply(float t)
        {
            float angle = openAngle * t;

            if (leftHinge != null)
            {
                leftHinge.localRotation = Quaternion.Euler(0f, -angle, 0f);
            }

            if (rightHinge != null)
            {
                rightHinge.localRotation = Quaternion.Euler(0f, angle, 0f);
            }
        }
    }
}
