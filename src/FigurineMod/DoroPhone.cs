using System;
using HarmonyLib;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// DORO 电话 3000：需持有名片；终端机+货物邮箱共 2000，
    /// 可付现金或记账，次日送到柜台。
    /// </summary>
    internal static class DoroPhone
    {
        public const long Number = 3000;

        public static void EnsureRegistered()
        {
            try
            {
                // 统一交给 PhoneHooks：无条件写进电话簿，能不能拨由 IsNumberExist 判断
                PhoneHooks.SyncContact();
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "DORO 电话登记失败：" + e.GetType().Name); }
        }

        private static bool IsDoro(StorePhoneClient c)
        {
            try { return c != null && string.Equals(c.dialogFuncId, PhoneHooks.DoroDialogId, StringComparison.Ordinal); }
            catch { return false; }
        }

        [HarmonyPatch(typeof(StorePhoneClient), nameof(StorePhoneClient.GetCallDialog))]
        internal static class CallDialogPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(StorePhoneClient __instance, ref Dialogue __result)
            {
                try
                {
                    if (!IsDoro(__instance)) return true;
                    if (PhoneHooks.AtWindow(PhoneHooks.DoroNumber))
                    {
                        var busy = new Dialogue();
                        busy.SetText("DORO", PhoneHooks.BusyText(PhoneHooks.DoroNumber));
                        __result = busy;
                        return false;
                    }
                    var d = new Dialogue();
                    if (Doro.HasPendingDelivery())
                    {
                        d.SetText("DORO", "货已经上路啦，明天这个时间之前送到你柜台。\n"
                            + "麻烦留意一下门口哦。");
                        __result = d;
                        return false;
                    }
                    var item = TerminalItem.Build();
                    bool hasT = DoroItems.HasTerminal(), hasM = DoroItems.HasMailbox();
                    int debt = Doro.Remaining;

                    // —— 设备齐全：只做扩仓 / 收账 ——
                    if (hasT && hasM)
                    {
                        d.SetText("DORO", "仓库要是堆不下了跟我说一声，我帮你扩，一档一万。"
                            + (debt > 0 ? "\n对了，你还欠我 " + debt + "，周六我过来收，麻烦留一下现金。" : ""));

                        if (Warehouse.CanUpgrade)
                        {
                            d.AddChoice(TerminalItem.Build(), "", "",
                                Opt("扩仓要多少钱？", Choice("好，等我重新装修一下。")),
                                (Action)(() => UpgradeCapacity()), (Func<bool>)(() => Cash() >= 10000));
                        }
                        else if (ModData.Get("doro_wh_max", "") != "1")
                        {
                            d.AddChoice(TerminalItem.Build(), "", "",
                                Opt("还能再扩吗？", Choice("已经扩到最大啦，你这仓库是真能装。")),
                                (Action)(() => { try { ModData.Set("doro_wh_max", "1"); } catch (System.Exception __ex) { Safe.Warn("DoroPhone.cs:78", __ex); } }),
                                (Func<bool>)(() => true));
                        }

                        if (debt > 0)
                        {
                            var repay = new Dialogue();
                            repay.SetText("DORO", "要还多少？还欠我 " + debt + "。");
                            int rest = Math.Max(0, debt - 100);
                            repay.AddChoice(TerminalItem.Build(), "", "",
                                Opt("先还 100。", Choice(rest > 0
                                    ? "收到 100，还欠 " + rest + "，周六我再过来。"
                                    : "收到 100，两清啦，谢谢你。")),
                                (Action)(() => Doro.PayDebt(100)), (Func<bool>)(() => Cash() >= Math.Min(100, debt)));
                            repay.AddChoice(TerminalItem.Build(), "", "",
                                Opt("一次结清。", Choice("都结清啦，谢谢你一直照顾我生意。")),
                                (Action)(() => Doro.PayDebt(debt)), (Func<bool>)(() => Cash() >= debt));
                            repay.AddChoice(TerminalItem.Build(), "", "", Opt("返回。", d), null, (Func<bool>)(() => true));

                            d.AddChoice(TerminalItem.Build(), "", "", Opt("还款。", repay), null, (Func<bool>)(() => true));
                        }
                        else
                        {
                            int pending = MailboxCount();
                            d.AddChoice(TerminalItem.Build(), "", "",
                                Opt(pending > 0 ? "寄货，把这些搬进仓库。" : "寄货是怎么回事？",
                                    Choice(pending > 0 ? "好，我这就把邮箱里的货搬进仓库。" : "先把货放进货物邮箱，我再帮你搬。")),
                                (Action)(() => ShipFromMailbox()), (Func<bool>)(() => true));
                        }

                        d.AddChoice(TerminalItem.Build(), "", "", Opt("回头再说。", Choice("好，那我先去送单了。")),
                            (Action)(() => { }), (Func<bool>)(() => true));
                        __result = d;
                        return false;
                    }

                    // —— 没设备：卖一套 ——
                    d.SetText("DORO", "终端机加货物邮箱，一套两千。\n"
                        + "现结，或者先记账——记账的话每周六我上门收一百，直到还清。");
                    d.AddChoice(TerminalItem.Build(), "", "", Opt("现付 2000。", Choice("收到啦，明天上午送到你柜台。")),
                        (Action)(() => BuyCash()), (Func<bool>)(() => Cash() >= 2000));
                    d.AddChoice(TerminalItem.Build(), "", "", Opt("先记账。", Choice("记上啦，每周六我过来收 100，慢慢还就好。")),
                        (Action)(() => BuyCredit()), (Func<bool>)(() => true));
                    d.AddChoice(TerminalItem.Build(), "", "", Opt("回头再说。", Choice("行，等你想好了再说。")),
                        (Action)(() => { }), (Func<bool>)(() => true));
                    __result = d;
                    return false;
                }
                catch { return true; }
            }


            private static Dialogue Choice(string text)
            {
                var d = new Dialogue(); d.SetText("DORO", text); return d;
            }

            /// <summary>
            /// 照 0.8.0 的写法：选项的**显示文字**放在下一层 PLAYER 对白里，
            /// `AddChoice` 的 label 传空串，子对话再 SetNextDialogue 到回复/子菜单。
            /// 电话 UI 就是按这个规则取文字的（把回复写进标签会出现「三个答案同时显示」）。
            /// </summary>
            private static Dialogue Opt(string label, Dialogue next)
            {
                var d = new Dialogue();
                d.SetText("PLAYER", label);
                if (next != null) d.SetNextDialogue(next);
                return d;
            }
        }

        /// <summary>货物邮箱里有多少件东西（按数量累加）。</summary>
        private static int MailboxCount()
        {
            try
            {
                var inv = MailboxInventory();
                if (inv == null) return 0;
                int n = 0;
                foreach (var it in inv.childItems) if (it != null) n += Math.Max(1, it.unitCount);
                return n;
            }
            catch { return 0; }
        }

        private static GameInventory MailboxInventory()
        {
            try
            {
                GameItem box = null;
                var ps = PlayerStore.Instance;
                if (ps != null)
                {
                    foreach (var it in ps.FindAllItem(true))
                    {
                        if (it == null) continue;
                        string id = null;
                        try { id = it.identifier; } catch (System.Exception __ex) { Safe.Warn("DoroPhone.cs:180", __ex); }
                        if (id == DoroItems.MailboxId) { box = it; break; }
                    }
                }
                if (box == null) return null;
                if (box.contentWindow == null || box.contentWindow.children == null || box.contentWindow.children.Count == 0) return null;
                return box.contentWindow.children[0] as GameInventory;
            }
            catch { return null; }
        }

        /// <summary>把货物邮箱里的东西全部搬进模组仓库（相当于 DORO 替你收货）。</summary>
        private static void ShipFromMailbox()
        {
            try
            {
                var inv = MailboxInventory();
                if (inv == null) { Debug.LogWarning(FigurineMod.LogTag + "寄货：找不到货物邮箱或邮箱为空"); return; }
                var items = new System.Collections.Generic.List<GameItem>(inv.childItems);
                int moved = 0;
                foreach (var it in items)
                {
                    if (it == null) continue;
                    string id = null; int qty = 1;
                    try { id = it.identifier; } catch (System.Exception __ex) { Safe.Warn("DoroPhone.cs:204", __ex); }
                    try { qty = Math.Max(1, it.unitCount); } catch (System.Exception __ex) { Safe.Warn("DoroPhone.cs:205", __ex); }
                    if (string.IsNullOrEmpty(id)) continue;
                    Warehouse.AddOrBag(id, qty);
                    try { it.Destroy(); } catch (System.Exception __ex) { Safe.Warn("DoroPhone.cs:208", __ex); }
                    moved += qty;
                }
                Debug.Log(FigurineMod.LogTag + "寄货：从货物邮箱搬进仓库 " + moved + " 件");
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "寄货失败：" + e.GetType().Name); }
        }
        private static int Cash()
        {
            try { return PlayerStore.Instance != null ? PlayerStore.Instance.GetCash() : 0; } catch { return 0; }
        }

        private static void BuyCash()
        {
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null || !ps.CanPay(2000)) return;
                ModData.Set("doro_paymode", "cash");
                Doro.SetPendingDelivery();
                Debug.Log(FigurineMod.LogTag + "DORO 电话：现金 2000，次日送到");
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "DORO 电话现金购买失败：" + e.GetType().Name); }
        }

        private static void BuyCredit()
        {
            try
            {
                ModData.Set("doro_paymode", "credit");
                Doro.SetPendingDelivery();
                Debug.Log(FigurineMod.LogTag + "DORO 电话：记账 2000，次日送到");
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "DORO 电话记账失败：" + e.GetType().Name); }
        }

        /// <summary>花 10000 信用点把仓库扩一档（40→60→80→100）。先扩容成功再扣钱。</summary>
        private static void UpgradeCapacity()
        {
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null) return;
                if (!Warehouse.CanUpgrade) { Debug.Log(FigurineMod.LogTag + "DORO 电话：仓库已经是最大容量"); return; }
                if (!ps.CanPay(10000)) { Debug.Log(FigurineMod.LogTag + "DORO 电话：现金不足 10000"); return; }

                int before = Warehouse.Capacity;
                if (!Warehouse.Upgrade()) return;
                ps.PayMoney(10000, false);
                Debug.Log(FigurineMod.LogTag + "DORO 电话：仓库扩容 " + before + " → " + Warehouse.Capacity + "，-10000");
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "DORO 电话扩容失败：" + e.GetType().Name); }
        }

        [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.LoadGame))]
        internal static class LoadPatch
        {
            [HarmonyPostfix]
            private static void Postfix() { EnsureRegistered(); }
        }
    }
}
