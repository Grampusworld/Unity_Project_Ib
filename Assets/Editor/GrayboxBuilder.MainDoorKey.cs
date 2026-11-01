using Ib.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ib.EditorTools
{
    /// <summary>
    /// 正门钥匙（老大 2026-11-01）：正门锁上补锁孔、锁舌和锁扣，插在锁里的钥匙换成 MainDoorKey 模型（MainDoorKeyBuilder），
    /// 摆成"插好、拧过 90°、纸签往下垂"的样子，MainDoorLock 的开锁动画从这个姿态倒推。
    /// 抽屉里的检视（KeyInspect）运行时直接从 Resources 实例化，不用在场景里摆。
    /// 按名字找、先删后建，可重复执行；生成灰盒的最后一步也会调用。
    /// </summary>
    public static partial class GrayboxBuilder
    {
        [MenuItem("Tools/Ib/布置正门钥匙（锁孔 + 锁舌 + 钥匙模型）")]
        private static void SetupMainDoorKeyMenu()
        {
            SetupMainDoorKey();
        }

        internal static void SetupMainDoorKey(bool save = true)
        {
            Scene scene = SceneManager.GetActiveScene();
            LoadFinishes();

            Transform lockRoot = FindAnywhere("MainDoor_Lock");
            Transform plate = lockRoot != null ? lockRoot.Find("LockPlate") : null;
            MainDoorLock lockComp = lockRoot != null ? lockRoot.GetComponent<MainDoorLock>() : null;
            if (plate == null || lockComp == null)
            {
                Debug.LogWarning("[GrayboxBuilder] 找不到 MainDoor_Lock/LockPlate，跳过正门钥匙。");
                return;
            }

            foreach (string n in new[] { "Key_InLock", "Keyhole", "Bolt", "BoltKeeper" })
            {
                Transform old = lockRoot.Find(n);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            Vector3 p = plate.localPosition;
            float front = p.z + plate.localScale.z * 0.5f;
            float holeY = p.y - 0.04f;

            Material dark = Lit("M_Keyhole", null, null, new Color(0.02f, 0.018f, 0.015f), 0.2f, 1f, 0f, false);
            GameObject hole = AddBox(lockRoot, "Keyhole", new Vector3(0f, holeY, front + 0.0008f), new Vector3(0.016f, 0.03f, 0.0016f), dark);
            RemoveCollider(hole);

            // 锁舌伸进右边门扇上的锁扣（锁着）；开锁时往左缩回锁片
            float boltY = p.y + 0.05f;
            GameObject bolt = AddBox(lockRoot, "Bolt", new Vector3(0.095f, boltY, front - 0.015f), new Vector3(0.05f, 0.022f, 0.022f), Finishes.Brass);
            RemoveCollider(bolt);
            GameObject keeper = AddBox(lockRoot, "BoltKeeper", new Vector3(0.128f, boltY, front - 0.018f), new Vector3(0.024f, 0.046f, 0.03f), dark);
            RemoveCollider(keeper);

            // 钥匙：+Z（钥匙头）朝门里（锁根节点 −Z），已拧 90°；纸签反转 90° 保持下垂
            GameObject prefab = MainDoorKeyBuilder.Load();
            GameObject key = (GameObject)PrefabUtility.InstantiatePrefab(prefab, lockRoot);
            key.name = "Key_InLock";
            key.transform.localPosition = new Vector3(0f, holeY, front);
            key.transform.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up) * Quaternion.AngleAxis(90f, Vector3.forward);
            key.transform.localScale = Vector3.one;
            Transform tag = key.transform.Find("Tag");
            if (tag != null)
            {
                tag.localRotation = Quaternion.AngleAxis(-90f, Vector3.forward);
            }

            key.SetActive(false);

            SetRef(lockComp, "keyVisual", key);
            SetRef(lockComp, "bolt", bolt.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            if (save)
            {
                EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[GrayboxBuilder] 正门钥匙：锁孔、锁舌、钥匙模型已布置。");
        }
    }
}
