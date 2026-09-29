using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡构建 + 游戏流程管理（挂在场景里名为 “GameObject” 的对象上）。
///
/// 整体流程：
///   入口界面（选难度 1/2/3 或示例关卡，界面里有游戏说明）
///     → 游戏中（人物走格子、踩到就捡，用允许步数内集齐所有种类）
///     → 结算界面（再来一次 / 换一关 / 返回入口界面）
///
/// 规则：
///   · 人物与物品等距排成一行，人物每走一格消耗 1 步；
///   · 起点格上的物品在开局即视为已获得；
///   · 允许步数 = 最少步数（MinMovesSolver 计算）+ extraSteps；先判“集齐 → 通过”，
///     再判“步数用尽 → 失败”；
///   · SampleScene 里序列化的 n/a/start/m 就是“示例关卡”的固定配置；
///     难度 1/2/3 由 LevelGenerator 随机构造（物品数量与位置随机、人物位置随机）。
///   · 挑战模式（入口界面开关 challengeMode）：进入关卡先点 Start 开始计时、
///     最优解提示被禁用、失败不能重试本关，并按难度用 PlayerPrefs 记录最佳用时；
///
/// 界面（入口界面 / HUD / 结算）全部在 GameHud.cs 中绘制，本脚本只负责数据与规则。
/// </summary>
public class createfruit : MonoBehaviour
{
    /// <summary>游戏所处的阶段。</summary>
    public enum Phase
    {
        Menu,       // 入口界面
        Playing     // 游戏中（含结算覆盖层）
    }

    // ============================ 示例关卡（场景序列化配置） ============================
    [Header("示例关卡数据（SampleScene 的固定配置，入口界面“示例关卡”使用）")]
    [Tooltip("格子数量")]
    public int n = 10;

    [Tooltip("每格物品的种类，下标即格子下标")]
    public int[] a = { 1, 2, 3, 2, 1, 2, 3, 2, 4, 1 };

    [Tooltip("人物初始所在的格子下标")]
    public int start = 7;

    [Tooltip("需要集齐的种类数，应等于数组里的不同种类数；<= 0 表示按数组自动统计")]
    public int m = 4;

    // ============================ 难度配置（随机生成） ============================
    /// <summary>一个难度档位的配置。</summary>
    [Serializable]
    public class Difficulty
    {
        [Tooltip("入口界面显示的难度名称")]
        public string title = "难度";

        [Tooltip("物品数量下限（格数）")]
        public int minCells = 4;

        [Tooltip("物品数量上限（格数）")]
        public int maxCells = 6;

        [Tooltip("物品种类数")]
        public int typeCount = 3;

        public Difficulty()
        {
        }

        public Difficulty(string title, int minCells, int maxCells, int typeCount)
        {
            this.title = title;
            this.minCells = minCells;
            this.maxCells = maxCells;
            this.typeCount = typeCount;
        }
    }

    [Header("难度配置（随机关卡：物品数量、物品与人物位置都随机）")]
    public Difficulty[] difficulties = new Difficulty[]
    {
        new Difficulty("难度 1", 4, 6, 3),
        new Difficulty("难度 2", 7, 9, 4),
        new Difficulty("难度 3", 10, 14, 5),
    };

    // ============================ 表现与规则 ============================
    [Header("网格与表现")]
    [Tooltip("一格的世界长度，也就是人物每步走的距离")]
    public float cellSize = 1f;

    [Tooltip("物品边长（以格为单位）")]
    public float itemScale = 0.8f;

    [Tooltip("整行所在的世界 z 坐标")]
    public float zOffset = 0f;

    [Tooltip("自动把主相机对准整行")]
    public bool autoFitCamera = true;

    [Tooltip("使用卡通小人形象（关掉则退回方块外观）")]
    public bool cartoonPlayer = true;

    [Header("规则")]
    [Tooltip("在最少步数之外额外给的步数，0 表示严格按最优解给")]
    public int extraSteps = 0;

    [Tooltip("游戏一开始是否先显示入口界面")]
    public bool startInMenu = true;

    [Tooltip("挑战模式（入口界面可开关）：进入关卡要先点 Start 开始计时、最优解提示被禁用、失败不能重试本关，并按难度记录最佳用时")]
    public bool challengeMode = false;

    // ============================ 运行时状态（界面只读） ============================
    [Header("运行时状态")]
    public Phase phase = Phase.Menu;
    public GameObject[] fruits;
    public string modeTitle = "";
    public int cellCount;
    public int[] types;
    public int startIndex;
    public int requiredTypes;
    public int minSteps;
    public int allowedSteps;
    public int usedSteps;
    public bool gameOver;
    public bool win;
    public string statusText = "";
    public string toastText = "";
    public float toastTime;
    public string errorText = "";
    public int difficultyIndex = -1;        // -1 = 示例关卡，>=0 = difficulties 的下标
    public bool showHint;
    public bool challengeActive;            // 挑战模式：本关是否已经开始计时
    public float challengeTime;             // 挑战模式：本关已用时（秒）
    public float challengeBest;             // 挑战模式：当前难度的最佳用时（秒），NoRecord 表示还没有记录
    public bool challengeNewRecord;         // 挑战模式：本次是否破了纪录
    [NonSerialized] public MinMovesSolver.MovePlan plan;

    private readonly Dictionary<int, FruitItem> fruitsByIndex = new Dictionary<int, FruitItem>();
    private readonly HashSet<int> collectedTypes = new HashSet<int>();
    private readonly List<int> typeList = new List<int>();
    private PlayerController playerController;
    private GameObject player;
    private GameHud hud;

    void Start()
    {
        EnsureWindowSize();

        // 挂上界面脚本（入口界面 / HUD / 结算都画在里面）
        hud = gameObject.GetComponent<GameHud>();
        if (hud == null) hud = gameObject.AddComponent<GameHud>();
        hud.game = this;

        if (startInMenu) EnterMenu();
        else StartSampleLevel();
    }

    /// <summary>启动后需要复查窗口尺寸的帧数（约 2 秒）。</summary>
    private int windowCheckFrames;
    private bool windowSizeWarned;

    /// <summary>
    /// 窗口尺寸兜底。
    /// 实测踩过的坑：PlayerSettings 里已经写了 1600x900 的窗口模式，
    /// 可启动时窗口客户区仍然是 0×0（Screen 返回 1x1），
    /// 界面全部画到屏幕外，表现就是“窗口打开了但一片黑”。
    /// 这里显式再设一次；正常环境下尺寸本来就是对的，不会触发。
    /// </summary>
    private void EnsureWindowSize()
    {
        const int minWidth = 640;
        const int minHeight = 360;
        if (Screen.width >= minWidth && Screen.height >= minHeight) return;

        if (!windowSizeWarned)
        {
            windowSizeWarned = true;
            Debug.LogWarning("[窗口] 检测到窗口尺寸异常：" + Screen.width + "x" + Screen.height + "，正在设置为 1600x900。");
        }
        Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
    }

    void Update()
    {
        // 启动后短时间内持续复查窗口尺寸：Screen.SetResolution 是异步生效的，
        // 只在 Start 里设一次不够，窗口可能被别的设置又拉回 0x0。
        if (windowCheckFrames < 120)
        {
            windowCheckFrames++;
            EnsureWindowSize();
        }

        if (toastTime > 0f) toastTime -= Time.deltaTime;

        if (phase == Phase.Menu)
        {
            HandleMenuKeys();
            return;
        }

        // 挑战模式：点 Start 之后才开始累计用时
        if (challengeMode && challengeActive && !gameOver) challengeTime += Time.unscaledDeltaTime;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            EnterMenu();
            return;
        }

        if (gameOver)
        {
            HandleGameOverKeys();
            return;
        }

        // 挑战模式必须先点 Start（或空格 / 回车）才开表，期间人物是锁住的
        if (challengeMode && !challengeActive)
        {
            if (IsStartKeyDown()) StartTimer();
            else if (Input.GetKeyDown(KeyCode.H)) Toast("挑战模式不能查看最优解提示。");
            return;
        }

        if (Input.GetKeyDown(KeyCode.H))
        {
            if (CanHint) showHint = !showHint;
            else Toast("挑战模式不能查看最优解提示。");
        }
        if (Input.GetKeyDown(KeyCode.R) && CanRetry) BuildLevel();     // 重开本关（同一关重新摆一次）
    }

    /// <summary>结算界面的键盘操作：空格 / R 重开本关（挑战模式禁用），N 换一关，H 看提示。</summary>
    private void HandleGameOverKeys()
    {
        if (CanRetry && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.R)))
        {
            BuildLevel();
            return;
        }
        if (Input.GetKeyDown(KeyCode.N) && difficultyIndex >= 0)
        {
            StartDifficulty(difficultyIndex);
            return;
        }
        if (Input.GetKeyDown(KeyCode.H))
        {
            if (CanHint) showHint = !showHint;
            else Toast("挑战模式不能查看最优解提示。");
        }
    }

    /// <summary>开始计时的按键（空格 / 回车），与 Start 按钮等价。</summary>
    private static bool IsStartKeyDown()
    {
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    private void HandleMenuKeys()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) StartDifficulty(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) StartDifficulty(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) StartDifficulty(2);
        else if (Input.GetKeyDown(KeyCode.E)) StartSampleLevel();
        else if (Input.GetKeyDown(KeyCode.C))   // 挑战模式开关的快捷键（也可以直接用鼠标点入口界面上的复选框）
        {
            challengeMode = !challengeMode;
            Toast(challengeMode ? "挑战模式已开启：进入关卡要先点 Start 开始计时。" : "挑战模式已关闭。");
        }
    }

    /// <summary>回到入口界面（会清掉场上的关卡物体）。</summary>
    public void EnterMenu()
    {
        ClearLevel();

        phase = Phase.Menu;
        gameOver = false;
        win = false;
        statusText = "";
        toastText = "";
        toastTime = 0f;
        errorText = "";
        modeTitle = "";
        showHint = false;
        challengeActive = false;
        challengeTime = 0f;
        challengeBest = NoRecord;
        challengeNewRecord = false;

        FitMenuCamera();
    }

    /// <summary>进入场景里配置好的示例关卡（SampleScene 的固定关卡）。</summary>
    public void StartSampleLevel()
    {
        if (a == null || a.Length == 0)
        {
            phase = Phase.Menu;
            Fail("示例关卡的数组 a 为空，请在 Inspector 里填写每格物品的种类。");
            return;
        }

        int[] data = (int[])a.Clone();
        StartLevel(data, start, m,
                   string.Format("示例关卡（{0} 格 · {1} 种）", data.Length, LevelGenerator.CountTypes(data)),
                   -1);
    }

    /// <summary>按难度随机生成一关（物品数量、物品与人物位置都随机）。</summary>
    public void StartDifficulty(int index)
    {
        if (difficulties == null || difficulties.Length == 0)
        {
            phase = Phase.Menu;
            Fail("Inspector 里没有配置任何难度。");
            return;
        }

        index = Mathf.Clamp(index, 0, difficulties.Length - 1);
        Difficulty config = difficulties[index];

        // 允许少量重随机，避免出现“一步就集齐”这种没意思的关卡
        LevelGenerator.LevelDefinition level = new LevelGenerator.LevelDefinition();
        MinMovesSolver.MovePlan check = new MinMovesSolver.MovePlan();
        bool ok = false;
        for (int attempt = 0; attempt < 30 && !ok; attempt++)
        {
            level = LevelGenerator.Generate(config.minCells, config.maxCells, config.typeCount);
            check = MinMovesSolver.Solve(level.Types, level.StartIndex, level.TypeCount);
            ok = check.Success && check.Steps >= 2;
        }

        if (!ok)
        {
            phase = Phase.Menu;
            Fail(string.Format("{0} 的配置无法生成有效关卡（物品 {1}~{2} 个 · {3} 种）。",
                               config.title, config.minCells, config.maxCells, config.typeCount));
            return;
        }

        Debug.Log(string.Format("[随机关卡] {0}：物品 {1} 个、{2} 种、起点格 {3}、最少 {4} 步、随机种子 {5}",
                                config.title, level.CellCount, level.TypeCount, level.StartIndex,
                                check.Steps, level.Seed));

        StartLevel(level.Types, level.StartIndex, level.TypeCount,
                   string.Format("{0}（{1} 格 · {2} 种）", config.title, level.CellCount, level.TypeCount),
                   index);
    }

    /// <summary>切换到某一关并立即开始。</summary>
    private void StartLevel(int[] levelTypes, int levelStart, int levelRequired, string title, int index)
    {
        types = (int[])levelTypes.Clone();      // 拷一份，避免外部数据被后续改动
        startIndex = levelStart;
        cellCount = types.Length;
        requiredTypes = levelRequired > 0 ? levelRequired : LevelGenerator.CountTypes(types);
        modeTitle = title;
        difficultyIndex = index;
        showHint = false;
        phase = Phase.Playing;

        BuildLevel();
    }

    /// <summary>按当前关卡数据重新生成人物与物品（也用于“再来一次”“R 重开”）。</summary>
    public void BuildLevel()
    {
        ClearLevel();

        collectedTypes.Clear();
        typeList.Clear();
        fruitsByIndex.Clear();
        usedSteps = 0;
        gameOver = false;
        win = false;
        statusText = "";
        toastText = "";
        toastTime = 0f;
        errorText = "";

        if (types == null || types.Length == 0)
        {
            Fail("关卡数据为空。");
            return;
        }
        if (startIndex < 0 || startIndex >= types.Length)
        {
            Fail(string.Format("起点下标 {0} 超出范围 [0, {1}]。", startIndex, types.Length - 1));
            return;
        }

        cellCount = types.Length;
        for (int i = 0; i < cellCount; i++)
        {
            if (!typeList.Contains(types[i])) typeList.Add(types[i]);
        }
        if (requiredTypes <= 0) requiredTypes = typeList.Count;

        // 关键规则：允许步数由求解器算出的“最少步数”决定
        plan = MinMovesSolver.Solve(types, startIndex, requiredTypes);
        if (!plan.Success)
        {
            Fail("无法求解本关的最少步数 —— " + plan.FailureReason);
            return;
        }
        if (!string.IsNullOrEmpty(plan.Notice)) Debug.LogWarning("[关卡] " + plan.Notice);

        minSteps = plan.Steps;
        allowedSteps = minSteps + Mathf.Max(0, extraSteps);

        fruits = new GameObject[cellCount];
        if (!SpawnLevel()) return;
        if (autoFitCamera) FitLevelCamera();

        // 挑战模式：等玩家点 Start 才开表、才允许移动
        challengeActive = false;
        challengeTime = 0f;
        challengeNewRecord = false;
        challengeBest = GetBestTime(difficultyIndex);
        if (challengeMode && playerController != null) playerController.canMove = false;

        CollectAt(startIndex, true);    // 起点格上的物品开局即算获得
        CheckResult();                  // 极短关卡可能开局就集齐，也要判胜

        if (challengeMode && !gameOver) Toast("挑战模式：点 Start（或按空格）开始计时。");

        Debug.LogFormat("[关卡] {0}：{1} 格、{2} 种，最少 {3} 步（先{4}，左{5} 格 / 右{6} 格），允许 {7} 步",
                        modeTitle, cellCount, requiredTypes, minSteps, plan.LeftFirst ? "左" : "右",
                        plan.LeftSteps, plan.RightSteps, allowedSteps);
    }

    /// <summary>生成物品与人物，返回是否成功。</summary>
    private bool SpawnLevel()
    {
        GameObject fruitPrefab = LoadPrefab("Fruit", "fruit");
        GameObject playerPrefab = LoadPrefab("Player", "player");
        if (fruitPrefab == null || playerPrefab == null)
        {
            Fail("找不到预制体：请确认 Assets/Resources 下有 fruit.prefab 与 Player.prefab。");
            return false;
        }

        float itemSize = cellSize * itemScale;
        for (int i = 0; i < cellCount; i++)
        {
            GameObject obj = Instantiate(fruitPrefab, CellToWorld(i, itemSize), Quaternion.identity);
            obj.name = string.Format("Fruit_{0}_type{1}", i, types[i]);
            obj.transform.localScale = Vector3.one * itemSize;
            PrepareBody(obj);

            FruitItem item = obj.GetComponent<FruitItem>();
            if (item == null) item = obj.AddComponent<FruitItem>();
            item.SetType(types[i]);

            fruits[i] = obj;
            fruitsByIndex[i] = item;
        }

        // 卡通小人从地面往上搭，所以根节点放在 y = 0；方块外观则放在半格高处
        float playerTileSize = cartoonPlayer ? 0f : cellSize;
        player = Instantiate(playerPrefab, CellToWorld(startIndex, playerTileSize), Quaternion.identity);
        player.name = "Player";
        player.transform.localScale = Vector3.one;
        PrepareBody(player);

        if (cartoonPlayer)
        {
            PlayerAvatar avatar = player.GetComponent<PlayerAvatar>();
            if (avatar == null) avatar = player.AddComponent<PlayerAvatar>();
            avatar.Build(cellSize);
        }
        else
        {
            Renderer playerRenderer = player.GetComponentInChildren<Renderer>();
            if (playerRenderer != null) playerRenderer.material.color = new Color(0.25f, 0.28f, 0.35f);
        }

        playerController = player.GetComponent<PlayerController>();
        if (playerController == null) playerController = player.AddComponent<PlayerController>();
        playerController.Configure(startIndex, 0, cellCount - 1, cellSize, CellOriginX(), allowedSteps,
                                   OnPlayerMoved, OnPlayerBlocked);
        return true;
    }

    /// <summary>把刚体固定住：位置完全由脚本决定，避免物理把物品/人物推歪。</summary>
    private static void PrepareBody(GameObject obj)
    {
        Rigidbody body = obj.GetComponent<Rigidbody>();
        if (body == null) return;

        body.isKinematic = true;
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeAll;
    }

    /// <summary>第 index 格的世界坐标：整行沿 x 轴居中，y 让物体底部正好贴着地面。</summary>
    private Vector3 CellToWorld(int index, float objectSize)
    {
        return new Vector3(CellOriginX() + index * cellSize, objectSize * 0.5f, zOffset);
    }

    /// <summary>第 0 格的 x 坐标（使整行以 x = 0 为中心）。</summary>
    private float CellOriginX()
    {
        return -(cellCount - 1) * 0.5f * cellSize;
    }

    private static GameObject LoadPrefab(params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            GameObject prefab = Resources.Load<GameObject>(names[i]);
            if (prefab != null) return prefab;
        }
        return null;
    }

    /// <summary>按整行宽度自动取景，关卡多长都能一眼看全。</summary>
    private void FitLevelCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        float rowWidth = (cellCount - 1) * cellSize + cellSize * 3f;    // 行宽 + 两侧留白
        float aspect = cam.aspect > 0.01f ? cam.aspect : 16f / 9f;

        cam.transform.rotation = Quaternion.identity;                    // 沿 +z 正对整行
        if (cam.orthographic)
        {
            cam.orthographicSize = Mathf.Max(rowWidth * 0.5f / aspect, cellSize * 2f);
            cam.transform.position = new Vector3(0f, cellSize * 2f, zOffset - 10f);
        }
        else
        {
            float halfWidth = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * aspect;
            float distance = Mathf.Max(rowWidth * 0.5f / Mathf.Max(0.1f, halfWidth), cellSize * 3f) + cellSize * 2f;
            cam.transform.position = new Vector3(0f, cellSize * 1.5f, zOffset - distance);
        }
    }

    /// <summary>入口界面固定一个机位，背景看起来更稳。</summary>
    private void FitMenuCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        cam.transform.rotation = Quaternion.identity;
        cam.transform.position = new Vector3(0f, cellSize * 2.5f, zOffset - cellSize * 7f);
    }

    /// <summary>人物移动成功后的回调：先捡起当前格的物品，再判定胜负。</summary>
    private void OnPlayerMoved(int index, int stepsUsed)
    {
        usedSteps = stepsUsed;
        CollectAt(index, false);
        CheckResult();
    }

    /// <summary>人物想走出格子范围时的回调（不消耗步数）。</summary>
    private void OnPlayerBlocked(int attemptedIndex)
    {
        Toast(string.Format("已经到头了（格子范围 0 ~ {0}），这一步不算步数。", cellCount - 1));
    }

    /// <summary>捡起第 index 格上的物品；silent 为 true 表示不弹提示（开局捡起点格用）。</summary>
    private void CollectAt(int index, bool silent)
    {
        FruitItem item;
        if (!fruitsByIndex.TryGetValue(index, out item)) return;

        fruitsByIndex.Remove(index);
        if (fruits != null && index >= 0 && index < fruits.Length) fruits[index] = null;
        if (item == null) return;

        int type = item.fruitType;
        bool isNewType = collectedTypes.Add(type);      // 第一次拿到这个种类才算“集齐进度 +1”
        Destroy(item.gameObject);

        if (isNewType)
        {
            statusText = string.Format("捡到种类 {0}，已集齐 {1}/{2} 种", type, collectedTypes.Count, requiredTypes);
            if (!silent) Toast(statusText);
        }
        else if (!silent)
        {
            Toast(string.Format("种类 {0} 之前已经捡过了，本次不增加集齐进度。", type));
        }
    }

    /// <summary>判定通过 / 失败。顺序很重要：用尽步数的那一步如果刚好集齐，算通过。</summary>
    private void CheckResult()
    {
        if (gameOver) return;

        if (collectedTypes.Count >= requiredTypes)
        {
            gameOver = true;
            win = true;
            LockPlayer();
            statusText = string.Format("通过！共用 {0} 步（最优 {1} 步）", usedSteps, minSteps);
            if (challengeMode)
            {
                if (challengeActive) RecordBestTime();
                statusText += string.Format("\n用时 {0}（{1}最佳 {2}）",
                                           FormatTime(challengeTime),
                                           challengeNewRecord ? "★新纪录，" : "",
                                           FormatTime(challengeBest));
            }
            Debug.Log("[游戏结束] " + statusText);
            return;
        }

        if (usedSteps >= allowedSteps)
        {
            gameOver = true;
            win = false;
            LockPlayer();
            statusText = string.Format("失败：{0} 步已经用完，还差 {1} 种没拿到",
                                       allowedSteps, requiredTypes - collectedTypes.Count);
            Debug.Log("[游戏结束] " + statusText);
        }
    }

    private void LockPlayer()
    {
        if (playerController != null) playerController.canMove = false;
    }

    /// <summary>清掉上一次生成的人物和物品。</summary>
    private void ClearLevel()
    {
        if (fruits != null)
        {
            for (int i = 0; i < fruits.Length; i++)
            {
                if (fruits[i] != null) Destroy(fruits[i]);
            }
        }
        if (player != null) Destroy(player);

        player = null;
        playerController = null;
        fruits = null;
        fruitsByIndex.Clear();
    }

    private void Fail(string reason)
    {
        errorText = reason;
        statusText = reason;
        Debug.LogError("[关卡] " + reason);
    }

    private void Toast(string message)
    {
        toastText = message;
        toastTime = 2.5f;
    }

    // ============================ 供 GameHud 读取的只读接口 ============================

    /// <summary>难度档位数量。</summary>
    public int DifficultyCount
    {
        get { return difficulties == null ? 0 : difficulties.Length; }
    }

    /// <summary>取某个难度的配置（下标越界返回 null）。</summary>
    public Difficulty GetDifficulty(int index)
    {
        if (difficulties == null || index < 0 || index >= difficulties.Length) return null;
        return difficulties[index];
    }

    /// <summary>当前关卡出现过的种类（按首次出现顺序）。</summary>
    public List<int> TypeList
    {
        get { return typeList; }
    }

    /// <summary>已集齐的种类数。</summary>
    public int CollectedTypeCount
    {
        get { return collectedTypes.Count; }
    }

    /// <summary>某种类是否已经拿到。</summary>
    public bool IsTypeCollected(int type)
    {
        return collectedTypes.Contains(type);
    }

    // ============================ 挑战模式 ============================

    /// <summary>还没有成绩记录时 BestTime 的取值。</summary>
    public const float NoRecord = -1f;

    private const string BestTimeKeyPrefix = "ZY001.Best.";

    /// <summary>是否允许查看最优解提示（挑战模式一律禁用）。</summary>
    public bool CanHint
    {
        get { return !challengeMode; }
    }

    /// <summary>是否允许“重开 / 重试本关”（挑战模式一律不允许，失败只能换一关）。</summary>
    public bool CanRetry
    {
        get { return !challengeMode; }
    }

    /// <summary>挑战模式：开始计时（由 Start 按钮或空格 / 回车触发）。</summary>
    public void StartTimer()
    {
        if (!challengeMode || challengeActive || gameOver) return;

        challengeActive = true;
        challengeTime = 0f;
        if (playerController != null) playerController.canMove = true;
        Toast("计时开始！用时越短越好。");
    }

    /// <summary>某个难度的最佳用时（index 小于 0 表示示例关卡），NoRecord 表示还没有记录。</summary>
    public float GetBestTime(int index)
    {
        return PlayerPrefs.GetFloat(BestTimeKeyPrefix + (index >= 0 ? "D" + index : "Sample"), NoRecord);
    }

    /// <summary>最佳用时的显示文本（mm:ss.cc，没有记录时显示 --）。</summary>
    public string GetBestTimeText(int index)
    {
        return FormatTime(GetBestTime(index));
    }

    /// <summary>把秒数格式化成 mm:ss.cc。</summary>
    public static string FormatTime(float seconds)
    {
        if (seconds < 0f) return "--";

        int centis = Mathf.RoundToInt(seconds * 100f);
        int minutes = centis / 6000;
        int rest = centis % 6000;
        return string.Format("{0:00}:{1:00}.{2:00}", minutes, rest / 100, rest % 100);
    }

    /// <summary>通关后把用时写进 PlayerPrefs，破了纪录时把 challengeNewRecord 置为 true。</summary>
    private void RecordBestTime()
    {
        if (!challengeMode || !challengeActive || challengeTime <= 0f) return;

        string key = BestTimeKeyPrefix + (difficultyIndex >= 0 ? "D" + difficultyIndex : "Sample");
        float best = PlayerPrefs.GetFloat(key, NoRecord);
        if (best <= 0f || challengeTime < best)
        {
            PlayerPrefs.SetFloat(key, challengeTime);
            PlayerPrefs.Save();
            challengeNewRecord = true;
            Debug.Log("[挑战模式] 新纪录：" + key + " = " + FormatTime(challengeTime));
        }
        challengeBest = GetBestTime(difficultyIndex);
    }

    /// <summary>示例关卡的一句话简介（入口界面按钮上显示）。</summary>
    public string GetSampleLevelSummary()
    {
        if (a == null || a.Length == 0) return "示例关卡（场景里的数组 a 为空）";

        int typeCount = LevelGenerator.CountTypes(a);
        MinMovesSolver.MovePlan sample = MinMovesSolver.Solve(a, start, m);
        if (!sample.Success)
        {
            return string.Format("示例关卡（场景固定配置：{0} 格 · {1} 种）", a.Length, typeCount);
        }
        return string.Format("示例关卡（场景固定配置：{0} 格 · {1} 种 · 最优 {2} 步）",
                             a.Length, typeCount, sample.Steps);
    }

    /// <summary>已拿到的种类文本。</summary>
    public string GetCollectedTypesText()
    {
        string text = "";
        for (int i = 0; i < typeList.Count; i++)
        {
            if (!collectedTypes.Contains(typeList[i])) continue;
            if (text.Length > 0) text += "、";
            text += typeList[i];
        }
        return text.Length == 0 ? "无" : text;
    }

    /// <summary>还缺的种类文本。</summary>
    public string GetMissingTypesText()
    {
        string text = "";
        for (int i = 0; i < typeList.Count; i++)
        {
            if (collectedTypes.Contains(typeList[i])) continue;
            if (text.Length > 0) text += "、";
            text += typeList[i];
        }
        return text.Length == 0 ? "无" : text;
    }

    /// <summary>把求解器给出的最优走法翻译成文字提示。</summary>
    public string GetHintText()
    {
        if (!plan.Success) return "";

        if (plan.LeftSteps == 0 && plan.RightSteps == 0)
        {
            return "最优解：0 步（起点格上的物品已经算拿到）";
        }
        if (plan.RightSteps == 0)
        {
            return string.Format("最优解：{0} 步 —— 一直向左走 {1} 格就集齐了（不用回头）", plan.Steps, plan.LeftSteps);
        }
        if (plan.LeftSteps == 0)
        {
            return string.Format("最优解：{0} 步 —— 一直向右走 {1} 格就集齐了（不用回头）", plan.Steps, plan.RightSteps);
        }
        return plan.LeftFirst
            ? string.Format("最优解：{0} 步 —— 先向左 {1} 格再向右 {2} 格（左边来回 + 一路向右）",
                            plan.Steps, plan.LeftSteps, plan.RightSteps)
            : string.Format("最优解：{0} 步 —— 先向右 {1} 格再向左 {2} 格（右边来回 + 一路向左）",
                            plan.Steps, plan.RightSteps, plan.LeftSteps);
    }
}
