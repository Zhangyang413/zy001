using System;
using System.Collections.Generic;

/// <summary>
/// 一行格子上的“集齐所有种类”最少步数求解器。
///
/// 问题模型：
///   types[i] 表示第 i 格上物品的种类；人物一开始站在第 start 格（该格的种类视为已获得）；
///   每走一格消耗 1 步；问至少多少步才能把数组中出现的所有种类各拿到至少一个。
///
/// 解题思路：
///   走动范围一定是一段包含 start 的区间 [start - L, start + R]，区间内的种类都会被捡到，
///   所以只需让“区间内出现的种类集合”等于全部种类，并让步数最少。代价只有几种：
///       · 先左后右：2L + R            · 先右后左：L + 2R
///       · 只往一边走（不需要回头）：L 或 R
///   左侧“首次出现”的记录按距离递增排列，只有跨过某个首次出现位置才会让覆盖到的种类变多，
///   因此枚举 leftRecords[minLeftIndex..lastLeftIndex] 与 rightRecords[minRightIndex..lastRightIndex]
///   即可得到全部候选解。
///
/// 说明：本类是纯 C# 实现（不引用 UnityEngine），可脱离编辑器单独做单元测试。
/// </summary>
public static class MinMovesSolver
{
    /// <summary>求解结果：一条“最优走法”的描述。</summary>
    public struct MovePlan
    {
        /// <summary>是否求得可行解。</summary>
        public bool Success;
        /// <summary>最少步数。</summary>
        public int Steps;
        /// <summary>向左最远走几格（0 表示不用往左）。</summary>
        public int LeftSteps;
        /// <summary>向右最远走几格（0 表示不用往右）。</summary>
        public int RightSteps;
        /// <summary>true = 先向左再向右（2L+R）；false = 先向右再向左（L+2R）。</summary>
        public bool LeftFirst;
        /// <summary>不可解时的原因；可解时为 null。</summary>
        public string FailureReason;
        /// <summary>不影响结果的提示（例如 m 传得和实际种类数不一致）；没有时为 null。</summary>
        public string Notice;
    }

    /// <summary>“某个种类第一次被走到的距离”。</summary>
    private struct FirstSeen
    {
        public int Distance;
        public int Type;

        public FirstSeen(int distance, int type)
        {
            Distance = distance;
            Type = type;
        }
    }

    /// <summary>一种走法（按两种先后顺序之一枚举出来的候选项）。</summary>
    private struct Candidate
    {
        public bool Valid;
        public int Steps;
        public int LeftSteps;
        public int RightSteps;
    }

    /// <summary>
    /// 求最少步数，并给出可复盘的走法。
    /// </summary>
    /// <param name="types">每格物品的种类，下标即格子下标。</param>
    /// <param name="start">人物起始格子下标。</param>
    /// <param name="requiredTypes">
    /// 需要集齐的种类数，应与 types 中的不同种类数一致；传 0 或负数表示按数组里实际出现的种类自动计算。
    /// </param>
    public static MovePlan Solve(int[] types, int start, int requiredTypes)
    {
        MovePlan plan = new MovePlan();
        plan.LeftFirst = true;

        if (types == null || types.Length == 0)
        {
            plan.FailureReason = "关卡数据 types 为空。";
            return plan;
        }
        if (start < 0 || start >= types.Length)
        {
            plan.FailureReason = string.Format("起点下标 {0} 超出范围 [0, {1}]。", start, types.Length - 1);
            return plan;
        }

        // 统计真正的种类数：m 只用于校验（原实现里 m 除了 m<=1 的早退之外也没有参与计算）。
        HashSet<int> allTypes = new HashSet<int>();
        for (int i = 0; i < types.Length; i++)
        {
            allTypes.Add(types[i]);
        }
        int distinctTypes = allTypes.Count;

        if (requiredTypes > distinctTypes)
        {
            plan.FailureReason = string.Format("要求集齐 {0} 种物品，但本关只出现了 {1} 种，无法完成。",
                                               requiredTypes, distinctTypes);
            return plan;
        }
        if (requiredTypes > 0 && requiredTypes < distinctTypes)
        {
            plan.Notice = string.Format("参数 m={0} 小于本关实际种类数 {1}，本算法按“集齐全部种类”计算（m 仅作校验）。",
                                        requiredTypes, distinctTypes);
        }

        // 记录左右两侧每个种类“第一次出现”的距离（距离递增，天然有序）
        List<FirstSeen> leftRecords = new List<FirstSeen>();
        List<FirstSeen> rightRecords = new List<FirstSeen>();
        HashSet<int> leftTypes = new HashSet<int>();
        HashSet<int> rightTypes = new HashSet<int>();

        for (int p = start; p >= 0; p--)
        {
            if (leftTypes.Add(types[p]))            // Add 返回 true 说明是新种类
            {
                leftRecords.Add(new FirstSeen(start - p, types[p]));
            }
        }
        for (int p = start; p < types.Length; p++)
        {
            if (rightTypes.Add(types[p]))
            {
                rightRecords.Add(new FirstSeen(p - start, types[p]));
            }
        }

        int lastLeftIndex = leftRecords.Count - 1;
        int lastRightIndex = rightRecords.Count - 1;

        // 只在左侧出现的种类，决定了“至少要往左走到哪”（往右走再远也拿不到）
        int minLeftIndex = lastLeftIndex;
        while (minLeftIndex > 0 && rightTypes.Contains(leftRecords[minLeftIndex].Type))
        {
            --minLeftIndex;
        }

        // 只在右侧出现的种类，决定了“至少要往右走到哪”
        int minRightIndex = lastRightIndex;
        while (minRightIndex > 0 && leftTypes.Contains(rightRecords[minRightIndex].Type))
        {
            --minRightIndex;
        }

        Candidate leftFirst = SearchLeftFirst(minLeftIndex, lastLeftIndex, lastRightIndex,
                                              leftRecords, rightRecords, rightTypes);
        Candidate rightFirst = SearchRightFirst(minRightIndex, lastRightIndex, lastLeftIndex,
                                                leftRecords, rightRecords, leftTypes);

        // 两种走法里步数更少的就是答案（步数相同时偏向“先左后右”，保证结果稳定）
        bool useLeftFirst = !rightFirst.Valid || (leftFirst.Valid && leftFirst.Steps <= rightFirst.Steps);
        Candidate best = useLeftFirst ? leftFirst : rightFirst;

        plan.Success = true;
        plan.Steps = best.Steps;
        plan.LeftSteps = best.LeftSteps;
        plan.RightSteps = best.RightSteps;
        plan.LeftFirst = useLeftFirst;
        return plan;
    }

    /// <summary>
    /// 兼容旧调用：返回最少步数；无解时返回 -1（旧实现遇到这种输入会给出错误结果）。
    /// </summary>
    public static int MinMovesToCollectM(int[] nums, int start, int m)
    {
        MovePlan plan = Solve(nums, start, m);
        return plan.Success ? plan.Steps : -1;
    }

    /// <summary>
    /// 枚举“先左后右”的走法：左侧走到第 i 个首次出现位置，
    /// 右侧只需要走到“剩余种类”里最靠右的那个首次出现位置。
    /// </summary>
    private static Candidate SearchLeftFirst(int minLeftIndex, int lastLeftIndex, int lastRightIndex,
                                             List<FirstSeen> leftRecords, List<FirstSeen> rightRecords,
                                             HashSet<int> rightTypes)
    {
        Candidate best = new Candidate();

        // needed：还需要被覆盖的种类（初始为右侧全部种类，减去左侧 minLeftIndex 之前已能覆盖的）
        HashSet<int> needed = new HashSet<int>(rightTypes);
        for (int i = 0; i < minLeftIndex; i++)
        {
            needed.Remove(leftRecords[i].Type);
        }

        int j = lastRightIndex;     // 剩余种类在右侧最靠右的首次出现下标（随 i 增大只减不增）
        for (int i = minLeftIndex; i <= lastLeftIndex; i++)
        {
            needed.Remove(leftRecords[i].Type);

            while (j >= 0 && !needed.Contains(rightRecords[j].Type))
            {
                --j;
            }

            int left = leftRecords[i].Distance;
            int right;
            int steps;
            if (j >= 0)
            {
                right = rightRecords[j].Distance;
                steps = 2 * left + right;       // 先左后右：左边来回 + 一路向右
            }
            else
            {
                right = 0;
                steps = left;                   // 右侧不用再走，往左走到位就结束，不必回头
            }

            if (!best.Valid || steps < best.Steps)
            {
                best.Valid = true;
                best.Steps = steps;
                best.LeftSteps = left;
                best.RightSteps = right;
            }
        }

        return best;
    }

    /// <summary>
    /// 枚举“先右后左”的走法：右侧走到第 j 个首次出现位置，
    /// 左侧只需要走到“剩余种类”里最远（距离最大）的那个首次出现位置。
    /// </summary>
    private static Candidate SearchRightFirst(int minRightIndex, int lastRightIndex, int lastLeftIndex,
                                              List<FirstSeen> leftRecords, List<FirstSeen> rightRecords,
                                              HashSet<int> leftTypes)
    {
        Candidate best = new Candidate();

        HashSet<int> needed = new HashSet<int>(leftTypes);
        for (int i = 0; i < minRightIndex; i++)
        {
            needed.Remove(rightRecords[i].Type);
        }

        int i2 = lastLeftIndex;     // 剩余种类在左侧最远的首次出现下标（随 j 增大只减不增）
        for (int j = minRightIndex; j <= lastRightIndex; j++)
        {
            needed.Remove(rightRecords[j].Type);

            while (i2 >= 0 && !needed.Contains(leftRecords[i2].Type))
            {
                --i2;
            }

            int right = rightRecords[j].Distance;
            int left;
            int steps;
            if (i2 >= 0)
            {
                left = leftRecords[i2].Distance;
                steps = 2 * right + left;       // 先右后左：右边来回 + 一路向左
            }
            else
            {
                left = 0;
                steps = right;                  // 左侧不用再走，往右走到位就结束
            }

            if (!best.Valid || steps < best.Steps)
            {
                best.Valid = true;
                best.Steps = steps;
                best.LeftSteps = left;
                best.RightSteps = right;
            }
        }

        return best;
    }
}
