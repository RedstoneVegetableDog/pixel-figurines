using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace FigurinePort
{
    /// <summary>
    /// 鉴定面板自测（调试用）：在 `Mods\FigurinePort\selftest.txt` 存在时，
    /// 读档后自动跑一遍「造赝品 → 开原生面板 → 勾对 / 勾错」，把每一步写日志。
    /// 不需要按键、不需要抢焦点 —— 测完把标记文件删掉即可。
    /// </summary>
    internal static class FigurineSelfTest
    {
        /// <summary>找 selftest.txt：按模组 manifest 的 ID 定位自己的文件夹（文件夹可以随便改名），
        /// 找不到再扫 Mods 下任意子目录。</summary>
        private static string FindMarker()
        {
            try
            {
                var mods = ModLoader.Mods;
                if (mods != null)
                    foreach (var m in mods)
                    {
                        if (m == null || m.Manifest == null) continue;
                        if (m.Manifest.ID != "figurine.gs") continue;
                        if (string.IsNullOrEmpty(m.FolderPath)) continue;
                        return Path.Combine(m.FolderPath, "selftest.txt");
                    }
            }
            catch (System.Exception __ex) { Safe.Warn("SelfTest.FindMarker", __ex); }
            try
            {
                var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mods");
                if (System.IO.Directory.Exists(dir))
                    foreach (var d in System.IO.Directory.GetDirectories(dir))
                    {
                        var p = Path.Combine(d, "selftest.txt");
                        if (File.Exists(p)) return p;
                    }
            }
            catch (System.Exception __ex) { Safe.Warn("SelfTest.FindMarker.Scan", __ex); }
            return null;
        }

        public static void RunIfRequested()
        {
            try
            {
                string path = FindMarker();
                if (path == null || !File.Exists(path)) return;
                Debug.Log(FigurineMod.LogTag + "鉴定自测：检测到 " + path);
                Run();
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "鉴定自测入口失败：" + e.GetType().Name); }
        }

        private static void Run()
        {
            var ui = InspectionUIManager.Instance;
            if (ui == null) { Debug.LogWarning(FigurineMod.LogTag + "鉴定自测：InspectionUIManager 为空"); return; }

            // ── 用例 1：真身赝品（已拥有）→ 勾真实缺件 = 勾对 ──
            var a = Spawn(true, true);
            if (a == null) return;
            ui.OnInspectItem(a);
            DumpPanel(ui, "用例1 开面板");
            PickLabel(ui, "反色");
            ui.OnConfront();
            Debug.Log(FigurineMod.LogTag + "鉴定自测 用例1（赝品·勾缺件）→ 已鉴定=" + FigurineAuth.IsChecked(a)
                + " 价格=" + FigurineAuth.PricePercent(a) + "%（期望 60%）面板还开着=" + (ui.confrontPanel != null && ui.confrontPanel.activeSelf));

            // ── 用例 2：真身赝品（未拥有）→ 勾「正品」= 勾错 → 锁死 ──
            var b = Spawn(true, true, false);
            if (b == null) return;
            ui.OnInspectItem(b);
            PickLabel(ui, "正品");
            ui.OnConfront();
            Debug.Log(FigurineMod.LogTag + "鉴定自测 用例2（赝品·勾正品）→ 锁死=" + FigurineAuth.IsLocked(b)
                + "（期望 True）已鉴定=" + FigurineAuth.IsChecked(b) + "（期望 False）");
            try { ui.CloseUI(); } catch (System.Exception __ex) { Safe.Warn("SelfTest.CloseUI", __ex); }

            // ── 用例 3：真身正品 → 勾「正品」= 勾对，确认正品 100% ──
            var c = Spawn(false, false, true);
            if (c == null) return;
            ui.OnInspectItem(c);
            DumpPanel(ui, "用例3 开面板（正品）");
            PickLabel(ui, "正品");
            ui.OnConfront();
            Debug.Log(FigurineMod.LogTag + "鉴定自测 用例3（正品·勾正品）→ 已鉴定=" + FigurineAuth.IsChecked(c)
                + " 价格=" + FigurineAuth.PricePercent(c) + "%（期望 100%）");

            // ── 用例 4：已鉴定的再照 → 不该弹 ──
            ui.OnInspectItem(c);
            bool shown = ui.confrontPanel != null && ui.confrontPanel.activeSelf;
            Debug.Log(FigurineMod.LogTag + "鉴定自测 用例4（已鉴定再照）→ 面板弹出=" + shown + "（期望 False）");
            try { ui.CloseUI(); } catch (System.Exception __ex) { Safe.Warn("SelfTest.CloseUI", __ex); }

            TestAppraisal(ui);
            TestLabelFeature();

            Debug.Log(FigurineMod.LogTag + "鉴定自测：结束");
        }

        /// <summary>标签迁移自测：标签存在条件特征里，改标签不动真身。</summary>
        private static void TestLabelFeature()
        {
            try
            {
                var it = Spawn(true, true);        // 真身赝品 + 标签赝品
                if (it == null) return;
                var f = FigurineLabelFeature.Find(it);
                Debug.Log(FigurineMod.LogTag + "鉴定自测 用例8（标签特征）→ 存在=" + (f != null)
                    + " fake=" + (f != null && f.fakeCondition != null ? f.fakeCondition.identifier : "?")
                    + " real=" + (f != null && f.realCondition != null ? f.realCondition.identifier : "?")
                    + " 标签=" + FigurineAuth.LabelFake(it) + " 真身=" + FigurineAuth.TruthFake(it)
                    + " 价格=" + FigurineAuth.PricePercent(it) + "%（期望 60）");

                FigurineAuth.SetLabelFake(it, false);   // 只改标签 → 正品
                var f2 = FigurineLabelFeature.Find(it);
                Debug.Log(FigurineMod.LogTag + "鉴定自测 用例9（只改标签）→ 标签=" + FigurineAuth.LabelFake(it)
                    + "（期望 False）真身=" + FigurineAuth.TruthFake(it) + "（期望 True）"
                    + " fake=" + (f2 != null && f2.fakeCondition != null ? f2.fakeCondition.identifier : "?")
                    + " real=" + (f2 != null && f2.realCondition != null ? f2.realCondition.identifier : "?")
                    + " 价格=" + FigurineAuth.PricePercent(it) + "%（期望 100）");
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "标签自测异常：" + e.GetType().Name); }
        }

        /// <summary>估价玩法自测：未鉴定件 → 注册表 → 完美估价 → 基础费 + 结果票。</summary>
        private static void TestAppraisal(InspectionUIManager ui)
        {
            try
            {
                FigurineAppraisal.Register();
                var def = FigurineAppraisal.PickDef();
                var item = FigurineAppraisal.BuildUnappraised(def);
                if (item == null) { Debug.LogWarning(FigurineMod.LogTag + "鉴定自测 用例5：造未鉴定件失败"); return; }

                int correct = FigurineAppraisal.GetCorrectValue(item);
                bool found = false;
                try { found = AppraisalItemRegistry.Find(item) != null; } catch (System.Exception __ex) { Safe.Warn("SelfTest.Registry", __ex); }
                Debug.Log(FigurineMod.LogTag + "鉴定自测 用例5（未鉴定件）→ " + item.identifier
                    + " 未鉴定=" + FigurineAuth.IsUnappraised(item)
                    + " 价格=" + FigurineAuth.PricePercent(item) + "%（期望 80）"
                    + " 真值=" + correct + " 注册表找到=" + found
                    + " 已打APPRAISAL_ITEM=" + (item.IsTag("APPRAISAL_ITEM")));

                // 未鉴定件不应该弹缺损面板
                ui.OnInspectItem(item);
                Debug.Log(FigurineMod.LogTag + "鉴定自测 用例6（未鉴定件照放大镜）→ 缺损面板弹出="
                    + (ui.confrontPanel != null && ui.confrontPanel.activeSelf) + "（期望 False）");
                try { ui.CloseUI(); } catch (System.Exception __ex) { Safe.Warn("SelfTest.CloseUI", __ex); }

                // 完美估价 → 走我们的 GiveFee
                long before = PlayerStore.Instance != null ? PlayerStore.Instance.playerCash : 0;
                try { FigurineAppraisal.Current = item; } catch (System.Exception __ex) { Safe.Warn("SelfTest.Cur", __ex); }
                AppraisalServiceHelper.OnEstimate(item, correct);
                long after = PlayerStore.Instance != null ? PlayerStore.Instance.playerCash : 0;
                Debug.Log(FigurineMod.LogTag + "鉴定自测 用例7（完美估价 " + correct + "）→ 现金 +" + (after - before)
                    + "（期望基础费）物品已销毁=" + (item == null));
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "鉴定自测 估价异常：" + e.GetType().Name + " " + e.Message); }
        }

        /// <summary>把 InspectionUIManager 下所有 Button 的路径/文字/持久监听打出来，用来定位「确认」按钮。</summary>
        private static void DumpButtons(InspectionUIManager ui)
        {
            try
            {
                var bs = ui.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                Debug.Log(FigurineMod.LogTag + "鉴定自测 按钮清单：共 " + bs.Length + " 个");
                foreach (var b in bs)
                {
                    try
                    {
                        string path = b.name;
                        var t = b.transform.parent;
                        for (int k = 0; k < 4 && t != null; k++) { path = t.name + "/" + path; t = t.parent; }
                        string txt = "-";
                        var tmp = b.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                        if (tmp != null) txt = tmp.text;
                        var sb = new System.Text.StringBuilder();
                        int n = b.onClick.GetPersistentEventCount();
                        for (int i = 0; i < n; i++)
                            sb.Append(b.onClick.GetPersistentTarget(i)?.GetType().Name).Append('.').Append(b.onClick.GetPersistentMethodName(i)).Append(';');
                        Debug.Log(FigurineMod.LogTag + "鉴定自测 按钮：" + path + " 文字='" + txt + "' 激活=" + b.gameObject.activeInHierarchy
                            + " 持久=" + n + " [" + sb + "]");
                    }
                    catch (System.Exception __ex) { Safe.Warn("SelfTest.DumpButtons.item", __ex); }
                }
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "鉴定自测 DumpButtons 异常：" + e.GetType().Name); }
        }

        private static GameItem Spawn(bool truthFake, bool labelFake, bool own = true)
        {
            try
            {
                var item = FigurineMod.BuildFigurine(Catalog.All[0]);
                if (item == null) { Debug.LogWarning(FigurineMod.LogTag + "鉴定自测：建物失败"); return null; }
                FigurineAuth.Apply(item, new FigurineAuth.Roll { TruthFake = truthFake, LabelFake = labelFake, Variant = 1 });
                if (!own)
                {
                    try { item.DisableTag("IS_OWNED_TAG", true); } catch (System.Exception __ex) { Safe.Warn("SelfTest.Unown", __ex); }
                    Debug.Log(FigurineMod.LogTag + "鉴定自测：造未拥有件 " + item.identifier);
                    return item;
                }
                var inv = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.invElement : null;
                bool ok = FigurineMod.TryPlace(inv, item);
                Debug.Log(FigurineMod.LogTag + "鉴定自测：造 " + item.identifier + " 真身=" + (truthFake ? "赝品" : "正品")
                    + " 标签=" + (labelFake ? "赝品" : "正品") + " 进背包=" + ok);
                return item;
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "鉴定自测建物异常：" + e.GetType().Name); return null; }
        }

        private static void DumpPanel(InspectionUIManager ui, string tag)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                Toggle[] ts = { ui.toggle1, ui.toggle2, ui.toggle3, ui.toggle4, ui.toggle5 };
                foreach (var t in ts)
                {
                    if (t == null) { sb.Append("[空] "); continue; }
                    string s = "-";
                    try
                    {
                        var tr = t.transform;
                        for (int i = 0; i < tr.childCount; i++)
                        {
                            var tmp = tr.GetChild(i).GetComponent<TMPro.TextMeshProUGUI>();
                            if (tmp != null) { s = tmp.text; break; }
                        }
                    }
                    catch (System.Exception __ex) { Safe.Warn("SelfTest.DumpPanel", __ex); }
                    sb.Append(t.gameObject.activeSelf ? s : ("(" + s + ")"));
                    sb.Append(" | ");
                }
                Debug.Log(FigurineMod.LogTag + "鉴定自测 " + tag + "：面板开="
                    + (ui.confrontPanel != null && ui.confrontPanel.activeSelf)
                    + " 标题=" + (ui.confrontTitleTMP != null ? ui.confrontTitleTMP.text : "?")
                    + " 候选=" + sb);
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "鉴定自测 DumpPanel 异常：" + e.GetType().Name); }
        }

        private static void PickLabel(InspectionUIManager ui, string label)
        {
            try
            {
                Toggle[] ts = { ui.toggle1, ui.toggle2, ui.toggle3, ui.toggle4, ui.toggle5 };
                foreach (var t in ts)
                {
                    if (t == null) continue;
                    string s = null;
                    var tr = t.transform;
                    for (int i = 0; i < tr.childCount; i++)
                    {
                        var tmp = tr.GetChild(i).GetComponent<TMPro.TextMeshProUGUI>();
                        if (tmp != null) { s = tmp.text; break; }
                    }
                    if (s == label) { t.isOn = true; Debug.Log(FigurineMod.LogTag + "鉴定自测：勾选「" + label + "」"); return; }
                }
                Debug.LogWarning(FigurineMod.LogTag + "鉴定自测：候选里没有「" + label + "」，改勾第一个");
                foreach (var t in ts) { if (t != null && t.gameObject.activeSelf) { t.isOn = true; return; } }
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "鉴定自测 Pick 异常：" + e.GetType().Name); }
        }
    }
}
