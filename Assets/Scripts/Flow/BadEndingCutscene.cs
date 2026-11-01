using System;
using System.Collections;
using Ib.Core;
using Ib.Environment;
using Ib.Interaction;
using Ib.Player;
using Ib.Threats;
using Ib.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ib.Flow
{
    /// <summary>
    /// 坏结局 CG（GDD §6.9，2026-11-01 老大需求）：玩家在正门选 Open 以后接管相机——
    /// 退半步让开门板 → 两扇门缓缓打开（MainDoorOpen）→ 镜头推进门后的库房 07 → 自己转身 →
    /// 身后 1–2 米站着一尊哭泣天使 → 静止一拍 → EndingStinger + 同一帧硬切黑屏 → 调 atBlack（结局字幕）。
    /// 由 SceneTransitions.BadEnding 调用，状态已经是 Ending、控制已锁；场景随后重新加载，所以这里改的东西都不复原。
    /// 天使是转身那一刻（相机正对库房、看不到身后）才从场景里那尊天使的攻击外形复制出来的。
    /// </summary>
    internal sealed class BadEndingCutscene : MonoBehaviour
    {
        private const string StorageRootName = "Level_Storage07";
        private const string DoorOpenSfx = "MainDoorOpen";
        private const string StingerSfx = "EndingStinger";

        private const float StepBackSeconds = 0.9f;
        private const float PushSeconds = 3.2f;
        private const float LookHoldSeconds = 0.9f;
        private const float TurnSeconds = 2.3f;
        private const float BeatSeconds = 0.7f;
        private const float BlackHoldSeconds = 2.6f;   // 重击的尾巴在黑屏里散掉，再出字幕

        private DoubleDoor door;
        private GameObject storage;
        private Camera cam;
        private Action atBlack;
        private Light fillLight;

        /// <summary>开始 CG。找不到门 / 库房 / 相机时返回 false（调用方退回普通渐黑）。</summary>
        public static bool Play(Action atBlack)
        {
            DoubleDoor door = FindMainDoor();
            GameObject storage = FindRoot(StorageRootName);
            Camera cam = Camera.main;
            if (door == null || storage == null || cam == null)
            {
                Debug.LogWarning($"[BadEndingCutscene] 缺少 正门={door != null} 库房07={storage != null} 相机={cam != null}，改用普通渐黑。");
                return false;
            }

            var runner = new GameObject("--- BadEndingCutscene ---").AddComponent<BadEndingCutscene>();
            runner.door = door;
            runner.storage = storage;
            runner.cam = cam;
            runner.atBlack = atBlack;
            runner.StartCoroutine(runner.Run());
            return true;
        }

        private IEnumerator Run()
        {
            Transform camT = TakeOverCamera();
            HideThreatsNearDoor();

            // ---- 门的几何：+Z（door.forward）= 馆内，库房在 −Z ----
            Transform root = door.transform;
            Vector3 inward = Flat(root.forward);
            float floorY = root.position.y;
            float eyeY = camT.position.y;
            float leafWidth = DoorLeafWidth();
            Vector3 doorCenter = root.position;
            if (door.LeftHinge != null && door.RightHinge != null)
            {
                doorCenter = (door.LeftHinge.position + door.RightHinge.position) * 0.5f;
            }

            doorCenter.y = eyeY;

            // 1) 退到门板扫不到的地方，正对门
            Vector3 standPos = doorCenter + inward * (leafWidth + 0.6f);
            Quaternion facingDoor = Quaternion.LookRotation(-inward, Vector3.up);
            yield return Move(camT, standPos, facingDoor, StepBackSeconds);

            // 2) 门后换成库房（和黑屋占同一块地，黑屋必须关着），开门
            if (WorldState.BlackRoom != null)
            {
                WorldState.BlackRoom.SetActive(false);
            }

            storage.SetActive(true);
            door.ForceOpen();
            Sfx.PlayAt(DoorOpenSfx, doorCenter, 1f, 40f);

            // 开门的同时往前蹭一点点（人被门吸过去的感觉）
            Vector3 creepPos = doorCenter + inward * (leafWidth + 0.35f);
            float openWait = 0f;
            Vector3 from = camT.position;
            while (!door.IsOpen && openWait < 6f)
            {
                openWait += Time.deltaTime;
                camT.position = Vector3.Lerp(from, creepPos, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(openWait / 3f)));
                yield return null;
            }

            yield return new WaitForSeconds(0.35f);

            // 3) 推进库房：过门槛 2 m 左右，看清通道两侧的木箱和尽头的卷帘
            Vector3 insidePos = doorCenter - inward * 2.0f;
            Quaternion lookIn = Quaternion.LookRotation(-inward, Vector3.up) * Quaternion.Euler(-3f, 0f, 0f);
            yield return Move(camT, insidePos, lookIn, PushSeconds);
            yield return new WaitForSeconds(LookHoldSeconds);

            // 4) 转身。相机此刻正对库房，身后看不见：天使在这时候出现，站在门口、离镜头约 1.6 m
            Vector3 angelFeet = camT.position + inward * 1.6f;
            angelFeet.y = floorY;
            GameObject angel = SpawnAngel(angelFeet, -inward);
            Vector3 lookAt = AngelFace(angel, angelFeet, eyeY);

            Vector3 toFace = lookAt - camT.position;
            float yaw0 = camT.eulerAngles.y;
            float pitch0 = NormalizePitch(camT.eulerAngles.x);
            float yaw1 = Quaternion.LookRotation(Flat(toFace)).eulerAngles.y;
            float pitch1 = -Mathf.Atan2(toFace.y, new Vector2(toFace.x, toFace.z).magnitude) * Mathf.Rad2Deg;
            float turn = Mathf.Repeat(yaw1 - yaw0, 360f);   // 一律向右转

            for (float t = 0f; t < TurnSeconds; t += Time.deltaTime)
            {
                // 先慢后快、最后一下顿住：像是察觉到身后有东西才回头
                float k = Mathf.Clamp01(t / TurnSeconds);
                float e = k < 0.25f ? Mathf.SmoothStep(0f, 0.12f, k / 0.25f) : Mathf.Lerp(0.12f, 1f, Mathf.SmoothStep(0f, 1f, (k - 0.25f) / 0.75f));
                camT.rotation = Quaternion.Euler(Mathf.Lerp(pitch0, pitch1, e), yaw0 + turn * e, 0f);
                yield return null;
            }

            camT.rotation = Quaternion.Euler(pitch1, yaw1, 0f);

            // 5) 静止一拍 → 重击 + 硬切黑
            yield return new WaitForSeconds(BeatSeconds);
            Sfx.PlayUI(StingerSfx, 1f);
            Presentation.FadeToBlack(0f);
            yield return new WaitForSecondsRealtime(BlackHoldSeconds);

            atBlack?.Invoke();
        }

        // ------------------------------------------------------------------ 相机

        /// <summary>停掉玩家的移动 / 转头脚本，把相机从玩家身上摘下来由 CG 直接摆。</summary>
        private Transform TakeOverCamera()
        {
            FirstPersonController player = FindAnyObjectByType<FirstPersonController>();
            if (player != null)
            {
                player.enabled = false;
                foreach (PlayerLook look in player.GetComponentsInChildren<PlayerLook>(true))
                {
                    look.enabled = false;
                }

                // 相机离开身体好几米，身体模型会露出来
                foreach (Renderer r in player.GetComponentsInChildren<Renderer>(true))
                {
                    r.enabled = false;
                }
            }

            PlayerLook.LockCursor(true);   // CG 期间不要露鼠标

            Transform camT = cam.transform;
            camT.SetParent(null, true);

            // 天使是靠手电看见的；没有手电（调试直接跳到这里）就补一盏很弱的灯
            PlayerFlashlight flashlight = PlayerFlashlight.Instance;
            if (flashlight == null || !flashlight.HasFlashlight)
            {
                fillLight = new GameObject("CG_FillLight").AddComponent<Light>();
                fillLight.transform.SetParent(camT, false);
                fillLight.type = LightType.Spot;
                fillLight.spotAngle = 60f;
                fillLight.range = 9f;
                fillLight.intensity = 6f;
                fillLight.color = new Color(0.85f, 0.88f, 1f);
            }

            return camT;
        }

        private void Update()
        {
            // 手电保持亮着、不闪（玩家按 F、天使脚本留下的闪烁都不算）
            PlayerFlashlight flashlight = PlayerFlashlight.Instance;
            if (flashlight != null && flashlight.HasFlashlight)
            {
                flashlight.ForceOn();
            }
        }

        private static IEnumerator Move(Transform t, Vector3 toPos, Quaternion toRot, float seconds)
        {
            Vector3 p0 = t.position;
            Quaternion r0 = t.rotation;
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                float e = Mathf.SmoothStep(0f, 1f, time / seconds);
                t.SetPositionAndRotation(Vector3.Lerp(p0, toPos, e), Quaternion.Slerp(r0, toRot, e));
                yield return null;
            }

            t.SetPositionAndRotation(toPos, toRot);
        }

        // ------------------------------------------------------------------ 天使

        /// <summary>真的天使和门口附近的人台先藏起来，免得转身时画面里多出一尊。</summary>
        private void HideThreatsNearDoor()
        {
            foreach (WeepingAngel a in FindObjectsByType<WeepingAngel>())
            {
                a.gameObject.SetActive(false);
            }

            Vector3 c = door.transform.position;
            foreach (MannequinPatrol m in FindObjectsByType<MannequinPatrol>())
            {
                if ((m.transform.position - c).sqrMagnitude < 12f * 12f)
                {
                    m.gameObject.SetActive(false);
                }
            }

            foreach (PaintingFigure f in FindObjectsByType<PaintingFigure>())
            {
                if ((f.transform.position - c).sqrMagnitude < 12f * 12f)
                {
                    f.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>复制场景里天使的攻击外形（只要模型，不要脚本和碰撞），脚放在 feet，面朝 facing。</summary>
        private static GameObject SpawnAngel(Vector3 feet, Vector3 facing)
        {
            var rootGo = new GameObject("CG_WeepingAngel");
            Transform root = rootGo.transform;
            root.SetPositionAndRotation(feet, Quaternion.LookRotation(Flat(facing), Vector3.up));

            WeepingAngel source = FindAnyObjectByType<WeepingAngel>(FindObjectsInactive.Include);
            Transform visual = source != null ? FindVisual(source.transform) : null;
            if (visual == null)
            {
                Debug.LogWarning("[BadEndingCutscene] 场景里找不到天使外形，CG 里用一块石柱代替。");
                GameObject stub = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Destroy(stub.GetComponent<Collider>());
                stub.transform.SetParent(root, false);
                stub.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                stub.transform.localScale = new Vector3(0.7f, 1.2f, 0.7f);
                return rootGo;
            }

            root.localScale = source.transform.lossyScale;
            GameObject copy = Instantiate(visual.gameObject, root, false);
            copy.transform.localPosition = visual.localPosition;
            copy.transform.localRotation = visual.localRotation;
            copy.transform.localScale = visual.localScale;
            copy.SetActive(true);

            foreach (Collider col in copy.GetComponentsInChildren<Collider>(true))
            {
                Destroy(col);
            }

            foreach (MonoBehaviour mb in copy.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Destroy(mb);
            }

            foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
            }

            return rootGo;
        }

        private static Transform FindVisual(Transform angel)
        {
            foreach (string name in new[] { "Visual_Attack", "Visual_Peek", "Visual_Still" })
            {
                Transform t = angel.Find(name);
                if (t != null)
                {
                    return t;
                }
            }

            return null;
        }

        /// <summary>镜头最后看的点：天使脸附近（包围盒上部），但不高过太多，免得 7 岁视角仰成看天花板。</summary>
        private static Vector3 AngelFace(GameObject angel, Vector3 feet, float eyeY)
        {
            Renderer[] renderers = angel.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Vector3(feet.x, eyeY + 0.4f, feet.z);
            }

            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers)
            {
                b.Encapsulate(r.bounds);
            }

            float faceY = b.min.y + b.size.y * 0.82f;
            return new Vector3(b.center.x, faceY, b.center.z);
        }

        // ------------------------------------------------------------------ 找东西

        private static DoubleDoor FindMainDoor()
        {
            MuseumEntrance entrance = FindAnyObjectByType<MuseumEntrance>(FindObjectsInactive.Include);
            DoubleDoor door = entrance != null ? entrance.GetComponent<DoubleDoor>() : null;
            if (door == null)
            {
                GameObject go = GameObject.Find("MainDoor_Double");
                door = go != null ? go.GetComponent<DoubleDoor>() : null;
            }

            return door;
        }

        private static GameObject FindRoot(string name)
        {
            foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name == name)
                {
                    return go;
                }
            }

            return null;
        }

        private float DoorLeafWidth()
        {
            if (door.LeftHinge != null && door.RightHinge != null)
            {
                return Vector3.Distance(door.LeftHinge.position, door.RightHinge.position) * 0.5f;
            }

            return 2.1f;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        private static float NormalizePitch(float x) => x > 180f ? x - 360f : x;
    }
}
