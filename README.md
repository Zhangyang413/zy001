# 捡物品大冒险（一行格子上的最优步数小游戏）

基于 **团结引擎（Tuanjie Editor 1.10.4 / Unity 2022.3.62t16）** 的 3D 小游戏：
人物和不同种类的物品等距生成在一条直线上，人物每走 **一格** 消耗 **一步**，
在 **给定步数** 内把所有种类各捡到至少一个即 **通过**，否则 **失败**。
给定步数由最优解算法 `MinMovesSolver.MinMovesToCollectM()` 计算得出。

游戏有 **入口界面**（可选难度 / 可看游戏说明 / 可开关挑战模式），人物是 **运行时拼出来的卡通小人**（戴棒球帽，会走路起伏）；
界面全部是 IMGUI，但用的是 **系统矢量字体 + 分辨率自适应**，任意分辨率下文字都清晰、都不会被面板裁掉。

## 玩法

| 操作 | 说明 |
| --- | --- |
| `←` `→` 或 `A` `D` | 向左 / 向右走一格（一格 = 一步） |
| `H` | 显示 / 隐藏最优解提示 |
| `R` | 重开本关（同一关重来） |
| `Esc` | 返回入口界面 |
| `1` `2` `3` | 在入口界面直接进入难度 1 / 2 / 3 |
| `E` | 在入口界面进入示例关卡 |
| `空格` / `N` | 结算界面：重试本关 / 换一关（重新随机）；挑战模式下重试被禁用 |
| `空格` / `回车` | 挑战模式：等价于点 HUD 上的 `Start` 按钮，开始计时 |
| `C` | 在入口界面开关挑战模式（也可以直接用鼠标点复选框） |

* 人物**起点格上的物品**在开局即视为已获得（与算法中距离为 0 的假设一致）。
* 踩到物品即自动捡起；重复种类的物品不会增加“集齐进度”，但依然消耗一步。
* 步数用尽时：**先判定是否集齐**（用尽的那一步刚好集齐也算通过），未集齐则失败。
* 走到两端继续按方向键属于无效操作，不消耗步数，只给提示。

## 关卡与难度

入口界面里可以选择：

| 入口 | 内容 |
| --- | --- |
| 难度 1 | 物品 **4~6** 个 · **3** 种，随机关卡 |
| 难度 2 | 物品 **7~9** 个 · **4** 种，随机关卡 |
| 难度 3 | 物品 **10~14** 个 · **5** 种，随机关卡 |
| 示例关卡 | `SampleScene` 里配置好的固定关卡：`n=10`、`a={1,2,3,2,1,2,3,2,4,1}`、`start=7`、`m=4`，最优 **4 步** |

随机关卡由 `LevelGenerator` 生成，保证：

* 物品数量落在难度区间内（4~6 / 7~9 / 10~14）；
* **每种物品至少出现一次**（否则永远集不齐）；
* 物品位置、人物初始位置都是随机的；
* 会重随机若干次，避免出现“一步就集齐”的无聊关卡；
* 生成时会打印 `随机种子`，可用于复现同一关。

难度区间的定义在场景对象的 `createfruit.difficulties` 数组里，可在 Inspector 直接改
（`title` / `minCells` / `maxCells` / `typeCount`）。

## 挑战模式（限时挑战）

入口界面有一个 **挑战模式** 开关（用鼠标点复选框，或直接按 `C` 键），打开后：

| 规则 | 说明 |
| --- | --- |
| **先点 Start** | 进入关卡后人物是锁住的，点 HUD 上的 `Start`（或按 `空格` / `回车`）才开始计时并解锁移动 |
| **禁用提示** | `H` 键的最优解提示被禁用，按了只会提示“挑战模式不能查看最优解提示” |
| **失败不能重试** | 失败后结算界面没有“重试本关”，空格 / `R` 都无效，只能 `N` 换一关或 `Esc` 返回入口 |
| **最佳成绩** | 每个难度（以及示例关卡）单独记录最佳用时，显示在入口界面对应的条目上，用时越短越好 |
| **新纪录** | 通关时用时比历史最好更短会显示 `★新纪录`，并立即写入存档 |

* 计时精度为 0.01 秒，格式 `mm:ss.cc`，用 `Time.unscaledDeltaTime` 累计（不受帧率与暂停影响）。
* 成绩保存在 `PlayerPrefs`（键名 `ZY001.Best.D0` / `D1` / `D2` / `Sample`），
  打包后的独立运行包存在 `%USERPROFILE%\AppData\LocalLow\ZhangYang\捡物品大冒险\` 下。

## 目录结构

```
Assets/
├─ createfruit.cs       游戏流程管理：入口/游戏/结算状态机、关卡切换、生成、拾取与胜负判定
├─ GameHud.cs           全部界面：入口界面（含游戏说明）、游戏中 HUD、结算界面（IMGUI）
├─ MinMovesSolver.cs    最少步数求解器（纯 C#，可脱离 Unity 单元测试）
├─ LevelGenerator.cs    随机关卡生成器（纯 C#，保证每种物品都出现）
├─ PlayerController.cs  人物网格移动与输入（每步一格、边界限制、步数控制）
├─ PlayerAvatar.cs      卡通小人形象（运行时用球体/方块拼出，带走路起伏与倾斜）
├─ FruitItem.cs         物品外观（种类、颜色、编号显示）
├─ UiFont.cs            界面高清字体（系统矢量动态字体）与分辨率缩放
├─ Editor/
│  ├─ BuildStandalone.cs  一键生成 Windows 独立运行包（.exe + _Data，双击即玩）
│  └─ SceneBootstrap.cs   场景自举：打开工程时自动加载 SampleScene（否则点 Play 无显示）
├─ Resources/
│  ├─ fruit.prefab      物品预制体（Cube + BoxCollider + Rigidbody + FruitItem）
│  └─ Player.prefab     人物预制体（Cube + BoxCollider + Rigidbody + PlayerController）
└─ Scenes/SampleScene.unity   示例关卡场景
```

> 人物与物品的刚体在运行时会被设为 **运动学（isKinematic）** 并冻结全部约束，
> 位置完全由脚本按格子计算，避免“整格瞬移”与物理互相推开导致的拾取错位；
> 卡通小人的手臂/腿等部件只保留外观（创建后会销毁其碰撞体）。

## 算法说明

设人物初始在 `start` 格，走动范围必然是一段包含 `start` 的区间 `[start - L, start + R]`，
区间内的物品都会被捡到。因此只需让该区间覆盖全部种类，并让步数最少：

* 先左后右：`2L + R`；先右后左：`L + 2R`；只往一边走（不需要回头）：`L` 或 `R`。
* 每种物品只关心它“第一次出现”的距离（左侧 `leftRecords`、右侧 `rightRecords`，天然按距离递增），
  因为只有跨过某个首次出现位置，覆盖到的种类才会增加。
* 只在某一侧出现的种类决定了该侧至少要走到哪（`minLeftIndex` / `minRightIndex`）；
  枚举帕累托前沿上的候选点即可，时间复杂度 `O(n)`。

`MinMovesSolver.Solve()` 返回 `MovePlan`（步数、左右最远格数、先左还是先右、失败原因），
`MinMovesToCollectM(nums, start, m)` 保留原调用方式，无解时返回 `-1`。

## 正确性验证（脱离编辑器）

* **算法对拍**：`MinMovesSolver` 与暴力 BFS（状态 = 位置 + 已收集种类掩码）比较，
  长度 1~8、种类取值 1~3、所有起点共 **73812 组全部一致**；
  示例关卡最优解为 **4 步**（先左 1 格 → 再右 2 格）。
* **随机关卡**：3 个难度各生成 2000 关，物品数量区间 / 种类齐全 / 起点合法 / 可解 **0 异常**，
  平均最少步数约 2.8 / 4.7 / 6.8 步（难度递增），同一种子可复现。
* 全部脚本用 Unity 2022.3.62t16 的 DLL 引用以 `netstandard2.1 + C# 9` 编译通过（0 错误，含 `Editor/BuildStandalone.cs`）。
* 独立运行包：`BuildStandalone.BuildWindows64` 生成 `PickUpAdventure.exe` + `PickUpAdventure_Data`（Mono 后端），
  实测可直接双击运行，不依赖团结引擎或其他软件。

## 打开与运行

1. 用团结 Hub 打开本工程目录；
2. 打开 `Assets/Scenes/SampleScene.unity`；
3. 点击 Play，先在入口界面读一遍游戏说明，再选难度开始。

> `Library/`、`Temp/`、`obj/`、`Logs/`、`UserSettings/`、`*.csproj`、`*.sln` 等都已加入 `.gitignore`，
> 克隆后由编辑器首次导入时自动生成。

## 独立运行包（不依赖编辑器）

`Assets/Editor/BuildStandalone.cs` 可以生成一个自带运行时的 Windows 版本，
**双击 `.exe` 就能玩，不需要装团结引擎或任何其他软件**。

方式一：编辑器里点菜单 **构建 / 生成 Windows 独立运行包**（工程没打开时更好），
默认输出到工程目录下的 `Builds/PickUpAdventure/`。

方式二：命令行批处理（工程必须处于关闭状态，否则项目锁会挡住）：

```powershell
& "C:\Program Files\Tuanjie\Hub\Editor\2022.3.62t16\Editor\Tuanjie.exe" `
  -batchmode -nographics -quit `
  -projectPath "e:\Learning Materials\course_new\unity\unity_folders\projects\My project1" `
  -executeMethod BuildStandalone.BuildWindows64 `
  -buildOutput "E:\zy001_build" `
  -logFile "E:\zy001_build\build.log"
```

产物结构（整个文件夹一起拷走，`.exe` 必须与 `_Data` 同级）：

```
PickUpAdventure/
├─ PickUpAdventure.exe        ← 双击运行
├─ PickUpAdventure_Data/      ← 场景、贴图、程序集等资源
├─ MonoBleedingEdge/          ← 自带的 C# 运行时（所以无需外部依赖）
├─ TuanjiePlayer.dll
└─ TuanjieCrashHandler64.exe
```

打包参数：产品名“捡物品大冒险”、公司名 `ZhangYang`、**1600×900 窗口模式（可自由缩放）**、
脚本后端 **Mono2x**、API 兼容性 .NET Standard 2.0。

> 注意 `defaultIsNativeResolution` 必须为 **关闭**：一旦开启，
> 上面的 1600×900 会被直接忽略而改用显示器原生分辨率；
> 实测在部分环境下会让窗口客户区变成 **0×0**（`Screen` 返回 1×1），
> 界面全部画到屏幕外，表现就是“窗口打开了但一片黑”。
> 运行时还有一层兜底：`createfruit.EnsureWindowSize()` 会在启动后 2 秒内
> 反复检查窗口尺寸，发现异常就调用 `Screen.SetResolution(1600, 900, Windowed)` 修正。

## 常见问题

### 点 Play 之后 Game 视图什么都不显示

团结引擎打开工程时如果**没有加载任何场景**（`Library/LastSceneManagerSetup.txt` 里
场景路径是空的），点 Play 跑的就是一个空场景 —— 既不显示任何东西，也不会执行任何脚本
（Console 里一条游戏日志都没有）。

`Assets/Editor/SceneBootstrap.cs` 已经处理好了两件事：

1. 打开工程时若没有已加载场景，会**自动打开 `SampleScene`**，Console 里会打印
   `[场景自举] 打开工程时没有打开任何场景，已自动打开 …，现在可以直接点 Play。`；
2. 菜单 **场景 / 打开并保存 SampleScene（规范化）** 可手动打开并重新保存场景，
   把文件从旧版 Unity 的 `%TAG unity3d.com,2011` 规范成团结引擎的 `%TAG yousandi.cn,2023`，
   同时让编辑器记下“上次打开的场景”。

命令行批处理执行一次（要求工程处于关闭状态）：

```powershell
& "C:\Program Files\Tuanjie\Hub\Editor\2022.3.62t16\Editor\Tuanjie.exe" `
  -batchmode -nographics -projectPath "e:\Learning Materials\course_new\unity\unity_folders\projects\My project1" `
  -executeMethod SceneBootstrap.NormalizeScenes -logFile "E:\zy001_build\scene.log"
```

日志里出现 `SCENE_OK` 即成功。

### 独立运行包打开后是一片黑

先看窗口左下角那行绿色的自检信息，它会显示当前分辨率和实际使用的字体名：

* **能看到自检行但看不到界面** → 面板排版问题；
* **连自检行都看不到** → 窗口本身就是 0×0，见上面 `defaultIsNativeResolution` 那一节。

运行日志（`%USERPROFILE%\AppData\LocalLow\ZhangYang\捡物品大冒险\Player.log`）
里也会有关键数据，可直接搜 `界面自检`：

```
[界面字体] 已启用系统动态字体：Microsoft YaHei UI，当前缩放 Scale = 0.85。
[界面自检] 入口面板 宽=697  高=698  y=101  行数=11  标题字号=29
[界面自检] Screen=1600x900  Scale=0.85  动态字体=Microsoft YaHei UI
```

若 `Screen=` 后面是很小的数字（如 `1x1`），就是窗口尺寸异常，`EnsureWindowSize()` 会自动修正。
