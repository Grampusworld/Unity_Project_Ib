# Lily 肖像（眼睛跟随）+ 库房 07 —— 美术交付说明（Ib 美术，2026-11-01）

生成：菜单 `Tools/Ib/布置结局美术（Lily 肖像眼睛 + 库房 07）`（`Assets/Editor/GrayboxBuilder.EndingArt.cs`），
可重复执行；生成灰盒最后一步也会调用。脚本第一次编译时如果开着 Graybox 场景会自动跑一次。

## 1. Lily 肖像（二楼值班室门口 `Sketch_LilyLooksAtDoor`）

- 原图：`Assets/Art/Textures/Paintings/Source/Sister_Original.png`（1280×720）
- 以小女孩为中心裁成 3:4（原图 x 300–840），输出 1080×1440：`Tools/gen_lily_portrait.py`
- 画框尺寸不变（0.6 × 0.8，放大后 0.84 × 1.12 m）。旧板子和画框留着，新画三层贴在板子前面。

### 层级（都在 `Sketch_LilyLooksAtDoor` 的父节点下）

```
Sketch_LilyLooksAtDoor_Portrait     ← 组：本地 1 单位 = 1 m；+X = 观众右手（画面右）、+Y 上、+Z 朝墙里；观众在 −Z 一侧
├── Canvas    画面，眼裂抠空（M_LilyPortrait_Canvas，URP Lit + Alpha Clip 0.5）  z = −0.0168
├── Iris_L    左眼虹膜（画面左边那只）M_LilyPortrait_Iris_L，Alpha Clip            z = −0.0112
├── Iris_R    右眼虹膜                 M_LilyPortrait_Iris_R，Alpha Clip            z = −0.0112
└── EyeBack   眼白底图（虹膜已涂掉）    M_LilyPortrait_EyeBack，不透明                z = −0.0056
```

### 给 Ib 技术：眼珠跟随参数（组的本地坐标，单位 m）

| | 静止位置 (x, y) | 直径 | 最大位移 X | 最大位移 Y |
|---|---|---|---|---|
| Iris_L | (−0.0233, 0.2551) | 0.0420 | ±0.0109 | ±0.0039 |
| Iris_R | ( 0.1136, 0.2263) | 0.0389 | ±0.0109 | ±0.0039 |

- 静止位置 = 生成时 `Iris_L/R.localPosition`，运行时 Awake 里记下来就行，不用写死。
- 位移限制按椭圆：`(dx/0.0109)² + (dy/0.0039)² ≤ 1`，超过就按比例缩回椭圆上。只改 localPosition 的 x、y，z 不动。
- 画里原本就是正视观众，静止位置 = 看正前方。玩家在画的右边（本地 +X）时，虹膜往 +X 移；玩家比画低（7 岁视角，基本都是）时往 −Y 移。
- 建议算法：`p = group.InverseTransformPoint(camera.position)`；`tx = p.x / max(0.2, −p.z)`、`ty = p.y / max(0.2, −p.z)`；
  `dx = clamp(tx / 1.0, −1, 1) · 0.0109`，`dy = clamp(ty / 0.8, −1, 1) · 0.0039`，再做椭圆限制，`SmoothDamp` 0.12–0.2 s。
  玩家在画背后（p.z > 0）就回到静止位置。
- 眼白在虹膜后面 5.6 mm，虹膜在画布后面 5.6 mm，斜着看有一点真实的视差，正是想要的效果，不用补偿。

## 2. 库房 07（坏结局 CG）

- 根节点 `Level_Storage07`（场景根，**默认隐藏**），已按 WorldScale 1.4 放大，灯的距离 / 强度也已放大。
- 占的是正门南边、黑屋那块地（生成坐标 x 14.5–24.5，z −9 到 −0.2；放大后 x 20.3–34.3，z −12.6 到 −0.3），
  **和 `Level_BlackRoom` 重叠：CG 里打开 `Level_Storage07` 时必须保证 `Level_BlackRoom` 是关着的**（进馆后 MuseumEntrance 本来就关了它）。
- 布局：正门正对一条 2.4 m（放大后 3.4 m）宽的空通道，镜头可以往里推 2–3 m；两侧沿墙堆满木箱（托盘 + 2–3 层），箱子朝通道一面喷着 "07"；
  通道尽头一排矮箱，再往后是拉下的临街卷帘，卷帘上方门牌 "07 / TEMPORARY STORAGE"，卷帘底缝漏一线冷蓝街灯；
  墙边斜靠三只扁平画箱、两件盖防尘布的雕像。
- 灯（都在 `Level_Storage07/Lights_Storage07`）：通道上方一根冷白灯管（带阴影）、卷帘缝贴地冷蓝漏光、门口一盏很弱的补光。整体昏暗冷色。
- 碰撞：地面 / 墙 / 箱子有碰撞体，CG 是摄像机动画的话不受影响。

## 3. 音效（`Assets/Resources/Audio`，`Tools/gen_ending_sfx.py` 合成）

| 文件 | 时长 | 用法 |
|---|---|---|
| `MainDoorOpen.wav` | 4.5 s（3.4 s 动作 + 混响尾） | 玩家选 Open、门开始动的同一帧播放；0 s 锁舌咔嗒，0.2–3.0 s 两扇门吱呀，3.05 s 开到底一顿（对应 DoubleDoor openDuration 3 s）。3D 放在门中间即可 |
| `EndingStinger.wav` | 4.2 s | 转身看到天使时：**播放的同一帧硬切黑屏**（峰值在 0.003 s），尾巴在黑屏里散掉后再出结局文字。2D 播放，音量 1 |

`Sfx.cs` 里还没加这两个名字的常量，`Sfx.Load("MainDoorOpen")` / `Sfx.Load("EndingStinger")` 直接能用。
