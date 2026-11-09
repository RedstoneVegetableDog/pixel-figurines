using System;
using System.Collections.Generic;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 抽奖 —— **用户拍板的框架**（数值未与 beta 核对），三套 IP 各一个池子。
    ///
    /// ```
    /// 金卡：常驻 3% / UP 4%
    /// 保底：since &lt; 50 → 基础概率；50~90 线性爬坡；≥ 90 → 100%（出金归零）
    /// 非金部分：1★ 50% / 2★ 25% / 3★ 13% / 4★ 12%
    /// 金卡归属：常驻池只出「本期 UP」；
    ///           UP 池 = 1 张本期 UP + 2 张常驻，三张各 1/3
    /// ```
    /// **不走券、直接扣信用点**（用户 2026-11-03 要求）：常驻 200 / UP 400，抽成功才扣。
    /// </summary>
    internal static class Gacha
    {
        public const int PriceNormal = 200;
        public const int PriceRare = 400;

        private static readonly System.Random Rng = new System.Random();

        /// <summary>保底计数：key = series + "|" + pool。</summary>
        private static readonly Dictionary<string, int> _since = new Dictionary<string, int>(StringComparer.Ordinal);
        private static bool _loaded;

        public static int Price(bool rare) { return rare ? PriceRare : PriceNormal; }

        // ───────────────────────── 池子 ─────────────────────────

        /// <summary>取某系列的全部手办（gs / sr / zzz）。</summary>
        public static List<Catalog.Def> PoolOf(string series)
        {
            var list = new List<Catalog.Def>();
            try
            {
                if (series == "sr") list.AddRange(StarRailData.ToCatalog());
                else if (series == "zzz") list.AddRange(ZzzData.ToCatalog());
                else list.AddRange(Catalog.All);
            }
            catch (System.Exception __ex) { Safe.Warn("Gacha.cs:44", __ex); }
            list.RemoveAll(d => string.IsNullOrEmpty(d.Id));
            return list;
        }

        private static List<Catalog.Def> TierOf(List<Catalog.Def> all, int star)
        {
            var list = new List<Catalog.Def>();
            foreach (var d in all) if (d.Star == star) list.Add(d);
            return list;
        }

        /// <summary>本期 UP：每 14 天轮换两位 5★（照 beta 的 FeaturedGolds 思路）。</summary>
        public static List<Catalog.Def> Featured(string series)
        {
            var res = new List<Catalog.Def>();
            var five = TierOf(PoolOf(series), 5);
            if (five.Count == 0) return res;
            int day = 1;
            try { day = Math.Max(1, StoreStation.GetDayCounter()); } catch (System.Exception __ex) { Safe.Warn("Gacha.cs:63", __ex); }
            int shift = ((day / 14) * 2) % five.Count;
            res.Add(five[shift % five.Count]);
            if (five.Count > 1) res.Add(five[(shift + 1) % five.Count]);
            return res;
        }

        // ───────────────────────── 概率 ─────────────────────────

        private static double GoldChance(bool rare, int since)
        {
            // 用户拍板的框架：金卡基础 3%（常驻）/ 4%（UP），50 抽软保底、90 抽硬保底
            double baseP = rare ? 0.04 : 0.03;
            if (since >= 90) return 1.0;
            if (since < 50) return baseP;
            double k = (since - 50) / 40.0;          // 50 → 90 线性爬坡
            return baseP + k * (1.0 - baseP);
        }

        /// <summary>
        /// 返回 1~5 星。非金部分按用户拍板的权重：
        /// 1★ 50% / 2★ 25% / 3★ 13% / 4★ 12%（余下）
        /// </summary>
        private static int RollStar(bool rare, int since)
        {
            double gold = GoldChance(rare, since);
            double r = Rng.NextDouble();
            if (r < gold) return 5;

            double r2 = Rng.NextDouble();
            if (r2 < 0.50) return 1;
            if (r2 < 0.75) return 2;
            if (r2 < 0.88) return 3;
            return 4;
        }
        private static Catalog.Def PickTier(List<Catalog.Def> all, int star)
        {
            var list = TierOf(all, star);
            if (list.Count == 0) return default(Catalog.Def);
            return list[Rng.Next(list.Count)];
        }

        private static Catalog.Def PickGold(string series, List<Catalog.Def> all, bool rare)
        {
            var five = TierOf(all, 5);
            if (five.Count == 0) return default(Catalog.Def);
            var feat = Featured(series);
            if (feat.Count == 0) return five[Rng.Next(five.Count)];

            if (!rare) return feat[Rng.Next(feat.Count)];   // 常驻池只出本期 UP

            var up = new List<Catalog.Def>();
            foreach (var d in five)
                if (!feat.Exists(x => x.Id == d.Id)) up.Add(d);
            if (up.Count == 0) return feat[Rng.Next(feat.Count)];

            return Rng.Next(3) == 0 ? up[Rng.Next(up.Count)] : feat[Rng.Next(feat.Count)];
        }

        // ───────────────────────── 抽 ─────────────────────────

        /// <summary>抽奖。<paramref name="series"/> = gs / sr / zzz。</summary>
        public static string Draw(string series, bool rare, int n, out List<string> names)
        {
            names = new List<string>();
            try
            {
                if (n < 1) n = 1;
                if (n > 10) n = 10;

                int free = Warehouse.Capacity - Warehouse.Used;
                if (free < n) return $"仓库放不下：仓库 {Warehouse.Used}/{Warehouse.Capacity}，还能放 {free} 件，这次要抽 {n} 件。先取出一些手办再抽";

                var all = PoolOf(series);
                if (all.Count == 0) return "这个系列还没有手办";

                LoadPity();
                string key = series + "|" + (rare ? "rare" : "normal");

                for (int i = 0; i < n; i++)
                {
                    int since;
                    _since.TryGetValue(key, out since);

                    int star = RollStar(rare, since);
                    var def = star >= 5 ? PickGold(series, all, rare) : PickTier(all, star);
                    if (string.IsNullOrEmpty(def.Id)) def = PickTier(all, 2);
                    if (string.IsNullOrEmpty(def.Id)) def = all[Rng.Next(all.Count)];

                    _since[key] = star >= 5 ? 0 : since + 1;

                    Warehouse.AddOrBag(def.Id, 1);
                    names.Add(def.Zh);
                }

                SavePity();
                Debug.Log(FigurineMod.LogTag + $"抽奖 {series}/{(rare ? "UP" : "常驻")} ×{n}：{string.Join("、", names.ToArray())}");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError(FigurineMod.LogTag + "抽奖异常：" + e.GetType().Name + ": " + e.Message);
                return "抽奖异常";
            }
        }

        // ───────────────────────── 保底持久化 ─────────────────────────

        private const string KeyPity = "gacha_pity";
        private const string KeyPitySlot = "gacha_pity_slot";

        public static void ResetPityCache() { _loaded = false; _since.Clear(); }

        private static int Slot()
        {
            try { return PlayerStore.Instance != null ? PlayerStore.Instance.saveSlotId : 0; }
            catch { return 0; }
        }

        private static void LoadPity()
        {
            if (_loaded) return;
            _loaded = true;
            _since.Clear();
            try
            {
                int slot;
                if (!int.TryParse(ModData.Get(KeyPitySlot, "0"), out slot) || slot != Slot()) return;

                foreach (var line in ModData.GetLines(KeyPity))
                {
                    var p = line.Split('|');
                    if (p.Length < 3) continue;
                    int v;
                    if (int.TryParse(p[2], out v)) _since[p[0] + "|" + p[1]] = v;
                }
            }
            catch (System.Exception __ex) { Safe.Warn("Gacha.cs:200", __ex); }
        }

        private static void SavePity()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                foreach (var kv in _since)
                {
                    int idx = kv.Key.IndexOf('|');
                    if (idx <= 0) continue;
                    sb.Append(kv.Key.Substring(0, idx)).Append('|')
                      .Append(kv.Key.Substring(idx + 1)).Append('|')
                      .Append(kv.Value).Append('\n');
                }
                ModData.Set(KeyPity, sb.ToString());
                ModData.Set(KeyPitySlot, Slot().ToString());
            }
            catch (System.Exception __ex) { Safe.Warn("Gacha.cs:219", __ex); }
        }
    }
}
