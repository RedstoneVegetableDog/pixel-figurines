using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 手办的「吸引力」加成。
    ///
    /// 规则（用户 2026-11-03 定）：**五档稀有度，每档最多只算 4 件**。
    /// ```
    /// 某档吸引力 = min(该档持有件数, 4) × 星级 × 5
    /// 总加成     = 五档相加（再把展示柜里摆的也算进对应档位，同样封顶 4）
    /// ```
    /// 也就是说：1★ 囤 10 件也只生效 4 件（+20），5★ 一样封顶 4 件（+100）。
    ///
    /// 覆盖原神 + 星铁 + 绝区零（统一走 `Defs.Find`）。
    /// </summary>
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.GetCurrentStoreAttractiveness))]
    internal static class FigurineAttractionPatch
    {
        /// <summary>每档最多生效的件数。</summary>
        public const int PerTierCap = 4;

        [HarmonyPostfix]
        private static void Postfix(PlayerStore __instance, ref int __result)
        {
            try
            {
                if (__instance == null) return;

                // GetCurrentStoreAttractiveness 会被 UI / 顾客生成频繁调用，
                // 而下面的 FindAllItem(true) 要遍历全部物品 —— 实测这会让打开界面时掉帧。
                // 这里做 0.25 秒缓存（吸引力变化是慢变量，0.25s 的滞后无感）。
                float now = Time.realtimeSinceStartup;
                if (now - _cacheAt < 0.25f)
                {
                    __result += _cacheBonus;
                    return;
                }
                _cacheAt = now;

                // 下标 1~5 = 档位；0 不用
                var count = new int[6];

                // ① 玩家持有的手办（按 identifier+位置去重，同款多件各算一件）
                try
                {
                    var seen = new HashSet<long>();
                    foreach (var it in __instance.FindAllItem(true))
                    {
                        if (it == null) continue;
                        string id = null;
                        try { id = it.identifier; } catch (System.Exception __ex) { Safe.Warn("FigurineAttractionPatch.cs:44", __ex); }
                        if (string.IsNullOrEmpty(id)) continue;

                        int px = 0, py = 0;
                        try { var ms = it.modifiedShape ?? it.shape; if (ms != null) { px = ms.minX; py = ms.minY; } } catch (System.Exception __ex) { Safe.Warn("FigurineAttractionPatch.cs:48", __ex); }
                        long key = ((long)id.GetHashCode() << 32) ^ (uint)(px * 1000 + py);
                        if (!seen.Add(key)) continue;

                        var def = Defs.Find(id);
                        if (string.IsNullOrEmpty(def.Id)) continue;
                        // 展示柜里的手办由下面 ② 单独统计；FindAllItem 会把容器内容也扫出来，
                        // 不跳过就会算两遍
                        if (IsInside(it, Cabinet.Id)) continue;
                        int s = Mathf.Clamp(def.Star, 1, 5);
                        count[s]++;
                    }
                }
                catch (System.Exception __ex) { Safe.Warn("FigurineAttractionPatch.cs:58", __ex); }

                // ② 展示柜里摆的（Cabinet 只存 id 列表）
                try
                {
                    foreach (var cid in Cabinet.Snapshot())
                    {
                        var def = Defs.Find(cid);
                        if (string.IsNullOrEmpty(def.Id)) continue;
                        int s = Mathf.Clamp(def.Star, 1, 5);
                        count[s]++;
                    }
                }
                catch (System.Exception __ex) { Safe.Warn("FigurineAttractionPatch.cs:71", __ex); }

                int bonus = 0;
                var detail = "";
                for (int t = 1; t <= 5; t++)
                {
                    int eff = Math.Min(count[t], PerTierCap);
                    if (eff <= 0) continue;
                    bonus += eff * t * 5;
                    detail += $" {Tier.Of(t)}={count[t]}件→生效{eff}";
                }

                _cacheBonus = bonus;
                __result += bonus;
                if (bonus != _lastBonus)
                {
                    _lastBonus = bonus;
                    Debug.Log(FigurineMod.LogTag + $"吸引力 +{bonus}（" + detail.Trim() + $"），合计 {__result}");
                }
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "吸引力补丁异常：" + e.GetType().Name); }
        }

        private static int _lastBonus = -1;
        private static int _cacheBonus;
        private static float _cacheAt = -99f;

        /// <summary>这件物品是不是在某个指定容器里（沿 parentInventory 往上找归属物）。</summary>
        private static bool IsInside(GameItem item, string containerId)
        {
            try
            {
                var node = item;
                for (int depth = 0; depth < 6; depth++)
                {
                    var parentInv = node != null ? node.parentInventory : null;
                    if (parentInv == null) return false;
                    var owner = GraphUtils.FindParentType<GameItem>(parentInv);
                    if (owner == null) return false;
                    if (owner.identifier == containerId) return true;
                    node = owner;
                }
            }
            catch (System.Exception __ex) { Safe.Warn("FigurineAttractionPatch.IsInside", __ex); }
            return false;
        }
    }
}
