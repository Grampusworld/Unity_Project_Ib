using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ib.Player
{
    /// <summary>
    /// 主角的脚步声：接 FirstPersonController.OnFootstep（按走过的距离触发，所以跑起来步点自然更密）。
    /// 判断脚下是什么地面：一层整块地板是木地板碰撞体，石砖（接待厅、雕塑厅、楼梯厅、厕所）和地毯只是贴在上面、没有碰撞体的薄片，
    /// 所以先看脚下有没有这些薄片（按包围盒），再看射线打到的碰撞体材质：Parquet / Wood = 木，楼梯一律算石砖，其余也算石砖。
    /// 跑步更响、蹲走很轻。纯音效，馆藏听不见（不改玩法）。
    /// 剪辑在 Resources/Audio/Step_{Wood,Stone,Carpet}_0..2（CC0 录音，见同目录 LICENSES.md）。场景加载后自己挂到玩家身上。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFootsteps : MonoBehaviour
    {
        private const float WalkVolume = 0.45f;
        private const float RunVolume = 0.8f;
        private const float CrouchVolume = 0.15f;
        private const float RunSpeed = 4.2f;   // 水平速度超过这个算跑（走 3.2、跑 5.4）

        private enum Surface { Stone, Wood, Carpet }

        private FirstPersonController player;
        private AudioSource source;
        private AudioClip[][] clips;
        private int last = -1;
        private readonly System.Collections.Generic.List<(Bounds bounds, Surface surface)> overlays =
            new System.Collections.Generic.List<(Bounds, Surface)>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive)
            {
                return;
            }

            FirstPersonController fpc = FindAnyObjectByType<FirstPersonController>();
            if (fpc != null && fpc.GetComponent<PlayerFootsteps>() == null)
            {
                fpc.gameObject.AddComponent<PlayerFootsteps>();
            }
        }

        private void Awake()
        {
            player = GetComponent<FirstPersonController>();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            clips = new AudioClip[3][];
            string[] names = { "Stone", "Wood", "Carpet" };
            for (int s = 0; s < names.Length; s++)
            {
                clips[s] = new AudioClip[3];
                for (int i = 0; i < 3; i++)
                {
                    clips[s][i] = Resources.Load<AudioClip>("Audio/Step_" + names[s] + "_" + i);
                }
            }

            // 贴在地板上的薄片（石砖、地毯），场景静态，只收集一次
            foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>())
            {
                string mat = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                bool carpet = mat.Contains("Carpet");
                if ((carpet || mat.Contains("Stone_Tile")) && r.bounds.size.y < 0.05f && r.GetComponent<Collider>() == null)
                {
                    overlays.Add((r.bounds, carpet ? Surface.Carpet : Surface.Stone));
                }
            }
        }

        private void OnEnable()
        {
            if (player != null)
            {
                player.OnFootstep += Step;
            }
        }

        private void OnDisable()
        {
            if (player != null)
            {
                player.OnFootstep -= Step;
            }
        }

        private void Step()
        {
            AudioClip[] set = clips[(int)SurfaceUnderfoot()];
            int i = Random.Range(0, set.Length);
            if (i == last && set.Length > 1)
            {
                i = (i + 1) % set.Length;   // 不连续两步同一个声音
            }

            last = i;
            if (set[i] == null)
            {
                return;
            }

            float volume = player.IsCrouching ? CrouchVolume : player.HorizontalSpeed > RunSpeed ? RunVolume : WalkVolume;
            source.pitch = Random.Range(0.92f, 1.08f);
            source.PlayOneShot(set[i], volume * Random.Range(0.85f, 1f));
        }

        private Surface SurfaceUnderfoot()
        {
            Vector3 feet = transform.position;
            Surface? found = null;
            foreach ((Bounds b, Surface surface) in overlays)
            {
                if (feet.x >= b.min.x && feet.x <= b.max.x && feet.z >= b.min.z && feet.z <= b.max.z && Mathf.Abs(feet.y - b.max.y) < 0.3f)
                {
                    // 地毯铺在石砖上面时以地毯为准
                    if (surface == Surface.Carpet)
                    {
                        return Surface.Carpet;
                    }

                    found = surface;
                }
            }

            if (found.HasValue)
            {
                return found.Value;
            }

            Vector3 origin = feet + Vector3.up * 0.3f;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return Surface.Stone;
            }

            // 楼梯（Stairs/Flight_*/Step_*）一律算石砖
            for (Transform t = hit.collider.transform; t != null; t = t.parent)
            {
                if (t.name.StartsWith("Stair") || t.name.StartsWith("Flight"))
                {
                    return Surface.Stone;
                }
            }

            Renderer r = hit.collider.GetComponent<Renderer>();
            string mat = r != null && r.sharedMaterial != null ? r.sharedMaterial.name : "";
            if (mat.Contains("Carpet"))
            {
                return Surface.Carpet;
            }

            if (mat.Contains("Parquet") || mat.Contains("Wood"))
            {
                return Surface.Wood;
            }

            return Surface.Stone;
        }
    }
}
