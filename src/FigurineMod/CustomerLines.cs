using System;
using System.Collections.Generic;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 顾客台词池 —— 数据按 **0.8.0-beta** 的 `CustomerLines` 原样还原（3 位顾客 × 16 个池，561 条）。
    /// 数据本体在自动生成的 `CustomerLinesData.g.cs` 里，这里只保留结构与取用 API。
    /// </summary>
    internal static class CustomerLines
    {
        public sealed class Pool
        {
            public string[] BuyMain;
            public string[] BuyDeal;
            public string[] BuyDone;
            public string[] BuyRepeat;
            public string[] BuyWrongPlace;
            public string[] BuyRightPlace;
            public string[] SellMain;
            public string[] SellDeal;
            public string[] SellDone;
            public string[] SellRepeat;
            public string[] SellExpensiveMoan;
            // ── 买卖都做（SELLNBUY）专用池：先卖再买，口气介于两者之间 ──
            public string[] BothMain;
            public string[] BothDeal;
            public string[] BothDone;
            public string[] BothRepeat;
            public string[] BothWrongPlace;
            public string[] BothRightPlace;
            public string[] BothExpensiveMoan;
            public string[] Interrogate;
            public string[] Glass;
            public string[] DealLast;
            public string[] PostArrest;
            public string[][] Bargain;
        }

        private static readonly Dictionary<string, Pool> _pools = new Dictionary<string, Pool>(StringComparer.Ordinal);
        private static bool _ready;

        /// <summary>登记一位顾客的台词池（海子这种只有少量原文的，用代码构造）。</summary>
        public static void Register(string identifier, Pool pool)
        {
            if (string.IsNullOrEmpty(identifier) || pool == null) return;
            EnsureBuilt();
            _pools[identifier] = pool;
        }

        public static Pool For(string identifier)
        {
            EnsureBuilt();
            Pool p;
            return _pools.TryGetValue(identifier ?? string.Empty, out p) ? p : null;
        }

        public static string Pick(string[] arr)
        {
            if (arr == null || arr.Length == 0) return null;
            return arr[UnityEngine.Random.Range(0, arr.Length)];
        }

        /// <summary>用某位顾客的人设台词覆盖它的池（保留原有 id 与结构）。</summary>
        private static void Override(string id, Action<Pool> fill, string label)
        {
            try
            {
                Pool p;
                if (!_pools.TryGetValue(id, out p) || p == null)
                {
                    p = new Pool();
                    _pools[id] = p;
                }
                fill(p);
                Debug.Log(FigurineMod.LogTag + "台词池已按人设覆盖：" + label);
            }
            catch (Exception e) { Safe.Warn("CustomerLines.Override:" + label, e); }
        }
        private static void EnsureBuilt()
        {
            if (_ready) return;
            _ready = true;
            try
            {
                CustomerLinesData.Fill(_pools);
                Debug.Log(FigurineMod.LogTag + "顾客台词池已载入（0.8.0 原版）：" + _pools.Count + " 位");

                // 用按人设撰写的台词覆盖这三位（数据在 CustomerLinesXxx.g.cs）
                Override("figurine_enthusiast_tourist", CustomerLinesMira.Fill, "米拉");
                Override("figurine_collector_upper_sell", CustomerLinesBailu.Fill, "白鹭");
                Override("figurine_pickpocket_1", CustomerLinesQueer.Fill, "雀儿");
            }
            catch (Exception e)
            {
                Debug.LogWarning(FigurineMod.LogTag + "顾客台词池载入失败：" + e.GetType().Name);
            }
        }
    }
}
