# 人群声 / 环境底噪 / 二楼厕所异响（Ib 美术 2026-11-01）

给 Ib 技术接代码用。路径固定，全部是单声道 44.1 kHz / 16-bit PCM，纯合成，无第三方素材
（生成脚本 `Tools/gen_ambient_sfx.py`）。Unity 里已按 PCM 导入、关闭 Normalize。

## 文件

| 文件 | 时长 | 峰值 / RMS | 循环 | 用途 |
|---|---|---|---|---|
| `Assets/Resources/Audio/CrowdMurmur.wav` | 24.000 s | −5.2 / −20.0 dBFS | 无缝 | 画中馆开馆时的人群低声交谈 + 脚步 + 轻咳 |
| `Assets/Resources/Audio/AmbientRoomTone.wav` | 40.000 s | −6.0 / −18.9 dBFS | 无缝 | 全程铺底：空调低频、50 Hz 电流嗡声、极轻气流 |
| `Assets/Resources/Audio/AmbientRoomTone_PowerOut.wav` | 40.000 s | −7.5 / −21.0 dBFS | 无缝 | 可选：断电后的阴冷版，没有电流声，更低、更空 |
| `Assets/Resources/Audio/ToiletNoise.wav` | 3.200 s | −1.4 / −21.2 dBFS | 单次 | 接完电话后从二楼厕所传出的金属/瓷器撞击 + 闷响 + 水管咕噜 |

`Resources.Load<AudioClip>("Audio/CrowdMurmur")` 等，名字与文件名一致。

## 循环

- 底噪用 FFT 整形噪声（本身以文件长度为周期）+ 整数周期正弦，混响用循环卷积；首尾采样严格连续。
- 人群声是事件性的：先生成 24 s + 3 s 尾巴，再用 2.5 s 等功率交叉淡化把尾巴卷回开头。实测循环点
  的采样跳变 0.0006（约 −64 dBFS），比素材自身的 99.9% 采样跳变还小，不会“咔”。
- 导入设置已经是 `compressionFormat: PCM`、`normalize: 0`、`loadType: DecompressOnLoad`；不要再改成
  Vorbis，压缩会在循环点留缝，也会改变我调好的电平。

## 建议用法

### 1. 人群声

```
AudioSource: loop = true, playOnAwake = false, spatialBlend = 0 (2D)
volume = 0.55
```

- 开场进画中馆时 `Play()`，同时 1.5 s 淡入（从 0 到 0.55）。
- `NPC_Crowd` 隐藏 / 场景切到真实馆时，用 **2.5–3 s 淡出**再 `Stop()`，不要硬切。硬切会像关收音机，
  淡出才像“人一下子不见了，只剩空调声”。
- 画中馆里不要随玩家位置做 3D 衰减：它是“循环录音”，整馆都听得到，这样才符合 GDD 的“永不与玩家互动”。
- 如果同时有馆内 BGM，人群声期间 BGM 压到 −6 dB 左右，出画后 BGM 也停，让死寂出来。

### 2. 环境底噪

```
AudioSource: loop = true, playOnAwake = true, spatialBlend = 0 (2D)
volume = 0.28   // AmbientRoomTone
```

- 从主菜单进游戏就一直播，不要随房间切换停。
- 停电那一刻：如果有 `AmbientRoomTone_PowerOut`，用 3–4 s 交叉淡化换过去（两个 AudioSource 对拉），
  音量 0.22；没有就继续播原版，不要静音。
- 暂停时跟随 `AudioListener.pause` 一起停；结局 CG 期间保留（GDD 要求过场保留环境音），Game Over 时
  2 s 淡出。

### 3. 二楼厕所异响

```
AudioSource: loop = false, spatialBlend = 0.85, rolloffMode = Logarithmic
minDistance = 2.5, maxDistance = 35, volume = 1.0
加一个 AudioLowPassFilter，cutoff 1500 Hz（隔墙、隔楼的闷感）
```

- **接完电话、电话听筒放下的对白结束之后**再播，建议延迟 0.6–0.8 s，让玩家先回到操作状态。
- 声源放在 `Toilet` 房间、`CP2_Flashlight_Toilet` 检查点附近（洗手台/隔间那一侧），不要放在走廊，
  否则方向感不对。
- 只播一次；如果玩家没上楼又触发了别的任务，不要重复播。
- `maxDistance 35` 是为了让玩家在楼下也能隐约听到；如果实际听起来太远，把 `spatialBlend` 降到 0.6，
  不要加音量（加音量会让它在楼上炸耳）。
- 播完这一条后再把任务提示更新为 **Investigate Second Floor**；停电后的 **Get the Flash** 提示删除。

## 还没验证

- 四条音效都只做了波形/频谱/电平/循环接缝检查，**没有在 Unity 里实际试听**，也没有在 5.1/耳机/笔记本
  喇叭上对比过。人群声的“像不像人”和厕所异响的“像不像瓷/金属”需要老大实际听一遍。
- 厕所异响从楼下听到的音量、隔墙低通的听感没有实测，`spatialBlend` 可能需要按实际路由微调。
