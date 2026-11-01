using System.Collections.Generic;
using Ib.Core;
using Ib.Environment;
using Ib.Flow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Ib.EditorTools
{
    /// <summary>
    /// 布展变化（GDD §3.3 / 支柱 3，老大 2026-11-01）：在 --- Rearrange --- 下生成一组 ChangeWhenUnseen，
    /// 每个只发生一次，大多在停电以后。只做软引导，不碰关键路线（楼梯、厕所、值班室、配电间、前台、正门、全家福厅、
    /// 一层四个大厅门 —— 接待厅和雕塑厅之间没有门，这四个门两两互为唯一绕行路线，一个都不能封）。
    ///   A  二楼名画长廊西墙那扇本来就锁着的门 → 墙（门框线脚一并抹平，护墙板 / 踢脚线接上）
    ///   B1 二楼《蒙娜丽莎》→ Lily（"她在每一幅画里"）
    ///   B2 二楼《持黄玫瑰的女士》转过去面朝墙
    ///   B3 一层油画厅《向日葵》→《戴珍珠耳环的少女》（同一幅画在馆里出现两次 = 正在重新挂画）
    ///   C1 一层雕塑厅《自由女神》原地转身背对楼梯口
    ///   C2 一层雕塑厅"蒙娜丽莎"雕像转向楼梯口
    ///   C3 一层服装厅绳圈里的一个人台挪到绳子边、面朝门口
    ///   D  走廊延长：没做（要动墙体和导航网格，容易把人困住或让怪物卡住）。
    /// 按名字找、先删后建，可重复执行；生成灰盒的最后一步也会调用。
    /// </summary>
    public static partial class GrayboxBuilder
    {
        private const string RearrangeRoot = "--- Rearrange ---";

        [MenuItem("Tools/Ib/布置布展变化（看不见才变）")]
        private static void SetupRearrangeMenu()
        {
            SetupRearrange();
        }

        internal static void SetupRearrange(bool save = true)
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform old = FindRoot(scene, RearrangeRoot);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            Transform root = new GameObject(RearrangeRoot).transform;
            var made = new List<string>();

            if (SealLockedDoor(scene, root)) made.Add("A");
            if (Retexture(root, "Rearrange_B1_MonaLisaBecomesLily", "Painting_MonaLisa_Canvas", "LilyPortrait_Flat.png",
                    GameState.AfterHours, GameState.AngelsActive, 6f, 1.5f, Sfx.HangingHook)) made.Add("B1");
            if (FaceWall(root, "Rearrange_B2_LadyFacesWall", "Painting_LadyWithYellowRoses", GameState.PowerOut, 6f, 2f)) made.Add("B2");
            if (Retexture(root, "Rearrange_B3_SunflowersRehung", "Painting_Sunflowers_Canvas", "GirlWithPearlEarring_Canvas.png",
                    GameState.AngelsActive, GameState.AngelsActive, 8f, 2f, Sfx.HangingHook)) made.Add("B3");
            if (Turn(root, "Rearrange_C1_LibertyTurnsAround", "StatueOfLiberty", 180f, Vector3.zero,
                    GameState.AngelsActive, false, 10f, 3f)) made.Add("C1");
            if (Turn(root, "Rearrange_C2_StatueFacesStairs", "MonaLisaStatue", 100f, Vector3.zero,
                    GameState.PowerOut, false, 10f, 2.5f)) made.Add("C2");
            if (WanderingMannequin(scene, root)) made.Add("C3");

            EditorSceneManager.MarkSceneDirty(scene);
            if (save)
            {
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log("[GrayboxBuilder] 布展变化：" + string.Join("、", made) + "（共 " + made.Count + " 处）。");
        }

        private static ChangeWhenUnseen NewChange(Transform root, string name, ChangeWhenUnseen.Kind kind, Bounds area,
            GameState from, GameState to, bool flashlight, float distance, float unseen, string sound)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(root, false);
            ChangeWhenUnseen c = go.AddComponent<ChangeWhenUnseen>();
            SerializedObject so = new SerializedObject(c);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("fromState").enumValueIndex = (int)from;
            so.FindProperty("toState").enumValueIndex = (int)to;
            so.FindProperty("needsFlashlight").boolValue = flashlight;
            so.FindProperty("minDistance").floatValue = distance;
            so.FindProperty("minUnseen").floatValue = unseen;
            area.Expand(0.1f);
            so.FindProperty("area").boundsValue = area;
            so.FindProperty("sound").stringValue = sound;
            so.ApplyModifiedPropertiesWithoutUndo();
            return c;
        }

        private static void SetArray(Object target, string field, IList<Object> values)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>要在运行时挪动的东西不能参与静态合批（合批后网格挪不动）。</summary>
        private static void Unstatic(Transform t)
        {
            foreach (Transform c in t.GetComponentsInChildren<Transform>(true))
            {
                StaticEditorFlags f = GameObjectUtility.GetStaticEditorFlags(c.gameObject);
                GameObjectUtility.SetStaticEditorFlags(c.gameObject, f & ~StaticEditorFlags.BatchingStatic);
            }
        }

        private static Transform FindAnywhere(string name)
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }

        // ---------------------------------------------------------------- A 门变墙

        private static bool SealLockedDoor(Scene scene, Transform root)
        {
            Transform door = FindAnywhere("Door_Closed_Locked");
            Transform finish = FindAnywhere("WallFinish");
            if (door == null || finish == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 找不到 Door_Closed_Locked / WallFinish，跳过 A。");
                return false;
            }

            Bounds d = door.GetComponent<Renderer>().bounds;
            float zMin = d.min.z;
            float zMax = d.max.z;
            float top = d.max.y;

            ChangeWhenUnseen change = NewChange(root, "Rearrange_A_LockedDoorBecomesWall", ChangeWhenUnseen.Kind.Swap, d,
                GameState.PowerOut, GameState.AngelsActive, false, 8f, 2f, Sfx.FrameScrape);
            Transform wall = NewGroup(change.transform, "SealedWall");

            // 墙芯：和墙同厚，门板的位置
            GameObject core = AddBox(wall, "Core", Vector3.zero, Vector3.one, null);
            core.transform.SetPositionAndRotation(new Vector3(d.center.x, d.center.y, d.center.z), Quaternion.identity);
            core.transform.localScale = new Vector3(WallThickness * WorldScale, d.size.y, d.size.z);
            NavMeshObstacle obstacle = core.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;
            obstacle.carving = true;

            // 长廊这一面的饰面：把门两边护墙板、踢脚线、墙皮接过来；门框线脚藏掉
            var hide = new List<Object> { door.gameObject };
            Material coreMat = null;
            foreach (Renderer r in finish.GetComponentsInChildren<Renderer>(true))
            {
                Bounds b = r.bounds;
                bool sameWall = Mathf.Abs(b.center.x - d.center.x) < 0.45f && b.min.y < top + 0.3f && b.max.y > d.min.y;
                if (!sameWall)
                {
                    continue;
                }

                if (r.name.StartsWith("Casing") && b.max.z > zMin - 0.3f && b.min.z < zMax + 0.3f)
                {
                    hide.Add(r.gameObject);
                    continue;
                }

                if (Mathf.Abs(b.max.z - zMin) > 0.05f || r.name.StartsWith("Casing"))
                {
                    continue;
                }

                // 门左侧（z 小的一边）那一段一直延到门边：复制一段补上门洞宽度，墙皮只补到门顶
                float yMin = b.min.y;
                float yMax = r.name == "Skin" ? top : b.max.y;
                GameObject fill = Object.Instantiate(r.gameObject, wall);
                fill.name = "Fill_" + r.name;
                foreach (Collider c in fill.GetComponents<Collider>())
                {
                    Object.DestroyImmediate(c);
                }

                fill.transform.SetPositionAndRotation(new Vector3(b.center.x, (yMin + yMax) * 0.5f, (zMin + zMax) * 0.5f), r.transform.rotation);
                Vector3 s = fill.transform.lossyScale;
                Vector3 local = fill.transform.localScale;
                fill.transform.localScale = new Vector3(local.x, local.y * (yMax - yMin) / s.y, local.z * (zMax - zMin) / s.z);
                if (r.name == "Skin")
                {
                    coreMat = r.sharedMaterial;
                }
            }

            core.GetComponent<Renderer>().sharedMaterial = coreMat;
            wall.gameObject.SetActive(false);
            SetArray(change, "hide", hide);
            SetArray(change, "show", new List<Object> { wall.gameObject });
            return true;
        }

        // ---------------------------------------------------------------- B 画作内容改变

        private static bool Retexture(Transform root, string name, string canvasName, string textureFile,
            GameState from, GameState to, float distance, float unseen, string sound)
        {
            Transform canvas = FindAnywhere(canvasName);
            string path = "Assets/Art/Textures/Paintings/" + textureFile;
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (canvas == null || tex == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 找不到 " + canvasName + " 或 " + path + "，跳过 " + name + "。");
                return false;
            }

            Renderer r = canvas.GetComponent<Renderer>();
            Bounds area = r.bounds;
            foreach (Renderer other in canvas.parent.GetComponentsInChildren<Renderer>())
            {
                area.Encapsulate(other.bounds);
            }

            ChangeWhenUnseen change = NewChange(root, name, ChangeWhenUnseen.Kind.Retexture, area, from, to, false, distance, unseen, sound);
            SetRef(change, "canvas", r);
            SetRef(change, "texture", tex);
            return true;
        }

        /// <summary>画绕自己包围盒中心的竖轴转 180°：背板朝外、画面贴墙，占地不变。</summary>
        private static bool FaceWall(Transform root, string name, string painting, GameState from, float distance, float unseen)
        {
            Transform t = FindAnywhere(painting);
            if (t == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 找不到 " + painting + "，跳过 " + name + "。");
                return false;
            }

            Bounds area = new Bounds(t.position, Vector3.zero);
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            {
                area.Encapsulate(r.bounds);
            }

            ChangeWhenUnseen change = NewChange(root, name, ChangeWhenUnseen.Kind.Move, area, from, GameState.AngelsActive, false,
                distance, unseen, Sfx.HangingHook);
            SetArray(change, "targets", new List<Object> { t });
            Unstatic(t);
            SerializedObject so = new SerializedObject(change);
            so.FindProperty("yaw").floatValue = 180f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // ---------------------------------------------------------------- C 展品挪位

        private static bool Turn(Transform root, string name, string exhibit, float yaw, Vector3 offset,
            GameState from, bool flashlight, float distance, float unseen)
        {
            Transform t = FindAnywhere(exhibit);
            if (t == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 找不到 " + exhibit + "，跳过 " + name + "。");
                return false;
            }

            Bounds area = t.GetComponent<Renderer>() != null ? t.GetComponent<Renderer>().bounds : new Bounds(t.position, Vector3.one);
            ChangeWhenUnseen change = NewChange(root, name, ChangeWhenUnseen.Kind.Move, area, from, GameState.AngelsActive, flashlight,
                distance, unseen, Sfx.FrameScrape);
            SetArray(change, "targets", new List<Object> { t });
            Unstatic(t);
            SerializedObject so = new SerializedObject(change);
            so.FindProperty("yaw").floatValue = yaw;
            so.FindProperty("offset").vector3Value = offset;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>绳圈里离南边绳子最近的那个人台（Mannequin_Pile_*）挪到绳子边、转过来面朝门口。</summary>
        private static bool WanderingMannequin(Scene scene, Transform root)
        {
            Transform pen = FindAnywhere("RopedMannequins_NW");
            Transform group = null;
            if (pen != null)
            {
                foreach (Transform c in pen)
                {
                    if (c.name.StartsWith("Mannequin_Pile") && (group == null || c.GetComponentInChildren<Renderer>().bounds.center.z
                            < group.GetComponentInChildren<Renderer>().bounds.center.z))
                    {
                        group = c;
                    }
                }
            }

            if (group == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 找不到 RopedMannequins_NW/Mannequin_Pile_*，跳过 C3。");
                return false;
            }

            Bounds area = new Bounds(group.position, Vector3.zero);
            foreach (Renderer r in group.GetComponentsInChildren<Renderer>())
            {
                area.Encapsulate(r.bounds);
            }

            ChangeWhenUnseen change = NewChange(root, "Rearrange_C3_MannequinAtTheRope", ChangeWhenUnseen.Kind.Move, area,
                GameState.PowerOut, GameState.AngelsActive, true, 7f, 2f, Sfx.FrameScrape);
            SetArray(change, "targets", new List<Object> { group });
            Unstatic(group);
            SerializedObject so = new SerializedObject(change);
            so.FindProperty("yaw").floatValue = 160f;
            so.FindProperty("offset").vector3Value = new Vector3(0.3f, 0f, -0.35f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
