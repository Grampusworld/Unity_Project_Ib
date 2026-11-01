using UnityEngine;
using UnityEngine.UI;

namespace Ib.UI
{
    /// <summary>
    /// 过场用的全屏遮罩，独立一块 Overlay Canvas，压在场景上、在 Presentation UI（字幕、渐黑）下面：
    /// 1. Hole：四周黑、中间留一个矩形洞（Ib/UI/RectVignette），出画时展厅从外往里褪黑、只剩那幅画；
    /// 2. Card：在洞的位置贴一张画（同一张贴图），盖住画前的人和两边灯光的差别，让镜头无缝换到黑屋；
    /// 3. Flash：整屏一闪（穿过空画框时的冷白光，GDD R4-3 的退路）。
    /// 坐标都是 0–1 的屏幕坐标（左下为原点）。场景重新加载时跟着销毁，下次用到再建。
    /// </summary>
    public sealed class ScreenVeil : MonoBehaviour
    {
        private const string MaterialPath = "UI/M_RectVignette";

        private static ScreenVeil instance;

        private RawImage hole;
        private Material holeMaterial;
        private RawImage card;
        private Image flash;
        private Text hint;

        private static readonly int RectId = Shader.PropertyToID("_Rect");
        private static readonly int FeatherId = Shader.PropertyToID("_Feather");
        private static readonly int ScreenSizeId = Shader.PropertyToID("_ScreenSize");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private static ScreenVeil Get()
        {
            if (instance != null)
            {
                return instance;
            }

            var go = new GameObject("--- Screen Veil ---", typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            // 和 Presentation UI 同一套缩放（提示字号跟分辨率走）；Card 用锚点定位，不受缩放影响
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            instance = go.AddComponent<ScreenVeil>();

            instance.hole = Layer<RawImage>(go.transform, "Hole");
            Material source = Resources.Load<Material>(MaterialPath);
            if (source == null)
            {
                Debug.LogError("[ScreenVeil] 缺少 Resources/" + MaterialPath + ".mat");
            }
            else
            {
                instance.holeMaterial = new Material(source);
                instance.hole.material = instance.holeMaterial;
            }

            instance.card = Layer<RawImage>(go.transform, "Card");
            instance.flash = Layer<Image>(go.transform, "Flash");
            instance.hint = UIFactory.Label(go.transform, "Hint", "", 24, TextAnchor.MiddleCenter, new Color(0.9f, 0.88f, 0.83f));
            RectTransform hr = instance.hint.rectTransform;
            hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(0.5f, 0.08f);
            hr.anchoredPosition = Vector2.zero;
            hr.sizeDelta = new Vector2(900f, 40f);
            instance.hint.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.85f);
            instance.HideAll();
            return instance;
        }

        private static T Layer<T>(Transform parent, string name) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(T));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            T g = go.GetComponent<T>();
            g.raycastTarget = false;
            return g;
        }

        private void HideAll()
        {
            hole.enabled = false;
            card.enabled = false;
            flash.enabled = false;
            hint.text = "";
        }

        private void OnDestroy()
        {
            if (holeMaterial != null)
            {
                Destroy(holeMaterial);
            }
        }

        /// <summary>四周黑幕，洞 = rect（0–1 屏幕坐标），洞外 feather 像素羽化。opacity 0 = 关掉。</summary>
        public static void Hole(Rect rect, float featherPx, float opacity)
        {
            ScreenVeil v = Get();
            v.hole.enabled = opacity > 0.001f && v.holeMaterial != null;
            if (!v.hole.enabled)
            {
                return;
            }

            // 羽化往洞外走：洞本身完全透明
            Vector2 px = new Vector2(Screen.width, Screen.height);
            Vector2 f = new Vector2(featherPx / px.x, featherPx / px.y);
            v.holeMaterial.SetVector(RectId, new Vector4(rect.xMin - f.x, rect.yMin - f.y, rect.xMax + f.x, rect.yMax + f.y));
            v.holeMaterial.SetFloat(FeatherId, featherPx);
            v.holeMaterial.SetVector(ScreenSizeId, px);
            v.holeMaterial.SetColor(ColorId, new Color(0f, 0f, 0f, opacity));
        }

        /// <summary>在 rect 处贴一张图，alpha 0 = 关掉。</summary>
        public static void Card(Texture texture, Rect rect, float alpha, Color tint)
        {
            ScreenVeil v = Get();
            v.card.enabled = alpha > 0.001f && texture != null;
            if (!v.card.enabled)
            {
                return;
            }

            v.card.texture = texture;
            tint.a = alpha;
            v.card.color = tint;
            RectTransform rt = v.card.rectTransform;
            rt.anchorMin = rect.min;
            rt.anchorMax = rect.max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>整屏纯色，alpha 0 = 关掉。</summary>
        public static void Flash(Color color, float alpha)
        {
            ScreenVeil v = Get();
            v.flash.enabled = alpha > 0.001f;
            color.a = alpha;
            v.flash.color = color;
        }

        /// <summary>屏幕底部一行小字操作提示（检视时用），空串 = 关掉。</summary>
        public static void Hint(string text)
        {
            Get().hint.text = text ?? "";
        }

        public static void Clear()
        {
            if (instance != null)
            {
                instance.HideAll();
            }
        }

        /// <summary>renderer 的四个角投到屏幕上的包围矩形（0–1）。任何一角在相机后面返回 false。</summary>
        public static bool ScreenRect(Camera cam, Transform quad, out Rect rect)
        {
            rect = default;
            float xMin = 1e9f, yMin = 1e9f, xMax = -1e9f, yMax = -1e9f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 local = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, 0f);
                Vector3 vp = cam.WorldToViewportPoint(quad.TransformPoint(local));
                if (vp.z <= 0f)
                {
                    return false;
                }

                xMin = Mathf.Min(xMin, vp.x);
                yMin = Mathf.Min(yMin, vp.y);
                xMax = Mathf.Max(xMax, vp.x);
                yMax = Mathf.Max(yMax, vp.y);
            }

            rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return true;
        }
    }
}
