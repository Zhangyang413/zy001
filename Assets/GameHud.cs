using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 游戏界面（全部 IMGUI 绘制，不需要额外场景对象）：
///   · 入口界面：标题 + 游戏说明 + 挑战模式开关 + 难度按钮（带最佳成绩）+ 示例关卡按钮；
///   · 游戏中 HUD：关卡信息、集齐进度、挑战模式计时与 Start 按钮、种类图例、步数进度条；
///   · 结算界面：通过 / 失败 + 用时成绩 + 换一关 / 返回入口界面。
///
/// 清晰度与自适应：
///   · 字体走 UiFont（操作系统的矢量动态字体），字号按分辨率缩放，任意分辨率下都清晰；
///   · 面板高度由每一行的样式实测高度累加得出（GUIStyle.CalcHeight 会算上自动换行），
///     背景再按这个高度绘制，所以文字再多也不会像以前那样被面板裁掉。
///
/// 由 createfruit 在运行时挂到同一个 GameObject 上，并把自己的引用赋给 game。
/// </summary>
public class GameHud : MonoBehaviour
{
    [Tooltip("由 createfruit 运行时赋值")]
    public createfruit game;

    /// <summary>本帧界面上被点到的操作，等所有布局画完后再统一执行（避免 BeginArea / EndArea 不配对）。</summary>
    private enum HudCommand
    {
        None,
        BackToMenu,
        Restart,
        NextLevel,
        StartTimer
    }

    private HudCommand pending = HudCommand.None;

    private float builtScale = -1f;
    private GUIStyle titleStyle;
    private GUIStyle subTitleStyle;
    private GUIStyle sectionStyle;
    private GUIStyle normalStyle;
    private GUIStyle smallStyle;
    private GUIStyle centerStyle;
    private GUIStyle timerStyle;
    private GUIStyle winStyle;
    private GUIStyle loseStyle;
    private GUIStyle errorStyle;
    private GUIStyle buttonStyle;
    private GUIStyle startButtonStyle;
    private GUIStyle toggleStyle;
    private GUIStyle boxStyle;

    // ============================ 面板行（高度实测，不会被裁切） ============================

    private enum RowKind
    {
        Label,
        Button,
        Toggle,
        Group              // 带边框的一组内容（例如“游戏说明”框）
    }

    private sealed class Row
    {
        public RowKind kind;
        public GUIStyle style;
        public string text;
        public int spaceBefore;        // 这一行前面额外留白（像素）
        public bool highlight;          // 按钮用高亮底色
        public bool clicked;            // 绘制后回填：按钮是否被点到
        public int action;              // 按钮被点到时对应的动作编号（0 = 无动作）
        public bool toggleValue;        // Toggle 的当前值
        public bool isChallengeToggle;  // 这个 Toggle 绑定到 challengeMode
        public List<Row> children;      // Group 的内容
        public float inset;             // Group 的左右内边距（取 box 样式的 padding）
    }

    private static Row Label(GUIStyle style, string text, int spaceBefore = 0)
    {
        return new Row { kind = RowKind.Label, style = style, text = text, spaceBefore = spaceBefore };
    }

    private static Row Button(GUIStyle style, string text, int action = 0, bool highlight = false, int spaceBefore = 0)
    {
        return new Row
        {
            kind = RowKind.Button, style = style, text = text, action = action,
            highlight = highlight, spaceBefore = spaceBefore
        };
    }

    private static Row Toggle(GUIStyle style, string text, bool value, int spaceBefore = 0)
    {
        return new Row
        {
            kind = RowKind.Toggle, style = style, text = text, spaceBefore = spaceBefore,
            toggleValue = value, isChallengeToggle = true
        };
    }

    private static Row Group(List<Row> children, int spaceBefore = 0)
    {
        return new Row { kind = RowKind.Group, children = children, spaceBefore = spaceBefore };
    }

    /// <summary>逐行实测高度：标签用 CalcHeight（包含自动换行），按钮 / 开关用样式高度，分组再加上边框内边距。</summary>
    private float Measure(List<Row> rows, float width)
    {
        float height = 0f;
        for (int i = 0; i < rows.Count; i++) height += MeasureRow(rows[i], width);
        return height;
    }

    private float MeasureRow(Row row, float width)
    {
        float height = row.spaceBefore;

        if (row.kind == RowKind.Group)
        {
            height += boxStyle.padding.vertical + boxStyle.margin.vertical;
            height += Measure(row.children, Mathf.Max(Px(60), width - row.inset * 2f));
            return height;
        }

        height += row.style.CalcHeight(new GUIContent(row.text ?? ""), width);
        return height + row.style.margin.vertical;
    }

    private void DrawRows(List<Row> rows)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            if (row.spaceBefore > 0) GUILayout.Space(row.spaceBefore);

            if (row.kind == RowKind.Group)
            {
                GUILayout.BeginVertical(boxStyle);
                DrawRows(row.children);
                GUILayout.EndVertical();
            }
            else if (row.kind == RowKind.Button)
            {
                Color oldBackground = GUI.backgroundColor;
                if (row.highlight) GUI.backgroundColor = new Color(1.5f, 1.3f, 0.5f);
                row.clicked = GUILayout.Button(row.text, row.style);
                GUI.backgroundColor = oldBackground;
            }
            else if (row.kind == RowKind.Toggle)
            {
                bool value = GUILayout.Toggle(row.toggleValue, row.text, row.style);
                if (row.isChallengeToggle && game != null && value != game.challengeMode)
                {
                    game.challengeMode = value;
                    if (!value) game.showHint = false;
                }
                row.toggleValue = value;
            }
            else
            {
                GUILayout.Label(row.text, row.style);
            }
        }
    }

    /// <summary>画一个面板：背景按内容实测高度绘制（文字不会被裁掉），返回面板矩形供图例等排版使用。</summary>
    private Rect DrawPanel(float centerX, float topY, float width, List<Row> rows, float alpha, bool centerY = false)
    {
        float pad = Px(16);
        float inner = Mathf.Max(Px(160), width - pad * 2f);
        float height = Measure(rows, inner) + pad * 2f + Px(4);      // 多留一点余量，舍入误差也不会裁字
        float y = centerY ? Mathf.Max(Px(8), (Screen.height - height) * 0.5f) : topY;
        Rect panel = new Rect(centerX - width * 0.5f, y, width, height);

        if (alpha > 0f)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0.03f, 0.05f, 0.09f, alpha);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        GUILayout.BeginArea(new Rect(panel.x + pad, panel.y + pad, inner, height - pad * 2f));
        DrawRows(rows);
        GUILayout.EndArea();

        return panel;
    }

    /// <summary>返回第一个被点到的按钮的动作编号（都没点到则返回 0）。</summary>
    private static int ClickedAction(List<Row> rows)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].clicked) return rows[i].action;
        }
        return 0;
    }

    // ============================ 样式 ============================

    private int Px(int designSize)
    {
        return UiFont.Px(designSize);
    }

    private void EnsureStyles()
    {
        float scale = UiFont.Scale;
        if (titleStyle != null && Mathf.Abs(scale - builtScale) < 0.001f) return;
        builtScale = scale;

        Font ui = UiFont.Font;
        Color white = new Color(0.95f, 0.96f, 0.99f);

        titleStyle = MakeStyle(ui, GUI.skin.label, 34, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.93f, 0.65f), true);
        subTitleStyle = MakeStyle(ui, GUI.skin.label, 18, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.82f, 0.88f, 0.97f), true);
        sectionStyle = MakeStyle(ui, GUI.skin.label, 22, FontStyle.Bold, TextAnchor.UpperLeft, new Color(1f, 0.90f, 0.55f), false);
        normalStyle = MakeStyle(ui, GUI.skin.label, 17, FontStyle.Normal, TextAnchor.UpperLeft, white, true);
        smallStyle = MakeStyle(ui, GUI.skin.label, 15, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.86f, 0.89f, 0.95f), true);
        centerStyle = MakeStyle(ui, GUI.skin.label, 18, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, true);
        timerStyle = MakeStyle(ui, GUI.skin.label, 26, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.55f, 0.92f, 1f), true);
        winStyle = MakeStyle(ui, GUI.skin.label, 30, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.45f, 1f, 0.5f), true);
        loseStyle = MakeStyle(ui, GUI.skin.label, 30, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.48f, 0.48f), true);
        errorStyle = MakeStyle(ui, GUI.skin.label, 19, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.35f), true);
        buttonStyle = MakeStyle(ui, GUI.skin.button, 20, FontStyle.Normal, TextAnchor.MiddleCenter, white, false, 46);
        toggleStyle = MakeStyle(ui, GUI.skin.toggle, 19, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.92f, 0.60f), true, 46);

        // Start 按钮用纯色底 + 深色字，靠 GUI.backgroundColor 上色，醒目又好认
        startButtonStyle = MakeStyle(ui, GUI.skin.button, 26, FontStyle.Bold, TextAnchor.MiddleCenter,
                                      new Color(0.16f, 0.13f, 0.04f), false, 62);
        startButtonStyle.normal.background = Texture2D.whiteTexture;
        startButtonStyle.hover.background = Texture2D.whiteTexture;
        startButtonStyle.active.background = Texture2D.whiteTexture;

        boxStyle = new GUIStyle(GUI.skin.box);

        // 预热字形图集，避免刚进游戏时中文显示成方块
        UiFont.WarmUp(34, 30, 26, 22, 20, 19, 18, 17, 15);
    }

    private GUIStyle MakeStyle(Font font, GUIStyle basis, int designSize, FontStyle fontStyle,
                               TextAnchor alignment, Color color, bool wordWrap, int designHeight = 0)
    {
        GUIStyle style = new GUIStyle(basis);
        style.font = font;
        style.fontSize = UiFont.FontPx(designSize);
        style.fontStyle = fontStyle;
        style.alignment = alignment;
        style.wordWrap = wordWrap;
        style.normal.textColor = color;
        if (designHeight > 0) style.fixedHeight = Px(designHeight);
        return style;
    }

    // ============================ 入口 ============================

    void OnGUI()
    {
        if (game == null) return;

        EnsureStyles();

        // 让内置控件也用上高清字体，结束后还原，避免影响编辑器本身的界面
        Font skinFont = GUI.skin.font;
        GUI.skin.font = UiFont.Font;
        try
        {
            if (game.phase == createfruit.Phase.Menu) DrawMenu();
            else DrawPlaying();
        }
        finally
        {
            GUI.skin.font = skinFont;
        }
    }

    // ============================ 入口界面 ============================

    private void DrawMenu()
    {
        Color oldColor = GUI.color;
        GUI.color = new Color(0.04f, 0.06f, 0.11f, 0.93f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = oldColor;

        List<Row> rows = new List<Row>();
        rows.Add(Label(titleStyle, "捡 物 品 大 冒 险"));
        rows.Add(Label(subTitleStyle, "在一行格子上左右走动，用给定的步数把每种物品都捡到至少一个"));
        if (!string.IsNullOrEmpty(game.errorText)) rows.Add(Label(errorStyle, "提示：" + game.errorText, Px(4)));

        // ---- 挑战模式开关 ----
        rows.Add(Toggle(toggleStyle, "  挑战模式（限时挑战）", game.challengeMode, Px(8)));
        rows.Add(Label(smallStyle, "开启后：进入关卡要先点 Start 开始计时，最优解提示被禁用，失败只能换一关，" +
                                   "并按难度记录最佳用时（用时越短越好）。"));

        // ---- 游戏说明（带边框的一组）----
        List<Row> help = new List<Row>();
        help.Add(Label(sectionStyle, "游戏说明"));
        help.Add(Label(normalStyle, "1. 人物与物品等距排成一行，人物每走一格消耗 1 步；"));
        help.Add(Label(normalStyle, "2. 走到物品上就自动捡起，人物脚下那格的物品在开局就算已获得；"));
        help.Add(Label(normalStyle, "3. 允许步数 = 本关的最优解步数；在步数用尽之前把每种物品都拿到至少一个 = 通过，否则失败；"));
        help.Add(Label(normalStyle, "4. 捡到重复种类的物品同样消耗一步，所以路线要算好；"));
        help.Add(Label(normalStyle, "5. 操作：← → 或 A D 移动、H 查看最优解提示、R 重开本关、Esc 返回本界面。"));
        Row helpGroup = Group(help, Px(8));
        helpGroup.inset = boxStyle.padding.horizontal;
        rows.Add(helpGroup);

        // ---- 难度按钮 ----
        rows.Add(Label(sectionStyle, "选择难度开始游戏（也可以直接按键盘 1 / 2 / 3 / E）：", Px(8)));
        for (int i = 0; i < game.DifficultyCount; i++)
        {
            createfruit.Difficulty difficulty = game.GetDifficulty(i);
            if (difficulty == null) continue;

            string hotkey = i < 9 ? "　［" + (i + 1) + "］" : "";
            string label = string.Format("  {0}　物品 {1}~{2} 个 · {3} 种　最佳 {4}{5}",
                                         difficulty.title, difficulty.minCells, difficulty.maxCells,
                                         difficulty.typeCount, game.GetBestTimeText(i), hotkey);
            rows.Add(Button(buttonStyle, label, i + 1, false, Px(2)));    // 动作编号：1 = 难度 0，2 = 难度 1，依此类推
        }
        rows.Add(Button(buttonStyle, "  " + game.GetSampleLevelSummary() + "　最佳 " + game.GetBestTimeText(-1), -1, false, Px(2)));
        rows.Add(Label(smallStyle, "难度 1/2/3 的物品数量、物品位置与人物位置都是随机生成的，每次开局都不一样。", Px(4)));

        float width = Mathf.Min(Screen.width - Px(24), Px(820));
        DrawPanel(Screen.width * 0.5f, 0f, width, rows, 0f, true);

        // 布局全部结束后再切关卡，避免 BeginArea / EndArea 不配对导致的 IMGUI 报错
        int action = ClickedAction(rows);
        if (action < 0) game.StartSampleLevel();
        else if (action > 0) game.StartDifficulty(action - 1);
    }

    // ============================ 游戏中 ============================

    private void DrawPlaying()
    {
        pending = HudCommand.None;

        Rect hudPanel = DrawHud();
        DrawLegend(hudPanel);
        DrawStepBar();

        if (!string.IsNullOrEmpty(game.errorText)) DrawMessageBox();
        else if (game.gameOver) DrawResult();

        DrawToast();

        // 布局全部结束后再改状态，避免 BeginArea / EndArea 不配对
        switch (pending)
        {
            case HudCommand.BackToMenu:
                game.EnterMenu();
                break;
            case HudCommand.Restart:
                game.BuildLevel();
                break;
            case HudCommand.NextLevel:
                if (game.difficultyIndex >= 0) game.StartDifficulty(game.difficultyIndex);
                break;
            case HudCommand.StartTimer:
                game.StartTimer();
                break;
        }
        pending = HudCommand.None;
    }

    /// <summary>左上角信息面板（挑战模式下包含计时与 Start 按钮）。</summary>
    private Rect DrawHud()
    {
        List<Row> rows = new List<Row>();
        rows.Add(Label(sectionStyle, game.modeTitle));
        rows.Add(Label(normalStyle, string.Format("物品 {0} 个 · 需要集齐 {1} 种", game.cellCount, game.requiredTypes)));
        rows.Add(Label(normalStyle, string.Format("剩余步数：{0} / {1}（最优 {2} 步）",
                                                  Mathf.Max(0, game.allowedSteps - game.usedSteps), game.allowedSteps, game.minSteps)));
        rows.Add(Label(normalStyle, string.Format("已集齐：{0} / {1} 种　已拿到：{2}　还缺：{3}",
                                                  game.CollectedTypeCount, game.requiredTypes,
                                                  game.GetCollectedTypesText(), game.GetMissingTypesText())));

        if (game.challengeMode)
        {
            rows.Add(Label(timerStyle, string.Format("用时 {0}　最佳 {1}{2}",
                                                    createfruit.FormatTime(game.challengeTime),
                                                    createfruit.FormatTime(game.challengeBest),
                                                    game.challengeNewRecord ? "　★新纪录" : ""), Px(4)));

            if (game.challengeActive) rows.Add(Label(smallStyle, "计时中：用时越短越好，最优解提示已禁用"));
            else rows.Add(Button(startButtonStyle, "Start　（开始计时，也可按空格）", 1, true));
        }
        else if (game.showHint && game.plan.Success)
        {
            rows.Add(Label(normalStyle, game.GetHintText()));
        }

        rows.Add(Label(smallStyle, game.challengeMode
                                        ? "操作：← → 或 A D 移动　Esc 返回本界面"
                                        : "操作：← → 或 A D 移动　H 提示　R 重开　Esc 返回本界面", Px(2)));

        float width = Mathf.Min(Px(460), Screen.width * 0.46f);
        Rect panel = DrawPanel(width * 0.5f + Px(12), Px(12), width, rows, 0.45f);

        // 右上角：返回入口界面按钮（绝对坐标，不参与面板排版）
        float backW = Px(150);
        float backH = Px(40);
        if (GUI.Button(new Rect(Screen.width - backW - Px(12), Px(12), backW, backH), "返回菜单 (Esc)", buttonStyle))
        {
            pending = HudCommand.BackToMenu;
        }

        if (ClickedAction(rows) == 1) pending = HudCommand.StartTimer;
        return panel;
    }

    /// <summary>种类图例：拿到的是本色，没拿到的是灰色。</summary>
    private void DrawLegend(Rect hudPanel)
    {
        float box = Px(20);
        float stepX = Px(74);
        float lineHeight = Px(32);
        float x0 = Px(18);
        float y0 = hudPanel.yMax + Px(10);
        float available = Screen.width - x0 * 2f;

        string title = "种类图例（灰色 = 还没拿到）：";
        float titleHeight = smallStyle.CalcHeight(new GUIContent(title), available);
        GUI.Label(new Rect(x0, y0, available, titleHeight), title, smallStyle);

        float chipsTop = y0 + titleHeight + Px(4);
        List<int> list = game.TypeList;
        int perLine = Mathf.Max(1, Mathf.FloorToInt((hudPanel.width - Px(20)) / stepX));

        for (int i = 0; i < list.Count; i++)
        {
            int type = list[i];
            bool got = game.IsTypeCollected(type);

            float x = x0 + (i % perLine) * stepX;
            float y = chipsTop + (i / perLine) * lineHeight;

            Color oldColor = GUI.color;
            GUI.color = got ? FruitItem.GetTypeColor(type) : new Color(0.42f, 0.42f, 0.42f);
            GUI.DrawTexture(new Rect(x, y, box, box), Texture2D.whiteTexture);
            GUI.color = oldColor;

            string text = got ? type.ToString() : type + "?";
            float textHeight = smallStyle.CalcHeight(new GUIContent(text), stepX - box);
            GUI.Label(new Rect(x + box + Px(6), y + (box - textHeight) * 0.5f, stepX - box, textHeight), text, smallStyle);
        }
    }

    /// <summary>底部步数进度条。</summary>
    private void DrawStepBar()
    {
        float barWidth = Mathf.Min(Screen.width * 0.5f, Px(470));
        float barHeight = Px(16);
        Rect bar = new Rect(Screen.width * 0.5f - barWidth * 0.5f, Screen.height - Px(34) - barHeight, barWidth, barHeight);

        float progress = game.allowedSteps > 0 ? Mathf.Clamp01((float)game.usedSteps / game.allowedSteps) : 0f;

        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.38f);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = (game.gameOver && !game.win) ? new Color(0.90f, 0.32f, 0.32f) : new Color(0.36f, 0.80f, 0.42f);
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * progress, bar.height), Texture2D.whiteTexture);
        GUI.color = oldColor;

        string text = string.Format("步数进度：{0} / {1}", game.usedSteps, game.allowedSteps);
        float textHeight = centerStyle.CalcHeight(new GUIContent(text), barWidth);
        GUI.Label(new Rect(bar.x, bar.y - textHeight - Px(4), barWidth, textHeight), text, centerStyle);
    }

    // ============================ 结算 / 出错 / 浮动提示 ============================

    private void DrawResult()
    {
        List<Row> rows = new List<Row>();
        rows.Add(Label(game.win ? winStyle : loseStyle, game.win ? "【通过】" : "【失败】"));
        rows.Add(Label(normalStyle, game.statusText));

        if (game.challengeMode)
        {
            rows.Add(Label(timerStyle, string.Format("用时 {0}　最佳 {1}{2}",
                                                    createfruit.FormatTime(game.challengeTime),
                                                    createfruit.FormatTime(game.challengeBest),
                                                    game.challengeNewRecord ? "　★新纪录！" : ""), Px(4)));
            if (!game.win) rows.Add(Label(smallStyle, "挑战模式失败后不能重试本关，请点“换一关”继续挑战。"));
        }

        if (game.CanRetry) rows.Add(Button(buttonStyle, game.win ? "再玩一次（本关）　[空格]" : "重试本关　[空格]", 1, false, Px(8)));
        if (game.difficultyIndex >= 0) rows.Add(Button(buttonStyle, "换一关（重新随机）　[N]", 2));
        rows.Add(Button(buttonStyle, "返回入口界面　[Esc]", 3));

        DrawPanel(Screen.width * 0.5f, 0f, Mathf.Min(Screen.width - Px(24), Px(620)), rows, 0.85f, true);

        switch (ClickedAction(rows))
        {
            case 1: pending = HudCommand.Restart; break;
            case 2: pending = HudCommand.NextLevel; break;
            case 3: pending = HudCommand.BackToMenu; break;
        }
    }

    /// <summary>关卡数据出错时的提示（例如场景里的数组为空）。</summary>
    private void DrawMessageBox()
    {
        List<Row> rows = new List<Row>();
        rows.Add(Label(loseStyle, "关卡数据有问题"));
        rows.Add(Label(normalStyle, game.errorText));
        rows.Add(Button(buttonStyle, "返回入口界面　[Esc]", 3, false, Px(8)));

        DrawPanel(Screen.width * 0.5f, 0f, Mathf.Min(Screen.width - Px(24), Px(560)), rows, 0.85f, true);
        if (ClickedAction(rows) == 3) pending = HudCommand.BackToMenu;
    }

    /// <summary>屏幕下方的浮动提示（捡到物品、撞到边界等）。</summary>
    private void DrawToast()
    {
        if (game.toastTime <= 0f || string.IsNullOrEmpty(game.toastText)) return;

        float width = Screen.width - Px(24);
        float height = centerStyle.CalcHeight(new GUIContent(game.toastText), width - Px(24));
        Rect rect = new Rect(Px(12), Screen.height - height - Px(40), width, height);

        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(game.toastTime) * 0.5f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(game.toastTime));
        GUI.Label(new Rect(rect.x + Px(12), rect.y, width - Px(24), height), game.toastText, centerStyle);
        GUI.color = oldColor;
    }
}
