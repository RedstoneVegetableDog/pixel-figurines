using System;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>运行时自检：目录数量、贴图数量、赝品/托盘图、补丁状态，一次性写日志。</summary>
    internal static class RuntimeSelfCheck
    {
        /// <summary>
        /// 补丁存活自检：枚举本程序集所有 HarmonyPatch，用反射拿到它声明的
        /// declaringType / methodName，再 AccessTools.Method 验证目标方法还在。
        /// 游戏更新后目标方法一改名，这里就会逐条打「补丁失效」，不用等玩家发现功能消失。
        /// </summary>
        public static void CheckPatches()
        {
            int ok = 0, dead = 0;
            try
            {
                var asm = typeof(RuntimeSelfCheck).Assembly;
                foreach (var type in asm.GetTypes())
                {
                    try
                    {
                        var attrs = type.GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), true);
                        if (attrs == null || attrs.Length == 0) continue;
                        foreach (HarmonyLib.HarmonyPatch attr in attrs)
                        {
                            var info = attr.info;
                            if (info == null || info.declaringType == null) continue;

                            System.Reflection.MethodInfo mi = null;
                            try
                            {
                                var argTypes = info.argumentTypes;
                                mi = argTypes != null && argTypes.Length > 0
                                    ? HarmonyLib.AccessTools.Method(info.declaringType, info.methodName, argTypes)
                                    : HarmonyLib.AccessTools.Method(info.declaringType, info.methodName);
                            }
                            catch (System.Exception __ex)
                            {
                                // AmbiguousMatchException：目标方法有重载，AccessTools 按名字找不到
                                // 这里退化为"按名字取第一个"，避免自检抛错（2026-11-05 玩家弹窗）
                                try
                                {
                                    foreach (var m in info.declaringType.GetMethods(
                                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                                        | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance))
                                    {
                                        if (m.Name == info.methodName) { mi = m; break; }
                                    }
                                }
                                catch { }
                                if (mi == null) Safe.Warn("SelfCheck.Patch", __ex);
                            }

                            if (mi == null)
                            {
                                dead++;
                                Debug.LogError(FigurineMod.LogTag + "补丁失效：" + type.Name
                                    + " → " + info.declaringType.Name + "." + info.methodName + "（目标方法不存在）");
                            }
                            else ok++;
                        }
                    }
                    catch (System.Exception __ex) { Safe.Warn("SelfCheck.PatchType", __ex); }
                }
            }
            catch (System.Exception __ex) { Safe.Warn("SelfCheck.Patches", __ex); }
            Debug.Log(FigurineMod.LogTag + "补丁自检：存活 " + ok + " 个，失效 " + dead + " 个");
        }

        /// <summary>一键自检：补丁 + 静默失败汇总 + 托盘配对。</summary>
        public static void Report()
        {
            try
            {
                CheckPatches();

                int tray = TrayArt.FileCount();
                Debug.Log(FigurineMod.LogTag + "自检报告：手办 " + Defs.All().Count
                    + " 件 / 托盘图(按需) " + tray + " 张 / " + Safe.Summary());
            }
            catch (System.Exception __ex) { Safe.Warn("SelfCheck.Report", __ex); }
        }
        /// <summary>
        /// 价格↔档位一致性自检：把某个系列按价格从高到低排，用金字塔比例反推“应该是几星”，
        /// 和 Def.Star 比对；不一致就点名（最多打 10 条）。用来抓“某张卡价格跑到别的档位”。
        /// </summary>
        private static void CheckPriceTiers(string series, System.Collections.Generic.List<Catalog.Def> defs)
        {
            try
            {
                if (defs == null || defs.Count == 0) return;
                var list = new System.Collections.Generic.List<Catalog.Def>(defs);
                list.Sort((a, b) =>
                {
                    int c = b.Value.CompareTo(a.Value);
                    if (c != 0) return c;
                    return string.CompareOrdinal(a.Id, b.Id);
                });

                int n = list.Count;
                double[] ratio = { 0.30, 0.25, 0.20, 0.15, 0.10 };
                var counts = new int[5];
                int sum = 0;
                for (int i = 0; i < 5; i++) { counts[i] = Math.Max(1, (int)Math.Round(n * ratio[i])); sum += counts[i]; }
                while (sum > n) { int idx = 4; for (int i = 0; i < 5; i++) if (counts[i] > counts[idx] && counts[i] > 1) idx = i; counts[idx]--; sum--; }
                while (sum < n) { int idx = 0; for (int i = 1; i < 5; i++) if (counts[i] < counts[idx]) idx = i; counts[idx]++; sum++; }

                int bad = 0, pos = 0;
                var sb = new System.Text.StringBuilder();
                for (int star = 5; star >= 1; star--)
                {
                    int cnt = counts[star - 1];
                    for (int k = 0; k < cnt && pos < n; k++, pos++)
                    {
                        var d = list[pos];
                        if (d.Star == star) continue;
                        bad++;
                        if (bad <= 10) sb.Append($"{d.Zh}({d.Value}₡ 档位{d.Star}★ 应{star}★) ");
                    }
                }
                if (bad > 0) UnityEngine.Debug.LogWarning(FigurineMod.LogTag + $"价格档位不一致 {series}：{bad} 件 → " + sb.ToString());
                else UnityEngine.Debug.Log(FigurineMod.LogTag + $"价格档位一致 {series}：{n} 件全部对得上");
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning(FigurineMod.LogTag + "价格档位自检异常：" + e.GetType().Name); }
        }

        public static void Run()
        {
            try
            {
                int catalog = Catalog.All.Length;
                int sr = StarRailData.ToCatalog().Length;
                int textures = 0, sprites = 0;
                try
                {
                    var mods = ModLoader.Mods;
                    if (mods != null)
                        foreach (var m in mods)
                        {
                            if (m == null || m.Manifest == null || m.Manifest.ID != "figurine.gs") continue;
                            if (m.Textures != null) textures = m.Textures.Count;
                            if (m.Sprites != null) sprites = m.Sprites.Count;
                        }
                }
                catch (System.Exception __ex) { Safe.Warn("RuntimeSelfCheck.cs:71", __ex); }

                int fakes = 0, trays = 0, fakeTrays = 0;
                try
                {
                    var mods = ModLoader.Mods;
                    if (mods != null)
                        foreach (var m in mods)
                        {
                            if (m == null || m.Manifest == null || m.Manifest.ID != "figurine.gs") continue;
                            if (m.Sprites == null) continue;
                            foreach (var k in m.Sprites.Keys)
                            {
                                if (k.IndexOf("_fake", StringComparison.Ordinal) >= 0) fakes++;
                            }
                        }
                }
                catch (System.Exception __ex) { Safe.Warn("RuntimeSelfCheck.cs:88", __ex); }
                // 托盘图不再走 ModLoader（按需从 Tray\ 加载），单独数文件
                try { trays = TrayArt.FileCount(); } catch (System.Exception __ex) { Safe.Warn("RuntimeSelfCheck.cs:90", __ex); }
                try { fakeTrays = TrayArt.FakeFileCount(); } catch (System.Exception __ex) { Safe.Warn("RuntimeSelfCheck.FakeTray", __ex); }

                Debug.Log(FigurineMod.LogTag + "自检：原神手办=" + catalog + " 星铁=" + sr
                    + " | Textures=" + textures + " Sprites=" + sprites
                    + " | 赝品物品图=" + fakes + "（设计上不做，赝品看托盘立绘） 托盘图=" + trays + "（其中赝品 " + fakeTrays + "）");

                // 手办鉴定：等级 / 进度 / 是否入会 / 证书在不在
                try
                {
                    int rank = FigurineCert.Rank;
                    Debug.Log(FigurineMod.LogTag + "自检·鉴定：已入会=" + FigurineCert.Introduced
                        + " 等级=" + rank + " 进度=" + FigurineCert.Progress + "/" + FigurineCert.ThresholdOf(rank)
                        + " 证书=" + FigurineCert.HasCert(rank)
                        + " 鉴定费=" + FigurineCert.BaseFeeOf(rank)
                        + " 可鉴星级=1~" + FigurineCert.MaxStarOf(rank)
                        + " 到店率=" + FigurineCert.SpawnChanceOf(rank) + "%"
                        + " 人数=" + FigurineCert.CountMinOf(rank) + "~" + FigurineCert.CountMaxOf(rank));
                }
                catch (System.Exception __ex) { Safe.Warn("RuntimeSelfCheck.Cert", __ex); }

                // 价格 ↔ 档位一致性（抓“某张卡价格跑到别的档位”）
                try { CheckPriceTiers("原神", new System.Collections.Generic.List<Catalog.Def>(Catalog.All)); } catch (System.Exception __ex) { Safe.Warn("RuntimeSelfCheck.cs:97", __ex); }
                try { CheckPriceTiers("星铁", new System.Collections.Generic.List<Catalog.Def>(StarRailData.ToCatalog())); } catch (System.Exception __ex) { Safe.Warn("RuntimeSelfCheck.cs:98", __ex); }
                try { CheckPriceTiers("绝区零", new System.Collections.Generic.List<Catalog.Def>(ZzzData.ToCatalog())); } catch (System.Exception __ex) { Safe.Warn("RuntimeSelfCheck.cs:99", __ex); }
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "自检失败：" + e.GetType().Name); }
        }
    }
}
