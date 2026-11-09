using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>DORO：第 1 天送终端机+货物邮箱（一套 2000）、周六要账；电话 3000 可购买/扩容。</summary>
    internal static class Doro
    {
        public const string ClientId = "doro_delivery";
        private const string SpriteKey = "doro_courier";

        private class State
        {
            public bool offered;
            public int remaining;
            public int lastCollectDay;
            public bool purchased;
            public bool terminalDelivered;   // beta 的 terminalDelivered
            public int pendingDay;           // beta 的 terminalDueDay（送货到期日）
        }

        private static Dictionary<int, State> _slots;
        private static float _cardAt = -1f;
        private static bool _deliverQueued;
        private static int _queuedDay = -1;

        public static void ResetSession()
        {
            _queuedDay = -1;
            _deliverQueued = false;
            Debug.Log(FigurineMod.LogTag + "DORO：读档重置会话标记");
        }

        private static string FilePath
        {
            get { try { return Path.Combine(Application.persistentDataPath, "figurine_doro.txt"); } catch { return "figurine_doro.txt"; } }
        }

        private static int SlotId
        {
            get { try { return PlayerStore.Instance != null ? PlayerStore.Instance.saveSlotId : 0; } catch { return 0; } }
        }

        private static State Cur
        {
            get
            {
                if (_slots == null)
                {
                    _slots = new Dictionary<int, State>();
                    try
                    {
                        if (ModData.GetLines("doro").Length > 0)
                        {
                            foreach (var line in ModData.GetLines("doro"))
                            {
                                var p = line.Split('|');
                                if (p.Length < 5) continue;
                                if (!int.TryParse(p[0], out var slot)) continue;
                                var st0 = new State
                                {
                                    offered = p[1] == "1",
                                    remaining = int.TryParse(p[2], out var r) ? r : 0,
                                    lastCollectDay = int.TryParse(p[3], out var d) ? d : 0,
                                    purchased = p[4] == "1"
                                };
                                // 新增字段（旧存档没有，默认 0 / false）
                                if (p.Length >= 6 && int.TryParse(p[5], out var dd)) st0.pendingDay = dd;
                                if (p.Length >= 7) st0.terminalDelivered = p[6] == "1";
                                _slots[slot] = st0;
                            }
                        }
                    }
                    catch (System.Exception __ex) { Safe.Warn("Doro.cs:76", __ex); }
                }
                int sid = SlotId;
                if (!_slots.TryGetValue(sid, out var st)) { st = new State(); _slots[sid] = st; }
                return st;
            }
        }

        public static void Save()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                foreach (var kv in _slots)
                    sb.Append(kv.Key).Append('|').Append(kv.Value.offered ? 1 : 0).Append('|')
                      .Append(kv.Value.remaining).Append('|').Append(kv.Value.lastCollectDay).Append('|')
                      .Append(kv.Value.purchased ? 1 : 0).Append('|')
                      .Append(kv.Value.pendingDay).Append('|')
                      .Append(kv.Value.terminalDelivered ? 1 : 0).Append('\n');
                ModData.Set("doro", sb.ToString());
            }
            catch (System.Exception __ex) { Safe.Warn("Doro.cs:97", __ex); }
        }

        /// <summary>每天开店时调用（由 ModEntry 订阅 ModHook.OnShutterOpenedLate）。</summary>
        public static void OnShopOpened()
        {
            try
            {
                EnsureSprite();
                int day = Math.Max(1, StoreStation.GetDayCounter());
                var st = Cur;
                DoroPhone.EnsureRegistered();
                // ① 已买 + 已送达，但身上没有终端机（卖了/丢了）→ 视为未送达，今天重新排期
                if (st.purchased && st.terminalDelivered && !DoroItems.HasTerminal())
                {
                    st.terminalDelivered = false;
                    st.pendingDay = day;
                    Save();
                    Debug.Log(FigurineMod.LogTag + "DORO：终端机不在了，今天重新送一次");
                }
                // ② 已买 + 未送达 + 到期（beta 的 terminalDueDay）→ 今天上门送货，每天只排一次
                if (st.purchased && !st.terminalDelivered && st.pendingDay > 0 && day >= st.pendingDay && _queuedDay != day)
                {
                    _queuedDay = day;
                    _deliverQueued = true;
                    QueueDeliver();
                    Debug.Log(FigurineMod.LogTag + "DORO 送货到期（第 " + day + " 天，约在第 " + st.pendingDay + " 天）→ 上门");
                    return;
                }
                if (day == 1 && _queuedDay != day)
                {
                    _queuedDay = day;
                    Queue(true);
                    Debug.Log(FigurineMod.LogTag + "DORO 第 1 天来送终端机");
                    return;
                }
                if (day % 7 == 6 && st.remaining > 0 && st.lastCollectDay != day)
                {
                    st.lastCollectDay = day;
                    Save();
                    Queue(false);
                    Debug.Log(FigurineMod.LogTag + "DORO 周六来要账，剩 " + st.remaining);
                }
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "DORO 开店异常：" + e.GetType().Name); }
        }

        private static void EnsureSprite()
        {
            try
            {
                var dict = SpriteDict.Instance.spriteDictionary;
                if (dict != null && dict.ContainsKey(SpriteKey)) return;
                var tex = LoadTexture("doro");
                if (tex == null) return;
                var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 750f);
                dict[SpriteKey] = sp;
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "DORO 立绘注入失败：" + e.GetType().Name); }
        }

        private static Texture2D LoadTexture(string key)
        {
            try
            {
                var mods = ModLoader.Mods;
                if (mods == null) return null;
                foreach (var m in mods)
                {
                    if (m == null || m.Manifest == null || m.Manifest.ID != "figurine.gs") continue;
                    if (m.Textures != null && m.Textures.TryGetValue(key, out var t)) return t;
                }
            }
            catch (System.Exception __ex) { Safe.Warn("Doro.cs:170", __ex); }
            return null;
        }

        public static void Tick()
        {
            try
            {
                if (_cardAt > 0f && Time.unscaledTime >= _cardAt)
                {
                    _cardAt = -1f;
                    var card = DoroItems.Build(DoroItems.CardId, "DORO 名片", 0, 2, 1);
                    if (card != null)
                    {
                        ModData.Set("doro_card", "1");
                        var inv = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.invElement : null;
                        bool ok = true; try { PlayerStore.Instance.AddDirectSellingItemToTableMiddle(card, true); } catch { ok = false; }
                        Debug.Log(FigurineMod.LogTag + "DORO 名片已发=" + ok);
                        Debug.Log(FigurineMod.LogTag + "DORO：名片已给");
                    }
                }
            }
            catch (System.Exception __ex) { Safe.Warn("Doro.cs:192", __ex); }
        }

        public static bool HasPendingDelivery()
        {
            try { return Cur.pendingDay > 0; } catch { return false; }
        }

        public static void AddDebt(int amount)
        {
            try
            {
                var st = Cur;
                st.remaining += Math.Max(0, amount);
                Save();
                Debug.Log(FigurineMod.LogTag + "DORO 欠款 +" + amount + " → " + st.remaining);
            }
            catch (System.Exception __ex) { Safe.Warn("Doro.cs:209", __ex); }
        }

        public static void SetPendingDelivery()
        {
            try
            {
                var st = Cur;
                st.purchased = true;
                st.terminalDelivered = false;
                st.pendingDay = Math.Max(1, StoreStation.GetDayCounter()) + 1;
                Save();
            }
            catch (System.Exception __ex) { Safe.Warn("Doro.cs:222", __ex); }
        }

        public static void OnArrived()
        {
            try
            {
                try
                {
                    int day = Math.Max(1, StoreStation.GetDayCounter());
                    if (ModData.Get("doro_card", "") != "1")
                    {
                        _cardAt = Time.unscaledTime + 2f;
                        Debug.Log(FigurineMod.LogTag + "DORO 名片 2 秒后发放");
                    }
                }
                catch (System.Exception __ex) { Safe.Warn("Doro.cs:238", __ex); }
                if (!_deliverQueued) return;
                _deliverQueued = false;
                GrantTerminal();
            }
            catch (System.Exception __ex) { Safe.Warn("Doro.cs:243", __ex); }
        }

        public static void GrantTerminal()
        {
            try
            {
                var item = TerminalItem.Build();
                if (item == null)
                {
                    var stF = Cur;
                    stF.pendingDay = Math.Max(1, StoreStation.GetDayCounter()) + 1;
                    Save();
                    Debug.LogWarning(FigurineMod.LogTag + "DORO 终端机发放失败，改为明天重送");
                    return;
                }
                var inv = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.invElement : null;
                try { PlayerStore.Instance.AddDirectSellingItemToTableMiddle(item, true); } catch (System.Exception __ex) { Safe.Warn("Doro.cs:260", __ex); }
                var box2 = DoroItems.Build(DoroItems.MailboxId, "货物邮箱", 400, 2, 2);
                if (box2 != null) { try { PlayerStore.Instance.AddDirectSellingItemToTableMiddle(box2, true); } catch (System.Exception __ex) { Safe.Warn("Doro.cs:262", __ex); } }
                Debug.Log(FigurineMod.LogTag + "DORO：终端机+货物邮箱已送到（柜台）");
                try
                {
                    var stD = Cur;
                    stD.purchased = true;
                    stD.terminalDelivered = true;
                    stD.pendingDay = 0;
                    Save();
                }
                catch (System.Exception __ex) { Safe.Warn("Doro.cs:272", __ex); }
                try
                {
                    string mode = ModData.Get("doro_paymode", "");
                    if (mode == "cash") { PlayerStore.Instance.PayMoney(2000, false); Debug.Log(FigurineMod.LogTag + "DORO 送达扣现金 2000"); }
                    else if (mode == "credit") { AddDebt(2000); Debug.Log(FigurineMod.LogTag + "DORO 送达记账 2000"); }
                    ModData.Set("doro_paymode", "");
                }
                catch (System.Exception __ex) { Safe.Warn("Doro.cs:280", __ex); }
            }
            catch (System.Exception __ex) { Safe.Warn("Doro.cs:282", __ex); }
        }

        private static void QueueDeliver()
        {
            try
            {
                var mgr = PlayerStore.Instance != null ? PlayerStore.Instance.storeClientManager : null;
                if (mgr == null) return;
                var c = new StoreClient();
                c.identifier = ClientId;
                c.displayName = "DORO";
                c.spriteName = SpriteKey;
                c.possibleSprites = new List<string> { SpriteKey };
                c.clientCash = 0;
                c.clientIntent = StoreClient.ClientIntent.DIALOGUE;
                c.isBargainAvailable = false;
                c.dismissable = true;
                c.isRegularSellDisable = true;
                c.clientBuyingIdList = new List<string>();
                c.clientBuyingTagList = new List<string>();
                c.clientBlackIdList = new List<string>();
                c.clientBlackTagList = new List<string>();
                var d = new Dialogue();
                d.SetText("DORO", "你的终端机，送到！");
                c.mainDialogue = d;
                try { ModHelper.SetClientSource(c, "figurine.gs"); } catch (System.Exception __ex) { Safe.Warn("Doro.cs:308", __ex); }
                try { mgr.AddNextClient(c); } catch (System.Exception __ex) { Safe.Warn("AddNextClient", __ex); }   // 必须插队首：clientStack.Add 会排到当天队尾且跳过 TrackClient
            }
            catch (System.Exception __ex) { Safe.Warn("Doro.cs:311", __ex); }
        }

        private static GameItem ChoiceItem()
        {
            try { return TerminalItem.Build(); } catch { return null; }
        }

        private static void Queue(bool offer)
        {
            try
            {
                var mgr = PlayerStore.Instance != null ? PlayerStore.Instance.storeClientManager : null;
                if (mgr == null) return;

                var c = new StoreClient();
                c.identifier = ClientId;
                c.displayName = "DORO";
                c.spriteName = SpriteKey;
                c.possibleSprites = new List<string> { SpriteKey };
                c.clientCash = 0;
                c.useClientBudget = false;
                c.clientIntent = StoreClient.ClientIntent.DIALOGUE;
                c.isBargainAvailable = false;
                c.dismissable = true;
                c.isRegularSellDisable = true;
                c.clientBuyingIdList = new List<string>();
                c.clientBuyingTagList = new List<string>();
                c.clientBlackIdList = new List<string>();
                c.clientBlackTagList = new List<string>();

                var d = new Dialogue();
                d.SetText("DORO", offer
                    ? "你好呀，我是 DORO。终端机和货物邮箱一套 2000，可以先记账，每周六我过来收一点。要吗？"
                    : "这周该还账了，还差 " + Cur.remaining + "。");

                // 每个选项独立物品 + PLAYER 台词做选项文字
                // （共用同一个 item 会让 AddChoice 反复覆盖 name；把 DORO 台词当选项则会显示成回复）
                if (offer)
                {
                    d.AddChoice(ChoiceItem(), "", "",
                        Opt("拿走终端机和货物邮箱。（欠 2000）", DoroChoice("行，记上账了，每周六我来收 100，直到还清。")),
                        (Action)(() => Accept()), (Func<bool>)(() => true));
                    d.AddChoice(ChoiceItem(), "", "",
                        Opt("以后再说。", DoroChoice("行，等你想好了再喊我。")),
                        (Action)(() => { }), (Func<bool>)(() => true));
                }
                else
                {
                    d.AddChoice(ChoiceItem(), "", "",
                        Opt("先还 100。", DoroChoice("收到 100，剩下的我周六再来收。")),
                        (Action)(() => Pay(100)), (Func<bool>)(() => true));
                    d.AddChoice(ChoiceItem(), "", "",
                        Opt("一次还清。", DoroChoice("那就两清啦，谢谢你。")),
                        (Action)(() => Pay(Cur.remaining)), (Func<bool>)(() => true));
                    d.AddChoice(ChoiceItem(), "", "",
                        Opt("下次再说。", DoroChoice("行，那我下周再来。")),
                        (Action)(() => { }), (Func<bool>)(() => true));
                }
                c.mainDialogue = d;
                try { ModHelper.SetClientSource(c, "figurine.gs"); } catch (System.Exception __ex) { Safe.Warn("Doro.cs:362", __ex); }
                try { mgr.AddNextClient(c); } catch (System.Exception __ex) { Safe.Warn("AddNextClient", __ex); }   // 必须插队首：clientStack.Add 会排到当天队尾且跳过 TrackClient
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "DORO 入队失败：" + e.GetType().Name + " | " + e.Message); }
        }

        /// <summary>电话/对话 UI 用「下一层文本」当选项文字，所以选项包一层 PLAYER 台词。</summary>
        private static Dialogue Opt(string label, Dialogue after)
        {
            var d = new Dialogue();
            d.SetText("PLAYER", label);
            if (after != null) d.SetNextDialogue(after);
            return d;
        }

        private static Dialogue DoroChoice(string label)
        {
            var d2 = new Dialogue();
            d2.SetText("DORO", label);
            return d2;
        }

        private static void Accept()
        {
            try
            {
                var st = Cur;
                st.remaining = 2000;
                st.purchased = true;
                Save();
                var inv = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.invElement : null;
                var item = TerminalItem.Build();
                if (item != null) { try { PlayerStore.Instance.AddDirectSellingItemToTableMiddle(item, true); } catch (System.Exception __ex) { Safe.Warn("Doro.cs:385", __ex); } }
                var box = DoroItems.Build(DoroItems.MailboxId, "货物邮箱", 400, 2, 2);
                Debug.Log(FigurineMod.LogTag + "DORO 邮箱构建=" + (box != null));
                if (box != null)
                {
                    bool ok = true; try { PlayerStore.Instance.AddDirectSellingItemToTableMiddle(box, true); } catch { ok = false; }
                    Debug.Log(FigurineMod.LogTag + "DORO 邮箱入包=" + ok);
                }
                Debug.Log(FigurineMod.LogTag + "DORO：给了终端机+邮箱，欠款 2000，3 秒后给名片");
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "DORO 接受失败：" + e.GetType().Name); }
        }

        /// <summary>当前欠款（给电话菜单用）。</summary>
        public static int Remaining { get { try { return Cur.remaining; } catch { return 0; } } }

        /// <summary>还款（给电话菜单用）。</summary>
        public static void PayDebt(int amount) { Pay(amount); }

        private static void Pay(int amount)
        {
            try
            {
                var ps = PlayerStore.Instance;
                var st = Cur;
                if (ps == null) return;
                if (st.remaining <= 0) return;
                int pay = Math.Min(amount, st.remaining);
                if (!ps.CanPay(pay)) { Debug.Log(FigurineMod.LogTag + "DORO：现金不足"); return; }
                ps.PayMoney(pay, false);
                st.remaining -= pay;
                Save();
                Debug.Log(FigurineMod.LogTag + "DORO：还款 " + pay + "，剩 " + st.remaining);
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "DORO 还款失败：" + e.GetType().Name); }
        }
    }
}
