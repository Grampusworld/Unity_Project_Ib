using UnityEngine;

namespace Ib.Environment
{
    /// <summary>
    /// 3D 文字用自己的材质（Ib/Text3D，带深度测试，不穿墙）时，材质的贴图必须是字体当前的动态贴图。
    /// 内置字体的贴图会在字符集变化时重建，这里在启用时和每次重建后把贴图重新挂上。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TextMesh))]
    public sealed class Text3DFont : MonoBehaviour
    {
        private TextMesh text;
        private MeshRenderer meshRenderer;

        private void OnEnable()
        {
            text = GetComponent<TextMesh>();
            meshRenderer = GetComponent<MeshRenderer>();
            Font.textureRebuilt += OnTextureRebuilt;
            Sync();
        }

        private void OnDisable()
        {
            Font.textureRebuilt -= OnTextureRebuilt;
        }

        private void OnTextureRebuilt(Font font)
        {
            if (text != null && font == text.font)
            {
                Sync();
            }
        }

        private void Sync()
        {
            if (text == null || text.font == null || meshRenderer == null || meshRenderer.sharedMaterial == null)
            {
                return;
            }

            Texture fontTexture = text.font.material.mainTexture;
            if (meshRenderer.sharedMaterial.mainTexture != fontTexture)
            {
                meshRenderer.sharedMaterial.mainTexture = fontTexture;
            }
        }
    }
}
