using UnityEngine;

namespace Ib.Player
{
    /// <summary>
    /// Ted 的简易身体（GDD §9 第 12b 项）：第一人称平时看不见，只给镜子照（PlanarMirror）。
    /// 放在 TedReflection 层，主相机不渲染这一层；每帧跟着玩家胶囊走、朝玩家的水平朝向，
    /// 走动时身体轻轻起伏、手臂前后摆，蹲下时整体压低。没有骨骼。
    /// Ib Level 由「布置展厅细节」生成在场景根上（不放进 Player 预制体）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TedBody : MonoBehaviour
    {
        public const string LayerName = "TedReflection";

        public static int LayerMask => 1 << UnityEngine.LayerMask.NameToLayer(LayerName);

        [SerializeField] private Transform armLeft;
        [SerializeField] private Transform armRight;
        [SerializeField, Min(0f)] private float bobHeight = 0.025f;
        [SerializeField, Min(0f)] private float swingDegrees = 22f;
        [SerializeField, Min(0.1f)] private float stepsPerMeter = 1.6f;

        private FirstPersonController player;
        private float phase;
        private float crouch;

        private void Start()
        {
            int layer = UnityEngine.LayerMask.NameToLayer(LayerName);
            if (layer < 0)
            {
                Debug.LogWarning("[TedBody] 没有 " + LayerName + " 层，镜子里看不到 Ted。", this);
                return;
            }

            foreach (Camera cam in FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                cam.cullingMask &= ~(1 << layer);
            }
        }

        private void LateUpdate()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<FirstPersonController>();
                if (player == null)
                {
                    return;
                }
            }

            float speed = player.HorizontalSpeed;
            phase += speed * stepsPerMeter * Mathf.PI * Time.deltaTime;
            float moving = Mathf.Clamp01(speed / 2f);
            crouch = Mathf.MoveTowards(crouch, player.IsCrouching ? 1f : 0f, Time.deltaTime * 6f);

            Camera eye = player.GetComponentInChildren<Camera>();
            float yaw = eye != null ? eye.transform.eulerAngles.y : player.transform.eulerAngles.y;
            Vector3 feet = player.transform.position;
            transform.SetPositionAndRotation(
                feet + Vector3.up * (Mathf.Abs(Mathf.Sin(phase)) * bobHeight * moving),
                Quaternion.Euler(0f, yaw, 0f));
            transform.localScale = new Vector3(1f, 1f - 0.3f * crouch, 1f);

            float swing = Mathf.Sin(phase) * swingDegrees * moving;
            if (armLeft != null)
            {
                armLeft.localRotation = Quaternion.Euler(swing, 0f, 0f);
            }

            if (armRight != null)
            {
                armRight.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            }
        }
    }
}
