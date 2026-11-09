using System;
using System.Collections.Generic;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>新顾客：米拉（元气冒险家/游客，买方，收艺术品+奢侈品，现金 3000）。</summary>
    internal static class CustomerMira
    {
        public const string ClientId = "figurine_enthusiast_tourist";

        public static void TrySchedule(StoreClient.ClientIntent? forced = null, string onlyBuyId = null, string barterTargetId = null)
        {
            try
            {
                var mgr = PlayerStore.Instance != null ? PlayerStore.Instance.storeClientManager : null;
                if (mgr == null) return;

                var c = new StoreClient();
                c.identifier = ClientId;
                c.displayName = "米拉";
                c.realName = "米拉";
                c.dismissable = true;
                c.isBargainAvailable = true;
                c.tag = new List<string>();
                c.clientFaction = StoreClient.FACTION_TOURIST;
                c.clientIntent = forced ?? StoreClient.ClientIntent.BUY;
                bool buySide = c.clientIntent == StoreClient.ClientIntent.BUY || c.clientIntent == StoreClient.ClientIntent.SELLNBUY;
                c.clientCash = 3000;
                // 买方/买卖都做：打开 useClientBudget → 原版交易面板会显示「顾客预算」（用户 2026-11-05 要求对齐原版）。
                // 纯卖方不开（她不买东西，原版出售界面本来也不显示顾客预算）。
                if (buySide) { try { c.SetBudget(3000); } catch (System.Exception __ex) { Safe.Warn("CustomerMira.cs:26", __ex); } }
                else c.useClientBudget = false;

                string sp = CustomerSprites.Get(ClientId);
                c.spriteName = sp;
                if (!string.IsNullOrEmpty(sp)) c.possibleSprites = new List<string> { sp };

                if (UnityEngine.Random.value < 0.30f) { try { c.SetVariety(); } catch (System.Exception __ex) { Safe.Warn("CustomerMira.cs:36", __ex); } }   // 旧版：到店随机判定「种类多样」
                c.clientBuyingTagList = new List<string> { "ART", "LUXURY_ITEM" };
                c.clientBuyingIdList = new List<string>();
                foreach (var d in Defs.All()) c.clientBuyingIdList.Add(d.Id);   // 三 IP 全收
                c.clientBlackTagList = new List<string> { "STOLEN_TAG" };
                if (!string.IsNullOrEmpty(onlyBuyId))
                {
                    c.clientBuyingIdList = new List<string> { onlyBuyId };
                    c.clientBuyingTagList = new List<string>();
                }
                c.clientBlackIdList = new List<string>();

                try { c.AddBasicDialog(); } catch (System.Exception __ex) { Safe.Warn("CustomerMira.cs:48", __ex); }
                try { BargainLines.ApplyFor(ClientId); } catch (System.Exception __ex) { Safe.Warn("CustomerMira.cs:49", __ex); }
                int voiceMode = c.clientIntent == StoreClient.ClientIntent.SELL ? CustomerVoice.ModeSell
                              : c.clientIntent == StoreClient.ClientIntent.SELLNBUY ? CustomerVoice.ModeBoth
                              : CustomerVoice.ModeBuy;
                // 0.8.0 原版台词：16 个池全部挂到 StoreClient 对应字段（开场/成交/收尾/复访/摆错/摆对/盘问/放大镜/被抓）
                CustomerVoice.Apply(c, ClientId, "米拉", voiceMode,
                    buySide
                        ? "哇——这地方居然有这种货！\n我在别的星区从没见过这种涂装。\n能让我仔细瞧瞧吗？"
                        : "我从别的星区淘了点东西。\n你看看有没有你感兴趣的？\n价钱好说。",
                    "太好了，我收下了！",
                    "下次还来，你可得给我留货。");

                // 卖方 / 买卖都做 都要带货；买卖都做同时也要带钱（上面 SetBudget 已给）
                bool hasWares = c.clientIntent == StoreClient.ClientIntent.SELL || c.clientIntent == StoreClient.ClientIntent.SELLNBUY;
                if (hasWares) CustomerSellers.ArmWares(ClientId, false);

                try { ModHelper.SetClientSource(c, "figurine.gs"); } catch (System.Exception __ex) { Safe.Warn("CustomerMira.cs:60", __ex); }
                try { mgr.AddNextClient(c); } catch (System.Exception __ex) { Safe.Warn("AddNextClient", __ex); }   // 必须插队首：clientStack.Add 会排到当天队尾且跳过 TrackClient
                if (!string.IsNullOrEmpty(barterTargetId)) { try { Barter.Attach(c, barterTargetId); } catch (System.Exception __ex) { Safe.Warn("CustomerMira.cs:62", __ex); } }
                Debug.Log(FigurineMod.LogTag + "米拉已到店（" + (voiceMode == CustomerVoice.ModeBoth ? "买卖都做" : voiceMode == CustomerVoice.ModeSell ? "卖方" : "买方") + "，3000）");
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "米拉入队失败：" + e.GetType().Name + " | " + e.Message); }
        }
    }
}
