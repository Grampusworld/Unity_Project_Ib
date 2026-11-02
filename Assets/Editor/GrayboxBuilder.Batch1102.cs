using Ib.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ib.EditorTools
{
    /// <summary>
    /// 2026-11-02 第五批改动（老大需求）：
    /// 1. 黑屋全家福草稿可交互：按 E 出字幕 C_BlackRoomPortrait（InspectCaption，挂在画架根上）。
    /// 2. Lily 肖像下移 0.4（Props_Museum 下 1 倍坐标 7.8 → 7.4，世界坐标 ×1.4 即 0.56 m），画框、肖像层、射灯一起跟。
    /// 3. 游客模糊脸加笔触法线（M_Visitor_Face_A/B），优先用美术交付的 T_VisitorFace_Brush_N.png，没有就程序生成。
    /// 4. 二楼全家福厅门：门洞东线 x = 26、z = 13.5，门板挡人，锁 / 开逻辑在 FamilyRoomDoor。
    /// 都按名字找、幂等（Lily 用目标 y 反推 dy，门的组件只补不叠），可重复执行；生成灰盒的最后一步也会调用。
    /// </summary>
    public static partial class GrayboxBuilder
    {
        // Lily 画板在 Props_Museum 下的 1 倍坐标 = Floor2Y 6.4 + 1.0（11-02 之前是 +1.4 = 7.8，下移 0.4）
        private const float LilyBoardLocalY = 7.4f;

        // 美术交付路径优先；没有就程序生成到下面的 *Proc* 路径（512×256，固定种子）
        private const string FaceBrushNormalPath = "Assets/Art/Incoming/贴图/T_VisitorFace_Brush_N.png";
        private const string FaceBrushProcNormalPath = "Assets/Art/Textures/Faces/T_VisitorFace_BrushProc_N.png";
        private const float FaceBrushNormalStrength = 0.8f;

        [MenuItem("Tools/Ib/布置 11-02 改动（黑屋画、Lily 下移、脸部笔触法线、全家福厅门锁）")]
        private static void SetupBatch1102Menu()
        {
            SetupBatch1102();
        }

        internal static void SetupBatch1102(bool save = true)
        {
            Scene scene = SceneManager.GetActiveScene();
            LoadFinishes();

            SetupBlackRoomPortrait(scene);
            LowerLilyPortrait(scene);
            ApplyFaceBrushNormals();
            BuildFamilyRoomDoor(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            if (save)
            {
                EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[GrayboxBuilder] 11-02 改动完成：黑屋画 / Lily 下移 / 脸部笔触法线 / 全家福厅门。");
        }

        // ================================================================ A1 黑屋画可交互

        /// <summary>黑屋的全家福草稿：画架子物体 Canvas 自带 BoxCollider，交互组件挂在画架根上（GetComponentInParent 能找到）。</summary>
        private static void SetupBlackRoomPortrait(Scene scene)
        {
            Transform blackRoom = FindRoot(scene, "Level_BlackRoom");
            Transform easel = blackRoom != null ? FindDeep(blackRoom, "FamilyPortraitSketch_Easel") : null;
            if (easel == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 找不到黑屋的 FamilyPortraitSketch_Easel，跳过黑屋画交互。");
                return;
            }

            InspectCaption inspect = Ensure<InspectCaption>(easel.gameObject);
            SerializedObject so = new SerializedObject(inspect);
            so.FindProperty("captionId").stringValue = "C_BlackRoomPortrait";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================ A2 Lily 肖像下移

        /// <summary>
        /// 画板、画框的四个方块、肖像三层（EndingArt 生成的组）都在 Props_Museum/2F/Gallery 下，直接改 localPosition.y；
        /// 射灯在 --- Lighting --- 下（同一个 1.4 缩放根里的 1 倍坐标），跟着下移后重新 LookAt 画板新的包围盒中心。
        /// 灯具（Level_Museum/Lamp_Fixtures/Fixture_Spot_...）挂在天花上，不动；只有灯的瞄准要变。
        /// </summary>
        private static void LowerLilyPortrait(Scene scene)
        {
            Transform props = FindRoot(scene, "Props_Museum");
            Transform board = props != null ? FindDeep(props, "Sketch_LilyLooksAtDoor") : null;
            if (board == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 找不到 Sketch_LilyLooksAtDoor，跳过 Lily 下移。");
                return;
            }

            // 幂等：第二次运行 dy ≈ 0，直接跳过
            float dy = LilyBoardLocalY - board.localPosition.y;
            if (Mathf.Abs(dy) < 1e-4f)
            {
                return;
            }

            board.localPosition += Vector3.up * dy;

            Transform frame = FindDeep(props, "Sketch_LilyLooksAtDoor_Frame");
            if (frame != null)
            {
                foreach (Transform child in frame)
                {
                    child.localPosition += Vector3.up * dy;
                }
            }

            Transform portrait = FindDeep(props, "Sketch_LilyLooksAtDoor_Portrait");
            if (portrait != null)
            {
                portrait.localPosition += Vector3.up * dy;
            }

            Transform lighting = FindRoot(scene, "--- Lighting ---");
            Transform spot = lighting != null ? FindDeep(lighting, "Spot_Gallery_Sketch_LilyLooksAtDoor") : null;
            if (spot == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 找不到 Spot_Gallery_Sketch_LilyLooksAtDoor，画已经挪了，灯没跟。");
                return;
            }

            spot.localPosition += Vector3.up * dy;
            Renderer renderer = board.GetComponent<Renderer>();
            if (renderer != null)
            {
                spot.LookAt(renderer.bounds.center, Vector3.up);
            }
        }

        // ================================================================ A3 模糊脸的笔触法线

        /// <summary>
        /// 两份游客脸材质都补同一张法线（模糊的脸 + 没干的油彩笔触）。Ted 的脸 M_Ted_Face 不动。
        /// 增量菜单用；全量生成时 BuildVisitors 建完脸的材质后也会调 ApplyFaceBrushNormal（"布置展厅细节"菜单同样生效）。
        /// </summary>
        private static void ApplyFaceBrushNormals()
        {
            Texture2D normal = FaceBrushNormalTexture();
            if (normal == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 脸部笔触法线不可用，跳过。");
                return;
            }

            foreach (string name in new[] { "M_Visitor_Face_A", "M_Visitor_Face_B" })
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(FinishDir + "/" + name + ".mat");
                if (mat == null)
                {
                    Debug.LogWarning("[GrayboxBuilder] 找不到 " + FinishDir + "/" + name + ".mat，跳过。");
                    continue;
                }

                ApplyFaceBrushNormal(mat, normal, FaceBrushNormalStrength);
            }
        }

        /// <summary>
        /// 优先用美术交付的 T_VisitorFace_Brush_N.png（存在就确保导入为 NormalMap、wrapU Repeat / wrapV Clamp）；
        /// 没有再程序生成一张 512×256 的，固定种子，结果每次一致（已存在就不重写，和工程里其它程序贴图一样）。
        /// </summary>
        private static Texture2D FaceBrushNormalTexture()
        {
            EnsureFolder(FaceDir);

            if (System.IO.File.Exists(ProjectFile(FaceBrushNormalPath)))
            {
                // 刚拷进来的文件可能还没导入，先补一次（已经导入过就不动，只有设置变了才 SaveAndReimport）
                TextureImporter art = AssetImporter.GetAtPath(FaceBrushNormalPath) as TextureImporter;
                if (art == null)
                {
                    AssetDatabase.ImportAsset(FaceBrushNormalPath, ImportAssetOptions.ForceSynchronousImport);
                    art = AssetImporter.GetAtPath(FaceBrushNormalPath) as TextureImporter;
                }

                if (art != null)
                {
                    bool dirty = false;
                    if (art.textureType != TextureImporterType.NormalMap)
                    {
                        art.textureType = TextureImporterType.NormalMap;
                        dirty = true;
                    }

                    if (art.wrapModeU != TextureWrapMode.Repeat)
                    {
                        art.wrapModeU = TextureWrapMode.Repeat;
                        dirty = true;
                    }

                    if (art.wrapModeV != TextureWrapMode.Clamp)
                    {
                        art.wrapModeV = TextureWrapMode.Clamp;
                        dirty = true;
                    }

                    if (dirty)
                    {
                        art.SaveAndReimport();
                    }

                    Texture2D delivered = AssetDatabase.LoadAssetAtPath<Texture2D>(FaceBrushNormalPath);
                    if (delivered != null)
                    {
                        return delivered;
                    }
                }
            }

            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(FaceBrushProcNormalPath);
            if (existing != null)
            {
                return existing;
            }

            const int w = 512;
            const int h = 256;
            Color[] pixels = FaceHeightToNormal(FaceBrushHeight(w, h), w, h, 3f);

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.SetPixels(pixels);
            tex.Apply();
            System.IO.File.WriteAllBytes(ProjectFile(FaceBrushProcNormalPath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(FaceBrushProcNormalPath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(FaceBrushProcNormalPath);
            importer.textureType = TextureImporterType.NormalMap;
            importer.wrapModeU = TextureWrapMode.Repeat;   // u 是球面一圈，横向要能接上
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 1;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(FaceBrushProcNormalPath);
        }

        /// <summary>
        /// 笔触高度场（0..1，0.5 = 平）：脸部涂抹区里叠 20 条横向为主、略带斜度的笔刷条，
        /// 每条有随机中点、长 30–120 px、宽 4–10 px，两端渐细、中心略隆起，条内是垂直笔触方向的高频刷毛细纹。
        /// 区域和 Smear 同一块：x = cx ± 70、y = cy − 62..+ 68，边缘用 16–18 px 羽化；脸外只剩极弱的布纹。
        /// </summary>
        private static float[] FaceBrushHeight(int w, int h)
        {
            float cx = 0.75f * w;    // 球面 UV：脸中心 u = 0.75
            float cy = 0.5f * h;     // 赤道
            float[] height = new float[w * h];
            for (int i = 0; i < height.Length; i++)
            {
                height[i] = 0.5f;
            }

            System.Random rng = new System.Random(1102);   // 固定种子：每次生成完全一致
            const int strokes = 20;
            for (int i = 0; i < strokes; i++)
            {
                float centerX = cx - 45f + (float)rng.NextDouble() * 90f;
                float centerY = cy - 38f + (float)rng.NextDouble() * 76f;
                float length = 30f + (float)rng.NextDouble() * 90f;
                float halfWidth = (4f + (float)rng.NextDouble() * 6f) * 0.5f;
                float slope = ((float)rng.NextDouble() - 0.5f) * 0.34f;   // 横向为主，略带斜度（约 ±10°）
                float dirX = Mathf.Cos(slope);
                float dirY = Mathf.Sin(slope);
                float sx = centerX - dirX * length * 0.5f;
                float sy = centerY - dirY * length * 0.5f;
                float phase = (float)rng.NextDouble() * Mathf.PI * 2f;
                float bristleFreq = 2f + (float)rng.NextDouble() * 0.8f;
                float lift = 0.55f + (float)rng.NextDouble() * 0.45f;

                int x0 = Mathf.Max(0, (int)(sx - length - 2f));
                int x1 = Mathf.Min(w - 1, (int)(sx + length + 2f));
                int y0 = Mathf.Max(0, (int)(sy - length - 2f));
                int y1 = Mathf.Min(h - 1, (int)(sy + length + 2f));
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float px = x + 0.5f - sx;
                        float py = y + 0.5f - sy;
                        float along = px * dirX + py * dirY;
                        if (along < 0f || along > length)
                        {
                            continue;
                        }

                        // 两端渐细
                        float taper = Mathf.Clamp01(Mathf.Min(along, length - along) / (length * 0.22f));
                        taper = taper * taper * (3f - 2f * taper);

                        float across = (-px * dirY + py * dirX) / (halfWidth * (0.35f + 0.65f * taper));
                        if (Mathf.Abs(across) >= 1f)
                        {
                            continue;
                        }

                        float profile = Mathf.Sqrt(1f - across * across);   // 中心略隆起
                        float ds = -px * dirY + py * dirX;                  // 垂直笔触方向：刷毛细纹
                        float bristle = 0.72f + 0.28f * Mathf.Sin(ds * bristleFreq + phase);
                        float stroke = taper * profile * bristle * lift;
                        int index = y * w + x;
                        height[index] = Mathf.Min(1f, Mathf.Max(height[index], 0.5f + 0.5f * stroke));
                    }
                }
            }

            // 涂抹区外压平，另补一点点布纹（几乎看不出来，只是不要死平）
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float fx = Mathf.Clamp01((70f - Mathf.Abs(x - cx)) / 18f);
                    float fy = Mathf.Clamp01(Mathf.Min(y - (cy - 62f), (cy + 68f) - y) / 16f);
                    float mask = fx * fy;
                    float weave = Mathf.Sin(x * 0.63f + y * 0.11f) * 0.5f + Mathf.Sin(y * 1.37f) * 0.5f;
                    int index = y * w + x;
                    height[index] = 0.5f + (height[index] - 0.5f) * mask + weave * 0.006f * (1f - mask);
                }
            }

            return height;
        }

        /// <summary>
        /// Materials.cs 的 HeightToNormal 固定按 TexSize × TexSize（512²）和双向 wrap，脸是 512 × 256、v 方向 Clamp，
        /// 所以这里照同一个公式写一份带尺寸的版本：n = ((左−右)·strength, (下−上)·strength, 1)，u 环绕。
        /// </summary>
        private static Color[] FaceHeightToNormal(float[] height, int w, int h, float strength)
        {
            Color[] pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float l = height[y * w + (x - 1 + w) % w];
                    float r = height[y * w + (x + 1) % w];
                    float d = height[Mathf.Max(0, y - 1) * w + x];
                    float u = height[Mathf.Min(h - 1, y + 1) * w + x];
                    Vector3 n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                    pixels[y * w + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f);
                }
            }

            return pixels;
        }

        /// <summary>
        /// 给 URP Lit 材质补法线：贴图、强度、_NORMALMAP 关键字，tiling / offset 跟 _BaseMap 一致。
        /// Lit() 只在新建材质时设置法线，已有的 M_Visitor_Face_A/B 要靠这里补上。
        /// </summary>
        private static void ApplyFaceBrushNormal(Material m, Texture2D normal, float strength)
        {
            if (m == null || normal == null)
            {
                return;
            }

            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", strength);
            m.EnableKeyword("_NORMALMAP");
            m.SetTextureScale("_BumpMap", m.GetTextureScale("_BaseMap"));
            m.SetTextureOffset("_BumpMap", m.GetTextureOffset("_BaseMap"));
            EditorUtility.SetDirty(m);
        }

        private static string ProjectFile(string assetPath)
        {
            return System.IO.Path.Combine(Application.dataPath, "..", assetPath);
        }

        // ================================================================ A4 全家福厅门

        /// <summary>
        /// 门板厚 0.1，填满二层东线 x = 26 的门洞（z 12.5–14.5、y Floor2Y..Floor2Y+3.0）。
        /// 先删旧门再建，保证重复执行不叠；全量生成时 BuildProps2F 的 FamilyRoom 段落也建同一块。
        /// </summary>
        private static void BuildFamilyRoomDoor(Scene scene)
        {
            Transform props = FindRoot(scene, "Props_Museum");
            Transform family = props != null ? FindDeep(props, "FamilyRoom") : null;
            if (family == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 找不到 Props_Museum/2F/FamilyRoom，跳过全家福厅门。");
                return;
            }

            Transform old = family.Find("Door_Family_Locked");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            Material doorMat = LoadPalette().Door;
            GameObject door = AddDoorPanel(family, "Door_Family_Locked", false, EastLineX,
                new Opening(FamilyDoorZ, FamilyDoorWidth, FamilyDoorHeight), Floor2Y, doorMat);
            door.AddComponent<FamilyRoomDoor>();
            BuildFamilyDoorLock(family, door.transform);
        }

        /// <summary>
        /// 门上的灰盒锁（老大 2026-11-02），外观照正门 MainDoor_Lock：黄铜锁片 0.16 × 0.26 × 0.04、深色锁孔，
        /// 外加一颗小锁扣。挂在走廊一面（x = 26 − 0.05）、门把手一侧（北边，合页在南边），高 1.09。
        /// 先在 FamilyRoom 下按 1 倍坐标建好，再保持世界姿态挂到门板下面（门板是非等比缩放的 Cube，直接按本地坐标建会被拉伸）。
        /// 没有碰撞体：交互打在门板上；FamilyRoomDoor 解锁后隐藏它，描边也打在它上面。门是先删后建，锁跟着重建，幂等。
        /// </summary>
        private static void BuildFamilyDoorLock(Transform family, Transform door)
        {
            const float face = EastLineX - 0.05f;            // EastLineX − 门板半厚：走廊一面
            const float z = FamilyDoorZ + FamilyDoorWidth * 0.5f - 0.22f;
            const float y = Floor2Y + 1.09f;

            Transform root = NewGroup(family, FamilyRoomDoor.LockName);
            root.localPosition = new Vector3(face, y, z);

            Material dark = Lit("M_Keyhole", null, null, new Color(0.02f, 0.018f, 0.015f), 0.2f, 1f, 0f, false);
            GameObject plate = AddBox(root, "LockPlate", new Vector3(-0.02f, 0f, 0f), new Vector3(0.04f, 0.26f, 0.16f), Finishes.Brass);
            GameObject hole = AddBox(root, "Keyhole", new Vector3(-0.0408f, -0.04f, 0f), new Vector3(0.0016f, 0.03f, 0.016f), dark);
            GameObject knob = AddBox(root, "LockKnob", new Vector3(-0.05f, 0.05f, 0f), new Vector3(0.02f, 0.045f, 0.045f), Finishes.Brass);
            RemoveCollider(plate);
            RemoveCollider(hole);
            RemoveCollider(knob);

            root.SetParent(door, true);
        }
    }
}
