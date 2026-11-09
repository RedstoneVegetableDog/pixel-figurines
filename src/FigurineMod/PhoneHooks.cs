using System;
using HarmonyLib;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 电话簿接线（照 0.8.0-beta 的四个 Prefix 做法）。
    ///
    /// 之前只在 `EnsureRegistered()` 里往 `PhoneClientDict` 塞一条，然后 `GetCallDialog`
    /// 的后缀靠 `displayName == "DORO"` 判断 —— 但 `displayName` 会被游戏的本地化流程
    /// 覆盖（`GetLocalizedDisplayName` 走 `locID`），一旦被清空/改写，前缀就不命中，
    /// 电话里就变成原版通用对话；号码也可能因为 `IsNumberExist` 只认原版表而拨不通。
    ///
    /// 现在按 beta 的写法接管四个点：
    /// ```
    /// IsNumberExist(号码)           → 我们的号：有名片就 true
    /// GetPhoneClientByNumber(号码)  → 我们的号：返回我们塞进字典的客户端
    /// GetCallDialog()               → dialogFuncId 是我们的：返回自建对话
    /// GetLocalizedDisplayName()     → dialogFuncId 是我们的：返回固定名字
    /// ```
    /// </summary>
    internal static class PhoneHooks
    {
        public const long DoroNumber = 3000;
        public const long HaiziNumber = 3721;
        public const string DoroDialogId = "figurine_doro_phone";
        public const string HaiziDialogId = "figurine_haizi_phone";
        public const string DoroName = "DORO";
        public const string HaiziName = "海子";

        private static bool _logged;
        private static string _lastSig;

        public static bool IsOurs(long number) { return number == DoroNumber || number == HaiziNumber; }

        public static string NameOf(long number) { return number == HaiziNumber ? HaiziName : DoroName; }

        public static string DialogIdOf(long number) { return number == HaiziNumber ? HaiziDialogId : DoroDialogId; }

        /// <summary>
        /// 这个人是不是「已经在窗口/已排进今天到店队列」。
        /// 0.8.0 的 `HasPendingVisit` 就是查这三处：当前顾客 / 未来队列 / 到店队列。
        /// </summary>
        public static bool AtWindow(long number)
        {
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null) return false;
                string id = number == HaiziNumber ? Haizi.ClientId : Doro.ClientId;

                try
                {
                    var cur = ps.currentClientInstance;
                    var c = cur != null ? cur.GetClientBlueprint() : null;
                    if (c != null && string.Equals(c.identifier, id, StringComparison.Ordinal)) return true;
                }
                catch (System.Exception __ex) { Safe.Warn("PhoneHooks.cs:58", __ex); }
                try
                {
                    var q = ps.futurStoreClientIdQueue;
                    if (q != null && q.Contains(id)) return true;
                }
                catch (System.Exception __ex) { Safe.Warn("PhoneHooks.cs:64", __ex); }
                try
                {
                    var mgr = ps.storeClientManager;
                    if (mgr != null && mgr.clientStack != null)
                    {
                        foreach (var c in mgr.clientStack)
                        {
                            if (c != null && string.Equals(c.identifier, id, StringComparison.Ordinal)) return true;
                        }
                    }
                }
                catch (System.Exception __ex) { Safe.Warn("PhoneHooks.cs:76", __ex); }
            }
            catch (System.Exception __ex) { Safe.Warn("PhoneHooks.cs:78", __ex); }
            return false;
        }

        /// <summary>有名片就能看到/拨通这个号；人在窗口时由 GetCallDialog 返回「占线」对话。</summary>
        public static bool CanCall(long number)
        {
            try { return HasCard(number); } catch { return false; }
        }

        /// <summary>占线时的对话（人在窗口 / 已排进今天队列）。</summary>
        public static string BusyText(long number)
        {
            if (number == HaiziNumber)
                return "先生人已经在您店里了，您直接同他讲吧。";
            return "我人已经到你柜台了，有事当面说就行。";
        }

        /// <summary>有名片才能在电话簿里看到这个号。</summary>
        public static bool HasCard(long number)
        {
            try { return number == HaiziNumber ? Haizi.HasCardOwned() : DoroItems.HasCard(); }
            catch { return false; }
        }

        /// <summary>
        /// 把两个号都写进电话簿（beta 的 `SyncContact` 也是无条件写入，
        /// 能不能拨由 `IsNumberExist` 决定）。
        /// </summary>
        private static float _lastSync = -99f;
        private static bool _lastHasDoro, _lastHasHaizi;

        public static void SyncContact(bool force = false)
        {
            // IsNumberExist 会被电话 UI 每帧调很多次；每次同步都要遍历玩家全部物品，
            // 不缓存会造成打开电话时明显卡顿（2026-11-04）。
            try
            {
                float now = Time.realtimeSinceStartup;
                if (!force && now - _lastSync < 0.5f) return;
                _lastSync = now;
            }
            catch { }
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null) return;
                var dict = ps.PhoneClientDict;
                if (dict == null) return;

                Ensure(dict, DoroNumber, DoroName, DoroDialogId, DoroItems.HasCard());
                Ensure(dict, HaiziNumber, HaiziName, HaiziDialogId, Haizi.HasCardOwned());

                // 状态有变化才打日志，避免每次 IsNumberExist 都刷屏
                string sig = HasCard(DoroNumber) + "|" + (dict.ContainsKey(DoroNumber)) + "|"
                           + HasCard(HaiziNumber) + "|" + (dict.ContainsKey(HaiziNumber)) + "|" + dict.Count;
                if (sig != _lastSig)
                {
                    _lastSig = sig;
                    Debug.Log(FigurineMod.LogTag + "电话簿同步：DORO 有名片=" + HasCard(DoroNumber)
                        + " 条目=" + dict.ContainsKey(DoroNumber)
                        + "；海子 有名片=" + HasCard(HaiziNumber)
                        + " 条目=" + dict.ContainsKey(HaiziNumber)
                        + "；字典共 " + dict.Count + " 条");
                }
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "电话簿登记失败：" + e.GetType().Name); }
        }

        /// <summary>
        /// 照 0.8.0 `DoroPhone.SyncContact` 逐字段设置：
        /// ```text
        /// 没名片 → 把号码从电话簿里删掉
        /// 有名片 → dialogFuncId / displayName / phoneState=Regular(4) / dialedBefore=true
        ///          cooldownDuration=0 / currentCooldown=0 / locID=null / amountSpent=0 / stockType=0
        ///          新建时 phoneClientType=Supplier(1)、alias=new List()
        /// ```
        /// </summary>
        private static void Ensure(System.Collections.Generic.Dictionary<long, StorePhoneClient> dict,
                                   long number, string name, string dialogId, bool hasCard)
        {
            try
            {
                if (!hasCard)
                {
                    if (dict.ContainsKey(number)) dict.Remove(number);
                    return;
                }

                StorePhoneClient c;
                if (dict.TryGetValue(number, out c) && c != null)
                {
                    c.dialogFuncId = dialogId;
                    c.displayName = name;
                    c.phoneState = StorePhoneClient.PhoneState.Regular;
                    c.dialedBefore = true;
                    c.cooldownDuration = 0;
                    c.currentCooldown = 0;
                    return;
                }

                c = new StorePhoneClient();
                c.phoneClientType = StorePhoneClient.PhoneClientType.Supplier;
                c.phoneState = StorePhoneClient.PhoneState.Regular;
                c.displayName = name;
                c.locID = null;
                c.dialogFuncId = dialogId;
                c.cooldownDuration = 0;
                c.currentCooldown = 0;
                c.dialedBefore = true;
                try { c.alias = new System.Collections.Generic.List<string>(); } catch (System.Exception __ex) { Safe.Warn("PhoneHooks.cs:169", __ex); }
                c.amountSpent = 0;
                try { c.stockType = 0; } catch (System.Exception __ex) { Safe.Warn("PhoneHooks.cs:171", __ex); }
                dict[number] = c;
            }
            catch (System.Exception __ex) { Safe.Warn("PhoneHooks.cs:174", __ex); }
        }

        private static StorePhoneClient Get(long number)
        {
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null || ps.PhoneClientDict == null) return null;
                StorePhoneClient c;
                return ps.PhoneClientDict.TryGetValue(number, out c) ? c : null;
            }
            catch { return null; }
        }

        // ───────────────────────── 四个补丁 ─────────────────────────

        /// <summary>
        /// 电话簿打开前强制同步一次（2026-11-04 修「有名片但电话簿里看不到」）：
        /// 读档后 `PhoneClientDict` 会被游戏重建，之前同步过的条目会丢；
        /// 这里在每次打开电话簿前补写，保证列表里一定有。
        /// </summary>
        [HarmonyPatch(typeof(PhoneBookUIManager), nameof(PhoneBookUIManager.OpenUI))]
        internal static class PhoneBookOpenPatch
        {
            [HarmonyPrefix]
            private static void Prefix()
            {
                try { SyncContact(); }
                catch (System.Exception __ex) { Safe.Warn("PhoneHooks.PhoneBookOpen", __ex); }
            }
        }

        /// <summary>打开电话（拨号界面）时也补一次。</summary>
        [HarmonyPatch(typeof(PhoneUIManager), nameof(PhoneUIManager.OpenUI))]
        internal static class PhoneOpenPatch
        {
            [HarmonyPrefix]
            private static void Prefix()
            {
                try { SyncContact(); }
                catch (System.Exception __ex) { Safe.Warn("PhoneHooks.PhoneOpen", __ex); }
            }
        }
        [HarmonyPatch(typeof(StorePhoneClient), nameof(StorePhoneClient.IsNumberExist))]
        internal static class IsNumberExistPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(long number, ref bool __result)
            {
                try
                {
                    if (!IsOurs(number)) return true;
                    SyncContact();
                    __result = CanCall(number);
                    Debug.Log(FigurineMod.LogTag + "号码查询 " + number + " → " + __result
                        + "（名片=" + HasCard(number) + "，在窗口=" + AtWindow(number) + "）");
                    return false;
                }
                catch { return true; }
            }
        }

        [HarmonyPatch(typeof(StorePhoneClient), nameof(StorePhoneClient.GetPhoneClientByNumber))]
        internal static class GetByNumberPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(long number, ref StorePhoneClient __result)
            {
                try
                {
                    if (!IsOurs(number)) return true;
                    SyncContact();
                    __result = Get(number);
                    return false;
                }
                catch { return true; }
            }
        }

        /// <summary>照 0.8.0 的 `PhoneShownPatch`：我们的号始终出现在电话簿里。</summary>
        [HarmonyPatch(typeof(StorePhoneClient), nameof(StorePhoneClient.ShownInPhoneBook))]
        internal static class ShownPatch
        {
            [HarmonyPostfix]
            private static void Postfix(StorePhoneClient __instance, ref bool __result)
            {
                try
                {
                    if (__instance == null || __result) return;
                    string id = __instance.dialogFuncId;
                    long num = id == HaiziDialogId ? HaiziNumber : id == DoroDialogId ? DoroNumber : 0;
                    if (num != 0) __result = CanCall(num);
                }
                catch (System.Exception __ex) { Safe.Warn("PhoneHooks.cs:241", __ex); }
            }
        }
        [HarmonyPatch(typeof(StorePhoneClient), nameof(StorePhoneClient.GetLocalizedDisplayName))]
        internal static class DisplayNamePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(StorePhoneClient __instance, ref string __result)
            {
                try
                {
                    string id = __instance != null ? __instance.dialogFuncId : null;
                    if (id == DoroDialogId) { __result = DoroName; return false; }
                    if (id == HaiziDialogId) { __result = HaiziName; return false; }
                }
                catch (System.Exception __ex) { Safe.Warn("PhoneHooks.cs:256", __ex); }
                return true;
            }
        }
    }
}
