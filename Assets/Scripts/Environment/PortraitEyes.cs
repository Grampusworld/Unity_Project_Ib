using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ib.Environment
{
    /// <summary>
    /// 二楼值班室门口的 Lily 肖像（Sketch_LilyLooksAtDoor_Portrait）：画里小女孩的眼珠一直看着玩家（2026-11-01 老大需求，方案 A）。
    /// 美术把画拆成 画布（眼裂抠空）/ 虹膜 Iris_L、Iris_R / 眼白 三层（Docs/LilyPortrait_Eyes.md），这里只平移两片虹膜的 localPosition.x/y：
    /// 按玩家相对画的方向算目标偏移，椭圆限幅后 SmoothDamp 过去；玩家在画背后就回正。从游戏一开始就生效。
    /// 场景里不用摆：每次加载场景按名字找到这组画，自己挂上去。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortraitEyes : MonoBehaviour
    {
        public const string PortraitName = "Sketch_LilyLooksAtDoor_Portrait";

        [Tooltip("虹膜最大位移（组的本地坐标，米），按椭圆限幅。")]
        [SerializeField] private Vector2 maxOffset = new Vector2(0.0109f, 0.0039f);
        [Tooltip("横向 / 纵向视角斜率达到这个值时虹膜走到头。")]
        [SerializeField] private Vector2 fullTurnSlope = new Vector2(1f, 0.8f);
        [SerializeField, Range(0.05f, 0.5f)] private float smoothTime = 0.15f;
        [Tooltip("离画超过这个距离就不再更新（看不清，省一点）。")]
        [SerializeField, Min(1f)] private float maxDistance = 40f;

        private Transform irisL;
        private Transform irisR;
        private Vector3 restL;
        private Vector3 restR;
        private Vector2 offset;
        private Vector2 velocity;
        private Camera viewer;

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
                Transform t = FindDeep(root.transform, PortraitName);
                if (t != null && t.GetComponent<PortraitEyes>() == null)
                {
                    t.gameObject.AddComponent<PortraitEyes>();
                }
            }
        }

        private void Awake()
        {
            irisL = transform.Find("Iris_L");
            irisR = transform.Find("Iris_R");
            if (irisL != null)
            {
                restL = irisL.localPosition;
            }

            if (irisR != null)
            {
                restR = irisR.localPosition;
            }

            if (irisL == null && irisR == null)
            {
                Debug.LogWarning("[PortraitEyes] " + name + " 下找不到 Iris_L / Iris_R。", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (viewer == null || !viewer.isActiveAndEnabled)
            {
                viewer = Camera.main;
                if (viewer == null)
                {
                    return;
                }
            }

            // 组的本地坐标：+X = 画面右，+Y 上，观众在 −Z 一侧
            Vector3 p = transform.InverseTransformPoint(viewer.transform.position);
            Vector2 target = Vector2.zero;
            if (p.z < 0f && p.sqrMagnitude < maxDistance * maxDistance)
            {
                float depth = Mathf.Max(0.2f, -p.z);
                float sx = Mathf.Clamp(p.x / depth / fullTurnSlope.x, -1f, 1f);
                float sy = Mathf.Clamp(p.y / depth / fullTurnSlope.y, -1f, 1f);
                target = new Vector2(sx * maxOffset.x, sy * maxOffset.y);

                // 椭圆限幅
                float e = (target.x / maxOffset.x) * (target.x / maxOffset.x) + (target.y / maxOffset.y) * (target.y / maxOffset.y);
                if (e > 1f)
                {
                    target /= Mathf.Sqrt(e);
                }
            }

            offset = Vector2.SmoothDamp(offset, target, ref velocity, smoothTime, Mathf.Infinity, Time.deltaTime);
            Apply(irisL, restL);
            Apply(irisR, restR);
        }

        private void Apply(Transform iris, Vector3 rest)
        {
            if (iris != null)
            {
                iris.localPosition = new Vector3(rest.x + offset.x, rest.y + offset.y, rest.z);
            }
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name)
            {
                return parent;
            }

            foreach (Transform child in parent)
            {
                Transform found = FindDeep(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
