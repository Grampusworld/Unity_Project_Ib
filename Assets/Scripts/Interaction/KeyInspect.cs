using System;
using System.Collections;
using Ib.Core;
using Ib.Flow;
using Ib.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ib.Interaction
{
    /// <summary>
    /// 拿钥匙时的检视（老大 2026-11-01）：锁控制（右下角按键提示跟着藏），钥匙从抽屉里匀速飞到屏幕正中间、
    /// 转成纸签朝着镜头；之后按住鼠标拖动旋转，E 或 Esc 收下（屏幕底部一行小字提示）。收下后钥匙消失、进背包（onTaken）。
    /// 模型是 Resources/Props/MainDoorKey（MainDoorKeyBuilder 生成）。检视时钥匙旁边挂一盏很弱的暖光，停电后也看得清纸签。
    /// </summary>
    public sealed class KeyInspect : MonoBehaviour
    {
        private const string PrefabPath = "Props/MainDoorKey";
        private const string HintText = "Drag to rotate  ·  E to take";

        [SerializeField, Min(0.05f)] private float flySpeed = 0.9f;
        [SerializeField, Min(0.1f)] private float holdDistance = 0.2f;
        [SerializeField, Min(0.01f)] private float dragDegreesPerPixel = 0.35f;

        private Transform eye;
        private Transform key;
        private Action onTaken;
        private bool taking;

        public static bool Begin(Vector3 from, Quaternion fromRotation, Action onTaken)
        {
            Camera cam = Camera.main;
            GameObject prefab = Resources.Load<GameObject>(PrefabPath);
            if (cam == null || prefab == null)
            {
                Debug.LogWarning("[KeyInspect] 缺少主相机或 Resources/" + PrefabPath + "，直接拿钥匙。");
                onTaken?.Invoke();
                return false;
            }

            var go = new GameObject("--- Key Inspect ---");
            KeyInspect k = go.AddComponent<KeyInspect>();
            k.eye = cam.transform;
            k.onTaken = onTaken;
            k.key = Instantiate(prefab, from, fromRotation).transform;
            k.key.SetParent(go.transform, true);

            Light glow = new GameObject("InspectLight").AddComponent<Light>();
            glow.transform.SetParent(go.transform, false);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.88f, 0.72f);
            glow.intensity = 0.25f;
            glow.range = 0.9f;
            glow.shadows = LightShadows.None;
            k.StartCoroutine(k.Run(glow.transform));
            return true;
        }

        /// <summary>正对镜头的姿态：钥匙横放（头朝右），纸签那一面（+X）朝镜头，稍微往下倾一点。</summary>
        private Quaternion Facing => eye.rotation * Quaternion.AngleAxis(-8f, Vector3.right) * Quaternion.Euler(0f, 90f, 0f);

        /// <summary>整把钥匙（连纸签）的包围盒中心落在屏幕正中：原点在领口，要按中心的本地偏移倒推。</summary>
        private Vector3 HoldPoint => eye.position + eye.forward * holdDistance - Facing * localCenter;

        private Vector3 localCenter;

        private IEnumerator Run(Transform glow)
        {
            GameFlow.SetInputLocked(true);
            Presentation.IsInspecting = true;

            localCenter = key.InverseTransformPoint(KeyCenter());

            // 1) 匀速飞到屏幕中间（朝向按同样的进度转过来）
            Vector3 start = key.position;
            Quaternion startRot = key.rotation;
            float distance = Vector3.Distance(start, HoldPoint);
            float seconds = Mathf.Max(0.25f, distance / flySpeed);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                key.SetPositionAndRotation(Vector3.Lerp(start, HoldPoint, k), Quaternion.Slerp(startRot, Facing, Mathf.SmoothStep(0f, 1f, k)));
                glow.position = eye.position + eye.up * 0.12f + eye.right * 0.1f;
                yield return null;
            }

            key.SetPositionAndRotation(HoldPoint, Facing);
            ScreenVeil.Hint(HintText);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // 2) 检视：按住左键拖动旋转；E / Esc 收下
            yield return null;
            while (!taking)
            {
                Keyboard kb = Keyboard.current;
                Mouse mouse = Mouse.current;
                if (kb != null && (kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                {
                    taking = true;
                    break;
                }

                if (mouse != null && mouse.leftButton.isPressed)
                {
                    Vector2 d = mouse.delta.ReadValue() * dragDegreesPerPixel;
                    // 绕包围盒中心转（纸签挂在下面，绕原点转会甩出画面）
                    Vector3 center = KeyCenter();
                    key.RotateAround(center, eye.up, -d.x);
                    key.RotateAround(center, eye.right, d.y);
                }

                glow.position = eye.position + eye.up * 0.12f + eye.right * 0.1f;
                yield return null;
            }

            // 3) 收下：往下收出画面，然后进背包
            ScreenVeil.Hint("");
            Sfx.PlayAt(Sfx.KeyClick, key.position, 0.6f, 5f);
            Vector3 from = key.position;
            Vector3 to = eye.position + eye.forward * 0.25f - eye.up * 0.25f;
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                key.position = Vector3.Lerp(from, to, t / 0.3f);
                yield return null;
            }

            Presentation.IsInspecting = false;
            GameFlow.SetInputLocked(false);
            onTaken?.Invoke();
            Destroy(gameObject);
        }

        private Vector3 KeyCenter()
        {
            Bounds b = default;
            bool any = false;
            foreach (Renderer r in key.GetComponentsInChildren<Renderer>())
            {
                if (!any)
                {
                    b = r.bounds;
                    any = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            return any ? b.center : key.position;
        }

        private void OnDestroy()
        {
            // 场景被重新加载（退回主菜单）时也别把检视状态留下
            if (Presentation.IsInspecting && !taking)
            {
                Presentation.IsInspecting = false;
            }
        }
    }
}
