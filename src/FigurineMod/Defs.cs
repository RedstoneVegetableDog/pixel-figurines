using System;
using System.Collections.Generic;

namespace FigurinePort
{
    /// <summary>
    /// 按 id 找 `Catalog.Def`（**原神 + 星铁合表**）。
    ///
    /// 为什么需要：星铁那 93 位不在 `Catalog.All` 里，只在 `StarRailData` 里。
    /// 以前购物车/仓库结算都是 `foreach (var d in Catalog.All) if (d.Id == key)`，
    /// 找不到就退回 `Catalog.All[0]`（雷电将军）—— 于是购物车里星铁手办全显示成雷电将军、
    /// 仓库取出星铁手办会取出个占位物品。
    /// </summary>
    internal static class Defs
    {
        private static Dictionary<string, Catalog.Def> _map;

        public static Catalog.Def Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return default(Catalog.Def);
            Ensure();
            Catalog.Def def;
            return _map.TryGetValue(id, out def) ? def : default(Catalog.Def);
        }

        public static bool Has(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            Ensure();
            return _map.ContainsKey(id);
        }

        /// <summary>
        /// 工具提示里的「属性行」：星铁 `【同谐·火】`、绝区零 `【电｜击破｜阵营】`、原神返回 null。
        ///
        /// 单独成行是为了**不让它被自动换行拆开**（以前把这段拼进描述里，
        /// 长描述会把 `【同谐·火】` 从中间断成两行）。
        /// </summary>
        public static string Meta(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            try
            {
                if (id.StartsWith("figurine_sr_", StringComparison.Ordinal))
                {
                    foreach (var e in StarRailData.All)
                        if (e.Id == id) return "【" + e.Path + " · " + e.Element + "】";
                }
                else if (id.StartsWith("figurine_zzz_", StringComparison.Ordinal))
                {
                    foreach (var e in ZzzData.All)
                        if (e.Id == id) return "【" + e.Element + " · " + e.Specialty + " · " + e.Faction + "】";
                }
                else if (id.StartsWith("figurine_gs_", StringComparison.Ordinal))
                {
                    // 原神：`【雷 · 稻妻】`，元素或地区缺失都显示「无」
                    var g = Find(id);
                    string el = string.IsNullOrEmpty(g.Element) ? "无" : g.Element;
                    string reg = null;
                    try
                    {
                        foreach (var r in GenshinRegion.Rows)
                            if (r != null && r.Length >= 3 && r[0] == id) { reg = r[2]; break; }
                    }
                    catch (System.Exception __ex) { Safe.Warn("Defs.cs:65", __ex); }
                    if (string.IsNullOrEmpty(reg)) reg = "无";
                    // 元素、地区都没有（若娜瓦 / 派蒙）→ 只显示 `【无】`
                    if (el == "无" && reg == "无") return "【无】";
                    return "【" + el + " · " + reg + "】";
                }
            }
            catch (System.Exception __ex) { Safe.Warn("Defs.cs:72", __ex); }
            return null;
        }

        /// <summary>
        /// 工具提示里的「厂牌」行：不同游戏写不同的游戏名 + 米哈游（不带“公司”二字）。
        /// ```
        /// 原神      → 原神·米哈游
        /// 星铁      → 星穹铁道·米哈游
        /// 绝区零    → 绝区零·米哈游
        /// ```
        /// </summary>
        public static string Company(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (id.StartsWith("figurine_gs_", StringComparison.Ordinal)) return "原神·米哈游";
            if (id.StartsWith("figurine_sr_", StringComparison.Ordinal)) return "星穹铁道·米哈游";
            if (id.StartsWith("figurine_zzz_", StringComparison.Ordinal)) return "绝区零·米哈游";
            return null;
        }

        /// <summary>换存档/新注册物品后调用，下次 Find 会重建表。</summary>
        /// <summary>
        /// 安全查表：找不到就记一条警告并返回 false。
        /// 直接用 Find 拿到 default(Def) 时 Value=0，容易造成「0 价物品 / 免费成交」。
        /// </summary>
        public static bool TryFind(string id, out Catalog.Def def)
        {
            def = Find(id);
            if (string.IsNullOrEmpty(def.Id))
            {
                Safe.Warn("Defs.TryFind:" + id, null);
                return false;
            }
            return true;
        }
        /// <summary>三个 IP 合表（原神 + 星铁 + 绝区零），按需构建一次。</summary>
        public static System.Collections.Generic.List<Catalog.Def> All()
        {
            var list = new System.Collections.Generic.List<Catalog.Def>();
            try
            {
                foreach (var d in Catalog.All) if (!string.IsNullOrEmpty(d.Id)) list.Add(d);
                foreach (var d in StarRailData.ToCatalog()) if (!string.IsNullOrEmpty(d.Id)) list.Add(d);
                foreach (var d in ZzzData.ToCatalog()) if (!string.IsNullOrEmpty(d.Id)) list.Add(d);
            }
            catch (System.Exception __ex) { Safe.Warn("Defs.cs:104", __ex); }
            return list;
        }

        public static void Invalidate() { _map = null; }

        private static void Ensure()
        {
            if (_map != null) return;
            var map = new Dictionary<string, Catalog.Def>(StringComparer.Ordinal);
            try
            {
                foreach (var d in Catalog.All) if (!string.IsNullOrEmpty(d.Id)) map[d.Id] = d;
                foreach (var d in StarRailData.ToCatalog()) if (!string.IsNullOrEmpty(d.Id)) map[d.Id] = d;
                foreach (var d in ZzzData.ToCatalog()) if (!string.IsNullOrEmpty(d.Id)) map[d.Id] = d;
            }
            catch (System.Exception __ex) { Safe.Warn("Defs.cs:120", __ex); }
            _map = map;
        }
    }
}
