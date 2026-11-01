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

        /// <summary>哭泣天使被抓住：贴脸 + 真人尖叫录音。</summary>
        public static void Play(WeepingAngel angel, System.Action onFinished)
        {
            Play(angel != null ? angel.transform : null, Sfx.AngelScreamRec, onFinished);
        }

        /// <summary>
        /// 通用版：把 subject 整尊搬到镜头正前方贴脸，配 scream（Sfx 里的名字）尖叫。
        /// 名画人物也走这条，实现「天使式」硬惊吓。
        /// </summary>
        public static void Play(Transform subject, string scream, System.Action onFinished)
        {
            Play(subject, scream, -1f, -1f, -1f, onFinished);
        }

        /// <summary>
        /// 同上，但可以按被吓对象的体型改机位：distance 是镜头到模型的水平距离（小于等于 0 用默认），
        /// faceBelowHead 是脸比包围盒顶再往下多少（小于 0 用默认），fov 是演出期间的视角（小于等于 0 用默认）。
        /// 名画人物比天使矮一大截、脑袋又大，贴到 0.35 m 会被相机近裁剪面切掉，所以要单独给距离。
        /// </summary>
        public static void Play(Transform subject, string scream, float distance, float faceBelowHead, float fov, System.Action onFinished)
        {
            if (Playing)
            {
                return;
            }

            EnsureInstance();
            instance.StartCoroutine(instance.Sequence(subject, scream, distance, faceBelowHead, fov, onFinished));
        }

        /// <summary>
        /// 名画人物专用的「正脸特写」：它是趴着从画框里爬出来的，脸朝下、头发垂在脸前，
        /// 按包围盒或骨骼平移都只能看到画框或头发。这里：
        /// 1) 用眼白/瞳孔子网格的顶点找到眼睛和脸的朝向，把整尊转过来，让正脸对着镜头、头顶朝上；
        /// 2) 演出期间相机只渲染它（临时换到第 31 层），背景纯黑，看起来就是一段固定的 CG；
        /// 3) 近裁剪面推到鼻尖前一点，垂在脸前的头发被切掉，露出整张脸。
        /// 找不到眼睛（换了模型）就退回 Play 的包围盒算法。
        /// </summary>
        public static void PlayFace(Transform subject, Transform head, string scream, System.Action onFinished)
        {
            if (Playing)
            {
                return;
            }

            EnsureInstance();
            instance.StartCoroutine(instance.FaceSequence(subject, head, scream, onFinished));
        }

        private static void EnsureInstance()
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<AngelJumpScare>();
                if (instance == null)
                {
                    instance = new GameObject("--- AngelJumpScare ---").AddComponent<AngelJumpScare>();
                }
            }
        }

        // 正脸特写：只渲染这一层（项目里 31 层没人用）
        private const int CgLayer = 31;
        // 脸（眉到下巴）占画面高度的比例
        private const float FaceFill = 0.8f;
        private const float FaceFov = 45f;

        private IEnumerator FaceSequence(Transform subject, Transform head, string scream, System.Action onFinished)
        {
            SkinnedMeshRenderer skin = subject != null ? subject.GetComponentInChildren<SkinnedMeshRenderer>() : null;
            if (subject == null || head == null || skin == null || !FindFace(skin, head, out Vector3 eyes, out Vector3 faceDir, out Vector3 faceUp, out float faceHeight))
            {
                Playing = false;
                Play(subject, scream, 0.85f, 0.3f, 58f, onFinished);
                yield break;
            }

            Playing = true;
            handoff = false;
            startedAt = Time.unscaledTime;
            ThreatSchedule.Paused = true;
            GameFlow.SetInputLocked(true);

            FirstPersonController player = FindAnyObjectByType<FirstPersonController>();
            Camera cam = player != null ? player.GetComponentInChildren<Camera>() : Camera.main;
            if (cam == null)
            {
                End(onFinished);
                yield break;
            }

            Transform eye = cam.transform;
            Vector3 localPos = eye.localPosition;
            Quaternion localRot = eye.localRotation;
            float fovBefore = cam.fieldOfView;
            float nearBefore = cam.nearClipPlane;
            int maskBefore = cam.cullingMask;
            CameraClearFlags clearBefore = cam.clearFlags;
            Color bgBefore = cam.backgroundColor;

            Vector3 subjectPos = subject.position;
            Quaternion subjectRot = subject.rotation;
            NavMeshAgent agent = subject.GetComponent<NavMeshAgent>();
            bool agentWasOn = agent != null && agent.enabled;
            if (agentWasOn)
            {
                agent.enabled = false;
            }

            Collider[] bodies = subject.GetComponentsInChildren<Collider>();
            bool[] bodyOn = new bool[bodies.Length];
            for (int i = 0; i < bodies.Length; i++)
            {
                bodyOn[i] = bodies[i].enabled;
                bodies[i].enabled = false;
            }

            Renderer[] parts = subject.GetComponentsInChildren<Renderer>(true);
            int[] partLayer = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                partLayer[i] = parts[i].gameObject.layer;
                parts[i].gameObject.layer = CgLayer;
            }

            // 整尊绕原点转：正脸朝镜头、头顶朝上；再平移，让脸的中心（眼睛往下一点）落在镜头正前方
            Vector3 forward = Vector3.ProjectOnPlane(eye.forward, Vector3.up);
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : -subject.forward;
            Quaternion turn = Quaternion.LookRotation(-forward, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(faceDir, faceUp));
            subject.rotation = turn * subject.rotation;
            Vector3 eyesNow = subject.position + turn * (eyes - subjectPos);
            float distance = faceHeight / (FaceFill * 2f * Mathf.Tan(FaceFov * 0.5f * Mathf.Deg2Rad));
            Vector3 faceCenter = eye.position + forward * distance;
            subject.position += faceCenter + Vector3.up * (faceHeight * 0.15f) - eyesNow;

            cam.cullingMask = 1 << CgLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = FaceFov;
            // 鼻尖大约在眼睛前 0.2 个脸高：近裁剪面放在那之前一点，垂在脸前面的头发就被切掉了
            cam.nearClipPlane = Mathf.Max(0.02f, distance - faceHeight * 0.3f);

            PlayerFlashlight flashlight = PlayerFlashlight.Instance;
            Light[] torch = flashlight != null ? flashlight.GetComponentsInChildren<Light>() : new Light[0];
            float[] torchIntensity = new float[torch.Length];
            for (int i = 0; i < torch.Length; i++)
            {
                torchIntensity[i] = torch[i].intensity;
                torch[i].intensity = 0f;
            }

            // 从下巴下方往上打的冷光：眼窝、鼻子下面都是阴影
            Light faceLight = new GameObject("JumpScare_FaceLight").AddComponent<Light>();
            faceLight.type = LightType.Spot;
            faceLight.color = new Color(0.82f, 0.88f, 1f);
            float lightScale = Mathf.Max(1f, distance / (scareDistance + FaceDepth));
            faceLight.intensity = faceLightIntensity * lightScale * lightScale;
            faceLight.range = Mathf.Max(3f, distance * 4f);
            faceLight.spotAngle = 60f;
            faceLight.shadows = LightShadows.None;
            Vector3 lightPos = eye.position - Vector3.up * (faceHeight * 0.6f);
            faceLight.transform.SetPositionAndRotation(lightPos, Quaternion.LookRotation(faceCenter - lightPos));

            Quaternion aim = Quaternion.LookRotation(faceCenter - eye.position, Vector3.up);
            Sfx.PlayUI(string.IsNullOrEmpty(scream) ? Sfx.AngelScreamRec : scream, 1f);

            float scareEnd = Mathf.Max(0.1f, totalSeconds - blackSeconds);
            for (float t = 0f; t < scareEnd; t += Time.unscaledDeltaTime)
            {
                float shake = Mathf.Lerp(1f, 0.4f, t / scareEnd) * shakeDegrees;
                eye.rotation = aim * Quaternion.Euler(
                    Random.Range(-1f, 1f) * shake,
                    Random.Range(-1f, 1f) * shake,
                    Random.Range(-1f, 1f) * shake * 0.6f);
                yield return null;
            }

            Destroy(faceLight.gameObject);
            for (int i = 0; i < torch.Length; i++)
            {
                if (torch[i] != null)
                {
                    torch[i].intensity = torchIntensity[i];
                }
            }

            cam.fieldOfView = fovBefore;
            cam.nearClipPlane = nearBefore;
            cam.cullingMask = maskBefore;
            cam.clearFlags = clearBefore;
            cam.backgroundColor = bgBefore;
            eye.localPosition = localPos;
            eye.localRotation = localRot;
            subject.SetPositionAndRotation(subjectPos, subjectRot);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != null)
                {
                    parts[i].gameObject.layer = partLayer[i];
                }
            }

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

        /// <summary>
        /// 按当前姿势找脸：眼睛 = 眼白和瞳孔子网格顶点的中心；头顶方向 = 颈骨 → Head 骨；
        /// 脸朝向 = 眼睛相对 Head 骨、去掉头顶方向分量后的方向。脸高按两眼外缘的距离估算。
        /// </summary>
        private static bool FindFace(SkinnedMeshRenderer skin, Transform head, out Vector3 eyes, out Vector3 faceDir, out Vector3 faceUp, out float faceHeight)
        {
            eyes = faceDir = faceUp = Vector3.zero;
            faceHeight = 0f;
            Material[] mats = skin.sharedMaterials;
            Mesh baked = new Mesh();
            try
            {
                // BakeMesh 出来的顶点该怎么换到世界（带不带父物体缩放）各版本说法不一，上一版就算错了，
                // 结果「眼睛」落在胸口。这里两种都算，取离 Head 骨最近的那个，不靠猜。
                skin.BakeMesh(baked, false);
                Vector3[] v = baked.vertices;
                Matrix4x4 scaled = skin.transform.localToWorldMatrix;
                Matrix4x4 unscaled = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                var picked = new System.Collections.Generic.HashSet<int>();
                for (int sub = 0; sub < baked.subMeshCount && sub < mats.Length; sub++)
                {
                    string n = mats[sub] != null ? mats[sub].name : "";
                    if (n.Contains("EyeWhite") || n.Contains("Iris"))
                    {
                        foreach (int index in baked.GetTriangles(sub))
                        {
                            picked.Add(index);
                        }
                    }
                }

                if (picked.Count == 0)
                {
                    return false;
                }

                Vector3 sumScaled = Vector3.zero;
                Vector3 sumUnscaled = Vector3.zero;
                foreach (int index in picked)
                {
                    sumScaled += scaled.MultiplyPoint3x4(v[index]);
                    sumUnscaled += unscaled.MultiplyPoint3x4(v[index]);
                }

                Vector3 eyesScaled = sumScaled / picked.Count;
                Vector3 eyesUnscaled = sumUnscaled / picked.Count;
                float dScaled = Vector3.Distance(eyesScaled, head.position);
                float dUnscaled = Vector3.Distance(eyesUnscaled, head.position);
                Matrix4x4 toWorld = dScaled <= dUnscaled ? scaled : unscaled;
                eyes = dScaled <= dUnscaled ? eyesScaled : eyesUnscaled;
                Debug.Log($"[AngelJumpScare] 眼睛离 Head 骨：带缩放 {dScaled:F3} m，不带 {dUnscaled:F3} m，用{(dScaled <= dUnscaled ? "带缩放" : "不带")}。");
                float span = 0f;
                foreach (int index in picked)
                {
                    span = Mathf.Max(span, Vector3.Distance(eyes, toWorld.MultiplyPoint3x4(v[index])));
                }

                Transform neck = head.parent;
                faceUp = neck != null ? (head.position - neck.position).normalized : Vector3.up;
                faceDir = Vector3.ProjectOnPlane(eyes - head.position, faceUp);
                if (faceDir.sqrMagnitude < 1e-6f || span <= 0f)
                {
                    return false;
                }

                faceDir.Normalize();
                // 两眼外缘半宽约 0.045 m，眉到下巴约 0.2 m
                faceHeight = span * 4.5f;
                return true;
            }
            catch (System.Exception e)
            {
                // 网格没开 Read/Write 时打包版会读不到顶点（Girl.fbx 的导入设置已打开）
                Debug.LogWarning("[AngelJumpScare] 找不到名画人物的脸，退回包围盒机位：" + e.Message);
                return false;
            }
            finally
            {
                Object.Destroy(baked);
            }
        }

        private IEnumerator Sequence(Transform angel, string scream, float distance, float faceBelowHead, float fov, System.Action onFinished)
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
            float fovBefore = cam.fieldOfView;
            float nearBefore = cam.nearClipPlane;
            // 脸贴得近时，相机的近裁剪面会把脑袋切掉（看到的是后面的墙），演出期间临时收到 5 cm
            cam.nearClipPlane = Mathf.Min(nearBefore, 0.05f);
            float scareDist = distance > 0.1f ? distance + FaceDepth : scareDistance + FaceDepth;
            // 不能把镜头塞进模型里（体型大的会被自己的背面包住、只看到一片黑），按包围盒半径兜底
            Bounds subjectBounds = SubjectBounds(angel);
            scareDist = Mathf.Max(scareDist, Mathf.Max(subjectBounds.extents.x, subjectBounds.extents.z) * 1.15f + 0.15f);
            float belowHead = faceBelowHead >= 0f ? faceBelowHead : FaceBelowHead;
            float targetFov = fov > 1f ? fov : scareFov;

            // 天使比孩子高得多（约 3.2 m），站在 0.35 m 处头会在视线上方将近 2 m，往前伸的手臂反而挡满画面。
            // 所以整尊往下沉，让脸正好在 Ted 的视线高度、正前方 scareDistance 处（脚埋在地板下面，近景里看不到）。
            Vector3 forward = Vector3.ProjectOnPlane(eye.forward, Vector3.up);
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : angel.forward;
            // HeadPoint 是包围盒顶往下一点，算上了头发；脸（眉眼）还要再往下 FaceBelowHead
            float headHeight = HeadPoint(angel).y - angel.position.y - belowHead;
            Vector3 spot = eye.position + forward * scareDist - Vector3.up * headHeight;

            Vector3 angelPos = angel.position;
            Quaternion angelRot = angel.rotation;
            NavMeshAgent agent = angel.GetComponent<NavMeshAgent>();
            bool agentWasOn = agent != null && agent.enabled;
            if (agentWasOn)
            {
                agent.enabled = false;   // 沉到地板下面不在导航网格上，演出期间先关掉
            }

            angel.SetPositionAndRotation(spot, Quaternion.LookRotation(-forward));

            // 镜头这时在天使的碰撞体里面：演出期间先关掉碰撞体，免得挤开玩家或挡住射线
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
            // 灯放在镜头处、对准正前方：距离一拉开亮度就掉，所以按距离平方补回来，远近两种体型的脸都能看清
            float lightScale = Mathf.Max(1f, scareDist / (scareDistance + FaceDepth));
            faceLight.intensity = faceLightIntensity * lightScale * lightScale;
            faceLight.range = Mathf.Max(3f, scareDist * 4f);
            faceLight.spotAngle = 70f;
            faceLight.shadows = LightShadows.None;
            faceLight.transform.SetPositionAndRotation(eye.position - Vector3.up * 0.08f, Quaternion.LookRotation(forward));

            Quaternion aim = Quaternion.LookRotation(forward, Vector3.up);

            Sfx.PlayUI(string.IsNullOrEmpty(scream) ? Sfx.AngelScreamRec : scream, 1f);

            float scareEnd = Mathf.Max(0.1f, totalSeconds - blackSeconds);
            for (float t = 0f; t < scareEnd; t += Time.unscaledDeltaTime)
            {
                float k = t / scareEnd;
                // 第一帧就到位：没有推近过程，脸直接出现在眼前
                cam.fieldOfView = targetFov;

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

            cam.fieldOfView = fovBefore;
            cam.nearClipPlane = nearBefore;
            eye.localPosition = localPos;
            eye.localRotation = localRot;
            angel.SetPositionAndRotation(angelPos, angelRot);
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

        private static Bounds SubjectBounds(Transform subject)
        {
            bool any = false;
            Bounds b = new Bounds(subject.position, Vector3.zero);
            foreach (Renderer r in subject.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!any)
                {
                    b = r.bounds;
                    any = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            return b;
        }

        private static Vector3 HeadPoint(Transform angel)
        {
            Bounds bounds = new Bounds(angel.position, Vector3.zero);
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
                return angel.position + Vector3.up * 1.7f;
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
