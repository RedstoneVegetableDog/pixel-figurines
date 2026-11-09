using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 手办「估价」玩法（用户 2026-11-04 定）—— 复刻原版「鉴定仿生手」那一套：
    ///
    /// · 鉴定客：`clientIntent = APPRAISAL_SERVICE`，只有这个 intent 会让游戏弹估价框；
    /// · 一次带 2~5 件**未鉴定**手办，真身 50/50 在获取时定死，价格显示 80%；
    /// · 到店：按等级概率 20/25/30/35%，来了按等级出 1~2 / 1~2 / 2~3 / 3~4 位（额外客流，
    ///   不占原版客流）；可鉴星级 = 1..(2+等级)；
    /// · 估价：默认 0，接受区间 50%~150%（原版 `IsAppraisalAcceptable`）；
    /// · 成功：给**基础费**（20/40/60/100，按精度抽成暂不做）+ 一张结果票
    ///   （真伪 / 估价准确率 / 真实价值 / 费用说明），手办由顾客带走。
    ///
    /// 等级暂用 `Rank`（第 4 步接证书后由证书驱动）。
    /// </summary>
    internal static class FigurineAppraisal
    {
        public const string ClientId = "figurine_appraiser_customer";
        public const string AppraisalTypeId = "figurine_appraisal";

        /// <summary>手办鉴定等级 0~3（学徒/初级/中级/高级）—— 由手办证书驱动（FigurineCert）。</summary>
        public static int Rank { get { return FigurineCert.Rank; } }

        /// <summary>带到店里的手办件数（原版 2~5 件）。</summary>
        private const int WaresMin = 2, WaresMax = 5;

        // ───────────────────────── 注册可鉴定物 ─────────────────────────

        private static bool _registered;

        public static void Register()
        {
            if (_registered) return;
            _registered = true;
            try
            {
                ModHelper.AddAppraisalItem(
                    "figurine.gs",
                    AppraisalTypeId,
                    "figurine_gs_klee",          // 仅用于显示名回退
                    0,                            // unlockRank：星级门槛我们自己按等级卡
                    MakeRandom,                   // 随机造一件未鉴定手办
                    GetCorrectValue,              // 「真实价值」
                    IsFigurineItem,
                    null,
                    () => "手办");
                Debug.Log(FigurineMod.LogTag + "已注册可鉴定物：手办（" + AppraisalTypeId + "）");
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "注册可鉴定物失败：" + e.GetType().Name + " " + e.Message); }
        }

        private static bool IsFigurineItem(GameItem item)
        {
            return FigurineAuth.IsFigurine(item);
        }

        /// <summary>「真实价值」= 真身价（正品 100% / 赝品 60%）。</summary>
        public static int GetCorrectValue(GameItem item)
        {
            try
            {
                if (item == null) return 0;
                var d = Defs.Find(item.identifier);
                if (d.Id == null) return 0;
                return FigurineAuth.TruthFake(item) ? d.Value * 60 / 100 : d.Value;
            }
            catch (Exception __ex) { Safe.Warn("FigurineAppraisal.GetCorrectValue", __ex); return 0; }
        }

        private static GameItem MakeRandom()
        {
            try
            {
                var def = PickDef();
                return BuildUnappraised(def);
            }
            catch (Exception __ex) { Safe.Warn("FigurineAppraisal.MakeRandom", __ex); return null; }
        }

        // ───────────────────────── 造未鉴定件 ─────────────────────────

        public static Catalog.Def PickDef()
        {
            var pool = new List<Catalog.Def>();
            int maxStar = FigurineCert.MaxStarOf(Rank);
            foreach (var d in Defs.All())
                if (!string.IsNullOrEmpty(d.Id) && d.Star >= 1 && d.Star <= maxStar) pool.Add(d);
            if (pool.Count == 0) return default(Catalog.Def);
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        public static GameItem BuildUnappraised(Catalog.Def def)
        {
            try
            {
                if (string.IsNullOrEmpty(def.Id)) return null;
                var item = FigurineMod.BuildFigurine(def);
                if (item == null) return null;
                // 未鉴定：真身 50/50 定死，价格显示 80%
                FigurineAuth.Apply(item, FigurineAuth.RollUnappraised(def.Id));
                // 打上原版「可鉴定」标记 —— 估价框只对带这个 tag 的物品弹
                // 用户 2026-11-04：估价框打开时默认填 0
                try { AppraisalHelper.InitAppraisalItem(item, 0); }
                catch { try { item.EnableTag("APPRAISAL_ITEM", true); } catch (System.Exception __ex) { Safe.Warn("FigurineAppraisal.InitAppraisal", __ex); } }
                return item;
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "造未鉴定件失败：" + e.GetType().Name); return null; }
        }

        // ───────────────────────── 到店 ─────────────────────────

        public static void OnShopOpened()
        {
            try
            {
                Register();
                // 未入会 → 手办鉴定客不来（用户定：isAppraisalIntroduced 之后才开始有生意）
                if (!FigurineCert.Introduced)
                {
                    Debug.Log(FigurineMod.LogTag + "鉴定客：还没入会（手办鉴定证书未拿），今天不来");
                    return;
                }
                int rank = Math.Max(0, Math.Min(3, Rank));
                int chance = FigurineCert.SpawnChanceOf(rank);
                if (UnityEngine.Random.Range(0, 100) >= chance)
                {
                    Debug.Log(FigurineMod.LogTag + "鉴定客：今天没来（等级 " + rank + " 概率 " + chance + "%）");
                    return;
                }
                int n = UnityEngine.Random.Range(FigurineCert.CountMinOf(rank), FigurineCert.CountMaxOf(rank) + 1);
                // 柜台放不下就别叫了：否则每件都会 "Destroyed unplaced items (front counter full)"
                int unowned = 0;
                try { unowned = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.GetAllNonOwendItemCount() : 0; }
                catch (System.Exception __ex) { Safe.Warn("FigurineAppraisal.CounterCount", __ex); }
                if (unowned >= 6)
                {
                    Debug.Log(FigurineMod.LogTag + "鉴定客：柜台已经堆了 " + unowned + " 件没人要的，今天不叫新的了");
                    return;
                }
                int ok = 0;
                for (int i = 0; i < n; i++)
                {
                    if (EmporiumEntry.Instance != null && EmporiumEntry.Instance.GetAllNonOwendItemCount() >= 8)
                    {
                        Debug.Log(FigurineMod.LogTag + "鉴定客：柜台满了，剩下的 " + (n - i) + " 位不叫了");
                        break;
                    }
                    if (SpawnOne(rank)) ok++;
                }
                Debug.Log(FigurineMod.LogTag + "鉴定客：等级 " + rank + " 今天来 " + ok + "/" + n + " 位（额外客流）");
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "鉴定客到店失败：" + e.GetType().Name); }
        }

        /// <summary>调试：不管概率，强制来 2 位鉴定客（F5）。</summary>
        public static void ForceSpawn(int n = 2)
        {
            try
            {
                Register();
                int rank = Math.Max(0, Math.Min(3, Rank));
                int ok = 0;
                for (int i = 0; i < n; i++) if (SpawnOne(rank)) ok++;
                Debug.Log(FigurineMod.LogTag + "鉴定客（强制）：来了 " + ok + "/" + n + " 位，等级 " + rank);
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "鉴定客强制到店失败：" + e.GetType().Name); }
        }

        private static bool SpawnOne(int rank)
        {
            try
            {
                var mgr = PlayerStore.Instance != null ? PlayerStore.Instance.storeClientManager : null;
                if (mgr == null) return false;

                var c = new StoreClient();
                c.identifier = ClientId;
                c.displayName = "手办鉴定客";
                c.realName = "手办鉴定客";
                c.dismissable = true;
                c.clientIntent = StoreClient.ClientIntent.APPRAISAL_SERVICE;
                c.clientFaction = "FACTION_LOWER_LEVEL";
                try { StoreClientFactionSetup.InitScav(c); } catch (System.Exception __ex) { Safe.Warn("FigurineAppraisal.InitScav", __ex); }
                try { c.spriteName = SpriteDict.GetRandomScavSpriteName(); } catch (System.Exception __ex) { Safe.Warn("FigurineAppraisal.Sprite", __ex); }

                // 台词：原版鉴定客那 10 条（手办化台词待用户给人设后替换）
                string line1 = SafeLoc("dialog_appraisal_request_1", "我这有几件手办，麻烦您给估个价。");
                string line2 = SafeLoc("dialog_appraisal_additional_1", "您看值多少，我照这个数收。");
                c.mainDialogue.SetText(c.displayName, line1).NextDialogue().SetText(c.displayName, line2);

                try { c.AddBasicDialog(); } catch (System.Exception __ex) { Safe.Warn("FigurineAppraisal.AddBasicDialog", __ex); }
                // 原版鉴定客建完也会走这一步，缺了它客户不会真正进场
                try { c.CompleteClientCreation(); } catch (System.Exception __ex) { Safe.Warn("FigurineAppraisal.Complete", __ex); }
                try { ModHelper.SetClientSource(c, "figurine.gs"); } catch (System.Exception __ex) { Safe.Warn("FigurineAppraisal.Source", __ex); }
                // 排队：**优先插队首**（AddNextClient）。
                // 2026-11-07 实测：装了「公司增强」等客流 mod 后队列能排到 17 位，
                // 鉴定客用 AddClient 会被排到当天队尾，永远轮不到（柜台没货、玩家看不到估价）。
                bool added = false;
                try { mgr.AddNextClient(c); added = true; }
                catch (System.Exception __ex) { Safe.Warn("FigurineAppraisal.AddNextClient", __ex); }
                if (!added)
                {
                    try { mgr.AddClient(c); added = true; }
                    catch (System.Exception __ex) { Safe.Warn("FigurineAppraisal.AddClient", __ex); }
                }
                // 顾客带着手办来：**到店后**再摆。
                // 以前是「入队就摆」：同一天会排 4~6 位鉴定客，一次性把 10~30 件 3×3 未鉴定件往
                // 18×4 的前柜台塞 → 柜台秒满，后面的全被 DestroyIfUnplaced 静默销毁，
                // 玩家看到的就是「鉴定客到场但柜台上没有未鉴定手办」（2026-11-05 用户报）。
                return true;
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "鉴定客入队失败：" + e.GetType().Name + " " + e.Message); return false; }
        }

        // ───────────────────────── 到店后摆货（2026-11-05 修） ─────────────────────────

        private static float _placeTimer = -1f;
        private static readonly List<GameItem> _deferred = new List<GameItem>();

        /// <summary>鉴定客真正到店（DoroArrivalPatch 在 TryCallNextClient 之后调用）。</summary>
        public static void OnArrived()
        {
            try
            {
                ClearDeferred("上一位鉴定客遗留");
                _placeTimer = 2.5f;
                Debug.Log(FigurineMod.LogTag + "鉴定客已到店，2.5 秒后摆未鉴定手办");
            }
            catch (System.Exception __ex) { Safe.Warn("FigurineAppraisal.OnArrived", __ex); }
        }

        /// <summary>每帧：到点摆货；柜台暂时放不下的先记着，等玩家清出位置再补。</summary>
        public static void Tick()
        {
            try
            {
                // 没活干就不要碰 PlayerStore.Instance：主菜单阶段它会被懒创建并抛 NRE（2026-11-06 实测）
                if (_placeTimer < 0f && _deferred.Count == 0) return;

                var ps = PlayerStore.Instance;
                var inst = ps != null ? ps.currentClientInstance : null;
                string cid = null;
                try { var bp = inst != null ? inst.GetClientBlueprint() : null; cid = bp != null ? bp.identifier : null; } catch { }
                bool onAppraiser = string.Equals(cid, ClientId, StringComparison.Ordinal);

                // 鉴定客已经走了：待摆/摆不下的未鉴定件全部销毁（它们只属于这一位顾客）
                if (!onAppraiser)
                {
                    _placeTimer = -1f;
                    ClearDeferred("鉴定客已离店");
                    return;
                }

                if (_deferred.Count > 0) RetryDeferred();

                if (_placeTimer < 0f) return;
                _placeTimer -= Time.unscaledDeltaTime;
                if (_placeTimer > 0f) return;
                _placeTimer = -1f;
                PlaceWares();
            }
            catch (System.Exception __ex) { Safe.Warn("FigurineAppraisal.Tick", __ex); }
        }

        private static void PlaceWares()
        {
            try
            {
                if (PlayerStore.Instance == null) return;
                int n = UnityEngine.Random.Range(WaresMin, WaresMax + 1);
                int front = 0, back = 0, deferred = 0;
                for (int i = 0; i < n; i++)
                {
                    var item = BuildUnappraised(PickDef());
                    if (item == null) continue;
                    if (TryPlaceFront(item)) front++;
                    else if (TryPlaceBack(item)) back++;
                    else { _deferred.Add(item); deferred++; }
                }
                Debug.Log(FigurineMod.LogTag + "鉴定客：摆出 前柜台 " + front + " 件，谈判柜台 " + back
                    + " 件，待摆 " + deferred + " 件（计划 " + n + " 件）");
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "鉴定客摆货失败：" + e.GetType().Name); }
        }

        /// <summary>放前柜台（不放不下时**不销毁**，返回 false 让调用方转后柜台/待摆）。</summary>
        private static bool TryPlaceFront(GameItem item)
        {
            try
            {
                var ee = EmporiumEntry.Instance;
                if (item == null || ee == null) return false;
                GeneralHelper.SetItemOwned(item, false);
                int left = GraphUtils.TryAcceptAll(ee.frontInvinvElement, item);
                if (left != 0) return false;
                try { StartingPerkContent.HandleGamblingAddictionItemAdded(item); } catch { }
                return true;
            }
            catch { return false; }
        }

        /// <summary>前柜台放不下时退到谈判柜台（放上去会自动弹估价框）。</summary>
        private static bool TryPlaceBack(GameItem item)
        {
            try
            {
                var ee = EmporiumEntry.Instance;
                if (item == null || ee == null) return false;
                GeneralHelper.SetItemOwned(item, false);
                int left = GraphUtils.TryAcceptAll(ee.backInvinvElement, item);
                return left == 0;
            }
            catch { return false; }
        }

        private static void RetryDeferred()
        {
            for (int i = _deferred.Count - 1; i >= 0; i--)
            {
                var it = _deferred[i];
                if (it == null) { _deferred.RemoveAt(i); continue; }
                if (TryPlaceFront(it) || TryPlaceBack(it)) _deferred.RemoveAt(i);
            }
        }

        private static void ClearDeferred(string reason)
        {
            if (_deferred.Count == 0) return;
            int n = _deferred.Count;
            foreach (var it in _deferred) { try { if (it != null) it.Destroy(); } catch { } }
            _deferred.Clear();
            Debug.LogWarning(FigurineMod.LogTag + "鉴定客：销毁 " + n + " 件没摆出去的未鉴定手办（" + reason + "）");
        }

        private static string SafeLoc(string key, string fallback)
        {
            try
            {
                string s = LocHelper.GetLocalizedDialogue(key);
                return string.IsNullOrEmpty(s) ? fallback : s;
            }
            catch { return fallback; }
        }

        // ───────────────────────── 鉴定费 + 结果票 ─────────────────────────

        /// <summary>当前正在估价的物品（OnEstimate 前置记下来，给结果票用）。</summary>
        public static GameItem Current;

        /// <summary>手办估价成功后，把紧接着那条「顾客应答」对白吞掉（顾客可能已经不在柜台，会变虚空对白）。</summary>
        public static bool SuppressNextClientDialog;

        public static void GiveFee(GameItem item, int value, int precision, int correctValue)
        {
            try
            {
                int baseFee = FigurineCert.BaseFeeOf(Rank);
                int accuracy = correctValue > 0
                    ? 100 - Mathf.RoundToInt(Mathf.Abs(value - correctValue) * 100f / correctValue)
                    : 0;
                bool fake = FigurineAuth.TruthFake(item);

                var sb = new System.Text.StringBuilder();
                sb.Append("真伪：").Append(fake ? "赝品" : "正品").Append('\n');
                sb.Append("估价准确率：").Append(accuracy).Append("%\n");
                sb.Append("真实价值：").Append(correctValue).Append('\n');
                sb.Append("费用说明：基础费 ").Append(baseFee).Append("（按精度抽成暂不做）");

                var ps = PlayerStore.Instance;
                if (ps != null) ps.playerCash += baseFee;

                string slipName = "手办鉴定结果票";
                try
                {
                    string loc = LocHelper.GetLocalizedMechanic("appraisal_slip_name");
                    if (!string.IsNullOrEmpty(loc)) slipName = loc;
                }
                catch (System.Exception __ex) { Safe.Warn("FigurineAppraisal.SlipName", __ex); }

                var paper = PreBuiltItemHelper.CreatePaperWithText(sb.ToString(), slipName);
                if (ps != null && paper != null)
                    try { ps.AddDirectSellingItemToTable(paper, true); } catch (System.Exception __ex) { Safe.Warn("FigurineAppraisal.Slip", __ex); }

                Debug.Log(FigurineMod.LogTag + "估价成功：" + (item != null ? item.identifier : "?")
                    + " 真伪=" + (fake ? "赝品" : "正品")
                    + " 估价=" + value + " 真值=" + correctValue + " 准确率=" + accuracy + "%"
                    + " 鉴定费=+" + baseFee);
                // 手办鉴定等级进度（按精度 1 完美 … 5 差）
                try { FigurineCert.AddProgress(precision); } catch (System.Exception __ex) { Safe.Warn("FigurineAppraisal.Progress", __ex); }
                // 紧接着原版会弹「顾客应答」，顾客可能已经离场 → 吞掉它
                SuppressNextClientDialog = true;
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "鉴定费发放失败：" + e.GetType().Name + " " + e.Message); }
        }
    }

    /// <summary>记下正在估价的物品（原版 OnEstimate 先拿到 gameItem）。</summary>
    [HarmonyPatch(typeof(AppraisalServiceHelper), "OnEstimate")]
    internal static class FigurineAppraisalTrackPatch
    {
        [HarmonyPrefix]
        private static void Prefix(GameItem gameItem)
        {
            // 注意：这里必须**每次都记**（包括非手办），否则上一位手办会残留，
            // 之后鉴定原版物品（仿生手等）时会被我们的 GiveFee 误接管。
            try
            {
                FigurineAppraisal.Current = gameItem;
                FigurineAppraisal.SuppressNextClientDialog = false;   // 每次估价先清，避免残留误吞别的对白
            }
            catch (Exception __ex) { Safe.Warn("FigurineAppraisal.Track", __ex); }
        }
    }

    /// <summary>手办的鉴定费只给基础费 + 手办化结果票；其它物品原样走原版。</summary>
    [HarmonyPatch(typeof(AppraisalServiceHelper), "GiveAppraisalFee")]
    internal static class FigurineAppraisalFeePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(int value, int precision, int correctValue)
        {
            try
            {
                var item = FigurineAppraisal.Current;
                if (item == null || !FigurineAuth.IsFigurine(item)) return true;   // 原版物品：不动
                FigurineAppraisal.GiveFee(item, value, precision, correctValue);
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning(FigurineMod.LogTag + "鉴定费补丁异常：" + e.GetType().Name);
                return true;
            }
        }
    }

    /// <summary>
    /// 未鉴定手办**不可购买**（用户 2026-11-04 定）：它们只用于「估价」，估完由顾客带走。
    /// 原版买货列表来自 `GetNonOwnedBackInvItems()`，这里把未鉴定件从列表里摘掉。
    /// </summary>
    [HarmonyPatch(typeof(NegociationUIManager), "GetNonOwnedBackInvItems")]
    internal static class FigurineNotSellablePatch
    {
        private static int _lastRemoved = -1;

        [HarmonyPostfix]
        private static void Postfix(ref System.Collections.Generic.List<GameItem> __result)
        {
            try
            {
                if (__result == null || __result.Count == 0) { _lastRemoved = 0; return; }
                int removed = __result.RemoveAll(it => it != null && FigurineAuth.IsUnappraised(it));
                // 这个方法每 0.2 秒被 RefreshNow 调一次，只在「摘掉的件数变化」时打日志，避免刷屏
                if (removed != _lastRemoved)
                {
                    _lastRemoved = removed;
                    if (removed > 0)
                        Debug.Log(FigurineMod.LogTag + "未鉴定手办不可购买：已从买货列表摘掉 " + removed + " 件");
                }
            }
            catch (Exception __ex) { Safe.Warn("FigurineNotSellable.Postfix", __ex); }
        }
    }

    /// <summary>
    /// 吞掉「虚空对白」：手办估价成功后原版会弹一条顾客应答（`appraisal_accept_*`），
    /// 但顾客常常已经离场，只剩一个没有人的气泡。这里只吞**紧接着的那一条 CLIENT 对白**。
    /// </summary>
    [HarmonyPatch(typeof(DialogUIManager), "StartDialogue", new Type[] { typeof(Dialogue), typeof(bool) })]
    internal static class FigurineGhostDialogPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Dialogue dialogue)
        {
            try
            {
                if (!FigurineAppraisal.SuppressNextClientDialog) return true;
                if (dialogue == null) return true;
                if (dialogue.title != "CLIENT") return true;   // 不是顾客应答：不吞
                FigurineAppraisal.SuppressNextClientDialog = false;
                Debug.Log(FigurineMod.LogTag + "已拦下手办估价后的顾客应答（避免虚空对白）："
                    + (dialogue.plainText ?? ""));
                return false;
            }
            catch (Exception __ex) { Safe.Warn("FigurineGhostDialog.Prefix", __ex); return true; }
        }
    }
}
