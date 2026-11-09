using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 手办「正品 / 赝品」标签 —— 存在**原版的条件特征**里（用户 2026-11-04 定，A 方案迁移）。
    ///
    /// 为什么：原版红色标签机只认 `ItemFeature`，且它改的是 `fakeCondition`（标签），
    /// `realCondition`（真身）动不到 —— 天然满足「标签机只改标签、真身只有鉴定才知道」。
    ///
    /// 结构：
    ///   category      = CATEGORY_FIGURINE_AUTH（注册进 ItemConditionList.conditionLookup）
    ///   fakeCondition = 正品 / 赝品（玩家看到的标签）
    ///   realCondition = 真身（鉴定前不可见，鉴定后由 FigurineAuth 决定价格）
    ///
    /// 旧存档兼容：没有特征的物品，读标签时退回旧 tag `FIG_LABEL_FAKE`；
    /// 打开标签机 / 新建物品时会把特征补上。
    /// </summary>
    internal static class FigurineLabelFeature
    {
        public const string Category = "CATEGORY_FIGURINE_AUTH";
        public const string FeatureId = "figurineAuthLabel";
        public const string GenuineId = "figurineGenuine";
        public const string FakeId = "figurineFake";

        public const string DisplayGenuine = "正品";
        public const string DisplayFake = "赝品";

        private static ItemCondition _genuine;
        private static ItemCondition _fake;

        // ───────────────────────── 条件注册 ─────────────────────────

        public static void Register()
        {
            if (_genuine != null) return;
            try
            {
                _genuine = new ItemCondition
                {
                    identifier = GenuineId,
                    display = DisplayGenuine,
                    modValue = 0,
                    modValueDisplay = 0,
                    category = Category,
                    hiddenAsPublic = false,
                };
                _fake = new ItemCondition
                {
                    identifier = FakeId,
                    display = DisplayFake,
                    modValue = 0,              // 真正价格由 FigurineAuth.PricePercent 算（标赝品 60%），这里不能再扣一次
                    modValueDisplay = -40,     // 只用于标签机面板显示：赝品 = 正品价 -40%
                    category = Category,
                    hiddenAsPublic = false,
                };
                if (!ItemConditionList.conditionLookup.ContainsKey(Category))
                    ItemConditionList.conditionLookup[Category] = new List<ItemCondition> { _genuine, _fake };
                // 标签机面板要显示这一行类别名（LabelerElement 直接索引这个字典，缺了会 KeyNotFound）
                if (!ItemFeatureList.CategoryNames.ContainsKey(Category))
                    ItemFeatureList.CategoryNames[Category] = "标签";
                Debug.Log(FigurineMod.LogTag + "手办标签类别已注册：" + Category + "（正品 / 赝品）");
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "注册手办标签类别失败：" + e.GetType().Name + " " + e.Message); }
        }

        // ───────────────────────── 特征读写 ─────────────────────────

        public static ItemFeature Find(GameItem item)
        {
            try
            {
                if (item == null || item.itemFeatures == null) return null;
                foreach (var f in item.itemFeatures)
                {
                    if (f == null) continue;
                    if (f.category == Category) return f;
                }
            }
            catch (Exception __ex) { Safe.Warn("FigurineLabelFeature.Find", __ex); }
            return null;
        }

        /// <summary>确保物品身上有这条标签特征（没有就按给定/现有状态建一条）。</summary>
        public static ItemFeature Ensure(GameItem item, bool? labelFake = null, bool? truthFake = null)
        {
            try
            {
                if (item == null) return null;
                Register();
                var f = Find(item);
                bool label = labelFake ?? LegacyLabelFake(item);
                bool truth = truthFake ?? FigurineAuth.TruthFake(item);

                if (f == null)
                {
                    f = new ItemFeature(ItemFeature.FeatureType.Normale);
                    f.identifier = FeatureId;
                    f.category = Category;
                    Set(f, "publicDisplay", label ? DisplayFake : DisplayGenuine);
                    Set(f, "actualDisplay", truth ? DisplayFake : DisplayGenuine);
                    Set(f, "valueModifier", 0);
                    Set(f, "initiallyShown", true);
                    f.isPublicHidden = false;
                    ApplyConditions(f, label, truth);
                    try { item.AddItemFeature(f); } catch (System.Exception __ex) { Safe.Warn("FigurineLabelFeature.Add", __ex); }
                    try { item.SyncModifiedState(); } catch (System.Exception __ex) { Safe.Warn("FigurineLabelFeature.Sync", __ex); }
                }
                else if (labelFake.HasValue || truthFake.HasValue)
                {
                    // 明确给了状态（新建物品 / 鉴定定案）→ 同步覆盖
                    ApplyConditions(f, label, truth);
                    Set(f, "publicDisplay", label ? DisplayFake : DisplayGenuine);
                    Set(f, "actualDisplay", truth ? DisplayFake : DisplayGenuine);
                }
                return f;
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "建手办标签特征失败：" + e.GetType().Name); return null; }
        }

        private static void ApplyConditions(ItemFeature f, bool labelFake, bool truthFake)
        {
            try
            {
                Register();
                var label = labelFake ? _fake : _genuine;
                var real = truthFake ? _fake : _genuine;
                f.SetConditions(label, real);
            }
            catch (System.Exception __ex) { Safe.Warn("FigurineLabelFeature.Conditions", __ex); }
        }

        /// <summary>改标签（只动 fakeCondition / 公开显示，真身 realCondition 不动）。</summary>
        public static void SetLabel(GameItem item, bool fake)
        {
            try
            {
                Register();
                var f = Ensure(item);
                if (f == null) return;
                f.SetConditions(fake ? _fake : _genuine, f.realCondition ?? (FigurineAuth.TruthFake(item) ? _fake : _genuine));
                Set(f, "publicDisplay", fake ? DisplayFake : DisplayGenuine);
                f.isPublicHidden = false;
                try { item.SyncModifiedState(); } catch (System.Exception __ex) { Safe.Warn("FigurineLabelFeature.SetLabelSync", __ex); }
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "改手办标签失败：" + e.GetType().Name); }
        }

        /// <summary>读标签：优先条件特征；旧存档没有特征时退回 tag。</summary>
        public static bool LabelFake(GameItem item)
        {
            try
            {
                var f = Find(item);
                if (f != null)
                {
                    var c = f.fakeCondition;
                    if (c != null && !string.IsNullOrEmpty(c.identifier)) return c.identifier == FakeId;
                }
            }
            catch (Exception __ex) { Safe.Warn("FigurineLabelFeature.LabelFake", __ex); }
            return LegacyLabelFake(item);
        }

        private static bool LegacyLabelFake(GameItem item)
        {
            try { return item != null && item.IsTag(FigurineAuth.TagLabelFake); } catch { return false; }
        }

        private static void Set(object obj, string field, object value)
        {
            try
            {
                if (obj == null) return;
                var fi = obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fi != null) fi.SetValue(obj, value);
            }
            catch (System.Exception __ex) { Safe.Warn("FigurineLabelFeature.Set", __ex); }
        }

        // ───────────────────────── 原版标签机接线 ─────────────────────────

        /// <summary>原版标签机开面板前，把手办缺的标签特征补上（否则列表里看不到）。</summary>
        [HarmonyPatch(typeof(LabelerUIManager), nameof(LabelerUIManager.OpenUI))]
        internal static class OpenPrefix
        {
            [HarmonyPrefix]
            private static void Prefix(GameItem __0)
            {
                try
                {
                    if (__0 == null) return;
                    if (!FigurineAuth.IsFigurine(__0)) return;
                    Ensure(__0);
                }
                catch (Exception __ex) { Safe.Warn("FigurineLabelFeature.OpenPrefix", __ex); }
            }
        }

        /// <summary>取标签机当前正在编辑的手办（curItem 是 private 字段）。</summary>
        internal static GameItem CurrentLabelerItem()
        {
            try
            {
                var mgr = LabelerUIManager.Instance;
                if (mgr == null) return null;
                var f = AccessTools.Field(typeof(LabelerUIManager), "curItem");
                return f != null ? f.GetValue(mgr) as GameItem : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// 标签机下拉框改了标签之后，立刻把物品名字刷成和标签一致。
        /// 原版只改 fakeCondition，不会调我们的 RefreshName → 以前会出现「标签正品、名字赝品」。
        /// </summary>
        [HarmonyPatch(typeof(LabelerElement), nameof(LabelerElement.OnDropdownValueChanged))]
        internal static class DropdownNamePatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                try
                {
                    var it = CurrentLabelerItem();
                    if (it != null && FigurineAuth.IsFigurine(it)) FigurineAuth.RefreshName(it);
                }
                catch (Exception __ex) { Safe.Warn("FigurineLabelFeature.DropdownName", __ex); }
            }
        }

        /// <summary>标签机「重复上一次标签」后同样要刷名字。</summary>
        [HarmonyPatch(typeof(LabelerUIManager), nameof(LabelerUIManager.Repeat))]
        internal static class RepeatNamePatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                try
                {
                    var it = CurrentLabelerItem();
                    if (it != null && FigurineAuth.IsFigurine(it)) FigurineAuth.RefreshName(it);
                }
                catch (Exception __ex) { Safe.Warn("FigurineLabelFeature.RepeatName", __ex); }
            }
        }
    }
}
