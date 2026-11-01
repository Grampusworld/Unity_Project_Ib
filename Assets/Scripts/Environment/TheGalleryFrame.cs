using Ib.Flow;
using UnityEngine;

namespace Ib.Environment
{
    /// <summary>
    /// 全家福厅北墙的画框（GDD §3.2 第四、五段）：
    /// · 画中馆：盖着布（画不能把自己画进去）；
    /// · 黑屋（二次）：布已揭开、框里是门洞（PaintingPortal，回看黑屋）；厅里亮一盏冷月光，透过黑屋的空画框看得见；
    /// · 刚出画：仍是门洞。玩家背过去、画框不在视野里（Unseen：视锥 + 多点射线）超过 minUnseen 秒，
    ///   换成《美术馆》这幅画（观众席少一个小人，全家福前站着一个模糊的大人），黑屋整块停用 —— 全片第一次"回头杀"。
    ///   只换一次，之后就是画布（§7.1"门已经用完了"）。
    /// · 读档 / 调试直接进入更后的状态：直接是画。
    /// Ib Level 挂在 Painting_TheMuseum_ExitFrame（「布置出画演出」菜单生成）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TheGalleryFrame : MonoBehaviour
    {
        private enum Mode { Covered, Portal, Painting }

        [SerializeField] private GameObject cloth;
        [SerializeField] private PaintingPortal window;
        [SerializeField] private GameObject painting;
        [SerializeField] private Light moonlight;
        [SerializeField, Min(0f)] private float minUnseen = 0.35f;

        private Mode mode = (Mode)(-1);
        private float unseenFor;
        private Camera viewer;

        private void OnEnable()
        {
            GameFlow.StateChanged += OnStateChanged;
            Sync(GameFlow.State, false);
        }

        private void OnDisable() => GameFlow.StateChanged -= OnStateChanged;

        private void OnStateChanged(GameState from, GameState to) => Sync(to, from == GameState.BlackRoomReturn);

        /// <summary>throughFrame：刚从黑屋的空画框穿过来，先保持门洞，等玩家背过去再换。</summary>
        private void Sync(GameState s, bool throughFrame)
        {
            if (s <= GameState.PaintedGallery)
            {
                Set(Mode.Covered);
            }
            else if (s == GameState.BlackRoomReturn || (throughFrame && mode == Mode.Portal))
            {
                Set(Mode.Portal);
            }
            else
            {
                Set(Mode.Painting);
            }

            if (moonlight != null)
            {
                moonlight.enabled = s == GameState.BlackRoomReturn;
            }
        }

        private void Set(Mode m)
        {
            if (mode == m)
            {
                return;
            }

            mode = m;
            unseenFor = 0f;
            if (cloth != null) cloth.SetActive(m == Mode.Covered);
            if (window != null) window.gameObject.SetActive(m == Mode.Portal);
            if (painting != null) painting.SetActive(m == Mode.Painting);

            // 画框关上以后黑屋就没用了（穿框前它得开着：门洞要渲染黑屋，玩家也站在里面）
            if (m == Mode.Painting && WorldState.BlackRoom != null && GameFlow.State > GameState.BlackRoomReturn)
            {
                WorldState.BlackRoom.SetActive(false);
            }
        }

        private void Update()
        {
            if (mode != Mode.Portal || GameFlow.State == GameState.BlackRoomReturn)
            {
                return;
            }

            if (viewer == null)
            {
                viewer = Camera.main;
                if (viewer == null)
                {
                    return;
                }
            }

            Bounds b = window.GetComponent<Renderer>().bounds;
            b.Expand(0.1f);
            if (Unseen.IsVisible(viewer, transform, b))
            {
                unseenFor = 0f;
                return;
            }

            unseenFor += Time.deltaTime;
            if (unseenFor >= minUnseen)
            {
                Set(Mode.Painting);
                Debug.Log("[TheGalleryFrame] 玩家背过去了：画框 → 《美术馆》。", this);
            }
        }
    }
}
