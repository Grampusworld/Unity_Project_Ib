using Ib.Core;
using Ib.Flow;
using Ib.Threats;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ib.Player
{
    /// <summary>
    /// 第一人称手电筒（老大 2026-11-01）：拿到手电后右下角显示 Flashlight_Viewmodel（Ib 美术；2026-11-01 起已去掉手，只剩手电本体）。
    /// 由 <see cref="PlayerFlashlight"/> 在相机下生成，参数按 Docs/Chase_Audio_Viewmodel.md：
    /// - 摆放 (0.17, −0.20, 0.32) / (3, −5, 0)，真实尺寸；刚拿到时从画面下方抬上来；
    /// - 走路轻微摆动（跟脚步频率），跑步摆动 ×2.5 并带一点 roll；转视角时有一点滞后（sway）；
    /// - 贴墙：从相机往灯头打球形射线，前面有墙就把手往回缩、手电往上竖起来，不会插进墙里；
    /// - 手电的聚光灯从灯头 LightMount 发出（灯头被墙挡住时退到墙前面），方向仍对准准星；
    /// - 镜片亮 / 灭材质跟着灯走（闪烁灭的那一拍也换）；
    /// - 一盏只照手电自己的弱补光（Rendering Layer「Light Layer 7」），断电后的黑暗里也看得见手电的轮廓；
    /// - 结局、被抓（jump scare）、Game Over 时收起。暂停时原样定格。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlashlightViewmodel : MonoBehaviour
    {
        /// <summary>只给第一人称手电用的灯光层（TagManager 的 Rendering Layers 第 7 位）。</summary>
        private const int FillLayerBit = 7;

        private static readonly Vector3 RestPosition = new Vector3(0.17f, -0.20f, 0.32f);
        private static readonly Vector3 RestEuler = new Vector3(3f, -5f, 0f);
        private static readonly Vector3 HiddenOffset = new Vector3(0.02f, -0.35f, -0.05f);

        private const float WalkBobX = 0.006f;
        private const float WalkBobY = 0.004f;
        private const float RunBobMultiplier = 2.5f;
        private const float RunRoll = 2f;
        private const float SwayPerDegree = 0.0012f;
        private const float SwayMax = 0.02f;
        private const float SwaySeconds = 0.08f;
        private const float RaiseSeconds = 0.45f;
        private const float RetractBack = 0.18f;   // 往回收 12 cm + 灯头往上翘 55° 省下的 ~6 cm

        private PlayerFlashlight flashlight;
        private Camera cam;
        private FirstPersonController player;
        private Transform lightMount;
        private Renderer lens;
        private Material lensOn;
        private Material lensOff;
        private Light fill;

        private float raise;          // 0 = 收在画面下方，1 = 举在右下角
        private float bobPhase;
        private float bobAmount;      // 0..1 走动幅度（平滑）
        private float runAmount;      // 0..1 跑步幅度（平滑）
        private Vector2 sway;
        private Vector2 swayVel;
        private float retract;        // 0..1 贴墙收回
        private float retractVel;
        private Quaternion lastCamRot;
        private bool hasLastRot;

        /// <summary>手电模型这一帧是否在画面里（举起来了）。</summary>
        public bool Shown => raise > 0.01f && gameObject.activeSelf;

        public void Setup(PlayerFlashlight owner, Camera camera, Material onMat, Material offMat)
        {
            flashlight = owner;
            cam = camera;
            lensOn = onMat;
            lensOff = offMat;
            player = GetComponentInParent<FirstPersonController>();

            lightMount = FindDeep(transform, "LightMount");
            Transform lensT = FindDeep(transform, "FL_Lens");
            lens = lensT != null ? lensT.GetComponent<Renderer>() : null;
            if (lens != null && lensOn == null)
            {
                lensOn = lens.sharedMaterial;
            }

            // 不挡射线、不投影；被补光层照到，同时也接受场景灯光（Default 层）
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
            {
                Destroy(c);
            }

            uint mask = 1u | (1u << FillLayerBit);
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.renderingLayerMask = mask;
            }

            // 只照手电自己的弱补光：射程很短，就算灯光层不生效也只会微微照亮贴脸的东西
            GameObject fillGo = new GameObject("ViewmodelFill");
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.localPosition = new Vector3(-0.12f, 0.16f, -0.05f);
            fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(0.78f, 0.82f, 0.95f);
            fill.intensity = 0.35f;
            fill.range = 0.6f;
            fill.shadows = LightShadows.None;
            fill.renderingLayerMask = 1 << FillLayerBit;
            UniversalAdditionalLightData data = fillGo.AddComponent<UniversalAdditionalLightData>();
            data.renderingLayers = 1u << FillLayerBit;

            transform.localPosition = RestPosition + HiddenOffset;
            transform.localRotation = Quaternion.Euler(RestEuler);
            raise = 0f;
        }

        /// <summary>该不该举着手电。</summary>
        private bool WantShown()
        {
            return flashlight != null && flashlight.HasFlashlight
                && GameFlow.State != GameState.Ending
                && GameFlow.State != GameState.MainMenu
                && !GameOverScreen.IsShowing
                && !AngelJumpScare.Playing
                && (player == null || !player.FreeFly);
        }

        private void LateUpdate()
        {
            if (cam == null)
            {
                return;
            }

            float dt = Time.deltaTime;   // 暂停（timeScale 0）时原样定格
            bool want = WantShown();

            // 被抓 / 结局：立刻收掉（镜头被演出接管，不要露出一只手）；平时拿到手电时抬上来
            if (!want && (AngelJumpScare.Playing || GameFlow.State == GameState.Ending || GameOverScreen.IsShowing))
            {
                raise = 0f;
            }
            else
            {
                raise = Mathf.MoveTowards(raise, want ? 1f : 0f, dt / RaiseSeconds);
            }

            bool visible = raise > 0.001f;
            SetRenderersVisible(visible);
            if (!visible)
            {
                hasLastRot = false;
                return;
            }

            UpdateMotion(dt);
            UpdateRetract(dt);

            float ease = Mathf.SmoothStep(0f, 1f, raise);
            float runK = runAmount;
            float bx = Mathf.Sin(bobPhase) * WalkBobX * bobAmount * Mathf.Lerp(1f, RunBobMultiplier, runK);
            float by = -Mathf.Abs(Mathf.Cos(bobPhase)) * WalkBobY * 2f * bobAmount * Mathf.Lerp(1f, RunBobMultiplier, runK);
            float roll = Mathf.Sin(bobPhase) * RunRoll * runK * bobAmount;

            Vector3 pos = RestPosition
                + new Vector3(bx + sway.x, by + sway.y, 0f)
                + new Vector3(0f, 0.01f, -0.12f) * retract
                + HiddenOffset * (1f - ease);
            Quaternion rot = Quaternion.Euler(RestEuler + new Vector3(-55f * retract + 25f * (1f - ease), -15f * retract, roll));

            transform.localPosition = pos;
            transform.localRotation = rot;

            UpdateLens();
        }

        /// <summary>走路 / 跑步摆动 + 转视角滞后。</summary>
        private void UpdateMotion(float dt)
        {
            if (dt <= 0f)
            {
                return;
            }

            float speed = player != null && player.IsGrounded ? player.HorizontalSpeed : 0f;
            bool running = player != null && player.IsSprinting;
            bobAmount = Mathf.MoveTowards(bobAmount, Mathf.Clamp01(speed / 2.5f), dt * 4f);
            runAmount = Mathf.MoveTowards(runAmount, running ? 1f : 0f, dt * 3f);
            // 一个周期 = 两步；走路约 1.8 步/秒，跑步约 2.8 步/秒
            float stepsPerSecond = Mathf.Lerp(1.8f, 2.8f, runAmount);
            bobPhase += dt * stepsPerSecond * Mathf.PI * Mathf.Max(0.2f, bobAmount);

            // 转视角：上一帧到这一帧相机转了多少度 → 手电往反方向滞后一点，再弹回来
            Quaternion now = cam.transform.rotation;
            Vector2 target = Vector2.zero;
            if (hasLastRot)
            {
                Quaternion delta = Quaternion.Inverse(lastCamRot) * now;
                Vector3 e = delta.eulerAngles;
                float yaw = Mathf.DeltaAngle(0f, e.y);
                float pitch = Mathf.DeltaAngle(0f, e.x);
                target = new Vector2(-yaw, pitch) * SwayPerDegree;
                target = Vector2.ClampMagnitude(target, SwayMax);
            }

            lastCamRot = now;
            hasLastRot = true;
            sway.x = Mathf.SmoothDamp(sway.x, target.x, ref swayVel.x, SwaySeconds, Mathf.Infinity, dt);
            sway.y = Mathf.SmoothDamp(sway.y, target.y, ref swayVel.y, SwaySeconds, Mathf.Infinity, dt);
        }

        /// <summary>贴墙：从相机往手电灯头（静止位置）打球形射线，被挡就往下往回收。</summary>
        private void UpdateRetract(float dt)
        {
            Vector3 origin = cam.transform.position;
            Vector3 headRest = cam.transform.TransformPoint(RestPosition + new Vector3(0f, 0f, 0.16f));
            Vector3 dir = headRest - origin;
            float full = dir.magnitude + 0.04f;
            float target = 0f;
            if (Physics.SphereCast(origin, 0.05f, dir.normalized, out RaycastHit hit, full, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && (player == null || !hit.transform.IsChildOf(player.transform)))
            {
                // 灯头要往回收多少才能离墙还剩 ~5 cm：手往回缩、手电往上竖起来（模型留在画面里）
                target = Mathf.Clamp01(((full - hit.distance) * 0.87f + 0.05f) / RetractBack);
            }

            retract = dt > 0f ? Mathf.SmoothDamp(retract, target, ref retractVel, 0.06f, Mathf.Infinity, dt) : retract;
            // 收的方向要快：墙比手电先到
            retract = Mathf.Max(retract, target * 0.9f);
        }

        private void UpdateLens()
        {
            bool lit = flashlight != null && flashlight.IsLit;
            if (lens != null && lensOn != null && lensOff != null)
            {
                Material want = lit ? lensOn : lensOff;
                if (lens.sharedMaterial != want)
                {
                    lens.sharedMaterial = want;
                }
            }
        }

        /// <summary>
        /// 手电光的发出点：灯头 LightMount；相机到灯头之间有墙时退到墙前面，光不会从墙后面打出来。
        /// 举着手电时返回 true。
        /// </summary>
        public bool TryGetLightOrigin(out Vector3 origin)
        {
            origin = Vector3.zero;
            if (lightMount == null || cam == null || raise < 0.5f)
            {
                return false;
            }

            Vector3 from = cam.transform.position;
            Vector3 to = lightMount.position;
            Vector3 d = to - from;
            float dist = d.magnitude;
            origin = to;
            if (dist > 0.001f
                && Physics.Raycast(from, d / dist, out RaycastHit hit, dist + 0.03f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && (player == null || !hit.transform.IsChildOf(player.transform)))
            {
                origin = from + d / dist * Mathf.Max(0.02f, hit.distance - 0.05f);
            }

            return true;
        }

        private bool renderersVisible = true;

        private void SetRenderersVisible(bool on)
        {
            if (renderersVisible == on)
            {
                return;
            }

            renderersVisible = on;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = on;
            }

            if (fill != null)
            {
                fill.enabled = on;
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }
    }
}
