using System;
using System.Collections.Generic;
using HarmonyLib;

namespace FigurinePort
{
    /// <summary>
    /// 议价反应台词（2026-11-10 去反射重写）
    ///
    /// 旧实现用反射替换 `BargainUIManager` 的 12 个私有静态台词池；新版本会拒绝 reflection-external，
    /// 所以改成 Harmony 补丁：`AppendClientLine(Dialogue, List&lt;string&gt;)` 是原版议价台词的唯一出口，
    /// 我们在前缀里识别「传进来的是哪个默认池」，把它换成当前顾客的 12 档专属台词。
    ///
    /// 默认池的识别不依赖私有字段：原版 `InitializeDialogueLines()` 是用公开的
    /// `LocHelper.GetLocalizedDialogue(key)` 拼出来的，我们按同样的 key 重建一份，按内容匹配分类。
    /// </summary>
    internal static class BargainLines
    {
        private const int CategoryCount = 12;

        /// <summary>和 CustomerLines.Bargain 的 12 档顺序一一对应。</summary>
        private static readonly string[][] DefaultKeys =
        {
            new[] { "dialog_bargain_accept_grateful_1", "dialog_bargain_accept_grateful_2", "dialog_bargain_accept_grateful_3" },
            new[] { "dialog_bargain_accept_very_grateful_1", "dialog_bargain_accept_very_grateful_2", "dialog_bargain_accept_very_grateful_3" },
            new[] { "dialog_bargain_accept_reluctant_1", "dialog_bargain_accept_reluctant_2", "dialog_bargain_accept_reluctant_3" },
            new[] { "dialog_bargain_accept_very_reluctant_1", "dialog_bargain_accept_very_reluctant_2", "dialog_bargain_accept_very_reluctant_3" },
            new[] { "dialog_bargain_reject_1", "dialog_bargain_reject_2", "dialog_bargain_reject_3" },
            new[] { "dialog_bargain_hard_reject_1", "dialog_bargain_hard_reject_2", "dialog_bargain_hard_reject_3" },
            new[] { "dialog_bargain_leave_1", "dialog_bargain_leave_2", "dialog_bargain_leave_3" },
            new[] { "dialog_bargain_budget_increase_1", "dialog_bargain_budget_increase_2", "dialog_bargain_budget_increase_3" },
            new[] { "dialog_bargain_budget_decrease_1", "dialog_bargain_budget_decrease_2", "dialog_bargain_budget_decrease_3" },
            new[] { "dialog_bargain_talk_bad_1", "dialog_bargain_talk_bad_2", "dialog_bargain_talk_bad_3" },
            new[] { "dialog_bargain_innocent_blackmail_1", "dialog_bargain_innocent_blackmail_2", "dialog_bargain_innocent_blackmail_3" },
            new[] { "dialog_bargain_false_threaten_1", "dialog_bargain_false_threaten_2", "dialog_bargain_false_threaten_3" },
        };

        private static Dictionary<string, int> _defaultMap;
        private static string[][] _custom;
        private static string _appliedId;

        /// <summary>顾客出现/进入议价前调用：记下这位顾客的 12 档台词。</summary>
        public static void ApplyFor(string identifier)
        {
            try
            {
                _appliedId = identifier;
                var pool = CustomerLines.For(identifier);
                _custom = pool != null ? pool.Bargain : null;
                int n = _custom != null ? _custom.Length : 0;
                UnityEngine.Debug.Log(FigurineMod.LogTag + "议价台词：" + identifier + " 已挂 " + n + " 档");
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning(FigurineMod.LogTag + "议价台词挂载失败：" + e.GetType().Name); }
        }

        /// <summary>兼容旧调用点：现在不改原版池，无需还原。</summary>
        public static void RestoreDefaults()
        {
            _appliedId = null;
            _custom = null;
        }

        private static void ReplaceLines(ref List<string> lines)
        {
            try
            {
                if (lines == null || _custom == null) return;
                EnsureDefaults();
                if (_defaultMap == null) return;

                string sig = Signature(lines);
                int cat;
                if (!_defaultMap.TryGetValue(sig, out cat)) return;
                if (cat < 0 || cat >= _custom.Length) return;
                var src = _custom[cat];
                if (src == null || src.Length == 0) return;

                lines = new List<string>(src);
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning(FigurineMod.LogTag + "议价台词替换失败：" + e.GetType().Name); }
        }

        private static string Signature(List<string> lines)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) sb.Append('\u0001');
                sb.Append(lines[i]);
            }
            return sb.ToString();
        }

        private static void EnsureDefaults()
        {
            if (_defaultMap != null) return;
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < DefaultKeys.Length; i++)
            {
                var probe = new List<string>();
                for (int k = 0; k < DefaultKeys[i].Length; k++)
                    probe.Add(LocHelper.GetLocalizedDialogue(DefaultKeys[i][k]));
                var sig = Signature(probe);
                if (!map.ContainsKey(sig)) map[sig] = i;
            }
            _defaultMap = map;
        }

        /// <summary>原版议价台词唯一出口：前缀按默认池识别分类，换成本顾客的专属台词。</summary>
        [HarmonyPatch(typeof(BargainUIManager), "AppendClientLine")]
        internal static class AppendClientLinePatch
        {
            [HarmonyPrefix]
            private static void Prefix(ref List<string> lines)
            {
                ReplaceLines(ref lines);
            }
        }
    }
}
