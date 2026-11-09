using System;
using System.Collections.Generic;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 统一的「吞异常但留痕」入口。
    ///
    /// 背景：全工程原有 300+ 处 `catch { }`，好处是单点失败不会崩游戏，
    /// 坏处是失败时日志里什么都没有（这几轮排查全靠零星的 LogWarning）。
    ///
    /// 这里给每处 catch 一个稳定站点名（文件:行），第一次失败打 Warning，
    /// 同一个站点后续失败只累加计数，避免刷屏。
    /// </summary>
    internal static class Safe
    {
        private static readonly Dictionary<string, int> _count = new Dictionary<string, int>(StringComparer.Ordinal);
        private const int MaxPerSite = 1;   // 每个站点最多打几条

        public static void Warn(string site, Exception e)
        {
            try
            {
                int n;
                _count.TryGetValue(site, out n);
                _count[site] = n + 1;
                if (n >= MaxPerSite) return;

                string msg = e != null ? e.GetType().Name + ": " + e.Message : "null";
                Debug.LogWarning(FigurineMod.LogTag + "静默失败 @" + site + " → " + msg
                    + (n == 0 ? "" : $"（同一站点已出现 {n + 1} 次）"));
            }
            catch { }
        }

        /// <summary>整个动作包一层，出错留痕并返回 false。</summary>
        public static bool Run(string site, Action action)
        {
            try { action(); return true; }
            catch (Exception e) { Warn(site, e); return false; }
        }

        /// <summary>取返回值的版本；出错返回 fallback。</summary>
        public static T Run<T>(string site, Func<T> func, T fallback)
        {
            try { return func(); }
            catch (Exception e) { Warn(site, e); return fallback; }
        }

        public static string Summary()
        {
            try
            {
                int sites = _count.Count, total = 0;
                foreach (var kv in _count) total += kv.Value;
                return "静默失败站点 " + sites + " 个 / 共 " + total + " 次";
            }
            catch { return ""; }
        }
    }
}
