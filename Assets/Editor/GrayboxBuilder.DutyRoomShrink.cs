using System.Collections.Generic;
using System.Text;
using Ib.Environment;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ib.EditorTools
{
    /// <summary>
    /// 2026-11-02 老大：二楼值班室（监控室）整体面积缩到原来的 1/4。
    /// 原房间墙中线 13 × 10.6 = 137.8；改后 6.5 × 5.3 = 34.45，正好 1/4。
    /// 东墙仍是 Line_West（电磁锁门不动），西侧新砌 Duty_West 到 x = 6.5，北墙 Duty_North 从 z = 10.6 收到 z = 5.3。
    /// 原房西北角变成封闭死区（玩家进不去也看不到），灯光、天花、家具、摆件全部跟着进新房间。
    ///
    /// 注意：这是运行在编辑器里的补丁，不在 BuildFromCommandLine 的流程里，
    /// 所以不能碰 GrayboxBuilder 的静态材质表（Finishes 在这时为 null），材质一律从场景里现成的物体上取。
    /// 幂等：墙 / 天花每次重建；灯、家具、摆件由 DutyRoomShrink_v1 标记保证只做一次。
    /// </summary>
    public static partial class GrayboxBuilder
    {
        private const float DutyRoomWestX = DutyWestX;     // 新西墙（墙中线），和生成器共用
        private const float DutyRoomNorthZ = DutyNorthZ;   // 新北墙（墙中线）
        private const float DutyRoomEastX = WestLineX;
        private const string ShrinkMarker = "DutyRoomShrink_v1";

        private const string WallDutyMatPath = "Assets/Art/Materials/Finish/M_Wall_Duty.mat";
        private const string CrateMatPath = "Assets/Art/Materials/Finish/M_Wood_Crate.mat";
        private const string MetalMatPath = "Assets/Art/Materials/Finish/M_Metal_Dark.mat";
        private const string PaperMatPath = "Assets/Art/Materials/Finish/M_Paper_Old.mat";
        private const string NavMesh2FPath = "Assets/Scenes/Graybox_NavMesh_2F.asset";

        [MenuItem("Tools/Ib/缩小二楼值班室到 1/4")]
        public static void ShrinkDutyRoomMenu()
        {
            Debug.Log(ShrinkDutyRoom());
        }

        public static string ShrinkDutyRoom()
        {
            StringBuilder log = new StringBuilder();
            Transform museum = GameObject.Find("Level_Museum")?.transform;
            if (museum == null)
            {
                return "ERROR: 场景里没有 Level_Museum";
            }

            Transform walls = museum.Find("Walls_2F");
            if (walls == null)
            {
                return "ERROR: 没有 Walls_2F";
            }

            bool layoutDone = museum.Find(ShrinkMarker) != null;
            log.AppendLine("开始：layoutDone=" + layoutDone);

            // ---- 1. 墙体：每次重建 ----
            Material wallMat = null;
            Transform oldNorth = walls.Find("Duty_North");
            if (oldNorth != null)
            {
                MeshRenderer mr = oldNorth.GetComponentInChildren<MeshRenderer>(true);
                wallMat = mr != null ? mr.sharedMaterial : null;
                Object.DestroyImmediate(oldNorth.gameObject);
            }

            Transform oldWest = walls.Find("Duty_West");
            if (oldWest != null)
            {
                if (wallMat == null)
                {
                    MeshRenderer mr = oldWest.GetComponentInChildren<MeshRenderer>(true);
                    wallMat = mr != null ? mr.sharedMaterial : null;
                }

                Object.DestroyImmediate(oldWest.gameObject);
            }

            if (wallMat == null)
            {
                wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/M_Wall_Concrete.mat");
            }

            WallAlongX(walls, "Duty_North", DutyRoomNorthZ, 0f, DutyRoomWestX, true, true, Floor2Y, Floor2Height, wallMat);
            WallAlongZ(walls, "Duty_West", DutyRoomWestX, 0f, DutyRoomNorthZ, Floor2Y, Floor2Height, wallMat);
            MarkStatic(walls.Find("Duty_North"));
            MarkStatic(walls.Find("Duty_West"));
            log.AppendLine("[1] 墙：Duty_North z=" + DutyRoomNorthZ + "，Duty_West x=" + DutyRoomWestX + "，材质 " + (wallMat != null ? wallMat.name : "NULL"));

            // ---- 2. 墙面饰面（值班室只有 Skin 一层）----
            Material skinMat = AssetDatabase.LoadAssetAtPath<Material>(WallDutyMatPath);
            Transform wf = museum.Find("Detail/WallFinish");
            if (wf != null && skinMat != null)
            {
                List<GameObject> stale = new List<GameObject>();
                foreach (Transform c in wf)
                {
                    bool rebuilt = c.name.StartsWith("Skin_Duty");
                    bool oldNorthSkin = false;
                    if (!rebuilt && c.name == "Skin")
                    {
                        Bounds b = WorldBounds(c.gameObject);
                        oldNorthSkin = b.min.y > 9f && b.min.z > 14.0f && b.max.z < 15.0f && b.size.x > 5f;
                    }

                    if (oldNorthSkin || rebuilt)
                    {
                        stale.Add(c.gameObject);
                    }
                }

                foreach (GameObject g in stale)
                {
                    Object.DestroyImmediate(g);
                }

                float sx0 = DutyRoomWestX + HalfWall;
                float sx1 = DutyRoomEastX - HalfWall;
                float sz0 = HalfWall;
                float sz1 = DutyRoomNorthZ - HalfWall;
                float topY = Floor2Y + Floor2Height;
                AddSkin(wf, "Skin_Duty_North", sx0, sx1, sz1 - 0.02f, sz1, Floor2Y, topY, skinMat);
                AddSkin(wf, "Skin_Duty_West", sx0, sx0 + 0.02f, sz0, sz1, Floor2Y, topY, skinMat);
                log.AppendLine("[2] 墙皮：重建北墙 / 西墙内表面，清掉旧的 " + stale.Count + " 片");
            }
            else
            {
                log.AppendLine("[2] 墙皮：跳过（WallFinish=" + (wf != null) + "，材质=" + (skinMat != null) + "）");
            }

            // ---- 3. 天花：井字梁 + 木板（每次重建，材质从旧物体上取）----
            Transform beamsRoot = museum.Find("Detail/Beams");
            Transform panels = museum.Find("Detail/Beams/CeilingPanels");
            Material beamMat = null;
            Material panelMat = null;
            Transform oldBeams = beamsRoot != null ? beamsRoot.Find("Beams_Duty") : null;
            Transform oldPanel = panels != null ? panels.Find("Ceiling_Duty") : null;
            if (oldBeams != null)
            {
                MeshRenderer mr = oldBeams.GetComponentInChildren<MeshRenderer>(true);
                beamMat = mr != null ? mr.sharedMaterial : null;
                Object.DestroyImmediate(oldBeams.gameObject);
            }

            if (oldPanel != null)
            {
                MeshRenderer mr = oldPanel.GetComponent<MeshRenderer>();
                panelMat = mr != null ? mr.sharedMaterial : null;
                Object.DestroyImmediate(oldPanel.gameObject);
            }

            if (beamMat == null)
            {
                beamMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Finish/M_Wood_Beam.mat");
            }

            if (panelMat == null)
            {
                panelMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Finish/M_Wood_Ceiling.mat");
            }

            if (beamsRoot != null && panels != null && beamMat != null && panelMat != null)
            {
                float cx0 = DutyRoomWestX + HalfWall;
                float cx1 = DutyRoomEastX - HalfWall;
                float cz0 = HalfWall;
                float cz1 = DutyRoomNorthZ - HalfWall;
                AddBeams(beamsRoot, "Duty", cx0, cx1, cz0, cz1, RoofY, beamMat);
                GameObject panel = AddSlab(panels, "Ceiling_Duty", cx0, cx1, cz0, cz1, RoofY - 0.02f, RoofY, panelMat);
                RemoveCollider(panel);
                panel.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                log.AppendLine("[3] 天花：按新房间重铺梁 " + (beamMat.name) + " 和木板 " + panelMat.name);
            }
            else
            {
                log.AppendLine("[3] 天花：失败（材质缺失）");
            }

            // ---- 以下只做一次 ----
            if (!layoutDone)
            {
                // ---- 4. 灯：4 盏收成 2 盏 ----
                Transform lighting = GameObject.Find("--- Lighting ---")?.transform;
                Transform dutyLights = lighting != null ? lighting.Find("Museum_Deferred/Lights_Duty") : null;
                Vector3 lampA = new Vector3(8.2f, Floor2Y + Floor2Height - 1f, 2.65f);
                Vector3 lampB = new Vector3(11.3f, Floor2Y + Floor2Height - 1f, 2.65f);
                if (dutyLights != null)
                {
                    Transform src = dutyLights.childCount > 0 ? dutyLights.GetChild(0) : null;
                    Vector3 oldPos = src != null ? src.localPosition : new Vector3(3.25f, lampA.y, 2.65f);
                    Light srcLight = src != null ? src.GetComponent<Light>() : null;

                    for (int i = dutyLights.childCount - 1; i >= 0; i--)
                    {
                        Object.DestroyImmediate(dutyLights.GetChild(i).gameObject);
                    }

                    AddDutyLight(dutyLights, "Light_Duty_0", lampA, srcLight);
                    AddDutyLight(dutyLights, "Light_Duty_1", lampB, srcLight);
                    log.AppendLine("[4] 灯：重建 2 盏点光 at " + lampA.ToString("F2") + " / " + lampB.ToString("F2"));

                    // 吊灯罩跟着灯走（Lamp 组在原点，子物体是绝对坐标，所以整体位移）
                    Transform lampRoot = museum.Find("Lamp_Fixtures");
                    Transform lamp0 = lampRoot != null ? lampRoot.Find("Lamp_Duty_0") : null;
                    if (lamp0 != null)
                    {
                        foreach (string n in new[] { "Lamp_Duty_1", "Lamp_Duty_2", "Lamp_Duty_3" })
                        {
                            Transform t = lampRoot.Find(n);
                            if (t != null)
                            {
                                Object.DestroyImmediate(t.gameObject);
                            }
                        }

                        lamp0.localPosition += new Vector3(lampA.x - oldPos.x, 0f, lampA.z - oldPos.z);
                        GameObject dup = Object.Instantiate(lamp0.gameObject, lampRoot);
                        dup.name = "Lamp_Duty_1";
                        dup.transform.localPosition += new Vector3(lampB.x - lampA.x, 0f, lampB.z - lampA.z);
                        log.AppendLine("[4] 吊灯罩：2 个");
                    }
                }

                // ---- 5. 家具：绝对坐标摆到新房间 ----
                Transform props = GameObject.Find("Props_Museum")?.transform;
                Transform dutyProps = props != null ? props.Find("2F/DutyRoom") : null;
                if (dutyProps != null)
                {
                    Place(dutyProps, "A_MonitorWall", 9.75f, null, 4.90f);
                    Place(dutyProps, "DutyDesk", 9.75f, null, 4.10f);
                    Place(dutyProps, "A_HandoverLog", 9.05f, null, 4.00f);
                    Place(dutyProps, "Text_T13_StockForm", 10.45f, null, 4.00f);
                    Place(dutyProps, "Text_T16_DeathRuling", 11.0f, null, 4.10f);

                    // 东墙那张值班须知原来在旧房间 z 6.5，房间缩了以后跑到墙外，挪到门南侧
                    Transform rules = dutyProps.Find("Text_T8_NightRules");
                    if (rules != null)
                    {
                        rules.localPosition = new Vector3(rules.localPosition.x, rules.localPosition.y, 1.2f);
                        log.AppendLine("    移动 Text_T8_NightRules -> z 1.2");
                    }

                    // 监控屏组的子物体用的是绝对坐标，整组平移
                    Transform screens = dutyProps.Find("Monitor_Screens");
                    if (screens != null)
                    {
                        screens.localPosition += new Vector3(9.75f - 6.5f, 0f, 4.90f - 10.2f);
                        log.AppendLine("    移动 Monitor_Screens");
                    }

                    // ---- 6. 摆件 ----
                    Material crateMat = AssetDatabase.LoadAssetAtPath<Material>(CrateMatPath);
                    Material metalMat = AssetDatabase.LoadAssetAtPath<Material>(MetalMatPath);
                    Material paperMat = AssetDatabase.LoadAssetAtPath<Material>(PaperMatPath);
                    Transform extras = NewGroup(dutyProps, "Extras_Duty");
                    if (metalMat != null)
                    {
                        AddProp(extras, "Locker_Metal", 6.95f, Floor2Y, 1.2f, 0.55f, 1.95f, 0.9f, metalMat);
                    }

                    if (crateMat != null)
                    {
                        AddProp(extras, "Crate_A", 6.98f, Floor2Y, 4.45f, 0.55f, 0.42f, 0.55f, crateMat);
                        AddProp(extras, "Crate_B", 7.0f, Floor2Y + 0.42f, 4.47f, 0.42f, 0.36f, 0.42f, crateMat);
                        AddProp(extras, "Crate_C", 7.02f, Floor2Y + 0.78f, 4.45f, 0.3f, 0.26f, 0.3f, crateMat);
                    }

                    if (paperMat != null)
                    {
                        GameObject board = AddBox(extras, "DutyRoster", new Vector3(6.69f, Floor2Y + 1.55f, 2.5f),
                            new Vector3(0.02f, 1.0f, 0.8f), paperMat);
                        RemoveCollider(board);
                    }

                    Model(extras, "18_folding_stool", 8.35f * WorldScale, Floor2Y * WorldScale, 1.35f * WorldScale, 25f, 1f, true);
                    Model(extras, "11_round_side_table", 7.45f * WorldScale, Floor2Y * WorldScale, 2.7f * WorldScale, 0f, 1f, true);
                    log.AppendLine("[6] 摆件：铁皮柜、纸箱 ×3、值班表、折叠凳、小圆几");
                }

                // 扶手椅：从旧房间北半边搬到门内侧
                Transform chair = props != null ? props.Find("Dressing/DutyRoom/10_armchair") : null;
                if (chair != null)
                {
                    chair.position = new Vector3(11.7f * WorldScale, chair.position.y, 1.45f * WorldScale);
                    log.AppendLine("[5] 扶手椅 -> (11.7, 1.45)");
                }

                // ---- 7. 房间标签 ----
                Transform note = GameObject.Find("--- Notes ---")?.transform.Find("Room_Duty");
                if (note != null)
                {
                    GrayboxNote gn = note.GetComponent<GrayboxNote>();
                    if (gn != null)
                    {
                        gn.Text = "值班室 6.5×5.3（1/4）· A 监控屏";
                    }

                    note.position = new Vector3(9.75f * WorldScale, (Floor2Y + 0.1f) * WorldScale, 2.65f * WorldScale);
                }

                GameObject marker = new GameObject(ShrinkMarker);
                marker.transform.SetParent(museum, false);
                marker.transform.localPosition = Vector3.zero;
            }

            // ---- 8. 二楼导航网格重烘 ----
            NavMeshSurface surface = null;
            foreach (NavMeshSurface s in Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (s.name == "NavMesh_2F")
                {
                    surface = s;
                }
            }

            if (surface != null)
            {
                surface.BuildNavMesh();
                if (surface.navMeshData != null)
                {
                    AssetDatabase.DeleteAsset(NavMesh2FPath);
                    AssetDatabase.CreateAsset(surface.navMeshData, NavMesh2FPath);
                    surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMesh2FPath);
                    log.AppendLine("[7] NavMesh_2F 已重烘并保存");
                }
                else
                {
                    log.AppendLine("[7] !! BuildNavMesh 没产出数据");
                }
            }
            else
            {
                log.AppendLine("[7] !! 找不到 NavMesh_2F");
            }

            // ---- 9. 尺寸与走位抽查 ----
            float netW = (DutyRoomEastX - HalfWall) - (DutyRoomWestX + HalfWall);
            float netD = (DutyRoomNorthZ - HalfWall) - HalfWall;
            log.AppendLine("[8] 房间净尺寸 " + netW.ToString("F2") + " × " + netD.ToString("F2") + " = "
                + (netW * netD).ToString("F1") + " ㎡（1× 基准）；世界坐标 " + (netW * WorldScale).ToString("F1") + " × "
                + (netD * WorldScale).ToString("F1") + " = " + (netW * netD * WorldScale * WorldScale).ToString("F1") + " ㎡");

            Vector3 doorIn = new Vector3(12.4f * WorldScale, Floor2Y * WorldScale + 0.2f, 3.0f * WorldScale);
            Vector3 mid = new Vector3(9.4f * WorldScale, Floor2Y * WorldScale + 0.2f, 2.2f * WorldScale);
            log.AppendLine("[9] NavMesh 抽样 门内侧=" + SampleNav(doorIn) + "，房内=" + SampleNav(mid));

            EditorSceneManager.MarkSceneDirty(museum.gameObject.scene);
            EditorSceneManager.SaveScene(museum.gameObject.scene);
            log.AppendLine("[10] 场景已保存");
            return log.ToString();
        }


        // ================================================================ 目检用：把 Scene 视图摆到值班室里

        private static void AddSkin(Transform parent, string name, float x0, float x1, float z0, float z1, float y0, float y1, Material mat)
        {
            GameObject go = AddSlab(parent, name, x0, x1, z0, z1, y0, y1, mat);
            RemoveCollider(go);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void AddBeams(Transform parent, string room, float x0, float x1, float z0, float z1, float ceilingY, Material mat)
        {
            const float cell = 3.25f;
            const float width = 0.28f;
            Transform g = NewGroup(parent, "Beams_" + room);

            int nz = Mathf.Max(1, Mathf.RoundToInt((z1 - z0) / cell));
            for (int k = 1; k < nz; k++)
            {
                float z = z0 + (z1 - z0) * k / nz;
                RemoveCollider(AddSlab(g, "BeamX_" + k, x0, x1, z - width * 0.5f, z + width * 0.5f, ceilingY - 0.42f, ceilingY, mat));
            }

            int nx = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / cell));
            for (int k = 1; k < nx; k++)
            {
                float x = x0 + (x1 - x0) * k / nx;
                RemoveCollider(AddSlab(g, "BeamZ_" + k, x - width * 0.5f, x + width * 0.5f, z0, z1, ceilingY - 0.40f, ceilingY, mat));
            }
        }

        private static void AddDutyLight(Transform parent, string name, Vector3 localPos, Light template)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = template != null ? template.color : new Color(1f, 0.92f, 0.82f);
            l.intensity = template != null ? template.intensity : 3.5f;
            l.range = template != null ? template.range : 14f;
            l.shadows = LightShadows.None;
            l.shadowStrength = 0.9f;
            l.shadowBias = 0.05f;
            l.shadowNormalBias = 0.4f;

            if (template != null)
            {
                UniversalAdditionalLightData srcData = template.GetComponent<UniversalAdditionalLightData>();
                UniversalAdditionalLightData dstData = go.AddComponent<UniversalAdditionalLightData>();
                if (srcData != null)
                {
                    EditorUtility.CopySerialized(srcData, dstData);
                }
            }
        }

        /// <summary>把家具摆到新房间的绝对位置（1× 基准坐标，房间局部空间）。y 传 null 表示不动。</summary>
        private static void Place(Transform parent, string name, float x, float? y, float z)
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                return;
            }

            t.localPosition = new Vector3(x, y ?? t.localPosition.y, z);
        }

        private static string SampleNav(Vector3 p)
        {
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            {
                return "命中（距离 " + hit.distance.ToString("F2") + "）";
            }

            return "未命中";
        }
    }
}
