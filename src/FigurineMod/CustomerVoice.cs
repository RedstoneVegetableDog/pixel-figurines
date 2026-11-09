using System;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 把 0.8.0 的 16 个台词池挂到 `StoreClient` 的对应字段上（beta 的 `FigurineCustomer` 就是这么做的）。
    ///
    /// 字段对应关系（正式版 `StoreClient` 里都有）：
    /// ```
    /// BuyMain / SellMain          → mainDialogue
    /// BuyDeal / SellDeal          → acceptDealDialogue
    /// DealLast                    → acceptDealDialogueLastLine
    /// BuyDone / SellDone          → allDoneDialogue
    /// BuyRepeat / SellRepeat      → repeatDialogue
    /// BuyWrongPlace               → placedWrongItemWhenSellingToDialogue
    /// BuyRightPlace / SellRightPlace → placeRightItemWhenSellingToDialogue
    /// Interrogate                 → interogationDialogue
    /// Glass                       → glasseDialogue
    /// PostArrest                  → customOnArrestDialogue
    /// ```
    /// 挂上之后，游戏会在对应时机自己播，不需要我们拦 UI。
    /// </summary>
    internal static class CustomerVoice
    {
        /// <summary>挂全部台词。返回主对话文本（调用方也可不用）。</summary>
        /// <summary>对话取向：0=买方 1=卖方 2=买卖都做（SELLNBUY）。</summary>
        public const int ModeBuy = 0, ModeSell = 1, ModeBoth = 2;

        private static string[] MainOf(CustomerLines.Pool p, int mode)
        {
            if (p == null) return null;
            if (mode == ModeBoth && p.BothMain != null && p.BothMain.Length > 0) return p.BothMain;
            return mode == ModeSell ? p.SellMain : p.BuyMain;
        }
        private static string[] DealOf(CustomerLines.Pool p, int mode)
        {
            if (p == null) return null;
            if (mode == ModeBoth && p.BothDeal != null && p.BothDeal.Length > 0) return p.BothDeal;
            return mode == ModeSell ? p.SellDeal : p.BuyDeal;
        }
        private static string[] DoneOf(CustomerLines.Pool p, int mode)
        {
            if (p == null) return null;
            if (mode == ModeBoth && p.BothDone != null && p.BothDone.Length > 0) return p.BothDone;
            return mode == ModeSell ? p.SellDone : p.BuyDone;
        }
        private static string[] RepeatOf(CustomerLines.Pool p, int mode)
        {
            if (p == null) return null;
            if (mode == ModeBoth && p.BothRepeat != null && p.BothRepeat.Length > 0) return p.BothRepeat;
            return mode == ModeSell ? p.SellRepeat : p.BuyRepeat;
        }
        private static string[] WrongOf(CustomerLines.Pool p, int mode)
        {
            if (p == null) return null;
            if (mode == ModeBoth && p.BothWrongPlace != null && p.BothWrongPlace.Length > 0) return p.BothWrongPlace;
            return p.BuyWrongPlace;
        }
        private static string[] RightOf(CustomerLines.Pool p, int mode)
        {
            if (p == null) return null;
            if (mode == ModeBoth && p.BothRightPlace != null && p.BothRightPlace.Length > 0) return p.BothRightPlace;
            return p.BuyRightPlace;
        }

        public static string Apply(StoreClient c, string identifier, string displayName, int mode,
                                   string fallbackMain = null, string fallbackDeal = null, string fallbackDone = null)
        {
            try
            {
                if (c == null) return null;
                var pool = CustomerLines.For(identifier);
                if (pool == null) return null;

                bool buySide = mode != ModeSell;
                string main = CustomerLines.Pick(MainOf(pool, mode));
                if (string.IsNullOrEmpty(main)) main = fallbackMain;
                if (!string.IsNullOrEmpty(main))
                {
                    var d = new Dialogue();
                    d.SetText(displayName, main);
                    c.mainDialogue = d;
                }

                string deal = CustomerLines.Pick(DealOf(pool, mode));
                if (string.IsNullOrEmpty(deal)) deal = fallbackDeal;
                if (!string.IsNullOrEmpty(deal))
                {
                    var d = new Dialogue();
                    d.SetText(displayName, deal);
                    c.acceptDealDialogue = d;
                }

                string last = CustomerLines.Pick(pool.DealLast);
                if (!string.IsNullOrEmpty(last))
                {
                    var d = new Dialogue();
                    d.SetText(displayName, last);
                    c.acceptDealDialogueLastLine = d;
                }

                string done = CustomerLines.Pick(DoneOf(pool, mode));
                if (string.IsNullOrEmpty(done)) done = fallbackDone;
                if (!string.IsNullOrEmpty(done))
                {
                    var d = new Dialogue();
                    d.SetText(displayName, done);
                    c.allDoneDialogue = d;
                }

                Set(c, CustomerLines.Pick(RepeatOf(pool, mode)), displayName, v => c.repeatDialogue = v);
                Set(c, CustomerLines.Pick(WrongOf(pool, mode)), displayName, v => c.placedWrongItemWhenSellingToDialogue = v);
                Set(c, CustomerLines.Pick(RightOf(pool, mode)), displayName, v => c.placeRightItemWhenSellingToDialogue = v);
                Set(c, CustomerLines.Pick(pool.Interrogate), displayName, v => c.interogationDialogue = v);
                Set(c, CustomerLines.Pick(pool.Glass), displayName, v => c.glasseDialogue = v);
                Set(c, CustomerLines.Pick(pool.PostArrest), displayName, v => c.customOnArrestDialogue = v);

                Debug.Log(FigurineMod.LogTag + "台词已挂 " + identifier + "（" + (mode == ModeBuy ? "买方" : mode == ModeSell ? "卖方" : "买卖都做") + "，intent=" + (int)c.clientIntent + "）");
                return main;
            }
            catch (Exception e)
            {
                Debug.LogWarning(FigurineMod.LogTag + "挂台词失败 " + identifier + "：" + e.GetType().Name);
                return null;
            }
        }

        private static void Set(StoreClient c, string text, string who, Action<Dialogue> assign)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                var d = new Dialogue();
                d.SetText(who, text);
                assign(d);
            }
            catch (System.Exception __ex) { Safe.Warn("CustomerVoice.cs:97", __ex); }
        }
    }
}
