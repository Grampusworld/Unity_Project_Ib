using Ib.Core;
using Ib.Flow;
using UnityEngine;

namespace Ib.Interaction
{
    /// <summary>
    /// 正门的黄铜锁（GDD §3.5 / §3.6 第 13、17 步），只在出画以后（真实馆里）出现：
    /// 没钥匙 = 推不动（咔哒一声，拿到手电以后才给「找到打开主门的钥匙」）；
    /// 有钥匙 = 第一次按 E 插进锁孔；之后再按 E 弹确认窗，选 Open 进坏结局，选 Don't open 关窗、钥匙留在锁里。
    /// 碰撞体是贴在门内侧的一块薄板，序章时关掉，免得挡住推门（DoubleDoor 自己的交互）。
    /// 锁片挂在门的根节点上、不跟门板转，所以只在门关着、并且已经进过馆（大门被 MuseumEntrance 关死以后）才显示；
    /// 推门开着的那段时间不显示，不然会悬在门洞里。
    /// 第一次插钥匙有一段约 1 s 的动画（老大 2026-11-01）：镜头凑到锁前，钥匙滑进锁孔、拧 90°，锁舌缩回去，咔哒一声，
    /// 镜头退回、放开控制；之后仍是"再按一次 → 确认窗"。钥匙模型 / 锁舌由「布置正门钥匙」菜单换上（keyVisual = MainDoorKey 实例）。
    /// Ib Level 挂在 MainDoor_Double/MainDoor_Lock 上（布置后半段时生成）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainDoorLock : MonoBehaviour, IInteractable
    {
        [SerializeField] private GameObject keyVisual;
        [SerializeField] private GameObject lockVisual;

        [Header("开锁动画")]
        [Tooltip("锁舌：锁上时伸出、开锁后缩回 boltTravel（本地坐标）")]
        [SerializeField] private Transform bolt;
        [SerializeField] private Vector3 boltTravel = new Vector3(-0.045f, 0f, 0f);
        [Tooltip("钥匙插好时 keyVisual 的本地位置往外退多少开始插（沿锁孔轴，本地坐标）")]
        [SerializeField] private Vector3 keyStartOffset = new Vector3(0f, 0f, 0.09f);
        [SerializeField, Min(0f)] private float turnDegrees = 90f;
        [SerializeField, Min(0.1f)] private float viewDistance = 0.42f;

        private Vector3 keyInsertedPos;
        private Quaternion keyTurnedRot;
        private Vector3 boltLocked;
        private bool animating;

        private Collider[] colliders;
        private DoubleDoor door;

        public string Prompt => !GameFlow.HasKey ? "Push" : GameFlow.KeyInserted ? "Open" : "Insert key";

        public InteractKind Kind => InteractKind.Door;

        public bool CanInteract => InRealMuseum && !SceneTransitions.Busy && !animating;

        private static bool InRealMuseum => GameFlow.State >= GameState.AfterHours && GameFlow.State != GameState.Ending;

        private void Awake()
        {
            colliders = GetComponents<Collider>();
            door = GetComponentInParent<DoubleDoor>();
            if (lockVisual == null)
            {
                Transform plate = transform.Find("LockPlate");
                lockVisual = plate != null ? plate.gameObject : null;
            }

            // keyVisual 在场景里摆的是"插好、已拧"的姿态
            if (keyVisual != null)
            {
                keyInsertedPos = keyVisual.transform.localPosition;
                keyTurnedRot = keyVisual.transform.localRotation;
            }

            if (bolt != null)
            {
                boltLocked = bolt.localPosition;
            }
        }

        private void OnEnable()
        {
            GameFlow.StateChanged += OnStateChanged;
            Apply();
        }

        private void OnDisable() => GameFlow.StateChanged -= OnStateChanged;

        private void OnStateChanged(GameState from, GameState to) => Apply();

        private void Apply()
        {
            bool on = InRealMuseum;
            foreach (Collider c in colliders)
            {
                c.enabled = on;
            }

            ApplyVisuals();
        }

        // 门是 MuseumEntrance 瞬间关上的，没有事件，每帧对一下
        private void LateUpdate() => ApplyVisuals();

        private void ApplyVisuals()
        {
            bool shown = GameFlow.State >= GameState.PaintedGallery && (door == null || door.IsClosed);
            if (lockVisual != null && lockVisual.activeSelf != shown)
            {
                lockVisual.SetActive(shown);
            }

            bool key = shown && (GameFlow.KeyInserted || animating);
            if (keyVisual != null && keyVisual.activeSelf != key)
            {
                keyVisual.SetActive(key);
            }

            if (bolt != null && !animating)
            {
                bolt.localPosition = GameFlow.KeyInserted ? boltLocked + boltTravel : boltLocked;
            }
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (!GameFlow.HasKey)
            {
                Sfx.PlayAt(Sfx.LockRattle, transform.position, 0.9f);
                SceneTransitions.TriedLockedMainDoor();
                return;
            }

            if (!GameFlow.KeyInserted)
            {
                Camera cam = interactor != null ? interactor.GetComponentInChildren<Camera>() : Camera.main;
                if (keyVisual == null || cam == null)
                {
                    Sfx.PlayAt(Sfx.KeyClick, transform.position);
                    GameFlow.KeyInserted = true;
                    Apply();
                    return;
                }

                StartCoroutine(Unlock(cam.transform));
                return;
            }

            SceneTransitions.AskOpenMainDoor();
        }

        /// <summary>镜头凑近 0.3 s → 钥匙滑进去 0.35 s → 拧 0.3 s → 锁舌缩回 0.25 s（咔哒）→ 镜头退回 0.3 s。</summary>
        private System.Collections.IEnumerator Unlock(Transform eye)
        {
            animating = true;
            GameFlow.SetInputLocked(true);

            Transform k = keyVisual.transform;
            Quaternion straight = keyTurnedRot * Quaternion.AngleAxis(-turnDegrees, Vector3.forward);
            k.localPosition = keyInsertedPos + keyStartOffset;
            k.localRotation = straight;
            // 纸签一直往下垂：钥匙拧多少，签子反着转多少（场景里摆的是拧好以后、签子已经反转的样子）
            Transform tag = k.Find("Tag");
            Quaternion tagTurned = tag != null ? tag.localRotation : Quaternion.identity;
            if (tag != null)
            {
                tag.localRotation = tagTurned * Quaternion.AngleAxis(turnDegrees, Vector3.forward);
            }
            ApplyVisuals();

            // 镜头：正对锁孔、离锁 viewDistance，略高一点往下看
            Vector3 hole = transform.TransformPoint(keyInsertedPos);
            Vector3 outward = transform.TransformDirection(Vector3.forward);
            Vector3 viewPos = hole + outward * viewDistance + Vector3.up * 0.06f + transform.TransformDirection(Vector3.right) * 0.08f;
            Quaternion viewRot = Quaternion.LookRotation(hole - viewPos, Vector3.up);
            Vector3 eyeLocal = eye.localPosition;
            Quaternion eyeLocalRot = eye.localRotation;
            Vector3 fromPos = eye.position;
            Quaternion fromRot = eye.rotation;

            yield return Tween(0.3f, s => eye.SetPositionAndRotation(Vector3.Lerp(fromPos, viewPos, s), Quaternion.Slerp(fromRot, viewRot, s)));
            yield return Tween(0.35f, s => k.localPosition = Vector3.Lerp(keyInsertedPos + keyStartOffset, keyInsertedPos, s));
            Sfx.PlayAt(Sfx.KeyClick, hole, 0.5f, 10f);
            yield return Tween(0.3f, s =>
            {
                k.localRotation = Quaternion.Slerp(straight, keyTurnedRot, s);
                if (tag != null)
                {
                    tag.localRotation = tagTurned * Quaternion.AngleAxis(turnDegrees * (1f - s), Vector3.forward);
                }
            });
            if (bolt != null)
            {
                yield return Tween(0.25f, s => bolt.localPosition = Vector3.Lerp(boltLocked, boltLocked + boltTravel, s));
            }

            Sfx.PlayAt(Sfx.LockRattle, hole, 0.35f, 10f);
            GameFlow.KeyInserted = true;
            yield return new WaitForSeconds(0.25f);

            Vector3 backPos = eye.parent != null ? eye.parent.TransformPoint(eyeLocal) : fromPos;
            Quaternion backRot = eye.parent != null ? eye.parent.rotation * eyeLocalRot : fromRot;
            yield return Tween(0.3f, s => eye.SetPositionAndRotation(Vector3.Lerp(viewPos, backPos, s), Quaternion.Slerp(viewRot, backRot, s)));
            eye.localPosition = eyeLocal;
            eye.localRotation = eyeLocalRot;

            animating = false;
            Apply();
            GameFlow.SetInputLocked(false);
        }

        private static System.Collections.IEnumerator Tween(float seconds, System.Action<float> set)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                set(Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }

            set(1f);
        }
    }
}
