using System;
using System.Collections.Generic;

namespace FigurinePort
{
    /// <summary>
    /// 按「数量金字塔」重新分配五档稀有度（用户 2026-11-03 要求）。
    ///
    /// 规则：每个系列内部，**1★ 人数最多 → 5★ 人数最少**，比例固定：
    /// ```
    /// 1★ 30%  ／ 2★ 25% ／ 3★ 20% ／ 4★ 15% ／ 5★ 10%
    /// ```
    /// 分配方式：先把该系列按「价值/稀有度」从高到低排序，
    /// 排名最前面的 10% 给 5★，依次往下切。
    ///
    /// 各系列的排序依据：
    /// ```
    /// 原神      按 catalog 的 Value 降序（原作者定的价值序）
    /// 星铁      按原来的哈希星级降序（保持稳定，不随机）
    /// 绝区零    按 wiki 稀有度 S/A 降序，再按 id
    /// ```
    /// </summary>
    internal static class TierQuota
    {
        /// <summary>1★ → 5★ 的占比（和 = 1）。</summary>
        private static readonly double[] Ratio = { 0.30, 0.25, 0.20, 0.15, 0.10 };

        /// <summary>
        /// 给定总人数，返回「按排名从上到下」的星级数组：
        /// 下标 0 是排名最高（5★），最后一个是 1★。
        /// </summary>
        public static int[] StarsTopDown(int total)
        {
            if (total <= 0) return new int[0];

            // 先按比例取整，再修掉余数（保证合计 = total，且每档至少 1 人）
            var counts = new int[5];   // 0 → 1★, 4 → 5★
            int assigned = 0;
            for (int t = 0; t < 5; t++)
            {
                counts[t] = (int)Math.Round(total * Ratio[t]);
                if (counts[t] < 1) counts[t] = 1;
                assigned += counts[t];
            }
            // 调整到刚好等于 total：从 1★（最容易多）开始削/补
            while (assigned > total)
            {
                int idx = -1;
                for (int t = 0; t < 5; t++) if (counts[t] > 1 && (idx < 0 || counts[t] > counts[idx])) idx = t;
                if (idx < 0) break;
                counts[idx]--; assigned--;
            }
            while (assigned < total)
            {
                int idx = 0;
                for (int t = 1; t < 5; t++) if (counts[t] < counts[idx]) idx = t;
                counts[idx]++; assigned++;
            }

            // 从上往下：先 5★（人数最少），再 4★ … 最后 1★
            var stars = new int[total];
            int pos = 0;
            for (int star = 5; star >= 1; star--)
            {
                int n = counts[star - 1];
                for (int k = 0; k < n && pos < total; k++) stars[pos++] = star;
            }
            // 万一还有余位
            while (pos < total) stars[pos++] = 1;
            return stars;
        }
        /// <summary>按排名从上到下给星级（下标 0 = 最高）。</summary>
        public static void ApplyGenshin()
        {
            var arr = Catalog.All;
            if (arr == null || arr.Length == 0) return;
            var order = new List<int>();
            for (int i = 0; i < arr.Length; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                int c = arr[b].Value.CompareTo(arr[a].Value);   // 价值高的排前面
                if (c != 0) return c;
                return string.CompareOrdinal(arr[a].Id, arr[b].Id);
            });
            var stars = StarsTopDown(arr.Length);
            for (int k = 0; k < order.Count && k < stars.Length; k++) arr[order[k]].Star = stars[k];

            var dist = new int[6];
            foreach (var d in arr) dist[Mathf_Clamp(d.Star)]++;
            UnityEngine.Debug.Log(FigurineMod.LogTag + $"原神重分档（金字塔）：1★{dist[1]} 2★{dist[2]} 3★{dist[3]} 4★{dist[4]} 5★{dist[5]}");
        }

        private static int Mathf_Clamp(int s) { return s < 1 ? 1 : (s > 5 ? 5 : s); }
    }
}
