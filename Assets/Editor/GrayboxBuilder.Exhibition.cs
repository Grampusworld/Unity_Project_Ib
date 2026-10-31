using System.Collections.Generic;
using Ib.Environment;
using Ib.Interaction;
using Ib.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Ib.EditorTools
{
    /// <summary>
    /// 展厅细节：游客（白圆柱 → 人台 + 抹开的油画脸）、接待大厅导言墙（标题、横幅、名牌、挂钟、平面图）、
    /// 厕所镜子 + Ted 的身体（只给镜子照）、值班室六格监控。
    /// 和「布置后半段」一样只在现有场景上补，按名字找，可重复执行；生成灰盒的最后一步也会调用。
    /// </summary>
    public static partial class GrayboxBuilder
    {
        private const string FaceDir = "Assets/Art/Textures/Faces";
        private static readonly Color Ink = new Color(0.16f, 0.13f, 0.1f);
        // 导言墙上的字：射灯打在墙上会有泛光，字要接近纯黑才压得住
        private static readonly Color WallInk = new Color(0.03f, 0.025f, 0.02f);
        private static readonly Color Cream = new Color(0.9f, 0.86f, 0.76f);

        [MenuItem("Tools/Ib/布置展厅细节（游客、导言墙、镜子、监控）")]
        private static void SetupExhibitionMenu()
        {
            SetupExhibition();
        }

        internal static void SetupExhibition()
        {
            Scene scene = SceneManager.GetActiveScene();
            LoadFinishes();
            if (LayerMask.NameToLayer(TedBody.LayerName) < 0)
            {
                Debug.LogError("[GrayboxBuilder] 先在 Tags and Layers 里加一个 " + TedBody.LayerName + " 层。");
                return;
            }

            Transform props = FindRoot(scene, "Props_Museum");
            BuildVisitors(FindRoot(scene, "NPC_Crowd"));
            BuildReceptionWall(Require(props, "Reception"));
            BuildToiletMirror(Require(props, "Toilet"));
            BuildTedBody(scene);
            BuildMonitors(scene, Require(props, "DutyRoom"));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[GrayboxBuilder] 展厅细节布置完成。");
        }

        // ================================================================ 游客

        private enum VisitorPose { HandsBehind, Booklet, Leaning, WithChild }

        private static Material visitorSkin;

        /// <summary>
        /// 人群里每根白圆柱换成一个人台游客：同样的位置、朝向、身高，碰撞体换成胶囊（还在 NPC_Crowd 下，准星照样穿过）。
        /// 脸只有两张，轮流用 —— 同一张脸会出现在两组人群里（GDD §3.2 第四段）。
        /// </summary>
        private static void BuildVisitors(Transform crowd)
        {
            if (crowd == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 找不到 NPC_Crowd，跳过游客。");
                return;
            }

            Material[] faces =
            {
                VisitorMaterial("M_Visitor_Face_A", PaintFace("T_VisitorFace_A", true, new Color(0.82f, 0.72f, 0.62f), new Color(0.22f, 0.17f, 0.13f), 11)),
                VisitorMaterial("M_Visitor_Face_B", PaintFace("T_VisitorFace_B", true, new Color(0.76f, 0.66f, 0.58f), new Color(0.42f, 0.38f, 0.34f), 23)),
            };
            Color[] cloth =
            {
                new Color(0.2f, 0.2f, 0.22f), new Color(0.22f, 0.25f, 0.32f), new Color(0.33f, 0.33f, 0.27f),
                new Color(0.36f, 0.22f, 0.22f), new Color(0.42f, 0.38f, 0.33f), new Color(0.3f, 0.33f, 0.36f),
            };
            Material[] fabrics = new Material[cloth.Length];
            for (int i = 0; i < cloth.Length; i++)
            {
                fabrics[i] = VisitorMaterial("M_Visitor_Fabric_" + i, null, cloth[i]);
            }

            Material paper = VisitorMaterial("M_Visitor_Booklet", null, new Color(0.86f, 0.82f, 0.72f));
            visitorSkin = VisitorMaterial("M_Visitor_Skin", null, new Color(0.8f, 0.7f, 0.61f));

            int count = 0;
            List<Transform> cylinders = new List<Transform>();
            foreach (MeshFilter mf in crowd.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.name.StartsWith("NPC_") && mf.sharedMesh != null && mf.sharedMesh.name.StartsWith("Cylinder"))
                {
                    cylinders.Add(mf.transform);
                }
            }

            Physics.SyncTransforms();
            foreach (Transform c in cylinders)
            {
                float height = c.localScale.y * 2f;
                float radius = c.localScale.x * 0.5f;
                Vector3 foot = c.localPosition - Vector3.up * (height * 0.5f);
                float yaw = c.localRotation.eulerAngles.y;
                Transform parent = c.parent;
                string name = c.name;
                int seed = Mathf.Abs(name.GetHashCode());
                int index = c.GetSiblingIndex();
                Object.DestroyImmediate(c.gameObject);

                bool grampus = name.Contains("Grampus");
                VisitorPose pose = grampus ? VisitorPose.HandsBehind : (VisitorPose)(seed % 4);
                Outfit outfit = grampus || seed % 3 == 0 ? Outfit.Coat : Outfit.Gown;
                Transform v = Visitor(parent, name, foot, yaw, height, radius, outfit, fabrics[seed % fabrics.Length],
                    faces[(seed / 7) % faces.Length], pose, paper);
                v.SetSiblingIndex(index);

                if (pose == VisitorPose.WithChild && !AddChild(v, faces[(seed / 7 + 1) % faces.Length], fabrics[(seed + 3) % fabrics.Length]))
                {
                    PoseArms(v, VisitorPose.HandsBehind);
                }

                count++;
            }

            Debug.Log("[GrayboxBuilder] 换了 " + count + " 个游客。");
        }

        private static Material VisitorMaterial(string name, Texture2D albedo, Color? color = null)
        {
            Material m = Lit(name, albedo != null ? albedo : Finishes.Canvas.mainTexture as Texture2D, null, color ?? Color.white, 0.12f, 1f, 0f, false);
            m.enableInstancing = true;
            if (albedo == null)
            {
                // 画布纹理细一点，像布面上的油彩
                m.mainTextureScale = new Vector2(3f, 3f);
            }

            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>origin、height、radius 都是父节点里的 1 倍坐标（NPC_Crowd 整体放大 WorldScale）。</summary>
        private static Transform Visitor(Transform parent, string name, Vector3 origin, float yaw, float height, float radius,
            Outfit outfit, Material fabric, Material face, VisitorPose pose, Material paper)
        {
            float s = height / 1.78f;
            Transform g = Mannequin(parent, name, origin, yaw, s, outfit, fabric);
            g.Find("Head").GetComponent<MeshRenderer>().sharedMaterial = face;
            g.Find("Neck").GetComponent<MeshRenderer>().sharedMaterial = visitorSkin;

            // 统一换成可摆姿势的手臂（大衣自带的袖子去掉）
            foreach (string sleeve in new[] { "Sleeve_L", "Sleeve_R" })
            {
                Transform t = g.Find(sleeve);
                if (t != null)
                {
                    Object.DestroyImmediate(t.gameObject);
                }
            }

            for (int side = -1; side <= 1; side += 2)
            {
                Transform shoulder = NewGroup(g, side < 0 ? "Arm_L" : "Arm_R", new Vector3(side * 0.21f, 1.36f, 0f), 0f);
                LocalCylinder(shoulder, "Sleeve", new Vector3(0f, -0.26f, 0f), 0.085f, 0.52f, fabric);
                LocalSphere(shoulder, "Hand", new Vector3(0f, -0.55f, 0f), new Vector3(0.07f, 0.09f, 0.07f), visitorSkin);
            }

            PoseArms(g, pose);
            if (pose == VisitorPose.Booklet)
            {
                GameObject book = LocalBox(g, "Booklet", new Vector3(0f, 1.0f, 0.3f), new Vector3(0.16f, 0.22f, 0.012f), paper);
                book.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
            }
            else if (pose == VisitorPose.Leaning)
            {
                // 底座、立杆不动，上半身（含衣服）从立杆顶端往前倾
                Transform upper = NewGroup(g, "Upper", new Vector3(0f, 0.78f, 0f), 0f);
                List<Transform> parts = new List<Transform>();
                foreach (Transform child in g)
                {
                    if (child != upper && child.name != "Base" && child.name != "Pole")
                    {
                        parts.Add(child);
                    }
                }

                foreach (Transform p in parts)
                {
                    p.SetParent(upper, true);
                }

                upper.localRotation = Quaternion.Euler(16f, 0f, 0f);
            }

            CapsuleCollider capsule = g.gameObject.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = radius / s;
            return g;
        }

        private static void PoseArms(Transform visitor, VisitorPose pose)
        {
            Transform l = FindDeep(visitor, "Arm_L");
            Transform r = FindDeep(visitor, "Arm_R");
            switch (pose)
            {
                case VisitorPose.HandsBehind:
                    l.localRotation = Quaternion.Euler(-28f, 0f, -14f);
                    r.localRotation = Quaternion.Euler(-28f, 0f, 14f);
                    break;
                case VisitorPose.Booklet:
                    l.localRotation = Quaternion.Euler(48f, 0f, 16f);
                    r.localRotation = Quaternion.Euler(48f, 0f, -16f);
                    break;
                case VisitorPose.Leaning:
                    l.localRotation = Quaternion.Euler(20f, 0f, -4f);
                    r.localRotation = Quaternion.Euler(20f, 0f, 4f);
                    break;
                case VisitorPose.WithChild:
                    l.localRotation = Quaternion.Euler(-4f, 0f, -5f);
                    r.localRotation = Quaternion.Euler(6f, 0f, 22f);
                    break;
            }
        }

        /// <summary>大人右手边牵一个小孩（童装人台）。两边都被墙、展品挡住就不牵，返回 false。</summary>
        private static bool AddChild(Transform adult, Material face, Material fabric)
        {
            foreach (float side in new[] { 1f, -1f })
            {
                Vector3 local = new Vector3(side * 0.62f, 0f, 0.05f);
                Vector3 world = adult.TransformPoint(local);
                bool blocked = false;
                foreach (Collider hit in Physics.OverlapSphere(world + Vector3.up * 0.5f * WorldScale, 0.3f * WorldScale, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!hit.transform.IsChildOf(adult) && hit.GetComponentInParent<FirstPersonController>() == null)
                    {
                        blocked = true;
                        break;
                    }
                }

                if (blocked)
                {
                    continue;
                }

                Transform child = Mannequin(adult, "Child", local, 0f, 0.6f, Outfit.Child, fabric);
                child.Find("Head").GetComponent<MeshRenderer>().sharedMaterial = face;
                child.Find("Neck").GetComponent<MeshRenderer>().sharedMaterial = visitorSkin;
                Transform arm = NewGroup(child, "Arm_Up", new Vector3(-side * 0.2f, 1.36f, 0f), 0f);
                LocalCylinder(arm, "Sleeve", new Vector3(0f, -0.26f, 0f), 0.085f, 0.52f, fabric);
                arm.localRotation = Quaternion.Euler(0f, 0f, side * 35f);

                Transform r = FindDeep(adult, side > 0 ? "Arm_R" : "Arm_L");
                r.localRotation = Quaternion.Euler(6f, 0f, side * 22f);
                return true;
            }

            return false;
        }

        // ================================================================ 脸

        /// <summary>
        /// 程序画一张脸，贴在 Unity 球体上（球的 UV：u = 0.75 正对 +Z，v = 0.5 是赤道）。
        /// smeared = 游客：五官位置对，但横向抹开，像没干的油画被人抹了一下（不是马赛克）；否则是画清楚的脸（Ted）。
        /// </summary>
        private static Texture2D PaintFace(string name, bool smeared, Color skin, Color hair, int seed)
        {
            EnsureFolder(FaceDir);
            const int w = 512;
            const int h = 256;
            System.Random rng = new System.Random(seed);
            Color[] px = new Color[w * h];
            float cx = 0.75f * w;
            float cy = 0.5f * h;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float du = Mathf.DeltaAngle(x * 360f / w, cx * 360f / w);   // 离脸中心的经度（度）
                    float v = y / (float)h;
                    // 头发：头顶一圈；脸以外的地方往下盖到后脑
                    float hairline = Mathf.Abs(du) < 60f ? 0.72f : 0.42f + 0.25f * (1f - Mathf.Clamp01((Mathf.Abs(du) - 60f) / 40f));
                    Color c = v > hairline ? hair : skin;
                    float grain = ((float)rng.NextDouble() - 0.5f) * 0.05f;
                    px[y * w + x] = c + new Color(grain, grain, grain, 0f);
                }
            }

            // 五官（像素坐标，按 u 一圈 512、v 半圈 256 换算）。球面一圈 512 像素，脸只占 1/6 左右，五官要画大才看得清
            const float f = 1.8f;
            Color dark = new Color(0.16f, 0.11f, 0.09f);
            Color iris = new Color(0.25f, 0.17f, 0.1f);
            Blob(px, w, h, cx - 17f * f, cy + 14f * f, 7f * f, 4.5f * f, smeared ? dark : Color.white, 1f);
            Blob(px, w, h, cx + 17f * f, cy + 14f * f, 7f * f, 4.5f * f, smeared ? dark : Color.white, 1f);
            Blob(px, w, h, cx - 17f * f, cy + 14f * f, 3.4f * f, 3.4f * f, iris, 1f);
            Blob(px, w, h, cx + 17f * f, cy + 14f * f, 3.4f * f, 3.4f * f, iris, 1f);
            Blob(px, w, h, cx - 17f * f, cy + 24f * f, 9f * f, 1.8f * f, hair * 0.8f, 1f);
            Blob(px, w, h, cx + 17f * f, cy + 24f * f, 9f * f, 1.8f * f, hair * 0.8f, 1f);
            Blob(px, w, h, cx, cy + 2f * f, 3.5f * f, 6f * f, skin * 0.82f, 0.7f);
            Blob(px, w, h, cx, cy - 16f * f, 10f * f, 2.2f * f, new Color(0.62f, 0.3f, 0.28f), 1f);
            Blob(px, w, h, cx - 26f * f, cy - 4f * f, 8f * f, 6f * f, new Color(0.9f, 0.6f, 0.55f), 0.25f);
            Blob(px, w, h, cx + 26f * f, cy - 4f * f, 8f * f, 6f * f, new Color(0.9f, 0.6f, 0.55f), 0.25f);

            if (smeared)
            {
                Smear(px, w, h, cx, cy, rng);
            }

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            string path = FaceDir + "/" + name + ".png";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void Blob(Color[] px, int w, int h, float cx, float cy, float rx, float ry, Color c, float alpha)
        {
            for (int y = Mathf.Max(0, (int)(cy - ry - 2)); y <= Mathf.Min(h - 1, (int)(cy + ry + 2)); y++)
            {
                for (int x = (int)(cx - rx - 2); x <= (int)(cx + rx + 2); x++)
                {
                    float dx = (x - cx) / rx;
                    float dy = (y - cy) / ry;
                    float d = dx * dx + dy * dy;
                    if (d > 1f)
                    {
                        continue;
                    }

                    int xi = (x % w + w) % w;
                    float a = alpha * Mathf.Clamp01((1f - d) * 3f);
                    px[y * w + xi] = Color.Lerp(px[y * w + xi], c, a);
                }
            }
        }

        /// <summary>只抹脸那一块：每行随机长度的横向拖拽（往同一个方向），再叠几道斜的笔触。</summary>
        private static void Smear(Color[] px, int w, int h, float cx, float cy, System.Random rng)
        {
            Color[] src = (Color[])px.Clone();
            for (int y = (int)(cy - 62f); y < (int)(cy + 68f); y++)
            {
                int length = 12 + rng.Next(40);
                float strength = 0.55f + (float)rng.NextDouble() * 0.4f;
                for (int x = (int)(cx - 95f); x < (int)(cx + 95f); x++)
                {
                    Color sum = Color.clear;
                    for (int k = 0; k < length; k++)
                    {
                        sum += src[y * w + ((x - k) % w + w) % w];
                    }

                    float fade = Mathf.Clamp01(1f - Mathf.Abs(x - cx) / 95f) * Mathf.Clamp01(1f - Mathf.Abs(y - cy) / 68f) * 2f;
                    px[y * w + x] = Color.Lerp(src[y * w + x], sum / length, Mathf.Clamp01(strength * fade));
                }
            }

            // 几道斜着拖过的笔触，带一点下面的颜色
            for (int i = 0; i < 6; i++)
            {
                float sx = cx - 65f + (float)rng.NextDouble() * 130f;
                float sy = cy - 45f + (float)rng.NextDouble() * 90f;
                float slope = ((float)rng.NextDouble() - 0.5f) * 0.6f;
                Color under = px[(int)sy * w + (int)sx];
                for (int t = 0; t < 60; t++)
                {
                    Blob(px, w, h, sx + t, sy + t * slope, 4f, 4f, under, 0.35f);
                }
            }
        }

        // ================================================================ 接待大厅导言墙

        /// <summary>
        /// 导言墙（T1）做成展览主视觉：大字标题 + 副标题 + 三行导言；左边一条深红横幅「Final week · Closes Thursday」；
        /// 证件照下面一块名牌；导言墙上方一只挂钟（WallClock）；前台左侧一块平面图板。都是 1 倍坐标，挂在 Reception 组下。
        /// </summary>
        private static void BuildReceptionWall(Transform reception)
        {
            if (reception == null)
            {
                return;
            }

            Transform old = reception.Find("IntroWall_Dressing");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            float y = Floor1Y;
            float wall = ReceptionNorthZ - HalfWall;    // 北墙面（朝 −Z）
            // 组转 180°：本地 +Z = 世界 −Z（朝大厅），Label3D 的字就朝外
            Transform g = NewGroup(reception, "IntroWall_Dressing", new Vector3(0f, 0f, wall), 180f);
            // 标题、副标题、导言：导言板正面离墙 0.07（板厚 0.06 + 离墙 0.01），字再往外 1 cm；导言板中心 x = 19.5，本地 x 取负
            Transform text = NewGroup(g, "Text", new Vector3(-19.5f, y, 0.08f), 0f);
            Label3D(text, "Title", new Vector3(0f, 3.2f, 0f), "The Audience", 0.26f, WallInk);
            Label3D(text, "Subtitle", new Vector3(0f, 2.8f, 0f), "A Grampus Retrospective", 0.13f, WallInk);
            string[] intro =
            {
                "Grampus worked across oil painting, sculpture and costume design.",
                "Four winters ago, he left us in his studio.",
                "This exhibition is dedicated to Ted.",
            };
            for (int i = 0; i < intro.Length; i++)
            {
                Label3D(text, "Intro_" + i, new Vector3(0f, 2.3f - i * 0.24f, 0f), intro[i], 0.085f, WallInk);
            }

            // 横幅：导言板左边（从大厅看），挂在一根黄铜杆上
            Material oxblood = Lit("M_Panel_Oxblood", null, null, new Color(0.22f, 0.05f, 0.05f), 0.25f, 1f, 0f, false);
            Transform banner = NewGroup(g, "Banner_FinalWeek", new Vector3(-15.9f, y, 0.03f), 0f);
            LocalBox(banner, "Cloth", new Vector3(0f, 2.55f, 0f), new Vector3(0.9f, 2.5f, 0.015f), oxblood);
            LocalCylinder(banner, "Rod", new Vector3(0f, 3.82f, 0f), 0.03f, 1.0f, Finishes.Brass).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            string[] words = { "FINAL", "WEEK", "·", "CLOSES", "THURSDAY" };
            for (int i = 0; i < words.Length; i++)
            {
                Label3D(banner, "Word_" + i, new Vector3(0f, 3.35f - i * 0.36f, 0.012f), words[i], i == 4 ? 0.075f : 0.1f, Cream);
            }

            // 证件照（x = 23）下面的名牌
            Transform plate = NewGroup(g, "NamePlate_Grampus", new Vector3(-23f, y, 0.02f), 0f);
            LocalBox(plate, "Plate", new Vector3(0f, 1.42f, 0f), new Vector3(0.62f, 0.17f, 0.015f), Finishes.Brass);
            Label3D(plate, "Name", new Vector3(0f, 1.455f, 0.01f), "GRAMPUS", 0.045f, Ink);
            Label3D(plate, "Role", new Vector3(0f, 1.385f, 0.01f), "Painter · Sculptor · Costume designer", 0.019f, Ink);

            BuildWallClock(g, new Vector3(-19.5f, y + 4.35f, 0.04f));
            BuildFloorPlan(reception, y);
        }

        /// <summary>老式挂钟：黄铜外圈 + 米色表盘 + 12 个刻度 + 时针分针（WallClock 按状态拨）。表盘朝本地 +Z。</summary>
        private static void BuildWallClock(Transform parent, Vector3 position)
        {
            Transform clock = NewGroup(parent, "WallClock", position, 0f);
            LocalCylinder(clock, "Rim", Vector3.zero, 0.62f, 0.06f, Finishes.Brass).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            LocalCylinder(clock, "Face", new Vector3(0f, 0f, 0.025f), 0.54f, 0.02f, Finishes.Label).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 12; i++)
            {
                Transform tick = NewGroup(clock, "Tick_" + i, new Vector3(0f, 0f, 0.037f), 0f);
                tick.localRotation = Quaternion.Euler(0f, 0f, -i * 30f);
                LocalBox(tick, "Mark", new Vector3(0f, 0.22f, 0f), new Vector3(i % 3 == 0 ? 0.02f : 0.01f, 0.05f, 0.005f), Finishes.MetalDark);
            }

            Transform hour = NewGroup(clock, "HourHand", new Vector3(0f, 0f, 0.042f), 0f);
            LocalBox(hour, "Blade", new Vector3(0f, 0.07f, 0f), new Vector3(0.025f, 0.16f, 0.005f), Finishes.MetalDark);
            Transform minute = NewGroup(clock, "MinuteHand", new Vector3(0f, 0f, 0.048f), 0f);
            LocalBox(minute, "Blade", new Vector3(0f, 0.1f, 0f), new Vector3(0.016f, 0.23f, 0.005f), Finishes.MetalDark);
            LocalCylinder(clock, "Pin", new Vector3(0f, 0f, 0.052f), 0.03f, 0.01f, Finishes.Brass).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            WallClock wc = clock.gameObject.AddComponent<WallClock>();
            SetRef(wc, "hourHand", hour);
            SetRef(wc, "minuteHand", minute);
        }

        /// <summary>
        /// 平面图板：前台左侧落地的一块牌子，正面朝正门（−Z）。左半 1F、右半 2F，各厅一块色块 + 名字，接待厅里一个红点 You are here。
        /// GDD 不用小地图，但馆里本来就该有一张平面图。
        /// </summary>
        private static void BuildFloorPlan(Transform reception, float y)
        {
            Transform old = reception.Find("FloorPlan_Board");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            // 转 180°：正面朝 −Z；本地 +X = 世界 −X，所以图上的 x 要取负（站在牌子前看，东在右边）
            Transform board = NewGroup(reception, "FloorPlan_Board", new Vector3(15.2f, y, 6.2f), 180f);
            LocalBox(board, "Leg_L", new Vector3(-0.5f, 0.45f, 0.03f), new Vector3(0.04f, 0.9f, 0.04f), Finishes.MetalDark);
            LocalBox(board, "Leg_R", new Vector3(0.5f, 0.45f, 0.03f), new Vector3(0.04f, 0.9f, 0.04f), Finishes.MetalDark);
            GameObject panel = AddBox(board, "Panel", new Vector3(0f, 1.15f, 0.03f), new Vector3(1.15f, 0.62f, 0.03f), Finishes.Label);
            panel.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            Transform face = NewGroup(panel.transform.parent, "Plan", new Vector3(0f, 1.15f, 0.047f), 0f);
            face.localRotation = panel.transform.localRotation;

            Label3D(face, "Header", new Vector3(0f, 0.25f, 0.002f), "FLOOR PLAN", 0.03f, Ink);
            Material room = Lit("M_Plan_Room", null, null, new Color(0.62f, 0.58f, 0.5f), 0.1f, 1f, 0f, false);
            Material accent = Lit("M_Plan_Accent", null, null, new Color(0.45f, 0.36f, 0.3f), 0.1f, 1f, 0f, false);
            Material here = Lit("M_Plan_Here", null, null, new Color(0.75f, 0.12f, 0.1f), 0.2f, 1f, 0f, false);

            const float scale = 0.0118f;     // 40 m → 0.47
            PlanFloor(face, "1F", new Vector2(-0.27f, -0.04f), scale, room, accent, new[]
            {
                ("Oil Paintings", new Rect(0f, 0f, WestLineX, BuildingDepth)),
                ("Reception", new Rect(WestLineX, 0f, EastLineX - WestLineX, ReceptionNorthZ)),
                ("Sculpture", new Rect(WestLineX, ReceptionNorthZ, EastLineX - WestLineX, StairHallSouthZ - ReceptionNorthZ)),
                ("Stairs", new Rect(WestLineX, StairHallSouthZ, EastLineX - WestLineX, BuildingDepth - StairHallSouthZ)),
                ("Costume", new Rect(EastLineX, 0f, BuildingWidth - EastLineX, BuildingDepth)),
            });
            PlanFloor(face, "2F", new Vector2(0.27f, -0.04f), scale, room, accent, new[]
            {
                ("Staff", new Rect(0f, 0f, WestLineX, DutyNorthZ)),
                ("Closed", new Rect(0f, DutyNorthZ, WestLineX, BuildingDepth - DutyNorthZ)),
                ("Gallery", new Rect(WestLineX, 0f, EastLineX - WestLineX, StairHallSouthZ)),
                ("Stairs", new Rect(WestLineX, StairHallSouthZ, EastLineX - WestLineX, BuildingDepth - StairHallSouthZ)),
                ("WC", new Rect(EastLineX, 0f, ToiletEastX - EastLineX, ToiletNorthZ)),
                ("Family Portrait", new Rect(EastLineX, FamilySouthZ, BuildingWidth - EastLineX, FamilyNorthZ - FamilySouthZ)),
            });

            // 你在这里：1F 接待厅、正门里面一点
            Vector2 at = PlanPoint(new Vector2(-0.27f, -0.04f), scale, new Vector2(MainDoorX, 2.5f));
            LocalCylinder(face, "YouAreHere", new Vector3(at.x, at.y, 0.004f), 0.022f, 0.004f, here).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Label3D(face, "YouAreHere_Label", new Vector3(at.x, at.y - 0.025f, 0.005f), "You are here", 0.011f, new Color(0.6f, 0.1f, 0.08f));
        }

        private static Vector2 PlanPoint(Vector2 center, float scale, Vector2 world)
        {
            // 图上 x：东在右（本地 −X 方向是世界 +X，见 BuildFloorPlan），这里的 Label3D 已经转了 180°，按看牌子的人的左右排
            float x = center.x + (world.x - BuildingWidth * 0.5f) * scale;
            float y = center.y + (world.y - BuildingDepth * 0.5f) * scale;
            return new Vector2(-x, y);
        }

        private static void PlanFloor(Transform face, string label, Vector2 center, float scale, Material room, Material accent,
            (string name, Rect rect)[] rooms)
        {
            Label3D(face, "Floor_" + label, new Vector3(-center.x, center.y + BuildingDepth * scale * 0.5f + 0.03f, 0.002f), label, 0.022f, Ink);
            for (int i = 0; i < rooms.Length; i++)
            {
                Rect r = rooms[i].rect;
                Vector2 c = PlanPoint(center, scale, r.center);
                LocalBox(face, label + "_" + rooms[i].name, new Vector3(c.x, c.y, 0.001f),
                    new Vector3(r.width * scale - 0.004f, r.height * scale - 0.004f, 0.002f), i % 2 == 0 ? room : accent);
                Label3D(face, label + "_" + rooms[i].name + "_Label", new Vector3(c.x, c.y, 0.003f), rooms[i].name, 0.009f, Cream);
            }
        }

        // ================================================================ 镜子

        /// <summary>
        /// 厕所的两面镜子，都挂 PlanarMirror：
        /// 1. 北墙洗手台上方原有的玻璃板前面贴一张镜面（大人的高度：7 岁的 Ted 照不到自己，只照得到房间）；
        /// 2. 西墙门洞北边一面落地穿衣镜（木框），孩子从头到脚都照得到 —— 进门左手边就是，倒影这一下靠它。
        /// 画中馆里厕所也能进，那时照得出 Ted；出画以后照不出（§5.3）。
        /// </summary>
        private static void BuildToiletMirror(Transform toilet)
        {
            Transform glass = toilet != null ? FindDeep(toilet, "Mirror") : null;
            if (glass == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 找不到厕所的 Mirror，跳过镜子。");
                return;
            }

            foreach (string n in new[] { "Mirror_Surface", "FullLengthMirror" })
            {
                Transform old = toilet.Find(n);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            Shader shader = Shader.Find("Ib/PlanarMirror");
            string path = FinishDir + "/M_Mirror.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = "M_Mirror" };
                AssetDatabase.CreateAsset(mat, path);
            }

            // 1. 玻璃板的正面朝 −Z（厕所里面）；Quad 的可见面也朝 −Z，不用转
            Vector3 size = glass.localScale;
            MirrorQuad(toilet, "Mirror_Surface", glass.localPosition - new Vector3(0f, 0f, size.z * 0.5f + 0.004f),
                new Vector2(size.x - 0.06f, size.y - 0.06f), 0f, mat);

            // 2. 落地镜：西墙内面（x = EastLineX + HalfWall），门洞北边，镜面朝 +X（厕所里面）
            float wallX = EastLineX + HalfWall;
            float z = (ToiletDoorZ + SmallDoorWidth * 0.5f + ToiletNorthZ - HalfWall) * 0.5f;
            Transform frame = NewGroup(toilet, "FullLengthMirror", new Vector3(wallX, Floor2Y, z), 90f);
            Material wood = Lit("M_MirrorFrame_Wood", null, null, new Color(0.28f, 0.18f, 0.1f), 0.35f, 1f, 0f, false);
            const float w = 0.85f;
            const float h = 1.75f;
            const float bottom = 0.12f;
            LocalBox(frame, "Frame_Top", new Vector3(0f, bottom + h + 0.04f, 0.03f), new Vector3(w + 0.16f, 0.08f, 0.05f), wood);
            LocalBox(frame, "Frame_Bottom", new Vector3(0f, bottom - 0.04f, 0.03f), new Vector3(w + 0.16f, 0.08f, 0.05f), wood);
            LocalBox(frame, "Frame_L", new Vector3(-w * 0.5f - 0.04f, bottom + h * 0.5f, 0.03f), new Vector3(0.08f, h, 0.05f), wood);
            LocalBox(frame, "Frame_R", new Vector3(w * 0.5f + 0.04f, bottom + h * 0.5f, 0.03f), new Vector3(0.08f, h, 0.05f), wood);
            // 组朝 +X（yaw 90，本地 +Z = 世界 +X）；Quad 的可见面是本地 −Z，再转 180° 才朝外
            MirrorQuad(frame, "Glass", new Vector3(0f, bottom + h * 0.5f, 0.02f), new Vector2(w, h), 180f, mat);
        }

        private static void MirrorQuad(Transform parent, string name, Vector3 localPos, Vector2 size, float yaw, Material mat)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = localPos;
            quad.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            RemoveCollider(quad);
            MeshRenderer r = quad.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            quad.AddComponent<PlanarMirror>();
        }

        // ================================================================ Ted

        /// <summary>
        /// Ted 的简易身体（7 岁男孩，约 1.3 m，不放大）：藏青毛衣、灰短裤、及膝袜、棕皮鞋，脸画清楚（他和 Lily 是画里唯二画清楚的人）。
        /// 全部在 TedReflection 层，主相机不渲染；TedBody 每帧跟着玩家。放在场景根上，不进 Player 预制体。
        /// </summary>
        private static void BuildTedBody(Scene scene)
        {
            Transform old = FindRoot(scene, "TedBody");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            Material sweater = VisitorMaterial("M_Ted_Sweater", null, new Color(0.13f, 0.17f, 0.32f));
            Material shorts = VisitorMaterial("M_Ted_Shorts", null, new Color(0.42f, 0.42f, 0.44f));
            Material socks = VisitorMaterial("M_Ted_Socks", null, new Color(0.86f, 0.84f, 0.78f));
            Material shoes = VisitorMaterial("M_Ted_Shoes", null, new Color(0.34f, 0.2f, 0.11f));
            Material skin = VisitorMaterial("M_Ted_Skin", null, new Color(0.95f, 0.8f, 0.68f));
            Material hair = VisitorMaterial("M_Ted_Hair", null, new Color(0.3f, 0.19f, 0.1f));
            Material face = VisitorMaterial("M_Ted_Face", PaintFace("T_TedFace", false, new Color(0.95f, 0.8f, 0.68f), new Color(0.3f, 0.19f, 0.1f), 5));

            Transform root = new GameObject("TedBody").transform;
            for (int side = -1; side <= 1; side += 2)
            {
                string s = side < 0 ? "_L" : "_R";
                LocalBox(root, "Shoe" + s, new Vector3(side * 0.07f, 0.03f, 0.03f), new Vector3(0.09f, 0.06f, 0.18f), shoes);
                LocalCylinder(root, "Sock" + s, new Vector3(side * 0.07f, 0.19f, 0f), 0.072f, 0.26f, socks);
                LocalCylinder(root, "Knee" + s, new Vector3(side * 0.07f, 0.37f, 0f), 0.07f, 0.1f, skin);
            }

            LocalBox(root, "Shorts", new Vector3(0f, 0.49f, 0f), new Vector3(0.26f, 0.17f, 0.17f), shorts);
            LocalSphere(root, "Sweater", new Vector3(0f, 0.77f, 0f), new Vector3(0.3f, 0.48f, 0.21f), sweater);
            LocalCylinder(root, "Neck", new Vector3(0f, 1.0f, 0f), 0.08f, 0.06f, skin);
            GameObject head = LocalSphere(root, "Head", new Vector3(0f, 1.13f, 0f), new Vector3(0.2f, 0.23f, 0.21f), face);
            LocalSphere(root, "Hair", new Vector3(0f, 1.175f, -0.012f), new Vector3(0.212f, 0.17f, 0.215f), hair);
            head.transform.localRotation = Quaternion.identity;

            Transform[] arms = new Transform[2];
            for (int side = -1; side <= 1; side += 2)
            {
                Transform shoulder = NewGroup(root, side < 0 ? "Arm_L" : "Arm_R", new Vector3(side * 0.165f, 0.93f, 0f), 0f);
                LocalCylinder(shoulder, "Sleeve", new Vector3(0f, -0.17f, 0f), 0.075f, 0.34f, sweater);
                LocalSphere(shoulder, "Hand", new Vector3(0f, -0.37f, 0f), new Vector3(0.065f, 0.075f, 0.065f), skin);
                arms[side < 0 ? 0 : 1] = shoulder;
            }

            int layer = LayerMask.NameToLayer(TedBody.LayerName);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
                MeshRenderer r = t.GetComponent<MeshRenderer>();
                if (r != null)
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;   // 主相机看不见他，也不要在地上投出一个人影
                }
            }

            TedBody body = root.gameObject.AddComponent<TedBody>();
            SetRef(body, "armLeft", arms[0]);
            SetRef(body, "armRight", arms[1]);

            FirstPersonController player = Object.FindAnyObjectByType<FirstPersonController>();
            if (player != null)
            {
                root.position = player.transform.position;
            }
        }

        // ================================================================ 监控

        /// <summary>
        /// 值班室监控墙（A_MonitorWall）南面贴 3 × 2 六块屏，上排中间那块是雕塑厅（下排被值班桌挡住一半）。
        /// 六台监控摄像机放在场景根的 SecurityFeeds 下（世界坐标），位置和馆里的监控模型一致，第六台看二层楼梯厅。
        /// </summary>
        private static void BuildMonitors(Scene scene, Transform duty)
        {
            Transform wallBox = duty != null ? FindDeep(duty, "A_MonitorWall") : null;
            if (wallBox == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 找不到 A_MonitorWall，跳过监控。");
                return;
            }

            Transform oldFeeds = FindRoot(scene, "SecurityFeeds");
            if (oldFeeds != null)
            {
                Object.DestroyImmediate(oldFeeds.gameObject);
            }

            Transform oldScreens = duty.Find("Monitor_Screens");
            if (oldScreens != null)
            {
                Object.DestroyImmediate(oldScreens.gameObject);
            }

            float y1 = Floor1Y;
            float y2 = Floor2Y;
            (string name, Vector3 mount, Vector3 target)[] cams =
            {
                ("Feed_Sculpture", new Vector3(13.5f, y1 + 4.4f, 20.5f), SculptureCenter() + Vector3.up * 1.2f),
                ("Feed_Reception", new Vector3(25.55f, y1 + 5.0f, 8.55f), new Vector3(19.5f, y1 + 1f, 1.5f)),
                ("Feed_OilHall", new Vector3(0.45f, y1 + 5.0f, 25.45f), new Vector3(6.5f, y1 + 1f, 13f)),
                ("Feed_Costume", new Vector3(39.55f, y1 + 5.0f, 0.45f), new Vector3(33f, y1 + 1f, 13f)),
                ("Feed_Gallery", new Vector3(13.45f, y2 + 4.0f, 0.45f), new Vector3(19.5f, y2 + 1f, 10f)),
                ("Feed_Stairs2F", new Vector3(25.55f, y2 + 4.0f, 25.45f), new Vector3(17f, y2 + 0.5f, 21f)),
            };

            Transform feedsRoot = new GameObject("SecurityFeeds").transform;
            Camera[] feeds = new Camera[cams.Length];
            for (int i = 0; i < cams.Length; i++)
            {
                Vector3 mount = cams[i].mount * WorldScale;
                Vector3 target = cams[i].target * WorldScale;
                Vector3 dir = (target - mount).normalized;
                GameObject go = new GameObject(cams[i].name);
                go.transform.SetParent(feedsRoot, false);
                go.transform.SetPositionAndRotation(mount + dir * 0.4f, Quaternion.LookRotation(dir, Vector3.up));
                Camera cam = go.AddComponent<Camera>();
                cam.fieldOfView = i == 0 ? 40f : 68f;   // 雕塑厅那台推近一点，天使在画面里看得清
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 70f;
                cam.enabled = false;
                UniversalAdditionalCameraData data = go.AddComponent<UniversalAdditionalCameraData>();
                data.renderShadows = false;
                data.renderPostProcessing = false;
                feeds[i] = cam;
            }

            Shader shader = Shader.Find("Ib/MonitorScreen");
            string path = FinishDir + "/M_MonitorScreen.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = "M_MonitorScreen" };
                AssetDatabase.CreateAsset(mat, path);
            }

            // 监控墙本身原来是黄色的任务道具色块，换成深色机壳
            wallBox.GetComponent<MeshRenderer>().sharedMaterial = Finishes.MetalDark;

            // 监控墙盒子的南面（朝 −Z，值班室里面）
            Vector3 c = wallBox.localPosition;
            Vector3 s = wallBox.localScale;
            float face = c.z - s.z * 0.5f - 0.006f;
            Transform screensRoot = NewGroup(duty, "Monitor_Screens");
            float[] cols = { 0f, -1.28f, 1.28f };       // 第一个是中间那列
            float[] rows = { 0.3f, -0.3f };            // 先上排
            Renderer[] screens = new Renderer[6];
            int n = 0;
            foreach (float row in rows)
            {
                foreach (float col in cols)
                {
                    GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = n == 0 ? "Screen_Sculpture" : "Screen_" + n;
                    quad.transform.SetParent(screensRoot, false);
                    quad.transform.localPosition = new Vector3(c.x + col, c.y + row, face);
                    quad.transform.localScale = new Vector3(1.18f, 0.54f, 1f);
                    RemoveCollider(quad);
                    MeshRenderer r = quad.GetComponent<MeshRenderer>();
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    screens[n++] = r;
                }
            }

            // feeds 的顺序要和 screens 对上：0 = 雕塑厅（上排中间）
            SecurityMonitors monitors = screensRoot.gameObject.AddComponent<SecurityMonitors>();
            SetRefArray(monitors, "feeds", new List<Object>(feeds));
            SetRefArray(monitors, "screens", new List<Object>(screens));
        }
    }
}
