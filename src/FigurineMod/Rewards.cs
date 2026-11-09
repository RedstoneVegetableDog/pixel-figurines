using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

#if !NO_FAKES
// 发布包（Release / NO_FAKES）整段不编译：连 Harmony 补丁都不挂

namespace FigurinePort
{
    /// <summary>
    /// 对质成功的购买优惠（旧版 FigurineConfront._rewards）：
    /// 只在该趟交易内有效，按物品位置键记录，买价打折 5~15%，交易结束清空。
    /// </summary>
    internal static class Rewards
    {
        private static readonly Dictionary<string, int> _rewards = new Dictionary<string, int>(StringComparer.Ordinal);

        private static string KeyOf(GameItem item)
        {
            try
            {
                if (item == null) return null;
                int x = 0, y = 0;
                var ms = item.modifiedShape ?? item.shape;
                if (ms != null) { x = ms.minX; y = ms.minY; }
                return item.identifier + ":" + x + "," + y;
            }
            catch { return null; }
        }

        public static void Record(GameItem item, int pct)
        {
            try
            {
                var k = KeyOf(item);
                if (!string.IsNullOrEmpty(k)) _rewards[k] = Mathf.Clamp(pct, 0, 95);
            }
            catch (System.Exception __ex) { Safe.Warn("Rewards.cs:39", __ex); }
        }

        public static int PercentOf(GameItem item)
        {
            try
            {
                var k = KeyOf(item);
                return !string.IsNullOrEmpty(k) && _rewards.TryGetValue(k, out var p) ? p : 0;
            }
            catch { return 0; }
        }

        public static void Clear()
        {
            try { _rewards.Clear(); } catch (System.Exception __ex) { Safe.Warn("Rewards.cs:54", __ex); }
        }

        public static int Count { get { return _rewards.Count; } }

        [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.GetStoreBuyPrice))]
        internal static class BuyPricePatch
        {
            [HarmonyPostfix]
            private static void Postfix(PlayerStore __instance, ref int __result)
            {
                try
                {
                    if (__instance == null || __result <= 0) return;
                    var it = __instance.currentNegociatedItem;
                    int pct = PercentOf(it);
                    if (pct > 0) __result = Math.Max(1, __result * (100 - pct) / 100);
                }
                catch (System.Exception __ex) { Safe.Warn("Rewards.cs:72", __ex); }
            }
        }
    }
}
#endif
