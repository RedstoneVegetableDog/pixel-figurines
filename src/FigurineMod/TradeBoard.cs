using System;
using System.Collections.Generic;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 交易板（终端机入口页的「交易」卡片）。
    ///
    /// 规则（用户 2026-11-02 定）：
    /// ```
    /// · 需要持有 DORO 终端机才能用
    /// · 每天随机 1~20 张卡（次日刷新）
    /// · 每张卡分两种：玩家买进 / 玩家卖出
    /// · 价格 = 原价 × (1 ± 0~10%)
    /// · 单张交易完变灰，当天再点无效；次日整板刷新
    /// · 每天交易张数不限
    /// ```
    ///
    /// 存档：全部走 `ModData`，按「天」存盘 —— 同一天重进游戏板子不变。
    ///   trade_day   当天日期
    ///   trade_plan  板子内容（每行：id|买或卖|价格）
    ///   trade_done  已成交的下标（逗号分隔）
    /// </summary>
    internal static class TradeBoard
    {
        public sealed class Entry
        {
            public Catalog.Def Def;
            public bool PlayerBuys;   // true = 玩家买进（板子卖给他）
            public int Price;
            public bool Done;
            // 三态（用户 2026-11-04 定）：真身 7:3、标签 5% 错标，价格按标签算
            public bool TruthFake;
            public bool LabelFake;
            public int Variant;
        }

        private const string KeyDay = "trade_day";
        private const string KeyPlan = "trade_plan";
        private const string KeyDone = "trade_done";

        private static int _day = int.MinValue;
        private static List<Entry> _entries;

        public static int Today
        {
            get
            {
                try { return Math.Max(1, StoreStation.GetDayCounter()); }
                catch { return 1; }
            }
        }

        public static List<Entry> Entries
        {
            get
            {
                Ensure();
                return _entries;
            }
        }

        private static void Ensure()
        {
            int day = Today;
            if (_entries != null && _day == day) return;

            var loaded = Load(day);
            if (loaded == null || loaded.Count == 0)
            {
                loaded = Generate(day);
                Save(day, loaded);
            }
            _entries = loaded;
            _day = day;
        }

        // ───────────────────────── 生成 ─────────────────────────

        private static List<Entry> Generate(int day)
        {
            var res = new List<Entry>();
            try
            {
                var pool = new List<Catalog.Def>();
                // 池子 = 原神 40 + 星铁 93（用户 2026-11-03 要求交易板也含星铁角色）
                foreach (var d in Defs.All()) if (!string.IsNullOrEmpty(d.Id)) pool.Add(d);
                foreach (var d in StarRailData.ToCatalog()) if (!string.IsNullOrEmpty(d.Id)) pool.Add(d);
                foreach (var d in ZzzData.ToCatalog()) if (!string.IsNullOrEmpty(d.Id)) pool.Add(d);
                if (pool.Count == 0) return res;

                for (int i = pool.Count - 1; i > 0; i--)
                {
                    int k = UnityEngine.Random.Range(0, i + 1);
                    var t = pool[i]; pool[i] = pool[k]; pool[k] = t;
                }

                int n = UnityEngine.Random.Range(5, 31);            // 5~30 张（用户 2026-11-03 改）
                if (n > pool.Count) n = pool.Count;

                for (int i = 0; i < n; i++)
                {
                    var def = pool[i];
                    // 三态：真身 7:3、标签 5% 错标；价格基准按「标签价」
                    var roll = FigurineAuth.RollFor(FigurineAuth.Source.TradeBoard, def.Id);
                    int labelPct = roll.LabelFake ? 60 : 100;
                    float k = UnityEngine.Random.Range(-0.10f, 0.10f);   // 原价上下 0~10%
                    int price = Mathf.Max(1, Mathf.RoundToInt(def.Value * labelPct / 100f * (1f + k)));
                    res.Add(new Entry
                    {
                        Def = def,
                        PlayerBuys = UnityEngine.Random.value < 0.5f,
                        Price = price,
                        Done = false,
                        TruthFake = roll.TruthFake,
                        LabelFake = roll.LabelFake,
                        Variant = roll.Variant,
                    });
                }
                Debug.Log(FigurineMod.LogTag + "交易板生成：第 " + day + " 天 / " + res.Count + " 张");
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "交易板生成失败：" + e.GetType().Name); }
            return res;
        }

        // ───────────────────────── 存取 ─────────────────────────

        private static void Save(int day, List<Entry> list)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                foreach (var e in list)
                    sb.Append(e.Def.Id).Append('|').Append(e.PlayerBuys ? "buy" : "sell").Append('|').Append(e.Price)
                      .Append('|').Append(e.TruthFake ? 1 : 0).Append('|').Append(e.LabelFake ? 1 : 0).Append('|').Append(e.Variant)
                      .Append('\n');
                ModData.Set(KeyPlan, sb.ToString());
                ModData.Set(KeyDay, day.ToString());
                ModData.Set(KeyDone, "");
            }
            catch (System.Exception __ex) { Safe.Warn("TradeBoard.cs:130", __ex); }
        }

        private static void SaveDone()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (!_entries[i].Done) continue;
                    if (sb.Length > 0) sb.Append(',');
                    sb.Append(i);
                }
                ModData.Set(KeyDone, sb.ToString());
            }
            catch (System.Exception __ex) { Safe.Warn("TradeBoard.cs:146", __ex); }
        }

        private static List<Entry> Load(int day)
        {
            try
            {
                int savedDay;
                if (!int.TryParse(ModData.Get(KeyDay, "0"), out savedDay) || savedDay != day) return null;

                var lines = ModData.GetLines(KeyPlan);
                if (lines == null || lines.Length == 0) return null;

                var res = new List<Entry>();
                foreach (var line in lines)
                {
                    if (string.IsNullOrEmpty(line)) continue;
                    var p = line.Split('|');
                    if (p.Length < 3) continue;

                    // 合表查（原神 + 星铁），否则星铁的卡片存盘后读不回来
                    var def = Defs.Find(p[0]);
                    if (string.IsNullOrEmpty(def.Id)) continue;

                    int price;
                    if (!int.TryParse(p[2], out price) || price <= 0) price = def.Value;

                    // 老存档只有 3 段（id|方向|价格）→ 默认全正品
                    bool truthFake = p.Length > 3 && p[3] == "1";
                    bool labelFake = p.Length > 4 && p[4] == "1";
                    int variant = 1;
                    if (p.Length > 5) int.TryParse(p[5], out variant);
                    if (variant < 1) variant = 1;

                    res.Add(new Entry
                    {
                        Def = def,
                        PlayerBuys = p[1] == "buy",
                        Price = price,
                        Done = false,
                        TruthFake = truthFake,
                        LabelFake = labelFake,
                        Variant = variant,
                    });
                }

                var done = ModData.Get(KeyDone, "");
                if (!string.IsNullOrEmpty(done))
                {
                    foreach (var s in done.Split(','))
                    {
                        int idx;
                        if (!int.TryParse(s, out idx)) continue;
                        if (idx >= 0 && idx < res.Count) res[idx].Done = true;
                    }
                }
                return res;
            }
            catch { return null; }
        }

        // ───────────────────────── 交易 ─────────────────────────

        public static bool HasTerminal() { return DoroItems.HasTerminal(); }

        /// <summary>成交一张卡，返回给玩家看的提示文字。</summary>
        public static string Transact(int index)
        {
            try
            {
                Ensure();
                if (_entries == null || index < 0 || index >= _entries.Count) return "无效的卡片";
                var e = _entries[index];
                if (e.Done) return "这张已经交易过了（明天刷新）";

                var ps = PlayerStore.Instance;
                if (ps == null) return "店内上下文缺失";
                var inv = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.invElement : null;

                if (e.PlayerBuys)
                {
                    // 玩家买进：先确认背包放得下再扣钱
                    if (!ps.CanPay(e.Price)) return $"现金不足：需要 {e.Price} ₡";
                    var item = FigurineMod.BuildFigurine(e.Def);
                    if (item == null) return "建物失败";
                    // 把卡片上记的三态落到物品身上（老存档 = 全正品）
                    try
                    {
                        FigurineAuth.Apply(item, new FigurineAuth.Roll
                        {
                            TruthFake = e.TruthFake,
                            LabelFake = e.LabelFake,
                            Variant = e.Variant,
                        });
                    }
                    catch (System.Exception __ex) { Safe.Warn("TradeBoard.Auth", __ex); }
                    if (!FigurineMod.TryPlace(inv, item)) return "背包放不下";
                    ps.PayMoney(e.Price, false);
                    e.Done = true;
                    SaveDone();
                    Debug.Log(FigurineMod.LogTag + $"交易板：买入 {e.Def.Zh} -{e.Price}");
                    return $"买入 {e.Def.Zh}（-{e.Price} ₡）";
                }
                else
                {
                    // 玩家卖出：**只认模组仓库 / 货物邮箱**（背包里的不算）
                    if (Warehouse.Consume(e.Def.Id, 1))
                    {
                        try { ModHelper.AddCredit(e.Price, false); } catch (System.Exception __ex) { Safe.Warn("TradeBoard.cs:233", __ex); }
                        e.Done = true;
                        SaveDone();
                        Debug.Log(FigurineMod.LogTag + $"交易板：卖出 {e.Def.Zh} +{e.Price}（仓库扣）");
                        return $"卖出 {e.Def.Zh}（+{e.Price} ₡）";
                    }

                    var inMailbox = FindInMailbox(e.Def.Id);
                    if (inMailbox != null)
                    {
                        try { inMailbox.Destroy(); } catch (System.Exception __ex) { Safe.Warn("TradeBoard.cs:243", __ex); }
                        try { ModHelper.AddCredit(e.Price, false); } catch (System.Exception __ex) { Safe.Warn("TradeBoard.cs:244", __ex); }
                        e.Done = true;
                        SaveDone();
                        Debug.Log(FigurineMod.LogTag + $"交易板：卖出 {e.Def.Zh} +{e.Price}（货物邮箱扣）");
                        return $"卖出 {e.Def.Zh}（+{e.Price} ₡）";
                    }

                    return $"仓库 / 货物邮箱里没有「{e.Def.Zh}」";
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(FigurineMod.LogTag + "交易板成交异常：" + ex.GetType().Name + ": " + ex.Message);
                return "交易失败";
            }
        }

        private static GameItem FindInBag(GameGridInventory inv, string id)
        {
            try
            {
                if (inv == null) return null;
                var kids = inv.childItems;
                if (kids == null) return null;
                for (int i = 0; i < kids.Count; i++)
                {
                    var it = kids[i];
                    if (it == null) continue;
                    if (string.Equals(it.identifier, id, StringComparison.Ordinal)) return it;
                }
            }
            catch (System.Exception __ex) { Safe.Warn("TradeBoard.cs:275", __ex); }
            return null;
        }

        /// <summary>
        /// 在「货物邮箱」容器里找一件指定手办。
        /// 做法：扫玩家全部物品，挑出 identifier 相同的，再沿 `parentInventory` 往上找它的归属物品，
        /// 归属物品是货物邮箱（`figurine_gs_mailbox`）就算命中。
        /// </summary>
        private static GameItem FindInMailbox(string id)
        {
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null) return null;
                var all = ps.FindAllItem(true);
                if (all == null) return null;

                foreach (var it in all)
                {
                    if (it == null) continue;
                    string cur = null;
                    try { cur = it.identifier; } catch (System.Exception __ex) { Safe.Warn("TradeBoard.cs:297", __ex); }
                    if (!string.Equals(cur, id, StringComparison.Ordinal)) continue;

                    var node = it;
                    for (int depth = 0; depth < 6; depth++)
                    {
                        GameInventory parentInv = null;
                        try { parentInv = node.parentInventory; } catch (System.Exception __ex) { Safe.Warn("TradeBoard.cs:304", __ex); }
                        if (parentInv == null) break;

                        GameItem owner = null;
                        try { owner = GraphUtils.FindParentType<GameItem>(parentInv); } catch (System.Exception __ex) { Safe.Warn("TradeBoard.cs:308", __ex); }
                        if (owner == null) break;

                        string ownerId = null;
                        try { ownerId = owner.identifier; } catch (System.Exception __ex) { Safe.Warn("TradeBoard.cs:312", __ex); }
                        if (string.Equals(ownerId, DoroItems.MailboxId, StringComparison.Ordinal)) return it;

                        node = owner;
                    }
                }
            }
            catch (System.Exception __ex) { Safe.Warn("TradeBoard.cs:319", __ex); }
            return null;
        }

        /// <summary>读档/换天时丢弃缓存（下次访问按新天重建）。</summary>
        public static void Invalidate() { _entries = null; _day = int.MinValue; }
    }
}
