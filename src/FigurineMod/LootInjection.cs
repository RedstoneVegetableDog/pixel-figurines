using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 掉落注入 —— 按**正式版**的权重语义实现（0.8.0-beta 的 `WeightForShare` 思路）。
    ///
    /// 关键点：官方 `ModHelper.AddLootGroupEntry(modId, group, table, weight)` 里的 `weight`
    /// 是**分组内的相对权重**，不是百分比。要让它占 p%，必须反推：
    /// ```
    /// 现有总权重 T（不含自己） → weight = T × p / (1 − p)
    /// ```
    /// 以前直接塞 `p×100`（3% → 300），实际占比取决于分组里其它条目权重之和，保证不了 3%。
    ///
    /// 占比（beta 默认值）：
    /// ```
    /// 普通堆 / 新鲜堆 各 5 档：3.0% / 1.5% / 0.78% / 0.48% / 0.24%
    /// ```
    /// 表：按档位 5 张，每张放该档位**所有**手办（原神 + 星铁 + 绝区零）。
    ///
    /// ⚠️ 正式版 `LootRegistry.groups` 是 private static，旧实现用反射读；2026-11-10 改成
    ///    公开 API `ModHelper.GetLootGroupEntries(groupId)`（不触发 reflection-external 能力）。
    ///     首帧分组没加载时先挂临时权重，读档时 `RetryIfNeeded()` 重算。
    /// </summary>
    internal static class LootInjection
    {
        private const string ModId = "figurine.gs";

        // 1★~5★ 五档 + 第 6/7 项：整盒盲盒
        // 盲盒原为 5.0 / 2.0，2026-11-06 用户要求「垃圾场盲盒掉落概率消减 50%」→ 2.5 / 1.0
        private static readonly float[] CommonShare = { 3.0f, 1.5f, 0.78f, 0.48f, 0.24f, 2.5f, 1.0f };
        private static readonly float[] FreshShare = { 3.0f, 1.5f, 0.78f, 0.48f, 0.24f, 2.5f, 1.0f };

        private static readonly string[] TableIds =
        {
            "figurine_gs_white", "figurine_gs_blue", "figurine_gs_purple",
            "figurine_gs_red", "figurine_gs_gold", "figurine_gs_blindbox_table", "figurine_gs_blindbox_plus_table",
        };

        private const int TableCount = 7;

        private const string GroupCommon = "dumpingGroundTG";
        private const string GroupFresh = "dumpingGroundFreshTG";

        private static bool _applied;        // 权重已按真实分组算好
        private static bool _tablesBuilt;    // 表已建过（重试时不再重复建）
        private static bool _attachedOnce;   // 组里已有我们的条目（重试时改用 SetWeight）

        public static bool Applied { get { return _applied; } }

        public static void Install()
        {
            if (_applied) return;
            try
            {
                BuildTables();

                int attached = 0;
                bool fallback = false;
                // 五张表放进同一个分组，必须**联立**算：先扣掉我们全部 5 张后的原版总权重 T，
                // 再让五张合计占 S = Σp，单张权重 w_i = T × p_i / (1 − S)。
                // 一张张单独算（每张都拿“当前总量”当分母）会互相稀释，实测白卡会跑到 3.74%。
                for (int i = 0; i < TableCount; i++)
                {
                    if (TryAttachJoint(GroupCommon, i, CommonShare, ref fallback)) attached++;
                    if (TryAttachJoint(GroupFresh, i, FreshShare, ref fallback)) attached++;
                }
                _attachedOnce = attached > 0;

                _applied = _tablesBuilt && attached >= TableCount * 2 && !fallback;
                Debug.Log(FigurineMod.LogTag + $"掉落注入：建表 {(_tablesBuilt ? TableCount : 0)}/{TableCount}，挂组 {attached}/{TableCount * 2}"
                    + (fallback ? "（分组还没加载，先用临时权重，读档后重算）" : "（权重按实际占比计算）"));
            }
            catch (Exception e)
            {
                Debug.LogWarning(FigurineMod.LogTag + "掉落注入失败：" + e.GetType().Name + " | " + e.Message);
            }
        }

        private static void BuildTables()
        {
            if (_tablesBuilt) return;
            int built = 0;
            for (int star = 1; star <= 5; star++)
            {
                var entries = new List<(string, float)>();
                foreach (var d in Catalog.All) if (d.Star == star && !string.IsNullOrEmpty(d.Id)) entries.Add((d.Id, 1f));
                foreach (var d in StarRailData.ToCatalog()) if (d.Star == star && !string.IsNullOrEmpty(d.Id)) entries.Add((d.Id, 1f));
                foreach (var d in ZzzData.ToCatalog()) if (d.Star == star && !string.IsNullOrEmpty(d.Id)) entries.Add((d.Id, 1f));
                if (entries.Count == 0) continue;

                try { ModHelper.RegisterLootTable(ModId, TableIds[star - 1], entries.ToArray()); built++; }
                catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "掉落建表失败 " + TableIds[star - 1] + "：" + e.GetType().Name); }
            }
            // 第 6 张：整盒盲盒（四种各 1 权重）
            try
            {
                var boxes = new List<(string, float)>
                {
                    // 垃圾场只出「普通档」四种；高级档不进掉落表
                    (BlindBox.IdNormal, 1f), (BlindBox.IdGs, 1f),
                    (BlindBox.IdSr, 1f), (BlindBox.IdZzz, 1f),
                };
                ModHelper.RegisterLootTable(ModId, TableIds[5], boxes.ToArray());
                built++;
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "掉落建表失败 " + TableIds[5] + "：" + e.GetType().Name); }

            // 第 7 张：高级档盲盒（四个 plus）
            try
            {
                var plus = new List<(string, float)>
                {
                    (BlindBox.IdNormalPlus, 1f), (BlindBox.IdGsPlus, 1f),
                    (BlindBox.IdSrPlus, 1f), (BlindBox.IdZzzPlus, 1f),
                };
                ModHelper.RegisterLootTable(ModId, TableIds[6], plus.ToArray());
                built++;
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "掉落建表失败 " + TableIds[6] + "：" + e.GetType().Name); }
            _tablesBuilt = built >= TableCount;
        }

        /// <summary>五张表联立求权重（见 Install 里的注释）。</summary>
        private static bool TryAttachJoint(string groupId, int idx, float[] shares, ref bool fallback)
        {
            try
            {
                float totalVanilla = GroupTotalExceptAny(groupId, TableIds);
                float sumShare = 0f;
                for (int k = 0; k < shares.Length; k++) sumShare += Mathf.Clamp(shares[k], 0f, 90f) / 100f;
                if (sumShare >= 0.95f) sumShare = 0.95f;

                float share = Mathf.Clamp(shares[idx], 0f, 90f) / 100f;
                bool exact = totalVanilla > 0f && share > 0f && sumShare > 0f;

                float weight;
                if (exact) weight = totalVanilla * share / (1f - sumShare);
                else { weight = shares[idx] * 100f; fallback = true; }

                string tableId = TableIds[idx];
                if (_attachedOnce)
                {
                    try { ModHelper.SetLootGroupWeight(ModId, groupId, tableId, weight); }
                    catch { ModHelper.AddLootGroupEntry(ModId, groupId, tableId, weight); }
                }
                else
                {
                    ModHelper.AddLootGroupEntry(ModId, groupId, tableId, weight);
                }

                Debug.Log(FigurineMod.LogTag + $"掉落挂组 {groupId}/{tableId} → 权重 {weight:0.##}"
                    + (exact ? $"（目标 {shares[idx]:0.##}%，五张合计 {sumShare * 100f:0.##}%，原版总权重 {totalVanilla:0.##}）" : "（分组为空/不可读，临时权重）"));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning(FigurineMod.LogTag + "掉落挂组失败 " + groupId + "/" + TableIds[idx] + "：" + e.GetType().Name);
                return false;
            }
        }

        /// <summary>读回两个分组，核对每张表的实际占比是否等于目标（公开 API，无反射）。</summary>
        public static void Verify()
        {
            try
            {
                int ok = 0, bad = 0;
                for (int i = 0; i < TableCount; i++)
                {
                    if (VerifyOne(GroupCommon, TableIds[i], CommonShare[i])) ok++; else bad++;
                    if (VerifyOne(GroupFresh, TableIds[i], FreshShare[i])) ok++; else bad++;
                }
                Debug.Log(FigurineMod.LogTag + $"掉落自检完成：符合目标 {ok} 项，偏离 {bad} 项");
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "掉落自检异常：" + e.GetType().Name); }
        }

        private static bool VerifyOne(string groupId, string tableId, float targetPercent)
        {
            try
            {
                var list = ModHelper.GetLootGroupEntries(groupId);
                if (list == null) return false;

                float total = 0f, mine = 0f;
                foreach (var e in list)
                {
                    if (e == null) continue;
                    total += e.weight;
                    if (e.id == tableId) mine += e.weight;
                }
                if (total <= 0f || mine <= 0f) return false;

                float actual = mine / total * 100f;
                bool ok = Mathf.Abs(actual - targetPercent) <= Mathf.Max(0.1f, targetPercent * 0.15f);
                if (!ok)
                    Debug.LogWarning(FigurineMod.LogTag + $"掉落自检：{groupId}/{tableId} 目标 {targetPercent:0.##}%，实际 {actual:0.##}%");
                return ok;
            }
            catch { return false; }
        }

        /// <summary>读档时重试：分组此时一般已加载，可以算出真实权重。</summary>
        public static void RetryIfNeeded()
        {
            if (!_applied) Install();
            Verify();
        }

        /// <summary>
        /// Harmony 兜底：原版在 `LoadVanillaGroups` 里把分组灌进 `LootRegistry`，
        /// 之后才 `RebuildIfDirty()`。我们在这里后置跑一次，保证分组一定已经就绪。
        /// </summary>
        [HarmonyPatch(typeof(LootRegistry), "LoadVanillaGroups")]
        internal static class VanillaGroupsLoadedPatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                try
                {
                    Debug.Log(FigurineMod.LogTag + "LoadVanillaGroups 完成 → 重算掉落权重");
                    RetryIfNeeded();
                }
                catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "掉落重算失败：" + e.GetType().Name); }
            }
        }

        // ───────────────────────── 公开 API 读分组 ─────────────────────────
        private static float GroupTotalExceptAny(string groupId, string[] excludeIds)
        {
            try
            {
                var list = ModHelper.GetLootGroupEntries(groupId);
                if (list == null) return 0f;

                float total = 0f;
                foreach (var e in list)
                {
                    if (e == null) continue;
                    bool skip = false;
                    if (excludeIds != null) foreach (var x in excludeIds) if (x == e.id) { skip = true; break; }
                    if (skip) continue;
                    total += e.weight;
                }
                return total;
            }
            catch { return 0f; }
        }
    }
}
