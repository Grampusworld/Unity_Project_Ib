using UnityEngine;
using UnityEngine.InputSystem;

namespace Ib.Player
{
    /// <summary>
    /// 主角手电筒：挂在玩家相机上，自己生成一盏聚光灯子物体。
    /// - 拿到手电之前什么都不做（HasFlashlight = false）；拿到后按 F 开关。
    /// - Flickering = true 时（被天使追击），灯每 flickerInterval 秒切换一次亮 / 灭，
    ///   灭的那一拍不算照着任何东西 —— 天使趁这一拍走。
    /// - IsIlluminating() 给注视判定用：点在光锥内、在射程内、灯和点之间没被挡住。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFlashlight : MonoBehaviour
    {
        [SerializeField] private bool hasFlashlight;
        [SerializeField] private bool startOn = true;

        [Header("光")]
        [SerializeField] private Color color = new Color(1f, 0.95f, 0.85f);
        [SerializeField, Min(0f)] private float intensity = 55f;
        [SerializeField, Min(1f)] private float range = 26f;
        [SerializeField, Range(10f, 90f)] private float spotAngle = 52f;
        [SerializeField] private Vector3 localOffset = new Vector3(0.18f, -0.15f, 0.1f);

        [Header("追击时闪烁")]
        [SerializeField, Min(0.05f)] private float flickerInterval = 0.4f;
        [Tooltip("每拍时长随机 ±，免得节奏太机械。")]
        [SerializeField, Range(0f, 0.2f)] private float flickerJitter = 0.08f;

        [Header("遮挡")]
        [SerializeField] private LayerMask occluders = ~0;

        public static PlayerFlashlight Instance { get; private set; }

        public bool HasFlashlight => hasFlashlight;

        /// <summary>玩家自己按 F 的开关状态（不含闪烁）。</summary>
        public bool SwitchedOn { get; private set; }

        /// <summary>被追击时由天使打开；所有天使都不追了再关上。</summary>
        public bool Flickering { get; set; }

        /// <summary>这一帧灯真的亮着。</summary>
        public bool IsLit => hasFlashlight && SwitchedOn && flickerPhaseOn;

        public float Range => range;

        private Light spot;
        private InputAction toggleAction;
        private bool flickerPhaseOn = true;
        private float nextFlickerTime;

        private void Awake()
        {
            Instance = this;
            SwitchedOn = startOn;

            GameObject go = new GameObject("Flashlight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localOffset;
            spot = go.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = color;
            spot.intensity = intensity;
            spot.range = range;
            spot.spotAngle = spotAngle;
            spot.innerSpotAngle = spotAngle * 0.55f;
            spot.shadows = LightShadows.Soft;
            spot.shadowNormalBias = 0.4f;

            toggleAction = new InputAction("Flashlight", InputActionType.Button, "<Keyboard>/f");
            ApplyLight();
        }

        private void OnEnable() => toggleAction?.Enable();

        private void OnDisable() => toggleAction?.Disable();

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (hasFlashlight && toggleAction.WasPressedThisFrame())
            {
                SwitchedOn = !SwitchedOn;
            }

            if (Flickering)
            {
                if (Time.time >= nextFlickerTime)
                {
                    flickerPhaseOn = !flickerPhaseOn;
                    nextFlickerTime = Time.time + flickerInterval + Random.Range(-flickerJitter, flickerJitter);
                }
            }
            else
            {
                flickerPhaseOn = true;
            }

            ApplyLight();
        }

        /// <summary>给主角手电筒（拾取 / 调试菜单）。</summary>
        public void Give()
        {
            hasFlashlight = true;
            SwitchedOn = true;
            ApplyLight();
        }

        /// <summary>这个点此刻是否被手电照着。</summary>
        public bool IsIlluminating(Vector3 point, Transform ignoreRoot)
        {
            if (!IsLit)
            {
                return false;
            }

            Vector3 origin = spot.transform.position;
            Vector3 toPoint = point - origin;
            float distance = toPoint.magnitude;
            if (distance > range || distance < 0.01f)
            {
                return false;
            }

            if (Vector3.Angle(spot.transform.forward, toPoint) > spotAngle * 0.5f)
            {
                return false;
            }

            return !Blocked(origin, point, ignoreRoot);
        }

        /// <summary>origin → point 之间有没有挡住的东西（忽略玩家自己和 ignoreRoot）。</summary>
        public bool Blocked(Vector3 origin, Vector3 point, Transform ignoreRoot)
        {
            Vector3 dir = point - origin;
            RaycastHit[] hits = Physics.RaycastAll(origin, dir.normalized, dir.magnitude, occluders, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                Transform t = hit.transform;
                if (t.IsChildOf(transform.root) || (ignoreRoot != null && t.IsChildOf(ignoreRoot)))
                {
                    continue;
                }
                return true;
            }
            return false;
        }

        private void ApplyLight()
        {
            if (spot != null)
            {
                spot.enabled = IsLit;
            }
        }
    }
}
