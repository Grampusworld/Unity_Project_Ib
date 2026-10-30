using Ib.Flow;
using Ib.Player;
using UnityEngine;

namespace Ib.Interaction
{
    /// <summary>
    /// 玩家走进触发区时推进状态。Ib Level 挂在带 isTrigger Collider 的物体上，选 step：
    /// EmptyFrame = 黑屋空画框（第 7 步，放在 EmptyFrame_SecondVisit 下，画框隐藏时它也不存在）；
    /// PowerOut = 楼梯中段（第 10 步，要先接过电话）。只在对应的前一个状态生效，重复进入无效。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class StateTrigger : MonoBehaviour
    {
        public enum Step { EmptyFrame, PowerOut }

        [SerializeField] private Step step;

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<FirstPersonController>() == null)
            {
                return;
            }

            if (step == Step.EmptyFrame && GameFlow.State == GameState.BlackRoomReturn)
            {
                SceneTransitions.EnterAfterHours();
            }
            // 下楼接电话也会经过楼梯平台：接过电话、重新上楼时才断电（第 8 → 10 步）
            else if (step == Step.PowerOut && GameFlow.State == GameState.AfterHours && GameFlow.PhoneAnswered)
            {
                SceneTransitions.CutPower();
            }
        }
    }
}
