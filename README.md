# 一行格子捡物品小游戏（集齐所有种类）

基于 **团结引擎（Tuanjie Editor 1.10.4 / Unity 2022.3.62t16）** 的 3D 小游戏：
人物和不同种类的物品等距生成在一条直线上，人物每走 **一格** 消耗 **一步**，
在 **给定步数** 内把所有种类各捡到至少一个即 **通过**，否则 **失败**。
给定步数由最优解算法 `MinMovesSolver.MinMovesToCollectM()` 计算得出。

## 玩法

| 操作 | 说明 |
| --- | --- |
| `←` `→` 或 `A` `D` | 向左 / 向右走一格（一格 = 一步） |
| `H` | 显示 / 隐藏最优解提示 |
| `R` | 重新开始本关 |

* 人物**起点格上的种类**在开局即视为已获得（与算法中距离为 0 的假设一致）。
* 踩到物品即自动捡起；重复种类的物品不会增加“集齐进度”，但依然消耗一步。
* 步数用尽时：**先判定是否集齐**（用尽的那一步刚好集齐也算通过），未集齐则失败。
* 走到两端继续按方向键属于无效操作，不消耗步数，只给提示。

## 关卡配置（Inspector：场景中的 `GameObject` → `createfruit`）

| 字段 | 含义 |
| --- | --- |
| `n` | 格子数量 |
| `a[]` | 每格物品的种类（下标即格子下标） |
| `start` | 人物初始格子下标 |
| `m` | 需要集齐的种类数，应与 `a` 中不同种类数一致；`<= 0` 表示自动统计 |
| `cellSize` | 一格的世界长度，也就是每步走的距离（默认 1） |
| `itemScale` | 物品边长（以格为单位，默认 0.8） |
| `zOffset` | 整行所在的世界 z 坐标 |
| `autoFitCamera` | 自动调整主相机，保证任意长度的关卡都能一屏看全 |
| `extraSteps` | 在最少步数之外额外给的步数（0 = 严格按最优解给步数） |

默认关卡：`n = 10`、`a = {1,2,3,2,1,2,3,2,4,1}`、`start = 7`、`m = 4`，最优解 **4 步**
（先向左 1 格 → 再向右 2 格：拿到格子 7(2)、6(3)、8(4)、9(1) 四种）。

## 目录结构

```
Assets/
├─ createfruit.cs       关卡构建 + 游戏流程（生成、拾取、胜负判定、HUD、相机取景）
├─ MinMovesSolver.cs    最少步数求解器（纯 C#，可脱离 Unity 单元测试）
├─ PlayerController.cs  人物网格移动与输入
├─ FruitItem.cs         物品外观（种类、颜色、编号显示）
├─ Resources/
│  ├─ fruit.prefab      物品预制体（Cube + BoxCollider + Rigidbody + FruitItem）
│  └─ Player.prefab     人物预制体（Cube + BoxCollider + Rigidbody + PlayerController）
└─ Scenes/SampleScene.unity
```

> 人物与物品的刚体在运行时会被设为 **运动学（isKinematic）** 并冻结全部约束，
> 位置完全由脚本按格子计算，避免“整格瞬移”与物理互相推开导致的拾取错位。

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

## 打开与运行

1. 用团结 Hub 打开本工程目录；
2. 打开 `Assets/Scenes/SampleScene.unity`；
3. 点击 Play，用方向键开始游戏。

> `Library/`、`Temp/`、`obj/`、`Logs/`、`UserSettings/`、`*.csproj`、`*.sln` 等都已加入 `.gitignore`，
> 克隆后由编辑器首次导入时自动生成。
