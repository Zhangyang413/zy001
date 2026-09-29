using System;
using UnityEngine;

/// <summary>
/// 界面字体与分辨率缩放工具。
///
/// 为什么需要它：IMGUI 自带的字体是 16 像素的位图（LegacyRuntime），
/// 放大到 20~40 像素后必然发虚，中文更是点阵效果（截图里的第一张图就是这个问题）。
/// 这里改成从操作系统取“矢量动态字体”，按每个样式的 fontSize 单独光栅化，
/// 字形与像素 1:1 对应，任意分辨率下都清晰；同时对外提供统一的 uiScale，
/// 让界面里所有字号、按钮高度、间距都按同一个比例缩放。
///
/// 用法：
///   · 界面里写尺寸时都按 1080p 基准来写，绘制前用 Px() 换算成实际像素；
///   · 建 GUIStyle 时把 font 设成 Font，fontSize 用 Px() 换算。
/// </summary>
public static class UiFont
{
    /// <summary>设计基准分辨率：界面里所有尺寸都以 1080p 为基准写死。</summary>
    private const float DesignHeight = 1080f;
    private const float DesignWidth = 1920f;

    /// <summary>
    /// 动态字体每个字号一张 1024×1024 图集，字号太大 + 汉字较多时字符会溢出图集变成方块，
    /// 所以这里给字号封顶。
    /// </summary>
    private const int MaxFontPixels = 54;

    /// <summary>字号下限：中文在 12 像素以下基本糊成一团，所以不低于 15 像素。</summary>
    private const int MinFontPixels = 15;

    private static Font font;

    /// <summary>
    /// 界面缩放系数：1 表示 1080p，2 表示 4K。
    /// 下限取 0.85 —— 窗口再小也只缩到 0.85，否则中文会被缩到看不清。
    /// </summary>
    public static float Scale
    {
        get
        {
            float scale = Mathf.Min(Screen.height / DesignHeight, Screen.width / DesignWidth);
            return Mathf.Clamp(scale, 0.85f, 2.0f);
        }
    }

    /// <summary>把 1080p 基准的设计尺寸换算成当前屏幕的实际像素（用于布局）。</summary>
    public static int Px(int designSize)
    {
        return Mathf.Max(1, Mathf.RoundToInt(designSize * Scale));
    }

    /// <summary>字号的实际像素（带上下限，避免小字糊 / 大字撑爆图集）。</summary>
    public static int FontPx(int designSize)
    {
        return Mathf.Clamp(Mathf.RoundToInt(designSize * Scale), MinFontPixels, MaxFontPixels);
    }

    /// <summary>高清动态字体（取不到系统字体时回退到内置字体）。</summary>
    public static Font Font
    {
        get
        {
            if (font == null) font = CreateFont();
            return font;
        }
    }

    private static Font CreateFont()
    {
        // 优先挑带中文字形的系统字体（Windows / macOS / Linux 常见字体都列上）
        string[] candidates =
        {
            "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑",
            "SimHei", "黑体", "Songti SC", "SimSun", "宋体",
            "PingFang SC", "Heiti SC", "WenQuanYi Micro Hei", "Arial Unicode MS", "Arial"
        };

        Font created = null;
        try
        {
            created = Font.CreateDynamicFontFromOSFont(candidates, FontPx(20));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[界面字体] 创建系统动态字体失败：" + e.Message);
        }

        if (created != null)
        {
            // 动态字体图集用双线性过滤，缩放时边缘更顺滑
            Material material = created.material;
            if (material != null && material.mainTexture != null) material.mainTexture.filterMode = FilterMode.Bilinear;
            Debug.Log("[界面字体] 已启用系统动态字体，当前缩放 Scale = " + Scale.ToString("0.00") + "。");
            return created;
        }

        Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (fallback == null) fallback = Resources.GetBuiltinResource<Font>("Arial.ttf");
        Debug.LogWarning("[界面字体] 没有找到系统字体，已回退到内置字体，中文可能显示为方块。");
        return fallback;
    }

    /// <summary>
    /// 预热：把界面会出现的字符提前光栅化，避免刚进游戏的第一帧出现方块字。
    /// 分辨率变化（字体重建）后再预热一次即可。
    /// </summary>
    public static void WarmUp(params int[] designSizes)
    {
        Font f = Font;
        if (f == null || string.IsNullOrEmpty(AllChars)) return;

        for (int i = 0; i < designSizes.Length; i++)
        {
            f.RequestCharactersInTexture(AllChars, FontPx(designSizes[i]), FontStyle.Normal);
        }
    }

    /// <summary>界面里可能出现的全部字符（用来预热字形图集）。</summary>
    public const string AllChars =
        "捡物品大冒险一行格子左右走动用给定的步数把每种都捡到至少一个" +
        "游戏说明人物等距排成每走消耗到上算已获得允许本关最优解在尽之前种类拿到否则失败" +
        "重复样路线要操作或移动查看提重开本返回入界面选择难度开始也可以直接按键盘挑战模式" +
        "限时点击开始计时用时越短越好已禁记录新的最佳无秒分厘示例场景固定配置数量位置都随机" +
        "每次开局不一样通过共用步还差没集齐种已种类中倒计时秒按键";
}
