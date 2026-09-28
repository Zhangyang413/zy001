using System;
using UnityEngine;

/// <summary>
/// 人物在一条网格线上的移动控制。
///
/// 规则：
///   · 每按一次 ←/→ 或 A/D 只走一格（cellStep = 一格的世界长度），人物始终吸附在格子中心；
///   · 不能走出 [minIndex, maxIndex]，撞到边界不消耗步数，只回调 onBlocked 给出提示；
///   · 每走一格消耗 1 步，已用步数达到允许步数（cnt）后不再响应输入；
///   · 移动成功后回调 onMoved(当前格下标, 已用步数)，由 createfruit 负责捡物品与判定胜负。
///
/// 所有参数由 createfruit 在生成人物时通过 Configure() 写入。
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("由 createfruit 运行时配置")]
    [Tooltip("允许的步数（沿用原 move 脚本的字段名，可直接使用预制体上已有的序列化值）")]
    public int cnt = 15;

    [Tooltip("格子间距，也就是每一步走的距离")]
    public float cellStep = 1f;

    [Tooltip("可以站立的格子下标范围")]
    public int minIndex = 0;
    public int maxIndex = 9;

    [Tooltip("人物当前所在的格子下标")]
    public int currentIndex = 0;

    [Tooltip("已经用掉的步数")]
    public int usedSteps = 0;

    [Tooltip("是否还能移动（游戏判定结束后会被关掉）")]
    public bool canMove = true;

    private float originX;                      // 第 0 格的世界 x 坐标（整行居中的基准）
    private Action<int, int> onMoved;           // 移动成功： (格子下标, 已用步数)
    private Action<int> onBlocked;              // 越界： (本来想去的格子下标)

    /// <summary>由 createfruit 在生成人物时调用，一次性写入全部参数。</summary>
    public void Configure(int startIndex, int minIndex, int maxIndex, float cellStep, float originX,
                          int allowedSteps, Action<int, int> onMoved, Action<int> onBlocked)
    {
        this.minIndex = minIndex;
        this.maxIndex = maxIndex;
        this.cellStep = Mathf.Max(0.01f, cellStep);
        this.originX = originX;
        this.onMoved = onMoved;
        this.onBlocked = onBlocked;

        currentIndex = Mathf.Clamp(startIndex, minIndex, maxIndex);
        cnt = allowedSteps;
        usedSteps = 0;
        canMove = true;
        ApplyPosition();
    }

    void Update()
    {
        if (!canMove) return;
        if (usedSteps >= cnt) return;                       // 步数已经用完，不能再走

        int dir = 0;
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) dir = -1;
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) dir = 1;
        if (dir == 0) return;

        int target = currentIndex + dir;
        if (target < minIndex || target > maxIndex)
        {
            if (onBlocked != null) onBlocked(target);       // 撞到边界：不消耗步数
            return;
        }

        currentIndex = target;
        usedSteps++;
        ApplyPosition();

        if (onMoved != null) onMoved(currentIndex, usedSteps);
    }

    /// <summary>把人物吸附到当前格的中心（x 由格子下标算出来，y/z 保持不变）。</summary>
    private void ApplyPosition()
    {
        Vector3 p = transform.position;
        p.x = originX + currentIndex * cellStep;
        transform.position = p;
    }
}
