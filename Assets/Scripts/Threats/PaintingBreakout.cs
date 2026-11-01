using System.Collections;
using System.Collections.Generic;
using Ib.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ib.Threats
{
    /// <summary>
    /// 名画人物「破框而出」的现场，挂在二楼画框的位置（由菜单 Tools/Ib/布置名画人物破框 生成）。
    /// 出场一帧先播 <see cref="Sfx.GlassBreak"/>，delaySeconds 后把完整画布和金框藏起来、在原位放碎框。
    /// 碎框优先用美术给的 <c>Assets/Art/Models/Props/BrokenFrame.fbx</c>（原点在框背面中心 = 几何中心，正面朝 +Z，
    /// 所以本站点就摆在画框中心的原地、局部 +Z 指向展厅，跟美术的约定一致）；资产没到的时候用
    /// <see cref="BuildPlaceholder"/> 现场拆一个占位碎框，不改任何 Assets/Art 资源，也不留碰撞体。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaintingBreakout : MonoBehaviour
    {
        [Tooltip("画布板本体（场景里那面带画的金框里的画）。破框时藏起来。")]
        [SerializeField] private GameObject intactCanvas;

        [Tooltip("画布外面的四条金框（Masterpiece_*_EmptyFrame*_Frame）。破框时藏起来。")]
        [SerializeField] private GameObject intactFrame;

        [Tooltip("美术给的碎框 Prefab/FBX。为空时用占位碎框。")]
        [SerializeField] private GameObject brokenPrefab;

        [Tooltip("占位碎框的玻璃用这个材质（只读引用，不改）。为空时现场建一个。")]
        [SerializeField] private Material brokenGlassMaterial;

        [Header("时机")]
        [Tooltip("玻璃破碎声之后多久换成碎框。0 = 同一帧。")]
        [SerializeField, Min(0f)] private float delaySeconds;

        [Header("音效")]
        [SerializeField, Range(0f, 1f)] private float glassVolume = 1f;
        [SerializeField, Min(1f)] private float glassMaxDistance = 30f;

        [Header("占位")]
        [Tooltip("没有碎框资产时，用现有画框现场拆一个。")]
        [SerializeField] private bool placeholderIfMissing = true;

        private bool triggered;
        private bool shattered;
        private GameObject spawned;
        private readonly List<Material> ownedMaterials = new List<Material>();

        public bool IsBroken => shattered;

        /// <summary>碎框资产是不是已经有人给了（美术交付后自动用上）。</summary>
        public bool HasBrokenPrefab => brokenPrefab != null;

        /// <summary>破框：先响，delaySeconds 之后藏完整画、放碎框。重复调用只响一次。</summary>
        public void Break() => Break(1f, false);

        /// <summary>
        /// 破框。glassScale 乘在玻璃声音量上（伏击时尖叫自带爆裂，玻璃声压低）；immediate = 同一帧换碎框，不等 delaySeconds。
        /// </summary>
        public void Break(float glassScale, bool immediate)
        {
            if (triggered)
            {
                return;
            }

            triggered = true;
            if (glassScale > 0.001f)
            {
                Sfx.PlayAt(Sfx.GlassBreak, transform.position, glassVolume * glassScale, glassMaxDistance);
            }

            if (immediate || delaySeconds <= 0.001f)
            {
                Shatter();
            }
            else
            {
                StartCoroutine(ShatterAfter(delaySeconds));
            }
        }

        /// <summary>复位（玩家读档重来）：碎框收掉，完整画和金框回来。</summary>
        public void Restore()
        {
            StopAllCoroutines();
            triggered = false;
            shattered = false;

            if (spawned != null)
            {
                Destroy(spawned);
                spawned = null;
            }

            for (int i = 0; i < ownedMaterials.Count; i++)
            {
                if (ownedMaterials[i] != null)
                {
                    Destroy(ownedMaterials[i]);
                }
            }

            ownedMaterials.Clear();

            if (intactCanvas != null)
            {
                intactCanvas.SetActive(true);
            }

            if (intactFrame != null)
            {
                intactFrame.SetActive(true);
            }
        }

        [ContextMenu("试破框")]
        private void TryBreak()
        {
            Restore();
            Break();
        }

        private IEnumerator ShatterAfter(float seconds)
        {
            // 用真实时间：出场这一步可能正好撞上 GameOver 把 timeScale 归零
            yield return new WaitForSecondsRealtime(seconds);
            Shatter();
        }

        private void Shatter()
        {
            if (shattered)
            {
                return;
            }

            shattered = true;

            if (intactCanvas != null)
            {
                intactCanvas.SetActive(false);
            }

            if (intactFrame != null)
            {
                intactFrame.SetActive(false);
            }

            if (brokenPrefab != null)
            {
                // 碎框原点在框背面中心，本站点就在画框中心的原地；rotation 原样给，模型的 +Z 正好朝展厅
                spawned = Instantiate(brokenPrefab, transform.position, transform.rotation, transform.parent);
                spawned.name = brokenPrefab.name + "_Broken";
            }
            else if (placeholderIfMissing)
            {
                spawned = BuildPlaceholder();
            }
        }

        // ── 占位碎框 ────────────────────────────────────────────────────────────
        // 局部坐标：原点 = 框背面中心（跟美术的 BrokenFrame 一致），X = 框宽左右，Y = 朝上，+Z = 展厅方向。
        private GameObject BuildPlaceholder()
        {
            GameObject root = new GameObject(name + "_BrokenFrame(placeholder)");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            Vector2 size = BoardSize();
            float w = size.x;
            float h = size.y;
            const float BarW = 0.14f;
            const float BarD = 0.09f;

            Material wood = CloneMaterial(intactFrame, new Color(0.72f, 0.58f, 0.34f));
            Material canvasMat = CloneMaterial(intactCanvas, new Color(0.52f, 0.48f, 0.44f));
            Material glass = GlassMaterial();

            System.Random rng = new System.Random(name.GetHashCode());

            // 四条框：被打断、歪斜、脱开，往展厅方向（−Z）翻出来一点
            Piece(root.transform, "Frame_Top",
                new Vector3(0.05f, h * 0.5f + BarW * 0.5f + 0.05f, 0.05f), new Vector3(0f, 0f, -13f),
                new Vector3(w + BarW * 2f, BarW, BarD), wood, true);
            Piece(root.transform, "Frame_Bottom",
                new Vector3(0.16f, -(h * 0.5f + BarW * 0.5f) - 0.01f, 0.04f), new Vector3(0f, 0f, 6f),
                new Vector3(w + BarW * 2f, BarW, BarD), wood, true);
            Piece(root.transform, "Frame_Left",
                new Vector3(-(w * 0.5f + BarW * 0.5f) - 0.03f, -h * 0.05f, 0.06f), new Vector3(0f, 0f, 8f),
                new Vector3(BarW, h, BarD), wood, true);
            Piece(root.transform, "Frame_Right",
                new Vector3(w * 0.5f + BarW * 0.5f + 0.09f, 0f, 0.13f), new Vector3(0f, 0f, -17f),
                new Vector3(BarW, h, BarD), wood, true);

            // 撕开的画布：上沿挂着几片、下沿积着几片，都不是平的
            for (int i = 0; i < 6; i++)
            {
                bool top = i < 3;
                float x = Range(rng, -w * 0.32f, w * 0.32f);
                float y = top ? h * 0.5f - Range(rng, 0.12f, 0.5f) : -h * 0.5f + Range(rng, 0.1f, 0.4f);
                Vector3 s = new Vector3(Range(rng, 0.25f, 0.6f), Range(rng, 0.25f, 0.7f), 0.012f);
                Vector3 e = new Vector3(Range(rng, -8f, 8f), Range(rng, -25f, 25f), Range(rng, -30f, 30f));
                Piece(root.transform, "Canvas_Tear_" + i, new Vector3(x, y, 0.04f + 0.012f * i), e, s, canvasMat, false);
            }

            // 挂在框边的碎玻璃
            for (int i = 0; i < 8; i++)
            {
                Vector3 s = new Vector3(Range(rng, 0.03f, 0.11f), Range(rng, 0.03f, 0.13f), 0.012f);
                Piece(root.transform, "Glass_Edge_" + i,
                    new Vector3(Range(rng, -w * 0.6f, w * 0.6f), Range(rng, -h * 0.5f + 0.05f, h * 0.5f - 0.05f), Range(rng, 0.04f, 0.24f)),
                    new Vector3(Range(rng, -40f, 40f), Range(rng, -40f, 40f), Range(rng, -60f, 60f)), s, glass, false);
            }

            // 掉到地上的碎屑：框底下往外散开一片
            float floorY = FloorLocalY();
            for (int i = 0; i < 16; i++)
            {
                float edge = Range(rng, 0f, 1f);
                float x = Range(rng, -w * 0.75f, w * 0.75f);
                float z = Mathf.Lerp(0.15f, 1.7f, edge * edge);
                Vector3 s = new Vector3(Range(rng, 0.03f, 0.13f), 0.015f, Range(rng, 0.03f, 0.13f));
                Piece(root.transform, "Glass_Floor_" + i,
                    new Vector3(x, floorY + 0.008f, z),
                    new Vector3(0f, Range(rng, 0f, 360f), Range(rng, -6f, 6f)), s, glass, false);
            }

            return root;
        }

        /// <summary>画布板的实际尺寸：库里的板是被缩放的 cube，宽在 Z、高在 Y。</summary>
        private Vector2 BoardSize()
        {
            if (intactCanvas != null)
            {
                Vector3 s = intactCanvas.transform.lossyScale;
                if (s.z > 0.1f && s.y > 0.1f)
                {
                    return new Vector2(s.z, s.y);
                }
            }

            return new Vector2(1.6f, 2f);
        }

        private float FloorLocalY()
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 20f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return transform.InverseTransformPoint(hit.point).y;
            }

            return -2.2f;   // 二楼地板 6.4、画框中心 8.6
        }

        private GameObject Piece(Transform parent, string pieceName, Vector3 pos, Vector3 euler, Vector3 size, Material mat, bool shadow)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = pieceName;

            // 碎块不要有碰撞体：不挡玩家、不进导航
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = size;

            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                if (mat != null)
                {
                    r.sharedMaterial = mat;
                }

                r.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = shadow;
            }

            return go;
        }

        private Material CloneMaterial(GameObject source, Color tint)
        {
            Material baseMat = null;
            if (source != null)
            {
                Renderer r = source.GetComponentInChildren<Renderer>(true);
                if (r != null)
                {
                    baseMat = r.sharedMaterial;
                }
            }

            if (baseMat == null)
            {
                return null;
            }

            Material copy = new Material(baseMat) { name = baseMat.name + "(broken)" };
            Tint(copy, tint);
            ownedMaterials.Add(copy);
            return copy;
        }

        private Material GlassMaterial()
        {
            if (brokenGlassMaterial != null)
            {
                Material copy = new Material(brokenGlassMaterial) { name = brokenGlassMaterial.name + "(broken)" };
                ownedMaterials.Add(copy);
                return copy;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                return null;
            }

            Material glass = new Material(shader) { name = "BrokenGlass(placeholder)" };
            Tint(glass, new Color(0.76f, 0.85f, 0.9f));
            if (glass.HasProperty("_Metallic"))
            {
                glass.SetFloat("_Metallic", 0.2f);
            }

            if (glass.HasProperty("_Smoothness"))
            {
                glass.SetFloat("_Smoothness", 0.88f);
            }

            ownedMaterials.Add(glass);
            return glass;
        }

        private static void Tint(Material m, Color color)
        {
            if (m.HasProperty("_BaseColor"))
            {
                m.SetColor("_BaseColor", color);
            }
            else if (m.HasProperty("_Color"))
            {
                m.SetColor("_Color", color);
            }
        }

        private static float Range(System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }
    }
}
