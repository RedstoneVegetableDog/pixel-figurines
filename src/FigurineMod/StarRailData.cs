using System;
using System.Collections.Generic;

namespace FigurinePort
{
    /// <summary>
    /// 星穹铁道手办数据（**93 位**，2026-11-03 按 `E:\深空当铺正式版mod\星铁\` 的立绘重做）。
    ///
    /// 数据来源：
    ///   · 角色名 / 命途 / 元素属性 / 角色详情 —— biligame 星铁 wiki（`_sr93.tsv`）
    ///   · id —— 外文名转 snake_case（开拓者 5 形态、三月七 2 形态用命途后缀区分）
    ///   · 物品贴图 —— `星铁\<角色名>立绘.png`（2048×2048）缩到 48×48，按 `figurine_sr_<id>.png` 存
    ///
    /// 具体 93 行数据在 `StarRailData.g.cs`（自动生成，别手改）。
    ///
    /// 稀有度：5 档随机（固定种子，保证每次一致）；价格按星级：
    /// 5★ 1200 / 4★ 800 / 3★ 500 / 2★ 300 / 1★ 200
    /// tag：全部艺术品（ART）；4★及以上奢侈品（LUXURY_ITEM）。
    /// </summary>
    internal static class StarRailData
    {
        public struct Entry
        {
            public string Id;
            public string Zh;
            public int Star;
            public int Value;
            public string Desc;
            /// <summary>命途（毁灭 / 存护 / 虚无 / 记忆 …）。</summary>
            public string Path;
            /// <summary>元素属性（物理 / 火 / 冰 / 雷 / 风 / 量子 / 虚数）。</summary>
            public string Element;
        }

        private static List<Entry> _all;

        public static List<Entry> All
        {
            get
            {
                if (_all != null) return _all;
                _all = new List<Entry>();
                var rows = StarRailRaw.Rows;
                if (rows == null) return _all;

                foreach (var r in rows)
                {
                    if (r == null || r.Length < 6) continue;
                    int val;
                    if (!int.TryParse(r[5], out val) || val <= 0) val = 200;
                    _all.Add(new Entry
                    {
                        Id = "figurine_sr_" + r[0],
                        Zh = r[1],
                        Star = StarOf(r[0]),           // 先给个稳定的临时序
                        Value = val,                   // 逐条定值（catalog 阶梯 1200→200）
                        Path = r[2],
                        Element = r[3],
                        Desc = r[4]
                    });
                }

                // 按「数量金字塔」重新分档：1★最多 → 5★最少（TierQuota 统一规则）
                // 注意：**只改档位，不改价格** —— 价格是逐条定值的
                var order = new List<int>();
                for (int i = 0; i < _all.Count; i++) order.Add(i);
                // 按**价格**降序排（价格高的先分高星），价格相同再按 id
                // 这样「价格档位 ↔ 稀有度」永远一致（2026-11-04 修 bug 报告问题 3）
                order.Sort((a, b) =>
                {
                    int c = _all[b].Value.CompareTo(_all[a].Value);
                    if (c != 0) return c;
                    return string.CompareOrdinal(_all[a].Id, _all[b].Id);
                });
                var stars = TierQuota.StarsTopDown(_all.Count);
                for (int k = 0; k < order.Count && k < stars.Length; k++)
                {
                    var e = _all[order[k]];
                    e.Star = stars[k];
                    _all[order[k]] = e;
                }

                var dist = new int[6];
                foreach (var e in _all) dist[Math.Max(1, Math.Min(5, e.Star))]++;
                UnityEngine.Debug.Log(FigurineMod.LogTag + $"星铁重分档（金字塔）：1★{dist[1]} 2★{dist[2]} 3★{dist[3]} 4★{dist[4]} 5★{dist[5]}");
                return _all;
            }
        }

        /// <summary>固定分配 1~5 星（按 id 哈希），只用作重分档前的稳定排序依据。</summary>
        private static int StarOf(string key)
        {
            int h = 17;
            foreach (var c in key) h = h * 31 + c;
            return Math.Abs(h % 5) + 1;
        }

        /// <summary>档位 → 价格（5★1200 / 4★800 / 3★500 / 2★300 / 1★200）。</summary>
        public static int PriceOf(int star)
        {
            return star >= 5 ? 1200 : star == 4 ? 800 : star == 3 ? 500 : star == 2 ? 300 : 200;
        }

        public static Catalog.Def[] ToCatalog()
        {
            var list = All;
            var arr = new Catalog.Def[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                // 命途/元素不再拼进描述（会被自动换行拆开），改由 Defs.Meta 单独出一行
                arr[i] = new Catalog.Def { Id = e.Id, Zh = e.Zh, Star = e.Star, Value = e.Value, W = 3, H = 3, Element = e.Element, Desc = e.Desc };
            }
            return arr;
        }

        public static bool IsLuxury(int star) { return star >= 4; }
        public static string TagsText(int star) { return star >= 4 ? "艺术品 / 奢侈品" : "艺术品"; }
    }
}
