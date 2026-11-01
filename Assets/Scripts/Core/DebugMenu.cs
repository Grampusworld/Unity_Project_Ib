using Ib.Environment;
using Ib.Player;
using Ib.Threats;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ib.Core
{
    /// <summary>
    /// 调试菜单：F1 开关，只在编辑器 / Development Build 里生效（正式包里按 F1 没反应）。
    /// 打开时释放光标、屏蔽玩家移动输入；关闭时把光标锁回去、恢复移动。
    /// 内容：自由飞行（穿墙 / 无重力）、无限体力、3 倍移动速度、哭泣天使阶段切换、全馆灯光开关、给主角手电筒。
    /// 提示：UI 用 OnGUI 顶着，和 PlayerInteractor 的交互提示同一套临时方案。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebugMenu : MonoBehaviour
    {
        [SerializeField] private FirstPersonController player;
        [SerializeField, Min(1f)] private float maxFlySpeed = 30f;

        /// <summary>菜单是否打开。别的系统要屏蔽输入时可以直接查这里。</summary>
        public bool IsOpen { get; private set; }

        private InputAction toggleAction;
        private PlayerStamina stamina;
        private bool infiniteStamina;
        private bool tripleSpeed;
        private float uiScale = 1f;
        private Texture2D panelTexture;

        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle hintStyle;
        private GUIStyle toggleStyle;
        private GUIStyle buttonStyle;
        private int styleHeight = -1;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Awake()
        {
            toggleAction = new InputAction("DebugMenu", InputActionType.Button, "<Keyboard>/f1");
        }

        private void OnEnable()
        {
            toggleAction?.Enable();
        }

        private void OnDisable()
        {
            toggleAction?.Disable();
            if (IsOpen)
            {
                Close();
            }
        }

        private void Update()
        {
            if (toggleAction == null)
            {
                return;
            }

            // PlayerInput 没接，直接读键盘：避免 Edit > Play 时焦点在别的窗口就失灵
            if (toggleAction.WasPressedThisFrame())
            {
                Toggle();
            }
        }
#endif

        /// <summary>无限体力开关。体力条会一直满，冲刺不再消耗。</summary>
        public void SetInfiniteStamina(bool on)
        {
            infiniteStamina = on;
            if (stamina == null && player != null)
            {
                stamina = player.GetComponent<PlayerStamina>();
            }

            if (stamina != null)
            {
                stamina.Infinite = on;
            }
        }

        /// <summary>3 倍移动速度开关（走 / 跑 / 蹲都乘 3）。</summary>
        public void SetTripleSpeed(bool on)
        {
            tripleSpeed = on;
            if (player != null)
            {
                player.SpeedMultiplier = on ? 3f : 1f;
            }
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;

            if (player == null)
            {
                player = FindAnyObjectByType<FirstPersonController>();
            }

            if (player != null)
            {
                player.InputBlocked = true;
            }

            if (stamina == null && player != null)
            {
                stamina = player.GetComponent<PlayerStamina>();
            }

            // 每次打开都对一遍真实状态，避免别处改过之后勾选框显示错误
            infiniteStamina = stamina != null && stamina.Infinite;
            tripleSpeed = player != null && Mathf.Approximately(player.SpeedMultiplier, 3f);

            PlayerLook.SuspendPointerCapture = true;
            PlayerLook.LockCursor(false);
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;

            if (player != null)
            {
                player.InputBlocked = false;
            }

            PlayerLook.SuspendPointerCapture = false;

            // 退出播放模式时别再抢光标，否则编辑器里鼠标消失
            PlayerLook.LockCursor(Application.isPlaying);
        }

        // ---------------------------------------------------------------- UI

        // 用固定面板而不是 GUILayout.Window：内置 window 样式的标题栏高度是写死的，
        // 一旦把字号调大标题就会被裁掉（试过，很难看）。

        private void OnGUI()
        {
            if (!IsOpen)
            {
                return;
            }

            EnsureStyles();

            float s = uiScale;
            Rect panel = new Rect(24f * s, 24f * s, 470f * s, 610f * s);
            GUI.DrawTexture(panel, panelTexture, ScaleMode.StretchToFill);

            GUILayout.BeginArea(new Rect(panel.x + 18f * s, panel.y + 14f * s, panel.width - 36f * s, panel.height - 24f * s));
            DrawContent();
            GUILayout.EndArea();
        }

        private void DrawContent()
        {
            GUILayout.Label("Ib 调试菜单", titleStyle);

            if (player == null)
            {
                GUILayout.Label("找不到 FirstPersonController。", labelStyle);
                return;
            }

            GUILayout.Space(10f);

            bool fly = GUILayout.Toggle(player.FreeFly, "  自由飞行（穿墙 / 无重力）", toggleStyle);
            if (fly != player.FreeFly)
            {
                player.SetFreeFly(fly);
            }

            GUILayout.Space(8f);
            GUILayout.Label("飞行速度  " + player.FlySpeed.ToString("0.0") + " m/s", labelStyle);
            float speed = GUILayout.HorizontalSlider(player.FlySpeed, 1f, maxFlySpeed);
            if (!Mathf.Approximately(speed, player.FlySpeed))
            {
                player.FlySpeed = Mathf.Round(speed * 10f) / 10f;
            }

            GUILayout.Label(player.FreeFly
                    ? "WASD 平移 · 空格上升 · Ctrl 下降 · Shift 加速"
                    : "打开后可用 WASD / 空格 / Ctrl / Shift 自由移动",
                hintStyle);

            GUILayout.Space(10f);

            bool infinite = GUILayout.Toggle(infiniteStamina, "  无限体力", toggleStyle);
            if (infinite != infiniteStamina)
            {
                SetInfiniteStamina(infinite);
            }

            bool triple = GUILayout.Toggle(tripleSpeed, "  3 倍移动速度", toggleStyle);
            if (triple != tripleSpeed)
            {
                SetTripleSpeed(triple);
            }

            GUILayout.Space(10f);
            GUILayout.Label("检查点传送：数字键 1–9（1 = CP0 黑屋起点）", hintStyle);

            DrawAngelControls();
            DrawWorldControls();
            GUILayout.FlexibleSpace();
            GUILayout.Label("F1 关闭菜单", hintStyle);
        }

        private void DrawAngelControls()
        {
            GUILayout.Space(12f);

            if (WeepingAngel.All.Count == 0)
            {
                GUILayout.Label("哭泣天使：场景里没有", labelStyle);
                return;
            }

            WeepingAngel first = WeepingAngel.All[0];
            string state = first.CurrentPhase switch
            {
                WeepingAngel.Phase.Still => "静止",
                WeepingAngel.Phase.Peek => "偷看",
                _ => first.IsChasing ? "攻击 · 追击中" : "攻击 · 巡逻中",
            };
            GUILayout.Label("哭泣天使：" + state + (first.IsObserved ? "（被注视，定住）" : ""), labelStyle);

            GUILayout.BeginHorizontal();
            AngelButton("静止 Still", WeepingAngel.Phase.Still, first.CurrentPhase);
            AngelButton("偷看 Peek", WeepingAngel.Phase.Peek, first.CurrentPhase);
            AngelButton("攻击 Attack", WeepingAngel.Phase.Attack, first.CurrentPhase);
            GUILayout.EndHorizontal();
        }

        private void AngelButton(string label, WeepingAngel.Phase target, WeepingAngel.Phase current)
        {
            GUI.enabled = target != current;
            if (GUILayout.Button(label, buttonStyle))
            {
                // ToArray：切到攻击可能触发 OnEnable/OnDisable 改动列表
                foreach (WeepingAngel angel in WeepingAngel.All.ToArray())
                {
                    angel.SetPhase(target);
                }
            }
            GUI.enabled = true;
        }

        private void DrawWorldControls()
        {
            GUILayout.Space(12f);

            if (GUILayout.Button(MuseumLights.On ? "关闭所有美术馆灯光" : "打开所有美术馆灯光", buttonStyle))
            {
                MuseumLights.Toggle();
            }

            PlayerFlashlight flashlight = PlayerFlashlight.Instance;
            GUI.enabled = flashlight != null && !flashlight.HasFlashlight;
            string label = flashlight == null ? "给主角手电筒（玩家相机上没有手电组件）"
                : flashlight.HasFlashlight ? "已有手电筒（F 开关）" : "给主角手电筒";
            if (GUILayout.Button(label, buttonStyle))
            {
                Ib.Flow.GameFlow.GiveFlashlight();
            }
            GUI.enabled = true;
        }

        /// <summary>按屏高重建样式，1080p 下正文 18 px。</summary>
        private void EnsureStyles()
        {
            if (styleHeight == Screen.height)
            {
                return;
            }

            styleHeight = Screen.height;
            uiScale = Mathf.Clamp(Screen.height / 1080f, 0.8f, 2.5f);
            float scale = uiScale;
            int size = Mathf.RoundToInt(18f * scale);

            if (panelTexture == null)
            {
                panelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point,
                };

                // 项目是 Linear 色彩空间，IMGUI 的 Color 按线性值走，屏幕上会再编码成 sRGB。
                // 想要视觉上的深灰面板（sRGB ≈ 0.10）就得写线性 0.010。
                panelTexture.SetPixel(0, 0, new Color(0.010f, 0.011f, 0.013f, 0.94f));
                panelTexture.Apply();
            }

            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(size * 1.25f) };
            titleStyle.normal.textColor = new Color(1f, 0.57f, 0.10f);

            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = size };
            labelStyle.normal.textColor = new Color(0.83f, 0.83f, 0.83f);

            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(10, Mathf.RoundToInt(size * 0.78f)),
                wordWrap = true,
            };
            hintStyle.normal.textColor = new Color(0.34f, 0.34f, 0.36f);

            toggleStyle = new GUIStyle(GUI.skin.toggle) { fontSize = size, fixedHeight = size * 1.9f };
            toggleStyle.normal.textColor = new Color(0.83f, 0.83f, 0.83f);
            toggleStyle.onNormal.textColor = Color.white;

            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = size, fixedHeight = size * 2f };
            buttonStyle.normal.textColor = new Color(0.83f, 0.83f, 0.83f);
        }
    }
}
