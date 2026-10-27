using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ib.EditorTools
{
    /// <summary>
    /// 统一场景里所有名画（Painting_* 的模型实例）的大小，并把它们贴到身后的墙上。
    /// - 大小：以《戴珍珠耳环的少女》为参照，让每幅画的画面面积和它一样（横幅、竖幅都看着差不多分量）；
    ///   如果墙不够大（碰到门洞、墙角、天花），就逐步缩小直到整幅画背后都是墙。
    /// - 朝向：只保留绕 Y 的旋转，并对齐到最近的 90°，去掉摆放时的歪斜。
    /// - 位置：画框背面（模型原点）正好落在墙面上，不留缝；中心高度按楼层地面重新算。
    /// </summary>
    public static class PaintingArranger
    {
        private const string ReferenceName = "Painting_GirlWithPearlEarring";
        private const float MinBottomGap = 0.5f;     // 画框下沿离地至少这么高
        private const float PreferredCenter = 2.6f;  // 小画的中心高度（离地）
        private const float WallProbe = 0.6f;

        [MenuItem("Tools/Ib/统一名画尺寸并贴墙")]
        public static void Arrange()
        {
            List<Transform> paintings = FindPaintings();
            Transform reference = paintings.Find(p => p.name == ReferenceName);
            if (reference == null)
            {
                EditorUtility.DisplayDialog("统一名画尺寸", "场景里找不到参照画 " + ReferenceName + "。", "好");
                return;
            }

            Vector2 refSize = FrameSize(reference) * reference.localScale.x;
            float targetArea = refSize.x * refSize.y;

            Undo.RecordObjects(paintings.ConvertAll(p => (Object)p).ToArray(), "统一名画尺寸并贴墙");

            foreach (Transform p in paintings)
            {
                Place(p, targetArea);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(reference.gameObject.scene);
        }

        private static void Place(Transform p, float targetArea)
        {
            // 1. 只留 Y 旋转，对齐到 90°
            float yaw = Mathf.Round(p.eulerAngles.y / 90f) * 90f;
            p.rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 front = p.forward;

            // 2. 找身后的墙面：从画前方往后打射线
            Vector3 probeOrigin = p.position + front * WallProbe;
            if (!Physics.Raycast(probeOrigin, -front, out RaycastHit wall, WallProbe + 1.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                Debug.LogWarning("[PaintingArranger] " + p.name + " 背后找不到墙，跳过。", p);
                return;
            }

            // 3. 地面高度
            float floorY = Physics.Raycast(new Vector3(p.position.x, p.position.y, p.position.z) + front * 0.5f, Vector3.down,
                out RaycastHit floor, 10f, ~0, QueryTriggerInteraction.Ignore) ? floor.point.y : 0f;

            Vector2 unit = FrameSize(p); // 缩放 = 1 时的宽高
            float scale = Mathf.Sqrt(targetArea / (unit.x * unit.y));

            Vector3 wallPoint = wall.point;
            for (int attempt = 0; attempt < 40; attempt++, scale *= 0.95f)
            {
                Vector2 size = unit * scale;
                float centerY = floorY + Mathf.Max(PreferredCenter, size.y * 0.5f + MinBottomGap);
                Vector3 pos = new Vector3(wallPoint.x, centerY, wallPoint.z);

                if (WallBehind(pos, p.right, front, size))
                {
                    p.localScale = Vector3.one * scale;
                    p.position = pos;
                    return;
                }
            }

            Debug.LogWarning("[PaintingArranger] " + p.name + " 所在的墙放不下，保持原样。", p);
        }

        /// <summary>画框范围内 5×3 个采样点，每个点正后方都要贴着墙（门洞、墙角、天花之外都算失败）。</summary>
        private static bool WallBehind(Vector3 center, Vector3 right, Vector3 front, Vector2 size)
        {
            for (int i = 0; i <= 4; i++)
            {
                for (int j = 0; j <= 2; j++)
                {
                    Vector3 offset = right * ((i / 4f - 0.5f) * size.x * 0.98f) + Vector3.up * ((j / 2f - 0.5f) * size.y * 0.98f);
                    Vector3 origin = center + offset + front * 0.2f;
                    if (!Physics.Raycast(origin, -front, out RaycastHit hit, 0.35f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        return false;
                    }
                    // 打到的必须是竖直的墙面，而不是楼板 / 天花的侧面
                    if (Vector3.Dot(hit.normal, front) < 0.9f)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>缩放 = 1 时画框的宽、高（本地空间，原点在背面中心）。</summary>
        private static Vector2 FrameSize(Transform p)
        {
            Bounds local = new Bounds();
            bool first = true;
            foreach (MeshFilter mf in p.GetComponentsInChildren<MeshFilter>())
            {
                Bounds mb = mf.sharedMesh.bounds;
                Matrix4x4 toRoot = p.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                foreach (Vector3 c in Corners(mb))
                {
                    Vector3 v = toRoot.MultiplyPoint3x4(c);
                    if (first) { local = new Bounds(v, Vector3.zero); first = false; }
                    else local.Encapsulate(v);
                }
            }
            return new Vector2(local.size.x, local.size.y);
        }

        private static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (int i = 0; i < 8; i++)
            {
                yield return new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
            }
        }

        private static List<Transform> FindPaintings()
        {
            var list = new List<Transform>();
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (t.name.StartsWith("Painting_") && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                {
                    list.Add(t);
                }
            }
            return list;
        }
    }
}
