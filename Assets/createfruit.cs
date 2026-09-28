using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡构建 + 游戏流程管理（挂在场景里的 “GameObject” 上）。
///
/// 玩法规则：
///   · 人物与各种类物品等距生成在一条直线上（1 格 = cellSize 个世界单位），人物每走一格消耗 1 步；
///   · 每格的物品被踩到就自动捡起，起点格上的种类在开局即视为已获得；
///   · 允许步数 = 最少步数（MinMovesSolver 计算）+ extraSteps，在允许步数内把每种物品
///     都捡到至少一个即【通过】，步数用尽还没集齐则【失败】；
///   · ← → / A D 移动，H 显示或隐藏最优解提示，R 重新开始。
/// </summary>
public class createfruit : MonoBehaviour
{
    [Header("关卡数据")]
    [Tooltip("格子数量")]
    public int n = 10;

    [Tooltip("每格物品的种类，下标即格子下标")]
    public int[] a = { 1, 2, 3, 2, 1, 2, 3, 2, 4, 1 };

    [Tooltip("人物初始所在的格子下标")]
    public int start = 7;

    [Tooltip("需要集齐的种类数，应与数组里的不同种类数一致；<= 0 表示按数组自动统计")]
    public int m = 4;

    [Header("网格与表现")]
    [Tooltip("一格的世界长度，也就是人物每步走的距离")]
    public float cellSize = 1f;

    [Tooltip("物品边长（以格为单位，1 表示刚好占满一格）")]
    public float itemScale = 0.8f;

    [Tooltip("整行所在的世界 z 坐标")]
    public float zOffset = 0f;

    [Tooltip("自动把主相机对准整行，保证不同长度的关卡都能看全")]
    public bool autoFitCamera = true;

    [Header("规则")]
    [Tooltip("在最少步数之外额外给的步数，0 表示严格按最优解的步数给")]
    public int extraSteps = 0;

    [Header("运行时状态（只读观察用）")]
    [Tooltip("每格上的物品，被捡走后对应位置为 null")]
    public GameObject[] fruits;
    public int requiredTypes;       // 本关真正的种类数
    public int minSteps;            // 最优解步数
    public int allowedSteps;        // 允许的步数 = minSteps + extraSteps
    public int usedSteps;           // 已用步数
    public bool gameOver;
    public bool win;
    public string statusText = "";

    private readonly Dictionary<int, FruitItem> fruitsByIndex = new Dictionary<int, FruitItem>();
    private readonly HashSet<int> collectedTypes = new HashSet<int>();
    private readonly List<int> typeList = new List<int>();      // 关卡里出现过的种类（按首次出现顺序）
    private PlayerController playerController;
    private GameObject player;
    private MinMovesSolver.MovePlan plan;
    private bool showHint;
    private string toastText = "";
    private float toastTime;
    private string errorText = "";

    void Start()
    {
        BuildLevel();
    }

    void Update()
    {
        if (toastTime > 0f) toastTime -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.R))
        {
            BuildLevel();
            return;
        }
        if (Input.GetKeyDown(KeyCode.H))
        {
            showHint = !showHint;
        }
    }

    /// <summary>重新构建关卡（首次进入以及按 R 重开都会调用）。</summary>
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

        if (a == null || a.Length == 0)
        {
            Fail("关卡数据 a 为空：请在 Inspector 里填写每格物品的种类。");
            return;
        }
        if (n != a.Length)
        {
            Debug.LogWarningFormat("[关卡] n={0} 与数组长度 {1} 不一致，已按数组长度处理。", n, a.Length);
            n = a.Length;
        }
        if (start < 0 || start >= n)
        {
            Fail(string.Format("起点下标 start={0} 超出范围 [0, {1}]。", start, n - 1));
            return;
        }

        for (int i = 0; i < n; i++)
        {
            if (!typeList.Contains(a[i])) typeList.Add(a[i]);
        }
        requiredTypes = typeList.Count;

        // 关键规则：允许步数由求解器算出的“最少步数”决定
        plan = MinMovesSolver.Solve(a, start, m);
        if (!plan.Success)
        {
            Fail("无法求解本关的最少步数 —— " + plan.FailureReason);
            return;
        }
        if (!string.IsNullOrEmpty(plan.Notice)) Debug.LogWarning("[关卡] " + plan.Notice);
        minSteps = plan.Steps;
        allowedSteps = minSteps + Mathf.Max(0, extraSteps);

        fruits = new GameObject[n];
        if (!SpawnLevel()) return;
        if (autoFitCamera) FitCamera();

        CollectAt(start, true);     // 起点格上的种类视为开局就已经拿到
        CheckResult();              // 万一开局就集齐（极短关卡）也要判胜

        Debug.LogFormat("[关卡] 种类数={0}，最少步数={1}（先{2}，左{3}格/右{4}格），允许步数={5}",
                        requiredTypes, minSteps, plan.LeftFirst ? "左" : "右",
                        plan.LeftSteps, plan.RightSteps, allowedSteps);
    }

    /// <summary>生成物品与人物，返回是否生成成功。</summary>
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
        for (int i = 0; i < n; i++)
        {
            GameObject obj = Instantiate(fruitPrefab, CellToWorld(i, itemSize), Quaternion.identity);
            obj.name = string.Format("Fruit_{0}_type{1}", i, a[i]);
            obj.transform.localScale = Vector3.one * itemSize;
            PrepareBody(obj);

            FruitItem item = obj.GetComponent<FruitItem>();
            if (item == null) item = obj.AddComponent<FruitItem>();
            item.SetType(a[i]);

            fruits[i] = obj;
            fruitsByIndex[i] = item;
        }

        player = Instantiate(playerPrefab, CellToWorld(start, cellSize), Quaternion.identity);
        player.name = "Player";
        player.transform.localScale = Vector3.one * cellSize;
        PrepareBody(player);

        // 人物用一个偏深的颜色，和彩色物品区分开
        Renderer playerRenderer = player.GetComponentInChildren<Renderer>();
        if (playerRenderer != null) playerRenderer.material.color = new Color(0.25f, 0.28f, 0.35f);

        playerController = player.GetComponent<PlayerController>();
        if (playerController == null) playerController = player.AddComponent<PlayerController>();
        playerController.Configure(start, 0, n - 1, cellSize, CellOriginX(), allowedSteps,
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
        return -(n - 1) * 0.5f * cellSize;
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
    private void FitCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        float rowWidth = (n - 1) * cellSize + cellSize * 3f;    // 行宽 + 两侧留白
        float aspect = cam.aspect > 0.01f ? cam.aspect : 16f / 9f;

        cam.transform.rotation = Quaternion.identity;           // 沿 +z 正对整行
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
        Toast(string.Format("已经到头了（格子范围 0 ~ {0}），这一步不算步数。", n - 1));
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

    /// <summary>判定通过 / 失败。顺序很重要：先用尽步数的那一步如果刚好集齐，算通过。</summary>
    private void CheckResult()
    {
        if (gameOver) return;

        if (collectedTypes.Count >= requiredTypes)
        {
            gameOver = true;
            win = true;
            LockPlayer();
            statusText = string.Format("通过！共用 {0} 步（最优 {1} 步）", usedSteps, minSteps);
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

    /// <summary>清掉上一次生成的人物和物品，供重新开局使用。</summary>
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

    /// <summary>已拿到的种类文本（供 HUD 显示）。</summary>
    private string BuildCollectedTypesText()
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

    /// <summary>还缺的种类文本（供 HUD 显示）。</summary>
    private string BuildMissingTypesText()
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
    private string BuildHintText()
    {
        if (!plan.Success) return "";

        if (plan.LeftSteps == 0 && plan.RightSteps == 0)
        {
            return "最优解：0 步（起点格上的种类已经算拿到）";
        }
        if (plan.RightSteps == 0)
        {
            return string.Format("最优解：{0} 步 —— 一直向左走 {1} 格就集齐了", plan.Steps, plan.LeftSteps);
        }
        if (plan.LeftSteps == 0)
        {
            return string.Format("最优解：{0} 步 —— 一直向右走 {1} 格就集齐了", plan.Steps, plan.RightSteps);
        }
        return plan.LeftFirst
            ? string.Format("最优解：{0} 步 —— 先向左 {1} 格再向右 {2} 格（左边来回 + 一路向右）",
                            plan.Steps, plan.LeftSteps, plan.RightSteps)
            : string.Format("最优解：{0} 步 —— 先向右 {1} 格再向左 {2} 格（右边来回 + 一路向左）",
                            plan.Steps, plan.RightSteps, plan.LeftSteps);
    }

    private GUIStyle titleStyle;
    private GUIStyle centerStyle;

    void OnGUI()
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;

            centerStyle = new GUIStyle(GUI.skin.label);
            centerStyle.fontSize = 22;
            centerStyle.fontStyle = FontStyle.Bold;
            centerStyle.alignment = TextAnchor.MiddleCenter;
            centerStyle.wordWrap = true;
        }

        DrawStatusPanel();
        DrawTypeLegend();
        DrawStepBar();

        if (!string.IsNullOrEmpty(errorText))
        {
            DrawCenterBox("关卡数据有问题：\n" + errorText + "\n\n按 R 重新生成", Color.yellow);
        }
        else if (gameOver)
        {
            DrawCenterBox((win ? "【通过】\n" : "【失败】\n") + statusText + "\n\n按 R 重新开始",
                          win ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.45f, 0.45f));
        }

        if (toastTime > 0f && !string.IsNullOrEmpty(toastText))
        {
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(toastTime));
            GUI.Label(new Rect(0f, Screen.height - 78f, Screen.width, 24f), toastText, centerStyle);
            GUI.color = old;
        }
    }

    /// <summary>左上角状态面板。</summary>
    private void DrawStatusPanel()
    {
        GUILayout.BeginArea(new Rect(10f, 10f, 340f, 168f), GUI.skin.box);
        GUILayout.Label(string.Format("剩余步数：{0} / {1}（最少 {2} 步）",
                                      Mathf.Max(0, allowedSteps - usedSteps), allowedSteps, minSteps), titleStyle);
        GUILayout.Label(string.Format("已集齐种类：{0} / {1}", collectedTypes.Count, requiredTypes), titleStyle);
        GUILayout.Label("已拿到：" + BuildCollectedTypesText(), titleStyle);
        GUILayout.Label("还缺：" + BuildMissingTypesText(), titleStyle);
        GUILayout.Label("操作：← → / A D 移动　H 提示　R 重开", titleStyle);
        if (showHint && plan.Success) GUILayout.Label(BuildHintText(), titleStyle);
        GUILayout.EndArea();
    }

    /// <summary>状态面板下方的种类图例：拿到的是本色，没拿到的是灰色。</summary>
    private void DrawTypeLegend()
    {
        const float boxSize = 18f;
        const float stepX = 64f;
        float x = 16f;
        float y = 186f;

        GUI.Label(new Rect(x, y, 260f, 20f), "种类图例（灰色 = 还没拿到）：", titleStyle);
        y += 22f;

        for (int i = 0; i < typeList.Count; i++)
        {
            int type = typeList[i];
            bool got = collectedTypes.Contains(type);

            Color old = GUI.color;
            GUI.color = got ? FruitItem.GetTypeColor(type) : new Color(0.42f, 0.42f, 0.42f);
            GUI.DrawTexture(new Rect(x + i * stepX, y, boxSize, boxSize), Texture2D.whiteTexture);
            GUI.color = old;

            GUI.Label(new Rect(x + i * stepX + boxSize + 4f, y - 2f, 42f, 22f),
                      got ? type.ToString() : type + "?", titleStyle);
        }
    }

    /// <summary>底部步数进度条。</summary>
    private void DrawStepBar()
    {
        float progress = allowedSteps > 0 ? Mathf.Clamp01((float)usedSteps / allowedSteps) : 0f;
        Rect bar = new Rect(Screen.width * 0.5f - 150f, Screen.height - 42f, 300f, 16f);

        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.35f);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = (gameOver && !win) ? new Color(0.9f, 0.3f, 0.3f) : new Color(0.35f, 0.8f, 0.4f);
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * progress, bar.height), Texture2D.whiteTexture);
        GUI.color = old;

        GUI.Label(new Rect(bar.x, bar.y - 22f, bar.width, 20f),
                  string.Format("步数进度：{0} / {1}", usedSteps, allowedSteps), titleStyle);
    }

    /// <summary>屏幕中间的半透明提示框（胜负结果、关卡数据出错）。</summary>
    private void DrawCenterBox(string text, Color color)
    {
        float width = Mathf.Min(Screen.width - 60f, 560f);
        float height = 150f;
        Rect rect = new Rect(Screen.width * 0.5f - width * 0.5f, Screen.height * 0.5f - height * 0.5f, width, height);

        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = color;
        GUI.Label(new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, rect.height - 24f), text, centerStyle);
        GUI.color = old;
    }
}
