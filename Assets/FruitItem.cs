using UnityEngine;

/// <summary>
/// 单个物品（水果）：记录自己的种类、按种类着色，并在物体上方显示种类编号。
/// 物品的生成、拾取与胜负判定都由 createfruit 负责，本脚本只负责“长什么样”。
/// </summary>
public class FruitItem : MonoBehaviour
{
    [Tooltip("物品种类，由 createfruit 生成时写入")]
    public int fruitType = 0;

    /// <summary>每种物品的颜色（种类值超出表格长度时会循环取用，保证任意种类数都能区分开）。</summary>
    private static readonly Color[] TypeColors =
    {
        new Color(0.95f, 0.25f, 0.25f),     // 1 红
        new Color(0.30f, 0.85f, 0.35f),     // 2 绿
        new Color(1.00f, 0.85f, 0.20f),     // 3 黄
        new Color(0.25f, 0.80f, 0.95f),     // 4 青
        new Color(0.75f, 0.40f, 0.95f),     // 5 紫
        new Color(1.00f, 0.55f, 0.15f),     // 6 橙
        new Color(1.00f, 0.45f, 0.75f),     // 7 粉
        new Color(0.35f, 0.55f, 1.00f),     // 8 蓝
    };

    /// <summary>按种类取颜色（本体和 HUD 共用，保证配色一致）。</summary>
    public static Color GetTypeColor(int type)
    {
        int index = ((type - 1) % TypeColors.Length + TypeColors.Length) % TypeColors.Length;
        return TypeColors[index];
    }

    /// <summary>设置种类并立刻上色；createfruit 生成物品时会调用。</summary>
    public void SetType(int type)
    {
        fruitType = type;
        ApplyColor();
    }

    /// <summary>把本体染成这种物品的颜色。</summary>
    public void ApplyColor()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend != null) rend.material.color = GetTypeColor(fruitType);
    }

    private static GUIStyle labelStyle;

    void OnGUI()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(transform.position);
        if (screenPos.z <= 0f) return;                      // 在相机背面就不显示

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.fontStyle = FontStyle.Bold;
        }

        Color oldColor = GUI.contentColor;
        GUI.contentColor = GetTypeColor(fruitType);
        GUI.Label(new Rect(screenPos.x - 25f, Screen.height - screenPos.y - 42f, 50f, 20f),
                  fruitType.ToString(), labelStyle);
        GUI.contentColor = oldColor;
    }
}
