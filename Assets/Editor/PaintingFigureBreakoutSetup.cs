using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ib.Threats;
using Object = UnityEngine.Object;

namespace Ib.EditorTools
{
    /// <summary>
    /// 给二楼两个名画人物布置「破框而出」的现场：在画框位置建（或复用）FrameBreakout_A / FrameBreakout_B 站点，
    /// 绑上那条画的画布板 + 金框组，再把站点挂到对应的 PaintingFigure 上。
    /// 可重复执行（重新生成灰盒、美术交了碎框之后都可以再跑一次）。
    /// 碎框资产在 Assets/Art/Models/Props/BrokenFrame.fbx 时就自动绑上，没有就留空走运行时占位碎框。
    /// </summary>
    public static class PaintingFigureBreakoutSetup
    {
        private const string ScenePath = "Assets/Scenes/Graybox.unity";
        private const string BrokenFramePath = "Assets/Art/Models/Props/BrokenFrame.fbx";
        private const string BrokenFrameAltPath = "Assets/Art/Models/Props/BrokenFrame.blend";
        private const string GlassMaterialPath = "Assets/Art/Materials/M_GB_Glass.mat";

        /// <summary>两只的节奏错开一点，别像同一个人在做同一个动作。</summary>
        private static readonly float[] FigureDelays = { 0.35f, 0.85f };
        private static readonly float[] FrameDelays = { 0.25f, 0.55f };

        [MenuItem("Tools/Ib/布置名画人物破框", false, 21)]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[破框布置] 退出 Play 模式再跑。");
                return;
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (scene.isDirty)
                {
                    Debug.LogError("[破框布置] 当前打开的不是 Graybox.unity，而且有未保存的修改。先保存，再切到 Graybox.unity 重跑。");
                    return;
                }

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                {
                    Debug.LogError("[破框布置] 找不到场景 " + ScenePath);
                    return;
                }

                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            PaintingFigure[] figures = Object.FindObjectsByType<PaintingFigure>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (figures.Length == 0)
            {
                Debug.LogError("[破框布置] 场景里没有 PaintingFigure。");
                return;
            }

            System.Array.Sort(figures, (a, b) => string.CompareOrdinal(a.name, b.name));

            List<GameObject> boards = CollectBoards();
            if (boards.Count == 0)
            {
                Debug.LogError("[破框布置] 没找到 Masterpiece_*_EmptyFrame* 画布板。");
                return;
            }

            GameObject brokenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BrokenFramePath);
            if (brokenPrefab == null)
            {
                brokenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BrokenFrameAltPath);
            }

            Material glass = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
            MeasureBrokenFrame(brokenPrefab);

            int done = 0;
            for (int i = 0; i < figures.Length; i++)
            {
                PaintingFigure figure = figures[i];
                GameObject board = NearestBoard(boards, figure.transform.position);
                if (board == null)
                {
                    Debug.LogWarning("[破框布置] " + figure.name + " 附近没有画布板，跳过。", figure);
                    continue;
                }

                GameObject frame = null;
                Transform boardParent = board.transform.parent;
                if (boardParent != null)
                {
                    Transform found = boardParent.Find(board.name + "_Frame");
                    if (found != null)
                    {
                        frame = found.gameObject;
                    }
                }

                // 站点朝向：局部 +Z 指向展厅（跟美术 BrokenFrame 的「正面朝 +Z」一致）
                Vector3 outward = figure.transform.position - board.transform.position;
                outward.y = 0f;
                if (outward.sqrMagnitude < 0.01f)
                {
                    outward = board.transform.right;
                }

                outward.Normalize();

                string siteName = "FrameBreakout_" + Suffix(board.name, figure.name);
                Transform existing = boardParent != null ? boardParent.Find(siteName) : null;
                GameObject site = existing != null ? existing.gameObject : new GameObject(siteName);
                if (existing == null)
                {
                    site.transform.SetParent(boardParent, false);
                }

                // 碎框 BrokenFrame 的原点在框背面中心（= 几何中心），所以站点就摆在画框中心的原地
                site.transform.position = board.transform.position;
                site.transform.rotation = Quaternion.LookRotation(outward, Vector3.up);
                site.transform.localScale = Vector3.one;

                PaintingBreakout breakout = site.GetComponent<PaintingBreakout>();
                if (breakout == null)
                {
                    breakout = site.AddComponent<PaintingBreakout>();
                }

                SerializedObject siteSo = new SerializedObject(breakout);
                siteSo.FindProperty("intactCanvas").objectReferenceValue = board;
                siteSo.FindProperty("intactFrame").objectReferenceValue = frame;
                siteSo.FindProperty("brokenPrefab").objectReferenceValue = brokenPrefab;
                siteSo.FindProperty("brokenGlassMaterial").objectReferenceValue = glass;
                siteSo.FindProperty("placeholderIfMissing").boolValue = true;
                siteSo.FindProperty("glassVolume").floatValue = 1f;
                siteSo.FindProperty("glassMaxDistance").floatValue = 30f;
                siteSo.FindProperty("delaySeconds").floatValue = FrameDelays[Mathf.Min(i, FrameDelays.Length - 1)];
                siteSo.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject figureSo = new SerializedObject(figure);
                figureSo.FindProperty("breakout").objectReferenceValue = breakout;
                figureSo.FindProperty("breakoutDelay").floatValue = FigureDelays[Mathf.Min(i, FigureDelays.Length - 1)];
                figureSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(figure);
                EditorUtility.SetDirty(site);

                done++;
                Debug.Log("[破框布置] " + figure.name + " ↔ " + siteName + "（画：" + board.name
                    + (frame != null ? "，框：" + frame.name : "，没找到金框组")
                    + "，碎框：" + (brokenPrefab != null ? brokenPrefab.name : "占位") + "）", site);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene))
            {
                Debug.Log("[破框布置] 完成：绑好 " + done + " 个；碎框资产"
                    + (brokenPrefab != null ? "已绑 " + brokenPrefab.name : "还没有，先用运行时占位碎框") + "。");
            }
            else
            {
                Debug.LogWarning("[破框布置] 场景没能保存，手动 Ctrl+S 一下。");
            }
        }

        /// <summary>量一下碎框导入后的真实包围盒：目标宽 1.88、高 2.28（跟程序化画框外框一致），枢轴应在背面中心。</summary>
        private static void MeasureBrokenFrame(GameObject prefab)
        {
            if (prefab == null || prefab.transform.localScale != Vector3.one)
            {
                return;
            }

            GameObject temp = Object.Instantiate(prefab);
            temp.hideFlags = HideFlags.HideAndDontSave;
            temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            Renderer[] renderers = temp.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }

                Vector3 size = bounds.size;
                Vector3 center = bounds.center;   // 相对枢轴
                bool ok = Mathf.Abs(size.x - 1.884f) < 0.03f && Mathf.Abs(size.y - 2.28f) < 0.03f;
                string line = "[破框布置] 碎框实测包围盒 " + size.x.ToString("F3") + " × " + size.y.ToString("F3")
                    + " × " + size.z.ToString("F3") + " m（目标 1.884 × 2.28），枢轴偏差 "
                    + center.x.ToString("F3") + " / " + center.y.ToString("F3") + " / " + center.z.ToString("F3");
                if (ok)
                {
                    Debug.Log(line + " → 尺寸对得上，直接套原画框 transform。");
                }
                else
                {
                    Debug.LogWarning(line + " → 尺寸不对，去 Importer 的 Scale Factor 调，别在场景里缩 transform。");
                }
            }

            Object.DestroyImmediate(temp);
        }

        /// <summary>画布板 = 名字带 EmptyFrame 但不是 _Frame 的金框组。</summary>
        private static List<GameObject> CollectBoards()
        {
            List<GameObject> boards = new List<GameObject>();
            GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name;
                if (!n.StartsWith("Masterpiece_") || !n.Contains("EmptyFrame") || n.EndsWith("_Frame"))
                {
                    continue;
                }

                if (all[i].transform.childCount > 0)
                {
                    continue;   // _Frame 组本身也带子物体，这里是保险
                }

                boards.Add(all[i]);
            }

            return boards;
        }

        private static GameObject NearestBoard(List<GameObject> boards, Vector3 from)
        {
            GameObject best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < boards.Count; i++)
            {
                Vector3 p = boards[i].transform.position;
                float dx = p.x - from.x;
                float dz = p.z - from.z;
                float d = dx * dx + dz * dz;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = boards[i];
                }
            }

            return best;
        }

        /// <summary>Masterpiece_W1_EmptyFrameA → A；取不到就用人物名字的最后一段。</summary>
        private static string Suffix(string boardName, string figureName)
        {
            char last = boardName[boardName.Length - 1];
            if (char.IsLetterOrDigit(last))
            {
                return last.ToString();
            }

            int underscore = figureName.LastIndexOf('_');
            return underscore >= 0 && underscore < figureName.Length - 1 ? figureName.Substring(underscore + 1) : "X";
        }
    }
}
