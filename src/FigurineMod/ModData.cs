using System;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>官方 ModHelper 存档数据封装（随存档槽保存，无需 files 权限）。</summary>
    internal static class ModData
    {
        public const string Id = "figurine.gs";

        public static string Get(string key, string def = "") 
        {
            try { return ModHelper.GetModData(Id, key, def) ?? def; } catch { return def; }
        }

        public static void Set(string key, string value)
        {
            try { ModHelper.SetModData(Id, key, value ?? ""); } catch (System.Exception __ex) { Safe.Warn("ModData.cs:18", __ex); }
        }

        public static string[] GetLines(string key)
        {
            var s = Get(key, "");
            if (string.IsNullOrEmpty(s)) return new string[0];
            return s.Split('\n');
        }

        public static void SetLines(string key, System.Collections.Generic.IEnumerable<string> lines)
        {
            Set(key, string.Join("\n", new System.Collections.Generic.List<string>(lines).ToArray()));
        }
    }
}
