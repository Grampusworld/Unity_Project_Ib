using System.Collections;
using Ib.Flow;
using UnityEngine;

namespace Ib.Interaction
{
    /// <summary>
    /// 二楼的《全家福》。画中馆和真实馆是同一个画架：
    /// PaintedGallery 时按 E → 黑屏回到黑屋（GDD §3.6 第 6 步）；
    /// 天使出动后、钥匙已经插在正门锁孔里（没开门）时按 E → 走进那块空白，真结局（§6.9）。
    /// 两种都先把镜头慢慢推向画面（对准画架上的 Canvas），推到一半开始慢慢变黑。
    /// Ib Level 挂在 FamilyPortrait_Easel（需要 Collider）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FamilyPortraitExit : MonoBehaviour, IInteractable
    {
        [SerializeField, Min(0.1f)] private float pushSeconds = 2.6f;
        [Tooltip("镜头最多往前推这么远（米）")]
        [SerializeField, Min(0f)] private float maxPush = 1.2f;
        [Tooltip("推完后离画面至少这么远（米）")]
        [SerializeField, Min(0.2f)] private float standOff = 0.9f;
        [Tooltip("推到这个进度开始渐黑")]
        [SerializeField, Range(0f, 1f)] private float fadeStartsAt = 0.5f;
        [SerializeField, Min(0.1f)] private float fadeSeconds = 1.8f;

        private bool pushing;

        public string Prompt => "Look";

        public InteractKind Kind => InteractKind.Painting;

        public bool CanInteract => (GameFlow.State == GameState.PaintedGallery || CanEnterBlank) && !SceneTransitions.Busy && !pushing;

        private static bool CanEnterBlank => GameFlow.State == GameState.AngelsActive && GameFlow.KeyInserted;

        public void Interact(PlayerInteractor interactor)
        {
            Camera cam = interactor != null ? interactor.GetComponentInChildren<Camera>() : null;
            if (cam == null)
            {
                cam = Camera.main;
            }

            StartCoroutine(PushIn(cam != null ? cam.transform : null, CanEnterBlank));
        }

        private IEnumerator PushIn(Transform cam, bool blank)
        {
            pushing = true;
            GameFlow.SetInputLocked(true);
            if (blank)
            {
                // 先切到 Ending：推镜头途中馆藏全部停下，不会被抓
                GameFlow.SetState(GameState.Ending);
            }

            if (cam == null)
            {
                Leave(blank, null);
                pushing = false;
                yield break;
            }

            Vector3 localPos = cam.localPosition;
            Quaternion localRot = cam.localRotation;
            Vector3 fromPos = cam.position;
            Quaternion fromRot = cam.rotation;
            Vector3 focus = FocusPoint();
            Vector3 dir = (focus - fromPos).normalized;
            float travel = Mathf.Clamp(Vector3.Distance(focus, fromPos) - standOff, 0f, maxPush);
            Vector3 toPos = fromPos + dir * travel;
            Quaternion toRot = Quaternion.LookRotation(dir, Vector3.up);

            bool fading = false;
            bool atBlack = false;
            for (float t = 0f; !atBlack && !(blank && t > pushSeconds); t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / pushSeconds));
                cam.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, k), Quaternion.Slerp(fromRot, toRot, k));

                if (!fading && t >= pushSeconds * fadeStartsAt)
                {
                    fading = true;
                    Leave(blank, () =>
                    {
                        // 全黑了、传送前：相机放回玩家身上的原位（朝向由出生点 ResetLook 再校正）
                        atBlack = true;
                        cam.localPosition = localPos;
                        cam.localRotation = localRot;
                    });
                }

                yield return null;
            }

            // 真结局停在推完的位置，场景随后重新加载
            pushing = false;
        }

        private void Leave(bool blank, System.Action atBlack)
        {
            if (blank)
            {
                SceneTransitions.TrueEnding(fadeSeconds);
            }
            else
            {
                SceneTransitions.ReturnToBlackRoom(fadeSeconds, atBlack);
            }
        }

        /// <summary>画架上的画布中心；找不到就用碰撞体中心。</summary>
        private Vector3 FocusPoint()
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (r.name == "Canvas")
                {
                    return r.bounds.center;
                }
            }

            Collider c = GetComponent<Collider>();
            return c != null ? c.bounds.center : transform.position + Vector3.up * 1.4f;
        }
    }
}
