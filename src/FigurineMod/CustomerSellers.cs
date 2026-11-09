using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>卖方顾客：白鹭（干净货 1~4 件）、雀儿（混赃物）。</summary>
    internal static class CustomerSellers
    {
        public const string BailuId = "figurine_collector_upper_sell";
        public const string QueerId = "figurine_pickpocket_1";

        public static string ActiveSeller { get; private set; }

        private static void AddSeller(string id, string name, string sprite, string mainText, string dealText, string doneText, bool stolen, StoreClient.ClientIntent? forced = null, string onlyBuyId = null, string barterTargetId = null, string sellText = null)
        {
            var mgr = PlayerStore.Instance != null ? PlayerStore.Instance.storeClientManager : null;
            if (mgr == null) return;
            var c = new StoreClient();
            c.identifier = id;
            c.displayName = name;
            c.realName = name;
            c.dismissable = true;
            c.isBargainAvailable = true;
            c.tag = new List<string>();
            try { c.SetBudget(0); } catch (System.Exception __ex) { Safe.Warn("CustomerSellers.cs:27", __ex); }
            try { c.AddBasicDialog(); } catch (System.Exception __ex) { Safe.Warn("CustomerSellers.cs:28", __ex); }
            try { BargainLines.ApplyFor(id); } catch (System.Exception __ex) { Safe.Warn("CustomerSellers.cs:29", __ex); }
            c.clientFaction = StoreClient.FACTION_TOURIST;
            c.clientIntent = forced ?? StoreClient.ClientIntent.SELL;
            c.clientCash = 0;
            c.useClientBudget = false;
            c.spriteName = sprite;
            if (!string.IsNullOrEmpty(sprite)) c.possibleSprites = new List<string> { sprite };
            // —— 方向收口：只要这一次是「买」侧（BUY / SELLNBUY），就必须有想买的东西和钱，
            //    否则会以买方身份进店、却什么都谈不成（0.8.0 的 CustomerDef 里 白鹭/雀儿 的
            //    BuyingIds 是 null、Cash 是 0，是给她们「只卖」用的；掷到买侧时得补上）。
            bool willBuy = forced == StoreClient.ClientIntent.BUY || forced == StoreClient.ClientIntent.SELLNBUY;
            if (willBuy)
            {
                c.clientBuyingTagList = new List<string> { "ART", "LUXURY_ITEM" };
                c.clientBuyingIdList = new List<string>();
                foreach (var d in Defs.All()) c.clientBuyingIdList.Add(d.Id);
                if (!string.IsNullOrEmpty(onlyBuyId))
                {
                    c.clientBuyingIdList = new List<string> { onlyBuyId };
                    c.clientBuyingTagList = new List<string>();
                }
                try { c.SetBudget(3000); c.clientCash = 3000; } catch (System.Exception __ex) { Safe.Warn("CustomerSellers.cs:50", __ex); }
            }
            else
            {
                c.clientBuyingIdList = new List<string>();
                if (!string.IsNullOrEmpty(onlyBuyId))
                {
                    c.clientBuyingIdList = new List<string> { onlyBuyId };
                    c.clientBuyingTagList = new List<string>();
                }
                c.clientBuyingTagList = new List<string>();
            }
            c.clientBlackIdList = new List<string>();
            c.clientBlackTagList = new List<string>();

            int voiceMode = c.clientIntent == StoreClient.ClientIntent.SELL ? CustomerVoice.ModeSell
                          : c.clientIntent == StoreClient.ClientIntent.SELLNBUY ? CustomerVoice.ModeBoth
                          : CustomerVoice.ModeBuy;
            bool buySide = c.clientIntent == StoreClient.ClientIntent.BUY || c.clientIntent == StoreClient.ClientIntent.SELLNBUY;
            // 0.8.0 原版台词：16 个池整组挂上（含复访/摆错/摆对/盘问/放大镜/被抓/成交收尾）
            // 开场白要跟方向匹配：卖方念卖方台词，买方念买方台词（以前共用一句，导致「带东西来卖却说要买」）
            string openText = buySide ? mainText : (string.IsNullOrEmpty(sellText) ? mainText : sellText);
            CustomerVoice.Apply(c, id, name, voiceMode, openText, dealText, doneText);

            if (stolen) c.identifier = id; // 赃物在摆货时逐件判定
            try { ModHelper.SetClientSource(c, "figurine.gs"); } catch (System.Exception __ex) { Safe.Warn("CustomerSellers.cs:70", __ex); }
            // 必须用 AddNextClient：它会 Insert(0) 插到队首并 TrackClient。
            // 以前用 clientStack.Add(c) 把人排到当天所有顾客后面 → 有货没人 / 买不了 / 人来了货没了。
            try { mgr.AddNextClient(c); }
            catch (System.Exception __ex) { Safe.Warn("CustomerSellers.AddNextClient", __ex); }
            if (!string.IsNullOrEmpty(barterTargetId)) { try { Barter.Attach(c, barterTargetId); } catch (System.Exception __ex) { Safe.Warn("CustomerSellers.cs:72", __ex); } }
            ActiveSeller = id;
            Debug.Log(FigurineMod.LogTag + name + " 已到店（卖方）");
            // SELLNBUY = 买卖都做：既带钱也摆货（对齐原版 GameItem 的判定：
            // 卖侧条件 = SELL || SELLNBUY，买侧条件 = BUY || SELLNBUY）
            bool hasWares = c.clientIntent == StoreClient.ClientIntent.SELL || c.clientIntent == StoreClient.ClientIntent.SELLNBUY;
            if (hasWares) ArmWares(id, stolen);   // 以前直接 QueueWares → 人还在排队，货就自己出现了
        }

        private static bool _wFull;   // 本次摆货遇到放不下，提前收手
        private static string _armId;
        private static bool _armStolen;
        private static bool _armed;

        /// <summary>登记「这位带了几件货」，等他真的走到窗口再摆（2026-11-05 修「人没到手办到了」）。</summary>
        public static void ArmWares(string id, bool stolen)
        {
            _armId = id; _armStolen = stolen; _armed = true;
            Debug.Log(FigurineMod.LogTag + "卖方已排队，到店后再摆货：" + id);
        }

        /// <summary>真正的到店回调（DoroArrivalPatch 在 TryCallNextClient 之后调用）。</summary>
        public static void OnArrived(string id)
        {
            try
            {
                if (!_armed || !string.Equals(id, _armId, StringComparison.Ordinal)) return;
                _armed = false;
                QueueWares(id, _armStolen);
            }
            catch (System.Exception __ex) { Safe.Warn("CustomerSellers.OnArrived", __ex); }
        }

        public static void TryBailu(StoreClient.ClientIntent? forced = null, string onlyBuyId = null, string barterTargetId = null)
        {
            try
            {
                string sp = CustomerSprites.Get(BailuId);
                AddSeller(BailuId, "白鹭", sp,
                    "白鹭，替人跑腿看行情。\n这件东西，我手上正缺。\n行情我熟，给你个实在价。",
                    "成交，痛快。", "下次有好货，记得叫我。", false, forced, onlyBuyId, barterTargetId,
                    "白鹭，替人跑腿收了一批。\n你看看这几件值多少，\n行情我熟，不会坑你。");
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "白鹭异常：" + e.GetType().Name); }
        }

        public static void TryQueer(StoreClient.ClientIntent? forced = null, string onlyBuyId = null, string barterTargetId = null)
        {
            try
            {
                string sp = CustomerSprites.Get(QueerId);
                AddSeller(QueerId, "雀儿", sp,
                    "嗨，叫我雀儿！\n这件我相中了，\n你开个价呗？",
                    "诶嘿，成交！", "下回还找你啊！", true, forced, onlyBuyId, barterTargetId,
                    "嗨，叫我雀儿！\n这几件刚到手，\n你要不要看看？");
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "雀儿异常：" + e.GetType().Name); }
        }

        // ───────────── 到店后摆货（0.8.0 的 FlushPendingWares：对话结束就摆，不是成交后才摆） ─────────────

        private static float _waresTimer = -1f;
        private static string _waresSeller;
        private static bool _waresStolen;

        /// <summary>卖方到店后延迟 2.5 秒摆货（等他走到柜台前）。</summary>
        public static void QueueWares(string id, bool stolen)
        {
            _waresSeller = id;
            _waresStolen = stolen;
            _waresTimer = 2.5f;
            Debug.Log(FigurineMod.LogTag + "卖家到店，2.5 秒后摆货：" + id);
        }

        public static void Tick()
        {
            // 兜底：没收到到店回调，但这位已经站在窗口了 → 立刻开始摆货计时
            if (_armed)
            {
                try
                {
                    var cur = PlayerStore.Instance != null ? PlayerStore.Instance.currentClientInstance : null;
                    var bp = cur != null ? cur.GetClientBlueprint() : null;
                    if (bp != null && string.Equals(bp.identifier, _armId, StringComparison.Ordinal))
                    {
                        _armed = false;
                        QueueWares(_armId, _armStolen);
                    }
                }
                catch (System.Exception __ex) { Safe.Warn("CustomerSellers.TickArrive", __ex); }
            }
            if (_waresTimer < 0f) return;
            _waresTimer -= Time.unscaledDeltaTime;
            if (_waresTimer > 0f) return;
            _waresTimer = -1f;
            string id = _waresSeller;
            _waresSeller = null;
            if (!string.IsNullOrEmpty(id)) PlaceWares(id, _waresStolen);
        }

        /// <summary>摆出 1~4 件手办（雀儿可能带赃物、可能带赝品）。</summary>
        /// <summary>摆到前柜台：先自己调 TryAcceptAllMid 判断，避免 AddDirectSellingItemToTableMiddle 内部静默销毁。</summary>
        private static bool PlaceOnCounter(GameItem item)
        {
            try
            {
                if (item == null) return false;
                var ps = PlayerStore.Instance;
                if (ps == null) return false;
                // 走原版卖货入口（鉴定客用的就是这条，实测 3×3 能放下；TryAcceptAllMid 不行）
                ps.AddDirectSellingItemToTable(item, false);
                return item.parentInventory != null;
            }
            catch (System.Exception __ex) { Safe.Warn("CustomerSellers.PlaceOnCounter", __ex); return false; }
        }

        public static void PlaceWares(string id, bool stolen)
        {
                try
                {
                    if (string.IsNullOrEmpty(id)) return;
                    int n = UnityEngine.Random.Range(2, 7);   // 2~6 件（用户 2026-11-03 定）
                    var ps = PlayerStore.Instance;
                    if (ps == null) return;
                    int placed = 0, movedToBack = 0, movedToMod = 0, lost = 0;
                    for (int i = 0; i < n; i++)
                    {
                        var pool = Defs.All();
                    if (pool.Count == 0) return;
                    var def = pool[UnityEngine.Random.Range(0, pool.Count)];
                        var item = FigurineMod.BuildFigurine(def);
                        if (item == null) continue;
                        // 三态：真身 7:3、标签 25% 错标（用户 2026-11-04 定）
                        // —— 原来那条「20% 掷赝品」已并入这里，不再单独 MakeFake
                        try { FigurineAuth.Apply(item, FigurineAuth.RollFor(FigurineAuth.Source.CustomerSeller, def.Id)); }
                        catch (System.Exception __ex) { Safe.Warn("CustomerSellers.Auth", __ex); }
                        if (stolen && UnityEngine.Random.value < 0.28f)
                        {
                            try { item.EnableTag("STOLEN_TAG", true); } catch (System.Exception __ex) { Safe.Warn("CustomerSellers.cs:151", __ex); }
                        }
                        if (PlaceOnCounter(item)) placed++;
                        else
                        {
                            // 柜台放不下：绝不静默销毁（问题 A4）。
                            // 先尝试转到后仓（玩家仓库），仍然放不下才销毁，并且一定打日志。
                            // 柜台放不下：停止摆货，剩下的不再造（避免白造白销毁）
                            _wFull = true;
                            bool toBack = false;
                            try
                            {
                                // 卖家的货只放前柜台；放不下就丢弃（与 0.8.0 一致），不进玩家后仓/仓库

                            }
                            catch (System.Exception __ex) { Safe.Warn("CustomerSellers.ToBack", __ex); }

                            // 方案 A：柜台放不下就收手 —— 丢掉这一件，后面的不再造
                            lost++;
                            Debug.LogWarning(FigurineMod.LogTag + "柜台放不下，丢弃这一件并停止摆货：" + def.Id);
                            try { item.Destroy(); } catch { }
                            break;
                        }
                    }
                    if (_wFull) Debug.LogWarning(FigurineMod.LogTag + "柜台放不下，本次只摆了 " + placed + " 件（计划 " + n + " 件）");
                    _wFull = false;
                    Debug.Log(FigurineMod.LogTag + id + " 摆出 " + placed + " 件手办（" + n + " 件中成功 " + placed
                        + (movedToBack > 0 ? "，转原版仓库 " + movedToBack : "") + (movedToMod > 0 ? "，转模组仓库 " + movedToMod : "") + (lost > 0 ? "，销毁 " + lost : "") + "）");
                    ActiveSeller = null;
#if !NO_FAKES
                    Rewards.Clear();
#endif
                }
                catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "摆货异常：" + e.GetType().Name); }
        }
    }
}
