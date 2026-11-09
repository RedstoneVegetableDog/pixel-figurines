using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FigurinePort
{
    /// <summary>
    /// 完全照 0.8.0 发布包的 `FigurineInspection.Show` 复刻：
    ///
    /// ```text
    /// 容器   = InspectionUIManager.cigaretteUISmall.gameObject
    ///          （0.8.0 就是这么用的 —— 先把香烟面板的部件全关掉，再往里放我们的立绘）
    /// 清场   = cig.DisableAll() + HideAllGraphics(容器)：所有 Image 关掉、SpriteRenderer 关掉
    /// 显示槽 = 查找名为 "FigurineDisplay" 的 Image；没有就新建（锚点居中，32×48）
    /// 尺寸   = ScaleToFit：sizeDelta = 贴图原始尺寸；
    ///          viewport = magnifyingGlass 里名字以 "Mask" 开头的 RectTransform（退路：面板矩形 / 484×456）
    ///          k = min(vw/贴图宽, vh/贴图高)；贴图远大于视野时不放大；
    ///          localScale = k；anchoredPosition = (0, -(贴图高×k)×0.025)
    /// ```
    /// 赝品走 `FigurineFake.TrayKeyFor(item)`（`_fakeN` 反色图），取不到再退回物品贴图。
    /// </summary>
    [HarmonyPatch(typeof(InspectionUIManager), "OnInspectItem")]
    internal static class InspectionViewPatch
    {
        private const string SlotName = "FigurineDisplay";

        [HarmonyPostfix]
        private static void Postfix(InspectionUIManager __instance, GameItem __0)
        {
            try
            {
                string id = null;
                try { id = __0 != null ? __0.identifier : null; } catch (System.Exception __ex) { Safe.Warn("InspectionViewPatch.cs:35", __ex); }

                if (__instance == null || !IsOurFigurine(id))
                {
                    HideOwnDisplay(__instance);
                    try { MaskCopy.Hide(); } catch (System.Exception __ex) { Safe.Warn("InspectionViewPatch.cs:40", __ex); }
                    return;
                }

                string key = FigurineFake.TrayKeyFor(__0);
                if (string.IsNullOrEmpty(key)) key = id;

                Sprite sp = TrayArt.Get(key);
                if (sp == null)
                {
                    sp = LoadItemSprite(id);
                    if (sp == null) return;
                    Debug.LogWarning(FigurineMod.LogTag + "检查托盘：托盘贴图 '" + key + "' 缺失 → 退回物品贴图 '" + id + "'");
                }

                var cig = __instance.cigaretteUISmall;
                if (cig == null) return;
                var container = cig.gameObject;
                if (container == null) return;

                try { cig.DisableAll(); } catch (System.Exception __ex) { Safe.Warn("InspectionViewPatch.cs:60", __ex); }
                HideAllGraphics(container);
                try { container.SetActive(true); } catch (System.Exception __ex) { Safe.Warn("InspectionViewPatch.cs:62", __ex); }

                var img = ResolveDisplayImage(container);
                if (img == null) return;

                img.sprite = sp;
                try { img.gameObject.SetActive(true); } catch (System.Exception __ex) { Safe.Warn("InspectionViewPatch.cs:68", __ex); }
                try { img.enabled = true; } catch (System.Exception __ex) { Safe.Warn("InspectionViewPatch.cs:69", __ex); }

                ScaleToFit(img, container);

                // 镜片里那张：0.8.0 的 FigurineMaskCopy
                try { MaskCopy.Show(sp, __instance); } catch (System.Exception __ex) { Safe.Warn("InspectionViewPatch.Lens", __ex); }

                // 鉴定面板：接管游戏原生的「勾缺损」面板（用户 2026-11-04 定）
                try
                {
                    FigurineInspect.Open(__instance, __0);
                }
                catch (System.Exception __ex) { Safe.Warn("InspectionViewPatch.Confront", __ex); }
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "检查面板显示失败：" + e.GetType().Name + " " + e.Message); }
        }

        /// <summary>0.8.0 `HideAllGraphics`：把容器下所有 Image / SpriteRenderer 关掉。</summary>
        private static void HideAllGraphics(GameObject root)
        {
            if (root == null) return;
            try
            {
                var imgs = root.GetComponentsInChildren<Image>(true);
                if (imgs != null)
                {
                    foreach (var im in imgs)
                    {
                        if (im == null || im.gameObject == null) continue;
                        try { im.gameObject.SetActive(false); } catch (System.Exception __ex) { Safe.Warn("InspectionViewPatch.cs:91", __ex); }
                        try { im.enabled = false; } catch (System.Exception __ex) { Safe.Warn("InspectionViewPatch.cs:92", __ex); }
                    }
                }
                var rds = root.GetComponentsInChildren<SpriteRenderer>(true);
                if (rds != null)
                {
                    foreach (var r in rds)
                    {
                        if (r == null) continue;
                        try { r.enabled = false; } catch (System.Exception __ex) { Safe.Warn("InspectionViewPatch.cs:101", __ex); }
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "清理容器图形失败：" + e.Message); }
        }

        /// <summary>0.8.0 `ResolveDisplayImage`：找 "FigurineDisplay" 槽，没有就新建。</summary>
        private static Image ResolveDisplayImage(GameObject panelGo)
        {
            if (panelGo == null) return null;
            var own = FindOwnSlot(panelGo.transform);
            if (own != null) return own;

            var go = new GameObject(SlotName);
            var rt = go.AddComponent<RectTransform>();
            go.transform.SetParent(panelGo.transform, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(32f, 48f);
            var im = go.AddComponent<Image>();
            try { im.raycastTarget = false; } catch (System.Exception __ex) { Safe.Warn("InspectionViewPatch.cs:124", __ex); }
            return im;
        }

        private static Image FindOwnSlot(Transform root)
        {
            if (root == null) return null;
            var stack = new Stack<Transform>();
            stack.Push(root);
            int guard = 0;
            while (stack.Count > 0 && guard++ < 200)
            {
                var tr = stack.Pop();
                if (tr == null) continue;
                if (string.Equals(tr.name, SlotName, StringComparison.Ordinal))
                {
                    var im = tr.gameObject.GetComponent<Image>();
                    if (im != null) return im;
                }
                for (int i = tr.childCount - 1; i >= 0; i--)
                {
                    var c = tr.GetChild(i);
                    if (c != null) stack.Push(c);
                }
            }
            return null;
        }

        /// <summary>0.8.0 `ScaleToFit`（含 FindViewportRect 与 0.025 下移）。</summary>
        private static void ScaleToFit(Image img, GameObject panelGo)
        {
            try
            {
                var rt = img.rectTransform;
                if (rt == null) return;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                float tw = 32f, th = 48f;
                if (img.sprite != null)
                {
                    tw = Mathf.Max(1f, img.sprite.rect.width);
                    th = Mathf.Max(1f, img.sprite.rect.height);
                }
                rt.sizeDelta = new Vector2(tw, th);

                float vw = 0f, vh = 0f;
                var vp = FindViewportRect(InspectionUIManager.Instance);
                if (vp != null) { vw = vp.rect.width; vh = vp.rect.height; }
                if (vw < 8f || vh < 8f)
                {
                    var prt = panelGo != null ? panelGo.GetComponent<RectTransform>() : null;
                    if (prt != null) { vw = prt.rect.width; vh = prt.rect.height; }
                }
                if (vw < 8f || vw > 3000f) vw = 484f;
                if (vh < 8f || vh > 3000f) vh = 456f;

                float k = Mathf.Min(vw / tw, vh / th);
                bool huge = tw > vw * 1.6f || th > vh * 1.6f;
                if (k < 1f && !huge) k = 1f;
                if (k <= 0f || float.IsNaN(k)) k = 1f;
                k *= 0.8f;                 // InspectionTrayScale 默认 0.8（0.8.0 配置）

                rt.localScale = new Vector3(k, k, 1f);
                rt.anchoredPosition = new Vector2(0f, -(th * k) * 0.025f);
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "尺寸适配失败：" + e.Message); }
        }

        /// <summary>0.8.0 `FindViewportRect`：放大镜里名字以 Mask 开头的矩形，退路是第一个有效子矩形。</summary>
        private static RectTransform FindViewportRect(InspectionUIManager ui)
        {
            try
            {
                var glass = ui != null ? ui.magnifyingGlass : null;
                if (glass == null || glass.transform == null) return null;
                int n = glass.transform.childCount;
                for (int i = 0; i < n; i++)
                {
                    var c = glass.transform.GetChild(i);
                    if (c == null || c.name == null) continue;
                    if (!c.name.StartsWith("Mask", StringComparison.OrdinalIgnoreCase)) continue;
                    var rt = c.GetComponent<RectTransform>();
                    if (rt != null && rt.rect.width > 8f && rt.rect.height > 8f) return rt;
                }
                for (int i = 0; i < n; i++)
                {
                    var c = glass.transform.GetChild(i);
                    if (c == null) continue;
                    var rt = c.GetComponent<RectTransform>();
                    if (rt != null && rt.rect.width > 8f && rt.rect.height > 8f) return rt;
                }
            }
            catch (System.Exception __ex) { Safe.Warn("InspectionViewPatch.cs:218", __ex); }
            return null;
        }

        private static void HideOwnDisplay(InspectionUIManager ui)
        {
            try
            {
                if (ui == null || ui.cigaretteUISmall == null) return;
                var slot = FindOwnSlot(ui.cigaretteUISmall.transform);
                if (slot == null) return;
                try { slot.sprite = null; } catch (System.Exception __ex) { Safe.Warn("InspectionViewPatch.cs:229", __ex); }
                try { slot.enabled = false; } catch (System.Exception __ex) { Safe.Warn("InspectionViewPatch.cs:230", __ex); }
                try { slot.gameObject.SetActive(false); } catch (System.Exception __ex) { Safe.Warn("InspectionViewPatch.cs:231", __ex); }
            }
            catch (System.Exception __ex) { Safe.Warn("InspectionViewPatch.cs:233", __ex); }
        }

        /// <summary>三 IP 都算（0.8.0 查 FigurineCatalog，我们查 Defs）。</summary>
        private static bool IsOurFigurine(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id)) return false;
                if (!id.StartsWith("figurine_", StringComparison.Ordinal)) return false;
                if (id.IndexOf("blindbox", StringComparison.Ordinal) >= 0) return false;
                if (id.IndexOf("card", StringComparison.Ordinal) >= 0) return false;
                if (id.IndexOf("mailbox", StringComparison.Ordinal) >= 0) return false;
                return !string.IsNullOrEmpty(Defs.Find(id).Id);
            }
            catch { return false; }
        }

        private static Sprite LoadItemSprite(string id)
        {
            try
            {
                var mods = ModLoader.Mods;
                if (mods == null) return null;
                foreach (var m in mods)
                {
                    if (m == null || m.Manifest == null || m.Manifest.ID != "figurine.gs") continue;
                    Sprite sp = null;
                    if (m.Sprites != null && m.Sprites.TryGetValue(id, out sp) && sp != null) return sp;
                }
            }
            catch (System.Exception __ex) { Safe.Warn("InspectionViewPatch.cs:264", __ex); }
            return null;
        }
    }
}
