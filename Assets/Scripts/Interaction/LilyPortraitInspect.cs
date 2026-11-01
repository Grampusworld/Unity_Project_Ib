using System.Collections;
using Ib.Flow;
using Ib.Threats;
using Ib.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ib.Interaction
{
    /// <summary>
    /// 二楼值班室门口的 Lily 肖像（Sketch_LilyLooksAtDoor_Portrait，眼珠由 PortraitEyes 跟着玩家）可以按 E 细看（老大 2026-11-01）：
    /// 锁控制 → 镜头缓缓推到画前给特写 → 屏幕下方 1/4 处两段独白（先是「好像一直在盯着我」，再是画里的小字 = Lily 的台词，GDD §7.2）
    /// → 镜头拉回、放开控制。可以反复看。演出期间馆藏照常走动，只是不抓人（ThreatSchedule.NoCatch），不会在特写里被抓。
    /// 场景里不用挂：场景加载后按名字找到这组画，自己挂上并按画布的大小补一个碰撞盒（画的几层 Quad 没有碰撞体）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LilyPortraitInspect : MonoBehaviour, IInteractable
    {
        private const string PortraitName = "Sketch_LilyLooksAtDoor_Portrait";

        // 第二句引号里是 Lily 的台词，英文定稿见 GDD §7.2
        private const string Staring = "It feels like this painting has been watching me.";
        private const string SmallWriting = "There's tiny writing in the paint:\n\"The door's open. You'll see it when you come back.\"";

        [SerializeField, Min(0.1f)] private float pushSeconds = 2.4f;
        [SerializeField, Min(0.1f)] private float pullSeconds = 1.4f;
        [Tooltip("特写时画面上下留多少余量（1 = 画布刚好撑满画面高度）")]
        [SerializeField, Min(1f)] private float framing = 1.25f;

        private bool playing;
        private Renderer canvas;

        public string Prompt => "Look";

        public InteractKind Kind => InteractKind.Painting;

        public bool CanInteract => !playing && !SceneTransitions.Busy
            && GameFlow.State != GameState.MainMenu && GameFlow.State != GameState.Ending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == PortraitName && t.GetComponent<LilyPortraitInspect>() == null)
                    {
                        t.gameObject.AddComponent<LilyPortraitInspect>();
                    }
                }
            }
        }

        private void Awake()
        {
            Transform c = transform.Find("Canvas");
            canvas = c != null ? c.GetComponent<Renderer>() : GetComponentInChildren<Renderer>();
            if (canvas == null || GetComponent<Collider>() != null)
            {
                return;
            }

            // 碰撞盒罩住画布（转到本组的局部坐标），稍微加点厚度，准星容易对上
            Bounds world = canvas.bounds;
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            Vector3 centre = transform.InverseTransformPoint(world.center);
            Vector3 size = transform.InverseTransformVector(world.size);
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            box.center = centre;
            box.size = new Vector3(Mathf.Max(size.x, 0.05f), Mathf.Max(size.y, 0.05f), Mathf.Max(size.z, 0.06f));
        }

        public void Interact(PlayerInteractor interactor)
        {
            Camera cam = interactor != null ? interactor.GetComponentInChildren<Camera>() : null;
            if (cam == null)
            {
                cam = Camera.main;
            }

            if (cam != null && canvas != null)
            {
                StartCoroutine(Inspect(cam));
            }
        }

        private IEnumerator Inspect(Camera cam)
        {
            playing = true;
            ThreatSchedule.NoCatch = true;
            GameFlow.SetInputLocked(true);

            Transform eye = cam.transform;
            Vector3 localPos = eye.localPosition;
            Quaternion localRot = eye.localRotation;
            Vector3 fromPos = eye.position;
            Quaternion fromRot = eye.rotation;

            // 画的正面朝向：取画布法线里指向镜头那一侧
            Vector3 focus = canvas.bounds.center;
            Vector3 normal = canvas.transform.forward;
            if (Vector3.Dot(normal, fromPos - focus) < 0f)
            {
                normal = -normal;
            }

            float halfHeight = canvas.bounds.extents.y * framing;
            float distance = halfHeight / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Vector3 toPos = focus + normal * distance;
            Quaternion toRot = Quaternion.LookRotation(-normal, Vector3.up);

            yield return Move(eye, fromPos, fromRot, toPos, toRot, pushSeconds);

            bool done = false;
            Presentation.ShowCaption(Staring, 2.8f, () => done = true);
            yield return new WaitUntil(() => done);
            done = false;
            Presentation.ShowCaption(SmallWriting, 4.2f, () => done = true);
            yield return new WaitUntil(() => done);

            yield return Move(eye, toPos, toRot, fromPos, fromRot, pullSeconds);
            eye.localPosition = localPos;
            eye.localRotation = localRot;

            GameFlow.SetInputLocked(false);
            ThreatSchedule.NoCatch = false;
            playing = false;
        }

        private static IEnumerator Move(Transform eye, Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                eye.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, k), Quaternion.Slerp(fromRot, toRot, k));
                yield return null;
            }

            eye.SetPositionAndRotation(toPos, toRot);
        }
    }
}
