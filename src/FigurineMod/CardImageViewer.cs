using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FigurinePort
{
    /// <summary>名片双击：自建叠加层显示原图（不依赖 CustomUI 的 AddImage）。</summary>
    [HarmonyPatch(typeof(ItemMouseDoubleClickHandler), "DoubleClickAction")]
    internal static class CardImageViewer
    {
        private static GameObject _root;

        [HarmonyPrefix]
        private static bool Prefix(GameItem __0)
        {
            try
            {
                if (__0 == null) return true;
                string key = null;
                if (__0.identifier == "figurine_gs_haizi_card") key = "haizi_card_ui";
                else if (__0.identifier == "dk_doro_card") key = "doro_card_big";
                if (key == null) return true;
                Show(key);
                return false;
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "名片显示失败：" + e.GetType().Name); }
            return true;
        }

        private static void Show(string key)
        {
            try
            {
                if (_root != null) UnityEngine.Object.Destroy(_root);
                var sprite = LoadSprite(key);
                if (sprite == null) { Debug.LogWarning(FigurineMod.LogTag + "名片图缺失：" + key); return; }

                _root = new GameObject("CardViewerCanvas");
                UnityEngine.Object.DontDestroyOnLoad(_root);
                var canvas = _root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 500;
                var scaler = _root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                _root.AddComponent<GraphicRaycaster>();

                var dim = new GameObject("Dim"); dim.transform.SetParent(_root.transform, false);
                var drt = dim.AddComponent<RectTransform>();
                drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
                var di = dim.AddComponent<Image>(); di.color = new Color(0, 0, 0, 0.7f);

                var imgGo = new GameObject("Image"); imgGo.transform.SetParent(_root.transform, false);
                var rt = imgGo.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
                float w = 660f, h = w * sprite.rect.height / Mathf.Max(1f, sprite.rect.width);
                rt.sizeDelta = new Vector2(w, h);
                var ii = imgGo.AddComponent<Image>(); ii.sprite = sprite; ii.preserveAspect = true;

                var close = new GameObject("Close"); close.transform.SetParent(_root.transform, false);
                var crt = close.AddComponent<RectTransform>();
                crt.anchorMin = new Vector2(0.5f, 0.5f); crt.anchorMax = new Vector2(0.5f, 0.5f);
                crt.anchoredPosition = new Vector2(w / 2 + 10, h / 2 - 20);
                crt.sizeDelta = new Vector2(46, 46);
                var ci = close.AddComponent<Image>(); ci.color = new Color(0.55f, 0.18f, 0.20f, 1f);
                var cb = close.AddComponent<Button>();
                cb.onClick.AddListener(new UnityEngine.Events.UnityAction(() => { if (_root != null) UnityEngine.Object.Destroy(_root); _root = null; }));
                var ct = new GameObject("x"); ct.transform.SetParent(close.transform, false);
                var trt = ct.AddComponent<RectTransform>();
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
                var tmp = ct.AddComponent<TMPro.TextMeshProUGUI>();
                tmp.text = "×"; tmp.fontSize = 30; tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.color = Color.white;

                Debug.Log(FigurineMod.LogTag + "名片原图已打开：" + key);
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "名片窗口异常：" + e.GetType().Name); }
        }

        private static Sprite LoadSprite(string key)
        {
            try
            {
                var mods = ModLoader.Mods;
                if (mods == null) return null;
                foreach (var m in mods)
                {
                    if (m == null || m.Manifest == null || m.Manifest.ID != "figurine.gs") continue;
                    Sprite sp = null;
                    try { if (m.Sprites != null && m.Sprites.TryGetValue(key, out sp) && sp != null) return sp; } catch (System.Exception __ex) { Safe.Warn("CardImageViewer.cs:92", __ex); }
                    if (m.Textures != null && m.Textures.TryGetValue(key, out var tex) && tex != null)
                        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }
            catch (System.Exception __ex) { Safe.Warn("CardImageViewer.cs:97", __ex); }
            return null;
        }
    }
}
