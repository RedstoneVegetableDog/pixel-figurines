using System;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>F7 手办商店；F8 状态；F9 生成第一件手办。</summary>
    public class ProbeDriver : MonoBehaviour
    {
        private const string WinId = "figurine.shop";
        private const int PageSize = 8;
        private int _page;
        private bool _open;

        private static bool _dbgOn;
        private static float _dbgAt = -99f;

        /// <summary>
        /// 调试键开关（2026-11-05）：**默认关闭**，玩家按 F 键没有任何效果。
        /// 打开方式二选一：① 放空文件 `Mods\PixelFigurines\调试.txt`；② ModData 里 `figurine_debug=1`。
        /// 带 5 秒缓存，避免每帧读盘。
        /// </summary>
        private static bool DebugOn()
        {
            try
            {
                if (Time.realtimeSinceStartup - _dbgAt < 5f) return _dbgOn;
                _dbgAt = Time.realtimeSinceStartup;
                try { if (ModData.Get("figurine_debug") == "1") { _dbgOn = true; return true; } } catch { }
                var dir = System.IO.Path.GetDirectoryName(typeof(ProbeDriver).Assembly.Location);
                _dbgOn = !string.IsNullOrEmpty(dir) && System.IO.File.Exists(System.IO.Path.Combine(dir, "调试.txt"));
            }
            catch { _dbgOn = false; }
            return _dbgOn;
        }

        private void Update()
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.F7)) ShopUI.Toggle();

                // 调试键总闸：**只包住按键**，不能把下面的 Tick 一起跳过
                // （2026-11-05 事故：这里原本直接 return，导致 Haizi/CustomerSellers/Doro/BoxAnimation
                //   的定时器全停 → 海子不送礼、盲盒动画卡死且残留到主菜单）
                if (DebugOn())
                {
                    if (Input.GetKeyDown(KeyCode.F8)) { Dump(); RuntimeSelfCheck.Report(); }
                    if (Input.GetKeyDown(KeyCode.F9))
                    {
                        if (Input.GetKey(KeyCode.LeftShift)) WarehouseTakeSelfTest();   // Shift+F9：仓库取出实测
                        else SpawnFirst();
                    }
                    if (Input.GetKeyDown(KeyCode.F12)) SpawnBlindBox();
                    if (Input.GetKeyDown(KeyCode.F10)) SpawnFake();
                    if (Input.GetKeyDown(KeyCode.F5)) FigurineAppraisal.ForceSpawn();
                    if (Input.GetKeyDown(KeyCode.F4)) FigurineCert.DebugSpawnBoard();
                    if (Input.GetKeyDown(KeyCode.F3)) FigurineCert.DebugFillProgress();
                    if (Input.GetKeyDown(KeyCode.F2)) OpenLabelerOnFirstFigurine();
                    if (Input.GetKeyDown(KeyCode.F6)) DumpMailbox();
                    if (Input.GetKeyDown(KeyCode.F1)) DumpSelectState();
                    if (Input.GetKeyDown(KeyCode.F11)) Cabinet.Toggle();
                }

                // 游戏逻辑：无论调试开关都必须执行
                Haizi.Tick();
                CustomerSellers.Tick();
                FigurineAppraisal.Tick();
                Doro.Tick();
                BoxAnimation.Tick();
            }
            catch (System.Exception __ex) { Safe.Warn("ProbeDriver.cs:29", __ex); }
        }

        private void Toggle()
        {
            try
            {
                var mgr = CustomUIManager.Instance;
                if (mgr == null) { Debug.LogError(FigurineMod.LogTag + "CustomUIManager.Instance 为空"); return; }

                if (mgr.IsOpen(WinId))
                {
                    mgr.CloseWindow(WinId);
                    _open = false;
                    Debug.Log(FigurineMod.LogTag + "商店已关闭");
                    return;
                }
                OpenPage(_page);
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "开窗失败：" + e); }
        }

        private void OpenPage(int page)
        {
            try
            {
                var mgr = CustomUIManager.Instance;
                int total = Catalog.All.Length;
                int pages = Math.Max(1, (total + PageSize - 1) / PageSize);
                _page = Mathf.Clamp(page, 0, pages - 1);

                if (mgr.IsOpen(WinId)) mgr.CloseWindow(WinId);

                var b = CustomUIManager.Window(WinId, $"手办商店  {_page + 1}/{pages}", "overlay");
                b.SetSize(430, 900);
                b.Center();

                int start = _page * PageSize;
                int end = Math.Min(start + PageSize, total);
                for (int i = start; i < end; i++)
                {
                    var def = Catalog.All[i];
                    var sp = mgr.GetModSprite("figurine.gs", def.Id);
                    b.AddImage(sp, 32, 32, "img_" + def.Id);
                    b.AddLabel($"{def.Zh}   {def.Value} 金   {Tier.Of(def.Star)}", "lbl_" + def.Id);
                    var d = def;
                    b.AddButton("买", () => Buy(d), "buy_" + def.Id);
                }

                if (_page > 0) b.AddButton("上一页", () => OpenPage(_page - 1), "prev");
                if (_page < pages - 1) b.AddButton("下一页", () => OpenPage(_page + 1), "next");
                b.AddButton("关闭", () => { try { CustomUIManager.Instance.CloseWindow(WinId); } catch (System.Exception __ex) { Safe.Warn("ProbeDriver.cs:80", __ex); } _open = false; }, "close");
                b.Show();
                _open = true;
                Debug.Log(FigurineMod.LogTag + $"商店页 {_page + 1}/{pages} 已打开");
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "OpenPage 异常：" + e); }
        }

        private void Buy(Catalog.Def def)
        {
            try
            {
                var ps = PlayerStore.Instance;
                var inv = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.invElement : null;
                if (ps == null || inv == null) { Debug.LogError(FigurineMod.LogTag + "买：店内上下文缺失"); return; }
                if (!ps.CanPay(def.Value)) { Debug.Log(FigurineMod.LogTag + "买：" + def.Zh + " 现金不足"); return; }

                var item = FigurineMod.BuildFigurine(def);
                if (item == null) return;
                if (!inv.UncheckedAccept(item)) { Debug.Log(FigurineMod.LogTag + "买：背包放不下 " + def.Zh); return; }
                try { item.EnableTag("IS_OWNED_TAG", true); } catch (System.Exception __ex) { Safe.Warn("ProbeDriver.cs:100", __ex); }

                ps.PayMoney(def.Value, false);
                Debug.Log(FigurineMod.LogTag + $"买入 {def.Zh} -{def.Value} 金，余额 {ps.GetCash()}");
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "Buy 异常：" + e); }
        }

        /// <summary>F1：打印背包/柜台上盲盒与手办的「能否被框选」关键字段，定位框选问题。</summary>
        private void DumpSelectState()
        {
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null) return;
                // 海子电话树冒烟测试：构造一次（不显示），确认不会无限递归
                try
                {
                    var dlg = Haizi.BuildPhoneDialogForTest();
                    var seen = new System.Collections.Generic.HashSet<Dialogue>();
                    var stack = new System.Collections.Generic.Stack<Dialogue>();
                    if (dlg != null) stack.Push(dlg);
                    int nodes = 0;
                    while (stack.Count > 0 && nodes < 500)
                    {
                        var cur = stack.Pop();
                        if (cur == null || !seen.Add(cur)) continue;
                        nodes++;
                        try { if (cur.nextDialogue != null) stack.Push(cur.nextDialogue); } catch { }
                        try
                        {
                            if (cur.choices != null)
                                foreach (var ch2 in cur.choices)
                                    if (ch2.Item2 != null) stack.Push(ch2.Item2);
                        }
                        catch { }
                    }
                    Debug.Log(FigurineMod.LogTag + "电话树自检：话题节点=" + nodes + "（<500 且无异常 = 递归已切断）");
                }
                catch (System.Exception e) { Debug.LogWarning(FigurineMod.LogTag + "电话树自检异常：" + e.GetType().Name); }

                int shown = 0;
                foreach (var it in ps.FindAllItem(true))
                {
                    if (it == null) continue;
                    string id = it.identifier ?? "";
                    bool isBox = id.IndexOf("blindbox", StringComparison.Ordinal) >= 0;
                    bool isFig = FigurineAuth.IsFigurine(it);
                    if (!isBox && !isFig) continue;
                    if (shown++ >= 6) break;
                    bool may = false, pinned = false; int maxRemove = -1, count = -1;
                    try { may = it.MayRemove(); } catch { }
                    try { maxRemove = it.MaxNumRemove(); } catch { }
                    try { count = it.unitCount; } catch { }
                    try { pinned = ItemPinHelper.IsPinned(it); } catch { }
                    string parent = "null";
                    try { parent = it.parentInventory != null ? it.parentInventory.GetType().Name : "null"; } catch { }
                    string ms = "null", sh = "null";
                    try { var s1 = it.modifiedShape; if (s1 != null) ms = s1.width + "x" + s1.height; } catch { }
                    try { var s2 = it.shape; if (s2 != null) sh = s2.width + "x" + s2.height; } catch { }
                    Debug.Log(FigurineMod.LogTag + "框选诊断：" + id
                        + " 类型=" + (isBox ? "盲盒" : "手办")
                        + " MayRemove=" + may + " MaxNumRemove=" + maxRemove
                        + " unitCount=" + count + " pinned=" + pinned
                        + " modifiedShape=" + ms + " shape=" + sh + " parent=" + parent);
                }
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "F1 异常：" + e.GetType().Name); }
        }

        /// <summary>F6：打印货物邮箱容器的网格尺寸（验证 9×12 是否生效）。</summary>
        private void DumpMailbox()
        {
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null) return;
                GameItem box = null;
                foreach (var it in ps.FindAllItem(true))
                {
                    if (it == null) continue;
                    if (it.identifier == DoroItems.MailboxId) { box = it; break; }
                }
                if (box == null) { Debug.LogWarning(FigurineMod.LogTag + "F6：没找到货物邮箱"); return; }
                var cw = box.contentWindow;
                var inv = (cw != null && cw.children != null && cw.children.Count > 0) ? cw.children[0] as GameGridInventory : null;
                if (inv == null) { Debug.LogWarning(FigurineMod.LogTag + "F6：邮箱没有内层库存"); return; }
                var shape = inv.inventoryShape;
                Debug.Log(FigurineMod.LogTag + "F6 货物邮箱：" + (shape != null ? shape.width + "×" + shape.height + " 格" : "shape=null")
                    + " （像素 " + inv.widthPixels + "×" + inv.heightPixels + "）");
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "F6 异常：" + e.GetType().Name); }
        }

        /// <summary>F2：对背包里第一件手办直接打开原版标签机面板（验证「正品/赝品」两项在不在）。</summary>
        private void OpenLabelerOnFirstFigurine()
        {
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null) { Debug.LogWarning(FigurineMod.LogTag + "F2：店内上下文缺失"); return; }
                GameItem found = null;
                foreach (var it in ps.FindAllItem(true))
                {
                    if (it == null) continue;
                    if (!FigurineAuth.IsFigurine(it)) continue;
                    found = it; break;
                }
                if (found == null) { Debug.LogWarning(FigurineMod.LogTag + "F2：背包里没有手办"); return; }
                FigurineLabelFeature.Ensure(found);
                var f = FigurineLabelFeature.Find(found);
                Debug.Log(FigurineMod.LogTag + "F2 打开标签机：" + found.identifier
                    + " 特征=" + (f != null)
                    + " fake=" + (f != null && f.fakeCondition != null ? f.fakeCondition.identifier : "?")
                    + " real=" + (f != null && f.realCondition != null ? f.realCondition.identifier : "?"));
                LabelerUIManager.Instance.OpenUI(found);
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "F2 异常：" + e.GetType().Name + " " + e.Message); }
        }

        /// <summary>F10：造一件「真身赝品 + 标赝品」的手办进背包，方便用放大镜测鉴定面板。</summary>
        private void SpawnFake()
        {
            try
            {
                var item = FigurineMod.BuildFigurine(Catalog.All[0]);
                if (item == null) { Debug.LogError(FigurineMod.LogTag + "F10 造物失败"); return; }
                FigurineAuth.Apply(item, new FigurineAuth.Roll { TruthFake = true, LabelFake = true, Variant = 1 });
                var inv = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.invElement : null;
                bool ok = FigurineMod.TryPlace(inv, item);
                Debug.Log(FigurineMod.LogTag + "F10 造赝品 " + item.identifier + " 进背包=" + ok
                    + " 价格=" + FigurineAuth.PricePercent(item) + "% 候选="
                    + string.Join("/", FigurineInspect.Options(item).ToArray()));
                // 直接开鉴定面板（等于把放大镜拖到这件上），省得手动拖
                try { InspectionUIManager.Instance.OnInspectItem(item); }
                catch (System.Exception __ex) { Safe.Warn("ProbeDriver.F10.Inspect", __ex); }
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "F10 异常：" + e); }
        }

        private void SpawnFirst()
        {
            try
            {
                var item = FigurineMod.BuildFigurine(Catalog.All[0]);
                if (item == null) { Debug.LogError(FigurineMod.LogTag + "F9 造物失败"); return; }
                var inv = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.invElement : null;
                bool ok = FigurineMod.TryPlace(inv, item);
                Debug.Log(FigurineMod.LogTag + $"F9 {item.name} shape={item.shape.width}x{item.shape.height} 进背包={ok}");
                // 顺带自检托盘图按需加载（F9 是调试键，这里只打印尺寸，不进游戏画面）
                try
                {
                    string tk = FigurineFake.TrayKeyFor(item);
                    var sp = TrayArt.Get(tk);
                    Debug.Log(FigurineMod.LogTag + "F9 托盘图键=" + tk + " 加载=" + (sp != null)
                        + (sp != null ? " " + (int)sp.rect.width + "×" + (int)sp.rect.height : ""));
                }
                catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "F9 托盘自检失败：" + e.GetType().Name); }
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "F9 异常：" + e); }
        }

        /// <summary>F12：往背包塞一个手办盲盒并立刻开箱（调试用，验证开盒动画）。</summary>
        private void SpawnBlindBox()
        {
            try
            {
                var box = BlindBox.Build();
                if (box == null) { Debug.LogError(FigurineMod.LogTag + "F12 盲盒构造失败"); return; }
                var inv = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.invElement : null;
                bool ok = FigurineMod.TryPlace(inv, box);
                Debug.Log(FigurineMod.LogTag + "F12 盲盒进背包=" + ok);
                if (ok) BlindBox.Open(box);
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "F12 异常：" + e); }
        }

        /// <summary>Shift+F9：实测「仓库取出」链路（加 2 件进仓库 → 取出 1 件 → 校验背包是否真的有）。</summary>
        private static void WarehouseTakeSelfTest()
        {
            try
            {
                const string id = "figurine_gs_klee";
                int added = Warehouse.Add(id, 2);
                int used0 = Warehouse.Used, cap = Warehouse.Capacity;
                var inv = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.invElement : null;
                int before = inv != null && inv.childItems != null ? inv.childItems.Count : -1;
                int taken = Warehouse.Take(id, 1);
                int after = inv != null && inv.childItems != null ? inv.childItems.Count : -1;
                bool contains = false;
                if (inv != null && inv.childItems != null)
                    foreach (var it in inv.childItems)
                        if (it != null && string.Equals(it.identifier, id, StringComparison.Ordinal)) { contains = true; break; }
                Debug.Log(FigurineMod.LogTag + "[仓库自测] Add=" + added + " 仓 " + used0 + "/" + cap
                    + " Take=" + taken + " 背包 " + before + "→" + after
                    + " 含手办=" + contains + " 仓剩余=" + Warehouse.Used);
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "[仓库自测] 异常：" + e.GetType().Name + " " + e.Message); }
        }

        private void Dump()
        {
            try
            {
                var mgr = CustomUIManager.Instance;
                int cash = 0;
                try { cash = PlayerStore.Instance != null ? PlayerStore.Instance.GetCash() : 0; } catch (System.Exception __ex) { Safe.Warn("ProbeDriver.cs:151", __ex); }
                Debug.Log(FigurineMod.LogTag + $"状态 open={_open} page={_page + 1} cash={cash} mgr={(mgr != null)}");
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "Dump 失败：" + e); }
        }
    }
}
