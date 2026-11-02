using System;
using System.Collections;
using Ib.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ib.Flow
{
    /// <summary>
    /// 真结局 CG（协程版，项目没装 Timeline）：4 张静帧，每张慢推/拉 + 平移 + 轻微明暗起伏，交叉淡化；
    /// 第 4 张在手指处扫一道湿颜料高光；最后慢慢渐黑，回调去放真结局字幕。约 29 s。Enter / Esc 跳过。
    /// 图：Resources/CG/TrueEnd/T_CG_TrueEnd_0N（编辑器里 TrueEndingCGImport 会把
    /// Assets/Art/Incoming/画作/ 下的同名图自动拷过来）；缺图时运行时生成占位图。
    /// BGM：Resources/Audio/BGM_TrueEnd（钢琴，未提供）→ 退回 Audio/MenuLullaby。
    /// </summary>
    public sealed class TrueEndingCG : MonoBehaviour
    {
        private const float Hold = 4.8f;      // 每张单独停留
        private const float Cross = 1.5f;     // 交叉淡化
        private const float FadeIn = 2f;
        private const float FadeOut = 3f;     // 末尾渐黑
        private const float MusicVolume = 0.35f;

        private static readonly string[] Names = { "T_CG_TrueEnd_01", "T_CG_TrueEnd_02", "T_CG_TrueEnd_03", "T_CG_TrueEnd_04" };
        private static readonly Color[] PlaceholderTint =
        {
            new Color(0.95f, 0.85f, 0.6f), new Color(0.75f, 0.7f, 0.65f), new Color(0.6f, 0.65f, 0.75f), new Color(0.8f, 0.6f, 0.5f),
        };

        // 每张的起止缩放与平移（归一化屏幕偏移）
        private static readonly Vector4[] Moves =
        {
            new Vector4(1.00f, 1.08f, 0.00f, 0.02f),  // 晨光：缓推
            new Vector4(1.10f, 1.03f, -0.02f, 0.02f), // Lily：缓拉 + 右移
            new Vector4(1.02f, 1.10f, 0.02f, -0.01f), // 补全的画：缓推
            new Vector4(1.06f, 1.14f, 0.00f, 0.00f),  // 手指特写：轻推
        };

        private RawImage[] frames;
        private RawImage glint;
        private Image black;
        private AudioSource music;
        private Action onDone;
        private bool finished;

        public static void Play(Action onDone)
        {
            var go = new GameObject("TrueEndingCG");
            var cg = go.AddComponent<TrueEndingCG>();
            cg.onDone = onDone;
            cg.Build();
            cg.StartCoroutine(cg.Run());
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            Stretch(NewGraphic<Image>("Back")).color = Color.black;
            frames = new RawImage[Names.Length];
            for (int i = 0; i < Names.Length; i++)
            {
                RawImage r = Stretch(NewGraphic<RawImage>(Names[i]));
                r.texture = LoadOrPlaceholder(i);
                r.color = new Color(1f, 1f, 1f, 0f);
                frames[i] = r;
            }

            glint = NewGraphic<RawImage>("WetGlint");
            glint.texture = GlintTexture();
            glint.color = new Color(1f, 0.97f, 0.9f, 0f);
            RectTransform g = glint.rectTransform;
            g.SetParent(frames[3].transform, false);   // 跟着第 4 张一起推镜
            g.anchorMin = g.anchorMax = new Vector2(0.645f, 0.713f); // 食指尖按在男孩胸口（04 原图 1920x1080 约 (1238, 310) px）
            g.sizeDelta = new Vector2(120f, 260f);
            g.localRotation = Quaternion.Euler(0f, 0f, -25f);

            black = Stretch(NewGraphic<Image>("Black"));
            black.color = Color.black;

            music = gameObject.AddComponent<AudioSource>();
            music.clip = Resources.Load<AudioClip>("Audio/BGM_TrueEnd") ?? Resources.Load<AudioClip>("Audio/MenuLullaby");
            music.loop = true;
            music.spatialBlend = 0f;
            music.ignoreListenerPause = true;
            music.volume = 0f;
            if (music.clip != null) music.Play();
        }

        private IEnumerator Run()
        {
            float total = FadeIn + Names.Length * Hold + (Names.Length - 1) * Cross + FadeOut;
            float shotLen = Hold + 2f * Cross;
            float start = Time.unscaledTime;
            frames[0].color = Color.white;

            while (!finished)
            {
                float t = Time.unscaledTime - start;
                if (t >= total || SkipPressed()) break;

                for (int i = 0; i < frames.Length; i++)
                {
                    float s = FadeIn - Cross + i * (Hold + Cross); // 这张开始淡入的时刻（第 0 张由黑幕淡开代替）
                    float local = Mathf.Clamp01((t - s) / shotLen);
                    // 后一张叠在上面淡入 = 交叉淡化；被完全盖住后再隐藏
                    float a = i == 0 ? 1f : Mathf.Clamp01((t - s) / Cross);
                    if (i < frames.Length - 1 && t > s + Hold + 2f * Cross + Cross) a = 0f;

                    Vector4 m = Moves[i];
                    float e = Mathf.SmoothStep(0f, 1f, local);
                    RectTransform rt = frames[i].rectTransform;
                    float scale = Mathf.Lerp(m.x, m.y, e);
                    rt.localScale = new Vector3(scale, scale, 1f);
                    rt.anchoredPosition = new Vector2(m.z * e * 1920f, m.w * e * 1080f);
                    float light = 0.9f + 0.1f * Mathf.Sin(local * Mathf.PI) + 0.03f * Mathf.Sin(t * 1.3f + i);
                    frames[i].color = new Color(light, light, light * 0.98f, a);
                }

                // 湿颜料高光：第 4 张进来 1.5 s 后扫一次
                float g0 = FadeIn - Cross + 3 * (Hold + Cross) + Cross + 1.5f;
                float gp = Mathf.Clamp01((t - g0) / 1.6f);
                glint.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-90f, 90f, gp), 0f);
                glint.color = new Color(1f, 0.97f, 0.9f, 0.55f * Mathf.Sin(gp * Mathf.PI));

                float fadeIn = Mathf.Clamp01(t / FadeIn);
                float fadeOut = Mathf.Clamp01((t - (total - FadeOut)) / FadeOut);
                black.color = new Color(0f, 0f, 0f, Mathf.Max(1f - fadeIn, fadeOut));
                music.volume = MusicVolume * AudioMix.Music * Mathf.Min(fadeIn, 1f - fadeOut * 0.7f);
                yield return null;
            }

            // 跳过 / 播完：确保全黑，再交给字幕
            float from = black.color.a, v0 = music.volume;
            for (float k = 0f; k < 0.8f && from < 1f; k += Time.unscaledDeltaTime)
            {
                black.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, 1f, k / 0.8f));
                yield return null;
            }

            black.color = Color.black;
            finished = true;
            onDone?.Invoke();
            // 音乐压低后留在字幕底下慢慢淡出
            for (float k = 0f; k < 4f; k += Time.unscaledDeltaTime)
            {
                music.volume = Mathf.Lerp(v0 * 0.6f, 0f, k / 4f);
                if (k > 0.2f) GetComponent<Canvas>().enabled = false;
                yield return null;
            }

            Destroy(gameObject);
        }

        private static bool SkipPressed()
        {
            Keyboard kb = Keyboard.current;
            return kb != null && (kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame);
        }

        private T NewGraphic<T>(string name) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            T g = go.AddComponent<T>();
            g.raycastTarget = false;
            return g;
        }

        private static T Stretch<T>(T g) where T : Graphic
        {
            RectTransform rt = g.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return g;
        }

        private static Texture LoadOrPlaceholder(int i)
        {
            Texture2D tex = Resources.Load<Texture2D>("CG/TrueEnd/" + Names[i]);
            if (tex != null) return tex;

            // 占位：竖向渐变 + 一块亮斑，色调区分四张
            const int w = 192, h = 108;
            tex = new Texture2D(w, h, TextureFormat.RGB24, false) { name = Names[i] + "_Placeholder", wrapMode = TextureWrapMode.Clamp };
            Color tint = PlaceholderTint[i];
            var spot = new Vector2(w * (0.3f + 0.15f * i), h * 0.55f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), spot) / w;
                float v = 0.25f + 0.35f * y / h + 0.5f * Mathf.Clamp01(1f - d * 3f);
                tex.SetPixel(x, y, tint * v);
            }

            tex.Apply();
            return tex;
        }

        private static Texture2D GlintTexture()
        {
            const int w = 32, h = 4;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < w; x++)
            {
                float a = Mathf.Pow(Mathf.Sin(Mathf.PI * x / (w - 1)), 3f);
                for (int y = 0; y < h; y++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

            tex.Apply();
            return tex;
        }
    }
}
