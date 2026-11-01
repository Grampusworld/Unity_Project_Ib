using System.Collections;
using System.IO;
using Ib.Core;
using Ib.Flow;
using Ib.Player;
using Ib.Threats;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Ib.EditorTools
{
    /// <summary>
    /// 临时自检：进 Play → 关主菜单 → 拿手电切停电 → 等破框 → 把人搬到镜头前做一次 jump scare 并连拍。
    /// 只用于开发验证，不进构建。
    /// </summary>
    [InitializeOnLoad]
    public static class PaintingFigureSelfTest
    {
        private const string PendingKey = "Ib.SelfTest.Pending";
        public const string LogPath = "/tmp/ib_selftest.log";

        static PaintingFigureSelfTest()
        {
            if (SessionState.GetBool(PendingKey, false))
            {
                EditorApplication.delayCall += TryStart;
            }
        }

        [MenuItem("Tools/Ib/自检名画人物", priority = 31)]
        public static void Run()
        {
            File.WriteAllText(LogPath, "[自检] 开始\n");
            SessionState.SetBool(PendingKey, true);
            if (EditorApplication.isPlaying)
            {
                TryStart();
            }
            else
            {
                EditorApplication.EnterPlaymode();
            }
        }

        private static void TryStart()
        {
            SessionState.SetBool(PendingKey, false);
            if (!Application.isPlaying || GameObject.Find("IbSelfTest") != null)
            {
                return;
            }

            new GameObject("IbSelfTest").AddComponent<SelfTestRunner>();
        }
    }

    public sealed class SelfTestRunner : MonoBehaviour
    {
        private static void Log(string line)
        {
            File.AppendAllText(PaintingFigureSelfTest.LogPath, line + "\n");
            Debug.Log("[自检] " + line);
        }

        private void Start()
        {
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            Application.runInBackground = true;
            Time.timeScale = 1f;
            yield return new WaitForSecondsRealtime(0.8f);

            GameObject menu = GameObject.Find("MainMenu");
            if (menu != null)
            {
                menu.SetActive(false);
            }

            GameFlow.StartNewGame();
            GameFlow.GiveFlashlight();
            GameFlow.SetState(GameState.PowerOut);
            Log($"给了手电 CollectionOut={ThreatSchedule.CollectionOut}");

            yield return new WaitForSecondsRealtime(3.5f);

            PaintingFigure target = PaintingFigure.All.Count > 0 ? PaintingFigure.All[0] : null;
            FirstPersonController pc = FindAnyObjectByType<FirstPersonController>();
            if (target == null || pc == null)
            {
                Log("缺人（没有 PaintingFigure 或 FirstPersonController），结束");
                UnityEditor.EditorApplication.ExitPlaymode();
                yield break;
            }

            Vector3 figPos = target.transform.position;
            if (NavMesh.SamplePosition(figPos - target.transform.forward * 2.5f, out NavMeshHit spot, 3f, NavMesh.AllAreas))
            {
                pc.transform.position = spot.position + Vector3.up * 0.05f;
                pc.transform.rotation = Quaternion.LookRotation(figPos - spot.position);
                Log($"玩家就位 {pc.transform.position.ToString("F2")}");
            }

            yield return new WaitForSecondsRealtime(0.8f);

            Camera cam = pc.GetComponentInChildren<Camera>();

            // 先站到它正面 2 m 处看一眼：确认它在这个距离是能画出来的
            Vector3 figNow = target.transform.position;
            if (NavMesh.SamplePosition(figNow + target.transform.forward * 3f, out NavMeshHit fr, 3f, NavMesh.AllAreas))
            {
                pc.transform.position = fr.position + Vector3.up * 0.05f;
                pc.transform.rotation = Quaternion.LookRotation(figNow - fr.position);
                yield return new WaitForSecondsRealtime(0.6f);
                Vector3 vp = cam.WorldToViewportPoint(new Vector3(target.transform.position.x, BoundsTop(target.transform) - 0.3f, target.transform.position.z));
                Log($"正面看：相机={cam.transform.position.ToString("F2")} 人物={target.transform.position.ToString("F2")} 视口脸={vp.ToString("F2")}");
                yield return StartCoroutine(Capture("_front"));
            }
            Log($"跳吓前：相机={(cam != null ? cam.transform.position.ToString("F2") : "无")} 近裁={(cam != null ? cam.nearClipPlane.ToString("F2") : "-")} "
                + $"人物={target.transform.position.ToString("F2")}");
            Log($"跳吓前：bounds={BoundsOf(target.transform)}");

            AngelJumpScare.Play(target.transform, Sfx.AngelScream, 0.85f, 0.3f, 58f, null);

            float[] shots = { 0.34f, 0.60f, 0.86f, 1.12f };
            float clock = 0f;
            for (int i = 0; i < shots.Length; i++)
            {
                yield return new WaitForSecondsRealtime(shots[i] - clock);
                clock = shots[i];
                if (i == 1)
                {
                    Transform girl = target.transform.Find("Visual_Girl");
                    if (girl != null)
                    {
                        Vector3 head = new Vector3(girl.position.x, BoundsTop(target.transform), girl.position.z);
                        Vector3 vpHead = cam.WorldToViewportPoint(head);
                        Vector3 vpRoot = cam.WorldToViewportPoint(girl.position);
                        Log($"  诊断 girlVis={girl.position.ToString("F2")} active={girl.gameObject.activeInHierarchy} "
                            + $"camForward={cam.transform.forward.ToString("F2")} 视口头={vpHead.ToString("F2")} 视口脚={vpRoot.ToString("F2")}");
                        foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
                        {
                            Log($"  渲染器 {r.name} enabled={r.enabled} active={r.gameObject.activeInHierarchy} "
                                + $"layer={LayerMask.LayerToName(r.gameObject.layer)} bounds={r.bounds.ToString("F1")}");
                        }

                        Log($"  相机 cullingMask={cam.cullingMask} near={cam.nearClipPlane:F2} far={cam.farClipPlane:F0}");
                    }

                    Log($"演出中：相机={cam.transform.position.ToString("F2")} 人物={target.transform.position.ToString("F2")} "
                        + $"rot={target.transform.eulerAngles.ToString("F1")} fov={cam.fieldOfView:F0} bounds={BoundsOf(target.transform)}");
                }

                yield return StartCoroutine(Capture("_scare" + i));
            }

            Log("自检结束");
            UnityEditor.EditorApplication.ExitPlaymode();
        }

        private static float BoundsTop(Transform t)
        {
            float top = t.position.y;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            {
                if (r.enabled && r.gameObject.activeInHierarchy)
                {
                    top = Mathf.Max(top, r.bounds.max.y);
                }
            }

            return top;
        }

        private static string BoundsOf(Transform t)
        {
            bool any = false;
            Bounds b = new Bounds(t.position, Vector3.zero);
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy)
                {
                    continue;
                }

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

            return any ? b.ToString("F2") : "无渲染体";
        }

        private IEnumerator Capture(string suffix)
        {
            string path = "/tmp/ib" + suffix + ".png";
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForEndOfFrame();
            yield return null;
            Log("截图 → " + path);
        }
    }
}
