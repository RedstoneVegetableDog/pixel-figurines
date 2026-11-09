using System;
using HarmonyLib;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 0.8.0-beta 海子补货：玩家把柜台上海子的盲盒/展示柜**买走**时登记，海子还在窗口就补回。
    ///
    /// 注意方向：玩家买走顾客的货走的是 `PlayerStore.OnItemBought` → `StoreClient.OnItemSold`
    /// （顾客「卖出」）；以前这里 patch 的是 `StoreClient.OnItemBought`（顾客买玩家的货），
    /// 方向反了 → 补货永远不会触发。
    /// </summary>
    [HarmonyPatch(typeof(StoreClient), nameof(StoreClient.OnItemSold))]
    internal static class HaiziOnSoldPatch
    {
        [HarmonyPostfix]
        private static void Postfix(StoreClient __instance, GameItem __0)
        {
            try
            {
                if (__instance == null || __0 == null) return;
                if (__instance.identifier != Haizi.ClientId) return;
                HaiziRestock.Register(__0.identifier);
            }
            catch (System.Exception __ex) { Safe.Warn("HaiziPatches.OnSold", __ex); }
        }
    }

    /// <summary>
    /// 0.8.0-beta 海子「收价加成」：当前顾客是海子、且这件手办是**玩家卖给他**时，
    /// 议价价值按本次到店掷的稀有度加成上浮（1★+1~3% … 5★+13~15%）。
    /// HaiziPricing.Roll() 在到店时已掷好；以前 BonusFor() 是死代码，这里接上。
    /// </summary>
    [HarmonyPatch(typeof(GameItem), nameof(GameItem.GetNegociatedValue))]
    internal static class HaiziPriceBonusPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GameItem __instance, ref long __result)
        {
            try
            {
                if (__instance == null || __result <= 0) return;
                if (!HaiziPricing.IsCurrent()) return;
                // 只加成「玩家 → 海子」方向：他摆出来的货（非玩家拥有）不加
                bool owned;
                try { owned = GeneralHelper.IsItemOwned(__instance); } catch { owned = false; }
                if (!owned) return;
                float bonus = HaiziPricing.BonusFor(__instance.identifier);
                if (bonus <= 0f) return;
                __result = (long)System.Math.Round(__result * (1.0 + bonus / 100.0));
            }
            catch (System.Exception __ex) { Safe.Warn("HaiziPatches.PriceBonus", __ex); }
        }
    }
}
