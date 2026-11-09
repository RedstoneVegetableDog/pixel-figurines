using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>海子：名片、第 1 天送礼、每周到访、电话 3721（交易清单/盲盒后续补）。</summary>
    internal static class Haizi
    {
        public const string ClientId = "figurine_haizi";

        // 0.8.0 `CustomerDef` 里的三段原文（到店 / 成交 / 离开），逐字照抄
        public const string MainText = "在下海子，做点二次元周边的小生意。\n"
            + "听说您这儿收着不少好东西 —— 手办我按成色加价收，\n"
            + "我这边也带了些货，您可以挑挑。";
        public const string DealText = "承蒙惠顾。";
        /// <summary>首次到店（第 1 天送礼那趟）的固定开场。</summary>
        public const string FirstVisitText = "你好，我是海子，有缘路过，看你好像是新店开业，送些礼物给你。";
        public const string DoneText = "改日再来叨扰。";
        public const string CardId = "figurine_gs_haizi_card";
        public const long PhoneNumber = 3721;
        public const string ServiceText =
            "二次元周边 · 少年商人\n\n" +
            "收货：手办（仅收干净货，赝品与赃物不收）\n" +
            "      按稀有度另加价：1★ +1~3% / 2★ +4~6% / 3★ +7~9%\n" +
            "                    4★ +10~12% / 5★ +13~15%\n\n" +
            "出货：手办 3~5 件（2★ 以上）· 盲盒 1 个 · 展示柜 1 个\n" +
            "      盲盒与展示柜买走即补货\n\n" +
            "到店：第 1 天登门（送礼，不做买卖）\n" +
            "      之后每 7 天来一趟（第 7、14、21… 天）\n" +
            "      也可随时来电：3721（当天最后一位到店，不占客流）\n\n" +
            "—— 海子";

        private static int _giftDay = -1;
        private static int _giftQueuedDay = -1;
        private static bool _pendingGift;
        private static float _giftTimer = -1f;
        private static bool _loaded;

        private static string FilePath
        {
            get { try { return Path.Combine(Application.persistentDataPath, "figurine_haizi.txt"); } catch { return "figurine_haizi.txt"; } }
        }

        private static int Slot
        {
            get { try { return PlayerStore.Instance != null ? PlayerStore.Instance.saveSlotId : 0; } catch { return 0; } }
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (ModData.GetLines("haizi").Length == 0) return;
                foreach (var line in ModData.GetLines("haizi"))
                {
                    var p = line.Split('|');
                    if (p.Length >= 2 && int.TryParse(p[0], out var slot) && slot == Slot)
                        int.TryParse(p[1], out _giftDay);
                }
            }
            catch (System.Exception __ex) { Safe.Warn("Haizi.cs:66", __ex); }
        }

        private static void Save()
        {
            try { ModData.Set("haizi", Slot + "|" + _giftDay + "\n"); } catch (System.Exception __ex) { Safe.Warn("Haizi.cs:71", __ex); }
        }

        public static void RegisterItems(ModItemDirectory dir)
        {
            if (dir == null) return;
            try { dir.Add(CardId, (Func<GameItem>)(() => BuildCard())); } catch (System.Exception __ex) { Safe.Warn("Haizi.cs:77", __ex); }
        }

        public static GameItem BuildCard()
        {
            try
            {
                var item = ItemDirectory.CreateEmptyItem(CardId);
                if (item == null) return null;
                item.name = "海子的名片";
                item.shortDescription = "双击：查看名片原图";
                item.unitCount = 1;
                item.unitValue = 0;
                item.unitBaseValue = 0;
                // 占格交给贴图推导（贴图 96×48 = 2×1 格），不再手动 SetShape
                item.SetSpriteAndShapeFromMod("figurine.gs", CardId);
                try { item.SetGameItemType("DOCUMENT"); } catch { try { item.EnableTag("DOCUMENT", true); } catch (System.Exception __ex) { Safe.Warn("Haizi.cs:93", __ex); } }
                try { GeneralHelper.SetItemOwned(item, true); } catch (System.Exception __ex) { Safe.Warn("Haizi.cs:94", __ex); }
                return item;
            }
            catch { return null; }
        }

        public static bool HasCardOwned()
        {
            // 名片可能在柜台/桌上，不在背包；用过存档标记兜底
            try { if (ModData.Get("haizi_card", "") == "1") return true; } catch (System.Exception __ex) { Safe.Warn("Haizi.cs:103", __ex); }
            try
            {
                var ps = PlayerStore.Instance;
                return ps != null && ps.IsPlayerOwnThisItem(CardId);
            }
            catch { return false; }
        }

        public static void ResetSession()
        {
            _giftQueuedDay = -1;
            _calledDay = -1;
            _pendingGift = false;
            Debug.Log(FigurineMod.LogTag + "海子：读档重置会话标记");
        }

        public static void OnShopOpened()
        {
            try
            {
                Load();
                EnsurePhone();
                int day = StoreStation.GetDayCounter();
                Debug.Log(FigurineMod.LogTag + "海子 OnShopOpened day=" + day + " giftDay=" + _giftDay);
                if (day < 1) return;
                if (day == 1 && _giftDay == 1 && !HasCardOwned())
                {
                    _giftDay = -1;
                    Save();
                    Debug.Log(FigurineMod.LogTag + "海子：上个版本误记已送礼，且背包没有名片 → 重置");
                }
                if (day == 1 && _giftQueuedDay != day)
                {
                    _giftQueuedDay = day;
                    _pendingGift = true;
                    // 第 1 天只做「首次到店 + 离店」，不做买卖：意图设成对话、清空收购列表
                    Queue(FirstVisitText, true, dialogueOnly: true);
                    Debug.Log(FigurineMod.LogTag + "海子：第 1 天已排送礼局（到店后 2.5 秒放柜台）");
                    return;
                }
                // 每周到访：同一家店当天反复开门也只排一次（以前每次 OnShopOpened 都 Queue → 一天多个海子）
                if (day >= 7 && day % 7 == 0 && !CalledToday())
                {
                    MarkCalledToday();
                    Queue(MainText);
                }
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "海子异常：" + e.GetType().Name); }
        }

        /// <summary>今天是否已经排过海子（每周到访 / 电话叫店共用）。以前 `_calledToday` 读档才重置，
        /// 导致「电话叫过一次之后，之后每天都提示今天已经安排过了」。</summary>
        private static int _calledDay = -1;

        private static bool CalledToday()
        {
            try { return _calledDay == StoreStation.GetDayCounter(); }
            catch { return false; }
        }

        private static void MarkCalledToday()
        {
            try { _calledDay = StoreStation.GetDayCounter(); }
            catch { _calledDay = -1; }
        }

        public static void OnArrived()
        {
            try
            {
                if (_pendingGift)
                {
                    _pendingGift = false;
                    _giftTimer = 2.5f;
                    Debug.Log(FigurineMod.LogTag + "海子已到店（送礼局），2.5 秒后放柜台");
                    return;
                }
                try { HaiziPricing.Roll(); } catch (System.Exception __ex) { Safe.Warn("Haizi.cs:162", __ex); }
                PlaceWares();
            }
            catch (System.Exception __ex) { Safe.Warn("Haizi.cs:165", __ex); }
        }

        public static void ScheduleNow()
        {
            try
            {
                int day = StoreStation.GetDayCounter();
                Debug.Log(FigurineMod.LogTag + "海子 OnShopOpened day=" + day + " giftDay=" + _giftDay);
                if (day < 1) return;
                if (CalledToday()) return;
                MarkCalledToday();
                Queue(MainText);
                Debug.Log(FigurineMod.LogTag + "海子：电话叫到店（第 " + day + " 天）");
            }
            catch (System.Exception __ex) { Safe.Warn("Haizi.cs:180", __ex); }
        }

        /// <summary>首日礼物用的盲盒池：保持原来的 4 款基础盒（礼物没要求加高级档）。</summary>
        private static readonly string[] GiftBoxIds =
        {
            BlindBox.IdNormal, BlindBox.IdGs, BlindBox.IdSr, BlindBox.IdZzz
        };

        /// <summary>海子到店摆的盲盒：从全部 8 种里随机（4 基础 + 4 高级，用户 2026-11-05 定）。</summary>
        private static string RandomBoxId()
        {
            return BlindBox.RandomId();
        }

        private static string RandomGiftBoxId()
        {
            return GiftBoxIds[UnityEngine.Random.Range(0, GiftBoxIds.Length)];
        }

        private static void PlaceWares()
        {
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null) return;
                // 盲盒随机一款（以前永远 BlindBox.Build() = 原神盒）
                string boxId = RandomBoxId();
                var box = BlindBox.Build(boxId);
                bool boxOk = box != null && Placement.TryFront(box, false);
                var cab = Cabinet.Build();
                bool cabOk = cab != null && Placement.TryFront(cab, false);
                if (box != null && !boxOk) { try { box.Destroy(); } catch { } }
                if (cab != null && !cabOk) { try { cab.Destroy(); } catch { } }
                Debug.Log(FigurineMod.LogTag + "海子到店摆货：盲盒（" + boxId + "）" + (boxOk ? "1" : "0")
                    + " + 展示柜 " + (cabOk ? "1" : "0")
                    + (boxOk && cabOk ? "" : "（前柜台位置不够，没摆上的已跳过）"));
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "海子摆货失败：" + e.GetType().Name); }
        }

        public static void Tick()
        {
            try
            {
                try { HaiziRestock.Flush(); } catch (System.Exception __ex) { Safe.Warn("Haizi.cs:202", __ex); }
                if (_giftTimer <= 0f) return;
                _giftTimer -= Time.unscaledDeltaTime;
                if (_giftTimer > 0f) return;
                _giftTimer = -1f;
                GiveDayOneGift();
            }
            catch (System.Exception __ex) { Safe.Warn("Haizi.cs:209", __ex); }
        }

        private static void GiveDayOneGift()
        {
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null) return;
                int giftOk = 0, giftTotal = 0;
                var card = BuildCard();
                giftTotal++;
                if (card != null && Placement.TryFront(card, true)) giftOk++;
                for (int i = 0; i < 2; i++)
                {
                    // 随机发一种基础盲盒（普通 / 原神 / 星铁 / 绝区零；礼物池没加高级档）
                    giftTotal++;
                    var box = BlindBox.Build(RandomGiftBoxId());
                    if (box != null && Placement.TryFront(box, true)) giftOk++;
                }
                var cab = Cabinet.Build();
                giftTotal++;
                if (cab != null && Placement.TryFront(cab, true)) giftOk++;
                try { ModData.Set("haizi_card", "1"); } catch (System.Exception __ex) { Safe.Warn("Haizi.cs:229", __ex); }
                // 额外送一个原版放大镜（用户 2026-11-05 要求）
                try
                {
                    giftTotal++;
                    var lens = DirectoryMaster.Item("magnifier");
                    if (lens != null && Placement.TryFront(lens, true)) giftOk++;
                }
                catch (System.Exception __ex) { Safe.Warn("Haizi.Lens", __ex); }
                Debug.Log(FigurineMod.LogTag + "海子：第 1 天礼物已放柜台 " + giftOk + "/" + giftTotal
                    + "（名片+2盲盒+展示柜+放大镜）");
                _giftDay = 1;
                Save();
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "海子送礼失败：" + e.GetType().Name); }
        }

        private static string InjectPortrait()
        {
            try
            {
                var dict = SpriteDict.Instance.spriteDictionary;
                if (dict != null && dict.ContainsKey("haizi_portrait")) return "haizi_portrait";
                var mods = ModLoader.Mods;
                if (mods != null)
                    foreach (var m in mods)
                    {
                        if (m == null || m.Manifest == null || m.Manifest.ID != "figurine.gs") continue;
                        Sprite direct = null;
                        try { if (m.Sprites != null && m.Sprites.TryGetValue("haizi_portrait", out direct) && direct != null) { dict["haizi_portrait"] = direct; return "haizi_portrait"; } } catch (System.Exception __ex) { Safe.Warn("Haizi.cs:249", __ex); }
                        if (m.Textures != null && m.Textures.TryGetValue("haizi_portrait", out var tex) && tex != null)
                        {
                            var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                            dict["haizi_portrait"] = sp;
                            return "haizi_portrait";
                        }
                    }
            }
            catch (System.Exception __ex) { Safe.Warn("Haizi.cs:258", __ex); }
            try { return SpriteDict.GetRandomLowerLevelSpriteName(); } catch (System.Exception __ex) { Safe.Warn("Haizi.cs:259", __ex); }
            return null;
        }

        private static bool _poolReady;

        /// <summary>
        /// 把海子的 0.8.0 原文填进 16 池结构：到店/成交/离开三段挂上，
        /// 其余池留空 —— 空池不会被 `CustomerVoice` 写入，游戏就继续用原版对话。
        /// </summary>
        private static void EnsurePool()
        {
            if (_poolReady) return;
            _poolReady = true;
            // 16 个池全部由 HaiziLines.g.cs 填（到店/成交/离开的 0.8.0 原文作为每池第一条）
            var pool = new CustomerLines.Pool();
            try { HaiziLines.Fill(pool); } catch (System.Exception __ex) { Safe.Warn("Haizi.Fill", __ex); }
            if (pool.BuyMain == null) pool.BuyMain = new[] { MainText };
            if (pool.BuyDeal == null) pool.BuyDeal = new[] { DealText };
            if (pool.BuyDone == null) pool.BuyDone = new[] { DoneText };
            CustomerLines.Register(ClientId, pool);
            Debug.Log(FigurineMod.LogTag + "海子台词池已登记（16 池：3 段 0.8.0 原文 + 10 池按人设撰写）");
        }

        private static void Queue(string text, bool forceMain = false, bool dialogueOnly = false)
        {
            try
            {
                var mgr = PlayerStore.Instance != null ? PlayerStore.Instance.storeClientManager : null;
                if (mgr == null) return;
                var c = new StoreClient();
                c.identifier = ClientId;
                c.displayName = "海子";
                c.clientFaction = StoreClient.FACTION_TOURIST;
                // 首日送礼局只对话；之后的每周到访 / 电话叫来 = 买卖都做（用户 2026-11-05 定）。
                // 以前这里固定 BUY：他带的盲盒/展示柜摆在柜台却结算成「买方」界面 → 玩家买不了。
                c.clientIntent = dialogueOnly ? StoreClient.ClientIntent.DIALOGUE : StoreClient.ClientIntent.SELLNBUY;
                c.clientCash = 0;
                c.useClientBudget = false;
                string sp = InjectPortrait();
                c.spriteName = sp;
                if (!string.IsNullOrEmpty(sp)) c.possibleSprites = new List<string> { sp };
                c.clientBuyingIdList = new List<string>();
                c.clientBuyingTagList = new List<string>();
                c.clientBlackIdList = new List<string>();
                c.clientBlackTagList = new List<string>();
                if (!dialogueOnly)
                {
                    // 买侧（服务说明「收货：手办」）：只认 276 件手办 id（不按 ART 标签全收，
                    // 否则香水/画作等原版艺术品也会被他买走）；脏货/赝品由 RefusalVeto 拦。预算 3000。
                    try
                    {
                        c.clientBuyingTagList = new List<string>();
                        c.clientBuyingIdList = new List<string>();
                        foreach (var d in Defs.All()) c.clientBuyingIdList.Add(d.Id);
                        c.SetBudget(3000);
                        c.clientCash = 3000;
                    }
                    catch (System.Exception __ex) { Safe.Warn("Haizi.BuySide", __ex); }
                }
                // 走统一的 16 池管线（买/卖共用同一批台词：海子只有一段原文）
                try
                {
                    EnsurePool();
                    CustomerVoice.Apply(c, ClientId, "海子", dialogueOnly ? CustomerVoice.ModeBuy : CustomerVoice.ModeBoth, text, DealText, DoneText);
                    if (forceMain)
                    {
                        var df = new Dialogue(); df.SetText("海子", text); c.mainDialogue = df;
                    }
                }
                catch
                {
                    var d = new Dialogue(); d.SetText("海子", text); c.mainDialogue = d;
                    var dd = new Dialogue(); dd.SetText("海子", DealText); c.acceptDealDialogue = dd;
                    var de = new Dialogue(); de.SetText("海子", DoneText); c.allDoneDialogue = de;
                }
                try { ModHelper.SetClientSource(c, "figurine.gs"); } catch (System.Exception __ex) { Safe.Warn("Haizi.cs:324", __ex); }
                try { mgr.AddNextClient(c); } catch (System.Exception __ex) { Safe.Warn("AddNextClient", __ex); }   // 必须插队首：clientStack.Add 会排到当天队尾且跳过 TrackClient
                if (!dialogueOnly)
                {
                    int budget = 0;
                    try { budget = c.GetBudget(); } catch (System.Exception __ex) { Safe.Warn("Haizi.BudgetLog", __ex); }
                    Debug.Log(FigurineMod.LogTag + "海子已排队：买卖都做（收货清单 " + c.clientBuyingIdList.Count + " 件，预算 " + budget + "）");
                }
            }
            catch (System.Exception __ex) { Safe.Warn("Haizi.cs:327", __ex); }
        }

        private static void EnsurePhone()
        {
            try
            {
                PhoneHooks.SyncContact();
            }
            catch (System.Exception __ex) { Safe.Warn("Haizi.cs:336", __ex); }
        }


        private static Dialogue PhoneChoice(string label)
        {
            var d = new Dialogue(); d.SetText("海子", label); return d;
        }

        private static void ShowTasks()
        {
            try
            {
                var list = TaskList.Today(CustomerMira.ClientId);
                var d = new Dialogue();
                d.SetText("海子", TaskList.Text("米拉", list));
                DialoguePhoneUIManager.Instance.StartDialogue(d);
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "委托清单失败：" + e.GetType().Name); }
        }

        private static void CallSeller(bool bailu)
        {
            try
            {
                string id = bailu ? CustomerSellers.BailuId : CustomerSellers.QueerId;
                string taskId = TaskList.FirstId(id);
                if (string.IsNullOrEmpty(taskId)) return;
                if (bailu) CustomerSellers.TryBailu(StoreClient.ClientIntent.BUY, taskId);
                else CustomerSellers.TryQueer(StoreClient.ClientIntent.BUY, taskId);
                Debug.Log(FigurineMod.LogTag + "海子：已叫" + (bailu ? "白鹭" : "雀儿") + "收货 " + taskId);
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "叫顾客失败：" + e.GetType().Name); }
        }


        private static void CallMira()
        {
            try
            {
                string id = TaskList.FirstId(CustomerMira.ClientId);
                if (string.IsNullOrEmpty(id)) return;
                CustomerMira.TrySchedule(StoreClient.ClientIntent.BUY, id);
                Debug.Log(FigurineMod.LogTag + "海子：已叫米拉上门收货 " + id);
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "叫米拉失败：" + e.GetType().Name); }
        }

        private static void BuyBox()
        {
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null || !ps.CanPay(300)) return;
                ps.PayMoney(300, false);
                var box = BlindBox.Build();
                if (box != null)
                {
                    var inv = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.invElement : null;
                    FigurineMod.TryPlace(inv, box);
                }
            }
            catch (System.Exception __ex) { Safe.Warn("Haizi.cs:398", __ex); }
        }

        /// <summary>
        /// 3721 通话树 —— 完全照 0.8.0 原版（`HaiziMerchant` 里那段 GetCallDialog 前缀）：
        /// ```
        /// 海子的助手：您好，这里是海子先生的联络处。请问需要什么？
        ///   [叫海子来店] → 好的，我这就转告先生，他今天会过去一趟。 / 今天已经安排过了，先生随后就到。
        ///   [任务清单]   → 好的，您想看谁的清单？
        ///                     [海子任务] / [顾客任务] → 这几位最近都想要东西，您看哪位？→ [XX任务]
        ///   [结束通话]   → 那就先不打扰您了。
        /// ```
        /// </summary>
        /// <summary>构造电话树时的递归守卫（见 BuildPhoneDialog 注释）。</summary>
        private static bool _buildingPhoneDialog;

        /// <summary>自检用：构造一次电话树（不显示），验证不会递归爆栈。</summary>
        public static Dialogue BuildPhoneDialogForTest() { return BuildPhoneDialog(); }

        /// <summary>
        /// 电话 UI 把 `choices[i].Item2.plainText` 当作**选项文字**（不是物品名字）。
        /// 所以每个选项都要包一层「玩家台词」，回复挂在它的 nextDialogue 上 —— 这是原版写法。
        /// </summary>
        private static Dialogue Opt(string label, Dialogue after)
        {
            var d = new Dialogue();
            d.SetText("PLAYER", label);
            if (after != null) d.SetNextDialogue(after);
            return d;
        }

        private static Dialogue BuildPhoneDialog()
        {
            // 递归守卫：任务菜单的「返回」曾经又建电话树 → 构造期无限递归（已改为终止页）
            if (_buildingPhoneDialog) return BuildPhoneShallow();

            _buildingPhoneDialog = true;
            try
            {
                var d = new Dialogue();
                d.SetText("海子的助手", "您好，这里是海子先生的联络处。请问需要什么？");

                d.AddChoice(BuildCard(), "", "",
                    Opt("[叫海子来店]", PhoneChoice(CalledToday() ? "今天已经安排过了，先生随后就到。" : "好的，我这就转告先生，他今天会过去一趟。")),
                    (Action)(() => ScheduleNow()), (Func<bool>)(() => true));

                d.AddChoice(BuildCard(), "", "", Opt("[任务清单]", BuildTaskMenu()), null, (Func<bool>)(() => true));

                d.AddChoice(BuildCard(), "", "",
                    Opt("[结束通话]", PhoneChoice("那就先不打扰您了。")), (Action)(() => { }), (Func<bool>)(() => true));
                return d;
            }
            finally { _buildingPhoneDialog = false; }
        }

        /// <summary>浅版电话树：不带任务菜单（避免递归）。</summary>
        private static Dialogue BuildPhoneShallow()
        {
            var d = new Dialogue();
            d.SetText("海子的助手", "您好，这里是海子先生的联络处。请问需要什么？");
            d.AddChoice(BuildCard(), "", "",
                Opt("[叫海子来店]", PhoneChoice(CalledToday() ? "今天已经安排过了，先生随后就到。" : "好的，我这就转告先生，他今天会过去一趟。")),
                (Action)(() => ScheduleNow()), (Func<bool>)(() => true));
            d.AddChoice(BuildCard(), "", "",
                Opt("[任务清单]", PhoneChoice("（先挂断，重新拨打可看任务清单）")), (Action)(() => { }), (Func<bool>)(() => true));
            d.AddChoice(BuildCard(), "", "",
                Opt("[结束通话]", PhoneChoice("那就先不打扰您了。")), (Action)(() => { }), (Func<bool>)(() => true));
            return d;
        }

        private static Dialogue BuildTaskMenu()
        {
            var d = new Dialogue();
            d.SetText("海子的助手", "好的，您想看谁的清单？");
            d.AddChoice(BuildCard(), "", "", Opt("[海子任务]", TaskPage("figurine_haizi", "海子")), null, (Func<bool>)(() => true));

            var d2 = new Dialogue();
            d2.SetText("海子的助手", "这几位最近都想要东西，您看哪位？");
            d2.AddChoice(BuildCard(), "", "", Opt("[雀儿任务]", TaskPage(CustomerSellers.QueerId, "雀儿")), null, (Func<bool>)(() => true));
            d2.AddChoice(BuildCard(), "", "", Opt("[白鹭任务]", TaskPage(CustomerSellers.BailuId, "白鹭")), null, (Func<bool>)(() => true));
            d2.AddChoice(BuildCard(), "", "", Opt("[米拉任务]", TaskPage(CustomerMira.ClientId, "米拉")), null, (Func<bool>)(() => true));

            d.AddChoice(BuildCard(), "", "", Opt("[顾客任务]", d2), null, (Func<bool>)(() => true));
            // 交货菜单直接作为对话分支挂上去（不再用 StartDialogue 硬顶，那样会被电话 UI 覆盖）
            d.AddChoice(BuildCard(), "", "", Opt("[交货]", TaskFlow.BuildMenu()), null, (Func<bool>)(() => true));
            d.AddChoice(BuildCard(), "", "", Opt("[返回]", BuildPhoneShallow()), null, (Func<bool>)(() => true));
            return d;
        }

        /// <summary>终止页：只显示一句话 + 结束通话，不再构造任何子菜单（切断递归）。</summary>
        private static Dialogue BuildFlatEnd(string text)
        {
            var d = new Dialogue();
            d.SetText("海子的助手", text);
            d.AddChoice(BuildCard(), "", "", Opt("[结束通话]", PhoneChoice("那就先不打扰您了。")), (Action)(() => { }), (Func<bool>)(() => true));
            return d;
        }

        private static Dialogue TaskPage(string ownerId, string ownerName, int page = 0)
        {
            var d = new Dialogue();
            try
            {
                var list = TaskList.Today(ownerId);
                var todo = new System.Collections.Generic.List<Catalog.Def>();
                foreach (var def in list)
                    if (!TaskFlow.Has(ownerId, def.Id)) todo.Add(def);

                string title = TaskList.Text(ownerName, todo.Count > 0 ? todo : list);
                int total = todo.Count;
                if (total == 0)
                {
                    d.SetText("海子的助手", title);
                    d.AddChoice(BuildCard(), "", "", Opt("[返回]", BuildPhoneShallow()), null, (Func<bool>)(() => true));
                    return d;
                }

                int cur = ((page % total) + total) % total;
                var item = todo[cur];
                if (total > 1) title += "（第 " + (cur + 1) + "/" + total + " 页）";
                d.SetText("海子的助手", title);

                string id = item.Id, zh = item.Zh;
                d.AddChoice(BuildCard(), "", "",
                    Opt("[接·" + zh + "]", PhoneChoice("记下了。" + zh + " 到手后再打给我。")),
                    (Action)(() => { try { TaskFlow.Accept(ownerId, id); } catch (System.Exception __ex) { Safe.Warn("Haizi.Accept", __ex); } }),
                    (Func<bool>)(() => true));

                // 最后一页不再给「翻页」（cur+1 取模会回 0 → 构造期无限递归）
                if (total > 1 && cur + 1 < total)
                    d.AddChoice(BuildCard(), "", "", Opt("[翻页]", TaskPage(ownerId, ownerName, cur + 1)), null, (Func<bool>)(() => true));

                d.AddChoice(BuildCard(), "", "", Opt("[返回]", BuildPhoneShallow()), null, (Func<bool>)(() => true));
            }
            catch { d.SetText("海子的助手", "今天没有清单。"); }
            return d;
        }

        private static void TryLogBubbleCapacity(Dialogue d)
        {
            try
            {
                int choices = d != null ? d.choices.Count : -1;
                int bubbles = -1;
                try
                {
                    var ui = DialoguePhoneUIManager.Instance;
                    if (ui != null && ui.dialogChoiceBubbles != null) bubbles = ui.dialogChoiceBubbles.Count;
                }
                catch (System.Exception __ex) { Safe.Warn("Haizi.cs:507", __ex); }
                Debug.Log(FigurineMod.LogTag + "电话容量探测：本页选项 " + choices + " 个 / UI 气泡槽 " + bubbles + " 个"
                    + (bubbles > 0 && choices > bubbles ? "  ⚠ 会溢出" : ""));
            }
            catch (System.Exception __ex) { Safe.Warn("Haizi.cs:511", __ex); }
        }
        [HarmonyPatch(typeof(StorePhoneClient), nameof(StorePhoneClient.GetCallDialog))]
        internal static class PhoneDialogPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(StorePhoneClient __instance, ref Dialogue __result)
            {
                try
                {
                    if (__instance == null || __instance.dialogFuncId != PhoneHooks.HaiziDialogId) return true;
                    if (PhoneHooks.AtWindow(PhoneHooks.HaiziNumber))
                    {
                        var busy = new Dialogue();
                        busy.SetText("海子的助手", PhoneHooks.BusyText(PhoneHooks.HaiziNumber));
                        __result = busy;
                        return false;
                    }
                    __result = BuildPhoneDialog();
                    TryLogBubbleCapacity(__result);
                    return false;
                }
                catch { return true; }
            }

            private static Dialogue Choice(string text)
            {
                var d = new Dialogue(); d.SetText("海子", text); return d;
            }
        }

    }
}
