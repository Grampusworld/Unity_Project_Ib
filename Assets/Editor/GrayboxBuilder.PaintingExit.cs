using Ib.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Ib.EditorTools
{
    /// <summary>
    /// 出画演出（GDD §3.2 第五段 / §3.6 第 6–6b 步 / R4-3，老大 2026-11-01）：
    /// 1. 黑屋空画框（EmptyFrame_SecondVisit）里一块门洞 PortalWindow：实时画面拍真实馆全家福厅，冷月光色；
    /// 2. 全家福厅北墙画框（Painting_TheMuseum_ExitFrame）前面两块：门洞 PortalWindow（回看黑屋）和《美术馆》画面 TheGallery_Canvas，
    ///    画框上挂 TheGalleryFrame 管"盖布 → 门洞 → 画"；两块门洞互为 partner；
    /// 3. 全家福厅一盏冷月光（只在黑屋二次时亮，透过黑屋画框看得见）。
    /// 按名字找、先删后建，可重复执行；生成灰盒的最后一步也会调用。《美术馆》贴图由 Tools/gen_the_gallery.py 生成。
    /// </summary>
    public static partial class GrayboxBuilder
    {
        private const string TheGalleryTexture = "Assets/Art/Textures/Paintings/TheGallery.png";
        private const string PortalMaterialPath = "Assets/Art/Materials/Finish/M_PaintingPortal.mat";
        private const string PortalWindowName = "PortalWindow";
        private const string TheGalleryName = "TheGallery";

        [MenuItem("Tools/Ib/布置出画演出（门洞 + 《美术馆》）")]
        private static void SetupPaintingExitMenu()
        {
            SetupPaintingExit();
        }

        internal static void SetupPaintingExit(bool save = true)
        {
            Scene scene = SceneManager.GetActiveScene();
            LoadFinishes();

            Transform blackRoom = FindRoot(scene, "Level_BlackRoom");
            Transform props = FindRoot(scene, "Props_Museum");
            Transform emptyFrame = blackRoom != null ? FindDeep(blackRoom, "EmptyFrame_SecondVisit") : null;
            Transform exit = props != null ? FindDeep(props, "Painting_TheMuseum_ExitFrame") : null;
            if (emptyFrame == null || exit == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 找不到 EmptyFrame_SecondVisit 或 Painting_TheMuseum_ExitFrame，跳过出画演出。");
                return;
            }

            Material portal = PortalMaterial();

            // ---- 黑屋空画框：框内开口 = 四根框条之间
            Remove(emptyFrame, PortalWindowName);
            Bounds left = FindDeep(emptyFrame, "Frame_Left").GetComponent<Renderer>().bounds;
            Bounds right = FindDeep(emptyFrame, "Frame_Right").GetComponent<Renderer>().bounds;
            Bounds top = FindDeep(emptyFrame, "Frame_Top").GetComponent<Renderer>().bounds;
            Bounds bottom = FindDeep(emptyFrame, "Frame_Bottom").GetComponent<Renderer>().bounds;
            Vector3 blackCenter = new Vector3((left.max.x + right.min.x) * 0.5f, (bottom.max.y + top.min.y) * 0.5f, left.center.z);
            Vector2 blackSize = new Vector2(right.min.x - left.max.x, top.min.y - bottom.max.y);
            GameObject blackWindow = WorldQuad(emptyFrame, PortalWindowName, blackCenter, blackSize, portal);
            PaintingPortal blackPortal = blackWindow.AddComponent<PaintingPortal>();

            // ---- 北墙画框：门洞和画面都贴在画板正面（朝 −Z）外 5 mm
            Remove(exit.parent, TheGalleryName);
            Bounds board = exit.GetComponent<Renderer>().bounds;
            Transform gallery = NewGroup(exit.parent, TheGalleryName);
            Vector3 face = new Vector3(board.center.x, board.center.y, board.min.z - 0.005f);
            Vector2 faceSize = new Vector2(board.size.x, board.size.y);
            GameObject exitWindow = WorldQuad(gallery, PortalWindowName, face, faceSize, portal);
            PaintingPortal exitPortal = exitWindow.AddComponent<PaintingPortal>();
            GameObject canvas = WorldQuad(gallery, "TheGallery_Canvas", face, faceSize, TheGalleryMaterial());

            SetRef(blackPortal, "partner", exitPortal);
            // 回看黑屋：黑屋有自己的暖光，用中性色、不额外提亮
            SerializedObject so = new SerializedObject(exitPortal);
            so.FindProperty("tint").colorValue = new Color(0.92f, 0.92f, 0.95f);
            so.FindProperty("exposure").floatValue = 1.6f;
            so.ApplyModifiedPropertiesWithoutUndo();
            SetRef(exitPortal, "partner", blackPortal);

            // ---- 冷月光：画框前 6 m、离地 3 m，照着厅里朝南的一半（黑屋那头馆灯是关的，全靠它）
            GameObject moon = new GameObject("Moonlight_PortalView");
            moon.transform.SetParent(gallery, false);
            moon.transform.position = new Vector3(board.center.x, board.min.y + 3f, board.min.z - 6f);
            Light light = moon.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.55f, 0.66f, 0.95f);
            light.intensity = 30f;
            light.range = 22f;
            light.shadows = LightShadows.None;
            light.enabled = false;

            TheGalleryFrame frame = exit.GetComponent<TheGalleryFrame>();
            if (frame == null)
            {
                frame = exit.gameObject.AddComponent<TheGalleryFrame>();
            }

            Transform cloth = FindDeep(exit.parent, "DustCloth");
            SetRef(frame, "cloth", cloth != null ? cloth.gameObject : null);
            SetRef(frame, "window", exitPortal);
            SetRef(frame, "painting", canvas);
            SetRef(frame, "moonlight", light);

            // 默认（画中馆）：盖布在、门洞和画都关着，运行时 TheGalleryFrame 按状态切
            exitWindow.SetActive(false);
            canvas.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            if (save)
            {
                EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[GrayboxBuilder] 出画演出：黑屋门洞 " + blackSize.ToString("F2") + "，北墙画框 " + faceSize.ToString("F2") + "。");
        }

        private static void Remove(Transform parent, string name)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            {
                if (t != parent && t.name == name && t.parent == parent)
                {
                    Object.DestroyImmediate(t.gameObject);
                    return;
                }
            }
        }

        /// <summary>按世界坐标摆一块不投影、没碰撞的 Quad（可见面朝 −Z），父节点带缩放也不影响。</summary>
        private static GameObject WorldQuad(Transform parent, string name, Vector3 center, Vector2 size, Material mat)
        {
            GameObject go = AddQuad(parent, name, Vector3.zero, Vector2.one, mat);
            RemoveCollider(go);
            go.transform.SetPositionAndRotation(center, Quaternion.identity);
            Vector3 s = parent.lossyScale;
            go.transform.localScale = new Vector3(size.x / s.x, size.y / s.y, 1f / s.z);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private static Material PortalMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(PortalMaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Ib/PaintingPortal")) { name = "M_PaintingPortal" };
                AssetDatabase.CreateAsset(mat, PortalMaterialPath);
            }

            return mat;
        }

        private static Material TheGalleryMaterial()
        {
            TextureImporter ti = AssetImporter.GetAtPath(TheGalleryTexture) as TextureImporter;
            if (ti != null && (ti.wrapMode != TextureWrapMode.Clamp || ti.maxTextureSize != 2048))
            {
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = 2048;
                ti.SaveAndReimport();
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TheGalleryTexture);
            if (tex == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 找不到《美术馆》贴图 " + TheGalleryTexture + "（Tools/gen_the_gallery.py 生成），先用画布色。");
            }

            Material mat = Lit("M_TheGallery", tex, null, Color.white, 0.12f, 1f, 0f, false);
            mat.SetTexture("_BaseMap", tex);
            mat.mainTextureScale = Vector2.one;
            return mat;
        }
    }
}
