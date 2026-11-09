using System;
using HarmonyLib;

namespace FigurinePort
{
    /// <summary>把 type_figurine_gs_item 的 Translation Error 文本换成「手办」（按 0.8.0-beta LocFix 思路，正式版 API）。</summary>
    internal static class LocFix
    {
        private static string Fix(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s.IndexOf("type_figurine_gs_item", StringComparison.Ordinal) < 0) return s;
            return s.Replace("Translation Error 'type_figurine_gs_item' in Mechanic", "手办")
                    .Replace("Translation Error 'type_figurine_gs_item' in UI", "手办")
                    .Replace("Translation Error 'type_figurine_gs_item'", "手办");
        }

        [HarmonyPatch(typeof(LocHelper), "GetLocalizedMechanic")]
        internal static class MechanicPatch
        {
            [HarmonyPostfix]
            private static void Postfix(ref string __result) { __result = Fix(__result); }
        }

        [HarmonyPatch(typeof(LocHelper), "GetLocalizedUI")]
        internal static class UIPatch
        {
            [HarmonyPostfix]
            private static void Postfix(ref string __result) { __result = Fix(__result); }
        }

        [HarmonyPatch(typeof(LocHelper), "GetLocalizedItem")]
        internal static class ItemPatch
        {
            [HarmonyPostfix]
            private static void Postfix(ref string __result) { __result = Fix(__result); }
        }
    }
}
