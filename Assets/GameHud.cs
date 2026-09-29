using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 游戏界面（全部 IMGUI 绘制，不需要额外场景对象）：
///   · 入口界面：标题 + 游戏说明 + 难度按钮（可点选，也能按 1 / 2 / 3 / E）；
///   · 游戏中 HUD：关卡信息、集齐进度、种类图例、步数进度条、提示；
///   · 结算界面：通过 / 失败 + 再来一次 / 换一关 / 返回入口界面。
///
/// 由 createfruit 在运行时挂到同一个 GameObject 上，并把自己的引用赋给 game。
/// </summary>
public class GameHud : MonoBehaviour
{
    [Tooltip("由 createfruit 运行时赋值")]
    public createfruit game;

    private float styleScale = -1f;
    private GUIStyle titleStyle;
    private GUIStyle subTitleStyle;
    private GUIStyle sectionStyle;
    private GUIStyle normalStyle;
    private GUIStyle smallStyle;
    private GUIStyle buttonStyle;
    private GUIStyle centerStyle;
    private GUIStyle winStyle;
    private GUIStyle loseStyle;
    private GUIStyle errorStyle;

    /// <summary>按屏幕高度缩放字号，小窗口也不会挤在一起。</summary>
    private int Scaled(int size)
    {
        return Mathf.Max(10, Mathf.RoundToInt(size * styleScale));
    }

    private void EnsureStyles()
    {
        float scale = Mathf.Clamp(Screen.height / 900f, 0.75f, 1.4f);
        if (titleStyle != null && Mathf.Abs(scale - styleScale) < 0.01f) return;
        styleScale = scale;

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = Scaled(40);
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = new Color(1f, 0.93f, 0.65f);

        subTitleStyle = new GUIStyle(GUI.skin.label);
        subTitleStyle.fontSize = Scaled(17);
        subTitleStyle.alignment = TextAnchor.MiddleCenter;
        subTitleStyle.normal.textColor = new Color(0.80f, 0.86f, 0.95f);
        subTitleStyle.wordWrap = true;

        sectionStyle = new GUIStyle(GUI.skin.label);
        sectionStyle.fontSize = Scaled(20);
        sectionStyle.fontStyle = FontStyle.Bold;
        sectionStyle.normal.textColor = new Color(1f, 0.90f, 0.55f);

        normalStyle = new GUIStyle(GUI.skin.label);
        normalStyle.fontSize = Scaled(16);
        normalStyle.wordWrap = true;
        normalStyle.normal.textColor = new Color(0.94f, 0.95f, 0.98f);

        smallStyle = new GUIStyle(GUI.skin.label);
        smallStyle.fontSize = Scaled(14);
        smallStyle.normal.textColor = new Color(0.90f, 0.92f, 0.96f);

        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = Scaled(19);
        buttonStyle.fixedHeight = Scaled(42);
        buttonStyle.alignment = TextAnchor.MiddleCenter;

        centerStyle = new GUIStyle(GUI.skin.label);
        centerStyle.fontSize = Scaled(20);
        centerStyle.fontStyle = FontStyle.Bold;
        centerStyle.alignment = TextAnchor.MiddleCenter;
        centerStyle.wordWrap = true;

        winStyle = new GUIStyle(GUI.skin.label);
        winStyle.fontSize = Scaled(32);
        winStyle.fontStyle = FontStyle.Bold;
        winStyle.alignment = TextAnchor.MiddleCenter;
        winStyle.normal.textColor = new Color(0.45f, 1f, 0.5f);

        loseStyle = new GUIStyle(GUI.skin.label);
        loseStyle.fontSize = Scaled(32);
        loseStyle.fontStyle = FontStyle.Bold;
        loseStyle.alignment = TextAnchor.MiddleCenter;
        loseStyle.normal.textColor = new Color(1f, 0.48f, 0.48f);

        errorStyle = new GUIStyle(GUI.skin.label);
        errorStyle.fontSize = Scaled(20);
        errorStyle.fontStyle = FontStyle.Bold;
        errorStyle.alignment = TextAnchor.MiddleCenter;
        errorStyle.wordWrap = true;
        errorStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);
    }

    void OnGUI()
    {
        if (game == null) return;

        EnsureStyles();

        if (game.phase == createfruit.Phase.Menu)
        {
            DrawMenu();
            DrawToast();
            return;
        }

        DrawHud_Result result = DrawHud();

        if (!string.IsNullOrEmpty(game.errorText)) DrawMessageBox();
        else if (game.gameOver) DrawResult();

        DrawToast();

        // 注意：所有 GUILayout 区域结束后再切换界面，避免 BeginArea / EndArea 不配对
        if (result == DrawHud_Result.BackToMenu) game.EnterMenu();
    }

    private enum DrawHud_Result
    {
        None,
        BackToMenu
    }

    // ============================ 入口界面 ============================

    private void DrawMenu()
    {
        // 深色背景，让菜单像一页“开始界面”
        Color oldColor = GUI.color;
        GUI.color = new Color(0.05f, 0.08f, 0.13f, 0.93f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = oldColor;

        float width = Mathf.Min(Screen.width - 60f, Scaled(700));
        float height = Mathf.Min(Screen.height - 40f, Scaled(690));
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

        GUILayout.BeginArea(panel);

        GUILayout.Label("捡 物 品 大 冒 险", titleStyle);
        GUILayout.Label("在一行格子上左右走动，用给定的步数把每种物品都捡到至少一个", subTitleStyle);
        if (!string.IsNullOrEmpty(game.errorText)) GUILayout.Label("提示：" + game.errorText, errorStyle);
        GUILayout.Space(Scaled(6));

        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("游戏说明", sectionStyle);
        GUILayout.Label("1. 人物与物品等距排成一行，人物每走一格消耗 1 步；", normalStyle);
        GUILayout.Label("2. 走到物品上就自动捡起，人物脚下那格的物品在开局就算已获得；", normalStyle);
        GUILayout.Label("3. 允许步数 = 本关的最优解步数；在步数用尽之前把每种物品都拿到至少一个 = 通过，否则失败；", normalStyle);
        GUILayout.Label("4. 捡到重复种类的物品同样消耗一步，所以路线要算好；", normalStyle);
        GUILayout.Label("5. 操作：← → 或 A D 移动、H 查看最优解提示、R 重开本关、Esc 返回本界面。", normalStyle);
        GUILayout.EndVertical();

        GUILayout.Space(Scaled(8));
        GUILayout.Label("选择难度开始游戏（也可以直接按键盘 1 / 2 / 3 / E）：", sectionStyle);

        int startDifficulty = -1;
        bool startSample = false;

        for (int i = 0; i < game.DifficultyCount; i++)
        {
            createfruit.Difficulty difficulty = game.GetDifficulty(i);
            if (difficulty == null) continue;

            string hotkey = i < 3 ? "　［快捷键 " + (i + 1) + "］" : "";
            string label = string.Format("  {0}　物品 {1}~{2} 个 · {3} 种{4}",
                                         difficulty.title, difficulty.minCells, difficulty.maxCells,
                                         difficulty.typeCount, hotkey);
            if (GUILayout.Button(label, buttonStyle)) startDifficulty = i;
        }

        if (GUILayout.Button("  " + game.GetSampleLevelSummary() + "　［快捷键 E］", buttonStyle)) startSample = true;

        GUILayout.Space(Scaled(4));
        GUILayout.Label("难度 1/2/3 的物品数量、物品位置与人物位置都是随机生成的，每次开局都不一样。", smallStyle);
        GUILayout.EndArea();

        // 布局全部结束后再切关卡，避免 BeginArea / EndArea 不配对导致的 IMGUI 报错
        if (startDifficulty >= 0) game.StartDifficulty(startDifficulty);
        else if (startSample) game.StartSampleLevel();
    }

    // ============================ 游戏中 HUD ============================

    private DrawHud_Result DrawHud()
    {
        float panelWidth = Scaled(400);
        float panelHeight = Scaled(212);

        GUILayout.BeginArea(new Rect(12f, 12f, panelWidth, panelHeight), GUI.skin.box);
        GUILayout.Label(game.modeTitle, sectionStyle);
        GUILayout.Label(string.Format("物品 {0} 个 · 需要集齐 {1} 种", game.cellCount, game.requiredTypes), normalStyle);
        GUILayout.Label(string.Format("剩余步数：{0} / {1}（最优 {2} 步）",
                                      Mathf.Max(0, game.allowedSteps - game.usedSteps), game.allowedSteps, game.minSteps), normalStyle);
        GUILayout.Label(string.Format("已集齐：{0} / {1} 种", game.CollectedTypeCount, game.requiredTypes), normalStyle);
        GUILayout.Label("已拿到：" + game.GetCollectedTypesText(), normalStyle);
        GUILayout.Label("还缺：" + game.GetMissingTypesText(), normalStyle);
        if (game.showHint && game.plan.Success) GUILayout.Label(game.GetHintText(), normalStyle);
        else GUILayout.Label("← → / A D 移动　H 提示　R 重开　Esc 菜单", normalStyle);
        GUILayout.EndArea();

        // 右上角：返回入口界面按钮
        DrawHud_Result result = DrawHud_Result.None;
        if (GUI.Button(new Rect(Screen.width - Scaled(140) - 12f, 12f, Scaled(140), Scaled(34)), "返回菜单 (Esc)", buttonStyle))
        {
            result = DrawHud_Result.BackToMenu;
        }

        DrawLegend(panelWidth, panelHeight);
        DrawStepBar();
        return result;
    }

    /// <summary>种类图例：拿到的是本色，没拿到的是灰色。</summary>
    private void DrawLegend(float panelWidth, float panelHeight)
    {
        float box = Scaled(18);
        float stepX = Scaled(64);
        float lineHeight = Scaled(26);
        float x0 = 16f;
        float y0 = 12f + panelHeight + Scaled(12);

        GUI.Label(new Rect(x0, y0, Scaled(280), Scaled(20)), "种类图例（灰色 = 还没拿到）：", smallStyle);

        List<int> list = game.TypeList;
        int perLine = Mathf.Max(1, Mathf.FloorToInt((panelWidth - Scaled(24)) / stepX));

        for (int i = 0; i < list.Count; i++)
        {
            int type = list[i];
            bool got = game.IsTypeCollected(type);

            float x = x0 + (i % perLine) * stepX;
            float y = y0 + Scaled(24) + (i / perLine) * lineHeight;

            Color oldColor = GUI.color;
            GUI.color = got ? FruitItem.GetTypeColor(type) : new Color(0.42f, 0.42f, 0.42f);
            GUI.DrawTexture(new Rect(x, y, box, box), Texture2D.whiteTexture);
            GUI.color = oldColor;

            GUI.Label(new Rect(x + box + Scaled(4), y - Scaled(2), Scaled(42), Scaled(22)),
                      got ? type.ToString() : type + "?", smallStyle);
        }
    }

    /// <summary>底部步数进度条。</summary>
    private void DrawStepBar()
    {
        float barWidth = Mathf.Min(Screen.width * 0.5f, 420f);
        float barHeight = Scaled(16);
        Rect bar = new Rect(Screen.width * 0.5f - barWidth * 0.5f, Screen.height - 44f, barWidth, barHeight);

        float progress = game.allowedSteps > 0 ? Mathf.Clamp01((float)game.usedSteps / game.allowedSteps) : 0f;

        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.38f);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = (game.gameOver && !game.win) ? new Color(0.90f, 0.32f, 0.32f) : new Color(0.36f, 0.80f, 0.42f);
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * progress, bar.height), Texture2D.whiteTexture);
        GUI.color = oldColor;

        GUI.Label(new Rect(bar.x, bar.y - Scaled(24), bar.width, Scaled(22)),
                  string.Format("步数进度：{0} / {1}", game.usedSteps, game.allowedSteps), centerStyle);
    }

    // ============================ 结算 / 出错 / 浮动提示 ============================

    private void DrawResult()
    {
        float width = Mathf.Min(Screen.width - 60f, Scaled(540));
        float height = Scaled(250);
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.82f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = oldColor;

        bool restart = false;
        bool newLevel = false;
        bool backToMenu = false;

        GUILayout.BeginArea(new Rect(panel.x + Scaled(18), panel.y + Scaled(16),
                                     panel.width - Scaled(36), panel.height - Scaled(32)));
        GUILayout.Label(game.win ? "【通过】" : "【失败】", game.win ? winStyle : loseStyle);
        GUILayout.Label(game.statusText, normalStyle);
        GUILayout.Space(Scaled(8));

        if (GUILayout.Button(game.win ? "再玩一次（本关）　[空格]" : "重试本关　[空格]", buttonStyle)) restart = true;
        if (game.difficultyIndex >= 0 && GUILayout.Button("换一关（重新随机）　[N]", buttonStyle)) newLevel = true;
        if (GUILayout.Button("返回入口界面　[Esc]", buttonStyle)) backToMenu = true;
        GUILayout.EndArea();

        // 布局全部结束后再切状态，避免 BeginArea / EndArea 不配对导致的 IMGUI 报错
        if (restart) game.BuildLevel();
        else if (newLevel) game.StartDifficulty(game.difficultyIndex);
        else if (backToMenu) game.EnterMenu();
    }

    /// <summary>关卡数据出错时的提示（例如场景里的数组为空）。</summary>
    private void DrawMessageBox()
    {
        float width = Mathf.Min(Screen.width - 60f, Scaled(520));
        float height = Scaled(180);
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.82f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = oldColor;

        GUILayout.BeginArea(new Rect(panel.x + Scaled(18), panel.y + Scaled(16),
                                     panel.width - Scaled(36), panel.height - Scaled(32)));
        GUILayout.Label("关卡数据有问题", loseStyle);
        GUILayout.Label(game.errorText, normalStyle);
        GUILayout.Space(Scaled(6));
        if (GUILayout.Button("返回入口界面　[Esc]", buttonStyle)) game.EnterMenu();
        GUILayout.EndArea();
    }

    /// <summary>屏幕下方的浮动提示（捡到物品、撞到边界等）。</summary>
    private void DrawToast()
    {
        if (game.toastTime <= 0f || string.IsNullOrEmpty(game.toastText)) return;

        Color oldColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(game.toastTime));
        GUI.Label(new Rect(0f, Screen.height - Scaled(84), Screen.width, Scaled(24)), game.toastText, centerStyle);
        GUI.color = oldColor;
    }
}
