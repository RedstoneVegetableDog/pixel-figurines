using System;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 手办「三态」地基（用户 2026-11-04 定）：
    ///   正品 / 赝品 / 未鉴定 —— 每件手办身上有两个身份：
    ///     · 标签（Label）：卖家嘴上说的，可能说谎，工具提示里就显示它；
    ///     · 真身（Truth）：实际真假，出场时掷好、永不改。
    ///   鉴定（勾缺损）把标签换成真身并锁死；估价（鉴定客）给未鉴定件用。
    ///
    /// 价格规则（用户定）：
    ///   标正品 100% / 标赝品 60% / 未鉴定 80%；鉴定后按真身（真 100% / 假 60%）。
    ///   其他系统（海子拒收、吸引力、顾客出价）在鉴定前读标签，不泄密。
    ///
    /// 来源掷骰（用户定）：
    ///   掉落 25% 错标 / 顾客卖方 25% / 交易板 5% / 以物易物 0%；
    ///   四者真身都是 正品 70% : 赝品 30%。
    ///   盲盒 / 抽奖 / 海子 / 展示柜 恒正品，不掷。
    /// </summary>
    internal static class FigurineAuth
    {
        public const string TagTruthFake = "FIG_TRUTH_FAKE";
        public const string TagLabelFake = "FIG_LABEL_FAKE";
        public const string TagUnappraised = "FIG_UNAPPRAISED";
        public const string TagChecked = "FIG_CHECKED";
        public const string TagLocked = "FIG_LOCKED";
        public const string TagVariant = "FIG_VARIANT";

        /// <summary>获取来源 —— 决定掷不掷、错标多少。</summary>
        public enum Source
        {
            None = 0,
            Loot,            // 掉落（游戏经 dir.Add 的 creator 建物）
            CustomerSeller,  // 顾客到店摆货
            TradeBoard,      // 交易板
            Barter,          // 以物易物
            BlindBox,        // 盲盒（恒正品）
            Gacha,           // 抽奖（恒正品）
            Haizi,           // 海子（恒正品）
            Cabinet,         // 展示柜（恒正品）
        }

        public struct Roll
        {
            public bool TruthFake;    // 真身是赝品
            public bool LabelFake;    // 标签写赝品
            public int Variant;       // 1 = 反色 / 2 = 缺件
            public bool Unappraised;  // 未鉴定（只有鉴定客的货）

            public override string ToString()
            {
                return (Unappraised ? "未鉴定" : (LabelFake ? "标赝品" : "标正品"))
                     + "/" + (TruthFake ? "真身赝品" : "真身正品")
                     + (TruthFake ? " v" + Variant : "");
            }
        }

        public const float TruthFakeChance = 0.30f;   // 正品:赝品 = 7:3

        private static bool Rolls(Source s)
        {
            return s == Source.Loot || s == Source.CustomerSeller
                || s == Source.TradeBoard || s == Source.Barter;
        }

        private static int MislabelPercent(Source s)
        {
            switch (s)
            {
                case Source.Loot: return 25;
                case Source.CustomerSeller: return 25;
                case Source.TradeBoard: return 5;
                case Source.Barter: return 0;
                default: return 0;
            }
        }

        /// <summary>按来源掷一次。不参与掷骰的来源返回全正品。id 用来选赝品变体（传 null 就按反色算）。</summary>
        public static Roll RollFor(Source s, string id = null)
        {
            var r = new Roll { Variant = 0 };
            if (!Rolls(s)) return r;

            r.TruthFake = UnityEngine.Random.value < TruthFakeChance;
            r.LabelFake = r.TruthFake;

            int mis = MislabelPercent(s);
            if (mis > 0 && UnityEngine.Random.Range(0, 100) < mis)
                r.LabelFake = !r.LabelFake;   // 标反了

            if (r.TruthFake) r.Variant = FigurineFake.PickVariant(id);
            return r;
        }

        /// <summary>未鉴定件：真身 50/50 定死（用户定），标签一律「未鉴定」。id 用来选赝品变体。</summary>
        public static Roll RollUnappraised(string id = null)
        {
            var r = new Roll { Unappraised = true };
            r.TruthFake = UnityEngine.Random.value < 0.5f;
            r.LabelFake = r.TruthFake;    // 未鉴定不看标签
            if (r.TruthFake) r.Variant = FigurineFake.PickVariant(id);
            return r;
        }

        // 变体选择已改为数据驱动：数量看 FakeData（原神 3 = 反色 + 两张赝品立绘；星铁/绝区零 1 = 反色），
        // 见 FigurineFake.VariantCount / FigurineFake.PickVariant。

        // ───────────────────────── 读写 tag ─────────────────────────

        private static void SetTag(GameItem item, string tag, bool on)
        {
            try
            {
                if (on) item.EnableTag(tag, true);
                else item.DisableTag(tag, true);
            }
            catch (Exception __ex) { Safe.Warn("FigurineAuth.SetTag:" + tag, __ex); }
        }

        public static void Apply(GameItem item, Roll r)
        {
            if (item == null) return;
            SetTag(item, TagTruthFake, r.TruthFake);
            // 未鉴定件没有"标签"这回事：tag 写 false，避免任何地方把内部真身当标签读出来
            SetTag(item, TagLabelFake, r.Unappraised ? false : r.LabelFake);
            SetTag(item, TagUnappraised, r.Unappraised);

            // 兼容旧逻辑：IS_FAKE / FAKE_VARIANT_n 一律跟「真身」走，
            // 放大镜托盘图（_fakeN）靠它们选图 —— 立绘本身就是鉴定的线索。
            SetTag(item, "IS_FAKE", r.TruthFake);
            if (r.TruthFake)
            {
                // 变体数量来自 FakeData（原神 3、星铁/绝区零 1）；老存档/调试传进来的越界值按取模兜底
                int count = Math.Max(1, FigurineFake.VariantCount(item.identifier));
                int v = r.Variant > 0 ? r.Variant : FigurineFake.PickVariant(item.identifier);
                if (v > count) v = ((v - 1) % count) + 1;
                r.Variant = v;
                SetTag(item, "FAKE_VARIANT_" + v, true);
                for (int i = 1; i <= 8; i++) if (i != v) SetTag(item, "FAKE_VARIANT_" + i, false);
            }
            try { item.SyncModifiedState(); } catch (Exception __ex) { Safe.Warn("FigurineAuth.Apply", __ex); }
            // 标签的正式载体是条件特征（原版标签机改的那条），在这里建/同步。
            // 未鉴定件**不挂**特征：否则原版会把「真身」当标签显示出来（`[标签] 正品`），提前泄露。
            if (!r.Unappraised)
            {
                try { FigurineLabelFeature.Ensure(item, r.LabelFake, r.TruthFake); } catch (Exception __ex) { Safe.Warn("FigurineAuth.Apply.Label", __ex); }
            }
            RefreshName(item);
            LogOnce(item, r);
        }

        /// <summary>
        /// 启动自检（纯数学，不依赖存档）：按来源各掷 2 千次，打印
        /// 真身赝品率 / 标签赝品率 / 错标率，核对是否符合「7:3 + 25/25/5/0%」。
        /// （2026-11-08 由 2 万次降到 2 千次：纯自检，2 千次已够看大盘，启动开销降到 1/10。）
        /// </summary>
        public static void SelfCheck()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                Source[] srcs = { Source.Loot, Source.CustomerSeller, Source.TradeBoard, Source.Barter };
                foreach (var s in srcs)
                {
                    const int n = 2000;
                    int truth = 0, label = 0, diff = 0;
                    for (int i = 0; i < n; i++)
                    {
                        var r = RollFor(s);
                        if (r.TruthFake) truth++;
                        if (r.LabelFake) label++;
                        if (r.TruthFake != r.LabelFake) diff++;
                    }
                    if (sb.Length > 0) sb.Append(" | ");
                    sb.Append(s).Append(" 真身赝品 ").Append(truth * 100 / n).Append('%')
                      .Append(" 标签赝品 ").Append(label * 100 / n).Append('%')
                      .Append(" 错标 ").Append(diff * 100 / n).Append('%');
                }
                Debug.Log(FigurineMod.LogTag + "三态自检（各 2,000 次）：" + sb
                    + " || 价格 标正品 100% / 标赝品 60% / 未鉴定 80%；鉴定后按真身");
            }
            catch (Exception __ex) { Safe.Warn("FigurineAuth.SelfCheck", __ex); }
        }

        private static int _logLeft = 12;

        private static void LogOnce(GameItem item, Roll r)
        {
            try
            {
                if (_logLeft <= 0) return;
                _logLeft--;
                Debug.Log(FigurineMod.LogTag + "三态 " + (item.identifier ?? "?") + "：" + r
                    + " 价格=" + PricePercent(item) + "%");
            }
            catch (Exception __ex) { Safe.Warn("FigurineAuth.LogOnce", __ex); }
        }

        /// <summary>掉落来源专用：建完再掷三态，返回同一件。</summary>
        public static GameItem Loot(GameItem item)
        {
            try { if (item != null) Apply(item, RollFor(Source.Loot, item.identifier)); }
            catch (Exception __ex) { Safe.Warn("FigurineAuth.Loot", __ex); }
            return item;
        }

        /// <summary>
        /// 名字按**标签**刷新：标赝品 → `XX（赝品）`，标正品 → `XX`。
        /// （用户 2026-11-04：赝品手办名字要带（赝品）。跟标签走，不跟真身，否则会提前泄密。）
        /// </summary>
        public static void RefreshName(GameItem item)
        {
            try
            {
                if (item == null) return;
                var d = Defs.Find(item.identifier);
                if (d.Id == null) return;
                // 未鉴定：名字保持干净（工具提示已写「[标签] 未鉴定」）
                if (IsUnappraised(item)) { item.name = d.Zh; return; }
                item.name = LabelFake(item) ? (d.Zh + "（赝品）") : d.Zh;
            }
            catch (Exception __ex) { Safe.Warn("FigurineAuth.RefreshName", __ex); }
        }

        /// <summary>读档时把全部手办的名字按标签刷一遍（老存档兼容：以前没名字后缀）。
        /// 必须包含**非玩家所有**的货（顾客柜台上的），否则鉴定前会出现「标签正品、名字赝品」。</summary>
        public static void RefreshAllNames()
        {
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null) return;
                int n = 0, fake = 0, unappr = 0;
                foreach (var it in ps.FindAllItem(false))
                {
                    if (it == null || !IsFigurine(it)) continue;
                    RefreshName(it);
                    n++;
                    if (IsUnappraised(it)) unappr++;
                    else if (LabelFake(it)) fake++;
                }
                if (n > 0) Debug.Log(FigurineMod.LogTag + "手办名字按标签刷新：" + n + " 件，其中标赝品 " + fake
                    + " 件，未鉴定 " + unappr + " 件");
            }
            catch (Exception __ex) { Safe.Warn("FigurineAuth.RefreshAllNames", __ex); }
        }

        /// <summary>只改标签（标签存在条件特征里；真身不动）。</summary>
        public static void SetLabelFake(GameItem item, bool fake)
        {
            // tag 保留写一份，给旧存档/兼容判断；真正读的是 FigurineLabelFeature（原版标签机改的那条）
            SetTag(item, TagLabelFake, fake);
            try { FigurineLabelFeature.SetLabel(item, fake); } catch (Exception __ex) { Safe.Warn("FigurineAuth.SetLabelFake.Feature", __ex); }
            try { FigurineScam.Clear(item); } catch (Exception __ex) { Safe.Warn("FigurineAuth.SetLabelFake.Scam", __ex); }
            RefreshName(item);
            try { item.SyncModifiedState(); } catch (Exception __ex) { Safe.Warn("FigurineAuth.SetLabelFake", __ex); }
        }

        public static void MarkChecked(GameItem item)
        {
            SetTag(item, TagChecked, true);
            try { item.SyncModifiedState(); } catch (Exception __ex) { Safe.Warn("FigurineAuth.MarkChecked", __ex); }
        }

        public static void ClearChecked(GameItem item)
        {
            SetTag(item, TagChecked, false);
            try { item.SyncModifiedState(); } catch (Exception __ex) { Safe.Warn("FigurineAuth.ClearChecked", __ex); }
        }

        /// <summary>勾错锁死（还在顾客手上时打上；买下后由鉴定面板自动解开）。</summary>
        public static void MarkLocked(GameItem item)
        {
            SetTag(item, TagLocked, true);
            try { item.SyncModifiedState(); } catch (Exception __ex) { Safe.Warn("FigurineAuth.MarkLocked", __ex); }
        }

        public static void ClearLocked(GameItem item)
        {
            SetTag(item, TagLocked, false);
            try { item.SyncModifiedState(); } catch (Exception __ex) { Safe.Warn("FigurineAuth.ClearLocked", __ex); }
        }

        public static bool IsLocked(GameItem item)
        {
            try { return item != null && item.IsTag(TagLocked); } catch { return false; }
        }

        public static bool TruthFake(GameItem item)
        {
            try { return item != null && item.IsTag(TagTruthFake); } catch { return false; }
        }

        public static bool LabelFake(GameItem item)
        {
            // 标签的**唯一来源**是条件特征（原版标签机改的就是它）；旧存档没特征时退回 tag
            try { return FigurineLabelFeature.LabelFake(item); }
            catch { try { return item != null && item.IsTag(TagLabelFake); } catch { return false; } }
        }

        public static bool IsUnappraised(GameItem item)
        {
            try { return item != null && item.IsTag(TagUnappraised); } catch { return false; }
        }

        public static bool IsChecked(GameItem item)
        {
            try { return item != null && item.IsTag(TagChecked); } catch { return false; }
        }

        public static int Variant(GameItem item)
        {
            try
            {
                for (int i = 1; i <= 8; i++) if (item.IsTag("FAKE_VARIANT_" + i)) return i;
            }
            catch (Exception __ex) { Safe.Warn("FigurineAuth.Variant", __ex); }
            return 1;
        }

        // ───────────────────────── 价格 / 显示 ─────────────────────────

        public static bool IsFigurine(GameItem item)
        {
            try
            {
                if (item == null) return false;
                string id = item.identifier;
                if (string.IsNullOrEmpty(id)) return false;
                // 快路径：不是 figurine_ 前缀的直接否掉，省掉字典查找（这个函数在价格/提示里被高频调用）
                if (id.Length < 9 || id[0] != 'f' || id[8] != '_') return false;
                if (!id.StartsWith("figurine_", StringComparison.Ordinal)) return false;
                return Defs.Find(id).Id != null;
            }
            catch { return false; }
        }

        /// <summary>价格百分比；不是手办返回 -1。</summary>
        public static int PricePercent(GameItem item)
        {
            if (!IsFigurine(item)) return -1;
            if (IsUnappraised(item)) return 80;
            // 价格**永远跟标签走**（用户 2026-11-04）：
            // 鉴定只负责把标签同步成真身并锁死，之后玩家再用标签机改，价格照样跟着变。
            return LabelFake(item) ? 60 : 100;
        }

        /// <summary>按一次掷骰结果算价格百分比（计划阶段还没有物品时用）：未鉴定 80 / 标赝品 60 / 标正品 100。</summary>
        public static int PricePercentForRoll(Roll r)
        {
            if (r.Unappraised) return 80;
            return r.LabelFake ? 60 : 100;
        }

        /// <summary>
        /// 「面向系统」的真假 = **标签**（价格/拒收/海子全按标签，用户 2026-11-04）。
        /// 鉴定后的标签已被同步成真身，所以鉴定后自然按真相；之后贴新标签就按新标签算。
        /// </summary>
        public static bool EffectiveFake(GameItem item)
        {
            if (item == null) return false;
            // 未鉴定件没有标签（内部 label 暂存的是真身），不能拿来判拒收，否则会泄露真假
            if (IsUnappraised(item)) return false;
            return LabelFake(item);
        }

        /// <summary>
        /// 工具提示里的那一行；不需要我们写时返回 null（游戏会自己渲染标签特征）。
        /// · 未鉴定 → `[标签] 未鉴定`（不能让它显示真身）
        /// · 有标签特征 → 交给原版 `LabelerHelper.CreateLabeledItemTooltip` 渲染，避免重复
        /// · 旧存档没有特征 → 我们兜底写一行
        /// </summary>
        public static string TooltipLine(GameItem item)
        {
            if (!IsFigurine(item)) return null;
            if (IsUnappraised(item)) return "[标签] 未鉴定";
            if (FigurineLabelFeature.Find(item) != null) return null;   // 原版会显示
            bool fake = IsChecked(item) ? TruthFake(item) : LabelFake(item);
            return "[标签] " + (fake ? "赝品" : "正品");
        }

        /// <summary>工具提示颜色：正品绿 / 赝品红 / 未鉴定橙。</summary>
        public static RenderHandler.ColorPalette TooltipColor(GameItem item)
        {
            if (IsUnappraised(item)) return RenderHandler.ColorPalette.LightOrange;
            bool fake = IsChecked(item) ? TruthFake(item) : LabelFake(item);
            return fake ? RenderHandler.ColorPalette.LightRed : RenderHandler.ColorPalette.LightGreen;
        }
    }
}
