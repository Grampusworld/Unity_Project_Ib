using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Ib.EditorTools
{
    /// <summary>
    /// 2026-11-01 老大的三条需求里美术的部分（Ib 美术）：
    /// 1. 二楼值班室门口 Sketch_LilyLooksAtDoor 换成老大给的 Lily 肖像（Assets/Art/Textures/Paintings/Source/Sister_Original.png
    ///    以小女孩为中心裁成 3:4，Tools/gen_lily_portrait.py 生成），拆三层给"眼睛跟着玩家"用：
    ///    画布（眼裂抠空，Alpha Clip）→ 虹膜 Iris_L / Iris_R（脚本平移）→ 眼白底图。说明见 Docs/LilyPortrait_Eyes.md。
    /// 2. 坏结局的"库房 07"（GDD §6.9）：正门背后、黑屋那块地方另起一个根 Level_Storage07，默认隐藏，
    ///    结局 CG 开门前由 Ib 技术打开（同时保证 Level_BlackRoom 是关着的，两者占同一块地）。
    /// 都按名字找、先删后建，可重复执行；生成灰盒的最后一步也会调用。
    /// </summary>
    public static partial class GrayboxBuilder
    {
        private const string LilyTexDir = "Assets/Art/Textures/Paintings/";
        private const string LilyMatDir = "Assets/Art/Materials/Paintings/";
        private const string LilyBoard = "Sketch_LilyLooksAtDoor";
        private const string LilyGroup = "Sketch_LilyLooksAtDoor_Portrait";
        public const string Storage07Root = "Level_Storage07";

        // 裁好的画布 540x720（原图像素）里虹膜中心和直径，见 Tools/gen_lily_portrait.py
        private static readonly Vector2 IrisL = new Vector2(255f, 196f);
        private static readonly Vector2 IrisR = new Vector2(343f, 214.5f);
        private const float IrisLDiameterPx = 27f;
        private const float IrisRDiameterPx = 25f;
        private const float CanvasPxW = 540f;
        private const float CanvasPxH = 720f;

        [MenuItem("Tools/Ib/布置结局美术（Lily 肖像眼睛 + 库房 07）")]
        private static void SetupEndingArtMenu()
        {
            SetupEndingArt();
        }

        internal static void SetupEndingArt(bool save = true)
        {
            Scene scene = SceneManager.GetActiveScene();
            LoadFinishes();

            bool portrait = SetupLilyPortrait(scene);
            bool storage = BuildStorage07(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            if (save)
            {
                EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[GrayboxBuilder] 结局美术：Lily 肖像 {(portrait ? "OK" : "跳过")}，库房 07 {(storage ? "OK" : "跳过")}。");
        }

        // ================================================================ Lily 肖像

        private static bool SetupLilyPortrait(Scene scene)
        {
            Transform props = FindRoot(scene, "Props_Museum");
            Transform board = props != null ? FindDeep(props, LilyBoard) : null;
            if (board == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 找不到 " + LilyBoard + "，跳过 Lily 肖像。");
                return false;
            }

            Transform old = FindDeep(board.parent, LilyGroup);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            Material canvas = LilyMaterial("M_LilyPortrait_Canvas", "LilyPortrait_Canvas.png", true);
            Material back = LilyMaterial("M_LilyPortrait_EyeBack", "LilyPortrait_EyeBack.png", false);
            Material irisL = LilyMaterial("M_LilyPortrait_Iris_L", "LilyPortrait_Iris_L.png", true);
            Material irisR = LilyMaterial("M_LilyPortrait_Iris_R", "LilyPortrait_Iris_R.png", true);
            if (canvas == null || back == null || irisL == null || irisR == null)
            {
                return false;
            }

            // 板子本身留着（画框按它围的），只把正面材质也换成画布，免得侧面露出旧速写
            Renderer boardRenderer = board.GetComponent<Renderer>();
            Bounds b = boardRenderer.bounds;
            Vector3 normal = Vector3.right;                  // 挂在长廊西墙，正面朝 +X（CollectPanelExhibits 表里同一个方向）
            float w = b.size.z;
            float h = b.size.y;
            float s = h / 0.8f;                              // 生成时板子 0.6 × 0.8，放大后 = 1.4
            Vector3 face = b.center + normal * b.extents.x;

            // 组：本地 +Z 朝墙里（−normal），+X = 观众的右手 = 画面右边，+Y 向上；本地 1 单位 = 1 米
            GameObject g = new GameObject(LilyGroup);
            g.transform.SetParent(board.parent, false);
            g.transform.SetPositionAndRotation(face, Quaternion.LookRotation(-normal, Vector3.up));
            g.transform.localScale = Vector3.one / board.parent.lossyScale.x;

            // 三层的间距（米）：画布在最前，虹膜夹在中间，眼白在最后；都在画框正面以内（画框比板面凸出 2 cm × s）
            float zCanvas = -0.012f * s;
            float zIris = -0.008f * s;
            float zBack = -0.004f * s;

            LayerQuad(g.transform, "Canvas", new Vector3(0f, 0f, zCanvas), new Vector2(w, h), canvas);
            LayerQuad(g.transform, "EyeBack", new Vector3(0f, 0f, zBack), new Vector2(w, h), back);
            LayerQuad(g.transform, "Iris_L", IrisLocal(IrisL, w, h, zIris), Vector2.one * (IrisLDiameterPx / CanvasPxW * w), irisL);
            LayerQuad(g.transform, "Iris_R", IrisLocal(IrisR, w, h, zIris), Vector2.one * (IrisRDiameterPx / CanvasPxW * w), irisR);
            return true;
        }

        private static Vector3 IrisLocal(Vector2 px, float w, float h, float z)
        {
            return new Vector3((px.x / CanvasPxW - 0.5f) * w, (0.5f - px.y / CanvasPxH) * h, z);
        }

        private static void LayerQuad(Transform parent, string name, Vector3 local, Vector2 size, Material mat)
        {
            GameObject go = AddQuad(parent, name, local, size, mat);
            RemoveCollider(go);
            MeshRenderer r = go.GetComponent<MeshRenderer>();
            r.receiveShadows = true;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static Material LilyMaterial(string name, string texture, bool alphaClip)
        {
            string texPath = LilyTexDir + texture;
            TextureImporter ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (ti == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 找不到贴图 " + texPath);
                return null;
            }

            bool dirty = false;
            if (ti.wrapMode != TextureWrapMode.Clamp)
            {
                ti.wrapMode = TextureWrapMode.Clamp;
                dirty = true;
            }

            if (alphaClip && !ti.alphaIsTransparency)
            {
                ti.alphaIsTransparency = true;
                dirty = true;
            }

            if (ti.maxTextureSize != 2048)
            {
                ti.maxTextureSize = 2048;
                dirty = true;
            }

            if (dirty)
            {
                ti.SaveAndReimport();
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            string path = LilyMatDir + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.22f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
            mat.SetFloat("_Cutoff", 0.5f);
            if (alphaClip)
            {
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)RenderQueue.AlphaTest;
                mat.SetOverrideTag("RenderType", "TransparentCutout");
            }
            else
            {
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.renderQueue = -1;
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ================================================================ 库房 07（坏结局）

        // 生成坐标（× WorldScale 之前）。北墙 = 美术馆南墙（正门所在），门厅往南 9 m，临街外门（卷帘）在南墙正中
        private const float StorageDepth = 9f;
        private const float StorageHalfWidth = 5f;
        private const float StorageHeight = 4.2f;
        private const float StorageAisleHalf = 1.2f;      // 正门正对的通道留空：镜头往里推 2–3 m 不撞箱子
        private const float ShutterWidth = 4f;
        private const float ShutterHeight = 3.2f;

        private static bool BuildStorage07(Scene scene)
        {
            Transform old = FindRoot(scene, Storage07Root);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            float cx = MainDoorX;
            float north = -HalfWall - 0.01f;                  // 美术馆南墙外表面再往外 1 cm（黑屋的黑皮在 0–2 cm，两者不同时开）
            float south = -StorageDepth;
            float west = cx - StorageHalfWidth;
            float east = cx + StorageHalfWidth;
            float h = StorageHeight;

            Material wall = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/M_Wall_Concrete.mat") ?? Finishes.PlasterBase;
            Material floor = Finishes.StoneTile;
            Material ceiling = Finishes.Ceiling;
            Material wood = Finishes.CrateWood;
            Material metal = Finishes.MetalDark;
            Material cloth = Finishes.Canvas;
            Material plate = Finishes.Label;

            // 在单位缩放下搭，最后整根放大 WorldScale（和 ApplyWorldScale 一样，灯的距离/强度也要跟着放大）
            GameObject rootGo = new GameObject(Storage07Root);
            SceneManager.MoveGameObjectToScene(rootGo, scene);
            Transform root = rootGo.transform;

            // ---- 壳：地面 / 天花 / 三面墙 + 北墙内皮（门洞留空） ----
            Transform shell = NewGroup(root, "Shell");
            const float t = 0.2f;
            AddSlab(shell, "Floor", west - t, east + t, south - t, north, -0.3f, 0.004f, floor);
            AddSlab(shell, "Ceiling", west - t, east + t, south - t, north, h, h + 0.3f, ceiling);
            AddSlab(shell, "Wall_West", west - t, west, south - t, north, 0f, h, wall);
            AddSlab(shell, "Wall_East", east, east + t, south - t, north, 0f, h, wall);
            float shW = cx - ShutterWidth * 0.5f;
            float shE = cx + ShutterWidth * 0.5f;
            AddSlab(shell, "Wall_South_W", west, shW, south - t, south, 0f, h, wall);
            AddSlab(shell, "Wall_South_E", shE, east, south - t, south, 0f, h, wall);
            AddSlab(shell, "Wall_South_Lintel", shW, shE, south - t, south, ShutterHeight, h, wall);
            AddSlab(shell, "NorthSkin_West", west, MainDoor.Min, north - 0.02f, north, 0f, h, wall);
            AddSlab(shell, "NorthSkin_East", MainDoor.Max, east, north - 0.02f, north, 0f, h, wall);
            AddSlab(shell, "NorthSkin_Lintel", MainDoor.Min, MainDoor.Max, north - 0.02f, north, MainDoorHeight, h, wall);
            // 踢脚：深色一圈，让墙地分界在暗处也读得出
            AddSlab(shell, "Skirting_W", west, west + 0.02f, south, north - 0.02f, 0f, 0.15f, metal);
            AddSlab(shell, "Skirting_E", east - 0.02f, east, south, north - 0.02f, 0f, 0.15f, metal);

            // ---- 临街外门：拉下的卷帘（横向波纹板）+ 卷轴箱 + 两侧导轨，门缝底下漏一线街灯冷光 ----
            Transform shutter = NewGroup(root, "Shutter_Street");
            float sz = south + 0.08f;                         // 卷帘面在墙内 8 cm（屋里一侧）
            int slats = Mathf.RoundToInt((ShutterHeight - 0.03f) / 0.1f);
            for (int i = 0; i < slats; i++)
            {
                float y0 = 0.03f + i * 0.1f;
                RemoveCollider(AddSlab(shutter, "Slat_" + i, shW, shE, sz - 0.025f, sz + 0.025f, y0, y0 + 0.085f, metal));
                RemoveCollider(AddSlab(shutter, "Groove_" + i, shW, shE, sz + 0.02f, sz + 0.035f, y0 + 0.085f, y0 + 0.1f, metal));
            }

            AddSlab(shutter, "Rail_W", shW - 0.08f, shW, south, sz + 0.06f, 0f, ShutterHeight, metal);
            AddSlab(shutter, "Rail_E", shE, shE + 0.08f, south, sz + 0.06f, 0f, ShutterHeight, metal);
            AddSlab(shutter, "Housing", shW - 0.12f, shE + 0.12f, south, south + 0.43f, ShutterHeight, ShutterHeight + 0.4f, metal);
            AddSlab(shutter, "BottomBar", shW, shE, sz - 0.04f, sz + 0.04f, 0.03f, 0.08f, Finishes.WarningYellow);
            GameObject leak = AddQuad(shutter, "StreetLight_Leak", new Vector3(cx, 0.015f, sz + 0.04f), new Vector2(ShutterWidth - 0.1f, 0.025f),
                Emissive("M_StreetLeak_Cold", new Color(0.55f, 0.68f, 0.95f), 2.5f));
            leak.transform.localRotation = Quaternion.LookRotation(Vector3.back);    // 正面朝屋里（北）
            RemoveCollider(leak);

            // ---- 门牌 07：卷帘上方，正对正门；字是黑色 3D 字，牌子是旧奶白 ----
            Transform sign = NewGroup(root, "Sign_07", new Vector3(cx, ShutterHeight + 0.68f, south + 0.02f), 0f);   // 本地 +Z 朝北（屋里）
            AddBox(sign, "Plate", new Vector3(0f, 0f, 0f), new Vector3(1.3f, 0.4f, 0.03f), plate);
            Label3D(sign, "Text_07", new Vector3(0f, 0.03f, 0.02f), "07", 0.2f, new Color(0.08f, 0.08f, 0.08f));
            Label3D(sign, "Text_Storage", new Vector3(0f, -0.13f, 0.02f), "TEMPORARY STORAGE", 0.045f, new Color(0.12f, 0.12f, 0.12f));

            // ---- 木箱：两侧沿墙堆满，靠通道的一排矮一些，留出镜头往里看的纵深 ----
            Transform crates = NewGroup(root, "Crates");
            Random.State saved = Random.state;
            Random.InitState(707);
            int n = 0;
            for (int side = -1; side <= 1; side += 2)
            {
                float aisle = cx + side * (StorageAisleHalf + 0.15f);
                float wallX = side < 0 ? west + 0.1f : east - 0.1f;
                for (float z = north - 1.1f; z > south + 1.2f; z -= Random.Range(1.05f, 1.35f))
                {
                    // 每一段：靠墙一垛高（2–3 层），靠通道一垛矮（1–2 层），中间偶尔夹一块托盘
                    StackCrates(crates, ref n, Mathf.Lerp(wallX, aisle, 0.22f), z, Random.Range(2, 4), side, wood);
                    StackCrates(crates, ref n, Mathf.Lerp(wallX, aisle, 0.68f), z + Random.Range(-0.25f, 0.25f), Random.Range(1, 3), side, wood);
                }
            }

            // 通道尽头（卷帘前 1.5 m）横一排矮箱，让推镜头时画面中景也有东西
            for (int i = 0; i < 3; i++)
            {
                float x = cx - 1.0f + i * 1.0f;
                Crate(crates, "Crate_End_" + i, x, 0f, south + 1.4f + Random.Range(-0.15f, 0.15f), Random.Range(0.6f, 0.8f), Random.Range(-10f, 10f));
                StencilOnCrate(crates.GetChild(crates.childCount - 1));
            }

            // ---- 待装箱的展品：几只扁平画箱靠墙斜放，两件盖着防尘布的雕像 ----
            Transform goods = NewGroup(root, "Exhibits_Packed");
            PaintingCrate(goods, "PaintingCrate_0", new Vector3(west + 0.35f, 0f, south + 0.95f), 90f, 1.6f, 1.2f, wood);
            PaintingCrate(goods, "PaintingCrate_1", new Vector3(west + 0.55f, 0f, south + 1.0f), 90f, 1.2f, 0.9f, wood);
            PaintingCrate(goods, "PaintingCrate_2", new Vector3(east - 0.35f, 0f, south + 1.05f), -90f, 1.8f, 1.4f, wood);
            ShroudedStatue(goods, "Shrouded_0", new Vector3(cx - StorageAisleHalf - 0.2f, 0f, south + 2.6f), 0.7f, 1.9f, cloth);
            ShroudedStatue(goods, "Shrouded_1", new Vector3(cx + StorageAisleHalf + 0.35f, 0f, south + 3.1f), 0.6f, 1.5f, cloth);
            Random.state = saved;

            // ---- 灯：一根冷白灯管吊在通道正上方（昏暗），卷帘缝下一盏贴地的冷蓝街灯漏光，门口一盏很弱的补光 ----
            Transform lights = NewGroup(root, "Lights_Storage07");
            Vector3 tube = new Vector3(cx, h - 0.45f, -4.2f);
            AddBox(lights, "Tube_Fixture", tube + Vector3.up * 0.06f, new Vector3(0.14f, 0.05f, 1.25f), metal);
            RemoveCollider(AddBox(lights, "Tube_Glow", tube, new Vector3(0.05f, 0.04f, 1.2f),
                Emissive("M_FluoTube_Cold", new Color(0.78f, 0.88f, 1f), 1.6f)));
            RemoveCollider(AddBox(lights, "Tube_Chain_A", tube + new Vector3(0f, 0.25f, 0.5f), new Vector3(0.01f, 0.4f, 0.01f), metal));
            RemoveCollider(AddBox(lights, "Tube_Chain_B", tube + new Vector3(0f, 0.25f, -0.5f), new Vector3(0.01f, 0.4f, 0.01f), metal));
            AddSpotLight(lights, "Spot_Tube", tube - Vector3.up * 0.05f, new Vector3(cx, 0f, -4.6f),
                new Color(0.74f, 0.84f, 1f), 3.2f, 8f, 120f, true);
            AddSpotLight(lights, "Spot_StreetLeak", new Vector3(cx, 0.06f, sz + 0.12f), new Vector3(cx, 0f, -2.5f),
                new Color(0.50f, 0.62f, 0.95f), 2.2f, 9f, 70f, false);
            AddPointLight(lights, "Fill_Doorway", new Vector3(cx, 2.4f, -1.2f), new Color(0.62f, 0.68f, 0.80f), 0.35f, 5f, false, false);

            MarkStatic(shell);    // 箱子上有 TextMesh（运行时重建网格），不进静态合批

            // 放大到世界尺寸（和 ApplyWorldScale 一致）
            const float s = WorldScale;
            root.localScale = Vector3.one * s;
            foreach (Light l in root.GetComponentsInChildren<Light>(true))
            {
                l.range *= s;
                l.intensity *= s * s;
            }

            rootGo.SetActive(false);
            return true;
        }

        private static void StackCrates(Transform parent, ref int n, float x, float z, int layers, int side, Material wood)
        {
            float y = 0f;
            if (Random.value < 0.4f)
            {
                // 托盘：一块板 + 三根垫木
                Transform p = NewGroup(parent, "Pallet_" + n, new Vector3(x, 0f, z), Random.Range(-4f, 4f));
                AddBox(p, "Deck", new Vector3(0f, 0.12f, 0f), new Vector3(1.0f, 0.03f, 1.0f), wood);
                for (int i = 0; i < 3; i++)
                {
                    AddBox(p, "Runner_" + i, new Vector3(-0.42f + i * 0.42f, 0.05f, 0f), new Vector3(0.1f, 0.1f, 1.0f), wood);
                }

                y = 0.135f;
            }

            float size = Random.Range(0.75f, 0.95f);
            for (int l = 0; l < layers; l++)
            {
                float jitterX = Random.Range(-0.06f, 0.06f);
                float jitterZ = Random.Range(-0.06f, 0.06f);
                Crate(parent, "Crate_" + n + "_" + l, x + jitterX, y, z + jitterZ, size, Random.Range(-7f, 7f));
                if (l == 0 || Random.value < 0.5f)
                {
                    StencilOnCrate(parent.GetChild(parent.childCount - 1), side);
                }

                y += size;
                size *= Random.Range(0.78f, 0.95f);
            }

            n++;
        }

        /// <summary>箱子朝通道的一面喷个"07"（side：−1 西侧一排 → 朝 +X；+1 东侧 → 朝 −X；0 → 朝北）。</summary>
        private static void StencilOnCrate(Transform crate, int side = 0)
        {
            Transform box = crate.Find("Box");
            if (box == null)
            {
                return;
            }

            float size = box.localScale.x;
            float yaw = side < 0 ? 90f : side > 0 ? -90f : 0f;        // 本地 +Z 转到朝外
            Transform g = NewGroup(crate, "Stencil", Vector3.zero, yaw);
            Label3D(g, "Text", new Vector3(0f, size * 0.5f, size * 0.5f + 0.004f), "07", size * 0.28f, new Color(0.10f, 0.09f, 0.08f));
        }

        /// <summary>扁平画箱：斜靠墙（前倾 8°），yaw 让本地 +Z 背对墙。</summary>
        private static void PaintingCrate(Transform parent, string name, Vector3 basePos, float yaw, float width, float height, Material wood)
        {
            Transform g = NewGroup(parent, name, basePos, yaw);
            g.localRotation *= Quaternion.Euler(-8f, 0f, 0f);
            AddBox(g, "Box", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, 0.18f), wood);
            RemoveCollider(AddBox(g, "Batten_H", new Vector3(0f, height * 0.5f, 0.095f), new Vector3(width, 0.08f, 0.02f), wood));
            RemoveCollider(AddBox(g, "Batten_D", new Vector3(0f, height * 0.5f, 0.1f), new Vector3(0.08f, height, 0.02f), wood));
            Transform st = NewGroup(g, "Stencil", Vector3.zero, 0f);
            Label3D(st, "Text", new Vector3(width * 0.3f, height * 0.75f, 0.115f), "07", 0.12f, new Color(0.10f, 0.09f, 0.08f));
        }

        /// <summary>盖防尘布的雕像：一根矮柱 + 比它大一圈的布罩 + 底下堆起的布褶。</summary>
        private static void ShroudedStatue(Transform parent, string name, Vector3 basePos, float radius, float height, Material cloth)
        {
            Transform g = NewGroup(parent, name, basePos, Random.Range(0f, 360f));
            AddBox(g, "Core", new Vector3(0f, height * 0.45f, 0f), new Vector3(radius * 0.8f, height * 0.9f, radius * 0.8f), cloth);
            RemoveCollider(AddBox(g, "Head", new Vector3(0.05f, height * 0.93f, 0f), new Vector3(radius * 0.55f, height * 0.16f, radius * 0.5f), cloth));
            GameObject skirt = AddBox(g, "Skirt", new Vector3(0f, 0.12f, 0f), new Vector3(radius * 1.2f, 0.24f, radius * 1.15f), cloth);
            skirt.transform.localRotation = Quaternion.Euler(0f, 17f, 0f);
            RemoveCollider(skirt);
        }
    }
}

namespace Ib.EditorTools
{
    /// <summary>
    /// 一次性：脚本第一次编译进来时，如果打开的是 Graybox 场景，就自动跑一遍"布置结局美术"
    /// （2026-11-01 Ib 美术交付时 Unity 开着但没法远程点菜单）。场景原本没有未存改动才顺手存盘。
    /// 以后要重跑用菜单 Tools/Ib/布置结局美术。
    /// </summary>
    [InitializeOnLoad]
    internal static class EndingArtAutoRun
    {
        private const string Key = "Ib.EndingArt.AutoRun.2026-11-01";

        static EndingArtAutoRun()
        {
            if (EditorPrefs.GetBool(Key, false))
            {
                return;
            }

            EditorApplication.delayCall += TryRun;
        }

        private static void TryRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Graybox.unity")
            {
                return;
            }

            EditorPrefs.SetBool(Key, true);
            GrayboxBuilder.SetupEndingArt(!scene.isDirty);
        }
    }
}
