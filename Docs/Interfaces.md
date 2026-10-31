# 跨 bot 接口（草案 v0，2026-10-30）

状态：Presentation 部分已实现并编译通过（2026-10-30 16:38），未在 Play 里验证。改动请在本文件追加一行变更记录。

## Presentation → 对外（`namespace Ib.UI`，静态入口 `Presentation`）

### 任务提示
```csharp
public enum HintId { Explore = 1, AnswerPhone = 2, GetOut = 3, FindKey = 4, GetStaffCard = 5, OpenDrawer = 6, GetFlash = 7 }
Presentation.ShowHint(HintId id);   // 屏幕上方淡入一行，3 秒后淡出（GDD §3.7）
```
- 文案存在 `Assets/Resources/Texts/Hints.asset`，英文按 GDD §7.2。
- 调用方：Ib Gameplay，按 GDD §3.7 表格的触发条件调用。拿到钥匙后不再调用。
- 同一条重复调用会重新播放，不去重；去重由调用方负责。

### 文本阅读器
```csharp
Presentation.OpenText(string textId, System.Action onClosed = null);
bool Presentation.IsReading { get; }
```
- `textId` 取值：`T1`–`T16`，T6 的四页拆成 `T6_1` `T6_2` `T6_3` `T6_4`（四个拾取物，位置见 GDD §6.11.2）。每份一个 ScriptableObject，在 `Assets/Resources/Texts/<textId>.asset`。
- 打开后屏幕中央展开纸面，按交互键或 Esc 关闭，关闭时回调 `onClosed`。
- 读过的 `textId` 记入本局已读列表，暂停菜单的 Texts 页可重读。
- **待定**：读文本时世界是否继续跑。GDD §6.11.5「读笔记要冒险」暗示不冻结；本草案按「不冻结时间、只锁玩家移动和视角」写，由 Gameplay 在 `IsReading` 为 true 时锁输入。
- 调用方：Ib Gameplay（交互物被拾取时）。拾取物由 Ib Level 摆放，对象上只需带 `textId` 字符串。

### 主菜单与暂停（按 Gameplay 的改法，Presentation 直接调 GameFlow）
- UI 全部运行时生成（`PresentationRoot`），场景里不用摆。主菜单在 Awake 调 `GameFlow.RegisterMainMenu()`，菜单项逐个淡出后调 `GameFlow.StartNewGame()`。
- 依赖顺序：Presentation 在 `BeforeSceneLoad` 订阅 `sceneLoaded`，晚于 GameFlow 的 `SubsystemRegistration`，所以 GameFlow 先复位、主菜单再登记。别改这两个时机。
- Esc 由 Presentation 统一处理：阅读器开着就关阅读器，设置页开着就关设置页，主菜单时不响应，其余开关暂停（`GameFlow.SetPaused`）。
- 暂停菜单：Resume / Texts / Settings / Quit to Menu。Quit to Menu 先 `SetPaused(false)` 再重载当前场景。
- 主菜单：Start / Settings / Quit（按 GDD §7.2）。Settings：Master Volume、Mouse Sensitivity，存 PlayerPrefs。

### 给 TextPickup 的约定
- `Presentation.IsReading` 为 true 时 `CanInteract` 返回 false，避免按 E 关纸面的同一帧又被拾取物打开。

## Gameplay → 对外（`namespace Ib.Flow`，静态入口 `GameFlow`）

### 全局状态
```csharp
public enum GameState { MainMenu, BlackRoom, PaintedGallery, BlackRoomReturn, AfterHours, PowerOut, AngelsActive, Ending }
GameState GameFlow.State { get; }
event System.Action<GameState, GameState> GameFlow.StateChanged;   // (from, to)
event System.Action GameFlow.OnPowerOut;                            // 第 10 步楼梯中段断电，触发一次
bool GameFlow.HasFlashlight { get; }                                // 已拿到手电
event System.Action GameFlow.FlashlightObtained;                    // 拿到手电时触发一次
void GameFlow.GiveFlashlight();                                     // 拾取物、调试菜单都调这个，不要直接调 PlayerFlashlight.Give()
```
- 右下角 F 提示：`OnEnable` 时先按 `HasFlashlight` 设一次显隐，再订阅 `FlashlightObtained`；不要每帧轮询。重新加载场景时 `HasFlashlight` 复位为 false。
- 场景加载时一律从 `MainMenu` 开始；Quit to Menu 重新加载场景即可，Gameplay 在加载时清掉静态状态（检查点、已发提示）。
- **暂停的 `Time.timeScale` 只有 Gameplay 写**，其他 bot 只读。`GameOverScreen` 仍自己冻结，但 Game Over 期间 `SetPaused` 被忽略，两者不会叠在一起。
- 输入锁：`MainMenu`、暂停、`Presentation.IsReading` 为 true、Game Over 时，Gameplay 置 `FirstPersonController.InputBlocked` 并锁视角；阅读时不冻结时间（同意上面草案）。

### 文本拾取物
```csharp
// Assets/Scripts/Interaction/TextPickup.cs，实现 IInteractable
[SerializeField] string textId;   // "T1"…"T16"、"T6_1"…"T6_4"
```
- Ib Level 只负责把带 `TextPickup` 和 Collider 的物体摆到 GDD §6.11.2 的位置并填 `textId`；按 E 时由它调 `Presentation.OpenText`。
- 交互提示文字统一为 "Read"。拾取后物体不消失（可重复读）。
- 可读阶段（§6.11.2）暂不限制，任何时候都能读；等后面的状态做出来再按阶段开放。
- 已实现：`Assets/Scripts/Interaction/TextPickup.cs`（未编译）。读 T7 关闭后自动发「Get the staff card…」提示。

### 手电拾取物
```csharp
// Assets/Scripts/Interaction/FlashlightPickup.cs，实现 IInteractable，无参数
```
- Ib Level：在厕所（GDD §3.6 第 11 步）摆一个带 Collider 的手电模型，挂 `FlashlightPickup`。
- 只在 `PowerOut` 可拿。按 E（"Take"）：`GiveFlashlight()` → `GetOut` 提示 → 记检查点 CP2 → 物体隐藏。**状态不变，仍是 PowerOut**（GDD 第 12 步）。
- 人台、名画人物出动由 Threats 的 `ThreatSchedule` 按 `HasFlashlight && State ∈ {PowerOut, AngelsActive}` 自己算，Gameplay 不另发事件。

### 场景切换与检查点（GDD §3.6 第 6–14 步、§5.3、§3.4）
代码：`Flow/SceneTransitions.cs`（切换）、`Flow/Checkpoints.cs`（检查点）、`Flow/WorldState.cs`（每个状态的场景样子）。

| 步 | 触发（Level 挂） | 结果 | 检查点（PlayerSpawnPoint 名） |
|---|---|---|---|
| 开局 | `StartNewGame` | BlackRoom | `CP0_BlackRoom_Start` |
| 6 | `FamilyPortraitExit` 挂 `FamilyPortrait_Easel`（要 Collider），PaintedGallery 时按 E（"Look"） | 黑屏 → 传回黑屋 → BlackRoomReturn；空画框显示、正门锁死 | `CP0_BlackRoom_Start` |
| 7 | `StateTrigger`（step = EmptyFrame）放在 `EmptyFrame_SecondVisit` 下，isTrigger 盒子盖住框内 | 黑屏 → 传到二楼家庭厅 → AfterHours；黑屋停用 | `CP1_ExitPainting_FamilyRoom` |
| 10 | `StateTrigger`（step = PowerOut）放楼梯中段，isTrigger | PowerOut：全馆灯关、不再亮；`OnPowerOut`；`MagLockDoor` 向值班室内开；提示 Get The Flash | `CP_PowerOut_Stairs`（**新的，Level 加**；没有时只记状态，位置沿用 CP1） |
| 12 | `FlashlightPickup` | 见上 | `CP2_Flashlight_Toilet` |
| 14 | `MonitorTerminal` 挂 `A_MonitorWall`（要 Collider），PowerOut 且有手电时按 E（"Check"） | AngelsActive（过场以后做） | `CP3_TaskA_DutyRoom` |

- 电磁锁门：`MagLockDoor` 挂 `Door_Duty_MagLock`，合页边和门宽从门板自己的 Renderer 包围盒算（不传参）。停电时 2.5 s 向值班室（−X）内开 90°；拿到手电后玩家走进值班室（离门墙 > 1.5 m、同一层）猛地关上（0.15 s + `DoorSlam`），关着时挡住玩家；监控过场（`SecurityMonitors.Playing` true → false）结束后 3 s 缓缓打开（`DoorCreak`）。状态 < PowerOut 瞬间关；状态 ≥ PowerOut（读档 / 重生）瞬间开着，PowerOut 且已关过时重生会重开并允许再关一次。
- `WorldState` 按物体名找：`NPC_Crowd`（State ≤ PaintedGallery 时显示）、`EmptyFrame_SecondVisit`（只在 BlackRoomReturn 显示）、`Level_BlackRoom`。**Level 改这几个名字要同步 `WorldState.cs`。**
- 死亡：`GameOverScreen` 先 `PlayerSpawner.Respawn()`（落到 `PlayerSpawner.CurrentCheckpoint`），再发 `Continued`；`Checkpoints.Restore` 订阅它，把状态和灯恢复成记检查点时的样子。手电不回收。
- 黑屏用 Presentation 的 `FadeToBlack/FadeFromBlack`（各 0.6 s），期间锁输入；`SceneTransitions.Busy` 为 true。
- `InteractKind` 加了 `Painting`（只有全家福）、`Device`（监控屏）。

### 交互提示
```csharp
// namespace Ib.Interaction
enum InteractKind { Door, Text, Pickup }
IInteractable PlayerInteractor.Target { get; }              // 当前对准的交互物，没有为 null
event Action<IInteractable> PlayerInteractor.TargetChanged; // 变化时触发一次（含变成 null）
// IInteractable 成员：string Prompt、bool CanInteract、InteractKind Kind
```
- 主菜单、暂停、阅读、Game Over 期间（`GameFlow.IsAiming == false`）Target 一律为 null，UI 不用再自己判断这几种情况；老大已定：确认窗算进 `IsAiming`。确认窗现在还没做；Presentation 做时提供 `Presentation.IsConfirming`，Gameplay 再加进 `IsAiming`，Presentation 不用自己藏。
- `PlayerInteractor` 原来的 OnGUI 提示已删，提示只由 Presentation 画。
- 新交互物实现 `IInteractable` 时必须给 `Kind`；老大已定：画不能交互，不做 Painting 组件。

### 任务提示触发（GDD §3.7，每条只发一次）
| HintId | 由谁在什么时候调 |
|---|---|
| Explore | 黑屋双开门被推开、进入画中馆（`BlackRoom → PaintedGallery`） |
| AnswerPhone | 出画落地后电话铃响（进入 `AfterHours` 后几秒） |
| GetOut | 拿起手电（`PowerOut → ` 馆藏出动） |
| FindKey | 第一次与正门锁交互完毕 |
| GetStaffCard | T7 阅读器关闭时（`OpenText("T7", onClosed)` 的回调） |
| OpenDrawer | 拿到工作卡 |
| GetFlash | 楼梯中段断电（`SceneTransitions.CutPower`） |
- 拿到钥匙后不再调用任何提示。

## Threats ← 状态（`namespace Ib.Threats`，只读 GameFlow，不改状态）
- 唯一规则表：`Assets/Scripts/Threats/ThreatSchedule.cs`。
  - 天使：`AfterHours`/`PowerOut` = Peek（不动），`AngelsActive` = Attack（巡逻+追击），其余 = Still。订阅 `StateChanged`。
  - 人台（`MannequinPatrol`）、名画人物（`PaintingFigure`）：`HasFlashlight && State ∈ {PowerOut, AngelsActive}` 时出动。订阅 `FlashlightObtained` + `StateChanged`。名画人物出动后会追击：看见就追到抓住；蹲在 `DisplayCase` 后视线被挡则不会发现；已经在追时视线断开，只走到玩家消失的位置。
- 死亡重生：所有怪物订阅 `GameOverScreen.Continued`，回原位并按**当时的** `GameFlow.State`/`HasFlashlight` 重设。Gameplay 读档恢复状态时，在 `Continued` 之前或之后 `SetState` 都行（之后的话由 `StateChanged` 再修正一次）；`HasFlashlight` 若要按检查点回退，回退后需再触发一次 `StateChanged` 或在 Continued 之前改好。
- 名画人物挂法（给 Ib Level）：人物物体挂 `PaintingFigure`，可选填 `stillVisual`（画里）/`outVisual`（出画后）。

## 变更记录
- 2026-10-30 Ib Presentation：起草 v0。
- 2026-10-30 Ib Gameplay：补 Gameplay 节（GameFlow 状态与事件、timeScale 单一写者、TextPickup、提示触发表）。
- 2026-10-30 Ib Gameplay：主菜单和暂停改成 Presentation 调 `GameFlow.RegisterMainMenu/StartNewGame/SetPaused`，取代 `NewGameFadeFinished`、`PauseChanged` 两个事件，这样 Flow 不依赖 UI 代码、各自能单独编译。`GameFlow.cs` 已写，未编译。
- 2026-10-30 Ib Presentation：按 Gameplay 的改法删掉两个事件，补 Esc 分发、TextPickup 约定；UI 代码已写，编译通过。
- 2026-10-30 Ib Gameplay：加 `TextPickup`、`GameFlow.ShowHintOnce`（每条提示每局一次）。未编译。
- 2026-10-30 Ib Gameplay：加 `HasFlashlight` / `FlashlightObtained` / `GiveFlashlight()`，给右下角 F 键提示用；调试菜单改走 `GiveFlashlight()`。
- 2026-10-30 Ib Presentation：UI 整体放大 1.3 倍（`PresentationRoot.UIScale`，参考分辨率约 1477×831）；新增右下角按键提示 `ControlHints`（文案 `Resources/Texts/Controls.asset`），F 行订阅 `GameFlow.FlashlightObtained`、开局读一次 `GameFlow.HasFlashlight`，不改 Gameplay 代码。Unity 外编译 0 错误，未在 Play 里验证。
- 2026-10-30 Ib Gameplay：加 `FlashlightPickup`（拿起手电 = 进入 `AngelsActive` + `GetOut` 提示），需要 Level 摆放、Threats 订阅。
- 2026-10-30 Ib Gameplay：加交互提示接口（`InteractKind`、`PlayerInteractor.Target/TargetChanged`），删掉 PlayerInteractor 的 OnGUI 提示。
- 2026-10-30 Ib Presentation：新增屏幕下方中间交互提示 `InteractPrompt`（文案 `Resources/Texts/Interact.asset`，样式复用 `ControlHints.Row`），订阅 `PlayerInteractor.TargetChanged`，非 IsAiming 时隐藏。Unity 外编译 0 错误，未在 Play 里验证。
- 2026-10-30 Ib Presentation：新增全屏黑幕 `Presentation.FadeToBlack(float seconds, Action onDone = null)` / `FadeFromBlack(...)`（`UI/ScreenFader.cs`，盖在所有 UI 之上，不受 timeScale 影响，新调用打断旧的、旧回调不调）。注意：UI 随场景加载重建，黑幕也会复位成透明；若切换时重载场景，加载后请再调一次 `FadeToBlack(0)` 再 `FadeFromBlack`。Unity 外编译 0 错误，未在 Play 里验证。
- 2026-10-30 Ib Gameplay：场景切换与检查点（见同名一节）。`FlashlightPickup` 改成只在 PowerOut 可拿、不再切 AngelsActive。`InteractKind` 加 `Painting`、`Device`。Unity 外编译 0 错误，未在 Play 里验证（等 Level 挂组件）。
- 2026-10-31 Ib Gameplay：`HintId.GetFlash = 7`（断电提示，`Hints.asset` 第 7 行）；`MagLockDoor` 从平移改成合页开门（停电开门 → 进屋猛关 → 过场后缓开），读档 / 重生按状态瞬间摆好。
