# 追击音效 + 第一人称手电筒 素材说明（Ib 美术 2026-11-01）

给 Ib 技术接代码用。路径和名字都按约定固定，不要改。

## 1. 名画人物破框尖叫 `Assets/Resources/Audio/PaintingScream.wav`

- `Resources.Load<AudioClip>("Audio/PaintingScream")`；单声道 44.1 kHz，2.35 s。
- 结构：0 s 就是爆点（画框木头/玻璃炸开的噪声 + 低频冲击），紧接着两个喉咙叠在一起的女声尖叫（原调 + 降 5 半音的"非人"层 + 升 3 半音的刺耳层，轻微失真），0.9 s 后进入走廊混响尾巴。
- **和破框同一帧播放**（不要延迟），建议：3D 声源挂在怪物身上，`spatialBlend` 0.6–0.8（太 3D 会被距离衰减吃掉惊吓感），`volume` 1，`minDistance` 4，`maxDistance` 30，`pitch` 每次随机 0.95–1.05，避免两只怪物听起来一模一样。
- 已经很响（峰值 −0.4 dBFS），不需要再叠别的爆点音效；如果有 `GlassBreak` 之类，建议去掉或压到 0.3。

## 2. 被追击心跳 `Assets/Resources/Audio/ChaseHeartbeat.wav`

- `Resources.Load<AudioClip>("Audio/ChaseHeartbeat")`；单声道 44.1 kHz，**正好 1.000 s = 原始 60 BPM**，一拍"咚-咚"（lub 在 0.02 s，dub 在 0.30 s），首尾已对齐，`loop = true` 无缝循环。导入设为 PCM（无压缩），避免 Vorbis 循环点出现小缝。
- 2D 播放（`spatialBlend = 0`），不要做距离衰减，远近由代码控制。
- 远近映射建议（d = 到最近一只正在追击的怪物的距离，t = InverseLerp(14, 2, d)）：
  - `pitch` = Lerp(1.0, 1.8, t) → 60–108 BPM（1.8 倍以内音色仍然是闷的低频，不会变"卡通"）
  - `volume` = Lerp(0.25, 0.95, t²)（平方让近处才猛地变响）
  - 目标值用 `SmoothDamp` 约 0.4 s 跟随，避免距离抖动时心跳忽快忽慢。
- 渐入/渐出：开始追击时 volume 从 0 用约 1.0 s 淡入；摆脱追击后约 2.5 s 淡出到 0 再 `Stop()`，淡出期间 pitch 同时回落到 1.0（心跳慢慢平复）。
- 暂停时 `AudioListener.pause` 或单独 Pause；结局 / Game Over 时按淡出处理即可。
- 声音集中在 45–200 Hz，笔记本扬声器靠 2/3 次谐波也听得到；如果有 BGM，追击时建议把 BGM 压低 3–6 dB 让心跳出来。

## 3. 第一人称手电筒 `Assets/Art/Models/Props/Flashlight_Viewmodel.prefab`

- 来源：`Flashlight_Viewmodel.fbx`（Blender 脚本 `Tools/build_flashlight_viewmodel.py` 生成），prefab 由菜单 `Tools/Ib/生成第一人称手电筒 prefab`（`Assets/Editor/FlashlightViewmodelSetup.cs`）生成，可重复执行。预览图：`Docs/Previews/Flashlight_Viewmodel_preview.png`。
- **2026-11-01 起只含手电本体，不含手**（老大要求第一人称只显示手电）。FBX、生成脚本和 prefab 里的 `FL_Hand`、手套材质 `M_FL_Glove` 都已删除；`FlashlightViewmodelSetup.cs` 如果发现 `FL_Hand` 会直接报错，以后重新生成也不会带回来。
- 内容：深色金属手电，真实尺寸：筒径 3.1 cm，喇叭口灯头直径 4.8 cm，全长 22 cm（尾盖 z=−0.075 → 镜片 z=+0.145），橡胶防滑纹 + 开关按钮。
- 层级：
  - `Flashlight_Viewmodel`（根）**原点 = 握持处**；灯头朝根的 **+Z**，开关朝 **+Y**。
  - `Model/`：`FL_Body`、`FL_Grip`、`FL_Button`、`FL_Reflector`、`FL_Lens` 五个网格，没有第六个手部网格。
  - `LightMount`：空节点，位于 (0, 0, 0.147)，就在镜片前 1 mm，旋转为单位（+Z 朝前）。把 SpotLight 挂在这里。
- 包围盒（根空间）：x −0.024…0.024，y −0.024…0.024，z −0.075…0.145（纯手电，没有手腕）。
- 所有渲染器 `Cast Shadows = Off`。
- 材质（`Assets/Art/Materials/Props/`，URP Lit）：M_FL_Metal / Rubber / Chrome，镜片 `M_FL_Lens_On`（暖白自发光）和 `M_FL_Lens_Off`（不发光）。手电关掉或闪烁灭的那一拍，把 `Model/FL_Lens` 的材质换成 Lens_Off，亮的时候换回 Lens_On。

### 建议的屏幕摆放（右下角）

挂在玩家相机下（相机 near clip 0.1，FOV 按现有）：

| 参数 | 值 |
|---|---|
| localPosition | (0.17, −0.20, 0.32)（保持现有值，去手后不用改） |
| localEulerAngles | (3, −5, 0)（灯头略微朝屏幕中心收） |
| localScale | (1, 1, 1)（真实尺寸，不要乘 1.4：孩子的手在放大的世界里本来就显小） |

- 去手后尾盖正好被画面下缘切掉，灯头落在画面右下约 (78%, 92%)，看起来像握持的手在画面外，不需要额外加假手。
- 如果觉得灯头离右下角的按键提示（`ControlHints`，距右下角 32 px）太近，**优先把按键提示往上挪约 90 px**，不要移动手电：再往左下移会让它看起来像悬空。确实要微调时，可用 (0.168, −0.19, 0.315) / (4, −6, 0) 作为备选。
- 晃动建议：走路 bob 位置振幅 (0.006, 0.004)，频率跟脚步；跑步振幅 ×2.5 并加 2° 的 roll；转视角时 0.08 s 的位置滞后（sway），最大偏移 0.02。
- 贴墙穿模：最简单是把它放到单独 Layer `Viewmodel`，主相机不渲染这层，再加一台 URP Overlay 相机（只渲染 Viewmodel，near 0.01）叠在上面；或者贴墙时用射线把它往后/往下收。
- 拿到手电之前隐藏；AngelJumpScare 改 FOV/near clip 期间、结局 CG、Game Over 时也隐藏。
- 光：SpotLight 从 LightMount 发出，但光源离相机只有 ~0.47 m，贴墙时可能出现在墙后。建议保留现有的相机光（或在 LightMount 做一条往回的射线检测），确保光源不穿进墙里。

## 生成脚本

- `python3 Tools/gen_chase_sfx.py <放两个 CC0 ogg 的文件夹> Assets/Resources/Audio`（录音原文件不进工程，来源见 `Assets/Resources/Audio/LICENSES.md`）
- `/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python Tools/build_flashlight_viewmodel.py -- Assets/Art/Models/Props/Flashlight_Viewmodel.fbx`
