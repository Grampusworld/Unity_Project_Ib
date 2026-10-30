using Ib.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace Ib.UI
{
    /// <summary>
    /// 屏幕正中的白色小点。跟着 GameFlow.IsAiming 开关：
    /// 主菜单、暂停、Game Over、读纸面时不画（那些界面盖住画面中心）。
    /// </summary>
    internal sealed class Crosshair : MonoBehaviour
    {
        // 参考分辨率 1920×1080 下，白点本身大约 6 px，外圈一环深色，亮背景上也不会丢
        private const float Size = 11f / PresentationRoot.UIScale;

        private Image image;
        private static Sprite dotSprite;

        public static Crosshair Create(Transform parent)
        {
            var go = new GameObject("Crosshair", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Size, Size);
            rt.anchoredPosition = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.sprite = DotSprite();
            image.color = Color.white;
            image.raycastTarget = false;
            image.maskable = false;

            var crosshair = go.AddComponent<Crosshair>();
            crosshair.image = image;
            return crosshair;
        }

        private void LateUpdate()
        {
            bool show = GameFlow.IsAiming;
            if (image.enabled != show)
            {
                image.enabled = show;
            }
        }

        private static Sprite DotSprite()
        {
            if (dotSprite != null)
            {
                return dotSprite;
            }

            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                name = "CrosshairDot",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color[n * n];
            float center = (n - 1) * 0.5f;
            const float whiteR = 9.5f;
            const float ringR = 12.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float white = Mathf.Clamp01(whiteR - d + 0.75f);
                    float ring = Mathf.Clamp01(ringR - d + 0.75f) * (1f - white);
                    float ringA = ring * 0.9f;
                    float a = white + ringA * (1f - white);
                    float rgb = a > 1e-4f ? white / a : 0f;
                    pixels[y * n + x] = new Color(rgb, rgb, rgb, a);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            dotSprite = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), n);
            dotSprite.hideFlags = HideFlags.HideAndDontSave;
            return dotSprite;
        }
    }
}
