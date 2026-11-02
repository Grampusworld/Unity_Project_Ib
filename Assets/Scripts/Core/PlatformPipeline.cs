using UnityEngine;
using UnityEngine.Rendering;

namespace Ib.Core
{
    /// <summary>
    /// WebGL 换一套便宜一档的管线资产（<c>Resources/WebGL_RPAsset</c>）：阴影距离 50 → 25 m、级联 4 → 2、
    /// 主光阴影图 2048 → 1024、附加光阴影图 2048 → 512、软阴影关掉、MSAA 关掉。桌面端不受影响。
    /// 浏览器里瓶颈在静态合批和每帧阴影重绘，这里只压 GPU 端开销，不动场景和灯光布置。
    /// </summary>
    public static class PlatformPipeline
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                return;
            }

            RenderPipelineAsset webgl = Resources.Load<RenderPipelineAsset>("WebGL_RPAsset");
            if (webgl == null)
            {
                Debug.LogWarning("[PlatformPipeline] 找不到 Resources/WebGL_RPAsset，WebGL 继续用桌面管线。");
                return;
            }

            QualitySettings.renderPipeline = webgl;
        }
    }
}
