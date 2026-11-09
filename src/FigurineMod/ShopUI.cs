using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FigurinePort
{
    /// <summary>
    /// 手办商店（自建 Canvas，淘宝式卡片网格）。
    /// ```
    /// F7 开关；5 列卡片：立绘 / 名字 / 价格 / 星级；点卡片购买。
    /// ```
    /// </summary>
    internal static class ShopUI
    {
        private static TMP_FontAsset _cachedFont;
        private static Catalog.Def[] _items = Catalog.All;
        private static GameObject _entryCanvas;
        private static TextMeshProUGUI _entryHint;
        private static GameObject _canvas;
        private static TextMeshProUGUI _cashText;
        private static TextMeshProUGUI _statusText;
        private static CashFlash _cashFlash;
        private static GameObject _confirmRoot;
        private static readonly Dictionary<string, int> _cart = new Dictionary<string, int>();
        private static readonly Dictionary<string, TextMeshProUGUI> _qtyTexts = new Dictionary<string, TextMeshProUGUI>();
        private static readonly Dictionary<string, GameObject[]> _qtyCtrls = new Dictionary<string, GameObject[]>();
        private static TextMeshProUGUI _cartText;
        private static TextMeshProUGUI _cartBtnText;
        private static GameObject _cartRoot;

        public static bool IsOpen { get { return _canvas != null && _canvas.activeSelf; } }

        public static void Toggle()
        {
            if (IsOpen) { Close(); return; }
            if (_entryCanvas != null && _entryCanvas.activeSelf) { CloseEntry(); return; }
            ShowEntry();
        }

        private static void CloseEntry()
        {
            if (_entryCanvas != null) UnityEngine.Object.Destroy(_entryCanvas);
            _entryCanvas = null;
            _entryHint = null;
        }

        private static void ShowEntry()
        {
            try
            {
                if (_entryCanvas == null)
                {
                    _entryCanvas = new GameObject("ShopEntryCanvas");
                    UnityEngine.Object.DontDestroyOnLoad(_entryCanvas);
                    var canvas = _entryCanvas.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 480;
                    var scaler = _entryCanvas.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920, 1080);
                    scaler.matchWidthOrHeight = 0.5f;
                    _entryCanvas.AddComponent<GraphicRaycaster>();
                }
                DrawEntry(0);
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "商店入口异常：" + e.GetType().Name + " | " + e.Message); }
        }

        private static void DrawEntry(int page)
        {
            try
            {
                var font = Font();
                var tr = _entryCanvas.transform;
                for (int i = tr.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(tr.GetChild(i).gameObject);

                var bg = NewRect("Bg", tr);
                Stretch(bg, 0, 0, 0, 0);
                var bgImg = bg.gameObject.AddComponent<Image>();
                bgImg.color = new Color(0.05f, 0.06f, 0.10f, 0.97f);

                string titleText = page == 0 ? "选择商店" : page == 1 ? "选择游戏" : "商店";
                AddCloseButton(tr, () => CloseEntry(), 40f);

                if (page != 0)
                {
                    var title = NewRect("Title", tr);
                    Anchor(title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 460));
                    title.sizeDelta = new Vector2(600, 60);
                    AddText(title, titleText, 42, TextAlignmentOptions.Center, font);
                }

                var hint = NewRect("Hint", tr);
                Anchor(hint, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -230));
                hint.sizeDelta = new Vector2(900, 36);
                _entryHint = AddText(hint, "", 22, TextAlignmentOptions.Center, font);

                if (page == 0)
                {
                    AddCard(tr, "商店", -260, 60, font, () => DrawEntry(1), "icon_shop", true);
                    AddCard(tr, "交易", 0, 60, font, () => DrawTradePage(), "icon_trade", true);
                    AddCard(tr, "仓库", 260, 60, font, () => DrawWarehousePage(), "icon_warehouse", true);

                    AddBack(tr, font, () => CloseEntry());
                }
                else if (page == 1)
                {
                    AddCard(tr, "原神", -280, 60, font, () => DrawEntry(2), "logo_genshin");
                    AddCard(tr, "星穹铁道", 0, 60, font, () => DrawEntry(3), "logo_starrail");
                    AddCard(tr, "绝区零", 280, 60, font, () => DrawEntry(4), "logo_zzz");
                    AddBack(tr, font, () => DrawEntry(0));
                }
                else if (page == 2)
                {
                    AddCard(tr, "手办", -230, 60, font, () => { CloseEntry(); OpenWith(Catalog.All); });
                    AddCard(tr, "抽奖", 230, 60, font, () => DrawGachaPage("gs"));
                    AddBack(tr, font, () => DrawEntry(1));
                }
                else if (page == 3)
                {
                    AddCard(tr, "手办", -230, 60, font, () => { CloseEntry(); OpenWith(StarRailData.ToCatalog()); });
                    AddCard(tr, "抽奖", 230, 60, font, () => DrawGachaPage("sr"));
                    AddBack(tr, font, () => DrawEntry(1));
                }
                else
                {
                    AddCard(tr, "手办", -230, 60, font, () => { CloseEntry(); OpenWith(ZzzData.ToCatalog()); });
                    AddCard(tr, "抽奖", 230, 60, font, () => DrawGachaPage("zzz"));
                    AddBack(tr, font, () => DrawEntry(1));
                }
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "入口页异常：" + e.GetType().Name); }
        }

        private static Sprite ModSprite(string key)
        {
            try
            {
                var mods = ModLoader.Mods;
                if (mods == null) return null;
                foreach (var m in mods)
                {
                    if (m == null || m.Manifest == null || m.Manifest.ID != "figurine.gs") continue;
                    Sprite sp = null;
                    try { if (m.Sprites != null && m.Sprites.TryGetValue(key, out sp) && sp != null) return sp; } catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:146", __ex); }
                    if (m.Textures != null && m.Textures.TryGetValue(key, out var tex) && tex != null)
                        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }
            catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:151", __ex); }
            return null;
        }

        private static void AddBack(Transform tr, TMP_FontAsset font, Action act)
        {
            var back = NewRect("Back", tr);
            Anchor(back, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -470));
            back.sizeDelta = new Vector2(180, 46);
            var bi = back.gameObject.AddComponent<Image>();
            bi.sprite = RoundedSprite.Pixel;
            bi.type = Image.Type.Sliced;
            bi.color = new Color(0.16f, 0.19f, 0.28f, 1f);
            var bb = back.gameObject.AddComponent<Button>();
            bb.onClick.AddListener(new UnityEngine.Events.UnityAction(() => { try { act(); } catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:165", __ex); } }));
            var bt = NewRect("t", back); Stretch(bt, 0, 0, 0, 0);
            AddText(bt, "返回", 22, TextAlignmentOptions.Center, font);
        }

        private static string _gachaResult = "";

        /// <summary>
        /// 页面右上角的 × —— 所有页都要有（用户 2026-11-03 要求：每页都要「返回」和「×」）。
        /// `act` 传的是「这一页的 × 要做什么」，一般是整层关掉。
        /// </summary>
        /// <summary>
        /// 统一的关闭按钮：<b>46×46、× 30px、红色底</b>，贴父物体右上角内缩 <paramref name="inset"/>。
        /// 页面级一律 inset = 40（从右上角内缩 40）——以前商店页是 44×44@-50、其它页 46×46@-40，
        /// 高度对不齐，现在全部走这里。
        /// </summary>
        private static void AddCloseButton(Transform parent, Action act, float inset)
        {
            try
            {
                var close = NewRect("Close", parent);
                // 轴心放到右上角，再按 inset 往内缩，按钮就完全落在面板内
                close.pivot = new Vector2(1f, 1f);
                Anchor(close, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-inset, -inset));
                close.sizeDelta = new Vector2(46, 46);
                var img = close.gameObject.AddComponent<Image>();
                img.color = new Color(0.55f, 0.18f, 0.20f, 1f);
                var btn = close.gameObject.AddComponent<Button>();
                btn.onClick.AddListener(new UnityEngine.Events.UnityAction(() => { try { act(); } catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:193", __ex); } }));
                var t = NewRect("x", close);
                Stretch(t, 0, 0, 0, 0);
                AddText(t, "×", 30, TextAlignmentOptions.Center, Font());
            }
            catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:198", __ex); }
        }

        private static void AddPageClose(Transform tr, Action act)
        {
            AddCloseButton(tr, act, 40f);
        }

        private static void DrawGachaPage(string series)
        {
            try
            {
                var font = Font();
                var tr = _entryCanvas.transform;
                for (int i = tr.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(tr.GetChild(i).gameObject);

                var bg = NewRect("Bg", tr);
                Stretch(bg, 0, 0, 0, 0);
                var bgImg = bg.gameObject.AddComponent<Image>();
                bgImg.color = new Color(0.05f, 0.06f, 0.10f, 0.97f);

                var title = NewRect("Title", tr);
                Anchor(title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 460));
                title.sizeDelta = new Vector2(600, 56);
                AddText(title, "抽奖 · " + SeriesName(series), 40, TextAlignmentOptions.Center, font);
                AddPageClose(tr, () => CloseEntry());

                AddPool(tr, -380, font, "常驻池", 200, false, series);
                AddPool(tr, 380, font, "UP 池", 400, true, series);

                var res = NewRect("Result", tr);
                Anchor(res, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -300));
                res.sizeDelta = new Vector2(1100, 60);
                AddText(res, _gachaResult, 22, TextAlignmentOptions.Center, font);

                var back = NewRect("Back", tr);
                Anchor(back, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -470));
                back.sizeDelta = new Vector2(180, 46);
                var bi = back.gameObject.AddComponent<Image>(); bi.color = new Color(0.16f, 0.19f, 0.28f, 1f);
                var bb = back.gameObject.AddComponent<Button>();
                bb.onClick.AddListener(new UnityEngine.Events.UnityAction(() => DrawEntry(0)));
                var bt = NewRect("t", back); Stretch(bt, 0, 0, 0, 0);
                AddText(bt, "返回", 22, TextAlignmentOptions.Center, font);
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "抽奖页异常：" + e.GetType().Name); }
        }

        /// <summary>系列显示名（gs / sr / zzz）。</summary>
        private static string SeriesName(string series)
        {
            if (series == "sr") return "星穹铁道";
            if (series == "zzz") return "绝区零";
            return "原神";
        }

        private static string _whResult = "";

        // ───────────────────────── 交易板 ─────────────────────────

        private static string _tradeResult = "";

        /// <summary>
        /// 交易页：每天 1~20 张卡，买/卖两种，价格原价 ±0~10%。
        /// 布局 7 列 × 3 行（最多 21 张），每张 240×240，间距 20，不与顶栏/返回键重叠。
        /// </summary>
        private static void DrawTradePage()
        {
            try
            {
                var font = Font();
                var tr = _entryCanvas.transform;
                for (int i = tr.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(tr.GetChild(i).gameObject);

                var bg = NewRect("Bg", tr);
                Stretch(bg, 0, 0, 0, 0);
                var bgImg = bg.gameObject.AddComponent<Image>();
                bgImg.color = new Color(0.05f, 0.06f, 0.10f, 0.97f);

                var title = NewRect("Title", tr);
                Anchor(title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 460));
                title.sizeDelta = new Vector2(900, 56);
                AddText(title, "交易 · 第 " + TradeBoard.Today + " 天", 40, TextAlignmentOptions.Center, font);
                AddPageClose(tr, () => CloseEntry());

                // 返回键放左上角，避免和底部提示/卡片重叠
                var back = NewRect("Back", tr);
                Anchor(back, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(120, 430));
                back.sizeDelta = new Vector2(140, 46);
                var bi = back.gameObject.AddComponent<Image>(); bi.color = new Color(0.16f, 0.19f, 0.28f, 1f);
                var bb = back.gameObject.AddComponent<Button>();
                bb.onClick.AddListener(new UnityEngine.Events.UnityAction(() => DrawEntry(0)));
                var bt = NewRect("t", back); Stretch(bt, 0, 0, 0, 0);
                AddText(bt, "返回", 22, TextAlignmentOptions.Center, font);

                var list = TradeBoard.Entries;
                // 每天最多 30 张 → 6 列 × 5 行 = 30 格，卡片 150×150（间距 12）
                // 网格 960×798，竖向中心落在 y≈-10，上不压标题(460)、下不压提示行(-470)
                for (int i = 0; i < list.Count && i < 30; i++)
                {
                    int col = i % 6, row = i / 6;
                    float x = -405f + col * 162f;
                    float y = 314f - row * 162f;
                    var e = list[i];

                    var card = NewRect("T" + i, tr);
                    Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y));
                    card.sizeDelta = new Vector2(150, 150);
                    var cb = card.gameObject.AddComponent<Image>();
                    cb.sprite = RoundedSprite.Gradient;
                    cb.type = Image.Type.Sliced;
                    cb.color = e.Done
                        ? new Color(0.20f, 0.21f, 0.24f, 0.95f)
                        : (e.PlayerBuys ? new Color(0.24f, 0.34f, 0.46f, 0.98f) : new Color(0.42f, 0.32f, 0.22f, 0.98f));

                    var ico = NewRect("I", card);
                    Anchor(ico, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -46));
                    ico.sizeDelta = new Vector2(68, 68);
                    var ii = ico.gameObject.AddComponent<Image>();
                    try { ii.sprite = CardArt.Get(e.Def.Id); } catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:316", __ex); }
                    if (ii.sprite == null) { try { ii.sprite = ModSprite(e.Def.Id); } catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:317", __ex); } }
                    ii.preserveAspect = true;
                    if (e.Done) ii.color = new Color(1f, 1f, 1f, 0.35f);

                    var nm = NewRect("N", card);
                    Anchor(nm, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 56));
                    nm.sizeDelta = new Vector2(144, 20);
                    AddText(nm, e.Def.Zh, 14, TextAlignmentOptions.Center, font);

                    var tag = NewRect("Tg", card);
                    Anchor(tag, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 38));
                    tag.sizeDelta = new Vector2(144, 18);
                    AddText(tag, e.PlayerBuys ? "可买入" : "可卖出", 12, TextAlignmentOptions.Center, font);

                    var pr = NewRect("P", card);
                    Anchor(pr, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 18));
                    pr.sizeDelta = new Vector2(144, 20);
                    AddText(pr, (e.PlayerBuys ? "-" : "+") + e.Price + " ₡", 14, TextAlignmentOptions.Center, font);

                    int idx = i;
                    var btn = card.gameObject.AddComponent<Button>();
                    btn.onClick.AddListener(new UnityEngine.Events.UnityAction(() =>
                    {
                        if (!TradeBoard.HasTerminal()) { _tradeResult = "需要先持有 DORO 终端机"; DrawTradePage(); return; }
                        _tradeResult = TradeBoard.Transact(idx);
                        DrawTradePage();
                    }));
                }

                var res = NewRect("Res", tr);
                Anchor(res, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -470));
                res.sizeDelta = new Vector2(1200, 36);
                AddText(res, _tradeResult, 22, TextAlignmentOptions.Center, font);
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "交易页异常：" + e.GetType().Name); }
        }

        /// <summary>购物车窗口底部那行提示（结算失败原因等）。</summary>
        private static string _cartResult = "";

        private static void DrawWarehousePage()
        {
            try
            {
                var font = Font();
                var tr = _entryCanvas.transform;
                for (int i = tr.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(tr.GetChild(i).gameObject);

                var bg = NewRect("Bg", tr);
                Stretch(bg, 0, 0, 0, 0);
                var bgImg = bg.gameObject.AddComponent<Image>();
                bgImg.color = new Color(0.05f, 0.06f, 0.10f, 0.97f);

                var title = NewRect("Title", tr);
                Anchor(title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 460));
                title.sizeDelta = new Vector2(600, 56);
                AddText(title, "仓库  " + Warehouse.Used + "/" + Warehouse.Capacity, 40, TextAlignmentOptions.Center, font);
                AddPageClose(tr, () => CloseEntry());

                var snap = Warehouse.Snapshot();
                int shown = 0;
                foreach (var kv in snap)
                {
                    if (shown >= 8) break;
                    // 原神 + 星铁合表查（以前只查 Catalog.All，星铁 ID 查不到就退回 Catalog.All[0]）
                    var def = Defs.Find(kv.Key);
                    if (string.IsNullOrEmpty(def.Id)) continue;
                    string id = kv.Key;

                    var row = NewRect("Row", tr);
                    Anchor(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 220 - shown * 70));
                    row.sizeDelta = new Vector2(760, 62);
                    var ri = row.gameObject.AddComponent<Image>(); ri.color = new Color(0.13f, 0.16f, 0.24f, 1f);

                    var icon = NewRect("Ico", row);
                    Anchor(icon, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(36, 0));
                    icon.sizeDelta = new Vector2(44, 44);
                    var ic = icon.gameObject.AddComponent<Image>();
                    try { ic.sprite = IconSprite(id); } catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:395", __ex); }
                    if (ic.sprite == null) ic.color = new Color(1f, 1f, 1f, 0f);   // 没图就别留白块
                    ic.preserveAspect = true;

                    var nm = NewRect("N", row);
                    Anchor(nm, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(200, 0));
                    nm.sizeDelta = new Vector2(300, 36);
                    AddText(nm, def.Zh + "  ×" + kv.Value, 24, TextAlignmentOptions.Left, font);

                    AddButton(row, 195, 0, 100, 44, "取出 1", () => { Warehouse.Take(id, 1); _whResult = "已取出 1 件"; DrawWarehousePage(); }, font);
                    AddButton(row, 312, 0, 116, 44, "全部取出", () => { int n = Warehouse.Take(id, 999); _whResult = "已取出 " + n + " 件"; DrawWarehousePage(); }, font);
                    shown++;
                }

                if (shown == 0)
                {
                    var empty = NewRect("Empty", tr);
                    Anchor(empty, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
                    empty.sizeDelta = new Vector2(400, 40);
                    AddText(empty, "仓库是空的", 24, TextAlignmentOptions.Center, font);
                }

                var res = NewRect("R", tr);
                Anchor(res, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -320));
                res.sizeDelta = new Vector2(900, 36);
                AddText(res, _whResult, 22, TextAlignmentOptions.Center, font);

                var back = NewRect("Back", tr);
                Anchor(back, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -470));
                back.sizeDelta = new Vector2(180, 46);
                var bi = back.gameObject.AddComponent<Image>(); bi.color = new Color(0.16f, 0.19f, 0.28f, 1f);
                var bb = back.gameObject.AddComponent<Button>();
                bb.onClick.AddListener(new UnityEngine.Events.UnityAction(() => DrawEntry(0)));
                var bt = NewRect("t", back); Stretch(bt, 0, 0, 0, 0);
                AddText(bt, "返回", 22, TextAlignmentOptions.Center, font);
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "仓库页异常：" + e.GetType().Name); }
        }

        private static void AddPool(Transform tr, float x, TMP_FontAsset font,
                                    string poolName, int price, bool rare, string series)
        {
            var panel = NewRect("Pool", tr);
            Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 40));
            panel.sizeDelta = new Vector2(420, 460);
            var pi = panel.gameObject.AddComponent<Image>();
            pi.sprite = RoundedSprite.Gradient;
            pi.type = Image.Type.Sliced;
            pi.color = Color.white;

            var t1 = NewRect("N", panel);
            Anchor(t1, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40));
            t1.sizeDelta = new Vector2(380, 44);
            AddText(t1, poolName, 30, TextAlignmentOptions.Center, font);

            // 不再有抽奖券：直接扣信用点
            var t2 = NewRect("T", panel);
            Anchor(t2, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -92));
            t2.sizeDelta = new Vector2(380, 30);
            AddText(t2, $"单抽 {price} ₡ · 十连 {price * 10} ₡", 22, TextAlignmentOptions.Center, font);

            AddButton(panel, -100, -220, 170, 56, $"单抽 {price}", () => DoDraw(series, rare, 1), font);
            AddButton(panel, 100, -220, 170, 56, $"十连 {price * 10}", () => DoDraw(series, rare, 10), font);
        }

        private static void AddButton(Transform parent, float x, float y, float w, float h, string label, Action act, TMP_FontAsset font)
        {
            var b = NewRect("Btn", parent);
            Anchor(b, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y));
            b.sizeDelta = new Vector2(w, h);
            var bi = b.gameObject.AddComponent<Image>();
            bi.sprite = RoundedSprite.Pixel;
            bi.type = Image.Type.Sliced;
            bi.color = new Color(0.16f, 0.30f, 0.24f, 1f);
            var bb = b.gameObject.AddComponent<Button>();
            bb.onClick.AddListener(new UnityEngine.Events.UnityAction(() => { try { act(); } catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:470", __ex); } }));
            var bt = NewRect("t", b); Stretch(bt, 0, 0, 0, 0);
            AddText(bt, label, 22, TextAlignmentOptions.Center, font);
        }

        /// <summary>
        /// 抽奖：**不再有抽奖券，直接扣信用点**（用户 2026-11-03 要求）。
        /// 常驻池 200 / UP 池 400（每次）。
        /// </summary>
        private static void DoDraw(string series, bool rare, int n)
        {
            int price = rare ? 400 : 200;

            // 先查仓库容量：放不下就不扣钱
            int freeSlots = Warehouse.Capacity - Warehouse.Used;
            if (freeSlots < n)
            {
                _gachaResult = $"仓库放不下：仓库 {Warehouse.Used}/{Warehouse.Capacity}，还能放 {freeSlots} 件，这次要抽 {n} 件";
                DrawGachaPage(series);
                return;
            }

            var ps = PlayerStore.Instance;
            if (ps == null) { _gachaResult = "店内上下文缺失"; DrawGachaPage(series); return; }

            int cost = n * price;
            if (!ps.CanPay(cost)) { _gachaResult = $"现金不足：这次要 {cost} ₡，持有 {ps.GetCash()} ₡"; DrawGachaPage(series); return; }

            var err = Gacha.Draw(series, rare, n, out var names);
            if (!string.IsNullOrEmpty(err)) { _gachaResult = err; DrawGachaPage(series); return; }

            ps.PayMoney(cost, false);
            _gachaResult = $"获得：{string.Join("、", names.ToArray())}（-{cost} ₡）";
            DrawGachaPage(series);
        }

        private static void AddCard(Transform parent, string name, float x, float y, TMP_FontAsset font, Action onClick, string iconKey = null, bool withCaption = false)
        {
            var card = NewRect("Shop_" + name, parent);
            Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y));
            card.sizeDelta = new Vector2(200, 200);
            var spNow = iconKey != null ? ModSprite(iconKey) : null;

            // withCaption = 入口页那三张（商店/交易/仓库）：整卡吃射线 + 图标带名字 + 悬停反馈。
            // 其余（选择游戏的三张 logo 卡）保持原样：logo 铺满整张卡、无文字、无悬停。
            if (withCaption)
            {
                var bg = card.gameObject.AddComponent<Image>();
                if (spNow == null)
                {
                    bg.sprite = RoundedSprite.Gradient;
                    bg.type = Image.Type.Sliced;
                    bg.color = Color.white;
                }
                else
                {
                    bg.color = new Color(1f, 1f, 1f, 0f);   // 全透明，只用来接收点击
                    bg.raycastTarget = true;
                }
                var btn = card.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(new UnityEngine.Events.UnityAction(() => { try { onClick(); } catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:531", __ex); } }));

                RectTransform icoRt = null;
                TextMeshProUGUI lblText = null;
                if (spNow != null)
                {
                    var ico = NewRect("I", card);
                    Anchor(ico, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 26));
                    ico.sizeDelta = new Vector2(150, 150);
                    var ii = ico.gameObject.AddComponent<Image>();
                    ii.sprite = spNow;
                    ii.preserveAspect = true;
                    ii.raycastTarget = false;
                    icoRt = ico;
                }
                var cap = NewRect("C", card);
                Anchor(cap, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, spNow != null ? -72 : 0));
                cap.sizeDelta = new Vector2(320, spNow != null ? 44 : 50);
                lblText = AddText(cap, name, spNow != null ? 30 : 34, TextAlignmentOptions.Center, font);
                try { lblText.raycastTarget = false; } catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:550", __ex); }

                try
                {
                    var hv = card.gameObject.AddComponent<HoverCard>();
                    hv.icon = icoRt;
                    hv.label = lblText;
                }
                catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:558", __ex); }
                return;
            }

            // —— 原版逻辑：图标铺满或纯文字，无悬停 ——
            if (spNow == null)
            {
                var ci = card.gameObject.AddComponent<Image>();
                ci.sprite = RoundedSprite.Gradient;
                ci.type = Image.Type.Sliced;
                ci.color = Color.white;
            }
            var btn2 = card.gameObject.AddComponent<Button>();
            btn2.onClick.AddListener(new UnityEngine.Events.UnityAction(() => { try { onClick(); } catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:571", __ex); } }));
            if (spNow != null)
            {
                var ico = NewRect("I", card);
                Anchor(ico, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
                ico.sizeDelta = new Vector2(200, 200);
                var ii = ico.gameObject.AddComponent<Image>();
                ii.sprite = spNow;
                ii.preserveAspect = true;
                // 选择游戏的三张 logo 卡：同样给悬停放大（1.07），只是不带文字变色
                try
                {
                    var hv = card.gameObject.AddComponent<HoverCard>();
                    hv.icon = ico;
                    hv.label = null;
                }
                catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:587", __ex); }
            }
            else
            {
                var lbl = NewRect("L", card);
                Anchor(lbl, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
                lbl.sizeDelta = new Vector2(320, 50);
                AddText(lbl, name, 34, TextAlignmentOptions.Center, font);
            }
        }
        public static void Close()
        {
            try { CloseCart(); CloseConfirm(); } catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:599", __ex); }
            if (_canvas != null) _canvas.SetActive(false);
        }

        private static TMP_FontAsset Font()
        {
            if (_cachedFont != null) return _cachedFont;
            try
            {
                var fl = FontLoader.Instance;
                if (fl != null)
                {
                    if (fl.simplifiedChineseFont != null) { _cachedFont = fl.simplifiedChineseFont; return _cachedFont; }
                    var locale = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale;
                    var f = fl.GetFontByRole(FontLoader.FontRole.Noto_Sans, locale);
                    if (f != null) { _cachedFont = f; return f; }
                }
            }
            catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:617", __ex); }
            try { _cachedFont = TMP_Settings.defaultFontAsset; } catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:618", __ex); }
            return _cachedFont;
        }

        public static void OpenWith(Catalog.Def[] items)
        {
            _items = items ?? Catalog.All;
            if (_canvas != null) { UnityEngine.Object.Destroy(_canvas); _canvas = null; }
            Open();
        }

        private static void Open()
        {
            try
            {
                if (_canvas == null) Build();
                Cleanup("CartWindow");
                _cartOpen = false;
                _cartRoot = null;
                _canvas.SetActive(true);
                RefreshCash();
                Debug.Log(FigurineMod.LogTag + "商店画布已打开");
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "商店打开失败：" + e); }
        }

        private static void Build()
        {
            _canvas = new GameObject("FigurineShopCanvas");
            UnityEngine.Object.DontDestroyOnLoad(_canvas);
            var canvas = _canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = _canvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _canvas.AddComponent<GraphicRaycaster>();

            var bg = NewRect("Bg", _canvas.transform);
            Stretch(bg, 0, 0, 0, 0);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0.06f, 0.07f, 0.10f, 0.97f);

            var title = NewRect("Title", bg);
            Anchor(title, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -80));
            title.sizeDelta = new Vector2(900, 60);
            AddText(title, "手办商店", 40, TextAlignmentOptions.Left);

            // 返回：回到入口页的「选择游戏」（以前手办页只有 × 关闭，没有返回）
            var backBtn = NewRect("BackButton", bg);
            Anchor(backBtn, new Vector2(0, 1), new Vector2(0, 1), new Vector2(320, -70));
            backBtn.sizeDelta = new Vector2(140, 46);
            var bbImg = backBtn.gameObject.AddComponent<Image>();
            bbImg.sprite = RoundedSprite.Pixel;
            bbImg.type = Image.Type.Sliced;
            bbImg.color = new Color(0.16f, 0.19f, 0.28f, 0.95f);
            var bbBtn = backBtn.gameObject.AddComponent<Button>();
            bbBtn.onClick.AddListener(new UnityEngine.Events.UnityAction(() =>
            {
                try { Close(); } catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:678", __ex); }
                try { ShowEntry(); DrawEntry(1); } catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:679", __ex); }
            }));
            var bbTxt = NewRect("t", backBtn); Stretch(bbTxt, 0, 0, 0, 0);
            AddText(bbTxt, "返回", 22, TextAlignmentOptions.Center);

            var cartBtn = NewRect("CartButton", bg);
            Anchor(cartBtn, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-470, -70));
            cartBtn.sizeDelta = new Vector2(210, 46);
            var cbImg = cartBtn.gameObject.AddComponent<Image>();
            cbImg.sprite = RoundedSprite.Pixel;
            cbImg.type = Image.Type.Sliced;
            cbImg.color = new Color(0.12f, 0.15f, 0.22f, 0.95f);
            var cbBtn = cartBtn.gameObject.AddComponent<Button>();
            cbBtn.onClick.AddListener(new UnityEngine.Events.UnityAction(() => ToggleCart()));

            var ico = NewRect("Ico", cartBtn);
            Anchor(ico, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0));
            ico.sizeDelta = new Vector2(28, 28);
            var icoImg = ico.gameObject.AddComponent<Image>();
            icoImg.sprite = TextureSprite("cart");
            icoImg.preserveAspect = true;

            var label = NewRect("L", cartBtn);
            Anchor(label, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(96, 0));
            label.sizeDelta = new Vector2(96, 30);
            AddText(label, "购物车", 22, TextAlignmentOptions.Left);

            var badge = NewRect("Badge", cartBtn);
            Anchor(badge, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-28, 0));
            badge.sizeDelta = new Vector2(26, 26);
            var bimg = badge.gameObject.AddComponent<Image>();
            bimg.sprite = RoundedSprite.Pixel;
            bimg.type = Image.Type.Sliced;
            bimg.color = new Color(0.55f, 0.18f, 0.22f, 1f);
            var bt = NewRect("B", badge); Stretch(bt, 0, 0, 0, 0);
            _cartBtnText = AddText(bt, "0", 18, TextAlignmentOptions.Center);

            var cashBox = NewRect("CashBox", bg);
            Anchor(cashBox, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-230, -70));
            cashBox.sizeDelta = new Vector2(200, 46);
            var cashBg = cashBox.gameObject.AddComponent<Image>();
            cashBg.sprite = RoundedSprite.Pixel;
            cashBg.type = Image.Type.Sliced;
            cashBg.color = new Color(0.12f, 0.15f, 0.22f, 0.95f);
            var cash = NewRect("Cash", cashBox);
            Stretch(cash, 0, 0, 0, 0);
            _cashText = AddText(cash, "0 ₡", 26, TextAlignmentOptions.Right);
            _cashFlash = _canvas.AddComponent<CashFlash>();
            _cashFlash.Init(_cashText);

            AddCloseButton(bg, () => Close(), 40f);


            // 滚动区
            var viewport = NewRect("Viewport", bg);
            Anchor(viewport, new Vector2(0, 0), new Vector2(1, 1), Vector2.zero);
            viewport.offsetMin = new Vector2(46, 40);
            viewport.offsetMax = new Vector2(-46, -110);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = NewRect("Content", viewport);
            Anchor(content, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(320, 340);
            grid.spacing = new Vector2(22, 22);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.childAlignment = TextAnchor.UpperCenter;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = bg.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 80f;
            content.anchoredPosition = Vector2.zero;

            var font = Font();
            // 5★ 一律不上架（原神/星铁/绝区零统一）；只从抽奖、盲盒、顾客来货获得
            var sorted = new List<Catalog.Def>();
            foreach (var d in _items) if (d.Star < 5) sorted.Add(d);
            sorted.Sort((a, b) => b.Star.CompareTo(a.Star));
            foreach (var def in sorted) BuildCard(content, def, font);
        }

        private static Sprite BigSprite(string id)
        {
            try
            {
                var mods = ModLoader.Mods;
                if (mods != null)
                {
                    foreach (var m in mods)
                    {
                        if (m == null || m.Manifest == null || m.Manifest.ID != "figurine.gs") continue;
                        if (m.Textures != null && m.Textures.TryGetValue(id + "_big", out var tex) && tex != null)
                            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    }
                }
            }
            catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:782", __ex); }
            return null;
        }

        /// <summary>
        /// 卡片/列表图标的统一取图顺序：高清卡片图（Big\ 按需）→ mod 内 `<id>_big` → 48px 物品贴图。
        /// ⚠️ 三步都拿不到时必须返回 null 之外还要让调用方别把 Image 留空，
        ///    否则 Unity 的 Image 没 sprite 就是一块纯白（仓库行以前就是这样白掉的）。
        /// </summary>
        private static Sprite IconSprite(string id)
        {
            try { var s = CardArt.Get(id); if (s != null) return s; } catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:793", __ex); }
            try { var s = BigSprite(id); if (s != null) return s; } catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:794", __ex); }
            try
            {
                var mgr = CustomUIManager.Instance;
                if (mgr != null) return mgr.GetModSprite("figurine.gs", id);
            }
            catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:800", __ex); }
            return null;
        }

        /// <summary>卡片底色 = 五档稀有度颜色（Tier.cs 统一管理）。</summary>
        private static Color StarColor(int star) { return Tier.Ui(star); }

        private static void BuildCard(RectTransform parent, Catalog.Def def, TMP_FontAsset font)
        {
            var card = NewRect("Card_" + def.Id, parent);
            var bg = card.gameObject.AddComponent<Image>();
            bg.sprite = RoundedSprite.Gradient;
            bg.type = Image.Type.Sliced;
            Color starCol = StarColor(def.Star);
            bg.color = starCol;
            var btn = card.gameObject.AddComponent<Button>();
            var d = def;
            btn.onClick.AddListener(new UnityEngine.Events.UnityAction(() => AddToCart(d)));
            var hover = card.gameObject.AddComponent<CardHover>();
            hover.Init(bg, starCol, new Color(Mathf.Min(1f, starCol.r * 1.45f), Mathf.Min(1f, starCol.g * 1.45f), Mathf.Min(1f, starCol.b * 1.45f), 1f), card);

            var icon = NewRect("Icon", card);
            Anchor(icon, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -112));
            icon.sizeDelta = new Vector2(220, 150);
            var img = icon.gameObject.AddComponent<Image>();
            try
            {
                // 先试高清卡片图（Big\ 下按需加载），再退回 mod 内的高清 key，最后才是 48px 物品贴图
                img.sprite = CardArt.Get(def.Id);
                if (img.sprite == null) img.sprite = BigSprite(def.Id);
                if (img.sprite == null)
                {
                    var mgr = CustomUIManager.Instance;
                    img.sprite = mgr != null ? mgr.GetModSprite("figurine.gs", def.Id) : null;
                }
            }
            catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:836", __ex); }
            img.preserveAspect = true;

            var name = NewRect("Name", card);
            Anchor(name, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -204));
            name.sizeDelta = new Vector2(300, 44);
            var nt = AddText(name, def.Zh, 26, TextAlignmentOptions.Center, font);
            // 1★ 的卡底色是白色：按要求**白字**，靠深色描边撑对比度（不再改字色）
            bool lightCard = def.Star <= 1;
            nt.color = lightCard
                ? Color.white
                : new Color(Mathf.Min(1f, starCol.r * 1.35f), Mathf.Min(1f, starCol.g * 1.35f), Mathf.Min(1f, starCol.b * 1.35f), 1f);
            if (lightCard) AddDarkOutline(nt);

            var meta = NewRect("Meta", card);
            Anchor(meta, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -246));
            meta.sizeDelta = new Vector2(300, 36);
            var mt2 = AddText(meta, $"{def.Value} 金 · {Tier.Of(def.Star)}", 22, TextAlignmentOptions.Center, font);
            if (lightCard) { mt2.color = Color.white; AddDarkOutline(mt2); }

            var hint = NewRect("Hint", card);
            Anchor(hint, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -272));
            hint.sizeDelta = new Vector2(300, 24);
            AddText(hint, "", 18, TextAlignmentOptions.Center, font);

            // − / 数量 / + 放在同一行的中心线上（占满 meta 以下到卡片底部的空处中点）
            var minus = NewRect("Minus", card);
            Anchor(minus, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-72, -302));
            minus.sizeDelta = new Vector2(40, 40);
            var mimg = minus.gameObject.AddComponent<Image>(); mimg.color = new Color(0.18f, 0.20f, 0.28f, 1f);
            var mbtn = minus.gameObject.AddComponent<Button>();
            mbtn.onClick.AddListener(new UnityEngine.Events.UnityAction(() => ChangeQty(d.Id, -1)));
            var mt = NewRect("M", minus); Stretch(mt, 0, 0, 0, 0);
            AddText(mt, "−", 24, TextAlignmentOptions.Center, font);

            var plus = NewRect("Plus", card);
            Anchor(plus, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(72, -302));
            plus.sizeDelta = new Vector2(40, 40);
            var pimg = plus.gameObject.AddComponent<Image>(); pimg.color = new Color(0.18f, 0.20f, 0.28f, 1f);
            var pbtn = plus.gameObject.AddComponent<Button>();
            pbtn.onClick.AddListener(new UnityEngine.Events.UnityAction(() => ChangeQty(d.Id, 1)));
            var pt = NewRect("P", plus); Stretch(pt, 0, 0, 0, 0);
            AddText(pt, "+", 24, TextAlignmentOptions.Center, font);

            var qty = NewRect("Qty", card);
            Anchor(qty, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -302));
            qty.sizeDelta = new Vector2(64, 40);
            var qt = AddText(qty, "", 22, TextAlignmentOptions.Center, font);
            _qtyTexts[d.Id] = qt;
            _qtyCtrls[d.Id] = new GameObject[] { minus.gameObject, plus.gameObject };
            minus.gameObject.SetActive(false);
            plus.gameObject.SetActive(false);
        }

        private static bool _cartOpen;

        private static void ToggleCart()
        {
            if (_cartOpen) { CloseCart(); return; }
            _cartOpen = true;
            BuildCartWindow();
        }

        private static void Cleanup(string name)
        {
            try
            {
                if (_canvas == null) return;
                var tr = _canvas.transform;
                for (int i = tr.childCount - 1; i >= 0; i--)
                {
                    var c = tr.GetChild(i);
                    if (c != null && c.name == name) UnityEngine.Object.Destroy(c.gameObject);
                }
            }
            catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:911", __ex); }
        }

        private static void CloseCart()
        {
            _cartOpen = false;
            if (_cartRoot != null) { UnityEngine.Object.Destroy(_cartRoot); _cartRoot = null; }
        }

        private static void BuildCartWindow()
        {
            try
            {
                Cleanup("CartWindow");
                var font = Font();
                _cartRoot = new GameObject("CartWindow");
                _cartRoot.transform.SetParent(_canvas.transform, false);
                var root = _cartRoot.AddComponent<RectTransform>();
                Stretch(root, 0, 0, 0, 0);
                var dim = _cartRoot.AddComponent<Image>();
                dim.color = new Color(0, 0, 0, 0.65f);

                var panel = NewRect("P", _cartRoot.transform);
                Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
                panel.sizeDelta = new Vector2(720, 620);
                var pbg = panel.gameObject.AddComponent<Image>();
                pbg.color = new Color(0.09f, 0.11f, 0.17f, 1f);

                var title = NewRect("Title", _cartRoot.transform);
                Anchor(title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-180, 250));
                title.sizeDelta = new Vector2(300, 44);
                AddText(title, "购物车", 30, TextAlignmentOptions.Left, font);

                AddCloseButton(panel, () => CloseCart(), 14f);

                int total = 0, shown = 0, count = 0;
                foreach (var kv in _cart)
                {
                    count += kv.Value;
                    // 原神 + 星铁合表查（以前只查 Catalog.All，星铁 ID 查不到就退回 Catalog.All[0]）
                    var def = Defs.Find(kv.Key);
                    if (string.IsNullOrEmpty(def.Id)) continue;
                    total += def.Value * kv.Value;
                    if (shown >= 6) continue;

                    float y = 190 - shown * 68;
                    var row = NewRect("Row" + shown, _cartRoot.transform);
                    Anchor(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, y));
                    row.sizeDelta = new Vector2(680, 62);
                    var rowBg = row.gameObject.AddComponent<Image>();
                    rowBg.color = new Color(0.13f, 0.16f, 0.24f, 1f);

                    string id = def.Id;
                    var icon = NewRect("Icon", row);
                    Anchor(icon, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0));
                    icon.sizeDelta = new Vector2(44, 44);
                    var ic = icon.gameObject.AddComponent<Image>();
                    try { ic.sprite = IconSprite(def.Id); } catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:968", __ex); }
                    if (ic.sprite == null) ic.color = new Color(1f, 1f, 1f, 0f);
                    ic.preserveAspect = true;

                    var nm = NewRect("N", row);
                    Anchor(nm, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(180, 12));
                    nm.sizeDelta = new Vector2(180, 26);
                    AddText(nm, def.Zh, 22, TextAlignmentOptions.Left, font);

                    var up = NewRect("U", row);
                    Anchor(up, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(180, -14));
                    up.sizeDelta = new Vector2(180, 22);
                    AddText(up, def.Value + " ₡ / 件", 18, TextAlignmentOptions.Left, font);

                    var minus = NewRect("M", row);
                    Anchor(minus, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-280, 0));
                    minus.sizeDelta = new Vector2(34, 34);
                    var mi = minus.gameObject.AddComponent<Image>(); mi.color = new Color(0.18f, 0.21f, 0.30f, 1f);
                    var mb = minus.gameObject.AddComponent<Button>();
                    mb.onClick.AddListener(new UnityEngine.Events.UnityAction(() => ChangeQty(id, -1)));
                    var mt = NewRect("t", minus); Stretch(mt, 0, 0, 0, 0);
                    AddText(mt, "−", 22, TextAlignmentOptions.Center, font);

                    var qty = NewRect("Q", row);
                    Anchor(qty, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-235, 0));
                    qty.sizeDelta = new Vector2(46, 30);
                    AddText(qty, kv.Value.ToString(), 20, TextAlignmentOptions.Center, font);

                    var plus = NewRect("P2", row);
                    Anchor(plus, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-188, 0));
                    plus.sizeDelta = new Vector2(34, 34);
                    var pi = plus.gameObject.AddComponent<Image>(); pi.color = new Color(0.18f, 0.21f, 0.30f, 1f);
                    var pb = plus.gameObject.AddComponent<Button>();
                    pb.onClick.AddListener(new UnityEngine.Events.UnityAction(() => ChangeQty(id, 1)));
                    var pt = NewRect("t", plus); Stretch(pt, 0, 0, 0, 0);
                    AddText(pt, "+", 22, TextAlignmentOptions.Center, font);

                    var sub = NewRect("S", row);
                    Anchor(sub, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-110, 0));
                    sub.sizeDelta = new Vector2(110, 30);
                    AddText(sub, (def.Value * kv.Value) + " ₡", 20, TextAlignmentOptions.Right, font);

                    var del = NewRect("D", row);
                    Anchor(del, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-34, 0));
                    del.sizeDelta = new Vector2(34, 34);
                    var di = del.gameObject.AddComponent<Image>(); di.color = new Color(0.34f, 0.14f, 0.17f, 1f);
                    var db = del.gameObject.AddComponent<Button>();
                    db.onClick.AddListener(new UnityEngine.Events.UnityAction(() => { _cart.Remove(id); RefreshCart(); }));
                    var dt = NewRect("t", del); Stretch(dt, 0, 0, 0, 0);
                    AddText(dt, "×", 22, TextAlignmentOptions.Center, font);
                    shown++;
                }

                if (count == 0)
                {
                    var empty = NewRect("Empty", _cartRoot.transform);
                    Anchor(empty, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
                    empty.sizeDelta = new Vector2(400, 40);
                    AddText(empty, "购物车是空的", 24, TextAlignmentOptions.Center, font);
                }
                else if (count > shown)
                {
                    var more = NewRect("More", _cartRoot.transform);
                    Anchor(more, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -230));
                    more.sizeDelta = new Vector2(400, 30);
                    AddText(more, $"还有 {count - shown} 件未显示", 18, TextAlignmentOptions.Center, font);
                }

                var sum = NewRect("Sum", _cartRoot.transform);
                Anchor(sum, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-160, -265));
                sum.sizeDelta = new Vector2(300, 36);
                AddText(sum, $"合计 {total} ₡", 24, TextAlignmentOptions.Left, font);

                if (!string.IsNullOrEmpty(_cartResult))
                {
                    var res = NewRect("Res", _cartRoot.transform);
                    Anchor(res, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -200));
                    res.sizeDelta = new Vector2(640, 30);
                    AddText(res, _cartResult, 20, TextAlignmentOptions.Center, font);
                }

                var checkout = NewRect("Checkout", _cartRoot.transform);
                Anchor(checkout, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(260, -265));
                checkout.sizeDelta = new Vector2(150, 48);
                var ck = checkout.gameObject.AddComponent<Image>(); ck.color = new Color(0.14f, 0.34f, 0.24f, 1f);
                var ckb = checkout.gameObject.AddComponent<Button>();
                ckb.onClick.AddListener(new UnityEngine.Events.UnityAction(() => { Checkout(); }));
                var ckt = NewRect("t", checkout); Stretch(ckt, 0, 0, 0, 0);
                AddText(ckt, "结算", 24, TextAlignmentOptions.Center, font);

                var clear = NewRect("Clear", _cartRoot.transform);
                Anchor(clear, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(90, -265));
                clear.sizeDelta = new Vector2(120, 48);
                var cl = clear.gameObject.AddComponent<Image>(); cl.color = new Color(0.30f, 0.15f, 0.18f, 1f);
                var clb = clear.gameObject.AddComponent<Button>();
                clb.onClick.AddListener(new UnityEngine.Events.UnityAction(() => { _cart.Clear(); RefreshCart(); }));
                var clt = NewRect("t", clear); Stretch(clt, 0, 0, 0, 0);
                AddText(clt, "清空", 24, TextAlignmentOptions.Center, font);
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "购物车窗口异常：" + e.GetType().Name + " | " + e.Message); }
        }

        private static void AddToCart(Catalog.Def def)
        {
            int n = _cart.TryGetValue(def.Id, out var q) ? q : 0;
            _cart[def.Id] = Math.Min(99, n + 1);
            RefreshCart();
        }

        private static void ChangeQty(string id, int delta)
        {
            int n = _cart.TryGetValue(id, out var q) ? q : 0;
            n = Math.Max(0, Math.Min(99, n + delta));
            if (n <= 0) _cart.Remove(id);
            else _cart[id] = n;
            RefreshCart();
        }

        private static void RefreshCart()
        {
            try
            {
                int count = 0, total = 0;
                foreach (var kv in _cart)
                {
                    count += kv.Value;
                    var dsum = Defs.Find(kv.Key);
                    if (!string.IsNullOrEmpty(dsum.Id)) total += dsum.Value * kv.Value;
                }
                if (_cartBtnText != null) _cartBtnText.text = count.ToString();
                if (_cartText != null)
                    _cartText.text = count > 0 ? $"购物车 {count} 件 · 合计 {total} ₡" : "购物车 0 件";
                if (_cartOpen) BuildCartWindow();
                foreach (var kv in _qtyTexts)
                {
                    bool has = _cart.TryGetValue(kv.Key, out var q) && q > 0;
                    if (kv.Value != null) kv.Value.text = has ? q.ToString() : "";
                    if (_qtyCtrls.TryGetValue(kv.Key, out var ctrls) && ctrls != null)
                        foreach (var c in ctrls) if (c != null) c.SetActive(has);
                }
            }
            catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:1109", __ex); }
        }

        private static void Checkout()
        {
            try
            {
                if (_cart.Count == 0) { Debug.Log(FigurineMod.LogTag + "购物车是空的"); return; }
                var ps = PlayerStore.Instance;
                var inv = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.invElement : null;
                if (ps == null || inv == null) { Debug.Log(FigurineMod.LogTag + "结算：店内上下文缺失"); return; }

                int total = 0;
                foreach (var kv in _cart)
                {
                    var d0 = Defs.Find(kv.Key);
                    if (!string.IsNullOrEmpty(d0.Id)) total += d0.Value * kv.Value;
                }

                if (!ps.CanPay(total))
                {
                    _cartResult = $"现金不足：需要 {total} ₡，持有 {ps.GetCash()} ₡";
                    BuildCartWindow();
                    return;
                }

                int need = 0;
                foreach (var kv in _cart) need += kv.Value;
                int free = Warehouse.Capacity - Warehouse.Used;
                if (need > free)
                {
                    _cartResult = $"仓库放不下：这次要放 {need} 件，仓库 {Warehouse.Used}/{Warehouse.Capacity}，还能放 {free} 件";
                    BuildCartWindow();
                    Debug.Log(FigurineMod.LogTag + "结算：" + _cartResult);
                    return;
                }

                int placed = 0, paid = 0;
                foreach (var kv in _cart)
                {
                    // 原神 + 星铁合表查（以前只查 Catalog.All，星铁 ID 查不到就退回 Catalog.All[0]）
                    var def = Defs.Find(kv.Key);
                    if (string.IsNullOrEmpty(def.Id)) continue;
                    for (int i = 0; i < kv.Value; i++)
                    {
                        Warehouse.AddOrBag(def.Id, 1);
                        placed++;
                        paid += def.Value;
                    }
                }

                if (paid > 0) ps.PayMoney(paid, false);
                _cartResult = "";
                _cart.Clear();
                RefreshCart();
                RefreshCash();
                try { _cashFlash.Flash(new Color(1f, 0.45f, 0.45f)); } catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:1165", __ex); }
                
                Debug.Log(FigurineMod.LogTag + $"结算 放置={placed} 扣款={paid}");
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "结算异常：" + e.GetType().Name); }
        }

        private static void ShowConfirm(Catalog.Def def)
        {
            try
            {
                if (_confirmRoot != null) UnityEngine.Object.Destroy(_confirmRoot);
                int cash = PlayerStore.Instance != null ? PlayerStore.Instance.GetCash() : 0;

                _confirmRoot = new GameObject("ConfirmRoot");
                _confirmRoot.transform.SetParent(_canvas.transform, false);
                var rt = _confirmRoot.AddComponent<RectTransform>();
                Stretch(rt, 0, 0, 0, 0);

                var dim = _confirmRoot.AddComponent<Image>();
                dim.color = new Color(0, 0, 0, 0.65f);

                var panel = NewRect("Panel", _confirmRoot.transform);
                Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
                panel.sizeDelta = new Vector2(560, 320);
                var pbg = panel.gameObject.AddComponent<Image>();
                pbg.sprite = RoundedSprite.Gradient;
                pbg.type = Image.Type.Sliced;
                pbg.color = Color.white;

                var t1 = NewRect("T1", panel);
                Anchor(t1, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60));
                t1.sizeDelta = new Vector2(500, 50);
                AddText(t1, "购买 " + def.Zh, 30, TextAlignmentOptions.Center, Font());

                var t2 = NewRect("T2", panel);
                Anchor(t2, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -130));
                t2.sizeDelta = new Vector2(500, 40);
                AddText(t2, $"价格 {def.Value} ₡   ·   现金 {cash} ₡", 24, TextAlignmentOptions.Center, Font());

                var yes = NewRect("Yes", panel);
                Anchor(yes, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-130, 50));
                yes.sizeDelta = new Vector2(200, 60);
                var ybg = yes.gameObject.AddComponent<Image>(); ybg.color = new Color(0.15f, 0.35f, 0.25f, 1f);
                var yb = yes.gameObject.AddComponent<Button>();
                yb.onClick.AddListener(new UnityEngine.Events.UnityAction(() => { Buy(def); }));
                var yt = NewRect("Y", yes); Stretch(yt, 0, 0, 0, 0);
                AddText(yt, "确认购买", 24, TextAlignmentOptions.Center, Font());

                var no = NewRect("No", panel);
                Anchor(no, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(130, 50));
                no.sizeDelta = new Vector2(200, 60);
                var nbg = no.gameObject.AddComponent<Image>(); nbg.color = new Color(0.32f, 0.16f, 0.18f, 1f);
                var nb = no.gameObject.AddComponent<Button>();
                nb.onClick.AddListener(new UnityEngine.Events.UnityAction(() => CloseConfirm()));
                var nt = NewRect("N", no); Stretch(nt, 0, 0, 0, 0);
                AddText(nt, "取消", 24, TextAlignmentOptions.Center, Font());

                // 购买失败的原因显示在这里（现金不足 / 仓库已满）
                var st = NewRect("Status", panel);
                Anchor(st, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 132));
                st.sizeDelta = new Vector2(500, 32);
                _statusText = AddText(st, "", 20, TextAlignmentOptions.Center, Font());
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "确认框异常：" + e.GetType().Name); }
        }

        private static void CloseConfirm()
        {
            if (_confirmRoot != null) { UnityEngine.Object.Destroy(_confirmRoot); _confirmRoot = null; }
        }

        private static void Status(string text)
        {
            try { if (_statusText != null) _statusText.text = text; } catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:1239", __ex); }
        }

        private static void Buy(Catalog.Def def)
        {
            try
            {
                var ps = PlayerStore.Instance;
                var inv = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.invElement : null;
                if (ps == null || inv == null) { Debug.Log(FigurineMod.LogTag + "结算：店内上下文缺失"); return; }
                if (!ps.CanPay(def.Value)) { Status("现金不足"); return; }
                if (Warehouse.Used >= Warehouse.Capacity) { Status("仓库已满（" + Warehouse.Used + "/" + Warehouse.Capacity + "），先取出一些"); return; }
                ps.PayMoney(def.Value, false);
                Warehouse.AddOrBag(def.Id, 1);
                try { _cashFlash.Flash(new Color(1f, 0.45f, 0.45f)); } catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:1253", __ex); }
                Debug.Log(FigurineMod.LogTag + $"买入 {def.Zh} -{def.Value} 金（入仓），余额 {ps.GetCash()}");
                RefreshCash();
                CloseConfirm();
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "Buy 异常：" + e); }
        }

        private static void RefreshCash()
        {
            try
            {
                if (_cashText == null) return;
                int c = PlayerStore.Instance != null ? PlayerStore.Instance.GetCash() : 0;
                _cashText.text = c + " ₡";
            }
            catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:1269", __ex); }
        }

        private static Sprite TextureSprite(string key)
        {
            try
            {
                var mods = ModLoader.Mods;
                if (mods != null)
                    foreach (var m in mods)
                    {
                        if (m == null || m.Manifest == null || m.Manifest.ID != "figurine.gs") continue;
                        if (m.Textures != null && m.Textures.TryGetValue(key, out var tex) && tex != null)
                            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    }
            }
            catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:1285", __ex); }
            return null;
        }

        private static void IconRect(RectTransform parent, float x, float y, float w, float h, Color color)
        {
            var rt = NewRect("I", parent);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
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

        private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pos)
        {
            rt.anchorMin = min; rt.anchorMax = max;
            rt.anchoredPosition = pos;
        }

        private static TextMeshProUGUI AddText(RectTransform rt, string text, float size, TextAlignmentOptions align, TMP_FontAsset font = null)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = new Color(0.88f, 0.91f, 0.97f, 1f);
            t.raycastTarget = false;
            if (font == null) font = _cachedFont;
            if (font != null) t.font = font;
            return t;
        }

        /// <summary>
        /// 白卡上的白字会糊在一起 → 加一圈深色描边。
        /// Unity UI 的 `Outline` 是 BaseMeshEffect，对 TMP 文本同样生效（把网格复制 4 份偏移绘制）。
        /// </summary>
        private static void AddDarkOutline(TextMeshProUGUI t)
        {
            try
            {
                if (t == null) return;
                var o = t.gameObject.AddComponent<UnityEngine.UI.Outline>();
                o.effectColor = new Color(0.05f, 0.06f, 0.09f, 0.95f);
                o.effectDistance = new Vector2(1.2f, -1.2f);
            }
            catch (System.Exception __ex) { Safe.Warn("ShopUI.cs:1347", __ex); }
        }
    }
}
