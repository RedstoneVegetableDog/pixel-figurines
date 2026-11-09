using System;
using HarmonyLib;

namespace FigurinePort
{
    /// <summary>
    /// 手办价格按「三态」折算（用户 2026-11-04 定）：
    ///   标正品 100% / 标赝品 60% / 未鉴定 80%；鉴定后按真身（真 100% / 假 60%）。
    /// 只对手办生效，其它物品原样返回。
    /// </summary>
    [HarmonyPatch(typeof(GameItem), nameof(GameItem.GetCurrentValue))]
    internal static class FigurinePricingPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GameItem __instance, ref long __result)
        {
            try
            {
                if (__instance == null || __result <= 0) return;
                int pct = FigurineAuth.PricePercent(__instance);
                if (pct < 0 || pct == 100) return;
                __result = __result * pct / 100;
            }
            catch (System.Exception __ex) { Safe.Warn("FigurinePricing.cs:21", __ex); }
        }
    }
}
