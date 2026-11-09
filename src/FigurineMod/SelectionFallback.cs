using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 框选兜底（2026-11-10 去反射重写）
    ///
    /// 原版 `ItemMultiSelectHandler.ComputeBandItems` 在「空地起框」（bandWindow/bandInventory 都为空）时
    /// 会扫 `WindowsHandler.visibleWindows` 里的**所有窗口**，于是框选会连着当前 UI 窗口后面的物品一起选中。
    ///
    /// 这里用 Harmony 补丁记录按下点（不读私有字段），并复刻原版的「世界矩形 + 形状格子」相交判断
    /// （不调用私有方法），只补「按下点所在的最上层窗口/库存」里的物品，绝不跨窗口。
    /// </summary>
    internal static class SelectionFallback
    {
        private static Vector3 _press;

        [HarmonyPatch(typeof(ItemMultiSelectHandler), "OnEventPress")]
        internal static class PressCapture
        {
            [HarmonyPrefix]
            private static void Prefix()
            {
                try { _press = Input.mousePosition; } catch { }
            }
        }

        [HarmonyPatch(typeof(ItemMultiSelectHandler), "OnEventReset")]
        internal static class ResetCapture
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                _press = Vector3.zero;
            }
        }

        [HarmonyPatch(typeof(ItemMultiSelectHandler), "ComputeBandItems")]
        internal static class ComputePatch
        {
            private static int _left = 12;

            [HarmonyPostfix]
            private static void Postfix(Rect bandRect, ref List<GameItemElement> __result)
            {
                try
                {
                    if (_press == Vector3.zero) return;

                    var bandInventory = RenderHandler.RaycastFindParentElement<GameInventory>(_press);
                    var bandWindow = RenderHandler.RaycastFindParentElement<PixelWindow>(_press);
                    if (bandWindow != null) return;   // 原版已经按该窗口扫过

                    var cands = new List<GameItem>();
                    if (bandInventory != null)
                    {
                        if (bandInventory.childItems != null) cands.AddRange(bandInventory.childItems);
                    }
                    else
                    {
                        // 空地起框：只取按下点所在的最上层窗口
                        var wh = WindowsHandler.current;
                        if (wh == null || wh.visibleWindows == null) return;
                        PixelWindow top = null;
                        foreach (var w in wh.visibleWindows)
                        {
                            if (w == null) continue;
                            try
                            {
                                var rt = w.rectTransform;
                                if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, _press)) top = w;
                            }
                            catch { }
                        }
                        if (top == null) return;
                        cands.AddRange(GraphUtils.FindChildrenType<GameItem>(top));
                    }
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

                            Rect clipRect = default;
                            bool hasClip = false;
                            var inv = el.parentInventory as PixelElement;
                            if (inv != null)
                            {
                                clipRect = GetElementWorldRect(inv);
                                hasClip = clipRect.width > 0f && clipRect.height > 0f;
                            }

                            if (!BandOverlaps(el, bandRect, clipRect, hasClip)) continue;
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

        // ───────────────────────── 复刻原版判定（不反射） ─────────────────────────

        private static Rect GetElementWorldRect(PixelElement element)
        {
            RectTransform rectTransform = element.handler.rectTransform;
            Vector3 position = rectTransform.position;
            Vector3 lossyScale = rectTransform.lossyScale;
            float w = (float)element.widthPixels * lossyScale.x;
            float h = (float)element.heightPixels * lossyScale.y;
            return Rect.MinMaxRect(position.x, position.y - h, position.x + w, position.y);
        }

        private static bool BandOverlaps(GameItemElement element, Rect bandRect, Rect clipRect, bool hasClip)
        {
            GridShape modifiedShape = element.modifiedShape;
            if (modifiedShape == null) return false;

            Vector3 position = element.middle.rectTransform.position;
            Vector3 lossyScale = element.middle.rectTransform.lossyScale;
            float cw = (float)UISettings.current.gridSize * lossyScale.x;
            float ch = (float)UISettings.current.gridSize * lossyScale.y;

            for (int i = 0; i < modifiedShape.height; i++)
            {
                for (int j = 0; j < modifiedShape.width; j++)
                {
                    if (modifiedShape.GetLocal(j, i) == 0) continue;
                    var (cx, cy) = modifiedShape.ForwardTransform(j, i);
                    float x1 = position.x + (float)(cx - modifiedShape.minX) * cw;
                    float y1 = position.y - (float)(cy - modifiedShape.minY) * ch;
                    float x2 = x1 + cw;
                    float y2 = y1 - ch;

                    if (hasClip)
                    {
                        x1 = Mathf.Max(x1, clipRect.xMin);
                        y2 = Mathf.Max(y2, clipRect.yMin);
                        x2 = Mathf.Min(x2, clipRect.xMax);
                        y1 = Mathf.Min(y1, clipRect.yMax);
                        if (x2 <= x1 || y1 <= y2) continue;
                    }

                    if (!(bandRect.xMin >= x2) && !(bandRect.xMax <= x1)
                        && !(bandRect.yMin >= y1) && !(bandRect.yMax <= y2))
                        return true;
                }
            }
            return false;
        }
    }
}
