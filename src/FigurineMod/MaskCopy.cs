using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FigurinePort
{
    /// <summary>
    /// 完全照 0.8.0 发布包的 `FigurineMaskCopy` 复刻：镜片里的那张立绘。
    ///
    /// ```text
    /// Mask   = ui.magnifyingGlass 下名字为 "Mask" 的子节点
    /// 面板   = Mask 下名为 "FigurineInspection" 的节点；没有就新建
    ///          （锚点居中，localScale = 2，sizeDelta = 484×456 —— 与原版七个物品面板一致）
    /// 子图   = 面板下 "Art"，localScale = 1
    /// 适配   = FitToMask：立绘尺寸 = (Mask 宽 / 面板缩放, Mask 高 / 面板缩放)，面板尺寸 = Mask 尺寸
    /// ```
    /// 面板是 Mask 的子节点，所以镜片移动时立绘自然跟随 —— 这一点和 0.8.0 相同。
    /// </summary>
    internal static class MaskCopy
    {
        public const string PanelName = "FigurineInspection";
        private const string ArtName = "Art";

        private static GameObject _panel;
        private static RectTransform _panelRt;
        private static Image _image;
        private static RectTransform _imageRt;
        private static GameObject _mask;
        private static Sprite _lastSprite;

        public static void Show(Sprite sprite, InspectionUIManager ui)
        {
            try
            {
                if (sprite == null) return;
                if (_mask == null)
                {
                    _mask = FindMask(ui);
                    if (_mask == null) { Debug.LogWarning(FigurineMod.LogTag + "镜片副本：找不到 Mask（原版镜片窗口）"); return; }
                }
                EnsurePanel();
                if (_panel == null) return;

                bool changed = _lastSprite != sprite;
                try
                {
                    if (_image != null && _image.sprite != sprite) _image.sprite = sprite;
                    _lastSprite = sprite;
                }
                catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "镜片副本换贴图失败：" + e.Message); }

                FitToMask();
                SetVisible(true);
                try
                {
                    // 0.8.0 的 GetGlassRect() = ui.magnifyingGlass 的 RectTransform（不是 Mask）
                    var glass = ui != null ? ui.magnifyingGlass : null;
                    var lf = _panel.GetComponent<LensFollow>();
                    if (lf == null) lf = _panel.AddComponent<LensFollow>();
                    lf.Bind(_panelRt, glass);
                    lf.follow = 0.8f;      // LensCopyFollow 默认 0.8
                    lf.tune = Vector2.zero; // LensCopyOffsetX/Y 默认 0
                }
                catch (System.Exception __ex) { Safe.Warn("MaskCopy.cs:65", __ex); }
                if (changed) Debug.Log(FigurineMod.LogTag + "镜片副本：显示 " + (sprite != null ? sprite.name : "null"));
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "镜片副本挂载失败：" + e.GetType().Name + " " + e.Message); }
        }

        public static void Hide()
        {
            try { SetVisible(false); } catch (System.Exception __ex) { Safe.Warn("MaskCopy.cs:73", __ex); }
        }

        private static void SetVisible(bool v)
        {
            try { if (_panel != null && _panel.activeSelf != v) _panel.SetActive(v); } catch (System.Exception __ex) { Safe.Warn("MaskCopy.cs:78", __ex); }
        }

        private static void EnsurePanel()
        {
            try
            {
                if (_panel != null || _mask == null) return;

                var found = FindOwnPanel(_mask.transform);
                if (found != null)
                {
                    _panel = found;
                    _panelRt = found.GetComponent<RectTransform>();
                    _image = found.GetComponentInChildren<Image>(true);
                    _imageRt = _image != null ? _image.rectTransform : null;
                    return;
                }

                var go = new GameObject(PanelName);
                var rt = go.AddComponent<RectTransform>();
                go.transform.SetParent(_mask.transform, false);
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = new Vector3(2f, 2f, 1f);
                rt.sizeDelta = new Vector2(484f, 456f);

                var art = new GameObject(ArtName);
                var artRt = art.AddComponent<RectTransform>();
                art.transform.SetParent(go.transform, false);
                artRt.anchorMin = new Vector2(0.5f, 0.5f);
                artRt.anchorMax = new Vector2(0.5f, 0.5f);
                artRt.pivot = new Vector2(0.5f, 0.5f);
                artRt.anchoredPosition = Vector2.zero;
                artRt.localScale = Vector3.one;
                artRt.sizeDelta = new Vector2(484f, 456f);

                var img = art.AddComponent<Image>();
                try { img.raycastTarget = false; } catch (System.Exception __ex) { Safe.Warn("MaskCopy.cs:118", __ex); }
                img.preserveAspect = true;

                _panel = go;
                _panelRt = rt;
                _image = img;
                _imageRt = artRt;
                go.SetActive(false);
                Debug.Log(FigurineMod.LogTag + "镜片副本：在 Mask 下新建面板 " + PanelName + "（localScale=2，与原版七个物品面板一致）");
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "镜片副本建面板失败：" + e.Message); }
        }

        private static void FitToMask()
        {
            try
            {
                if (_mask == null || _panelRt == null) return;
                var mrt = _mask.GetComponent<RectTransform>();
                float w = 484f, h = 456f;
                if (mrt != null && mrt.rect.width > 8f && mrt.rect.height > 8f)
                {
                    w = mrt.rect.width;
                    h = mrt.rect.height;
                }
                float ps = Mathf.Abs(_panelRt.localScale.x);
                if (ps < 0.01f) ps = 1f;
                // LensCopyScale：0.8.0 默认 1.4，按用户要求改为 1.2
                float extra = 1.2f;
                float aw = (w / ps) * extra;
                float ah = (h / ps) * extra;
                if (_imageRt != null) _imageRt.sizeDelta = new Vector2(aw, ah);
                _panelRt.sizeDelta = new Vector2(w, h);
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "镜片副本适配失败：" + e.Message); }
        }

        /// <summary>
        /// 照 0.8.0 `FigurineMaskCopy.Tick()`：
        /// ```csharp
        /// Vector2 p = glassRect.anchoredPosition;
        /// _panelRt.anchoredPosition = new Vector2(-p.x * follow, -p.y * follow) + _tune;
        /// ```
        /// 也就是面板反向抵消镜片位移 → 立绘看起来钉在托盘上，镜片从它上面扫过（不会被「锁」在镜片上）。
        /// </summary>
        internal class LensFollow : MonoBehaviour
        {
            private RectTransform _panel;
            private GameObject _glass;
            public float follow = 0.8f;
            public Vector2 tune = Vector2.zero;

            public void Bind(RectTransform panel, GameObject glass)
            {
                _panel = panel;
                _glass = glass;
                Debug.Log(FigurineMod.LogTag + "镜片跟随绑定：glass=" + (glass != null ? glass.name : "null"));
            }

            private void LateUpdate()
            {
                try
                {
                    if (_panel == null || _glass == null) return;
                    var gr = _glass.GetComponent<RectTransform>();
                    if (gr == null) return;
                    Vector2 p = gr.anchoredPosition;
                    _panel.anchoredPosition = new Vector2(-p.x * follow, -p.y * follow) + tune;
                }
                catch (System.Exception __ex) { Safe.Warn("MaskCopy.cs:187", __ex); }
            }
        }

        private static GameObject FindMask(InspectionUIManager ui)
        {
            try
            {
                var glass = ui != null ? ui.magnifyingGlass : null;
                if (glass == null) return null;
                foreach (var tr in glass.GetComponentsInChildren<Transform>(true))
                    if (tr != null && tr.name == "Mask") return tr.gameObject;
            }
            catch (System.Exception __ex) { Safe.Warn("MaskCopy.cs:200", __ex); }
            return null;
        }

        private static GameObject FindOwnPanel(Transform root)
        {
            if (root == null) return null;
            var stack = new Stack<Transform>();
            stack.Push(root);
            int guard = 0;
            while (stack.Count > 0 && guard++ < 200)
            {
                var tr = stack.Pop();
                if (tr == null) continue;
                if (string.Equals(tr.name, PanelName, StringComparison.Ordinal)) return tr.gameObject;
                for (int i = tr.childCount - 1; i >= 0; i--)
                {
                    var c = tr.GetChild(i);
                    if (c != null) stack.Push(c);
                }
            }
            return null;
        }
    }
}
