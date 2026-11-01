using Ib.Core;
using Ib.Flow;
using UnityEngine;

namespace Ib.Interaction
{
    /// <summary>
    /// 前台左边的抽屉（GDD §3.5 任务 C / §3.6 第 16 步）：要刷工作卡。
    /// 天使出动以后：没卡按 E 只有咔哒一声；有卡按 E 抽屉拉开，钥匙从屉盒里飞到眼前检视（KeyInspect），
    /// 收下才算拿到主门钥匙。拿到以后不再给任何提示（§3.7）。
    /// Ib Level 挂在 FrontDesk/C_FrontDesk_Drawer（布置后半段时补碰撞体）。open 是拉开的本地位移。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FrontDeskDrawer : MonoBehaviour, IInteractable
    {
        [SerializeField] private Vector3 open = new Vector3(0f, 0f, 0.3f);
        [SerializeField, Min(0.01f)] private float duration = 0.5f;

        private Vector3 closed;
        private float t;
        private bool opening;

        public string Prompt => GameFlow.HasStaffCard ? "Swipe card" : "Locked";

        public InteractKind Kind => InteractKind.Device;

        public bool CanInteract => GameFlow.State == GameState.AngelsActive && !GameFlow.HasKey && !opening;

        private void Awake() => closed = transform.localPosition;

        private void Update()
        {
            float target = GameFlow.HasKey || opening ? 1f : 0f;
            if (Mathf.Approximately(t, target))
            {
                return;
            }

            t = Mathf.MoveTowards(t, target, Time.deltaTime / duration);
            transform.localPosition = closed + open * Mathf.SmoothStep(0f, 1f, t);
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (!GameFlow.HasStaffCard)
            {
                Sfx.PlayAt(Sfx.LockRattle, transform.position, 0.5f, 15f);
                return;
            }

            Sfx.PlayAt(Sfx.KeyClick, transform.position, 0.7f);
            opening = true;
            StartCoroutine(OpenAndInspect());
        }

        private System.Collections.IEnumerator OpenAndInspect()
        {
            GameFlow.SetInputLocked(true);
            yield return new WaitForSeconds(duration);

            // 钥匙躺在屉盒中间（抽屉已经拉开），平放、头朝里
            Vector3 inTray = transform.TransformPoint(new Vector3(0f, -0.03f, -0.12f));
            Quaternion flat = transform.rotation * Quaternion.Euler(0f, 90f, 90f);
            KeyInspect.Begin(inTray, flat, () =>
            {
                opening = false;
                SceneTransitions.TakeKey();
            });
        }
    }
}
