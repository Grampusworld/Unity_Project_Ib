using System.Collections;
using Ib.Core;
using Ib.Flow;
using Ib.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Ib.Threats
{
    /// <summary>
    /// 被哭泣天使抓住时的 jump scare（插在 CatchPlayer → GameOver 之间）：
    /// 锁输入、停威胁 → 天使瞬移到相机正前方约 0.35 m、脸对镜头 → 同一帧相机直接对准它的脸、FOV 收紧（脸约占七成画面），
    /// 轻微抖动，2D 满音量尖叫（真人录音 AngelScreamRec）→ 定格约 1 s 后切黑，恢复相机后回调（通常 GameOverScreen.Show() + 天使复位）。
    /// 不加任何屏幕闪烁（白闪、红闪都去掉了），就是一张脸突然怼到眼前。
    /// 运行时自己生成，不用摆进场景；全程 unscaled 时间（此时 timeScale 还是 1，中途暂停也不受影响）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AngelJumpScare : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float totalSeconds = 1.35f;
        [SerializeField, Min(0.05f)] private float blackSeconds = 0.25f;
        [SerializeField, Min(0.2f)] private float scareDistance = 0.35f;
        [Tooltip("脸离镜头 0.35 m，56° 时眉眼到张开的嘴占满画面七八成（实测）")]
        [SerializeField, Range(10f, 90f)] private float scareFov = 56f;
        [SerializeField, Min(0f)] private float shakeDegrees = 0.8f;
        [SerializeField, Min(0f)] private float faceLightIntensity = 1.5f;

        // 脸前面离模型原点大约这么远（Attack 模型头部在 forward 方向上占 −0.38 … +0.06 m，脸在 +0.06 那一侧）
        private const float FaceDepth = 0.06f;

        // 眉眼比包围盒顶往下 0.12 的那个点还低这么多（实测）
        private const float FaceBelowHead = 0.2f;

        // 演出期间手电压到原来的这么多
        private const float TorchDim = 0f;

        public static bool Playing { get; private set; }

        private static AngelJumpScare instance;

        private Texture2D black;
        private float startedAt;
        private bool handoff;

        public static void Play(WeepingAngel angel, System.Action onFinished)
        {
            if (Playing)
            {
                return;
            }

            if (instance == null)
            {
                instance = FindAnyObjectByType<AngelJumpScare>();
                if (instance == null)
                {
                    instance = new GameObject("--- AngelJumpScare ---").AddComponent<AngelJumpScare>();
                }
            }

            instance.StartCoroutine(instance.Sequence(angel, onFinished));
        }

        private IEnumerator Sequence(WeepingAngel angel, System.Action onFinished)
        {
            Playing = true;
            handoff = false;
            startedAt = Time.unscaledTime;
            ThreatSchedule.Paused = true;
            GameFlow.SetInputLocked(true);

            FirstPersonController player = FindAnyObjectByType<FirstPersonController>();
            Camera cam = player != null ? player.GetComponentInChildren<Camera>() : Camera.main;
            if (cam == null || angel == null)
            {
                End(onFinished);
                yield break;
            }

            Transform eye = cam.transform;
            Vector3 localPos = eye.localPosition;
            Quaternion localRot = eye.localRotation;
            float fov = cam.fieldOfView;

            // 天使比孩子高得多（约 3.2 m），站在 0.35 m 处头会在视线上方将近 2 m，往前伸的手臂反而挡满画面。
            // 所以整尊往下沉，让脸正好在 Ted 的视线高度、正前方 scareDistance 处（脚埋在地板下面，近景里看不到）。
            Vector3 forward = Vector3.ProjectOnPlane(eye.forward, Vector3.up);
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : angel.transform.forward;
            // HeadPoint 是包围盒顶往下一点，算上了头发；脸（眉眼）还要再往下 FaceBelowHead
            float headHeight = HeadPoint(angel).y - angel.transform.position.y - FaceBelowHead;
            Vector3 spot = eye.position + forward * (scareDistance + FaceDepth) - Vector3.up * headHeight;

            Vector3 angelPos = angel.transform.position;
            Quaternion angelRot = angel.transform.rotation;
            NavMeshAgent agent = angel.GetComponent<NavMeshAgent>();
            bool agentWasOn = agent != null && agent.enabled;
            if (agentWasOn)
            {
                agent.enabled = false;   // 沉到地板下面不在导航网格上，演出期间先关掉
            }

            angel.transform.SetPositionAndRotation(spot, Quaternion.LookRotation(-forward));

            // 镜头这时在天使的碰撞体里面：先关掉，不然穿墙检测（NoclipXRay）会以为人埋进了墙里、切成线框视图
            Collider[] bodies = angel.GetComponentsInChildren<Collider>();
            bool[] bodyOn = new bool[bodies.Length];
            for (int i = 0; i < bodies.Length; i++)
            {
                bodyOn[i] = bodies[i].enabled;
                bodies[i].enabled = false;
            }

            // 光：手电贴脸会把整张脸照爆，而且天使附近手电会闪（时亮时灭），画面不可控。
            // 演出期间把手电压到 0，换成镜头处一盏弱的冷光聚光灯正对着脸，眉骨、鼻子、张开的嘴都有阴影，每次都一样
            PlayerFlashlight flashlight = PlayerFlashlight.Instance;
            Light[] torch = flashlight != null ? flashlight.GetComponentsInChildren<Light>() : new Light[0];
            float[] torchIntensity = new float[torch.Length];
            for (int i = 0; i < torch.Length; i++)
            {
                torchIntensity[i] = torch[i].intensity;
                torch[i].intensity *= TorchDim;
            }

            Light faceLight = new GameObject("JumpScare_FaceLight").AddComponent<Light>();
            faceLight.type = LightType.Spot;
            faceLight.color = new Color(0.82f, 0.88f, 1f);
            faceLight.intensity = faceLightIntensity;
            faceLight.range = 3f;
            faceLight.spotAngle = 70f;
            faceLight.shadows = LightShadows.None;
            faceLight.transform.SetPositionAndRotation(eye.position - Vector3.up * 0.08f, Quaternion.LookRotation(forward));

            Quaternion aim = Quaternion.LookRotation(forward, Vector3.up);

            Sfx.PlayUI(Sfx.AngelScreamRec, 1f);

            float scareEnd = Mathf.Max(0.1f, totalSeconds - blackSeconds);
            for (float t = 0f; t < scareEnd; t += Time.unscaledDeltaTime)
            {
                float k = t / scareEnd;
                // 第一帧就到位：没有推近过程，脸直接出现在眼前
                cam.fieldOfView = scareFov;

                // 轻微抖动，前面略强
                float shake = Mathf.Lerp(1f, 0.4f, k) * shakeDegrees;
                Quaternion jitter = Quaternion.Euler(
                    Random.Range(-1f, 1f) * shake,
                    Random.Range(-1f, 1f) * shake,
                    Random.Range(-1f, 1f) * shake * 0.6f);
                eye.rotation = aim * jitter;
                yield return null;
            }

            // 恢复相机和天使（回原位、重新挂上导航，回调里还要 Warp）；黑屏由 OnGUI 垫着，GameOver 的渐黑从纯黑上接过去
            Destroy(faceLight.gameObject);
            for (int i = 0; i < torch.Length; i++)
            {
                if (torch[i] != null)
                {
                    torch[i].intensity = torchIntensity[i];
                }
            }

            cam.fieldOfView = fov;
            eye.localPosition = localPos;
            eye.localRotation = localRot;
            angel.transform.SetPositionAndRotation(angelPos, angelRot);
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] != null)
                {
                    bodies[i].enabled = bodyOn[i];
                }
            }

            if (agentWasOn)
            {
                agent.enabled = true;
            }

            handoff = true;
            End(onFinished);
        }

        /// <summary>收尾：交还控制权再回调（GameOverScreen.Show() 会马上把输入重新锁住）。</summary>
        private void End(System.Action onFinished)
        {
            ThreatSchedule.Paused = false;
            GameFlow.SetInputLocked(false);
            Playing = false;
            onFinished?.Invoke();
        }

        private static Vector3 HeadPoint(WeepingAngel angel)
        {
            Bounds bounds = new Bounds(angel.transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in angel.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!any)
                {
                    bounds = r.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            if (!any)
            {
                return angel.transform.position + Vector3.up * 1.7f;
            }

            return new Vector3(bounds.center.x, bounds.max.y - 0.12f, bounds.center.z);
        }

        private void OnGUI()
        {
            // 演出结束到 Game Over 盖满之间垫一层纯黑（depth 比 GameOverScreen 高 = 画在它下面）
            if (handoff)
            {
                if (GameOverScreen.IsShowing)
                {
                    EnsureTextures();
                    GUI.depth = -50;
                    GUI.DrawTexture(FullRect(), black, ScaleMode.StretchToFill);
                }

                return;
            }

            if (!Playing)
            {
                return;
            }

            EnsureTextures();
            GUI.depth = -50;
            Rect full = FullRect();
            float t = Time.unscaledTime - startedAt;
            float scareEnd = Mathf.Max(0.1f, totalSeconds - blackSeconds);

            // 切黑
            if (t > scareEnd)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(full, black, ScaleMode.StretchToFill);
            }

            GUI.color = Color.white;
        }

        private static Rect FullRect() => new Rect(0f, 0f, Screen.width, Screen.height);

        private void EnsureTextures()
        {
            if (black == null)
            {
                black = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                black.SetPixel(0, 0, Color.black);
                black.Apply();
            }
        }
    }
}
