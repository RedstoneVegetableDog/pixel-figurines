using System;
using HarmonyLib;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 手办贴图四周自带透明边（实测 132 张原神手办平均不透明像素只占 47%）。
    /// 原版 GameItemElement 用 `alphaHitTestMinimumThreshold = 1e-5` 做像素级点击判定：
    /// 透明边点不到 → 点击/悬停判定只在中间一小块，表现为「贴图没占满格子 + 交互偏移」。
    ///
    /// 这里只把我们手办元素的 alpha 阈值放宽到 0，让整块 3×3 格子都能点中；**不改贴图、不改显示**。
    /// </summary>
    [HarmonyPatch(typeof(GameItemElement), "ValidateSelectLocked")]
    internal static class FigurineHitAreaPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GameItemElement __instance)
        {
            try
            {
                if (__instance == null) return;
                if (!FigurineAuth.IsFigurine(__instance)) return;
                var bg = __instance.background;
                if (bg == null || bg.image == null) return;
                if (bg.image.alphaHitTestMinimumThreshold > 0f)
                    bg.image.alphaHitTestMinimumThreshold = 0f;
                bg.image.raycastTarget = true;
            }
            catch (Exception __ex) { Safe.Warn("FigurineHitArea", __ex); }
        }
    }
}
