using System;
using System.Collections.Generic;

/// <summary>
/// 随机关卡生成器（纯 C#，不依赖 UnityEngine，便于单独测试）。
///
/// 生成的关卡保证：
///   · 格子（物品）数量落在 [minCells, maxCells] 区间内；
///   · 每种物品都至少出现一次（否则永远集不齐）；
///   · 人物的初始格子下标随机，但一定在格子范围内；
///   · 物品位置与人物位置都是随机的。
/// </summary>
public static class LevelGenerator
{
    /// <summary>一关的完整描述。</summary>
    public struct LevelDefinition
    {
        public int[] Types;         // 每格物品的种类，下标即格子下标
        public int StartIndex;      // 人物初始格子下标
        public int TypeCount;       // 本关物品种类数
        public int Seed;            // 生成时使用的随机种子（方便复现同一关）

        public int CellCount
        {
            get { return Types == null ? 0 : Types.Length; }
        }
    }

    /// <summary>用随机种子生成一关。</summary>
    public static LevelDefinition Generate(int minCells, int maxCells, int typeCount)
    {
        int seed = unchecked(Environment.TickCount ^ (int)(DateTime.Now.Ticks & 0x7fffffff));
        return Generate(minCells, maxCells, typeCount, seed);
    }

    /// <summary>用指定种子生成一关（同一个种子必定得到同一关）。</summary>
    public static LevelDefinition Generate(int minCells, int maxCells, int typeCount, int seed)
    {
        return GenerateInternal(minCells, maxCells, typeCount, seed);
    }

    private static LevelDefinition GenerateInternal(int minCells, int maxCells, int typeCount, int seed)
    {
        Random rng = new Random(seed);

        if (minCells < 1) minCells = 1;
        if (maxCells < minCells) maxCells = minCells;
        if (typeCount < 1) typeCount = 1;

        int cells = rng.Next(minCells, maxCells + 1);
        if (typeCount > cells) typeCount = cells;          // 种类数不会超过格子数

        // 先把每种各放一个，保证“每种都至少有一个”，剩下的格子随机填
        List<int> pool = new List<int>();
        for (int t = 1; t <= typeCount; t++)
        {
            pool.Add(t);
        }
        while (pool.Count < cells)
        {
            pool.Add(rng.Next(1, typeCount + 1));
        }
        Shuffle(pool, rng);

        LevelDefinition level = new LevelDefinition();
        level.Types = pool.ToArray();
        level.StartIndex = rng.Next(0, cells);
        level.TypeCount = typeCount;
        level.Seed = seed;
        return level;
    }

    /// <summary>Fisher-Yates 洗牌。</summary>
    private static void Shuffle(List<int> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    /// <summary>统计一个关卡数组里的不同种类数。</summary>
    public static int CountTypes(int[] types)
    {
        if (types == null) return 0;

        HashSet<int> set = new HashSet<int>();
        for (int i = 0; i < types.Length; i++)
        {
            set.Add(types[i]);
        }
        return set.Count;
    }
}
