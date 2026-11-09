using System;
using HarmonyLib;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 手办盲盒：3×4、价值 300，双击开启。
    ///
    /// 开启流程照 0.8.0-beta：
    /// ```
    /// 双击 → IsEmpty 拦截 → Open()
    ///   ├─ BoxAnimation.Enabled → BoxAnimation.Enqueue(box)     ← 动画播完在 EnterHold 里发奖
    ///   └─ 否                    → OpenImmediate(box)
    /// 发奖 = GrantPrize(box, def, prize)：手办进模组仓库 + Consume(box) 把盒子变空
    /// ```
    ///
    /// ⚠️ 与 beta 的唯一差异（**既有移植行为，不是这次动画引入的**）：
    ///    beta 的 `GrantPrize` 是把奖品 `AddDirectToWeightedTable` 摆到谈判柜台；
    ///    正式版按用户要求改成**直接进背包**（抽奖才进模组仓库）。
    /// </summary>
    internal static class BlindBox
    {
        // 四种盲盒
        public const string IdGs = "figurine_gs_blindbox";           // 原神
        public const string IdSr = "figurine_gs_blindbox_sr";        // 星穹铁道
        public const string IdZzz = "figurine_gs_blindbox_zzz";      // 绝区零
        public const string IdNormal = "figurine_gs_blindbox_normal";// 普通（三 IP 混抽）
        // 高级档（同样的四个系列，池子偏高档位）
        public const string IdGsPlus = "figurine_gs_blindbox_gs_plus";
        public const string IdSrPlus = "figurine_gs_blindbox_sr_plus";
        public const string IdZzzPlus = "figurine_gs_blindbox_zzz_plus";
        public const string IdNormalPlus = "figurine_gs_blindbox_normal_plus";
        public const string Id = IdGs;                                // 兼容旧引用
        public const string EmptySprite = "figurine_gs_blindbox_empty";
        /// <summary>内部标记：这个盒子已经开过（成功开出奖品 → 变空盒）。</summary>
        /// 比用 spritePath 判断可靠：即使贴图刷新失败，也不可能二次开启。
        public const string TagOpened = "FIG_BOX_OPENED";
        public const int Value = 500;            // 三个 IP 专属盒
        public const int ValueNormal = 400;      // 普通盒（三 IP 混抽）

        public static int PriceOf(string id)
        {
            if (id == IdNormal) return ValueNormal;                  // 400
            if (id == IdNormalPlus) return 700;                       // 普通高级
            if (id == IdGsPlus || id == IdSrPlus || id == IdZzzPlus) return 800;  // 系列高级
            return Value;                                             // 500
        }

        private static readonly string[] AllIds = { IdGs, IdSr, IdZzz, IdNormal, IdGsPlus, IdSrPlus, IdZzzPlus, IdNormalPlus };

        /// <summary>从全部 8 种盲盒里随机取一个 id（补货等场景用）。</summary>
        public static string RandomId()
        {
            return AllIds[UnityEngine.Random.Range(0, AllIds.Length)];
        }

        // 星级权重（1★→5★），普通档偏低星、高级档偏高星
        private static readonly int[] WeightNormal = { 45, 28, 16, 8, 3 };   // 普通档：偏低星
        private static readonly int[] WeightPlus   = { 10, 18, 27, 28, 17 };

        /// <summary>贴图 key：高级档直接复用同系列普通档的图（不再单独出图）。</summary>
        public static string SpriteKeyOf(string id)
        {
            if (id == IdGsPlus) return IdGs;
            if (id == IdSrPlus) return IdSr;
            if (id == IdZzzPlus) return IdZzz;
            if (id == IdNormalPlus) return IdNormal;
            return id;
        }

        public static bool IsBoxId(string id)
        {
            foreach (var x in AllIds) if (x == id) return true;
            return false;
        }

        /// <summary>每种盒子各自的空盒贴图。</summary>
        public static string EmptySpriteFor(string id)
        {
            if (id == IdSr || id == IdSrPlus) return "figurine_gs_blindbox_sr_empty";
            if (id == IdZzz || id == IdZzzPlus) return "figurine_gs_blindbox_zzz_empty";
            if (id == IdNormal || id == IdNormalPlus) return "figurine_gs_blindbox_normal_empty";
            return "figurine_gs_blindbox_empty";
        }

        private static string NameOf(string id)
        {
            if (id == IdSr) return "手办盲盒 · 星穹铁道";
            if (id == IdZzz) return "手办盲盒 · 绝区零";
            if (id == IdNormal) return "手办盲盒（普通）";
            if (id == IdGsPlus) return "手办盲盒 · 原神（高级）";
            if (id == IdSrPlus) return "手办盲盒 · 星穹铁道（高级）";
            if (id == IdZzzPlus) return "手办盲盒 · 绝区零（高级）";
            if (id == IdNormalPlus) return "手办盲盒（普通·高级）";
            return "手办盲盒 · 原神";
        }

        /// <summary>该盒子的星级权重表（下标 = 星级-1）。普通档偏低星，高级档偏高星。</summary>
        public static int[] WeightsOf(string id)
        {
            return (id == IdGsPlus || id == IdSrPlus || id == IdZzzPlus || id == IdNormalPlus) ? WeightPlus : WeightNormal;
        }

        /// <summary>按权重抽一个奖品（普通档偏低星、高级档偏高星）。</summary>
        public static Catalog.Def PickPrize(string id)
        {
            var pool = PoolFor(id);
            if (pool.Count == 0) return default(Catalog.Def);
            var w = WeightsOf(id);
            int total = 0;
            foreach (var d in pool) total += w[Mathf.Clamp(d.Star - 1, 0, 4)];
            if (total <= 0) return pool[UnityEngine.Random.Range(0, pool.Count)];
            int r = UnityEngine.Random.Range(0, total);
            foreach (var d in pool)
            {
                r -= w[Mathf.Clamp(d.Star - 1, 0, 4)];
                if (r < 0) return d;
            }
            return pool[pool.Count - 1];
        }

        /// <summary>某只盲盒的奖品池。</summary>
        public static System.Collections.Generic.List<Catalog.Def> PoolFor(string id)
        {
            var list = new System.Collections.Generic.List<Catalog.Def>();
            try
            {
                if (id == IdSr) list.AddRange(StarRailData.ToCatalog());
                else if (id == IdZzz) list.AddRange(ZzzData.ToCatalog());
                else if (id == IdNormal)
                {
                    list.AddRange(Catalog.All);
                    list.AddRange(StarRailData.ToCatalog());
                    list.AddRange(ZzzData.ToCatalog());
                }
                else list.AddRange(Catalog.All);
            }
            catch (System.Exception __ex) { Safe.Warn("BlindBox.cs:130", __ex); }
            return list;
        }

        public static System.Collections.Generic.List<Catalog.Def> PoolFor(GameItem box)
        {
            try { return PoolFor(box != null ? box.identifier : IdGs); } catch (System.Exception __ex) { Safe.Warn("BlindBox.cs:136", __ex); }
            return PoolFor(IdGs);
        }

        public static void Register(ModItemDirectory dir)
        {
            if (dir == null) return;
            foreach (var id in AllIds)
            {
                string bid = id;
                try { dir.Add(bid, (Func<GameItem>)(() => Build(bid))); } catch (System.Exception __ex) { Safe.Warn("BlindBox.cs:146", __ex); }
            }
        }

        public static GameItem Build() { return Build(IdGs); }

        public static GameItem Build(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id)) id = IdGs;
                var item = ItemDirectory.CreateEmptyItem(id);
                if (item == null) return null;
                item.name = NameOf(id);
                if (id == IdGsPlus || id == IdSrPlus || id == IdZzzPlus || id == IdNormalPlus)
                {
                    try { item.shortDescription = "高级：增加稀有度高的手办抽取概率"; } catch (System.Exception __ex) { Safe.Warn("BlindBox.cs:162", __ex); }
                }
                else
                {
                    try { item.shortDescription = "普通：搏一搏，单车变摩托"; } catch (System.Exception __ex) { Safe.Warn("BlindBox.cs:166", __ex); }
                }
                item.unitCount = 1;
                item.unitValue = PriceOf(id);
                item.unitBaseValue = PriceOf(id);
                item.SetSpriteAndShapeFromMod("figurine.gs", SpriteKeyOf(id));
                try
                {
                    var sb = new GridShapeBuilder(4, 3);
                    sb.SetDataFill(4, 3, 1);
                    item.SetShape(sb.Build());
                }
                catch (System.Exception __ex) { Safe.Warn("BlindBox.cs:178", __ex); }

                try { item.SetGameItemType("MATERIAL"); } catch (System.Exception __ex) { Safe.Warn("BlindBox.cs:180", __ex); }
                try { GeneralHelper.SetItemOwned(item, true); } catch (System.Exception __ex) { Safe.Warn("BlindBox.cs:181", __ex); }
                // 新盒子 = 可开一次
                try { item.DisableTag(TagOpened, true); } catch { }
                return item;
            }
            catch { return null; }
        }

        public static GameItem BuildEmpty()
        {
            try
            {
                var item = ItemDirectory.CreateEmptyItem(Id);
                if (item == null) return null;
                item.name = "手办盲盒（空）";
                item.unitCount = 1;
                item.unitValue = 30;
                item.unitBaseValue = 30;
                item.SetSpriteAndShapeFromMod("figurine.gs", EmptySprite);
                try { var sb = new GridShapeBuilder(4, 3); sb.SetDataFill(4, 3, 1); item.SetShape(sb.Build()); } catch (System.Exception __ex) { Safe.Warn("BlindBox.cs:198", __ex); }
                try { GeneralHelper.SetItemOwned(item, true); } catch (System.Exception __ex) { Safe.Warn("BlindBox.cs:199", __ex); }
                try { item.EnableTag(TagOpened, true); } catch { }
                try { item.SetGameItemType("MATERIAL"); item.SetGameItemType("JUNK"); } catch (System.Exception __ex) { Safe.Warn("BlindBox.cs:200", __ex); }
                return item;
            }
            catch { return null; }
        }

        /// <summary>是否已经是空盒：优先看内部标记，旧存档回退到 spritePath 判断。</summary>
        public static bool IsEmpty(GameItem item)
        {
            try
            {
                if (item == null) return false;
                try { if (item.IsTag(TagOpened)) return true; } catch { }
                string sp = item.spritePath;
                return sp == EmptySprite
                    || sp == "figurine_gs_blindbox_sr_empty"
                    || sp == "figurine_gs_blindbox_zzz_empty"
                    || sp == "figurine_gs_blindbox_normal_empty";
            }
            catch { return false; }
        }

        // ───────────────────────── 开启 ─────────────────────────

        /// <summary>beta 的 `Open`：优先走动画，动画入口异常就退回即时开。</summary>
        public static void Open(GameItem box)
        {
            if (box == null) return;
            try
            {
                if (BoxAnimation.Enabled)
                {
                    BoxAnimation.Enqueue(box);
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(FigurineMod.LogTag + "[盲盒] 动画入口异常：" + ex.GetType().Name + ": " + ex.Message);
            }
            OpenImmediate(box);
        }

        /// <summary>不开动画（或动画建 UI 失败时的兜底）。</summary>
        public static void OpenImmediate(GameItem box)
        {
            try
            {
                var pool = PoolFor(box);
                if (pool == null || pool.Count == 0) { Debug.LogWarning(FigurineMod.LogTag + "盲盒：奖品池为空"); return; }
                var def = pool[UnityEngine.Random.Range(0, pool.Count)];
                GrantPrize(box, def, null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(FigurineMod.LogTag + "[盲盒] 开启失败：" + ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>占位：beta 在这里更新盲盒保底计数，正式版移植没有保底系统，保留调用点。</summary>
        public static void Acknowledge(Catalog.Def def) { }

        /// <summary>
        /// 发奖：**手办直接进背包** → 消耗盲盒。
        /// （抽奖那边走仓库，两条路径不同：盲盒开出来是实物，抽奖进仓库再由玩家取出。）
        /// 背包放不下就**不消耗盒子**，并提示。
        /// </summary>
        public static void GrantPrize(GameItem box, Catalog.Def def, GameItem prize)
        {
            try
            {
                if (string.IsNullOrEmpty(def.Id)) return;

                if (prize == null) prize = FigurineMod.BuildFigurine(def);
                if (prize == null)
                {
                    Debug.LogWarning(FigurineMod.LogTag + "[盲盒] 构建奖品失败：" + def.Id + " → 盒子保留");
                    return;
                }

                var inv = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.invElement : null;
                int before = -1;
                try { before = inv != null && inv.childItems != null ? inv.childItems.Count : -1; } catch (System.Exception __ex) { Safe.Warn("BlindBox.cs:281", __ex); }

                // 只放玩家背包：**不再**走 GraphUtils.EmporiumTryAdd 的兜底链。
                // 那个方法背包满时会依次塞进后柜台/展示柜/前柜台，而这里只检查 inv.Contains →
                // 会出现「判定没放进去、盒子不消耗，但奖品已经出现在别的库存里」→ 无限开（2026-11-06 用户报）。
                bool placed = false;
                try { if (inv != null) placed = FigurineMod.TryPlace(inv, prize); }
                catch (Exception e1) { Debug.LogWarning(FigurineMod.LogTag + "[盲盒] 入包异常：" + e1.GetType().Name); }
                if (placed)
                {
                    try { placed = inv.childItems != null && inv.childItems.Contains(prize) && prize.parentInventory == inv; }
                    catch { placed = false; }
                }

                // 没放进去的奖品直接销毁，避免「看不见的奖品」污染库存；盒子保留
                if (!placed && prize.parentInventory == null)
                {
                    try { prize.Destroy(); } catch { }
                }

                int after = -1;
                try { after = inv != null && inv.childItems != null ? inv.childItems.Count : -1; } catch (System.Exception __ex) { Safe.Warn("BlindBox.cs:297", __ex); }
                Debug.Log(FigurineMod.LogTag + "[盲盒] 入包尝试 " + def.Zh + " placed=" + placed
                    + " before=" + before + " after=" + after);

                if (!placed)
                {
                    ShowMessage("背包放不下：" + def.Zh + " 已销毁，盒子保留（腾出位置后再开）");
                    Debug.LogWarning(FigurineMod.LogTag + "[盲盒] 背包放不下 " + def.Zh + " → 盒子保留（奖品已销毁）");
                    return;
                }

                Consume(box);
                Debug.Log(FigurineMod.LogTag + "盲盒开启：" + def.Zh + " 已进背包");
            }
            catch (Exception ex)
            {
                Debug.LogWarning(FigurineMod.LogTag + "[盲盒] 发奖异常：" + ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>beta 的 `Consume`：数量 &gt;1 就减一，否则整只变成空盒。</summary>
        private static void Consume(GameItem box)
        {
            if (box == null) return;
            try
            {
                int n = 1;
                try { n = box.unitCount; } catch (System.Exception __ex) { Safe.Warn("BlindBox.cs:323", __ex); }

                if (n > 1)
                {
                    try { box.unitCount = n - 1; } catch (System.Exception __ex) { Safe.Warn("BlindBox.cs:327", __ex); }
                    Debug.Log(FigurineMod.LogTag + "盲盒：剩余 " + (n - 1) + " 个");
                }
                else
                {
                    MakeEmpty(box);
                    Debug.Log(FigurineMod.LogTag + "盲盒：已拆开 → 变成「手办盲盒（空）」");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(FigurineMod.LogTag + "[盲盒] 变形失败：" + ex.GetType().Name);
            }
        }

        /// <summary>beta 的 `MakeEmpty`：换贴图 + 改价 + 加 JUNK 类型（形状保持 3×4）。</summary>
        private static void MakeEmpty(GameItem box)
        {
            if (box == null) return;
            // ⚠️ 不能只写 spritePath：`spriteChanged` 是 protected，外部改路径不会触发重绘，
            //    实测背包里仍然显示原来的彩盒贴图。走 SetSpriteAndShapeFromMod 才会真正刷新。
            string empty = EmptySpriteFor(box.identifier);
            try { box.SetSpriteAndShapeFromMod("figurine.gs", empty); } catch (System.Exception __ex) { Safe.Warn("BlindBox.cs:349", __ex); }
            // 关键：用内部标记锁死「只能开一次」，不依赖贴图是否刷新成功
            try { box.EnableTag(TagOpened, true); } catch { }
            try { box.name = "手办盲盒（空）"; } catch (System.Exception __ex) { Safe.Warn("BlindBox.cs:350", __ex); }
            try { box.unitValue = 30; box.unitBaseValue = 30; } catch (System.Exception __ex) { Safe.Warn("BlindBox.cs:351", __ex); }
            try { box.SetGameItemType("MATERIAL"); box.SetGameItemType("JUNK"); } catch (System.Exception __ex) { Safe.Warn("BlindBox.cs:352", __ex); }
            try { box.SyncModifiedState(); } catch (System.Exception __ex) { Safe.Warn("BlindBox.cs:353", __ex); }
        }

        // ───────────────────────── 接入点 ─────────────────────────

        [HarmonyPatch(typeof(ItemMouseDoubleClickHandler), "DoubleClickAction")]
        internal static class OpenPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(GameItem __0)
            {
                try
                {
                    if (__0 == null || !IsBoxId(__0.identifier)) return true;
                    // 未拥有 → 锁住双击互动（买下/拿到手、状态变成已拥有后才解锁）
                    if (!GeneralHelper.IsItemOwned(__0))
                    {
                        ShowMessage("这个盲盒还不是你的，先买下来/捡起来再开");
                        Debug.Log(FigurineMod.LogTag + "盲盒：未拥有，双击已锁：" + __0.identifier);
                        return false;
                    }
                    if (IsEmpty(__0))
                    {
                        Debug.Log(FigurineMod.LogTag + "盲盒：这个盒子已经拆过了");
                        return false;
                    }
                    Open(__0);
                    return false;
                }
                catch { return true; }
            }
        }

        /// <summary>给玩家看的短提示（用游戏自带 TooltipManager，不额外建 UI）。</summary>
        public static void ShowMessage(string msg)
        {
            try { if (TooltipManager._instance != null) TooltipManager._instance.SetAndShowTooltip(msg); }
            catch (System.Exception __ex) { Safe.Warn("BlindBox.ShowMessage", __ex); }
        }
    }
}
