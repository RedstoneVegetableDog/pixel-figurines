using System;
using System.Collections.Generic;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 手办鉴定「等级 / 证书 / 委员会成员」（第 4 步，用户 2026-11-04 定）。
    ///
    /// · 独立 NPC：`figurine_appraisal_board`（外观用原版理事），与**原版鉴定完全独立**；
    /// · 上门日：正常剧情 **第 13 天** / 跳过开场 **第 10 天**（原版 8 / 5 之后 +5 天）；
    /// · 入会费：85 × 难度倍率（普通 85），到手 = 手办鉴定证书（学徒）+ 放大镜；
    /// · 拒绝「以后再说」→ **7 天后再来**；
    /// · 晋升：进度满 150/250/400 → 理事上门 → 付 85×1.25/1.5/1.75 → 换证；
    ///   （2026-11-06 用户要求「减客流 + 升难度」：晋升阈值 100/150/250 → 150/250/400，
    ///     单次进度也整体下调，鉴定客到店率/人数同步减少）
    /// · 等级/进度存 ModData，与 `appraiserLevel` 无关；证书丢了自动补发。
    /// </summary>
    internal static class FigurineCert
    {
        public const string BoardId = "figurine_appraisal_board";
        public const string BoardLaterId = "figurine_appraisal_board2";
        public const string BoardPromoId = "figurine_appraisal_board_promo";

        public const int MaxRank = 3;

        private const string KeyRank = "fig_rank";
        private const string KeyProgress = "fig_progress";
        private const string KeyIntro = "fig_intro";
        private const string KeyPromoDay = "fig_promo_day";
        private const string KeyNextVisit = "fig_next_visit";
        private const string KeyDeclined = "fig_declined";

        // 原版数值（AppraisalRankHelper）；客流/进度为 2026-11-06 用户要求下调后的平衡值
        private static readonly int[] BaseFee = { 20, 40, 60, 100 };
        private static readonly float[] Threshold = { 150f, 250f, 400f, 0f };
        private static readonly float[] CostShare = { 1f, 1.25f, 1.5f, 1.75f };
        // 到店率：33/42/48/55% → 20/25/30/35%
        private static readonly int[] SpawnChance = { 20, 25, 30, 35 };
        // 每天额外客流位：1~3/4~6/7~9/10~12 → 1~2/1~2/2~3/3~4
        private static readonly int[] CountMin = { 1, 1, 2, 3 };
        private static readonly int[] CountMax = { 2, 2, 3, 4 };
        private static readonly int[][] ProgressGain =
        {
            new[] { 8, 5, 3, 2, 1 },      // 学徒
            new[] { 7, 4, 2, 1, -2 },     // 初级
            new[] { 5, 3, 1, -2, -3 },    // 中级
            new[] { 0, 0, 0, 0, 0 },      // 高级（封顶）
        };

        private static readonly string[] CertNames =
        {
            "手办鉴定委员会认证证书",
            "手办鉴定委员会初级证书",
            "手办鉴定委员会高级认证证书",
            "手办鉴定委员会大师认证证书",
        };

        // ───────────────────────── 数值 ─────────────────────────

        public static int Rank
        {
            get
            {
                int v;
                if (!int.TryParse(ModData.Get(KeyRank, "0"), out v)) v = 0;
                return Math.Max(0, Math.Min(MaxRank, v));
            }
        }

        public static bool Introduced { get { return ModData.Get(KeyIntro, "0") == "1"; } }

        public static float Progress
        {
            get
            {
                float v;
                if (!float.TryParse(ModData.Get(KeyProgress, "0"), out v)) v = 0f;
                return v;
            }
        }

        public static int BaseFeeOf(int rank) { return BaseFee[Math.Max(0, Math.Min(MaxRank, rank))]; }
        public static float ThresholdOf(int rank) { return Threshold[Math.Max(0, Math.Min(MaxRank, rank))]; }
        public static int SpawnChanceOf(int rank) { return SpawnChance[Math.Max(0, Math.Min(MaxRank, rank))]; }
        public static int CountMinOf(int rank) { return CountMin[Math.Max(0, Math.Min(MaxRank, rank))]; }
        public static int CountMaxOf(int rank) { return CountMax[Math.Max(0, Math.Min(MaxRank, rank))]; }

        /// <summary>可鉴星级上限：学徒 2 / 初级 3 / 中级 4 / 高级 5。</summary>
        public static int MaxStarOf(int rank) { return Math.Min(5, 2 + Math.Max(0, Math.Min(MaxRank, rank))); }

        public static int JoinCost { get { try { return PlayerStore.Instance.GetAppraisalJoinCost(); } catch { return 85; } } }

        public static int PromoteCost(int toRank)
        {
            return Mathf.RoundToInt(JoinCost * CostShare[Math.Max(0, Math.Min(MaxRank, toRank))]);
        }

        // ───────────────────────── 物品注册 ─────────────────────────

        private static bool _registered;

        public static void Register(ModItemDirectory dir)
        {
            if (dir == null || _registered) return;
            _registered = true;
            for (int r = 0; r <= MaxRank; r++)
            {
                int rr = r;
                try { dir.Add(CertId(r), (Func<GameItem>)(() => BuildCert(rr))); }
                catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "注册手办证书失败：" + e.GetType().Name); }
            }
            Debug.Log(FigurineMod.LogTag + "手办鉴定证书已注册 4 张");
        }

        public static string CertId(int rank) { return "figurine_cert" + (Math.Max(0, Math.Min(MaxRank, rank)) + 1); }

        public static GameItem BuildCert(int rank)
        {
            try
            {
                rank = Math.Max(0, Math.Min(MaxRank, rank));
                var item = ItemDirectory.CreateEmptyItem(CertId(rank));
                if (item == null) return null;
                item.name = CertNames[rank];
                item.unitCount = 1;
                item.unitValue = 0;
                item.unitBaseValue = 0;
                item.shortDescription = "手办鉴定等级证明。";
                // 贴图借用原版 items_misc 的 cert1~cert4
                bool ok = false;
                try
                {
                    item.SetSpriteAndShape("Items/items_misc", "cert" + (rank + 1));
                    ok = true;
                }
                catch (System.Exception __ex) { Safe.Warn("FigurineCert.Sprite", __ex); }
                if (!ok)
                {
                    try { item.SetSpriteAndShapeFromMod("figurine.gs", "probe"); } catch (System.Exception __ex) { Safe.Warn("FigurineCert.SpriteFallback", __ex); }
                }
                try { item.SetGameItemType("DOCUMENT"); } catch (System.Exception __ex) { Safe.Warn("FigurineCert.Type", __ex); }
                try { item.EnableTag("IMPORTANT_TAG", true); } catch (System.Exception __ex) { Safe.Warn("FigurineCert.Tag", __ex); }
                try { GeneralHelper.SetItemOwned(item, true); } catch (System.Exception __ex) { Safe.Warn("FigurineCert.Owned", __ex); }
                // 用户 2026-11-04：证书不要双击功能（不挂 onActivateSlotItemFunc）
                return item;
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "建手办证书失败：" + e.GetType().Name); return null; }
        }

        // ───────────────────────── 存档读写 ─────────────────────────

        private static void SetRank(int r) { ModData.Set(KeyRank, Math.Max(0, Math.Min(MaxRank, r)).ToString()); }
        private static void SetProgress(float p) { ModData.Set(KeyProgress, p.ToString("0.#")); }
        private static void SetIntroduced() { ModData.Set(KeyIntro, "1"); }

        private static int NextVisit
        {
            get { int v; return int.TryParse(ModData.Get(KeyNextVisit, "0"), out v) ? v : 0; }
            set { ModData.Set(KeyNextVisit, value.ToString()); }
        }

        // ───────────────────────── 进度 ─────────────────────────

        /// <summary>估价成功时按精度（1 完美 … 5 差）加进度。</summary>
        public static void AddProgress(int grade)
        {
            try
            {
                int rank = Rank;
                if (rank >= MaxRank) return;
                int idx = Math.Max(1, Math.Min(5, grade)) - 1;
                float gain = ProgressGain[rank][idx];
                float th = Threshold[rank];
                float p = Mathf.Clamp(Progress + gain, 0f, th);
                SetProgress(p);
                if (p >= th && th > 0f)
                {
                    // 第二天理事上门谈晋升
                    try { ModData.Set(KeyPromoDay, (StoreStation.Instance.dayCounter + 1).ToString()); }
                    catch (System.Exception __ex) { Safe.Warn("FigurineCert.PromoDay", __ex); }
                }
                Debug.Log(FigurineMod.LogTag + "手办鉴定进度 +" + gain + " → " + p + "/" + th + "（等级 " + rank + "）");
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "进度写入失败：" + e.GetType().Name); }
        }

        private static int PromoDay
        {
            get { int v; return int.TryParse(ModData.Get(KeyPromoDay, "-1"), out v) ? v : -1; }
        }

        // ───────────────────────── 每天判断 ─────────────────────────

        public static void OnShopOpened()
        {
            try
            {
                EnsureCertItem();

                int day = 1;
                try { day = StoreStation.Instance.dayCounter; } catch (System.Exception __ex) { Safe.Warn("FigurineCert.Day", __ex); }
                bool skip = false;
                try { skip = PlayerStore.Instance.skipIntro; } catch (System.Exception __ex) { Safe.Warn("FigurineCert.Skip", __ex); }
                int introDay = skip ? 10 : 13;

                // 明确拒绝过（[不加入]）→ 委员会不再上门
                if (!Introduced && ModData.Get(KeyDeclined, "0") == "1") return;

                // 第一次：把上门日排进计划
                if (NextVisit == 0 && !Introduced) NextVisit = introDay;

                if (!Introduced)
                {
                    if (day >= introDay && day >= NextVisit)
                    {
                        SpawnBoard(false);
                        NextVisit = day + 7;      // 这次没谈成，一周后再来
                    }
                    return;
                }

                // 已入会：晋升
                int promoDay = PromoDay;
                if (Rank < MaxRank && Progress >= Threshold[Rank] && promoDay >= 0 && day >= promoDay)
                {
                    SpawnBoard(true);
                    try { ModData.Set(KeyPromoDay, (day + 7).ToString()); } catch (System.Exception __ex) { Safe.Warn("FigurineCert.PromoRetry", __ex); }
                }
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "手办理事排期失败：" + e.GetType().Name); }
        }

        // ───────────────────────── 理事上门 ─────────────────────────

        /// <summary>调试（F3）：把手办鉴定进度填满，用来测晋升。</summary>
        public static void DebugFillProgress()
        {
            try
            {
                int r = Rank;
                if (r >= MaxRank) { Debug.Log(FigurineMod.LogTag + "调试：已是最高等级，无法再晋升"); return; }
                SetProgress(Threshold[r]);
                try { ModData.Set(KeyPromoDay, StoreStation.Instance.dayCounter.ToString()); } catch (System.Exception __ex) { Safe.Warn("FigurineCert.DebugPromoDay", __ex); }
                Debug.Log(FigurineMod.LogTag + "调试：进度已填满 " + Threshold[r] + "，下次 F4 会来谈晋升（等级 " + r + "）");
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "调试进度失败：" + e.GetType().Name); }
        }

        /// <summary>调试（F4）：强制委员会成员上门（不等第 10/13 天）。
        /// 已入会 → 谈晋升；未入会 → 谈入会。会先把柜台前那位打发走（测试用）。</summary>
        public static void DebugSpawnBoard(bool? promo = null)
        {
            try
            {
                try
                {
                    var ps = PlayerStore.Instance;
                    if (ps != null && ps.currentClientInstance != null) ps.DismissCurrentClient(true);
                }
                catch (System.Exception __ex) { Safe.Warn("FigurineCert.DebugDismiss", __ex); }
                bool isPromo = promo ?? Introduced;
                Debug.Log(FigurineMod.LogTag + "调试：强制委员会上门（" + (isPromo ? "晋升" : "入会") + "）");
                SpawnBoard(isPromo);
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "调试上门失败：" + e.GetType().Name); }
        }

        private static void SpawnBoard(bool promo)
        {
            try
            {
                var mgr = PlayerStore.Instance != null ? PlayerStore.Instance.storeClientManager : null;
                if (mgr == null) return;

                int cost = promo ? PromoteCost(Rank + 1) : JoinCost;
                var c = new StoreClient();
                c.identifier = promo ? BoardPromoId : BoardId;
                c.nonShootable = true;
                c.dismissable = true;
                c.displayName = "手办鉴定委员会成员";
                c.realName = "手办鉴定委员会成员";
                c.clientIntent = StoreClient.ClientIntent.DIALOGUE;
                c.clientFaction = "FACTION_UPPER_LEVEL";
                try { c.spriteName = SpriteDict.GetClerkSpriteName(); } catch (System.Exception __ex) { Safe.Warn("FigurineCert.Sprite", __ex); }

                // ── 对话：照原版「鉴定委员会成员」那套（台词取自游戏本地化）──
                // 选项图标：原版用 DirectoryMaster.Item("$")，模组上下文里取不到（会变 dummy），
                // 所以用我们自己的证书物品当图标。
                GameItem icon = null;
                try { icon = BuildCert(Rank); } catch (System.Exception __ex) { Safe.Warn("FigurineCert.Icon", __ex); }
                // 不再回退到 DirectoryMaster.Item("$")：那个 id 在模组上下文里取不到，
                // 只会打 "Could not find GameItem identifier '$'" 的噪音。
                var leave = (Action)(() =>
                {
                    try { DialogUIManager.Instance.CurrentClientLeave(); }
                    catch (System.Exception __ex) { Safe.Warn("FigurineCert.Leave", __ex); }
                });

                // 入会成功（原版 join_1..4）
                var joinDialogue = new Dialogue();
                joinDialogue.SetText("PLAYER", "好，我加入。")
                    .NextDialogue().SetText(c.displayName, "明智之选！欢迎加入委员会。")
                    .NextDialogue().SetText(c.displayName, "我们会开始把顾客派到你这里来。")
                    .NextDialogue().SetText(c.displayName, "过几天我会再来看看你这边的情况。")
                    .SetStartAction(() =>
                    {
                        try { PlayerStore.Instance.playerCash -= cost; } catch (System.Exception __ex) { Safe.Warn("FigurineCert.Pay", __ex); }
                        SetIntroduced();
                        SetRank(0);
                        SetProgress(0f);
                        GiveCert(0);
                        Debug.Log(FigurineMod.LogTag + "手办鉴定：入会成功，付 " + cost + "，拿到「" + CertNames[0] + "」");
                    })
                    .SetEndAction(leave);

                // 不加入（原版 decline_1..2）→ 不再来
                var declineDialogue = new Dialogue();
                declineDialogue.SetText("PLAYER", "不加入。")
                    .NextDialogue().SetText(c.displayName, Loc("dialog_appraisal_board_decline_1", "真遗憾，我还以为你是块可造之材。"))
                    .NextDialogue().SetText(c.displayName, Loc("dialog_appraisal_board_decline_2", "再见。"))
                    .SetStartAction(() =>
                    {
                        ModData.Set(KeyDeclined, "1");
                        Debug.Log(FigurineMod.LogTag + "手办鉴定：玩家拒绝入会，委员会不再上门");
                    })
                    .SetEndAction(leave);

                // 晚点再来（原版 later_1）→ 7 天
                var laterDialogue = new Dialogue();
                laterDialogue.SetText("PLAYER", "以后再说。")
                    .NextDialogue().SetText(c.displayName, Loc("dialog_appraisal_board_later_1", "没问题，我过几天再来。"))
                    .SetStartAction(() =>
                    {
                        try { NextVisit = StoreStation.Instance.dayCounter + 7; } catch (System.Exception __ex) { Safe.Warn("FigurineCert.Later", __ex); }
                        Debug.Log(FigurineMod.LogTag + "手办鉴定：入会推迟 7 天");
                    })
                    .SetEndAction(leave);

                // 晋升成功（原版 promo_accept_1..2）
                var promoYes = new Dialogue();
                promoYes.SetText("PLAYER", "好，我升。")
                    .NextDialogue().SetText(c.displayName, Loc("dialog_appraisal_promo_accept_1", "恭喜，这是你的新证书。"))
                    .NextDialogue().SetText(c.displayName, Loc("dialog_appraisal_promo_accept_2", "今后来找你的顾客，要求可就更高了。"))
                    .SetStartAction(() =>
                    {
                        try { PlayerStore.Instance.playerCash -= cost; } catch (System.Exception __ex) { Safe.Warn("FigurineCert.Pay", __ex); }
                        Promote();
                    })
                    .SetEndAction(leave);

                var head = new Dialogue();
                if (promo)
                {
                    string promoLine = Loc("dialog_appraisal_promo_1", "委员会希望将你晋升为{0}。");
                    try { promoLine = string.Format(promoLine, RankName(Rank + 1)); } catch { promoLine = "委员会希望将你晋升为" + RankName(Rank + 1) + "。"; }
                    string feeLine = Loc("dialog_appraisal_promo_fee", "认证费用为{0}信用点。");
                    try { feeLine = string.Format(feeLine, cost); } catch { feeLine = "认证费用为" + cost + "信用点。"; }

                    head.SetText(c.displayName, Loc("dialog_appraisal_board2_intro_1", "你最近的鉴定表现引起了委员会的注意。"))
                        .NextDialogue().SetText(c.displayName, promoLine)
                        .NextDialogue().SetText(c.displayName, Loc("dialog_appraisal_promo_2", "今后每件物品的鉴定报酬都会更高，不过委员会也会要求你的估价更加准确。"))
                        .NextDialogue().SetText(c.displayName, Loc("dialog_appraisal_promo_items", "你也将获得鉴定新物品种类的资格。"))
                        .NextDialogue().SetText(c.displayName, feeLine);
                }
                else
                {
                    string feeLine = Loc("dialog_appraisal_board_fee", "入会费只要{0}信用点。");
                    try { feeLine = string.Format(feeLine, cost); } catch { feeLine = "入会费只要" + cost + "信用点。"; }

                    head.SetText(c.displayName, "你好，我是手办鉴定委员会的成员。")
                        .NextDialogue().SetText(c.displayName, "某些手办具有复杂的价值结构。")
                        .NextDialogue().SetText(c.displayName, Loc("dialog_appraisal_board_intro_3", "我们帮助顾客确定其确切价值。"))
                        .NextDialogue().SetText(c.displayName, Loc("dialog_appraisal_board_intro_4", "总之，我听说你刚搬来。"))
                        .NextDialogue().SetText(c.displayName, Loc("dialog_appraisal_board_intro_5", "你的店铺可以提供鉴定服务。"))
                        .NextDialogue().SetText(c.displayName, Loc("dialog_appraisal_board_intro_6", "这将为你提供额外的收入来源。"))
                        .NextDialogue().SetText(c.displayName, Loc("dialog_appraisal_board_intro_7", "你有兴趣作为鉴定师加入委员会吗？"))
                        .NextDialogue().SetText(c.displayName, feeLine);
                }

                var tail = head;
                while (tail.nextDialogue != null) tail = tail.nextDialogue;

                if (promo)
                {
                    tail.AddChoice(icon, "认证（" + cost + "）", "", promoYes, null, () => Cash() >= cost);
                    tail.AddChoice(icon, "以后再说", "", laterDialogue, null, () => true);
                    tail.AddChoice(icon, "明确拒绝，不再考虑", "", declineDialogue, null, () => true);
                }
                else
                {
                    tail.AddChoice(icon, "[加入委员会（" + cost + "信用点）]", "", joinDialogue, null, () => Cash() >= cost);
                    tail.AddChoice(icon, "[让对方晚点再来]", "", laterDialogue, null, () => true);
                    tail.AddChoice(icon, "[不加入]", "", declineDialogue, null, () => true);
                }

                c.mainDialogue = head;
                try { c.CompleteClientCreation(); } catch (System.Exception __ex) { Safe.Warn("FigurineCert.Complete", __ex); }
                try { ModHelper.SetClientSource(c, "figurine.gs"); } catch (System.Exception __ex) { Safe.Warn("FigurineCert.Source", __ex); }
                try { mgr.AddNextClient(c); } catch (System.Exception __ex) { Safe.Warn("Cert.AddNextClient", __ex); }   // 插队首
                Debug.Log(FigurineMod.LogTag + "手办理事已上门（" + (promo ? "晋升" : "入会") + "，费用 " + cost + "）");
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "手办理事上门失败：" + e.GetType().Name + " " + e.Message); }
        }

        private static long Cash()
        {
            try { return PlayerStore.Instance != null ? PlayerStore.Instance.playerCash : 0; } catch { return 0; }
        }

        private static void Promote()
        {
            try
            {
                int old = Rank;
                int next = Math.Min(MaxRank, Rank + 1);
                SetRank(next);
                SetProgress(0f);
                ModData.Set(KeyPromoDay, "-1");
                // 旧证书销毁（否则会叠两张；照原版 ReplaceCertificate 的做法）
                try { DestroyCert(old); } catch (System.Exception __ex) { Safe.Warn("FigurineCert.OldCert", __ex); }
                GiveCert(next);
                Debug.Log(FigurineMod.LogTag + "手办鉴定：晋升到等级 " + next + "（" + CertNames[next] + "）");
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "晋升失败：" + e.GetType().Name); }
        }

        /// <summary>销毁指定等级的证书（晋升换证用）。</summary>
        private static void DestroyCert(int rank)
        {
            try
            {
                var all = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.GetAllItems() : null;
                if (all == null) return;
                string id = CertId(rank);
                var doomed = new System.Collections.Generic.List<GameItem>();
                foreach (var it in all) if (it != null && it.identifier == id) doomed.Add(it);
                foreach (var it in doomed)
                {
                    try { it.Destroy(); } catch (System.Exception __ex) { Safe.Warn("FigurineCert.Destroy", __ex); }
                }
                if (doomed.Count > 0) Debug.Log(FigurineMod.LogTag + "手办鉴定：换证，销毁旧证书 " + doomed.Count + " 张（等级 " + rank + "）");
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "销毁旧证书失败：" + e.GetType().Name); }
        }

        // ───────────────────────── 证书发放 ─────────────────────────

        private static void GiveCert(int rank)
        {
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null) return;
                var item = BuildCert(rank);
                if (item == null) return;
                try { ps.AddDirectSellingItemToTable(item, true); }
                catch (System.Exception __ex) { Safe.Warn("FigurineCert.Give", __ex); }
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "发证书失败：" + e.GetType().Name); }
        }

        /// <summary>证书丢了自动补发（读档/开店时各查一次）。</summary>
        public static void EnsureCertItem()
        {
            try
            {
                if (!Introduced) return;
                if (HasCert(Rank)) return;
                GiveCert(Rank);
                Debug.Log(FigurineMod.LogTag + "手办鉴定证书丢失 → 已补发（等级 " + Rank + "）");
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "补发证书失败：" + e.GetType().Name); }
        }

        public static bool HasCert(int rank)
        {
            try
            {
                string id = CertId(rank);
                var ps = PlayerStore.Instance;
                if (ps == null) return false;
                // 先扫所有库存顶层物品；再递归进「容器内容窗口」（档案箱/货物邮箱/背包等），
                // 以前只扫顶层 → 证书放进档案箱后读档检测不到，导致无限补发（2026-11-06 用户报）。
                var seen = new HashSet<GraphNodeStorage>();
                foreach (var it in ps.FindAllItem(false))
                {
                    if (it == null) continue;
                    if (string.Equals(it.identifier, id, StringComparison.Ordinal)) return true;
                    if (ScanContainer(it, id, seen)) return true;
                }
            }
            catch (System.Exception __ex) { Safe.Warn("FigurineCert.HasCert", __ex); }
            return false;
        }

        /// <summary>递归检查某个物品（容器）内容窗口里的所有物品。</summary>
        private static bool ScanContainer(GameItem item, string id, HashSet<GraphNodeStorage> seen)
        {
            try
            {
                if (item == null) return false;
                GraphNodeWindow child = null;
                try { child = item.child; } catch { }
                if (child == null) return false;
                if (!seen.Add(child)) return false;
                foreach (var it in GraphUtils.FindAllChildrenType<GameItem>(child))
                {
                    if (it == null) continue;
                    if (string.Equals(it.identifier, id, StringComparison.Ordinal)) return true;
                    if (ScanContainer(it, id, seen)) return true;
                }
            }
            catch (System.Exception __ex) { Safe.Warn("FigurineCert.ScanContainer", __ex); }
            return false;
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

        private static string Loc(string key, string fallback) { return SafeLoc(key, fallback); }

        private static string RankName(int rank)
        {
            switch (rank)
            {
                case 0: return "认证鉴定师";
                case 1: return "初级鉴定师";
                case 2: return "高级鉴定师";
                default: return "大师鉴定师";
            }
        }
    }
}
