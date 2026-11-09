using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 议价反应台词：把 BargainUIManager 的 12 个私有静态台词池换成顾客专属内容，
    /// 议价界面关闭后还原（旧版同款反射方案）。
    /// </summary>
    internal static class BargainLines
    {
        private static readonly string[] FieldNames =
        {
            "acceptGratefulLines", "acceptVeryGratefulLines", "acceptReluctantLines",
            "acceptVeryReluctantLines", "rejectLines", "hardRejectLines", "leaveLines",
            "budgetIncreaseLines", "budgetDecreaseLines", "talkBadLines",
            "innocentBlackmailLines", "falseThreatenLines",
        };

        private static object[] _pools;
        private static List<List<string>> _defaults;
        private static bool _ready;
        private static string _appliedId;

        private static object Resolve(string name)
        {
            var t = typeof(BargainUIManager);
            try
            {
                var f = t.GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null) return f;
            }
            catch (System.Exception __ex) { Safe.Warn("BargainLines.cs:36", __ex); }
            try
            {
                var p = t.GetProperty(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (p != null && p.CanRead) return p;
            }
            catch (System.Exception __ex) { Safe.Warn("BargainLines.cs:42", __ex); }
            return null;
        }

        private static List<string> Read(object accessor)
        {
            try
            {
                object v = null;
                if (accessor is FieldInfo f) v = f.GetValue(null);
                else if (accessor is PropertyInfo p) v = p.GetValue(null);
                return v as List<string>;
            }
            catch { return null; }
        }

        private static bool Write(object accessor, List<string> value)
        {
            try
            {
                if (accessor is FieldInfo f) { f.SetValue(null, value); return true; }
                if (accessor is PropertyInfo p && p.CanWrite) { p.SetValue(null, value); return true; }
            }
            catch (System.Exception __ex) { Safe.Warn("BargainLines.cs:65", __ex); }
            return false;
        }

        private static bool EnsureFields()
        {
            if (_ready) return _pools != null;
            _ready = true;
            _pools = new object[FieldNames.Length];
            _defaults = new List<List<string>>(FieldNames.Length);

            for (int i = 0; i < FieldNames.Length; i++)
            {
                try
                {
                    var acc = Resolve(FieldNames[i]);
                    _pools[i] = acc;
                    var cur = acc != null ? Read(acc) : null;
                    var snap = new List<string>();
                    if (cur != null) snap.AddRange(cur);
                    _defaults.Add(snap);
                }
                catch { _defaults.Add(new List<string>()); }
            }

            int ok = 0;
            foreach (var f in _pools) if (f != null) ok++;
            Debug.Log(FigurineMod.LogTag + "议价台词池接入 " + ok + "/" + FieldNames.Length);
            return _pools != null;
        }

        public static void ApplyFor(string identifier)
        {
            if (!EnsureFields() || _pools == null) return;
            if (string.Equals(_appliedId, identifier, StringComparison.Ordinal)) return;

            // 议价台词：按文档的 12 档情绪（欣然/非常满意/勉强/非常勉强/拒绝/强硬拒绝/
            // 直接走人/加预算/减预算/说不中听/被诬陷/虚张声势）各 5 条——顺序与游戏 12 个池一一对应
            string[][] pool = CustomerLines.For(identifier)?.Bargain;   // 0.8.0 原文（12 档 × 5 条）
            if (pool == null) { RestoreDefaults(); return; }

            int written = 0;
            for (int i = 0; i < _pools.Length && i < pool.Length; i++)
            {
                if (_pools[i] == null) continue;
                var src = pool[i];
                if (src == null || src.Length == 0) continue;
                try
                {
                    var cur = Read(_pools[i]);
                    if (cur == null)
                    {
                        cur = new List<string>();
                        if (!Write(_pools[i], cur)) continue;
                    }
                    cur.Clear();
                    foreach (var s in src) cur.Add(s);
                    written++;
                }
                catch (System.Exception __ex) { Safe.Warn("BargainLines.cs:125", __ex); }
            }
            _appliedId = identifier;
            Debug.Log(FigurineMod.LogTag + "议价台词：" + identifier + " 替换 " + written + " 个池");
        }

        public static void RestoreDefaults()
        {
            if (_pools == null || _defaults == null || string.IsNullOrEmpty(_appliedId)) return;
            for (int i = 0; i < _pools.Length; i++)
            {
                if (_pools[i] == null || _defaults[i] == null) continue;
                try
                {
                    var cur = Read(_pools[i]);
                    if (cur == null) continue;
                    cur.Clear();
                    foreach (var s in _defaults[i]) cur.Add(s);
                }
                catch (System.Exception __ex) { Safe.Warn("BargainLines.cs:144", __ex); }
            }
            _appliedId = null;
        }

        [HarmonyPatch(typeof(NegociationUIManager), nameof(NegociationUIManager.CloseUI))]
        internal static class CloseRestorePatch
        {
            [HarmonyPostfix]
            private static void Postfix() { try { RestoreDefaults(); } catch (System.Exception __ex) { Safe.Warn("BargainLines.cs:153", __ex); } }
        }
    }
}
