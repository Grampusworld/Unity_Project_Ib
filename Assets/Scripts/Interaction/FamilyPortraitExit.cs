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
    /// 天使出动后、身上带着主门钥匙（能离开，但没去开正门）时按 E → 走进那块空白，真结局（§6.9）。
    /// 回黑屋（出画第一拍，GDD §3.2 第五段，老大 2026-11-01）：镜头锁到画上、慢慢拉回到正对整幅画的位置，
    /// 展厅从四周往里褪成黑色（ScreenVeil.Hole），只剩那幅画；画上叠一张同样的画（盖住画前的 Grampus 和两边灯光的差别），
    /// 传回黑屋并把玩家摆在与草稿完全相同的相对位置 → 叠的画淡掉、黑幕往外散开，镜头回到手里时已经站在黑屋画架前。全程不剪。
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

        // 老大 2026-11-02：正门改成单次交互直接进坏结局，真结局不能再要求"钥匙已插进锁孔"，
        // 改为只要身上有钥匙（= 已经能离开，却选择回来）就能走进空白。
        private static bool CanEnterBlank => GameFlow.State == GameState.AngelsActive && GameFlow.HasKey;

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

            if (cam != null && PortraitRenderer() != null && SketchPortrait() != null)
            {
                StartCoroutine(PullBackToBlackRoom(cam));
                return;
            }

            StartCoroutine(PushIn(cam != null ? cam.transform : null, CanEnterBlank));
        }

        [Header("回黑屋 · 拉回 + 四周褪黑")]
        [SerializeField, Min(0.1f)] private float pullSeconds = 4.2f;
        [Tooltip("拉回结束时画面宽度占屏幕宽度的比例")]
        [SerializeField, Range(0.2f, 0.9f)] private float pullFill = 0.5f;
        [SerializeField, Min(0f)] private float vignetteFeather = 140f;
        [SerializeField, Min(0.1f)] private float revealSeconds = 1.8f;

        private const string SketchEasel = "FamilyPortraitSketch_Easel";

        private IEnumerator PullBackToBlackRoom(Camera cam)
        {
            pushing = true;
            GameFlow.SetInputLocked(true);
            SceneTransitions.Hold(true);

            Transform eye = cam.transform;
            FirstPersonController player = eye.GetComponentInParent<FirstPersonController>();
            Vector3 localPos = eye.localPosition;
            Quaternion localRot = eye.localRotation;
            float eyeHeight = player != null ? eye.position.y - player.transform.position.y : 1.3f;

            Renderer art = PortraitRenderer();
            Vector3 fromPos = eye.position;
            Quaternion fromRot = eye.rotation;
            // 两个画架的根节点都落在各自的地板上
            Vector3 toPos = FramingPose(cam, art.transform, eyeHeight, transform.position.y, out Quaternion toRot, out float distance);

            // 1) 锁到画上、慢慢拉回；四周从 30% 处开始往里褪黑；最后 20% 把同一张画淡淡叠上去
            Texture tex = art.sharedMaterial.GetTexture("_BaseMap");
            for (float t = 0f; t < pullSeconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / pullSeconds);
                eye.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, k), Quaternion.Slerp(fromRot, toRot, k));
                float close = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.95f, t / pullSeconds));
                Veil(cam, art.transform, close, Mathf.InverseLerp(0.8f, 1f, t / pullSeconds), tex);
                yield return null;
            }

            eye.SetPositionAndRotation(toPos, toRot);
            Veil(cam, art.transform, 1f, 1f, tex);
            yield return new WaitForSeconds(0.5f);

            // 2) 只剩那幅画：传回黑屋，摆到与草稿相同的相对位置（同一套画架几何，画面在屏幕上纹丝不动）
            SceneTransitions.ReturnToBlackRoomSeamless();
            Renderer sketch = SketchPortrait();
            eye.localPosition = localPos;
            eye.localRotation = localRot;
            if (player != null && sketch != null)
            {
                Vector3 sketchEye = FramingPoseAt(sketch.transform, distance, eyeHeight, sketch.transform.parent.position.y, out Quaternion sketchRot);
                float yaw = sketchRot.eulerAngles.y;
                player.Teleport(sketchEye - Vector3.up * eyeHeight, Quaternion.Euler(0f, yaw, 0f));
                PlayerLook look = player.GetComponent<PlayerLook>();
                if (look != null)
                {
                    look.ResetLook(yaw);
                }
            }

            yield return null;

            // 3) 叠的画淡掉（露出黑屋里的草稿），黑幕往外散开
            Transform sketchQuad = sketch != null ? sketch.transform : art.transform;
            for (float t = 0f; t < revealSeconds; t += Time.deltaTime)
            {
                float k = t / revealSeconds;
                Veil(cam, sketchQuad, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1f, k)), 1f - Mathf.InverseLerp(0f, 0.4f, k), tex);
                yield return null;
            }

            ScreenVeil.Clear();
            SceneTransitions.Hold(false);
            GameFlow.SetInputLocked(false);
            pushing = false;
        }

        /// <summary>正对画、画宽约占屏宽 pullFill、视线水平（pitch 0）的相机位姿；离墙太近时往前收。</summary>
        private Vector3 FramingPose(Camera cam, Transform quad, float eyeHeight, float floorY, out Quaternion rot, out float distance)
        {
            float halfW = Mathf.Abs(quad.lossyScale.x) * 0.5f;
            float halfH = Mathf.Abs(quad.lossyScale.y) * 0.5f;
            float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * cam.aspect;
            distance = halfW / (pullFill * tanH);

            // 画顶要在画面里（视线水平，画比眼睛高）
            float above = quad.position.y + halfH - (floorY + eyeHeight);
            distance = Mathf.Max(distance, above / (tanV * 0.92f));

            Vector3 normal = -quad.forward;
            normal.y = 0f;
            normal.Normalize();
            // 拉回路上有墙就往前收（画前的人、玩家自己、画架不算）
            float wall = distance + 0.3f;
            foreach (RaycastHit hit in Physics.RaycastAll(quad.position, normal, wall, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform h = hit.collider.transform;
                if (h.IsChildOf(transform) || h.GetComponentInParent<FirstPersonController>() != null
                    || (WorldState.Crowd != null && h.IsChildOf(WorldState.Crowd.transform)))
                {
                    continue;
                }

                wall = Mathf.Min(wall, hit.distance);
            }

            distance = Mathf.Max(1.2f, Mathf.Min(distance, wall - 0.3f));

            return FramingPoseAt(quad, distance, eyeHeight, floorY, out rot);
        }

        private static Vector3 FramingPoseAt(Transform quad, float distance, float eyeHeight, float floorY, out Quaternion rot)
        {
            // Quad 可见面是本地 −Z（画架上转了 180°，朝观众）
            Vector3 normal = -quad.forward;
            normal.y = 0f;
            normal.Normalize();
            Vector3 p = quad.position + normal * distance;
            p.y = floorY + eyeHeight;
            rot = Quaternion.LookRotation(-normal, Vector3.up);
            return p;
        }

        /// <summary>close：0 = 不遮，1 = 只剩画；card：叠在画上的同一张画的不透明度。</summary>
        private void Veil(Camera cam, Transform quad, float close, float card, Texture tex)
        {
            if (!ScreenVeil.ScreenRect(cam, quad, out Rect painting))
            {
                painting = new Rect(0.25f, 0.25f, 0.5f, 0.5f);
            }

            // 洞从比屏幕大一圈收到画的轮廓
            Rect full = Rect.MinMaxRect(-0.3f, -0.3f, 1.3f, 1.3f);
            Rect hole = Rect.MinMaxRect(
                Mathf.Lerp(full.xMin, painting.xMin, close), Mathf.Lerp(full.yMin, painting.yMin, close),
                Mathf.Lerp(full.xMax, painting.xMax, close), Mathf.Lerp(full.yMax, painting.yMax, close));
            ScreenVeil.Hole(hole, vignetteFeather * (1f - 0.9f * close), close > 0.001f ? 1f : 0f);
            ScreenVeil.Card(tex, painting, card, new Color(0.78f, 0.74f, 0.7f));
        }

        private Renderer SketchPortrait()
        {
            GameObject easel = WorldState.BlackRoom != null ? FindChild(WorldState.BlackRoom.transform, SketchEasel) : null;
            if (easel == null)
            {
                return null;
            }

            foreach (Renderer r in easel.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "FamilyPortrait")
                {
                    return r;
                }
            }

            return null;
        }

        private static GameObject FindChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t.gameObject;
                }
            }

            return null;
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
