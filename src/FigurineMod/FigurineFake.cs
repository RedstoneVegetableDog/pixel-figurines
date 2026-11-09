using System;
using System.Collections.Generic;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 赝品地基：缺陷数据（FakeData）、赝品 tag、价值折扣；有假立绘时才换图。
    /// 缺陷数据来自旧 catalog.json（40/40 都有）；目前物品级假立绘素材不全，缺图时只打 tag/降值。
    /// </summary>
    internal static class FigurineFake
    {
        private const string TagFake = "IS_FAKE";
        private const string TagExposed = "FAKE_EXPOSED";
        private const string TagVariantPrefix = "FAKE_VARIANT_";

        public static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            const string prefix = "figurine_gs_";
            return id.StartsWith(prefix, StringComparison.Ordinal) ? id.Substring(prefix.Length) : id;
        }

        /// <summary>
        /// 托盘图用的短 id：原神 `klee`、星铁 `sr_kafka`、绝区零 `zzz_anby_demara`
        /// —— 和 `Tray\<IP>\figurine_tray_*.png` 的命名一致。
        /// </summary>
        public static string TrayShortId(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            const string gs = "figurine_gs_", sr = "figurine_sr_", zzz = "figurine_zzz_";
            if (id.StartsWith(gs, StringComparison.Ordinal)) return id.Substring(gs.Length);
            if (id.StartsWith(sr, StringComparison.Ordinal)) return "sr_" + id.Substring(sr.Length);
            if (id.StartsWith(zzz, StringComparison.Ordinal)) return "zzz_" + id.Substring(zzz.Length);
            return id;
        }

        public static bool HasFakes(string id)
        {
            try { return FakeData.Defects.ContainsKey(ShortId(id)); } catch { return false; }
        }

        public static int VariantCount(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id)) return 0;   // 自检/无 id 时不要走字典（null key 会抛）
                if (FakeData.Defects.TryGetValue(ShortId(id), out var v) && v != null) return v.Length;
            }
            catch (System.Exception __ex) { Safe.Warn("FigurineFake.cs:49", __ex); }
            return 0;
        }

        /// <summary>
        /// 随机挑一个变体（1-based）：数量看 FakeData —— 原神 3（反色 + 两张赝品立绘），
        /// 星铁/绝区零 1（只有反色）。传 null / 查不到也回 1。
        /// </summary>
        public static int PickVariant(string id)
        {
            try
            {
                int count = Math.Max(1, VariantCount(id));
                return count <= 1 ? 1 : UnityEngine.Random.Range(1, count + 1);
            }
            catch { return 1; }
        }

        public static string[] Defects(string id, int variant)
        {
            try
            {
                if (string.IsNullOrEmpty(id)) return null;
                if (FakeData.Defects.TryGetValue(ShortId(id), out var v) && v != null && variant >= 0 && variant < v.Length)
                    return v[variant];
            }
            catch (System.Exception __ex) { Safe.Warn("FigurineFake.cs:60", __ex); }
            return null;
        }

        /// <summary>把物品变成赝品：打 tag、降值；若该 id 有 _fakeN 贴图则换图。</summary>
        public static bool MakeFake(GameItem item, string id, int variant)
        {
            try
            {
                // 发布包屏蔽赝品玩法（见 FeatureFlags）
                if (!FeatureFlags.Fakes) return false;
                if (item == null) return false;
                int count = Math.Max(1, VariantCount(id));
                if (variant < 0) variant = UnityEngine.Random.Range(0, count);
                variant %= count;

                // 三态：真身 + 标签都改成赝品。
                // 用户 2026-11-04 定：名字不再加「（赝品）」后缀，真伪只写在工具提示里。
                FigurineAuth.Apply(item, new FigurineAuth.Roll
                {
                    TruthFake = true,
                    LabelFake = true,
                    Variant = variant + 1,
                });
                try { FigurineFeature.AttachFake(item); } catch (System.Exception __ex) { Safe.Warn("FigurineFake.cs:92", __ex); }
                return true;
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "赝品处理失败：" + e.GetType().Name); return false; }
        }

        public static bool IsFake(GameItem item, out int variant)
        {
            variant = -1;
            try
            {
                // 发布包屏蔽赝品玩法：一律视为正品（工具提示/估价/放大镜都走正品分支）
                if (!FeatureFlags.Fakes) return false;
                if (item == null || !item.IsTag(TagFake)) return false;
                for (int i = 1; i <= 8; i++)
                    if (item.IsTag(TagVariantPrefix + i)) { variant = i - 1; break; }
                return true;
            }
            catch { return false; }
        }

        public static bool IsExposed(GameItem item)
        {
            try { return item != null && item.IsTag(TagExposed); } catch { return false; }
        }

        public static void MarkExposed(GameItem item)
        {
            try { item.EnableTag(TagExposed, true); item.SyncModifiedState(); } catch (System.Exception __ex) { Safe.Warn("FigurineFake.cs:120", __ex); }
            try { FigurineFeature.Expose(item); } catch (System.Exception __ex) { Safe.Warn("FigurineFake.cs:121", __ex); }
        }

        /// <summary>
        /// 托盘（放大镜检查）应显示的贴图键 —— **照 0.8.0 的 `TrayKeyFor`**：
        /// 正品 = `figurine_tray_&lt;短id&gt;`，赝品 = `figurine_tray_&lt;短id&gt;_fakeN`。
        /// 以前正品返回 null，导致回落到 48px 的物品贴图（放大镜里糊成一片）；
        /// 0.8.0-beta 用的是 1254×1254 原尺寸托盘图。
        /// </summary>
        public static string TrayKeyFor(GameItem item)
        {
            if (item == null) return null;
            string shortId = TrayShortId(item.identifier);
            if (string.IsNullOrEmpty(shortId)) return null;

            int v;
            if (!IsFake(item, out v)) return "figurine_tray_" + shortId;
            return "figurine_tray_" + shortId + "_fake" + (v + 1);
        }

        /// <summary>赝品的原始（真品）价值；取不到返回 -1。</summary>
        public static int OriginalValue(GameItem item)
        {
            try
            {
                var d = FindDef(item != null ? item.identifier : null);
                return d.Id != null ? d.Value : -1;
            }
            catch { return -1; }
        }

        /// <summary>还原为正品：名字/价值回定义值，清掉赝品与缺陷 tag。</summary>
        public static void RestoreGenuine(GameItem item)
        {
            try
            {
                if (item == null) return;
                var d = FindDef(item.identifier);
                for (int v = 1; v <= 8; v++) { try { item.DisableTag(TagVariantPrefix + v, true); } catch (System.Exception __ex) { Safe.Warn("FigurineFake.cs:159", __ex); } }
                try { item.DisableTag(TagFake, true); } catch (System.Exception __ex) { Safe.Warn("FigurineFake.cs:160", __ex); }
                try { item.DisableTag(TagExposed, true); } catch (System.Exception __ex) { Safe.Warn("FigurineFake.cs:161", __ex); }
                // 三态也一并清成「正品」
                try { FigurineAuth.Apply(item, new FigurineAuth.Roll()); } catch (System.Exception __ex) { Safe.Warn("FigurineFake.cs:162", __ex); }
                if (d.Id != null)
                {
                    item.name = d.Zh;
                    item.unitValue = d.Value;
                    item.unitBaseValue = d.Value;
                }
                try { item.SyncModifiedState(); } catch (System.Exception __ex) { Safe.Warn("FigurineFake.cs:168", __ex); }
                try { FigurineFeature.AttachGenuine(item); } catch (System.Exception __ex) { Safe.Warn("FigurineFake.cs:169", __ex); }
            }
            catch (System.Exception __ex) { Safe.Warn("FigurineFake.cs:171", __ex); }
        }

        private static Catalog.Def FindDef(string id)
        {
            { var dd = Defs.Find(id); if (!string.IsNullOrEmpty(dd.Id)) return dd; }
            return default;
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
            catch (System.Exception __ex) { Safe.Warn("FigurineFake.cs:192", __ex); }
            return null;
        }
    }
}
