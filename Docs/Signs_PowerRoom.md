# 厅名门牌 + 配电室门（2026-11-01）

给 Ib 技术的接口说明。所有位置都写了「生成常量（1×）」和「场景实际坐标（×1.4）」两套；
代码里只用节点名和本地旋转，不要写死世界坐标，重新生成灰盒后仍然有效。

关联生成脚本：`Assets/Editor/GrayboxBuilder.Wayfinding.cs`（新文件），
入口在 `GrayboxBuilder.Build()` 的 `BuildArchitectureDetail()` 之后：
`BuildWayfindingSigns(props)`、`BuildPowerRoomDoor(props, p)`。

---

## 1. 厅名门牌

原来的 `Exit_Reception_W` / `Exit_Reception_E` 两块绿色出口牌已从生成脚本删除，
场景重建后不会再有这两个节点。`Checkpoints.cs` 里只有 `CP4_TaskB_PowerRoom` 这个检查点名，
不引用这两块牌子，不需要改代码。

四块牌都是同一个预制结构：

```
Props_Museum/WayfindingSigns/Sign_<Name>
  BrassBorder      黄铜背板（无碰撞）
  WoodFace         深色木底（无碰撞）
  InnerRule_Top    内侧铜线
  InnerRule_Bottom 内侧铜线
  Title            TextMesh（Cinzel 衬线字，Ib/Text3D，无碰撞）
  MountFoot        底部小铜托
```

| 节点名 | 文字 | 朝向（牌面法线） | 生成常量坐标 (x, y, z) | 场景实际坐标 (×1.4) |
|---|---|---|---|---|
| `Sign_PaintingGallery` | PAINTING GALLERY | +X（朝接待厅） | (13.15, 3.80, 4.50) | (18.41, 5.32, 6.30) |
| `Sign_CostumeHall` | COSTUME HALL | −X（朝接待厅） | (25.85, 3.80, 4.50) | (36.19, 5.32, 6.30) |
| `Sign_SculptureHall_W` | SCULPTURE HALL | −X（朝油画厅） | (12.85, 3.80, 15.00) | (17.99, 5.32, 21.00) |
| `Sign_SculptureHall_E` | SCULPTURE HALL | +X（朝服装厅） | (26.15, 3.80, 15.00) | (36.61, 5.32, 21.00) |

- 尺寸（生成常量）：木底 1.92 × 0.62 m，黄铜外框 2.06 × 0.76 m；×1.4 后是 2.69 × 0.87 m / 2.88 × 1.06 m。
- 牌底离门套顶约 10 cm；全部高于门洞，不挡人，也没有碰撞体。
- 文字用 `Assets/Art/Fonts/Cinzel-Regular.ttf`，SIL OFL 1.1；材质
  `Assets/Art/Materials/Finish/M_Text3D_SignSerif.mat`，和其它说明牌共用的 `M_Text3D.mat` 分开，
  避免字体图集被覆盖。
- `Ib/Text3D` 是 Unlit + 深度测试，断电后没有灯也能读；手电照到黄铜边会有一点反光。

> 如果老大希望雕塑厅的牌子改挂在厅内侧（从雕塑厅看向两侧门），把对应
> `HallPlaque(...)` 的 normal 反一下即可，文字会自动跟着转。

---

## 2. 配电室门

### 2.1 层级与节点

```
Props_Museum/PowerRoomDoor
  PowerRoom_DoorFrame         门框（Jamb_W / Jamb_E / Head / Threshold，静态，带碰撞）
  PowerRoom_DoorHinge         空节点，门轴；位置 = 门洞西侧、北面
    PowerRoom_Door            空节点，门板中心；只挂 1 个 BoxCollider
      Slab                    门板网格（灰蓝色漆面钢板，无碰撞）
      Panel_Upper / Panel_Lower / KickPlate
      Hinge_0..2
      HandleRose / HandleLever / LockCylinder
      DoorLabel / DoorLabelSub
```

- **轴心节点**：`PowerRoom_DoorHinge`
- **门板节点**：`PowerRoom_Door`（`PowerRoom_DoorHinge` 的直接子节点；空节点 + 单个 BoxCollider，
  尺寸就是整扇门，射线检测直接打它）
- **门板网格**：`PowerRoom_Door/Slab`（无碰撞）。装饰件也都不带碰撞，避免门开合时卡住玩家。
- 门板尺寸（生成常量）：1.16 W × 2.36 H × 0.07 D m；×1.4 后 1.624 × 3.304 × 0.098 m。
- 门洞（生成常量）：x 18.90–20.10，y 0–2.40，z 12.05–12.35；×1.4 后
  x 26.46–28.14，y 0–3.36，z 16.87–17.29。

> 修正记录（2026-11-01 重建时发现）：早期版本把门板细节直接挂在 Cube 门板下，
> Cube 的 localScale（1.16 × 2.36 × 0.07）把子节点位置和尺寸又乘了一遍，门把手、门牌、
> 凹板全部错位到门外/地板下。现在门板改成空节点，细节挂在缩放为 1 的节点下，已修正。

### 2.2 轴心与开门方向

| 项 | 生成常量 | 场景实际 |
|---|---|---|
| `PowerRoom_DoorHinge` 位置 | (18.92, 0, 12.35) | **(26.488, 0, 17.29)** |
| 门板关闭时中心 | (19.50, 1.20, 12.315) | (27.30, 1.68, 17.241) |
| 门板完全打开后占位 | 约 x 18.92–18.99、z 12.35–13.51 | 约 x 26.49–26.59、z 17.29–18.91 |

- 门向**北**开，也就是朝雕塑厅（+Z）外开，不往配电室里开。
- 代码把 `PowerRoom_DoorHinge.localRotation` 从 `(0, 0, 0)` 转到 `(0, -90, 0)` 就是全开；
  门板会贴着门洞西侧向雕塑厅摆过去，不挡门洞。
- 建议动画：**1.8 s**，用 SmoothStep 或先慢后快的曲线；转到 -90° 后保持开着。
  如果要做“开一条缝再全开”，在 -18° 停 0.15 s 再继续。
- `PowerRoom_Door` 空节点上的 BoxCollider 跟着转；门框/门槛是静态碰撞，玩家不会穿过门框。

### 2.3 交互与音效

- 老大要的“拿到钥匙时门打开”由 Ib 技术在交互代码里驱动，轴心就是 `PowerRoom_DoorHinge`。
  如果要找门板做射线检测，用 `PowerRoom_Door`（有 Collider）。
- 音效：`Assets/Resources/Audio/PowerRoomDoorOpen.wav`
  - 2.200 s，单声道 44.1 kHz / 16-bit PCM，峰值 −1.3 dBFS，RMS −15.2 dBFS。
  - 0.00 s 锁舌“咔”+ 低频“咚”；0.10–1.55 s 门轴吱呀；1.53 s 限位闷响；1.83 / 1.92 s 把手回弹。
  - 建议 3D 声源挂在 `PowerRoom_DoorHinge`：`spatialBlend = 1`，`minDistance = 2`，
    `maxDistance = 30`，`rolloffMode = Logarithmic`，音量 0.8；和开门动画同一帧 `Play()`。
  - 生成脚本：`Tools/gen_power_room_door.py`，纯合成，来源已补进
    `Assets/Resources/Audio/LICENSES.md`。

### 2.4 室内新增细节

都挂在 `Props_Museum/PowerRoom` 下，靠墙，不挡门：

- `Cabinet_W`（x 17.52，朝东）、`Cabinet_E`（x 21.48，朝西）：2.1 m 高铁灰配电柜，
  浅色柜门、通风格栅、把手/锁孔、柜顶红绿指示灯、柜脚。
- `CableTray` + `TrayBracket_0..3`：顶面电缆桥架，高 5.55 m。
- `Conduit_0_0..1_1` + `JunctionBox_0/1`：从柜顶到桥架的导管。
- `InsulationMat`：地面绝缘垫；`Bucket`：墙角水桶（带碰撞）。
- `DangerSign`：东墙黄底黑字 `DANGER / HIGH VOLTAGE`，无碰撞。场景坐标约
  **(30.54, 3.57, 16.17)**，特意放在配电柜北侧、门与柜之间，避开柜顶导管（z≈14.8）遮挡。
- `PowerRoom_Door/Slab`：门板网格，灰蓝色漆面钢板 `M_Plastic_Grey`；凹板/踢脚/五金见 2.1。

原有的 `B_SecurityCabinet`（工作卡柜）和 `Breaker_Panel` 没动。

---

## 3. 生成与备份

- 备份：`Docs/Backup/2026-11-01_before_signs_powerroom/`
  （`Graybox.unity`、`GrayboxBuilder.cs`、`GrayboxBuilder.Dressing.cs`）。
- 重建菜单：`Tools/Ib/生成灰盒关卡（无确认）`；命令行：
  `Unity -batchmode -quit -projectPath ... -executeMethod Ib.EditorTools.GrayboxBuilder.BuildFromCommandLine`
- 门牌、配电室门、室内细节都由生成脚本重建，可重复执行；不会引用手改场景里的临时对象。
- 重建流程最后会自动补跑两只名画人物的生成步骤（Girl 模型 + 破框站点），见 `Docs/Graybox_Notes.md`；门牌和配电室不受影响。
- 2026-11-01 补做自动化的场景备份：`Docs/Backup/2026-11-01_before_autofigure/Graybox.unity`。

## 4. 字体授权

`Assets/Art/Fonts/Cinzel-Regular.ttf` 来自 Google Fonts 的 Cinzel，
SIL Open Font License 1.1，全文见 `Assets/Art/Fonts/OFL_Cinzel.txt`。
