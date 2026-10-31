using System.Collections.Generic;
using Ib.Flow;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Ib.Interaction
{
    /// <summary>
    /// 给场景里所有 IInteractable 头顶自动挂一个始终朝向镜头的"!"标记，提醒玩家这里能交互。
    /// 场景加载后扫一遍整个场景收集，不用在预制体/关卡里逐个手摆。
    /// 玩家交互过一次后，该物体的标记永久去掉（同一场景内）。
    /// 物体当前不能交互（已拾取、已用完、阶段不对）或离镜头太远时隐藏标记；非正常游玩（菜单/暂停/阅读）时整体隐藏。
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class InteractMarkers : MonoBehaviour
    {
        private const float MaxDistance = 12f;
        private const float HeightAboveBounds = 0.12f;
        private const float BobAmount = 0.06f;
        private const float BobSpeed = 2.4f;
        private const float PulseAmount = 0.18f;
        private const float PulseSpeed = 3.1f;
        private const float BaseScale = 0.22f;

        private static Sprite markSprite;

        private readonly List<Entry> entries = new List<Entry>();
        private Transform cameraTransform;

        private sealed class Entry
        {
            public IInteractable Interactable;
            public Transform Marker;
            public SpriteRenderer Renderer;
            public Vector3 BasePosition;
            public float Phase;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Bootstrap();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive)
            {
                return;
            }

            Bootstrap();
        }

        private static void Bootstrap()
        {
            if (FindAnyObjectByType<InteractMarkers>() != null)
            {
                return;
            }

            new GameObject("--- Interact Markers ---").AddComponent<InteractMarkers>();
        }

        private void OnEnable() => PlayerInteractor.Interacted += OnInteracted;

        private void OnDisable() => PlayerInteractor.Interacted -= OnInteracted;

        private void OnInteracted(IInteractable interactable)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(entries[i].Interactable, interactable)) continue;
                if (entries[i].Marker != null) Destroy(entries[i].Marker.gameObject);
                entries.RemoveAt(i);
            }
        }

        private void Awake()
        {
            foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb is IInteractable interactable)
                {
                    Add(interactable, mb);
                }
            }
        }

        private void Add(IInteractable interactable, MonoBehaviour owner)
        {
            Renderer[] renderers = owner.GetComponentsInChildren<Renderer>();
            Bounds bounds = default;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!hasBounds)
                {
                    bounds = renderers[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            Vector3 top = hasBounds
                ? new Vector3(bounds.center.x, bounds.max.y, bounds.center.z)
                : owner.transform.position;
            Vector3 basePosition = top + Vector3.up * HeightAboveBounds;

            var markerGo = new GameObject("InteractMark");
            markerGo.transform.SetPositionAndRotation(basePosition, Quaternion.identity);
            markerGo.transform.localScale = Vector3.one * BaseScale;

            var sr = markerGo.AddComponent<SpriteRenderer>();
            sr.sprite = MarkSprite();
            sr.color = new Color(1f, 0.85f, 0.35f, 1f);
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;
            sr.lightProbeUsage = LightProbeUsage.Off;
            sr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            sr.enabled = false;

            entries.Add(new Entry
            {
                Interactable = interactable,
                Marker = markerGo.transform,
                Renderer = sr,
                BasePosition = basePosition,
                Phase = Random.Range(0f, Mathf.PI * 2f),
            });
        }

        private void Update()
        {
            if (cameraTransform == null)
            {
                Camera cam = Camera.main;
                if (cam == null)
                {
                    return;
                }

                cameraTransform = cam.transform;
            }

            bool aiming = GameFlow.IsAiming;
            float time = Time.time;

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                Entry entry = entries[i];
                if (entry.Marker == null)
                {
                    entries.RemoveAt(i);
                    continue;
                }

                bool visible = aiming && entry.Interactable.CanInteract
                    && Vector3.Distance(cameraTransform.position, entry.BasePosition) <= MaxDistance;

                entry.Renderer.enabled = visible;
                if (!visible)
                {
                    continue;
                }

                float bob = Mathf.Sin((time + entry.Phase) * BobSpeed) * BobAmount;
                entry.Marker.position = entry.BasePosition + Vector3.up * bob;
                entry.Marker.rotation = cameraTransform.rotation;

                float pulse = 1f + Mathf.Sin((time + entry.Phase) * PulseSpeed) * PulseAmount;
                entry.Marker.localScale = Vector3.one * (BaseScale * pulse);
            }
        }

        /// <summary>"!" 的程序纹理：圆点 + 竖条，暖黄色，自带描边方便在任何背景上看清。</summary>
        private static Sprite MarkSprite()
        {
            if (markSprite != null)
            {
                return markSprite;
            }

            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                name = "InteractMark",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color[n * n];
            float cx = n * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = x - cx;
                    float a = Shape(dx, y, n);
                    pixels[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            markSprite = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), n);
            markSprite.hideFlags = HideFlags.HideAndDontSave;
            return markSprite;
        }

        /// <summary>竖条（0.62~0.88 高度）+ 圆点（0.12~0.22 高度），居中、带 1px 柔化边。</summary>
        private static float Shape(float dx, int y, int n)
        {
            float ny = y / (float)n;
            float halfWidth = 0.09f * n;

            bool inBar = ny >= 0.42f && ny <= 0.88f;   // 竖条在上
            bool inDot = ny >= 0.12f && ny <= 0.28f;   // 圆点在下
            if (!inBar && !inDot)
            {
                return 0f;
            }

            float edge = Mathf.Clamp01(halfWidth - Mathf.Abs(dx) + 0.75f);
            return edge;
        }
    }
}
