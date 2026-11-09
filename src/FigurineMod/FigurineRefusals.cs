using System;
using System.Collections.Generic;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// **拒收手办台词池（按理由分三类）** —— 玩家把手办摆给"不收"的顾客时，他说什么。
    ///
    /// # 三个理由（用户定：**优先级相同**，命中即在命中项里随机选）
    ///
    /// | 类 | 判据 | 游戏侧依据 |
    /// |---|---|---|
    /// | `Duplicate` 同款重复 | 顾客要求「种类多样」且这件已收过 | `StoreClient.isRequireVariety` + `IsItemInVarietyList(item)` |
    /// | `Stolen` 赃物 | 物品带 `STOLEN_TAG` | `StolenHelper.IsStolenItem(item)` |
    /// | `Fake` 赝品 | 物品是赝品变体 | `FigurineFake.IsFake(item, ...)` |
    ///
    /// 三类的**判定互不排斥**（一件手办可以既是赃物又是赝品），
    /// 所以是"把命中的类都收集起来，在命中的类里随机挑一类、再在那类里随机挑一句"。
    ///
    /// # 为什么"同款重复"交给游戏判
    ///
    /// 项目里已经有「种类多样」机制（`FigurineCustomer.RollRequireVariety` 每次到店 50% 随机判定，
    /// 为真时调游戏自己的 `SetVariety()`）。游戏随后会：
    /// ```
    /// 维护 `varietyTrackingList`（收过的种类）
    /// 用 `IsItemInVarietyList(item)` 判重
    /// 界面「顾客购买要求」显示「种类多样」
    /// ```
    /// 所以"同款"的判据**直接用游戏那个方法**，不另造一套 —— 避免两套判重逻辑打架。
    ///
    /// # 与赃物那套的关系
    ///
    /// 原先只有"赃物"一类（`FigurineRefusals`）。现在扩成三类，
    /// 台词也一并按类组织。本文件**取代** `FigurineRefusals`。
    /// </summary>
    internal static class FigurineRefusals
    {
        /// <summary>拒收理由。</summary>
        public enum Reason
        {
            /// <summary>同款重复（顾客要求种类多样，这件他已收过）。</summary>
            Duplicate = 0,
            /// <summary>赃物（带 `STOLEN_TAG`）。</summary>
            Stolen = 1,
            /// <summary>赝品（假货变体）。</summary>
            Fake = 2,
        }

        /// <summary>一位顾客的三类台词池。</summary>
        public sealed class Pool
        {
            /// <summary>同款重复。</summary>
            public string[] Duplicate;
            /// <summary>赃物。</summary>
            public string[] Stolen;
            /// <summary>赝品。</summary>
            public string[] Fake;

            /// <summary>按理由取池。</summary>
            public string[] Of(Reason r)
            {
                switch (r)
                {
                    case Reason.Duplicate: return Duplicate;
                    case Reason.Stolen: return Stolen;
                    case Reason.Fake: return Fake;
                    default: return null;
                }
            }
        }

        /// <summary>理由的中文名（日志用）。</summary>
        public static string NameOf(Reason r)
        {
            switch (r)
            {
                case Reason.Duplicate: return "同款重复";
                case Reason.Stolen: return "赃物";
                case Reason.Fake: return "赝品";
                default: return "?";
            }
        }

        /// <summary>该顾客有没有拒收台词（没有就返回 null）。</summary>
        public static Pool For(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return null;
            return Table.TryGetValue(identifier, out var p) ? p : null;
        }

        /// <summary>
        /// **按命中的理由随机抽一句**。命中的理由由调用方给出（可以多个，优先级相同）。
        /// 全都没命中 / 池里没有 → 返回 null（调用方保持安静）。
        /// </summary>
        public static string Pick(string identifier, List<Reason> hits, out Reason picked)
        {
            picked = Reason.Stolen;
            try
            {
                var pool = For(identifier);
                if (pool == null || hits == null || hits.Count == 0) return null;

                // 先随机挑一个"命中的理由"，再在该类里随机挑一句。
                // 两类都命中时各 50% —— 用户定的"优先级相同"。
                picked = hits[UnityEngine.Random.Range(0, hits.Count)];
                var arr = pool.Of(picked);
                if (arr == null || arr.Length == 0)
                {
                    // 该类没有台词 → 退到任意一个有词的命中类
                    for (int i = 0; i < hits.Count; i++)
                    {
                        var a2 = pool.Of(hits[i]);
                        if (a2 != null && a2.Length > 0) { picked = hits[i]; arr = a2; break; }
                    }
                }
                if (arr == null || arr.Length == 0) return null;
                if (arr.Length == 1) return arr[0];
                return arr[UnityEngine.Random.Range(0, arr.Length)];
            }
            catch { return null; }
        }

        // ══════════════════════════════════════════════════════════════════════
        // 台词表：4 位 × 3 类 × 10 条 = 120 条
        // ══════════════════════════════════════════════════════════════════════
        private static readonly Dictionary<string, Pool> Table =
            new Dictionary<string, Pool>(StringComparer.Ordinal)
        {
            // ────────────────────────────────────────────────────────────── 收藏家
            

            // ────────────────────────────────────────────────────────────── 外地爱好者
            ["figurine_enthusiast_tourist"] = new Pool
            {
                Duplicate = new[]
                {
                    "这个我上次已经买过一件了。\n再买就重了。",
                    "哎呀，我箱子里就有一个一样的。\n这个就算了吧。",
                    "我买这个是为了凑一套，重复的没用。\n你还有别的款吗？",
                    "同款我已经有了。\n我攒的是款式，不是数量。",
                    "这个我家柜子上就摆着一个。\n换一个新款给我看看？",
                    "我买重复的回去要被朋友笑的。\n这个不要了。",
                    "一模一样的我有一件了，够看就行。\n下次带别的来吧。",
                    "我收藏有清单的，这件已经打勾了。\n不能再买。",
                    "同款我不收第二件。\n你别怪我挑。",
                    "这个我真的很喜欢，可是我已经有了。\n留着卖给别人吧。",
                },
                Stolen = new[]
                {
                    "啊……这个我不敢要。\n我就是个普通人，惹不起那些事。",
                    "东西是好东西，可惜来路不对。\n我还是别碰了。",
                    "别、别拿这个给我看。\n我买这个是想留个念想，不是想找麻烦。",
                    "我坐了三天车来的，可不想因为一件东西被留下。\n这个我不要。",
                    "这个……我还是算了吧。\n我胆子小，你别为难我。",
                    "我朋友说过，这种货不能碰。\n我信他的。",
                    "我真的很喜欢，可是不行。\n你放回去吧，我看看别的。",
                    "我这趟是请假来的，回去还得上班。\n别让我摊上事。",
                    "真可惜。\n要是正经来路的，我砸锅卖铁也买。",
                    "你别拿这个试探我了。\n我买不起麻烦。",
                },
                Fake = new[]
                {
                    "咦？这个头饰好像少了……\n是不是有点不对？",
                    "等等，我记得原版不是这样的。\n这个是不是仿的？",
                    "我虽然不专业，但看得出做工不对。\n这个我不能要。",
                    "这个细节跟我在网上看到的图不一样。\n是假货吧？",
                    "你老实说，这个是仿的对不对？\n那我不要了。",
                    "我朋友是专门收这个的，他一眼就能看出来。\n我不敢买。",
                    "这件的部件缺了。\n正品不会这样出货的。",
                    "啊……我还以为捡到宝了。\n原来是假的。",
                    "做工差太多了。\n我买回去会被人笑的。",
                    "这个不对。\n我再看看别的吧。",
                },
            },

            // ────────────────────────────────────────────────────────────── 白鹭（温柔系邻家少女）
            //   说话短、轻、带停顿；拒绝也不伤人，但原则问题上很坚定
            ["figurine_collector_upper_sell"] = new Pool
            {
                Duplicate = new[]
                {
                    "嗯……这件我有了。",
                    "诶，一样的那件，我已经收过了。",
                    "不好意思呀，同款的我不能再要。",
                    "这件我拿过了，换一件好吗？",
                    "……重复的，我就先不要了。",
                    "我这边已经有一件一样的了。",
                    "抱歉，同款我收不下第二件。",
                    "嗯，这件我记着。换别的吧。",
                    "我这边的规矩是不收重复的，对不住呀。",
                    "这件我已经有了，你别为难我。",
                },
                Stolen = new[]
                {
                    "这个……来路我不太敢问。",
                    "嗯，这件我还是不收了。",
                    "抱歉呀，它有点说不清。",
                    "我不太敢碰这种。",
                    "……可以换一件吗？",
                    "这件让我有点不安。",
                    "我不想惹麻烦，对不住。",
                    "嗯，收下它我心里不踏实。",
                    "换件干净的来吧。",
                    "这件我实在不能要。",
                },
                Fake = new[]
                {
                    "诶，这个……好像不太对。",
                    "我感觉它少了点什么。",
                    "嗯，这件我不太敢收。",
                    "看起来有点怪，你确认过吗？",
                    "……这件的细节，我有点在意。",
                    "抱歉，我怕是假的。",
                    "我虽不懂行，但这件怪怪的。",
                    "这个我不收，你再看看别件吧。",
                    "嗯，它不太像正品。",
                    "这件放着吧，我怕看错。",
                },
            },

            // ────────────────────────────────────────────────────────────── 雀儿（复古飞行少女）
            //   短句、冷淡、略带慵懒；不羞辱人，但拒绝得很干脆
            ["figurine_pickpocket_1"] = new Pool
            {
                Duplicate = new[]
                {
                    "同款。不要。",
                    "这件我拿过了。",
                    "啧，重复的。",
                    "一样的，跳过。",
                    "同一种我不收第二件。",
                    "换一个。这个我有了。",
                    "嗯……重复的不要。",
                    "行吧，这件不算。",
                    "我只收没见过的。",
                    "这件留着吧。",
                },
                Stolen = new[]
                {
                    "这件来路太明显了。",
                    "啧，我不碰这种。",
                    "标签还挂着呢。",
                    "拿走吧，我不想惹事。",
                    "这个我收不了。",
                    "……你确定要卖这件给我？",
                    "我不碰烫手的。",
                    "换一件干净的。",
                    "这东西太扎眼。",
                    "不了。风险太大。",
                },
                Fake = new[]
                {
                    "假的。",
                    "啧，少了个件。",
                    "这个别拿出来了。",
                    "假的我不收。",
                    "你自己没看出来？",
                    "……做工太糙。",
                    "这个糊弄不了人。",
                    "拿走。",
                    "我不收假货。",
                    "假的，下一个。",
                },
            },
        };

        /// <summary>探针：列出所有拒收台词池。</summary>
        public static void Dump()
        {
            Debug.Log($"── 拒收手办台词池（三类）──");
            int total = 0;
            foreach (var kv in Table)
            {
                var p = kv.Value;
                int n = (p.Duplicate?.Length ?? 0) + (p.Stolen?.Length ?? 0) + (p.Fake?.Length ?? 0);
                total += n;
                Debug.Log($"     {kv.Key,-36} 同款 {p.Duplicate?.Length ?? 0}"
                    + $" / 赃物 {p.Stolen?.Length ?? 0} / 赝品 {p.Fake?.Length ?? 0}  = {n}");
            }
            Debug.Log($"  共 {Table.Count} 位顾客，{total} 条台词");
        }
    }
}
