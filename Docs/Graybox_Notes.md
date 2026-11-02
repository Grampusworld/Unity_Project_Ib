# 美术馆灰盒说明（2026-10-22）

## 怎么生成
1. 打开 Unity，等脚本编译完。
2. 菜单 **Tools → Ib → 生成灰盒关卡**，会覆盖 `Assets/Scenes/Graybox.unity` 并重建 `Player.prefab`。
3. Play。默认出生在黑屋、正对画架。**F1 开调试菜单**；**数字键 1–9** 在检查点之间传送（F1 让给菜单了，所以从 F 键改成主键盘数字键）：

| 键 | 位置 |
|---|---|
| 1 | CP0 黑屋起点 |
| 2 | CP1 出画后 · 全家福厅《美术馆》前 |
| 3 | CP2 拿到手电 · 厕所 |
| 4 | CP3 任务 A 完成 · 值班室门口 |
| 5 | CP4 任务 B · 配电间里 |
| 6 | CP5 任务 C · 前台 |
| 7 | 调试：正门内侧 |
| 8 | 调试：一层楼梯脚 |
| 9 | 调试：二层楼梯口（面朝长廊） |

> **两只名画人物已接进重建流程（2026-11-01）**：`GrayboxBuilder.Build()` 最后会调用 `PaintingFigureSetup.Setup()` + `PaintingFigureBreakoutSetup.Setup()`，生成灰盒后自动换 Girl、绑破框站点、写回 0.35 / 0.85 的出场延迟，不用再手动点菜单。此前要靠人工补跑，漏跑会退回红色胶囊、两只都用 0.3 s 同时出场。
> 单独修已有场景仍可用菜单：**Tools → Ib → 二楼名画人物换成 Girl 模型**、**Tools → Ib → 布置名画人物破框**（两步都会自己保存场景）。
> 第一步还会给两只预挂一个**禁用状态**的 `NavMeshAgent`，否则 Play 开始时运行时代码新建的 Agent 会把人物吸到最近的导航网格（实测掉到一楼/楼外）。
> **二楼导航网格检查（2026-11-01）**：20:31 重建留下的 `Graybox_NavMesh_2F.asset` 是空的（6.9 KB，0 个可行走面），人物一启用 Agent 就掉到一楼。已重烘为约 25 KB。以后重建后看一眼资产大小；不正常就重跑 **Tools → Ib → 布置哭泣天使**（会重烘两层导航网格）。`PaintingFigure.PlaceOnMesh` 已改为先采样成功再启用 Agent，采不到会保持禁用并直线兜底，不会再把人瞬移走。

## 调试菜单（F1，2026-10-22）
- `Assets/Scripts/Core/DebugMenu.cs`，挂在 `--- Game ---` 上。只在编辑器 / Development Build 生效。
- 打开时：光标解锁、玩家输入屏蔽（`FirstPersonController.InputBlocked`）、`PlayerLook.SuspendPointerCapture = true`（防止点菜单顺手把光标锁回去）。关闭时恢复并重新锁光标。
- **自由飞行（穿墙）**：开关打开时 `CharacterController.enabled = false` —— 胶囊碰撞没了，所以能穿墙、能悬空；移动方向用相机 forward/right（带 pitch），空格上升、Ctrl 下降、Shift ×3 加速，无重力无加速度。
- 关掉飞行时会找一个不卡几何体的落点：原地 → 正下方射线找地面 → 向上逐 0.5 m 抬（最多 4 m）→ 都不行就退回进入飞行前的位置（并打 warning）。
- UI 用 OnGUI 固定面板，**不用 GUILayout.Window**（内置 window 样式标题栏高度写死，字号一大标题就被裁）。
- 色彩坑：项目是 Linear 空间，IMGUI 的 `Color` 按线性值处理，屏幕会再编码成 sRGB。想要视觉上的深灰面板就得写线性值（0.010 → sRGB 0.10）。

旧版"小房间+走廊"的生成器备份在 `Docs/Backup/GrayboxBuilder_v1_room_corridor.cs.txt`。

## 布局（坐标：原点 = 西南角，+X 东，+Z 北）
- 楼体 40 × 26 m，一层净高 6.0 m，二层地面 6.4 m，二层净高 5.0 m（10-22 挑高，更空旷）。墙厚 0.3 m。
- **一层**：西列油画厅 13×26；中列由南到北：接待厅 13×9（正门在南墙正中）、雕塑厅 13×12（天使在正中 19.5, 15，配电间 5×3.2 切在南墙中段，门朝北）、楼梯厅 13×5；东列服装厅 14×26。
- **二层**：西列值班室 13×10.6（南）+ 未开放（北）；中列名画长廊 13×21 + 楼梯厅 13×5（北端）；东列厕所 7×7（东南）、全家福厅 14×11（中段）、未开放（东北、东南）。
- **楼梯**：双跑折返，平台在西端（高 3.2 m），踏步 0.213 × 0.3 m（仍小于 CharacterController stepOffset 0.3）。上到二层后向东走出、右转就是长廊。
- **黑屋**：贴在美术馆南墙外（x 14.5–24.5，z −16–0），北墙就是美术馆南墙，那道双开门就是正门。画架在 z = −14（离门 14 m），出生时背对门看画。空气墙通道 5.2 m 宽（`AirBandHalfWidth = 2.6`），两侧各留 1.2 m、画架后方留 1.3 m，玩家能绕到画布背面看空白边缘的刮痕。"空画框"已建好但默认隐藏（第二次进黑屋时打开）。
- 玩家改成 7 岁身高：胶囊 1.3 m，眼睛 1.15 m（§3.1）。

## 门的连接
| 门 | 位置 |
|---|---|
| 黑屋 ↔ 接待厅（正门） | 南墙 x = 19.5，宽 3.0 高 3.6，双开门，中缝 3.5 cm，向馆内开 90° / 3 s |
| 油画厅 ↔ 接待厅 / 雕塑厅 | 西线 z = 4.5 / 15，宽 2.4 |
| 服装厅 ↔ 接待厅 / 雕塑厅 | 东线 z = 4.5 / 15，宽 2.4 |
| 接待厅 ↔ 雕塑厅 | **不开门**（去楼梯必须穿过天使） |
| 雕塑厅 ↔ 楼梯厅 | 通高开口，宽 7 m |
| 配电间 | 北墙小门，宽 1.2 |
| 值班室（电磁锁）/ 未开放区锁门 | 二层西线 z = 3.0 / 18.5 |
| 厕所 / 全家福厅 | 二层东线 z = 3.3 / 13.5 |

## 我先取的默认值（场景里橙色文字标着，等试跑再定）
1. **楼梯方案**：按你选的"一层楼梯厅移到北端"，配电间改到雕塑厅南墙。GDD 里写"雕塑厅北墙"的几处、一层俯视图还没改。
2. **天窗**：雕塑厅正上方是二层长廊，真天窗做不了。现在是顶棚一块发光板 + 一盏冷色射灯打天使。
3. **展厅连接**：服装厅西墙开两扇门（接 接待厅、雕塑厅），楼梯厅与服装厅、油画厅不相通。
4. **备用后路**（配电间）：没做，先试倒退穿雕塑厅的难度。
5. **怪物数值**：没做。人偶 3 条往返路线（WaypointPath）、名画人物 2 个、天使都只是红色占位，没有碰撞。
6. **检查点**：按 §3.4 建议放了 6 个（见上表）。
7. **其他小处**：天使面朝东（背对油画厅进来的门）；值班室监控屏靠北墙（文字说靠北墙，俯视图画在房间中部，按文字）；厕所隔间只有隔板没有门；空气墙是硬碰撞，"黑暗变浓 + 阻力"没做。

## 序章大门（2026-10-22）
- 黑屋只有两盏灯：画架**正上方**一盏暖色顶光（新加）+ 前方补光。
- **门缝光 = 单面自发光 Quad**：`MainDoor_Double/Hinge_Left/Seam_Crack`，0.028 × 3.58 m，材质 `M_GB_SeamGlow`（暖白 emission 1.0）。
  - Unity 自带 Quad 的正面法线是 **−Z**，`localRotation = identity` 就朝黑屋；URP Lit `_Cull = 2` 剔除背面，所以接待厅那侧完全看不见（已验证）。
  - 位置 z = hinge + (−0.012)，塞在门板厚度中间；挂在左门铰链上，开门时跟着门板转走。
  - **别再改成实心 Box**：两面都会露，从接待厅看就是一条刺眼白线（10-22 晚踩过两次）。
- **门把手两个**：`Keyhole_Left` / `Keyhole_Right`，`M_GB_KeyItem`，0.12 × 0.18 × 0.06，离中缝各 0.2675 m，高 1.09 m（7 岁孩子够得到），都贴在馆内 +Z 面。
- 冷知识：正对门缝看过去能在缝里看到一条 2 cm 宽的**画布**——那是 14 m 外被顶光照亮的画（不是 bug）。
- 走到门前，准星对准门出现「[E] 推开」，按 E 两扇门 3 秒缓缓向馆内打开，没有过场，全程可以自由走动。
- 进入馆内（离门 > 1 m）后，只要门还在视野里（视锥 + 射线遮挡检测）就保持敞开；**视线一离开大门**：门瞬间关死上锁，`Level_BlackRoom` 整块停用，通道关闭。
- 漏光处理：馆内点光不开阴影会穿墙，所以黑屋阶段只开接待厅两盏（带阴影）；其余馆内灯在 `--- Lighting ---/Museum_Deferred` 下，推门那一刻才打开。
- 调试：数字键 1 传回黑屋会自动恢复序章状态（黑屋启用、门可推、馆灯关）；2–9 直接进馆会自动开灯。
- 已在 Play 模式实测：对准门 → 推开 → 打开 → 进馆回头时不关 → 转身后关闭、黑屋停用。

## 提亮（2026-10-22）
房间点光 2.8 → 14（range 10 → 16），环境光约 ×4，雾密度 0.03 → 0.014，后处理曝光 −0.25 → 0、暗角 0.45 → 0.35。删掉了 Lockers、值班室的录像机和椅子。

## 布置与装修（2026-10-27）
**重要：名画、雕像原来是在场景里手摆的，重新生成会被冲掉。** 这一轮把当时场景里的位置原样写进了 `GrayboxBuilder.Dressing.cs` 的 `Artworks` 表；同时把手工改过的地方也写回代码（油画厅只留两块占位板、北侧两角底座放大到 1.5×0.9×1.5、去掉楼梯厅三幅小稿）。以后要挪画，改表，不要再在场景里拖。改前的场景备份在 `Docs/Backup/Graybox_2026-10-27_before_dressing.unity.txt`。

生成流程现在是：生成灰盒 → 自动接着跑「布置哭泣天使」（天使模型、巡逻线、导航网格、手电），一次菜单就是完整场景。

代码拆成了三个文件（都是 `GrayboxBuilder` 的 partial）：
- `GrayboxBuilder.Materials.cs`：程序生成贴图（抹灰、深色拼花木地板、石材地砖、旧石材、木纹、浮尘圆点），存到 `Assets/Art/Textures/Graybox`；材质在 `Assets/Art/Materials/Finish`。都是"已存在不覆盖"，想重做就删掉对应文件。方块 UV 改成世界坐标，贴图不再随方块拉伸。
- `GrayboxBuilder.Detail.cs`：建筑细节，全部从墙体包围盒推出来，改墙/门常量会自动跟着变。
- `GrayboxBuilder.Dressing.cs`：名画/雕像表、道具模型、博物馆杂项、展品射灯、吊灯、浮尘。

### 建筑细节
| 项目 | 做法 |
|---|---|
| 每间房一种墙色 | 护墙板以上贴一层房间色：接待厅暖灰、油画厅深牛血红、雕塑厅石灰白、楼梯厅灰绿、服装厅暗青、二层长廊深绿、全家福厅灰玫瑰、值班室米色、厕所白瓷砖 |
| 墙面分层 | 踢脚线 → 深色护墙板（0.22–0.86 m）→ 墙裙压条 → 墙色 → 描金挂镜线 → 两级檐口石膏线 |
| 壁柱 | 接待厅、油画厅、雕塑厅、长廊的整段空墙上，间距约 4.2 m，带柱础柱头；碰到画、展板、门自动跳过 |
| 门套 | 所有带过梁的门洞两面加深色木门套 |
| 天花 | 油画厅、接待厅、服装厅、二层长廊加深色井字梁 |
| 雕塑厅 | 楼梯口 7 m 开口加拱梁 + 两根立柱，南侧再两根，大理石柱身 |
| 地面 | 木拼花打底；接待厅、雕塑厅、楼梯厅、厕所铺石材方砖 |
| 通风口 | 5 个高处铸铁格栅 |

不共面规则：每层出墙深度都不一样（1.2 / 2 / 3.5 / 4.5 / 5 / 6 / 16 cm），厚的几层在画、展板、楼梯前断开。

### 道具模型（`Assets/Art/Models/Props`，19 个全部用上）
| 位置 | 摆了什么 |
|---|---|
| 接待厅门两侧 | 西南角：沙发 + 圆边桌 + 陶罐 + 扶手椅；东南角：扶手椅 + 圆边桌 + 青瓷笔罐 |
| 接待厅北墙两角 | 石材展台：西边青花梅瓶，东边窑铸玻璃雕塑 |
| 油画厅 | 3 张长凳（两张对西墙大画、一张对《向日葵》）；《戴珍珠耳环的少女》《黄玫瑰夫人》《最后的晚餐》前拉黄铜立柱红绳 |
| 服装厅西墙 | 3 个矮首饰柜（玻璃罩、丝绒垫）：胸针、手镯、吊坠（放大 1.6 倍，7 岁视角看得见） |
| 二层长廊 | 南端一张长凳；《蒙娜丽莎》《黄玫瑰夫人》前拉绳 |
| 二层楼梯口东北角 | "没收拾完的修复角"：画架、折叠凳 + 调色板、颜料箱 + 画笔、颜料管、松节油罐、笔罐、画布卷、防尘布，用绳围着 |
| 值班室 | 桌前一把扶手椅 |

### 博物馆杂项（几何体搭的）
- 4 个灭火器（带挂架和红色标牌）：接待厅东西墙、服装厅西墙、二层长廊西墙
- 4 个绿色出口灯（带微弱绿光）：正门里侧、接待厅两侧门、二层厕所门
- 4 个监控摄像头（红色指示灯）：接待厅、油画厅、服装厅、长廊各一个角
- 一层楼梯底下：保洁推车 + 拖把 + 黄色"小心地滑"牌
- 二层锁着的门旁：3 个木箱 + 一架靠墙的梯子
- 每幅真名画旁边一块说明牌（离地 1.3 m）

没放东西的地方：全家福厅（只有画架和《美术馆》）、天使周围 4 m、门洞、楼梯、人偶路线、长廊展柜之间的通道、黑屋。

### 灯光（明暗对比）
- 底光调暗（7 → 3.5），环境光降到 0.05，雾稍浓（0.022），远处自然沉进黑里
- 每件展品一盏暖色射灯（共 34 盏），按距离平方给亮度，画面亮、画与画之间暗；《最后的晚餐》这种宽画拆成几盏并排
- 两座雕像的射灯开阴影；全馆开阴影的灯共 7 盏（接待厅 2 盏为防黑屋漏光 + 天使天窗 + 黑屋 2 盏 + 2 座雕像）
- 楼梯厅一二层的灯换成冷白色，各有一盏接触不良会闪
- 每盏底光配一个吊杆 + 发光灯罩

### 浮尘（`Assets/Scripts/Environment/DustMotes.cs`）
- 900 颗白色尘点，发射盒 14 × 7 × 14 m 跟着相机走，粒子在世界空间慢慢飘（低频噪声 + 极弱上浮），出生和消失都淡入淡出
- 用受光的 Particles/Lit：光柱和射灯下最明显，暗处几乎看不见
- 推门前（馆内灯组没开）不发射，黑屋里没有尘
- 密度、大小、透明度、漂浮速度都在组件 Inspector 上可调

截图：`Docs/Shots/01_Reception.png` … `06_DustInSpotlight.png`。

## 自建模型与几处修正（2026-10-27 晚）
新文件 `Assets/Editor/GrayboxBuilder.Models.cs`：没有现成模型的东西都用代码建模。回转体（人台躯干、长裙、外套、挂衣、童装、洗手盆）用"车床"网格：一条轮廓线绕 Y 轴转一圈，网格存为 `Assets/Art/Models/Generated/Mesh_*.asset`；其余用方块和圆柱拼。

| 位置 | 做了什么 |
|---|---|
| 服装厅三面墙 | 黄铜挂杆 + 托架，16 件衣服挂在衣架上（裙子 / 大衣交替，8 种压暗的年代布料色），每件带薄碰撞盒，人走不进衣服里 |
| 服装厅 3 组玻璃罩人台 | 深色木底座 + 透明玻璃罩 + 黄铜棱线；每组 3 个无脸蛋头人台，穿长裙或外套。第 3 组往东挪 0.6 m，给接待厅 ↔ 服装厅的门留出通道 |
| 3 个会动的人台 | 红色胶囊换成同款人台（无衣服、无碰撞，路线不变） |
| Lily 的小裙子 | 东南角小玻璃柜里一个童装人台 + 粉色蓬裙 + 展签 T10 |
| 二层长廊 5 组展柜 | 0.9 m 实心木柜身（凸镶板、黄铜压条，蹲下能藏住）+ 0.4 m 玻璃罩，里面是旧信纸、速写本、钢笔。玻璃在 Ignore Raycast 层：挡人、不挡视线判定，也不投影 |
| 接待厅前台 | 镶板柜身、挑出的石材台面、踢脚和黄铜线；员工那面是抽屉（C_FrontDesk_Drawer 原名保留）和柜门；台面上电话、摊开的登记簿和笔 |
| 二层厕所 | 东墙 3 个隔间（2 m 高、离地 15 cm，门关着，有把手和红 / 绿指示，第三间贴"暂停使用"），整排一个实心碰撞盒封住；镜子下面洗手台 + 3 个盆和龙头；南墙两个小便斗；烘手机、纸巾盒、垃圾桶、地漏、拖把桶、小心地滑牌、天花通风口。手电架子从门口就能看到 |

修正：
- 二层锁着的门旁边那架梯子方向反了（顶端悬空）。现在梯脚着地、顶端正好抵住墙面，护墙板也不会再为梯子断开。
- 全家福厅门口：第 2 组展柜从 z 14.07 挪到 16.8；《蒙娜丽莎》的隔离绳只拉在画前（z 10.3–12.2），《黄玫瑰夫人》前面已有展柜，不再拉绳。
- 新增"门前通道检查"：生成时每个门洞两侧留 2.5 m 宽、3 m 深的空区，有带碰撞的布置物伸进来就在 Console 警告。锁死的门、配电间保安柜、贴墙的画和展板不算。现在全部畅通。
- 全场道具做过一遍悬空检查：剩下离地的都是故意的（挂衣、墙上的东西、柜面镶板）。

截图：`Docs/Shots/07_Gallery_Ladder.png` 到 `12_GalleryVitrine.png`（梯子、全家福厅门口、厕所、服装厅、前台、长廊展柜）。

## 孩子视角放大 + 收尾（2026-10-27 夜）
### 整体放大 1.4 倍（`GrayboxBuilder.WorldScale`）
- 主角是 1.3 m 的孩子（眼高 1.15 m，**不放大**），馆和馆里的一切都放大 1.4 倍，"在孩子眼里什么都很大"。
- 做法：所有常量和摆放表仍按 GDD 的 1 倍值写；生成的最后一步把场景各根物体绕原点放大（`ApplyWorldScale`），灯的照射距离 × 1.4、亮度 × 1.96（照度不变）。天使布置脚本的巡逻点和导航网格范围也乘了同一个倍数。
- **实际建成尺寸 = 常量 × 1.4**：楼体 56 × 36.4 m，一层净高 8.4 m，二层地面 8.96 m，黑屋画架到门 19.6 m，正门 4.2 × 5.0 m。GDD 里的尺寸以后可以按这个倍数更新。
- 楼梯每跑从 15 级改成 23 级，放大后每级 0.195 m（角色迈步上限 0.3 m），已实测走上走下都正常。二层楼梯口的修复角往东让了一点。
- 长廊展柜木柜身放大后 1.4 m，比孩子高，不用蹲就能藏住；任务票据放在玻璃罩顶上。
- 交互距离 2.5 m；相机远裁剪 90 m；穿透调试视图的馆 / 黑屋范围同步放大。

### 收尾
- **天花**：一层油画厅、接待厅、服装厅和二层长廊、二层楼梯厅、值班室统一成木板天花 + 深色井字梁（二层原来是灰色抹灰）。全家福厅、厕所、雕塑厅、配电间保持抹灰。
- **每盏灯都有灯具**：画和展品的射灯都有吊杆 + 灯筒 + 发光灯片；服装厅首饰柜和接待厅展台上方是垂下来的吊杆射灯，正对下方打光（亮度提高 2.5 倍）；黑屋两盏灯有灯具；Lily 柜顶内侧有粉色灯片。生成时自动检查，没有灯具的灯会在 Console 点名，目前全部通过。
- **雕塑厅角落两座雕像**（程序建模）：《提灯人》（披袍立像，手里提着一盏还在发微光的灯）、《跪者》（跪地前倾、头埋进手里），都面朝天使所在的大厅中央。
- **二层空白展板**：W2 换成《呐喊》、W3 换成《拿破仑翻越阿尔卑斯山》（馆里原有、之前没挂出来的两幅模型）；W1 / E3（停电后变空框的两幅）是程序画的暗色无脸肖像和夜景风景 + 做旧描金框；Grampus 证件照是小幅肖像 + 深色木框；Lily 看门的速写是铅笔稿；全家福厅北墙的《美术馆》按 GDD 盖着一块布。
- **挂衣轻晃**（`Assets/Scripts/Environment/GarmentSway.cs`）：移动中的人台走到 4.5 m 内，附近的衣服绕衣架挂钩轻轻摆一下再慢慢停。
- **会动的人台**（`Assets/Scripts/Threats/MannequinPatrol.cs`）：沿各自路线 0.7 m/s 往返；默认不动（GDD：拿起手电才出动，到时调 `MannequinPatrol.SetAllActive(true)`）。测试：Inspector 勾 startActive，或组件右键「切换移动」。碰到即失败的判定还没做。

截图：`Docs/Shots/13`–`17`（二层木天花、长廊新画、服装厅首饰柜吊灯、《提灯人》、孩子视角比例）。

## 人群站位、油画厅布置、断电摇晃（2026-10-28）
### 保留了你手动改的地方（已写进生成代码，重新生成不会丢）
对比了你保存的场景和代码生成的场景，逐个物体比较位置、旋转、缩放、材质、显隐，找到 4 处手动修改。生成后复查，和你的版本误差都在 3 mm 以内：
| 手动修改 | 写进了哪里 |
|---|---|
| 油画厅长凳：两张挪到南墙下（门两边，放大 1.1 倍，一张转 180°），第三张删掉 | `GrayboxBuilder.Dressing.cs` 长凳那两行 |
| 接待厅青花梅瓶放大 1.3 倍 | `Pedestal(..., 1.3f)` |
| 《拿破仑翻越阿尔卑斯山》的画布往外推约 5 cm | 挂画后把 `Painting_NapoleonCrossingAlps_Canvas` 沿墙面法线外移 3.5 cm（× 1.4） |
| 二层楼梯栏杆换成石材，最上面 7 段截到二层地面以下 | `BuildStairs` 第二跑栏杆 |
另外 URP 会在灯被选中时自动加一个 UniversalAdditionalLightData 组件，这不是手动修改，忽略。改前的场景备份在 `Docs/Backup/2026-10-28_before_crowd/`。

### 人群站位（`GrayboxBuilder.Crowd.cs`，根节点 `NPC_Crowd`）
- 纯白圆柱 + 碰撞体，固定站位、面朝展品，按房间分组（以后"闭馆后人群消失"直接关根节点）。
- 共 51 个：油画厅 8、接待厅 7、雕塑厅 5、楼梯厅 2、服装厅 17、二层长廊 10、全家福厅 1（Grampus）、二层楼梯口 1。
- 身高 1.85–2.45 m（实际尺寸），全部高于 1.3 m 的主角，高矮不一；按展品名字做随机种子，重新生成站位不变。
- 每幅画 / 每件展品 1–2 个；站在隔离绳外，离墙上的画至少约 3 m。
- 天使周围 4.5 m（实际 6.3 m）内一个都没有，最近的人离天使 8.7 m；不挡门前通道、楼梯上下口、雕塑厅拱门、二层修复角，离天使和人台的巡逻线保持距离。
- 找不到合适位置的跳过不硬塞：服装厅东墙中间两组挂衣离"右侧人台"的路线太近，所以没人。

### 油画厅（`GrayboxBuilder.OilHall.cs`）
- **两盏水晶吊灯**：挂在中轴梁交点下面，从上到下是吸顶盘、吊链、花瓶灯柱、上下两层黄铜环和弯臂（6 + 10 支）、烛托、白色烛套和暖光灯泡，加上臂端垂坠、层间水晶串、下环一圈水晶帘、底部尖顶和长坠。黄铜、烛套、水晶各合并成一个网格。灯就在灯泡那一圈里。
- **断电摇晃**（`Assets/Scripts/Environment/ChandelierSway.cs`）：全馆断电后灯泡熄灭，吊灯绕吊点慢慢摆（2–4°，周期 4–6 s），隔几秒补一下力所以停不下来；两盏相位错开。来电后熄火的灯泡亮回来，摆动几秒内收住。测试：Play 时按 **F10** 切换全馆灯光（F1 调试菜单里"关闭所有美术馆灯光"也一样）。
- 断电时吊灯罩、射灯镜片也一起熄灭（`LampGlow.cs`），不再"灯灭了灯罩还亮"；出口指示灯、雕像手里的灯照旧亮。
- **红地毯**：沿中轴铺，带金边。
- **大型绿植**：5 盆（西北、东北、东南角，中门两侧）。
- **语音导览架**：在接待厅进来那扇门的南边，两排 10 台导览机，绿色充电灯，顶上"AUDIO GUIDE"牌子。
- **本单元导言牌**：在那扇门的北边，深红底描金框，英文标题"GALLERY I · THE OIL PAINTINGS"和一段导言。

截图：`Docs/Shots/18`–`22`（油画厅全景、吊灯特写、长廊人群、天使周围的空圈、服装厅人群）。

### 10-28 补充：文字穿墙修复 + 全家福厅只留 Grampus
- **文字穿墙**：导言牌、语音导览架上的英文是 TextMesh，原来用字体自带材质（GUI/Text Shader，ZTest Always，永远画在最上面），隔着墙也看得见。现在换成自己写的 `Assets/Art/Shaders/Text3D.shader`（同样取字体贴图，但做深度测试），材质 `M_Text3D`；`Text3DFont.cs` 在字体贴图重建时把贴图重新挂上。场景里所有生成的文字都走 `Label3D`，一处改全修。已从墙另一侧（接待厅）实测看不到字。
- **全家福厅**：只站一个人 —— `NPC_Grampus_FamilyPortrait`，在全家福画架前，面朝画、背对门口（在门前通道外）。人群总数 51。
- 重新生成前又对比了一次你保存的场景：除了这两处，没有新的手动修改。备份在 `Docs/Backup/2026-10-28_before_textfix/`。
- 截图：`Docs/Shots/23_IntroPanel_Front.png`、`24_IntroPanel_BehindWall.png`、`25_FamilyRoom_Grampus.png`。

## 颜色
灰 = 墙 / 普通道具 · 米色 = 展品（画、雕塑、衣服）· 橙 = 关键交互物（前台抽屉、保安柜、监控屏、手电、拾取物）· 红 = 怪物占位 · 蓝 = 玻璃（展柜、镜子）· 棕 = 门板。

## 新增脚本
- `Assets/Scripts/Environment/GrayboxNote.cs`：Scene 视图文字批注，运行时不做事。
- `Assets/Scripts/Environment/WaypointPath.cs`：路点路径（人台巡回用，现在只画 Gizmo）。
- `Assets/Scripts/Core/DebugSpawnSwitcher.cs`：数字键 1–9 传送，只在编辑器 / Development Build 生效。
- `Assets/Scripts/Core/DebugMenu.cs`：F1 调试菜单（自由飞行开关 + 飞行速度），同上只在编辑器 / Development Build 生效。
- `Assets/Scripts/Interaction/IInteractable.cs` / `PlayerInteractor.cs`：E 键交互（准星细球扫 2.2 m，OnGUI 临时提示），挂在 Player 预制体上。提示字号按屏高算（`Screen.height × 0.0325`，1080p ≈ 35 px，原来固定 22），带深色描影。
- `Assets/Scripts/Interaction/DoubleDoor.cs`：双开门（缓开 / 瞬关 / 上锁）。
- `Assets/Scripts/Environment/MuseumEntrance.cs`：序章大门的灯光分段、视线检测和关闭通道。
- `Assets/Scripts/Environment/DustMotes.cs`：空气浮尘（2026-10-27）。

截图：`Docs/Graybox_Screenshot.png`（俯视：一层 / 二层 / 黑屋），`Docs/Graybox_FP_*.png`（第一人称：画架、转身看门缝、开门中、门开、接待厅）。
10-22 晚复查截图：`Docs/check_spawn_easel.png`（出生视角）、`Docs/check_behind_canvas.png`（绕到画布背后）、`Docs/check_crack_blackroom.png`（黑屋看门缝光，单面可见）、`Docs/check_door_reception.png`（接待厅侧：看不到缝光、两个把手对称）、`Docs/check_door_closeup.png`（中缝特写）、`Docs/check_prompt.png`（放大后的交互提示）。

## 10-30：全家福贴图 + 文本拾取物 + 写回手改
（坐标：括号里是放大后的世界坐标 ≈ 常量 × 1.4。**还没在 Unity 里重新生成验证**。）

### 全家福贴图
- `AddEasel` 画布加宽到 1.78 × 1.0 m（原图 16:9，不裁），正面贴 `FamilyPortrait` Quad，材质 `M_FamilyPortrait`，贴图 `Assets/Art/Textures/Paintings/FamilyPortrait.png`。黑屋和二楼暂用同一张。

### 文本拾取物（GDD §6.11.2，19 个物体）
- 都挂 `TextPickup` 并填 `textId`；PlayerInteractor 球扫忽略 Trigger，所以都是实心 BoxCollider。
- 在 `BuildTextPickups` 里统一放，调用位置在 `BuildCrowd` 之后：新碰撞体不会挤动人群站位。

| textId | 物体（Props_Museum 下） | 世界坐标 ≈ | |
|---|---|---|---|
| T1 | 1F/Reception/Intro_Wall_TheAudience | (27.3, 3.4, 12.3) | 复用 |
| T4 | 1F/Reception/FrontDesk/Register_Book/Cover | (29.0, 1.4, 9.7) | 复用 |
| T5 | 1F/Reception/FrontDesk/Text_T5_StickyNote（台面电话旁，前台没有屏幕） | (26.1, 1.4, 9.9) | 新 |
| T14 | 1F/Reception/FrontDesk/Text_T14_Workbook（抽屉面板外，左） | (27.0, 1.0, 10.3) | 新 |
| T15 | 1F/Reception/FrontDesk/Text_T15_CaseReport（抽屉面板外，右） | (27.6, 1.0, 10.3) | 新 |
| T9 | 1F/PowerRoom/Text_T9_ServiceSheet（西墙） | (24.1, 1.8, 15.1) | 新 |
| T10 | 1F/CostumeHall/LilyDress_Case/Label_T10（补回碰撞） | (53.3, 0.25, 2.2) | 复用 |
| T6_1 | 2F/Gallery/DisplayCase_Cover_1/Text_T6_1_StudioNote1（柜身北面） | (20.6, 9.8, 24.9) | 新 |
| T11 | 2F/Gallery/DisplayCase_Cover_2/Text_T11_Clippings（南面） | (31.2, 9.8, 22.8) | 新 |
| T6_2 | 2F/Gallery/DisplayCase_Cover_3/Text_T6_2_StudioNote2（南面） | (20.6, 9.8, 15.1) | 新 |
| T6_3 | 2F/Gallery/DisplayCase_Cover_5/Text_T6_3_StudioNote3（北面） | (20.6, 9.8, 9.8) | 新 |
| T2 | 2F/FamilyRoom/FamilyPortrait_Easel/Text_T2_PortraitLabel（右腿正面） | (42.6, 9.8, 19.7) | 新 |
| T3 | 2F/FamilyRoom/Text_T3_MuseumLabel（北墙，防尘布东侧） | (54.4, 10.8, 26.3) | 新 |
| T6_4 | 2F/FamilyRoom/Text_T6_4_LastPage（放在 Text_T6_4_Stand 小展台上） | (54.4, 10.2, 26.0) | 新 |
| T12 | 2F/FamilyRoom/Pickup_FlowerAndNote | (42.1, 9.0, 19.2) | 复用 |
| T7 | 2F/DutyRoom/A_HandoverLog | (8.1, 10.0, 13.0) | 复用 |
| T8 | 2F/DutyRoom/Text_T8_NightRules（东墙，门北边） | (17.9, 10.8, 9.1) | 新 |
| T13 | 2F/DutyRoom/Text_T13_StockForm（桌面文件夹） | (10.1, 10.0, 13.0) | 新 |
| T16 | 2F/DutyRoom/Text_T16_DeathRuling（桌面文件夹） | (10.9, 10.0, 13.2) | 新 |

- 长廊的纸贴在柜身镶板外面，不放玻璃罩顶（顶面放大后 1.96 m，孩子眼高 1.15 m，看不见也扫不到）。贴哪一面避开了画中人站的那一侧。第 4 个展柜顶上的画室门票没动。

### 写回手改（备份 `Docs/Backup/2026-10-30_before_familyportrait/`）
- 人群不生成 `NPC_Garments_E12_0`、`NPC_Vitrine_Pendant_1`、`NPC_StatueOfLiberty_1`（`CrowdRemoved`）。站位照算、照样占位，其余人位置不变。
- `NPC_LilyDress_Case_0` 挪到 (34.27, 9.66)，朝向 −45.4°（`CrowdMoved`，高度不变）。
- 黑屋画架 `FamilyPortraitSketch_Easel` 不要后腿（`AddEasel(..., backLeg: false)`），二楼的画架照旧。
- 服装厅西北角绳围人台：`Props_Museum/1F/CostumeHall/RopedMannequins_NW`，5 个无衣无脖人台（朝向 90°）+ 两段立柱红绳（x 28.78，z 22.58–25.28；z ≈ 22.69，x 26.26–28.75）。原来手摆在 `Threats_Placeholder/Mannequin_Top` 下，现在是独立的静态物体，每块都有 BoxCollider，导航网格按物理碰撞烘焙会绕开。也放在人群之后。
- 巡逻人台 `Mannequin_Top` 的 A 点从 (28.7, 22.8) 东移到 (29.45, 22.8)，否则起点压在绳子上（巡逻是直线移动，不看碰撞）。B 点不变。人群禁区那条线仍按 28.7 算（不动现有站位），只是多挡 0.75 m。
- `Mannequin_Top` 在场景里手改的起点没写回（运行时巡逻会覆盖）。

### 10-30 第一次 Play 反馈后的修改
- 黑屋出生点 `CP0_BlackRoom_Start` 从画架前 1.6 改成 2.4（放大前），主菜单视角能看到整幅《全家福》，顶部不再被裁。
- 新加的文本（T2、T3、T5、T6_1–T6_4、T8、T9、T11、T13–T16）放大到原来的约 1.5 倍，改成「深色底板 `M_TextBoard` + 偏暗纸面 `M_TextPaper`」（`Framed()`）。原因：手电近距离直照时，浅色纸和浅色台面一起过曝，分不出来。复用的现有物体（T1、T4、T7、T10、T12）没动。

## 10-30 交互提示：交互物组件核对
- 场景现有交互物：正门 `DoubleDoor` ×1，便签 `TextPickup` ×19，都带碰撞体。
- 补上：`Toilet/Pickup_Flashlight` 挂 `FlashlightPickup`（Gameplay 写的），用自身 BoxCollider 被准星命中。需重新生成才进场景。
- 画作不做交互（老大 10-30 定）。
- 生成前备份：`Docs/Backup/2026-10-30_before_regen3/`。

## 10-30 场景切换：触发器与组件

按 Interfaces.md「场景切换与检查点」「Threats ← 状态」挂组件，全部在 GrayboxBuilder.cs 里生成（坐标为放大前 1 倍值，实际 ×1.4）。
- `FamilyPortrait_Easel`：挂 `FamilyPortraitExit`（碰撞体用画架子物体，交互检测是 GetComponentInParent）。
- `EmptyFrame_SecondVisit/Trigger_EmptyFrame`：isTrigger 盒，盖住框内 2.5×1.8（y 0.3–2.1）并向黑屋侧多 0.5 m；`StateTrigger` step = EmptyFrame。
- `Triggers_Flow/Trigger_PowerOut_Stairs`：楼梯平台（"中段"）整块，x 13.15–15.5、z 21.15–25.85、平台面 y 2.7 往上 2 m；step = PowerOut。
- `--- Spawns ---/CP_PowerOut_Stairs`：(14.3, 2.72, 22.3)，朝东（yaw 90，第二跑上楼方向）；不进调试 F 键列表。
- `Door_Duty_MagLock`：挂 `MagLockDoor`，slide = (0, 0, −1.68)。门在 x = 13 的墙里、沿 Z 宽 1.2 m（z 2.4–3.6），往南滑一个门宽缩进墙里，门洞全空；默认 −1 不够门宽（且没乘 1.4）。
- `A_MonitorWall`：挂 `MonitorTerminal`（自带 BoxCollider）。
- `Threats_Placeholder/PaintingFigure_A`、`PaintingFigure_B`：挂 `PaintingFigure`；占位胶囊没有子物体，stillVisual/outVisual 未填。
- 触发盒是空物体 + isTrigger BoxCollider（无渲染），加 NavMeshModifier ignoreFromBuild，不进天使的一层 NavMesh。
- 备份：`Docs/Backup/2026-10-30_before_regen4/`（GrayboxBuilder.cs.txt、Graybox.unity.txt）。待 Unity 里重新生成后验证。

## 2026-10-31 修正（第 6 轮）
- 前台 T14/T15 文件夹挪到柜身员工那面、抽屉左右（本地 x ±0.62, z 0.407）；`布置后半段` 菜单会把旧场景里的两份挪过去（`MoveDeskNote`）。原来贴在抽屉面板前，抽屉拉出穿模，抽屉碰撞体还挡住准星。
- 楼梯镜头：`FirstPersonController` 把每级台阶的高度跳变记到镜头上再指数放掉（`stepSmoothingSpeed`），下楼走出台阶边会贴回下一级（`SnapDownStep`），头摆按 0.15 s 着地宽限判断。
- 全家福：`FamilyPortraitExit` 先把镜头 2.6 s 推向画架上的 Canvas，推到一半开始 1.8 s 渐黑；真结局推镜头开始时就切 Ending，馆藏停下。参数都在组件 Inspector 上。
- 开门确认窗按钮：`PaperButtonHover`（淡墨底色 + 文字放大 1.06 + 下划线）。
- 备份：`Docs/Backup/2026-10-31_before_fix6/`。

## 2026-10-31 jump scare / BGM / 天使楼下等待
- 抓住玩家先放 jump scare 再 Game Over：`AngelJumpScare`（运行时自生成，约 1.35 s，unscaled 时间）——锁输入停威胁、天使瞬移到相机前 0.7 m、镜头怼头 + FOV 推 34° + 抖动、白闪转暗红闪烁、2D 满音量 `Sfx.AngelScream`，切黑后回调 `GameOverScreen.Show()` + 天使回巡逻起点。数值在 `AngelJumpScare` Inspector 上；尖叫音效由 `Tools/gen_sfx.py` 末尾的 `angel_scream()` 合成（重新生成：`python3 Tools/gen_sfx.py Assets/Resources/Audio`，放在最后所以已有 wav 不变）。
- 全局 BGM：`MuseumBgm`（自生成 2D 循环，`Resources/Audio/BGM_Museum.mp3`，公有领域见同目录 LICENSE）。BlackRoom 按「出生点离大门距离 → 0」SmoothStep 从 0 升到 0.5；PaintedGallery 起保持最大；第一次进 BlackRoomReturn 淡出 2.5 s 并停，本局不再播。常量在 `MuseumBgm` 顶部。
- 天使楼下等待：进 Attack 时玩家在一楼地面 + 4 m 以上就不下展台，Warp 到 `DBG7_Stairs_1F` 东侧 2 m、朝西罚站（被不被看都不动），玩家下到 +1.5 m 以内才从最近巡逻点照常开始；Warp 失败打 warning 退回展台下地。参数在 `WeepingAngel` 的「楼下等待」组。`SecurityMonitors` 的 `MoveAngelsOutOfFeed`/`Occluded` 已删。

## 2026-10-31 便签声音 / 前台便签 / Get The Flash / 体力 / 值班室门
- **便签声音**：`Interaction/TextPickup.cs` 打开纸面的 `Sfx.PlayUI(Sfx.PaperFlip, ...)` 从 0.6 降到 0.25；全项目只有这一处播 `PaperFlip`（`PaperFlip_0..2` 目前没有代码用）。
- **前台便签**：`GrayboxBuilder.SecondHalf.cs` 的 `NotePosT14/T15` 从 x = ∓0.62 改成 ∓0.75（y 0.73、z 0.407 不变）。抽屉宽 ±0.35、两侧柜门从 ±0.95 开始、便签宽 0.26，0.75 正好在中间。旧场景跑一次 `Tools/Ib/布置后半段（电话、正门锁、工作卡、抽屉）` 才会挪过去。
- **Get The Flash**：`HintId.GetFlash = 7`，`Resources/Texts/Hints.asset` 末尾第 7 行 `Get The Flash`（`HintTable.Get` 是 `lines[(int)id - 1]`）；`SceneTransitions.CutPower` 在 `SetState(PowerOut)` 后调 `ShowHintOnce`。
- **体力**：`Player/PlayerStamina.cs` 的 `maxStamina` 5 → 3.75（原来的 3/4），`recoverSeconds` 仍是 3 s。Prefab / Scene / Editor 里都没有覆盖这个字段。
- **值班室门**（`Interaction/MagLockDoor.cs`）：不再沿 `slide` 平移。合页边、门宽运行时从门板自己的 Renderer 包围盒算：门在 x = 13 的墙里、宽 1.2（世界 1.68），合页取靠墙的南侧竖边 + 门板朝室内的一面，绕竖轴向值班室（−X）转 90°，开到头贴在门洞南侧门套上，扫不到值班室的墙；关着时的位置和朝向不变。
  - 时序：停电（进入 PowerOut）→ `PowerClunk` + 2.5 s 向里开；拿到手电后玩家进屋（离门墙 > 1.5 m、同一层，位置判断，无新触发器）→ 0.15 s 猛关 + `DoorSlam`；监控过场 `SecurityMonitors.Playing` 由 true 变 false → 3 s 缓开 + `DoorCreak`。关着时门板碰撞体跟着回到原位，出不去。
  - 读档 / 重生：状态 < PowerOut 瞬间关；≥ PowerOut 瞬间开着；PowerOut 且已经关过时重生会重新打开并允许再关一次。
  - 生成器（`GrayboxBuilder.cs` 约 574 行）只留 `AddComponent<MagLockDoor>()`，删掉了 `SetVector3(..., "slide", ...)`。音效 `DoorSlam` / `DoorCreak` 在 `Tools/gen_sfx.py` 末尾合成，生成命令不变：`python3 Tools/gen_sfx.py Assets/Resources/Audio`。

## 2026-11-01 修正（第 7 轮）
- 二楼名画人物 jump scare：原来按包围盒顶找脸，但 Girl 模型是趴着从画框里爬出来的，包围盒顶是画框顶，结果镜头对着画框和下半身。现在 `PaintingFigure.Catch` 把 `Visual_Girl` 下的 `Head` 骨传给 `AngelJumpScare.Play`：脸 = Head 骨沿颈→头再伸 0.9 倍颈头距离，整尊平移让脸落在镜头前 0.55 m，镜头直接对准脸、每帧跟着动画修正，FOV 50。天使不传骨骼，行为不变。
- 文本收集编号：`TextCatalog`（在 `TextDocument.cs`）按 T 编号排 19 份，阅读器左上角显示「No. n / 19」，暂停菜单 Texts 页标题显示已读数 / 19，列表按编号排并带两位编号。新增文本要补进 `TextCatalog.Order`。
- 备份：`Docs/Backup/2026-11-01_before_fix7/`。
- （同日更正）名画人物 jump scare 改走 `AngelJumpScare.PlayFace`：按眼白/瞳孔子网格顶点找眼睛和脸朝向，把整尊转成正脸对镜头；演出期间相机只渲染第 31 层（纯黑背景，等于一段固定 CG）；近裁剪面推到鼻尖前，切掉垂在脸前的头发。为此 `Girl.fbx` 导入设置打开了 Read/Write（`isReadable: 1`）。

## 2026-11-02 第五批：黑屋画交互 / Lily 下移 / 笔触法线 / 全家福厅门锁

菜单：`Tools/Ib/布置 11-02 改动（黑屋画、Lily 下移、脸部笔触法线、全家福厅门锁）`（`Assets/Editor/GrayboxBuilder.Batch1102.cs` 的 `SetupBatch1102`）。生成灰盒的最后一步也会调用；按名字找、幂等，重复执行结果一致。

### 黑屋全家福草稿可交互
- `GrayboxBuilder.Batch1102.cs`：`Level_BlackRoom/FamilyPortraitSketch_Easel` 上 `Ensure<InspectCaption>`，用 SerializedObject 把 `captionId` 设成 `C_BlackRoomPortrait`。
- 新增 `Assets/Scripts/Interaction/InspectCaption.cs`（`Ib.Interaction`）：Prompt "Look"、Kind `Painting`、不锁输入、0.5 s 防连按。画架子物体 `Canvas` 自带 BoxCollider，准星命中后由 `PlayerInteractor` 用 `GetComponentInParent` 找到组件。
- 新增 `Assets/Scripts/UI/Captions.cs`（`Ib.UI`）：`Captions.Get(id)` 读 `Resources/Texts/<id>` 的 `body`（缺失返回 null 并只警告一次）；`Captions.Show(id, seconds = 3.5)` 转给 `Presentation.ShowCaption`。
- 新增 `Assets/Resources/Texts/C_BlackRoomPortrait.asset`。`C_` 开头的不是收集文本：不进 `TextCatalog`、不写 `Presentation.ReadTexts`；暂停菜单 Texts 页只遍历 `ReadTexts`，所以不会列出来。

### Lily 肖像下移（老大 11-02）
- 坐标口径：`Props_Museum` 下 `localPosition.y` **7.8 → 7.4**（1 倍单位，差 0.4）；世界坐标 ×1.4 后 **10.92 → 10.36**（下移 0.56 m）。
- 全量生成：`GrayboxBuilder.cs` 的 `BuildProps2F` 里 `Sketch_LilyLooksAtDoor` 从 `y + 1.4f` 改成 `y + 1.0f`。
- 增量：按目标 y 反推 `dy`（第二次运行 dy ≈ 0，直接跳过）；`Sketch_LilyLooksAtDoor` 本体 `localPosition.y += dy`，`Sketch_LilyLooksAtDoor_Frame` 的每个子物体、`Sketch_LilyLooksAtDoor_Portrait` 组同步加 dy（三者父节点都是 `Props_Museum/2F/Gallery`）。
- 灯：`--- Lighting ---` 下的 `Spot_Gallery_Sketch_LilyLooksAtDoor` 也 `localPosition.y += dy`，然后对画板新的 `Renderer.bounds.center` 重新 `LookAt`。`Level_Museum/Lamp_Fixtures/Fixture_Spot_Gallery_Sketch_LilyLooksAtDoor` 挂在天花上，不动；只有灯的瞄准变了。

### 游客模糊脸的笔触法线
- `GrayboxBuilder.Batch1102.cs` 新增 `FaceBrushNormalTexture` / `FaceBrushHeight` / `FaceHeightToNormal` / `ApplyFaceBrushNormal`；`BuildVisitors`（`GrayboxBuilder.Exhibition.cs`）建完两张脸材质后调用，所以 `Tools/Ib/布置展厅细节` 菜单也会生效。
- 材质：`Assets/Art/Materials/Finish/M_Visitor_Face_A.mat` 和 `_B.mat`，设 `_BumpMap`、`_BumpScale = 0.8`（`FaceBrushNormalStrength`）、开 `_NORMALMAP`，tiling / offset 跟 `_BaseMap` 一致。Ted 的 `M_Ted_Face` 不动。
- 程序生成的图：`Assets/Art/Textures/Faces/T_VisitorFace_BrushProc_N.png`，512×256、RGB24、固定种子 1102。高度场只在脸部涂抹区（球面 UV 脸中心 u = 0.75；像素上 x = cx ± 70、y = cy − 62..cy + 68，边缘羽化）叠 20 条横向为主、略带斜度（约 ±10°）的笔刷条；每条长 30–120 px、宽 4–10 px，两端渐细、中心略隆起，条内是垂直笔触方向的高频刷毛细纹；脸外只剩极弱的布纹。
- **法线贴图交付规格**：路径 `Assets/Art/Textures/Faces/T_VisitorFace_Brush_N.png`；512×256，与脸 albedo（`T_VisitorFace_A/B.png`）同一套 UV 布局（Unity 球面 UV，脸中心 u = 0.75、v = 0.5）；PNG 8bit RGB；切线空间、OpenGL 约定（绿通道 Y+，Unity 默认）；只在脸部涂抹区有笔触，其余平坦 `(128,128,255)`；横向（u）可平铺、纵向（v）不需要。文件放进来后重跑菜单即可：导入类型自动设为 NormalMap、wrapU Repeat / wrapV Clamp，替换程序生成的那张。
- 注意：`Materials.cs` 的 `HeightToNormal` 固定按 `TexSize × TexSize`（512²）且双向 wrap；脸是 512×256、v 向 Clamp，所以 Batch1102 里照同一公式写了带尺寸的 `FaceHeightToNormal`。

### 全家福厅门锁
- 门洞：二层东线 `x = EastLineX (26)`、`z = FamilyDoorZ (13.5)`，宽 `FamilyDoorWidth (2.0)`、高 `FamilyDoorHeight (3.0)`、地面 `Floor2Y (6.4)`；门板厚 0.1、x 26 ± 0.05、z 12.5–14.5（1 倍坐标）。
- 生成：`GrayboxBuilder.cs` 的 `BuildProps2F` 的 FamilyRoom 段落建 `Door_Family_Locked`（材质同 `p.Door`，即 `Palette.Door` / `LoadPalette().Door`）+ `FamilyRoomDoor`；增量菜单在 `Props_Museum/2F/FamilyRoom` 下先删同名再建，不重复。
- 新增 `Assets/Scripts/Interaction/FamilyRoomDoor.cs`（`Ib.Interaction`，`IInteractable`，Kind Door）：合页 = 门板南侧竖边，向全家福厅 +X 开 90°，关闭姿态 = 场景摆放姿态；门宽 / 厚度 / 底边从 `Renderer.bounds` 算（和 `MagLockDoor` 同一套写法）。
- `Assets/Scripts/Flow/GameFlow.cs` 新增 `FamilyDoorUnlocked`（不随检查点回滚，和 `HasKey` 一样），加载场景时一起清 false。
- 新增 `Assets/Resources/Texts/C_LockedDoor.asset`；`MainDoorLock` 没钥匙时 Prompt 从 "Push" 改成 "Locked"，并在 `TriedLockedMainDoor` 之后加 `Captions.Show("C_LockedDoor")`。

门的状态表（`monitorsDone` = `SecurityMonitors` 不存在或 `Playing == false`）：

| 条件 | 门 |
| --- | --- |
| State < AngelsActive | 开（瞬间） |
| State == AngelsActive 且监控过场在放 | 开（等放完再关） |
| State == AngelsActive 且监控过场刚由 true → false | 0.25 s 猛关 + `DoorSlam`；玩家在门洞内侧 2.2 m 内、全家福厅一侧时推迟到离开 |
| State == AngelsActive 且过场放完、未解锁 | 关（锁住，挡人） |
| 有钥匙按 E | `KeyClick` → `FamilyDoorUnlocked = true` → 2 s 缓开 + `DoorCreak`；钥匙不消耗 |
| 没钥匙按 E | `LockRattle` + 字幕 `C_LockedDoor` |
| FamilyDoorUnlocked | 开（读档 / 重生也保持） |
| State == Ending | 保持当前姿态 |


## 2026-11-02 第六批：人群声跟 BGM 淡入 / 正门锁悬空 / 配电室门缝 / 全家福厅门高亮 + 锁

备份：`/tmp/ib/before6/`。菜单：仍是 `Tools/Ib/布置 11-02 改动（黑屋画、Lily 下移、脸部笔触法线、全家福厅门锁）`（只有第 4 项的锁体要跑菜单，其余是运行时脚本）。

### 1. 人群声不再开局就响
- BGM（`Core/MuseumBgm.cs`）：MainMenu 不播；进 BlackRoom 后目标音量 = 0.5 × SmoothStep(1 − 玩家到大门距离 / 出生点到大门距离)，每帧指数平滑（3/s）；PaintedGallery 起满音量。
- 原来 `AmbientAudio` 的 CrowdMurmur 一离开 MainMenu 就 1.5 s 淡到 0.55，所以开局在黑屋就听到嘈杂声。
- 现在：`MuseumBgm.Envelope`（= volume / MaxVolume，0..1，不含音乐音量和心跳压低）；人群音量 = 0.55 × Envelope × Effects 音量，同一起点、同一条曲线。淡出（人群消失 / 状态越过 PaintedGallery）仍是 2 s。删掉 `CrowdFadeIn`。

### 2. MainDoor_Lock 悬空
- 根因：`MainDoorLock` 只开关 `LockPlate`，「布置正门钥匙」加的 `Keyhole` / `Bolt` / `BoltKeeper` 是锁根下的兄弟节点，一直渲染，门开着就悬在门洞里。
- 修：Awake 收集锁根下除钥匙外的所有 Renderer，每帧 LateUpdate 按「State ≥ PaintedGallery 且门关着」开关 `Renderer.enabled`；碰撞体（交互）改成「真实馆里 且 门关着（插钥匙动画期间保持）」。状态驱动，重生 / 读档自然正确。

### 3. 配电室门
- 生成器（`GrayboxBuilder.Wayfinding.cs`）里门轴 `PowerRoom_DoorHinge` 是 0°（关）；以前是运行时 `PowerRoomDoor` 开局就摆成 26° 虚掩。
- 现在三段：关 0° → 监控过场 `SecurityMonitors.Playing` true→false 时 3 s 缓开到 15° + `DoorCreak` → 推门或拿到工作卡 1.5 s 开到 92° + `PowerRoomDoorOpen`。
- 15° 门缝世界宽约 0.4 m，人过不去，而工作卡柜在配电室里（`StaffCardCabinet` 只在 AngelsActive 可拿），所以新增 `Interaction/PowerRoomDoorPush.cs`（运行时挂到 `PowerRoom_Door` 门板上，门板自带 BoxCollider）：门缝状态按 E「Push」推到全开。
- 重生 / 读档：HasStaffCard → 92°；State ≥ AngelsActive 且过场不在放 → 15°（本局推开过 → 92°）；否则 0°。回滚到 AngelsActive 之前的检查点会清掉「推开过」。

### 4. 全家福厅门不高亮 + 锁
- 高亮机制：`PlayerInteractor` 细球扫 → `GetComponentInParent<IInteractable>` → `InteractHighlight` 给目标的 MeshRenderer 套一层沿法线挤出 0.03 m、只画背面的壳（ZTest 正常）。便签 / 正门锁片都凸在外面，壳的边缘露得出来。
- 根因：`Door_Family_Locked` 碰撞体、layer、组件都正常（准星能命中、CanInteract 为真），但门板 0.1 厚、正好填满门洞、缩在 0.3 厚墙的中间，壳只在四周露边，四周全埋在墙 / 门套里被深度挡掉，正面的挤出又是正面被剔除 → 屏幕上什么都看不到。
- 修：`InteractHighlight.cs` 新增接口 `IHighlightSource { Transform HighlightRoot }`，交互物可以指定描边哪部分；`FamilyRoomDoor` 实现它，描边打在门上的锁上。
- 锁体：`GrayboxBuilder.Batch1102.cs` 的 `BuildFamilyDoorLock`，节点 `Door_Family_Locked/Door_Family_Lock`（`LockPlate` 黄铜 0.04×0.26×0.16、`Keyhole` 用 `M_Keyhole`、`LockKnob`），走廊一面 x = 25.95、z = 14.28（门把手一侧，合页在南）、y = Floor2Y + 1.09，无碰撞体。先按 1 倍坐标建在 FamilyRoom 下再保持世界姿态挂到门板下（门板是非等比缩放的 Cube）。门先删后建，锁跟着重建，幂等。
- `FamilyRoomDoor` 每帧：`FamilyDoorUnlocked` 为 false 显示锁，true 隐藏（不随检查点回滚）。

## 1F 楼梯天使伏击调整（2026-11-02）
- 伏击位置不变（`WeepingAngel.TryWaitByStairs`）。
- 就位时播一声 `Sfx.StoneScrape`（`Sfx.PlayAt`）。
- 玩家下楼触发伏击（离开楼梯脚等待）后 `ambushGraceSeconds` = 1.5 s 内天使不动；被注视照样冻住。
- 抓人条件加一条：玩家脚底 y ≤ `groundFloorY + catchFloorMargin`（0.12，最低一级台阶 0.195），在楼梯上不会被抓。
- 无需重跑生成菜单；影子/剪影可见性未改（需在游戏里确认）。

## 2026-11-02 真结局 CG
- `Flow/TrueEndingCG.cs`：协程播放器（未装 Timeline）。真结局黑屏后放 4 张静帧（慢推/拉+平移、明暗起伏、1.5 s 交叉淡化，第 4 张手指处 UI 高光扫过），共约 29 s，末尾渐黑后接原有结局字幕。Enter/Esc 跳过。
- 图：运行时加载 `Resources/CG/TrueEnd/T_CG_TrueEnd_01..04`，缺图用代码生成的占位色块。`Editor/TrueEndingCGImport.cs` 在 `Assets/Art/Incoming/画作/T_CG_TrueEnd_*` 导入时自动拷进 Resources（菜单 Ib/Sync True Ending CG 手动同步）。
- BGM：`Resources/Audio/BGM_TrueEnd`（钢琴，尚未提供）→ 退回 `Audio/MenuLullaby`。
- 高光位置（锚点 0.5,0.45）是猜的，真图到了要调。
