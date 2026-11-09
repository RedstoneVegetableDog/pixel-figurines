using System;
using HarmonyLib;

namespace FigurinePort
{
    /// <summary>
    /// 展示柜禁用「切换」：原版 CanToggleSlotItem / MayToggleSlotItem 直接返回 false，
    /// 右键项会按原版禁用条件渲染（与装备/激活同一套颜色），提示框也不再出现切换说明。
    /// </summary>
    [HarmonyPatch(typeof(GameItem))]
    internal static class CabinetToggleOff
    {
        private static bool IsCabinet(GameItem it)
        {
            try { return it != null && (it.identifier == Cabinet.Id || it.identifier == DoroItems.MailboxId); } catch { return false; }
        }

        [HarmonyPatch(typeof(ToggleAppearanceHelper), "CreateToggleAppearanceTooltip")]
        internal static class TooltipOff
        {
            [HarmonyPrefix]
            private static bool Prefix(ToggleAppearanceHelper __instance, RichTextBuilder __0, GameItem __1)
            {
                try { if (IsCabinet(__1)) return false; } catch (System.Exception __ex) { Safe.Warn("CabinetToggleOff.cs:24", __ex); }
                return true;
            }
        }

        [HarmonyPatch(nameof(GameItem.CanToggleSlotItem))]
        [HarmonyPrefix]
        private static bool CanToggle(GameItem __instance, ref bool __result)
        {
            if (IsCabinet(__instance)) { __result = false; return false; }
            return true;
        }

        [HarmonyPatch(nameof(GameItem.MayToggleSlotItem))]
        [HarmonyPrefix]
        private static bool MayToggle(GameItem __instance, ref bool __result)
        {
            if (IsCabinet(__instance)) { __result = false; return false; }
            return true;
        }
    }
}
