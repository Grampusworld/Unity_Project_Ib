using Ib.Flow;
using UnityEngine;

namespace Ib.Interaction
{
    /// <summary>
    /// 厕所里的手电（GDD §3.6 第 11–12 步）。
    /// 停电前不出现。停电后刷新在原地，并且是打开的：光束朝厕所门外（最长的那条无遮挡方向，也就是门洞），
    /// 玩家一上二楼就能看见光从厕所打出来。按 E 拿起后物体和这盏灯一起关掉，馆藏由 HasFlashlight 出动。
    /// 手电就躺在厕所地上（离门洞约 2 m）；旁边还有一盏小的暖色点光，把厕所门口整个照亮，楼梯口一抬头就能看见。
    /// 全馆关灯不会关这两盏（MuseumLights 会跳过它们）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlashlightPickup : MonoBehaviour, IInteractable
    {
        public string Prompt => "Take";

        public InteractKind Kind => InteractKind.Pickup;

        public bool CanInteract => Shown && !GameFlow.HasFlashlight && GameFlow.State == GameState.PowerOut;

        private bool Shown =>
            !GameFlow.HasFlashlight
            && GameFlow.State >= GameState.PowerOut
            && GameFlow.State != GameState.Ending;

        private Light beam;
        private Light glow;
        private Renderer[] renderers;
        private Collider[] colliders;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);

            Transform existing = transform.Find("Beam");
            GameObject go = existing != null ? existing.gameObject : new GameObject("Beam");
            go.transform.SetParent(transform, false);
            go.transform.position = transform.position + Vector3.up * BeamHeight;
            beam = go.GetComponent<Light>();
            if (beam == null)
            {
                beam = go.AddComponent<Light>();
            }

            beam.type = LightType.Spot;
            beam.color = new Color(1f, 0.95f, 0.82f);
            // 楼梯口离厕所门 20 多米、门又是斜着对楼梯：80 在楼梯口完全看不见，300 才能在长廊尽头的墙和天花上看出一片暖光
            beam.intensity = 300f;
            beam.range = 30f;
            beam.spotAngle = 68f;
            beam.innerSpotAngle = 36f;
            beam.shadows = LightShadows.Soft;
            beam.shadowNormalBias = 0.3f;
            beam.enabled = false;

            Transform glowExisting = transform.Find("Glow");
            GameObject glowGo = glowExisting != null ? glowExisting.gameObject : new GameObject("Glow");
            glowGo.transform.SetParent(transform, false);
            glowGo.transform.position = transform.position + Vector3.up * 0.5f;
            glow = glowGo.GetComponent<Light>();
            if (glow == null)
            {
                glow = glowGo.AddComponent<Light>();
            }

            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.8f, 0.55f);
            glow.intensity = 8f;
            glow.range = 8f;
            glow.shadows = LightShadows.Hard; // 不开阴影会穿墙照到长廊
            glow.enabled = false;
        }

        private const float BeamHeight = 0.15f;
        private const float BeamTilt = 10f;

        private void OnEnable()
        {
            GameFlow.StateChanged += OnStateChanged;
            GameFlow.FlashlightObtained += Apply;
        }

        private void Start() => Apply();

        private void OnDisable()
        {
            GameFlow.StateChanged -= OnStateChanged;
            GameFlow.FlashlightObtained -= Apply;
        }

        private void OnStateChanged(GameState from, GameState to) => Apply();

        private void Apply()
        {
            bool show = Shown;
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                    {
                        renderers[i].enabled = show;
                    }
                }
            }

            if (colliders != null)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null)
                    {
                        colliders[i].enabled = show;
                    }
                }
            }

            if (beam == null)
            {
                return;
            }

            if (show)
            {
                AimOutTheDoor();
            }

            beam.enabled = show;
            if (glow != null)
            {
                glow.enabled = show;
            }
        }

        /// <summary>朝厕所里射得最远的水平方向打出去。门洞是唯一的缺口，光就会从门里漏到长廊。</summary>
        private void AimOutTheDoor()
        {
            Vector3 origin = transform.position + Vector3.up * BeamHeight;
            Vector3 bestDir = Vector3.left;
            float best = -1f;
            for (int yaw = -80; yaw <= 80; yaw += 5)
            {
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.left;
                float dist = 20f;
                if (Physics.Raycast(origin, dir, out RaycastHit hit, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    dist = hit.distance;
                }

                if (dist > best)
                {
                    best = dist;
                    bestDir = dir;
                }
            }

            // 略微上抬：光打到长廊对面的墙和天花上，远处（楼梯口）也看得见，而不只是贴地一片
            beam.transform.rotation = Quaternion.LookRotation(bestDir, Vector3.up) * Quaternion.Euler(-BeamTilt, 0f, 0f);
        }

        public void Interact(PlayerInteractor interactor)
        {
            SceneTransitions.TakeFlashlight();
            Apply();
        }
    }
}
