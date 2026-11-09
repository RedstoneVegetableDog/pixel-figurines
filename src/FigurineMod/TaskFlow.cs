using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 海子委托（按 0.8.0-beta BuildTaskSubtree/BuildHandInTree/BuildSettleTree 实现）：
    /// 逐件接单 → 交任务只列“已接且持有” → 钱结算/换货 → 成交销账；IsDoing 防当天重复安排。
    /// </summary>
    internal static class TaskFlow
    {
        private static List<string> _accepted;
        private static string _pending;

        /// <summary>存档槽：委托状态必须按槽隔离，否则换档会串数据。</summary>
        private static int Slot { get { try { return PlayerStore.Instance != null ? PlayerStore.Instance.saveSlotId : 0; } catch (System.Exception __ex) { Safe.Warn("TaskFlow.Slot", __ex); return 0; } } }

        private static string KeyTasks { get { return "tasks." + Slot; } }
        private static string KeyDoing { get { return "doing." + Slot; } }

        /// <summary>读某个键；该槽还没有数据时回落到旧的全局键（老存档迁移）。</summary>
        private static string[] Lines(string key, string legacyKey)
        {
            try
            {
                var cur = ModData.GetLines(key);
                if (cur != null && cur.Length > 0) return cur;
                var old = ModData.GetLines(legacyKey);
                if (old != null && old.Length > 0)
                {
                    ModData.SetLines(key, new List<string>(old));
                    Debug.Log(FigurineMod.LogTag + "委托：已把旧键 " + legacyKey + " 迁到 " + key);
                    return old;
                }
            }
            catch (System.Exception __ex) { Safe.Warn("TaskFlow.Lines", __ex); }
            return new string[0];
        }

        private static List<string> Accepted
        {
            get
            {
                if (_accepted != null) return _accepted;
                _accepted = new List<string>();
                try { foreach (var l in Lines(KeyTasks, "tasks")) if (!string.IsNullOrEmpty(l)) _accepted.Add(l); }
                catch (System.Exception __ex) { Safe.Warn("TaskFlow.cs:24", __ex); }
                return _accepted;
            }
        }

        private static void Save() { try { ModData.SetLines(KeyTasks, Accepted); } catch (System.Exception __ex) { Safe.Warn("TaskFlow.cs:29", __ex); } }

        private static string Key(string owner, string item) { return owner + "|" + item; }
        public static bool Has(string owner, string item) { return Accepted.Contains(Key(owner, item)); }

        public static bool IsDoing(string owner, string item)
        {
            try
            {
                int day = Math.Max(1, StoreStation.GetDayCounter());
                foreach (var l in Lines(KeyDoing, "doing"))
                {
                    var p = l.Split('|');
                    if (p.Length >= 4 && p[0] == day.ToString() && p[1] == owner && p[2] == item) return true;
                }
            }
            catch (System.Exception __ex) { Safe.Warn("TaskFlow.cs:45", __ex); }
            return false;
        }

        private static void MarkDoing(string owner, string item)
        {
            try
            {
                int day = Math.Max(1, StoreStation.GetDayCounter());
                var all = new List<string>(Lines(KeyDoing, "doing"));
                all.RemoveAll(l => { var p = l.Split('|'); return p.Length >= 1 && p[0] == day.ToString(); });
                all.Add(day + "|" + owner + "|" + item + "|1");
                ModData.SetLines(KeyDoing, all);
            }
            catch (System.Exception __ex) { Safe.Warn("TaskFlow.cs:59", __ex); }
        }

        /// <summary>
        /// 交货菜单：**返回对话树**，由调用方（海子电话）挂在选项的 nextDialogue 上。
        ///
        /// 以前这里直接 `DialoguePhoneUIManager.StartDialogue(root)` 强行替换界面，
        /// 但电话 UI 选完选项后自己还会把 currentDialog 切到该选项的 nextDialogue，
        /// 两个动作打架 → 菜单被顶掉，看起来"点交货没反应"（2026-11-04 修）。
        /// </summary>
        public static Dialogue BuildMenu()
        {
            try
            {
                var root = new Dialogue();
                root.SetText("海子的助手", "这几位最近都想要东西，您看哪位？");
                AddOwner(root, CustomerMira.ClientId, "米拉");
                AddOwner(root, CustomerSellers.BailuId, "白鹭");
                AddOwner(root, CustomerSellers.QueerId, "雀儿");
                return root;
            }
            catch (Exception e)
            {
                Debug.LogError(FigurineMod.LogTag + "委托菜单失败：" + e.GetType().Name);
                return null;
            }
        }

        /// <summary>兼容旧调用：仍然可以主动弹出（例如调试）。</summary>
        public static void Open()
        {
            try
            {
                var root = BuildMenu();
                if (root != null) DialoguePhoneUIManager.Instance.StartDialogue(root);
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "委托菜单失败：" + e.GetType().Name); }
        }

        /// <summary>电话 UI 取「下一层对话文本」当选项文字，所以选项要包一层 PLAYER 台词。</summary>
        private static Dialogue Opt(string label, Dialogue after)
        {
            var d = new Dialogue();
            d.SetText("PLAYER", label);
            if (after != null) d.SetNextDialogue(after);
            return d;
        }

        private static void AddOwner(Dialogue root, string ownerId, string ownerName)
        {
            var page = new Dialogue();
            var list = TaskList.Today(ownerId);
            var open = new List<Catalog.Def>();
            foreach (var d in list) if (!Has(ownerId, d.Id)) open.Add(d);

            page.SetText("海子的助手", open.Count > 0 ? ownerName + "的清单：要接哪一件？" : ownerName + "：今天的都接过了。");
            foreach (var d in open)
            {
                string id = d.Id;
                var ok = new Dialogue();
                ok.SetText("海子的助手", "记下了。" + d.Zh + " 到手后再来交任务。");
                page.AddChoice(Haizi.BuildCard(), "", "", Opt("[接·" + d.Zh + "]", ok), (Action)(() => Accept(ownerId, id)), (Func<bool>)(() => true));
            }

            var hand = new Dialogue();
            hand.SetText("PLAYER", "[交任务]");
            hand.SetNextDialogue(HandIn(Haizi.BuildCard(), ownerId, ownerName));
            page.AddChoice(Haizi.BuildCard(), "", "", hand, (Action)null, (Func<bool>)(() => true));
            root.AddChoice(Haizi.BuildCard(), "", "", Opt("[" + ownerName + "任务]", page), (Action)null, (Func<bool>)(() => true));
        }

        public static void Accept(string owner, string item)
        {
            try
            {
                if (Has(owner, item)) return;
                Accepted.Add(Key(owner, item));
                Save();
                Debug.Log(FigurineMod.LogTag + "接单 " + owner + " → " + item);
            }
            catch (System.Exception __ex) { Safe.Warn("TaskFlow.cs:109", __ex); }
        }

        private static Dialogue HandIn(GameItem icon, string ownerId, string ownerName)
        {
            var owned = new List<Catalog.Def>();
            foreach (var d in TaskList.Today(ownerId))
                if (Has(ownerId, d.Id) && Owns(d.Id)) owned.Add(d);

            var page = new Dialogue();
            if (owned.Count == 0)
            {
                page.SetText("海子的助手", "您手里还没有清单上的东西。");
                return page;
            }
            page.SetText("海子的助手", ownerName + " 的任务：您要交哪一件？");
            foreach (var d in owned)
            {
                string id = d.Id;
                var pick = new Dialogue();
                pick.SetText("PLAYER", "[交·" + d.Zh + "]");
                pick.SetNextDialogue(Settle(icon, ownerId, ownerName, d));
                page.AddChoice(Haizi.BuildCard(), "", "", pick, (Action)null, (Func<bool>)(() => true));
            }
            return page;
        }

        private static Dialogue Settle(GameItem icon, string ownerId, string ownerName, Catalog.Def def)
        {
            if (IsDoing(ownerId, def.Id))
            {
                var d0 = new Dialogue();
                d0.SetText("海子的助手", "《" + def.Zh + "》今天已经安排过了，等他来吧。");
                return d0;
            }
            var d = new Dialogue();
            d.SetText("海子的助手", "给钱，还是换货？");

            var money = new Dialogue();
            money.SetText("PLAYER", "[钱结算]");
            var money2 = new Dialogue();
            money2.SetText("海子的助手", ownerName + " 会去店里收这件。");
            // 以前这里 money2.SetNextDialogue(money2) 是自引用：玩家点“继续”会无限循环同一句
            money.SetNextDialogue(money2);
            d.AddChoice(icon, "", "", money, (Action)(() => Schedule(ownerId, def.Id, false)), (Func<bool>)(() => true));

            var barter = new Dialogue();
            barter.SetText("PLAYER", "[以物易物]");
            var barter2 = new Dialogue();
            barter2.SetText("海子的助手", ownerName + " 会带货过去，您用 " + def.Zh + " 换。");
            barter.SetNextDialogue(barter2);
            d.AddChoice(icon, "", "", barter, (Action)(() => Schedule(ownerId, def.Id, true)), (Func<bool>)(() => true));
            return d;
        }

        private static void Schedule(string ownerId, string itemId, bool barter)
        {
            try
            {
                if (!Owns(itemId)) return;
                MarkDoing(ownerId, itemId);
                _pending = Key(ownerId, itemId);
                if (ownerId == CustomerMira.ClientId) CustomerMira.TrySchedule(StoreClient.ClientIntent.BUY, barter ? null : itemId, barter ? itemId : null);
                else if (ownerId == CustomerSellers.BailuId) CustomerSellers.TryBailu(StoreClient.ClientIntent.BUY, barter ? null : itemId, barter ? itemId : null);
                else CustomerSellers.TryQueer(StoreClient.ClientIntent.BUY, barter ? null : itemId, barter ? itemId : null);
                Debug.Log(FigurineMod.LogTag + "任务安排：" + ownerId + " " + (barter ? "换货" : "钱结算") + " " + itemId);
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "任务安排失败：" + e.GetType().Name); }
        }

        private static bool Owns(string itemId)
        {
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null) return false;
                foreach (var it in ps.FindAllItem(true))
                    if (it != null && it.identifier == itemId) return true;
            }
            catch (System.Exception __ex) { Safe.Warn("TaskFlow.cs:188", __ex); }
            return false;
        }

        /// <summary>
        /// 下班清理（对应 0.8.0 的 `ClearDoing`）：把今天的「进行中」标记全部退回可交，
        /// 同时删掉往日遗留的记录，避免 `doing` 越积越多。
        /// </summary>
        public static void OnDayEnded()
        {
            try
            {
                var all = new List<string>(Lines(KeyDoing, "doing"));
                if (all.Count == 0) return;
                int cleared = all.Count;
                ModData.SetLines(KeyDoing, new List<string>());
                Debug.Log(FigurineMod.LogTag + "下班：" + cleared + " 个进行中的任务已退回可交");
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "下班清理失败：" + e.GetType().Name); }
        }

        /// <summary>开店时静默清掉往日遗留的 doing 记录（不改变今天的）。</summary>
        public static void PruneStale()
        {
            try
            {
                var all = Lines(KeyDoing, "doing");
                if (all.Length == 0) return;
                int day = Math.Max(1, StoreStation.GetDayCounter());
                string today = day.ToString();
                var keep = new List<string>();
                foreach (var l in all)
                {
                    var p = l.Split('|');
                    if (p.Length >= 1 && p[0] == today) keep.Add(l);
                }
                if (keep.Count != all.Length) ModData.SetLines(KeyDoing, keep);
            }
            catch (System.Exception __ex) { Safe.Warn("TaskFlow.cs:226", __ex); }
        }
        public static void OnBusinessCompleted(string clientId)
        {
            try
            {
                if (string.IsNullOrEmpty(_pending)) return;
                var p = _pending.Split('|');
                if (p.Length < 2 || p[0] != clientId) return;
                Accepted.Remove(_pending);
                Save();
                _pending = null;
                Debug.Log(FigurineMod.LogTag + "委托成交销账");
            }
            catch (System.Exception __ex) { Safe.Warn("TaskFlow.cs:240", __ex); }
        }

        [HarmonyPatch(typeof(StoreClient), nameof(StoreClient.CompleteBusiness))]
        internal static class CompletePatch
        {
            [HarmonyPostfix]
            private static void Postfix(StoreClient __instance)
            {
                try { if (__instance != null) OnBusinessCompleted(__instance.identifier); } catch (System.Exception __ex) { Safe.Warn("TaskFlow.cs:249", __ex); }
            }
        }
    }
}
