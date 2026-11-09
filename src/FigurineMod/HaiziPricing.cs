using System;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 海子「收价加成」：到店时按档位掷一次，卖货时按星级取用（照 0.8.0 的 `_bonusLo/_bonusHi`）。
    ///
    /// 数值范围（0.8.0 原值）：
    /// ```text
    /// 1★ 1~3%   2★ 4~6%   3★ 7~9%   4★ 10~12%   5★ 13~15%
    /// ```
    /// 由 `Haizi` 在到店流程里调用 `Roll()` 生效，直到下一次到店重掷。
    /// </summary>
    internal static class HaiziPricing
    {
        private static readonly float[] Lo = { 1f, 4f, 7f, 10f, 13f };
        private static readonly float[] Hi = { 3f, 6f, 9f, 12f, 15f };
        private static readonly float[] Bonus = new float[5];

        /// <summary>当前窗口顾客是不是海子。</summary>
        public static bool IsCurrent()
        {
            try
            {
                var ps = PlayerStore.Instance;
                var inst = ps != null ? ps.currentClientInstance : null;
                var bp = inst != null ? inst.GetClientBlueprint() : null;
                return bp != null && bp.identifier == Haizi.ClientId;
            }
            catch { return false; }
        }

        /// <summary>到店时重掷一轮加成。</summary>
        public static void Roll()
        {
            try
            {
                for (int i = 0; i < 5; i++) Bonus[i] = UnityEngine.Random.Range(Lo[i], Hi[i]);
                Debug.Log(FigurineMod.LogTag + "海子本次收价加成：1★+" + Bonus[0].ToString("0.0") + "% 5★+" + Bonus[4].ToString("0.0") + "%");
            }
            catch (Exception e) { Safe.Warn("HaiziPricing.Roll", e); }
        }

        /// <summary>该手办本次能多卖多少（百分比）。未知 id 返回 0。</summary>
        public static float BonusFor(string itemId)
        {
            try
            {
                var dd = Defs.Find(itemId);
                int star = string.IsNullOrEmpty(dd.Id) ? 0 : dd.Star;
                if (star <= 0) return 0f;
                return Bonus[Math.Min(4, star - 1)];
            }
            catch { return 0f; }
        }
    }
}
