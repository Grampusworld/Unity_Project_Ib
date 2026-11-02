using System.IO;
using UnityEditor;

namespace Ib.EditorTools
{
    /// <summary>
    /// 真结局 CG 图：Assets/Art/Incoming/画作/T_CG_TrueEnd_0N.* 一导入就拷到 Resources/CG/TrueEnd/（TrueEndingCG 运行时按名加载，
    /// 缺图用运行时占位）。也可手动跑菜单 Ib/Sync True Ending CG。
    /// </summary>
    internal sealed class TrueEndingCGImport : AssetPostprocessor
    {
        private const string Source = "Assets/Art/Incoming/画作";
        private const string Target = "Assets/Resources/CG/TrueEnd";

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string p in imported)
            {
                if (p.StartsWith(Source + "/") && Path.GetFileName(p).StartsWith("T_CG_TrueEnd_") && !p.EndsWith(".meta"))
                {
                    EditorApplication.delayCall += Sync;
                    return;
                }
            }
        }

        [MenuItem("Ib/Sync True Ending CG")]
        public static void Sync()
        {
            if (!AssetDatabase.IsValidFolder(Source)) return;
            foreach (string guid in AssetDatabase.FindAssets("T_CG_TrueEnd_ t:Texture2D", new[] { Source }))
            {
                string src = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.IsValidFolder("Assets/Resources/CG")) AssetDatabase.CreateFolder("Assets/Resources", "CG");
                if (!AssetDatabase.IsValidFolder(Target)) AssetDatabase.CreateFolder("Assets/Resources/CG", "TrueEnd");
                string dst = Target + "/" + Path.GetFileName(src);
                if (File.Exists(dst) && File.GetLastWriteTimeUtc(dst) >= File.GetLastWriteTimeUtc(src)) continue;
                AssetDatabase.DeleteAsset(dst);
                AssetDatabase.CopyAsset(src, dst);
            }
        }
    }
}
