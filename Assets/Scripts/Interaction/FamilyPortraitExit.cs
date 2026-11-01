using System.Collections;
using Ib.Flow;
using Ib.Player;
using Ib.UI;
using UnityEngine;

namespace Ib.Interaction
{
    /// <summary>
    /// 二楼的《全家福》。画中馆和真实馆是同一个画架：
    /// PaintedGallery 时按 E → 黑屏回到黑屋（GDD §3.6 第 6 步）；
    /// 天使出动后、钥匙已经插在正门锁孔里（没开门）时按 E → 走进那块空白，真结局（§6.9）。
    /// 回黑屋：镜头慢慢推向画面（对准画架上的 Canvas），推到一半开始慢慢变黑。
    /// 真结局（老大 2026-11-01）：镜头推到画前、让残缺的全家福撑满画面 → 渐黑 → 画布换成补齐四个人的完整全家福
    /// （Resources/Paintings/FamilyPortrait_Complete）→ 亮屏停一会儿 → 照旧进真结局字幕（SceneTransitions.TrueEnding）。
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

            if (CanEnterBlank && cam != null)
            {
                StartCoroutine(CompletePortrait(cam));
                return;
            }

            StartCoroutine(PushIn(cam != null ? cam.transform : null, CanEnterBlank));
        }

        private const string CompleteTexture = "Paintings/FamilyPortrait_Complete";

        [Header("真结局 · 补齐全家福")]
        [SerializeField, Min(0.1f)] private float revealPushSeconds = 3f;
        [SerializeField, Min(0f)] private float holdIncomplete = 1.2f;
        [SerializeField, Min(0.1f)] private float revealFadeSeconds = 1.6f;
        [SerializeField, Min(0f)] private float holdComplete = 4f;
        [Tooltip("特写时画面上下留多少余量（1 = 画布刚好撑满画面高度）")]
        [SerializeField, Min(1f)] private float revealFraming = 1.12f;

        private IEnumerator CompletePortrait(Camera cam)
        {
            pushing = true;
            GameFlow.SetInputLocked(true);
            GameFlow.SetState(GameState.Ending);   // 推镜头途中馆藏全部停下，不会被抓

            Renderer art = PortraitRenderer();
            Transform eye = cam.transform;
            Vector3 fromPos = eye.position;
            Quaternion fromRot = eye.rotation;
            Vector3 focus = art != null ? art.bounds.center : FocusPoint();
            Vector3 normal = art != null ? art.transform.forward : (fromPos - focus).normalized;
            if (Vector3.Dot(normal, fromPos - focus) < 0f)
            {
                normal = -normal;
            }

            float halfHeight = (art != null ? art.bounds.extents.y : 0.7f) * revealFraming;
            Vector3 toPos = focus + normal * (halfHeight / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
            Quaternion toRot = Quaternion.LookRotation(-normal, Vector3.up);

            // 停电后厅里很暗，手电又会在画中间照出一块亮斑：手电先灭掉，换镜头处一盏广角暖光把整幅画均匀照亮
            // （场景随后会重新加载，手电不用复原）
            if (PlayerFlashlight.Instance != null)
            {
                foreach (Light torch in PlayerFlashlight.Instance.GetComponentsInChildren<Light>())
                {
                    torch.intensity = 0f;
                }
            }

            Light lamp = new GameObject("TrueEnding_PortraitLight").AddComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.color = new Color(1f, 0.9f, 0.76f);
            lamp.intensity = 1.6f;
            lamp.range = 6f;
            lamp.spotAngle = 120f;
            lamp.innerSpotAngle = 90f;
            lamp.shadows = LightShadows.None;

            for (float t = 0f; t < revealPushSeconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / revealPushSeconds);
                eye.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, k), Quaternion.Slerp(fromRot, toRot, k));
                lamp.transform.SetPositionAndRotation(eye.position, Quaternion.LookRotation(focus - eye.position));
                yield return null;
            }

            eye.SetPositionAndRotation(toPos, toRot);
            lamp.transform.SetPositionAndRotation(toPos, toRot);
            yield return new WaitForSeconds(holdIncomplete);

            bool black = false;
            Presentation.FadeToBlack(revealFadeSeconds, () => black = true);
            yield return new WaitUntil(() => black);

            Texture2D complete = Resources.Load<Texture2D>(CompleteTexture);
            if (art != null && complete != null)
            {
                art.material.SetTexture("_BaseMap", complete);
                art.material.SetTexture("_MainTex", complete);
            }
            else
            {
                Debug.LogWarning("[FamilyPortraitExit] 找不到全家福画面或 Resources/" + CompleteTexture + "，跳过补齐。", this);
            }

            yield return new WaitForSeconds(0.4f);
            bool shown = false;
            Presentation.FadeFromBlack(revealFadeSeconds, () => shown = true);
            yield return new WaitUntil(() => shown);
            yield return new WaitForSeconds(holdComplete);

            // 之后照旧：渐黑 → 真结局字幕 → 回主菜单
            pushing = false;
            SceneTransitions.TrueEnding(fadeSeconds);
        }

        /// <summary>画架上贴着《全家福》的那张 Quad（AddEasel 里叫 FamilyPortrait）。</summary>
        private Renderer PortraitRenderer()
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (r.name == "FamilyPortrait")
                {
                    return r;
                }
            }

            return null;
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
