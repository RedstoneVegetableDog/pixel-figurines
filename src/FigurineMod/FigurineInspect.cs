using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FigurinePort
{
    /// <summary>
    /// 手办「鉴定」——**接管游戏原生的缺损面板**（用户 2026-11-04 定）。
    ///
    /// 原生面板 = `InspectionUIManager.confrontPanel`（照香烟时右下角那 5 个勾选框 + 确认按钮）。
    /// 原版只有香烟 / 食品券 / 注射器会弹它；手办不是这三种，所以由我们：
    ///   1. `Open()`：照到手办时把面板 SetActive(true)，把 5 个标签换成手办的缺件候选；
    ///   2. Prefix 拦 `OnConfront`：物品是手办就自己判定，其它物品原样交回原版；
    ///   3. 原版每次开面板都会重写标签（`OpenCigaretteConfrontPanel` 等），所以我们的字不会残留。
    ///
    /// 判定（用户定）：
    ///   · 勾对（选中的集合 == 真身赝品的实际缺件）→ 打「已鉴定」永久锁，价格改按真身；
    ///   · 勾错 → 未拥有：打「勾错锁死」，不能再鉴；拥有：不锁，可重来；
    ///   · 已勾对过 → 面板不再弹。
    /// 5 个候选 = 正确答案 + 4 个干扰项（按物品 id 固定，重开不变）。
    /// </summary>
    internal static class FigurineInspect
    {
        private const string Title = "这件手办哪里不对？";

        /// <summary>正品那一项；勾它且真身是正品 = 勾对（确认正品）。</summary>
        private const string NoDefect = "正品";

        // ───────────────────────── 打开面板 ─────────────────────────

        public static void Open(InspectionUIManager ui, GameItem item)
        {
            try
            {
                if (ui == null || item == null) return;
                if (!FigurineAuth.IsFigurine(item)) return;

                bool owned = IsOwned(item);

                // 未鉴定件只走「估价」流程（鉴定客），不弹缺损面板——否则会泄露真身
                if (FigurineAuth.IsUnappraised(item)) { Hide(ui); return; }
                // 已勾对（永久锁）→ 不再弹
                if (FigurineAuth.IsChecked(item)) { Hide(ui); return; }
                // 勾错锁死：还在顾客手上 → 不弹；已经买下 → 解锁重鉴
                if (FigurineAuth.IsLocked(item))
                {
                    if (!owned) { Hide(ui); return; }
                    FigurineAuth.ClearLocked(item);
                }
                if (ui.confrontPanel == null) { Debug.LogWarning(FigurineMod.LogTag + "鉴定：confrontPanel 空，放弃接管"); return; }

                // 用户 2026-11-04：去掉干扰项 —— 只列这件手办真实的缺件。
                // 真身正品没有缺件 → 没有可勾的东西 → 不弹面板。
                var opts = Options(item);
                if (opts.Count == 0) { Hide(ui); return; }

                SetTitle(ui, Title);
                try { ui.ResetAllConfrontToggle(); } catch (System.Exception __ex) { Safe.Warn("FigurineInspect.ResetToggles", __ex); }

                Toggle[] toggles = { ui.toggle1, ui.toggle2, ui.toggle3, ui.toggle4, ui.toggle5 };
                for (int i = 0; i < toggles.Length; i++)
                {
                    var t = toggles[i];
                    if (t == null) continue;
                    bool on = i < opts.Count;
                    try { t.gameObject.SetActive(on); } catch (System.Exception __ex) { Safe.Warn("FigurineInspect.ToggleActive", __ex); }
                    if (!on) continue;
                    var tmp = ToggleLabel(t);
                    if (tmp != null)
                    {
                        try { tmp.text = opts[i]; } catch (System.Exception __ex) { Safe.Warn("FigurineInspect.ToggleText", __ex); }
                    }
                }
                ui.confrontPanel.SetActive(true);
                // 按钮文字保持原版（用户 2026-11-04：按钮不用改）
                Debug.Log(FigurineMod.LogTag + "鉴定面板（原生）：" + item.identifier
                    + " 候选=" + string.Join("/", opts.ToArray())
                    + " 已拥有=" + owned);
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "鉴定面板打开失败：" + e.GetType().Name + " " + e.Message); }
        }

        public static void Hide(InspectionUIManager ui)
        {
            try { if (ui != null && ui.confrontPanel != null) ui.confrontPanel.SetActive(false); }
            catch (System.Exception __ex) { Safe.Warn("FigurineInspect.Hide", __ex); }
        }

        // ───────────────────────── 判定 ─────────────────────────

        public static void Judge(InspectionUIManager ui, GameItem item)
        {
            try
            {
                if (ui == null || item == null) return;
                var picked = Picked(ui);
                if (picked.Count == 0) return;   // 一个都没勾：什么也不做

                bool owned = IsOwned(item);
                bool truthFake = FigurineAuth.TruthFake(item);
                // 正品也有一个可选项「没有缺损」——勾它就等于确认正品（勾对）
                var actual = new HashSet<string>(CorrectLabels(item));
                if (!truthFake) actual.Add(NoDefect);

                bool correct = actual.SetEquals(picked);
                FigurineConfrontLines.Kind kind = correct
                    ? FigurineConfrontLines.Kind.Correct
                    : (picked.Contains(NoDefect) ? FigurineConfrontLines.Kind.NotFake : FigurineConfrontLines.Kind.WrongPart);

                string part = string.Join("、", new List<string>(picked).ToArray());

                if (correct)
                {
                    // 勾对：鉴定成立 → 把**标签同步成真身**（这就是「揭晓」）并永久锁死。
                    // 之后价格跟标签走（用户 2026-11-04），玩家再贴标签就按新标签算。
                    try { FigurineAuth.SetLabelFake(item, truthFake); } catch (System.Exception __ex) { Safe.Warn("FigurineInspect.SyncLabel", __ex); }
                    FigurineAuth.MarkChecked(item);
                }
                else if (!owned)
                {
                    // 勾错且还在顾客手上：锁死，不能再鉴；买下后自动解锁可重鉴
                    FigurineAuth.MarkLocked(item);
                }

                Debug.Log(FigurineMod.LogTag + "鉴定判定：" + item.identifier + " → " + kind
                    + "（勾=" + part + " 真身=" + (truthFake ? "赝品" : "正品")
                    + " 价格=" + FigurineAuth.PricePercent(item) + "% 已拥有=" + owned + "）");


                try { ui.confrontPanel.SetActive(false); } catch (System.Exception __ex) { Safe.Warn("FigurineInspect.ClosePanel", __ex); }
                try { ui.CloseUI(); } catch (System.Exception __ex) { Safe.Warn("FigurineInspect.CloseUI", __ex); }
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "鉴定判定失败：" + e.GetType().Name + " " + e.Message); }
        }

        // ───────────────────────── 候选 / 读数 ─────────────────────────

        /// <summary>正确答案的标签集合：变体 1 = 反色；变体 2..N = FakeData 里该变体的部位名。</summary>
        public static List<string> CorrectLabels(GameItem item)
        {
            var list = new List<string>();
            try
            {
                if (!FigurineAuth.TruthFake(item)) return list;   // 正品没有缺件
                int idx = Math.Max(0, FigurineAuth.Variant(item) - 1);
                var defects = FigurineFake.Defects(item.identifier, idx)
                              ?? FigurineFake.Defects(item.identifier, 0);
                if (defects != null && defects.Length > 0) list.AddRange(defects);
                else list.Add("反色");
            }
            catch (System.Exception __ex) { Safe.Warn("FigurineInspect.CorrectLabels", __ex); }
            return list;
        }

        /// <summary>
        /// 勾选项 = 这件手办**真实**的缺件 + 一个「没有缺损」（用户 2026-11-04）。
        /// 不掺别的角色的部件当干扰项；正品只有「没有缺损」一项，勾它即确认正品。
        /// </summary>
        public static List<string> Options(GameItem item)
        {
            var res = new List<string>();
            try
            {
                foreach (var c in CorrectLabels(item)) res.Add(c);
                if (!res.Contains(NoDefect)) res.Add(NoDefect);
            }
            catch (System.Exception __ex) { Safe.Warn("FigurineInspect.Options", __ex); }
            return res;
        }

        private static HashSet<string> Picked(InspectionUIManager ui)
        {
            var set = new HashSet<string>();
            try
            {
                Toggle[] toggles = { ui.toggle1, ui.toggle2, ui.toggle3, ui.toggle4, ui.toggle5 };
                foreach (var t in toggles)
                {
                    if (t == null || !t.isOn) continue;
                    var tmp = ToggleLabel(t);
                    string s = tmp != null ? tmp.text : null;
                    if (!string.IsNullOrEmpty(s)) set.Add(s);
                }
            }
            catch (System.Exception __ex) { Safe.Warn("FigurineInspect.Picked", __ex); }
            return set;
        }

        private static TextMeshProUGUI ToggleLabel(Toggle t)
        {
            try
            {
                if (t == null) return null;
                var tr = t.transform;
                for (int i = 0; i < tr.childCount; i++)
                {
                    var c = tr.GetChild(i);
                    if (c == null) continue;
                    var tmp = c.GetComponent<TextMeshProUGUI>();
                    if (tmp != null) return tmp;
                }
            }
            catch (System.Exception __ex) { Safe.Warn("FigurineInspect.ToggleLabel", __ex); }
            return null;
        }

        private static void SetTitle(InspectionUIManager ui, string text)
        {
            try
            {
                if (ui == null) return;
                if (ui.confrontTitleTMP != null) ui.confrontTitleTMP.text = text;
            }
            catch (System.Exception __ex) { Safe.Warn("FigurineInspect.SetTitle", __ex); }
        }

        private static bool IsOwned(GameItem item)
        {
            try { return item != null && GeneralHelper.IsItemOwned(item); } catch { return false; }
        }
    }

    /// <summary>拦原生「确认」按钮：手办走我们判定，其它物品原样交回原版。</summary>
    [HarmonyPatch(typeof(InspectionUIManager), "OnConfront")]
    internal static class FigurineConfrontPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(InspectionUIManager __instance)
        {
            try
            {
                var item = __instance != null ? __instance.currentInspectingItem : null;
                if (item == null || !FigurineAuth.IsFigurine(item)) return true;   // 原版物品：不动
                FigurineInspect.Judge(__instance, item);
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning(FigurineMod.LogTag + "鉴定补丁异常：" + e.GetType().Name + " " + e.Message);
                return true;
            }
        }
    }
}
