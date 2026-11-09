using System;
using System.Collections.Generic;

namespace FigurinePort
{
    /// <summary>
    /// 绝区零手办数据（**58 位**，2026-11-03 按 `E:\深空当铺正式版mod\绝区零\` 的 58 张立绘建）。
    ///
    /// 数据来源：biligame 绝区零 wiki（属性 / 特性 / 阵营 / 官方介绍「代理人档案·档案1 + 档案人」）；
    /// 具体 58 行在 `ZzzData.g.cs`（自动生成，别手改）。
    ///
    /// 星级：wiki 稀有度 S → 5★、A → 4★；价格照原神档位 5★1200 / 4★800。
    /// 描述格式（用户 2026-11-03 指定）：`【属性｜特性｜阵营｜官方介绍】`
    /// </summary>
    internal static class ZzzData
    {
        public struct Entry
        {
            public string Id;
            public string Zh;
            public int Star;
            public int Value;
            public string Element;
            public string Specialty;
            public string Faction;
            public string Quote;
            public string Speaker;
            /// <summary>拼好的描述（【属性｜特性｜阵营｜官方介绍】）。</summary>
            public string Desc;
        }

        private static List<Entry> _all;

        public static List<Entry> All
        {
            get
            {
                if (_all != null) return _all;
                _all = new List<Entry>();
                var rows = ZzzRaw.Rows;
                if (rows == null) return _all;

                foreach (var r in rows)
                {
                    if (r == null || r.Length < 9) continue;
                    int star;
                    if (!int.TryParse(r[2], out star) || star < 1 || star > 5) star = 4;
                    int val;
                    if (!int.TryParse(r[8], out val) || val <= 0) val = 200;

                    // 属性/特性/阵营不再拼进描述（会被自动换行拆开），由 Defs.Meta 单独出一行
                    string desc = r[6];
                    if (!string.IsNullOrEmpty(r[7])) desc += "　—— " + r[7];

                    _all.Add(new Entry
                    {
                        Id = "figurine_zzz_" + r[0],
                        Zh = r[1],
                        Star = star,                 // 先给 wiki 的 S/A 序
                        Value = val,                 // 逐条定值（catalog 阶梯 1200→200）
                        Element = r[3],
                        Specialty = r[4],
                        Faction = r[5],
                        Quote = r[6],
                        Speaker = r[7],
                        Desc = desc,
                    });
                }

                // 按「数量金字塔」重新分档：1★最多 → 5★最少（S 在前、A 在后）
                // 注意：**只改档位，不改价格** —— 价格是逐条定值的
                var order = new List<int>();
                for (int i = 0; i < _all.Count; i++) order.Add(i);
                // 按**价格**降序（价格高的分高星），保证价格档位与稀有度一致
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
                UnityEngine.Debug.Log(FigurineMod.LogTag + $"绝区零重分档（金字塔）：1★{dist[1]} 2★{dist[2]} 3★{dist[3]} 4★{dist[4]} 5★{dist[5]}");
                return _all;
            }
        }

        public static Catalog.Def[] ToCatalog()
        {
            var list = All;
            var arr = new Catalog.Def[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                arr[i] = new Catalog.Def { Id = e.Id, Zh = e.Zh, Star = e.Star, Value = e.Value, W = 3, H = 3, Element = e.Element, Desc = e.Desc };
            }
            return arr;
        }

        public static bool IsLuxury(int star) { return star >= 4; }
    }
}
