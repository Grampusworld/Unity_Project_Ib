# 可清理文件清单 · 2026-11-02

> 只读盘点：除了这份文档，没有删除、移动或提交任何东西。路径相对于工程根目录 `Unity_Project_Ib/`。
> 「跟踪」= 在 git 里（`git ls-files` 能查到）；「忽略」= 被 `.gitignore` 排除。
> 未引用的判断方法：资源的 GUID 不出现在任何 `.unity / .prefab / .mat / .asset / .meta / ProjectSettings` 里，文件名也不出现在任何 `.cs` 或 `Tools/*.py` 里（`Resources.Load` 按路径加载的都算用到）。

## ⚠️ 先说一件正在发生的事

**`Docs/` 里有 93 个已跟踪文件，在今天 11:27 左右从工作区被删掉了，还没提交。** 同一时刻 `Docs/.DS_Store` 也更新了，看起来是在 Finder 里删的。HEAD 里的 `Docs/` 一共 96 个文件、约 112 MB，现在只剩 `Graybox_Notes.md`、`Interfaces.md`、`GDD_Unimplemented_2026-11-02.md` 和一个空的 `Screenshots/`。本地没跟踪的 `Docs/Backup/` 也已经不在了。

被删的有：
- `Docs/FloorPlan_1F.svg`、`Docs/FloorPlan_2F.svg`：**GDD §5.1、§5.2 直接引用了这两张图**，删掉以后 GDD 里的平面图就挂了。
- `Docs/Graybox_Screenshot.png`、`Docs/Graybox_FP_*.png`：`Docs/Graybox_Notes.md` 第 230 行引用了。
- 三份审计 `GDD_Gap_Audit_2026-11-01.md`、`…_v2.md`、`…_2026-11-02.md`，以及 `Ambient_Audio.md`、`Chase_Audio_Viewmodel.md`、`LilyPortrait_Eyes.md`、`Signs_PowerRoom.md` 这几份说明文档。
- `Docs/Screenshots/` 里 29 张，`Docs/Shots/` 里 24 张，还有 `check_*.png` 等开发截图。

这些都还在 git 里，`git restore <路径>` 就能拿回来。**需要你决定**：是要真的删（那就提交这次删除，并把 GDD 和 Graybox_Notes 里的引用一起改掉），还是至少把两张平面图恢复回来。另外，只在工作区删、不改历史的话，`.git` 一点都不会变小。

## A. 可以放心删（删了能自动重新生成，或者确定没用）

| 路径 | 大小 | git | 原因 / 代价 |
|---|---|---|---|
| `Library/` | 2.4 GB | 忽略 | Unity 的导入缓存（其中 PackageCache 1.5 GB、Artifacts 329 MB、Bee 278 MB）。**删前先关 Unity**；下次打开会全部重新导入，包会重新下载，按这个工程的体量要等十几分钟到半小时以上 |
| `Temp/` | 54 MB | 忽略 | Unity 运行时临时目录；其中 `Temp/Captures/` 53 MB 是我测试时截的图，已经挑好的那些都复制到 `Docs/Screenshots` 了（现在也被删了，见上）。删前先关 Unity |
| `Logs/` | 16 MB | 忽略 | 编辑器日志（`Editor.log` 15 MB），会重新生成 |
| `Assembly-CSharp.csproj`、`Assembly-CSharp-Editor.csproj`、`Unity_Project_Ib.slnx` | 约 260 KB | 忽略 | IDE 工程文件，Unity 打开时自动重新生成 |
| `mono_crash.137d23bcc2.0.json` | 252 KB | 忽略 | 10-28 的一次编辑器崩溃转储，没有用了 |
| `Tools/__pycache__/` | 16 KB | 忽略 | Python 字节码缓存 |
| `.DS_Store`（5 个） | 约 30 KB | 忽略 | macOS Finder 生成的 |
| `Assets/Art/Textures/Paintings/Source/FamilyPortrait_Old.png`（连同 `.meta`） | 1.7 MB | 跟踪 | 换新全家福之前的旧版本备份；没有任何引用，git 历史里也有 |
| `Docs/Backup/`（场景备份、旧生成器 `*.txt`） | — | 忽略 | **已经不在了**，不用再处理；更早提交过的版本在 git 历史里 |

**小计**：约 **2.47 GB**（几乎全是 `Library/`）。不算 `Library/` 只有约 72 MB。

## B. 大概率可以删，但要你确认

| 路径 | 大小 | git | 原因 |
|---|---|---|---|
| `Docs/` 里那 93 个已删除文件 | 约 112 MB（在 git 里） | 跟踪 | 见开头。审计文档、开发截图删了不影响推进；**平面图 SVG 建议留**，GDD 在用 |
| `weeping-angel-turnaround.miora` + `weeping-angel-turnaround_assets/` | 7.2 MB | 跟踪 | 10-22 做天使三视图时 Miora 的工程和 5 张生成图。天使已经建模完成、工程里不引用；如果以后还要回去改天使的参考图就留着 |
| `Assets/Art/Models/Angels/Angel_Base.fbx` | 32 KB | 跟踪 | 没有任何场景 / 预制体 / 代码引用（天使现在用 `Angel_Still`、`Angel_Attack1/2`）。GDD §4.1 提到过这个名字，删之前确认一下不打算再用 |
| `Assets/Art/Materials/M_Floor_Room.mat`、`M_Floor_Corridor.mat`、`M_Ceiling.mat`、`M_Stair.mat` | 约 15 KB | 跟踪 | 早期灰盒材质，现在的生成器改用 `Finish/` 下的材质，没有引用 |
| `Assets/Scenes/SampleScene.unity` | 12 KB | 跟踪 | URP 模板自带的示例场景，**还在 Build Settings 里**（第 2 个）；删之前先从 Build Settings 里去掉 |
| `Assets/TutorialInfo/` + `Assets/Readme.asset` | 约 84 KB | 跟踪 | URP 模板的说明面板，和游戏无关 |
| `Packages/manifest.json` 里的 `com.unity.collab-proxy`、`com.unity.multiplayer.center`、`com.unity.visualscripting`、`com.unity.test-framework`、`com.unity.timeline` | — | 跟踪 | 工程代码里搜不到它们的用法（版本管理用的是 git，没有多人、没有可视化脚本、没有测试、没有 Timeline）。删了能少装几个包、`Library` 小一点；`ide.rider` 和 `ide.visualstudio` 看你用哪个 IDE，留一个就行 |
| `.workbuddy/` | 56 KB | 忽略 | WorkBuddy 在这个工程里的记忆。你还在用 WorkBuddy 就留着 |
| `.git/` 体积（206 MB） | — | — | 历史里有早先提交过的 `Docs/Backup` 场景副本和大截图。不建议改历史；想省空间可以跑一次 `git gc`，效果有限 |

**小计**：本地能省约 7.4 MB；`Docs` 那 112 MB 要提交删除才算数，而且不会让 `.git` 变小。

## C. 必须留

| 路径 | 原因 |
|---|---|
| `Assets/`（含所有 `.meta`） | 游戏本体。`.meta` 里存着 GUID，删了场景里的引用会全部断掉。检查过：**没有孤立的 `.meta`** |
| `Assets/Resources/` | 代码按路径加载（`Resources.Load`），GUID 扫不到也算在用。例如 `Audio/Step_*` 脚步声是 `PlayerFootsteps` 拼名字加载的 |
| `Assets/Art/Textures/Paintings/Source/Sister_Original.png`、`TheGallery_Render.png` | `Tools/gen_lily_portrait.py`、`Tools/gen_the_gallery.py` 的输入图，要重新生成贴图就得有它们 |
| `ProjectSettings/` | 工程设置（画质、输入、Always Included Shaders、Build Settings 等） |
| `Packages/`（`manifest.json`、`packages-lock.json`） | 依赖清单，`Library/` 删了以后就靠它把包装回来 |
| `UserSettings/` | 你个人的编辑器布局等（已忽略）。删了不影响工程，只是窗口布局会重置，可留可不留 |
| `GameDesignDocument.md` | GDD，今天刚更新到 v2.3 |
| `Docs/Graybox_Notes.md`、`Docs/Interfaces.md`、`Docs/GDD_Unimplemented_2026-11-02.md` | 生成器说明、接口约定、未实现清单 |
| `Tools/*.py` | 音效、贴图、模型的生成脚本，改效果要重新跑 |
| `.gitignore`、`.vscode/` | git 规则；VS Code 调试配置（`.vscode` 是跟踪的，不用 VS Code 的话可以改成忽略） |

## 跟踪了、但更适合忽略的

- `weeping-angel-turnaround.miora` + `_assets/`：外部工具的工程文件，不是 Unity 资源。
- `Docs/` 下的大截图：每次截图都会让仓库变大，可以考虑只留关键的几张，或者挪到仓库外。
- `.vscode/`：个人编辑器配置，多人协作时一般忽略（现在只有你一个人，留着也行）。

## 汇总

| 类别 | 可省空间 |
|---|---|
| A 可以放心删 | 约 2.47 GB（不含 `Library/` 约 72 MB） |
| B 要你确认 | 本地约 7.4 MB；`Docs` 已删的 112 MB 要提交才算 |
| C 必须留 | — |

最值得做的：**关掉 Unity 后删 `Temp/Captures`、`Logs`**（约 70 MB，零风险）。`Library/` 只有在磁盘真的紧张时才删，代价是一次很长的重新导入。
