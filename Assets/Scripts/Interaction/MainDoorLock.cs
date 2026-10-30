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
    /// Ib Level 挂在 MainDoor_Double/MainDoor_Lock 上（布置后半段时生成）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainDoorLock : MonoBehaviour, IInteractable
    {
        [SerializeField] private GameObject keyVisual;

        private Collider[] colliders;

        public string Prompt => !GameFlow.HasKey ? "Push" : GameFlow.KeyInserted ? "Open" : "Insert key";

        public InteractKind Kind => InteractKind.Door;

        public bool CanInteract => InRealMuseum && !SceneTransitions.Busy;

        private static bool InRealMuseum => GameFlow.State >= GameState.AfterHours && GameFlow.State != GameState.Ending;

        private void Awake() => colliders = GetComponents<Collider>();

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

            if (keyVisual != null)
            {
                keyVisual.SetActive(GameFlow.KeyInserted);
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
                Sfx.PlayAt(Sfx.KeyClick, transform.position);
                GameFlow.KeyInserted = true;
                Apply();
                return;
            }

            SceneTransitions.AskOpenMainDoor();
        }
    }
}
