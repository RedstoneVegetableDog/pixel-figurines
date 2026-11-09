using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 框选兜底（2026-11-06 修正）
    ///
    /// 旧版兜底在「空地/柜台背景起框」时把 WindowsHandler.visibleWindows 里的**所有窗口**都扫一遍，
    /// 于是拖拽/框选会连当前 UI 窗口后面的物品一起选中、一起拖走（用户 2026-11-06 视频复现）。
    ///
    /// 现在按原版的三种作用域补选，绝不跨窗口：
    /// · bandWindow != null      → 原版已经按该窗口扫，直接不补；
    /// · bandInventory != null   → 只补该库存的子物品；
    /// · 两者都 null（空地起框） → 只补「鼠标按下点所在的最上层窗口」里的物品。
    /// </summary>
    [HarmonyPatch(typeof(ItemMultiSelectHandler), "ComputeBandItems")]
    internal static class SelectionFallback
    {
        private static int _left = 12;
        private static MethodInfo _overlap;
        private static MethodInfo _elemRect;

        private static T GetField<T>(object obj, string name)
        {
            try
            {
                if (obj == null) return default(T);
                var f = AccessTools.Field(obj.GetType(), name);
                return f != null ? (T)f.GetValue(obj) : default(T);
            }
            catch { return default(T); }
        }

        [HarmonyPostfix]
        private static void Postfix(ItemMultiSelectHandler __instance, Rect bandRect, ref List<GameItemElement> __result)
        {
            try
            {
                if (_overlap == null) _overlap = AccessTools.Method(typeof(ItemMultiSelectHandler), "BandOverlapsItemShape");
                if (_elemRect == null) _elemRect = AccessTools.Method(typeof(ItemMultiSelectHandler), "GetElementWorldRect");
                if (_overlap == null) return;

                // 作用域：只在原版该扫的窗口/库存里补，避免跨窗口把后面的物品也选上
                var bandWindow = GetField<PixelWindow>(__instance, "bandWindow");
                var bandInventory = GetField<GameInventory>(__instance, "bandInventory");
                if (bandWindow != null) return;   // 原版已按该窗口扫过

                var cands = new List<GameItem>();
                try
                {
                    if (bandInventory != null)
                    {
                        if (bandInventory.childItems != null) cands.AddRange(bandInventory.childItems);
                    }
                    else
                    {
                        // 空地起框：只取鼠标按下处最上层的窗口
                        var wh = WindowsHandler.current;
                        if (wh == null || wh.visibleWindows == null) return;
                        Vector3 press = GetField<Vector3>(__instance, "pressPosition");
                        PixelWindow top = null;
                        foreach (var w in wh.visibleWindows)
                        {
                            if (w == null) continue;
                            try
                            {
                                var rt = w.rectTransform;
                                if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, press)) top = w;
                            }
                            catch { }
                        }
                        if (top == null) return;
                        cands.AddRange(GraphUtils.FindChildrenType<GameItem>(top));
                    }
                }
                catch (System.Exception __ex) { Safe.Warn("SelectionFallback.collect", __ex); }
                if (cands.Count == 0) return;

                var added = new List<GameItemElement>();
                foreach (var it in cands)
                {
                    var el = it as GameItemElement;
                    if (el == null) continue;
                    try
                    {
                        if (el.IsDestroyed()) continue;
                        if (el.parentInventory == null) continue;
                        if (el.isDebugMenu) continue;
                        if (!el.MayRemove()) continue;
                        if (el.MaxNumRemove() < 1) continue;
                        if (ItemPinHelper.IsPinned(el)) continue;
                        if (el.handler == null) continue;
                        if (!el.handler.gameObject.activeInHierarchy) continue;

                        bool dup = false;
                        if (__result != null)
                            foreach (var e2 in __result) { if (e2 == el) { dup = true; break; } }
                        if (dup) continue;

                        // 原版的裁剪矩形（所属库存的可视范围）
                        Rect clipRect = default;
                        bool hasClip = false;
                        try
                        {
                            var inv = el.parentInventory as PixelElement;
                            if (inv != null && _elemRect != null)
                            {
                                clipRect = (Rect)_elemRect.Invoke(null, new object[] { inv });
                                hasClip = clipRect.width > 0f && clipRect.height > 0f;
                            }
                        }
                        catch { }

                        var args = new object[] { el, bandRect, clipRect, hasClip, null };
                        bool hit = false;
                        try { hit = (bool)_overlap.Invoke(null, args); } catch { }
                        if (!hit) continue;

                        added.Add(el);
                    }
                    catch (System.Exception __ex) { Safe.Warn("SelectionFallback.item", __ex); }
                }

                if (added.Count > 0)
                {
                    int vanillaCount = __result != null ? __result.Count : 0;
                    var merged = new List<GameItemElement>();
                    if (__result != null) merged.AddRange(__result);
                    merged.AddRange(added);
                    __result = merged;

                    if (_left > 0)
                    {
                        _left--;
                        var sb = new System.Text.StringBuilder();
                        for (int i = 0; i < added.Count && i < 20; i++)
                        {
                            try { sb.Append(added[i].identifier).Append(' '); } catch { }
                        }
                        Debug.Log(FigurineMod.LogTag + "框选兜底：原版 " + vanillaCount + " 件 + 补选 " + added.Count
                            + " 件：" + sb.ToString().Trim());
                    }
                }
            }
            catch (Exception __ex) { Safe.Warn("SelectionFallback", __ex); }
        }
    }
}
