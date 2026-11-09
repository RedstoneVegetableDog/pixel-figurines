using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FigurinePort
{
    /// <summary>
    /// 终端机 →「展示」页 · 第一版（2026-11-05）
    ///
    /// 背景素材：`Textures/BG/show_stage.png`（1672×941，太空舱+两架+大窗+三圆台）
    /// 本版内容：背景图 + **35 个手办位**（架 A 4×4 / 架 B 4×4 / 圆台 3）+ 透视（后排缩小变暗）+ 抽屉骨架
    /// 还没做：从容器读真实手办、拖放、背景切换、吸引力公式
    ///
    /// 坐标全部按图量出，单位是「相对窗口的比例（0~1，左下原点）」。
    /// 想微调只改下面的锚点常量即可。
    /// </summary>
    internal static class ShowPage
    {
        // ── 布局锚点（按 1672×941 的素材量出来的比例）──
        private static readonly float[] TierY = { 0.256f, 0.391f, 0.525f, 0.660f };  // 架 1~4 层（下→上）
        private static readonly float[] TierScale = { 1.00f, 0.94f, 0.88f, 0.82f };  // 透视：越上越远
        private static readonly float[] TierAlpha = { 1.00f, 0.96f, 0.92f, 0.88f };
        private static readonly float[] TierYOff = { 0f, -6f, -12f, -18f };          // 像素上移

        private static readonly float[] ShelfAX = { 0.030f, 0.082f, 0.133f, 0.185f };
        private static readonly float[] ShelfBX = { 0.815f, 0.867f, 0.918f, 0.970f };

        private static readonly Vector2 CounterL = new Vector2(0.376f, 0.176f);
        private static readonly Vector2 CounterM = new Vector2(0.496f, 0.145f);
        private static readonly Vector2 CounterR = new Vector2(0.605f, 0.176f);

        private const float SlotW = 0.042f;   // 单格宽（占窗口比例）
        private const float SlotH = 0.085f;   // 单格高

        private static GameObject _root;
        private static RectTransform _stage;
        private static RectTransform _drawer;
        private static TMP_FontAsset _font;
        private static bool _drawerOpen;
        private static string _bgKey = "bg_galaxy";   // 可换：bg_galaxy / 以后的 bg_teyvat / bg_train / bg_sixth

        public static bool IsOpen { get { return _root != null && _root.activeSelf; } }

        public static void Open()
        {
            if (IsOpen) { Close(); return; }
            try
            {
                if (_root == null) Build();
                _root.SetActive(true);
                DrawStage();
                try { ItemSelectHandler.current?.Disable(); } catch { }
                try { IMultiSelectOff(); } catch { }
                Debug.Log(FigurineMod.LogTag + "展示页已打开");
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "展示页异常：" + e.GetType().Name + " | " + e.Message); }
        }

        public static void Close()
        {
            if (_root != null) _root.SetActive(false);
            try { ItemSelectHandler.current?.Enable(); } catch { }
        }

        /// <summary>把多选拖拽也停掉（拿不到实例就跳过）。</summary>
        private static void IMultiSelectOff()
        {
            try
            {
                var h = ItemMultiSelectHandler.current;
                if (h != null) h.CustomCancel(false);
            }
            catch { }
        }

        private static void Build()
        {
            _root = new GameObject("ShowPageCanvas");
            UnityEngine.Object.DontDestroyOnLoad(_root);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;   // 盖在游戏 UI 之上
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _root.AddComponent<GraphicRaycaster>();
            _font = Font();

            // 遮罩
            var dim = NewRect("Dim", _root.transform);
            Stretch(dim, 0, 0, 0, 0);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.6f);

            // 窗口（保持素材比例 1672:941）
            var win = NewRect("Window", _root.transform);
            win.anchorMin = Vector2.zero;
            win.anchorMax = Vector2.one;
            win.offsetMin = Vector2.zero;
            win.offsetMax = Vector2.zero;
            var winImg = win.gameObject.AddComponent<Image>();
            winImg.color = new Color(0.06f, 0.07f, 0.09f, 1f);

            // 关闭按钮
            var close = NewRect("Close", win);
            close.anchorMin = close.anchorMax = new Vector2(1f, 1f);
            close.anchoredPosition = new Vector2(-48f, -48f);
            close.sizeDelta = new Vector2(56, 56);
            close.SetAsLastSibling();   // 舞台是后建的，会把 × 盖住 → 提到最上层
            var closeImg = close.gameObject.AddComponent<Image>();
            closeImg.color = new Color(0.45f, 0.16f, 0.16f, 0.95f);
            var closeBtn = close.gameObject.AddComponent<Button>();
            closeBtn.onClick.AddListener(new UnityEngine.Events.UnityAction(Close));
            var closeText = NewRect("X", close);
            Stretch(closeText, 0, 0, 0, 0);
            var ct = closeText.gameObject.AddComponent<TextMeshProUGUI>();
            ct.font = _font; ct.fontSize = 22; ct.color = Color.white; ct.text = "×";
            ct.alignment = TextAlignmentOptions.Center;

            // 舞台（背景 + 手办位）
            _stage = NewRect("Stage", win);
            Stretch(_stage, 0, 0, 0, 0);   // 铺满整屏；把手是浮在上面的

            // 抽屉（收起状态只留把手）
            var handle = NewRect("Handle", win);
            handle.anchorMin = handle.anchorMax = new Vector2(0.5f, 0f);
            handle.anchoredPosition = new Vector2(0f, 20f);
            handle.sizeDelta = new Vector2(420, 36);
            var hImg = handle.gameObject.AddComponent<Image>();
            hImg.color = new Color(0.14f, 0.17f, 0.20f, 1f);
            var hBtn = handle.gameObject.AddComponent<Button>();
            hBtn.onClick.AddListener(new UnityEngine.Events.UnityAction(ToggleDrawer));
            var hText = NewRect("T", handle);
            Stretch(hText, 0, 0, 0, 0);
            var ht = hText.gameObject.AddComponent<TextMeshProUGUI>();
            ht.font = _font; ht.fontSize = 18; ht.color = new Color(0.8f, 0.86f, 0.92f);
            ht.text = "▲ 手办（点开）"; ht.alignment = TextAlignmentOptions.Center;

            _drawer = NewRect("Drawer", win);
            _drawer.anchorMin = _drawer.anchorMax = new Vector2(0.5f, 0f);
            _drawer.anchoredPosition = new Vector2(0f, 78f);
            _drawer.sizeDelta = new Vector2(1560, 120);
            _drawer.pivot = new Vector2(0.5f, 0f);
            var dImg = _drawer.gameObject.AddComponent<Image>();
            dImg.color = new Color(0.07f, 0.08f, 0.10f, 1f);
            var dText = NewRect("Hint", _drawer);
            Stretch(dText, 0, 0, 0, 0);
            var dt = dText.gameObject.AddComponent<TextMeshProUGUI>();
            dt.font = _font; dt.fontSize = 18; dt.color = new Color(0.55f, 0.6f, 0.66f);
            dt.text = "抽屉：展示架 16+16 / 柜台 3 / 背包手办（待接容器）";
            dt.alignment = TextAlignmentOptions.Center;
            _drawer.gameObject.SetActive(false);

            // 最上层挡板：吞掉所有点击/拖拽，避免「穿图」操作到背后的游戏
            var blocker = NewRect("Blocker", _root.transform);
            Stretch(blocker, 0, 0, 0, 0);
            var bImg = blocker.gameObject.AddComponent<Image>();
            bImg.color = new Color(0f, 0f, 0f, 0f);
            bImg.raycastTarget = true;
            blocker.SetSiblingIndex(0);   // 挡板放最底层
            close.SetAsLastSibling();     // 舞台/把手/抽屉都是后建的，最后再把 × 提上来
        }

        /// <summary>画背景 + 35 个手办位。</summary>
        private static void DrawStage()
        {
            for (int i = _stage.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_stage.GetChild(i).gameObject);

            // 背景图（铺满舞台，保持比例用 preserveAspect）
            var bg = NewRect("BG", _stage);
            Stretch(bg, 0, 0, 0, 0);
            var bgImg = bg.gameObject.AddComponent<Image>();
            var sp = Sprite("show_stage");
            if (sp != null) { bgImg.sprite = sp; bgImg.preserveAspect = false; bgImg.color = Color.white; }   // 拉伸铺满，不留黑边
            else { bgImg.color = new Color(0.12f, 0.16f, 0.22f, 1f); }
            bgImg.raycastTarget = false;

            // 窗外背景：**先画满屏**，房间图盖在上面，靠素材自带的透明窗洞露出来 → 天然对齐
            var sky = NewRect("Sky", _stage);
            // 窗洞：按素材实测 786~788 × 402~405 像素 → 归一化锚点
            sky.anchorMin = new Vector2(0.2632f, 0.3114f);
            sky.anchorMax = new Vector2(0.7333f, 0.7386f);
            sky.offsetMin = Vector2.zero;
            sky.offsetMax = Vector2.zero;
            var skyImg = sky.gameObject.AddComponent<Image>();
            var bsp = Sprite(_bgKey);
            if (bsp != null) { skyImg.sprite = bsp; skyImg.preserveAspect = false; }
            else skyImg.color = new Color(0.03f, 0.05f, 0.10f, 1f);
            skyImg.raycastTarget = false;
            sky.SetSiblingIndex(0);   // 天空必须在房间图**下面**，否则会把整个房间盖住

            // 架 A / 架 B
            for (int t = 0; t < 4; t++)
            {
                for (int c = 0; c < 4; c++)
                {
                    Slot(string.Format("A{0}{1}", t, c), ShelfAX[c], TierY[t], t);
                    Slot(string.Format("B{0}{1}", t, c), ShelfBX[c], TierY[t], t);
                }
            }
            // 三柜台
            Slot("CL", CounterL.x, CounterL.y, 0);
            Slot("CM", CounterM.x, CounterM.y, 0);
            Slot("CR", CounterR.x, CounterR.y, 0);
        }

        /// <summary>
        /// 手办位：**不可见**，只是给手办贴图当锚点用（效果页不该出现格子）。
        /// 空位不画任何东西；以后要提示可以在这里加一个极淡的轮廓，默认不要。
        /// </summary>
        private static void Slot(string name, float x, float y, int tier)
        {
            var rt = NewRect(name, _stage);
            rt.anchorMin = rt.anchorMax = new Vector2(x, y);
            rt.sizeDelta = new Vector2(1600 * SlotW * TierScale[tier], 900 * SlotH * TierScale[tier]);
            rt.anchoredPosition = new Vector2(0f, TierYOff[tier]);
            // 不挂 Image → 完全看不见；手办贴图以后作为它的子物体挂上来
        }

        private static void ToggleDrawer()
        {
            _drawerOpen = !_drawerOpen;
            if (_drawer != null) _drawer.gameObject.SetActive(_drawerOpen);
        }

        private static Sprite Sprite(string key)
        {
            try
            {
                var mods = ModLoader.Mods;
                if (mods == null) return null;
                foreach (var m in mods)
                {
                    if (m == null || m.Manifest == null || m.Manifest.ID != "figurine.gs") continue;
                    Sprite sp = null;
                    if (m.Sprites != null && m.Sprites.TryGetValue(key, out sp) && sp != null) return sp;
                    Texture2D tex = null;
                    if (m.Textures != null && m.Textures.TryGetValue(key, out tex) && tex != null)
                        return UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }
            catch (System.Exception __ex) { Safe.Warn("ShowPage.Sprite", __ex); }
            return null;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        private static void Stretch(RectTransform rt, float l, float b, float r, float t)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        private static void Anchor(RectTransform rt, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        /// <summary>中文必须走游戏的 FontLoader（Resources 里随便抓的字体没有 CJK 字形，会显示成方块）。</summary>
        private static TMP_FontAsset Font()
        {
            if (_font != null) return _font;
            try
            {
                var fl = FontLoader.Instance;
                if (fl != null)
                {
                    if (fl.simplifiedChineseFont != null) { _font = fl.simplifiedChineseFont; return _font; }
                    var locale = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale;
                    var f = fl.GetFontByRole(FontLoader.FontRole.Noto_Sans, locale);
                    if (f != null) { _font = f; return f; }
                }
            }
            catch (System.Exception __ex) { Safe.Warn("ShowPage.Font", __ex); }
            try { _font = TMP_Settings.defaultFontAsset; } catch { }
            return _font;
        }
    }
}
