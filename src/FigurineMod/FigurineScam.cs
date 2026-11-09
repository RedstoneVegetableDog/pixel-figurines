using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 「假货冒充正品」被识破（用户 2026-11-04 定）：
    /// 真身赝品、标签写成正品的手办卖出去时，**35%** 概率被顾客识破，
    /// 成交价按正品价的 **50%~65%** 算（随机），其余按标签价正常成交。
    ///
    /// 判定结果按 `uniqueId` 缓存，保证「列表里显示的价格」和「实际结算的价格」一致，
    /// 不会每刷新一次界面就重掷一次。
    /// </summary>
    internal static class FigurineScam
    {
        public const int CaughtChancePercent = 35;
        public const float MinFactor = 0.50f;   // 50%
        public const float MaxFactor = 0.65f;   // 65%

        private static readonly Dictionary<int, float> _factor = new Dictionary<int, float>();

        /// <summary>这件货是不是「真身赝品、标签正品」。</summary>
        public static bool IsScam(GameItem item)
        {
            try
            {
                if (item == null) return false;
                if (!FigurineAuth.IsFigurine(item)) return false;
                if (FigurineAuth.IsUnappraised(item)) return false;
                return FigurineAuth.TruthFake(item) && !FigurineAuth.LabelFake(item);
            }
            catch { return false; }
        }

        /// <summary>取（必要时掷一次）这件货的成交系数：1 = 没被识破，0.5~0.65 = 被识破。</summary>
        public static float FactorOf(GameItem item)
        {
            try
            {
                if (!IsScam(item)) return 1f;
                // 只在**玩家卖出**时生效：顾客摆在柜台的货不套这个折扣，否则玩家买货会白拿便宜
                bool owned;
                try { owned = GeneralHelper.IsItemOwned(item); } catch { owned = false; }
                if (!owned) return 1f;

                int uid = Key(item);
                float f;
                if (_factor.TryGetValue(uid, out f)) return f;

                bool caught = UnityEngine.Random.Range(0, 100) < CaughtChancePercent;
                f = caught ? UnityEngine.Random.Range(MinFactor, MaxFactor) : 1f;
                // 缓存上限：卖掉的物品不会再来清，防长期存档里越积越多
                if (_factor.Count > 256) _factor.Clear();
                _factor[uid] = f;
                if (caught)
                    Debug.Log(FigurineMod.LogTag + "假货被识破：" + item.identifier + " 成交价按 "
                        + Mathf.RoundToInt(f * 100f) + "% 算");
                return f;
            }
            catch (Exception __ex) { Safe.Warn("FigurineScam.FactorOf", __ex); return 1f; }
        }

        /// <summary>缓存 key：优先 uniqueId；没有唯一 id 的物品用运行时哈希，避免全被当成同一件。</summary>
        private static int Key(GameItem item)
        {
            try
            {
                int uid = item.uniqueId;
                if (uid != 0) return uid;
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item);
            }
            catch { return 0; }
        }

        /// <summary>标签/真身变了就清掉旧判定，下次重新掷。</summary>
        public static void Clear(GameItem item)
        {
            try { _factor.Remove(Key(item)); }
            catch (System.Exception __ex) { Safe.Warn("FigurineScam.Clear", __ex); }
        }
    }

    /// <summary>把识破后的折扣挂到「议价成交价」上（列表显示与结算用的是同一个值）。</summary>
    [HarmonyPatch(typeof(GameItem), nameof(GameItem.GetNegociatedValue))]
    internal static class FigurineScamPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GameItem __instance, ref long __result)
        {
            try
            {
                if (__instance == null || __result <= 0) return;
                float f = FigurineScam.FactorOf(__instance);
                if (f >= 0.999f) return;
                __result = (long)(__result * f);
            }
            catch (Exception __ex) { Safe.Warn("FigurineScam.Patch", __ex); }
        }
    }
}
