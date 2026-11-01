using Ib.Core;
using Ib.Flow;
using UnityEngine;

namespace Ib.Environment
{
    /// <summary>
    /// 布展变化（GDD §3.3 / 支柱 3「不能相信自己身后的空间」）：馆藏在玩家看不见的地方布展。
    /// 一个组件 = 一处变化，只发生一次：在状态窗口 [fromState, toState] 内（可要求已拿手电），
    /// 玩家离 area 至少 minDistance 米、且 area 连续 minUnseen 秒不在视野里（Unseen：视锥 + 多点射线，R4-2）时生效。
    /// 四种做法：
    ///   Swap      —— hide 里的关掉、show 里的打开（门变墙）；
    ///   Retexture —— 把 renderer 的画面换成 texture（画作内容改变）；
    ///   Move      —— targets 绕 area 中心（水平）转 yaw 度、再平移 offset（展品挪位 / 画原地转过去面朝墙）；
    /// 生效时在 area 中心放一声 sound（布展声：挂画钩 / 拖画框 / 石头摩擦），概率 soundChance。
    /// 读档不复原（布展是单向的）。由「布置布展变化」菜单生成，挂在 --- Rearrange --- 下各自的物体上。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChangeWhenUnseen : MonoBehaviour
    {
        public enum Kind { Swap, Retexture, Move }

        [SerializeField] private Kind kind;

        [Header("什么时候")]
        [SerializeField] private GameState fromState = GameState.PowerOut;
        [SerializeField] private GameState toState = GameState.AngelsActive;
        [SerializeField] private bool needsFlashlight;
        [SerializeField, Min(0f)] private float minUnseen = 2f;
        [SerializeField, Min(0f)] private float minDistance = 8f;
        [Tooltip("要检测的区域（世界坐标），生成时取受影响物体的包围盒")]
        [SerializeField] private Bounds area;

        [Header("Swap")]
        [SerializeField] private GameObject[] hide = new GameObject[0];
        [SerializeField] private GameObject[] show = new GameObject[0];

        [Header("Retexture")]
        [SerializeField] private Renderer canvas;
        [SerializeField] private Texture2D texture;

        [Header("Move")]
        [SerializeField] private Transform[] targets = new Transform[0];
        [SerializeField] private Vector3 offset;
        [SerializeField] private float yaw;

        [Header("布展声")]
        [SerializeField] private string sound = Sfx.FrameScrape;
        [SerializeField, Range(0f, 1f)] private float soundChance = 1f;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 0.55f;

        private const float CheckInterval = 0.15f;

        private bool done;
        private float unseenFor;
        private float nextCheck;
        private Camera viewer;

        public bool Done => done;

        private bool InWindow =>
            GameFlow.State >= fromState && GameFlow.State <= toState
            && GameFlow.State != GameState.Ending
            && (!needsFlashlight || GameFlow.HasFlashlight);

        private void Update()
        {
            if (done)
            {
                return;
            }

            if (!InWindow)
            {
                unseenFor = 0f;
                return;
            }

            if (Time.time < nextCheck)
            {
                return;
            }

            nextCheck = Time.time + CheckInterval;

            if (viewer == null)
            {
                viewer = Camera.main;
                if (viewer == null)
                {
                    return;
                }
            }

            Vector3 eye = viewer.transform.position;
            if ((area.ClosestPoint(eye) - eye).magnitude < minDistance
                || Unseen.IsVisible(viewer, Subject, area))
            {
                unseenFor = 0f;
                return;
            }

            unseenFor += CheckInterval;
            if (unseenFor >= minUnseen)
            {
                Apply();
            }
        }

        /// <summary>射线打到它也算看见：Swap 看被关掉的那件，其余看要改的那件。</summary>
        private Transform Subject
        {
            get
            {
                if (kind == Kind.Swap && hide.Length > 0 && hide[0] != null) return hide[0].transform;
                if (kind == Kind.Retexture && canvas != null) return canvas.transform.parent != null ? canvas.transform.parent : canvas.transform;
                if (kind == Kind.Move && targets.Length > 0 && targets[0] != null) return targets[0];
                return transform;
            }
        }

        /// <summary>立即生效（调试 / 截图用）。</summary>
        public void Apply()
        {
            if (done)
            {
                return;
            }

            done = true;
            switch (kind)
            {
                case Kind.Swap:
                    foreach (GameObject go in hide) if (go != null) go.SetActive(false);
                    foreach (GameObject go in show) if (go != null) go.SetActive(true);
                    break;
                case Kind.Retexture:
                    if (canvas != null && texture != null)
                    {
                        Material m = canvas.material;
                        m.SetTexture("_BaseMap", texture);
                        m.mainTexture = texture;
                    }

                    break;
                case Kind.Move:
                    if (targets.Length > 0 && targets[0] != null)
                    {
                        // 绕包围盒中心（水平）转：画、雕像原地转身，占地不变
                        Vector3 pivot = new Vector3(area.center.x, targets[0].position.y, area.center.z);
                        Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
                        foreach (Transform t in targets)
                        {
                            if (t == null) continue;
                            t.SetPositionAndRotation(pivot + turn * (t.position - pivot) + offset, turn * t.rotation);
                        }
                    }

                    break;
            }

            if (!string.IsNullOrEmpty(sound) && Random.value <= soundChance)
            {
                Sfx.PlayAt(sound, area.center, soundVolume, 22f);
            }

            Debug.Log("[ChangeWhenUnseen] 布展：" + name + "（" + kind + "）", this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
            Gizmos.DrawWireCube(area.center, area.size);
        }
    }
}
