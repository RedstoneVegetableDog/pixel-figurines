using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 拒收拦截（旧版 VetoStolenSale 简化）：顾客不收赃物/赝品/同款重复时，
    /// 把 CanSellThisItem 从 true 改成 false，并记录对应拒收台词。
    /// </summary>
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.CanSellThisItem))]
    internal static class RefusalVeto
    {
        private static string _lastKey;

        [HarmonyPostfix]
        private static void Postfix(PlayerStore __instance, GameItem __0, ref bool __result)
        {
            try
            {
                if (!__result || __instance == null || __0 == null) return;
                if (!IsOurFigurine(__0.identifier)) return;

                var inst = __instance.currentClientInstance;
                if (inst == null) return;
                var bp = inst.GetClientBlueprint();
                if (bp == null) return;

                string cid = null;
                try { cid = bp.identifier; } catch (System.Exception __ex) { Safe.Warn("RefusalVeto.cs:31", __ex); }
                if (cid == Haizi.ClientId)
                {
                    // 海子（买卖都做）：只收干净手办 —— 赃物/赝品拒收，其余放行。
                    // 以前是无条件 false（整条买侧被掐掉），改 SELLNBUY 后必须放行干净货，
                    // 否则他的「买方」一半永远什么都买不了。
                    try
                    {
                        bool bad = false;
                        try { if (StolenHelper.IsStolenItem(__0)) bad = true; } catch (System.Exception __ex) { Safe.Warn("RefusalVeto.HaiziStolen", __ex); }
                        try { if (!bad && FigurineAuth.EffectiveFake(__0)) bad = true; } catch (System.Exception __ex) { Safe.Warn("RefusalVeto.HaiziFake", __ex); }
                        if (bad) __result = false;
                    }
                    catch (System.Exception __ex) { Safe.Warn("RefusalVeto.Haizi", __ex); }
                    return;
                }
                if (cid != CustomerMira.ClientId && cid != CustomerSellers.BailuId && cid != CustomerSellers.QueerId) return;

                var hits = new List<FigurineRefusals.Reason>();
                try { if (bp.isRequireVariety && bp.IsItemInVarietyList(__0)) hits.Add(FigurineRefusals.Reason.Duplicate); } catch (System.Exception __ex) { Safe.Warn("RefusalVeto.cs:40", __ex); }
                try { if (StolenHelper.IsStolenItem(__0)) hits.Add(FigurineRefusals.Reason.Stolen); } catch (System.Exception __ex) { Safe.Warn("RefusalVeto.cs:41", __ex); }
                // 拒收按「面向系统的真假」= 已鉴定看真身 / 未鉴定看标签（避免提前泄露）
                try { if (FigurineAuth.EffectiveFake(__0)) hits.Add(FigurineRefusals.Reason.Fake); } catch (System.Exception __ex) { Safe.Warn("RefusalVeto.cs:43", __ex); }

                if (hits.Count == 0) return;

                string key = cid + "|" + __0.identifier + "|" + string.Join(",", hits.ConvertAll(h => h.ToString()).ToArray());
                if (key != _lastKey)
                {
                    _lastKey = key;
                    FigurineRefusals.Reason why;
                    string line = FigurineRefusals.Pick(cid, hits, out why);
                    Debug.Log(FigurineMod.LogTag + "拒收 " + __0.identifier + "（" + why + "）：" + line);
                    try
                    {
                        var dlg = new Dialogue();
                        dlg.SetText(bp.displayName, line);
                        bp.placedWrongItemWhenSellingToDialogue = dlg;
                    }
                    catch (System.Exception __ex) { Safe.Warn("RefusalVeto.cs:60", __ex); }
                }
                __result = false;
            }
            catch (System.Exception __ex) { Safe.Warn("RefusalVeto.cs:64", __ex); }
        }

        private static bool IsOurFigurine(string id)
        {
            // 三 IP 统一：原神 figurine_gs_ / 星铁 figurine_sr_ / 绝区零 figurine_zzz_
            // （旧判断只放行 figurine_gs_ → 星铁/绝区零的赝品/赃物以前不会被拒收）
            if (string.IsNullOrEmpty(id) || !id.StartsWith("figurine_", StringComparison.Ordinal)) return false;
            return !string.IsNullOrEmpty(Defs.Find(id).Id);
        }
    }

    /// <summary>
    /// 海子「只收干净货」的**界面侧**同步：赃物/赝品在面板里直接显示成「不感兴趣」，
    /// 不显示价格（仅靠 CanSellThisItem 否决的话，列表里仍有价格、点了却没反应）。
    /// 其他三位顾客保持原版流程（点/摆错时出拒收台词）。
    ///
    /// ⚠️ `IsClientBuyingThisItem` 有两个重载 `(GameItem)` / `(GameItem, bool)`：
    /// 不写参数类型时 Harmony 用 `AccessTools.Method(type, name)` 会抛
    /// `AmbiguousMatchException` → 整个模组加载失败（v1.0.3 初版就是这个 bug）。
    /// </summary>
    [HarmonyPatch(typeof(StoreClient), nameof(StoreClient.IsClientBuyingThisItem), new Type[] { typeof(GameItem) })]
    internal static class HaiziRefusalUiPatch
    {
        [HarmonyPostfix]
        private static void Postfix(StoreClient __instance, GameItem __0, ref bool __result)
        {
            try
            {
                if (!__result || __instance == null || __0 == null) return;
                if (__instance.identifier != Haizi.ClientId) return;
                bool bad = false;
                try { if (StolenHelper.IsStolenItem(__0)) bad = true; } catch (System.Exception __ex) { Safe.Warn("HaiziRefusalUi.Stolen", __ex); }
                try { if (!bad && FigurineAuth.EffectiveFake(__0)) bad = true; } catch (System.Exception __ex) { Safe.Warn("HaiziRefusalUi.Fake", __ex); }
                if (bad) __result = false;
            }
            catch (System.Exception __ex) { Safe.Warn("HaiziRefusalUiPatch", __ex); }
        }
    }
}
