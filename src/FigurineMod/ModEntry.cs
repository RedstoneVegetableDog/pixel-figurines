using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 手办模组（正式版）——批量手办阶段。
    ///
    /// ```
    /// · Textures\Items\<id>.png 由 build_catalog.py 按 16px/格 生成
    /// · 每个手办 = 空白物品 + 名字 + 价格 + SetSpriteAndShapeFromMod
    /// ```
    /// </summary>
    public class FigurineMod : IMod
    {
        public const string LogTag = "[手办] ";
        private static GameObject _driver;
        private static ModItemDirectory _dir;
        private static bool _registered;
        private static bool _harmonyPatched;

        public void Init(ModManifest manifest)
        {
            try
            {
                Debug.Log(LogTag + "Init id=" + (manifest != null ? manifest.ID : "?"));
                // 构建配置走 #if DEBUG（和赝品开关无关）；赝品玩法单独一个字段，避免「Release 却显示 Debug」的误读
#if DEBUG
                string build = "Debug";
#else
                string build = "Release";
#endif
                Debug.Log(LogTag + "构建：" + build + "｜赝品玩法：" + (FeatureFlags.Fakes ? "开" : "关"));
            }
            catch (Exception e) { Debug.LogError(LogTag + "Init 异常：" + e); }
        }

        public void OnEnable()
        {
            try
            {
                ModHook.OnModItemDirectoryInit += OnItemDirectoryInit;
                ModHook.OnGameLoadedEarly += EnsureDirectoryFallback;
                ModHook.OnGameLoadedNormal += OnGameLoaded;
                ModHook.OnGameLoadedLate += OnGameLoadedLate;
                ModHook.OnCreateTooltipLate += OnTooltip;
                try { LootInjection.Install(); } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:42", __ex); }
                try { FigurineEvents.EnsureRegistered(); } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:43", __ex); }
                ModHook.OnShutterOpenedLate += Doro.OnShopOpened;
                ModHook.OnShutterOpenedLate += CustomerScheduler.OnShopOpened;
                ModHook.OnShutterOpenedLate += FigurineAppraisal.OnShopOpened;
                ModHook.OnShutterOpenedLate += FigurineCert.OnShopOpened;
                ModHook.OnShutterOpenedLate += Haizi.OnShopOpened;
                ModHook.OnShutterOpenedLate += TaskFlow.PruneStale;
                ModHook.OnShutterClosedLate += TaskFlow.OnDayEnded;
                // 原版分组是在「战利品表加载完」之后才进 LootRegistry 的，挂这个钩子重算权重
                ModHook.OnLootTablesLoaded += OnLootTablesLoaded;

                if (_driver == null)
                {
                    _driver = new GameObject("FigurinePortDriver");
                    UnityEngine.Object.DontDestroyOnLoad(_driver);
                    _driver.AddComponent<ProbeDriver>();
                }

                EnsureDirectory();
                // 三态掷骰自检（纯数学，启动即打，不等读档）
                try { FigurineAuth.SelfCheck(); } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:61", __ex); }
                // 宿主加载器会自动应用 [HarmonyPatch]（日志 Harmony: N method(s) patched），这里不再手动 PatchAll
                Debug.Log(LogTag + "OnEnable 完成");
            }
            catch (Exception e) { Debug.LogError(LogTag + "OnEnable 异常：" + e); }
        }

        public void OnDisable()
        {
            try
            {
                ModHook.OnModItemDirectoryInit -= OnItemDirectoryInit;
                ModHook.OnGameLoadedEarly -= EnsureDirectoryFallback;
                ModHook.OnGameLoadedNormal -= OnGameLoaded;
                ModHook.OnGameLoadedLate -= OnGameLoadedLate;
                ModHook.OnCreateTooltipLate -= OnTooltip;
                ModHook.OnShutterOpenedLate -= Doro.OnShopOpened;
                ModHook.OnShutterOpenedLate -= CustomerScheduler.OnShopOpened;
                ModHook.OnShutterOpenedLate -= FigurineAppraisal.OnShopOpened;
                ModHook.OnShutterOpenedLate -= FigurineCert.OnShopOpened;
                ModHook.OnShutterOpenedLate -= Haizi.OnShopOpened;
                ModHook.OnShutterOpenedLate -= TaskFlow.PruneStale;
                ModHook.OnShutterClosedLate -= TaskFlow.OnDayEnded;
                ModHook.OnLootTablesLoaded -= OnLootTablesLoaded;
            }
            catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:80", __ex); }
        }

        /// <summary>
        /// **不再自己抢建 ModItemDirectory**（2026-11-04 修，问题 B）：
        /// 以前在 OnEnable 里 AddComponent&lt;ModItemDirectory&gt;()，会抢在游戏前面建目录，
        /// 游戏的目录被 DirectoryMaster 按类型判重丢弃 → OnModItemDirectoryInit 只 fire 一次，
        /// 排在后面的 mod 全部收不到回调、物品注册失败（WaterTowerPlus / CorpPresence 实测被打坏）。
        ///
        /// 现在的顺序：
        /// 1) 已经有别人建好的目录 → 直接复用
        /// 2) 还没有 → 什么都不做，等 ModHook.OnModItemDirectoryInit（游戏建目录时回调）
        /// 3) 读档前仍然没有（极端情况）→ 才自己建，并主动 InitDirectory 补发事件
        /// </summary>
        private static void EnsureDirectory()
        {
            if (_dir != null) return;
            Debug.Log(LogTag + "暂无 ModItemDirectory，等待游戏创建（不抢建）");
        }

        /// <summary>
        /// 读档前的兜底（无反射）：所有 mod 都加载、都订阅之后，如果还没有目录，就由本模组创建。
        /// 原版 `Directory.Awake` 会 AddDirectory → InitDirectory → 触发 OnModItemDirectoryInit。
        /// </summary>
        private static void EnsureDirectoryFallback()
        {
            try
            {
                if (_dir != null) return;
                Debug.LogWarning(LogTag + "读档前仍无 ModItemDirectory，由本模组创建（此时所有 mod 都已订阅）");
                var go = new GameObject("FigurineModItemDirectory");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<ModItemDirectory>();
                if (_dir == null)
                    Debug.LogWarning(LogTag + "ModItemDirectory 创建后仍未收到回调（可能已有同名目录被丢弃），本次不注册");
            }
            catch (System.Exception __ex) { Safe.Warn("ModEntry.EnsureDirectoryFallback", __ex); }
        }

        private static void OnItemDirectoryInit(ModItemDirectory dir)
        {
            try
            {
                if (dir == null) return;
                _dir = dir;
                if (_registered) return;
                _registered = true;

                // 先把原神 40 位按「数量金字塔」重分五档，再注册
                try { TierQuota.ApplyGenshin(); } catch (Exception e) { Debug.LogWarning(LogTag + "重分档失败：" + e.GetType().Name); }

                int n = 0;
                foreach (var def in Catalog.All)
                {
                    var d = def;
                    // 这个 creator 只有「游戏自己建物」时才会被调（掉落表最典型）→ 按掉落来源掷三态
                    if (dir.Add(d.Id, (Func<GameItem>)(() => BuildLoot(d)))) n++;
                }
                Debug.Log(LogTag + $"批量注册手办 {n}/{Catalog.All.Length}");
                StarRailItems.Register(dir);
                ZzzItems.Register(dir);
                try { FigurineCert.Register(dir); } catch (System.Exception __ex) { Safe.Warn("ModEntry.CertRegister", __ex); }
                TerminalItem.Register(dir);
                DoroItems.Register(dir);
                Haizi.RegisterItems(dir);
                BlindBox.Register(dir);
                Cabinet.Register(dir);
            }
            catch (Exception e) { Debug.LogError(LogTag + "注册异常：" + e.GetType().Name + " | " + e.Message); }
        }

        /// <summary>放置后的硬校验：必须真的在这个库存里，且 parentInventory 正确。</summary>
        private static bool VerifiedIn(GameGridInventory inv, GameItem item)
        {
            try
            {
                return inv != null && item != null
                    && item.parentInventory == inv
                    && inv.childItems != null && inv.childItems.Contains(item);
            }
            catch { return false; }
        }

        /// <summary>库存某个格子是否允许放东西（inventoryShape: 0=可用，1=不可用；兼容非矩形背包）。</summary>
        private static bool CellAllowed(GameGridInventory inv, int x, int y)
        {
            try
            {
                if (inv == null || x < 0 || y < 0) return false;
                var ish = inv.inventoryShape;
                if (ish == null) return true;
                if (x >= ish.width || y >= ish.height) return false;
                return ish.Get(x, y) == 0;
            }
            catch { return true; }
        }

        public static bool TryPlace(GameGridInventory inv, GameItem item)
        {
            try
            {
                if (inv == null || item == null) return false;
                // 用 inventoryShape 的格数（widthPixels/16 在改过背包形状的 mod 下可能对不上）
                int iw = inv.inventoryShape != null ? inv.inventoryShape.width : inv.widthPixels / 16;
                int ih = inv.inventoryShape != null ? inv.inventoryShape.height : inv.heightPixels / 16;
                int w = item.shape != null ? item.shape.width : 1;
                int h = item.shape != null ? item.shape.height : 1;
                if (w <= 0 || h <= 0 || w > iw || h > ih) return false;

                // ① 先问游戏自己「能不能放下、放在哪」—— 用它算出来的槽位就不会压到别的物品。
                //    （`TryFindOneValidInventorySlot(item, keepOrientation)` 会遍历所有位置和朝向，
                //     用 `TryInventorySlot` 做真正的碰撞检测；参数必须传两个，
                //     单参数那个重载在游戏里写成了自我递归，不能调用。）
                try
                {
                    var marker = inv.TryFindOneValidInventorySlot(item, false);
                    if (marker != null && marker.numTransfer > 0 && marker.targetItem == null && marker.itemGridShape != null)
                    {
                        var sh = marker.itemGridShape;
                        bool inBounds = sh.minX >= 0 && sh.minY >= 0
                            && sh.minX + sh.width <= iw && sh.minY + sh.height <= ih;
                        bool cellsOk = inBounds;
                        if (cellsOk)
                        {
                            for (int yy = 0; yy < sh.height && cellsOk; yy++)
                                for (int xx = 0; xx < sh.width; xx++)
                                    if (sh.GetLocal(xx, yy) != 0 && !CellAllowed(inv, sh.minX + xx, sh.minY + yy)) { cellsOk = false; break; }
                        }
                        if (!inBounds || !cellsOk)
                        {
                            // 实测出现过 at 18,2 但物品不见：槽位越界/不在库存形状里，不信任，走手动扫描
                            Debug.LogWarning(LogTag + $"TryPlace 槽位不可用 {item.identifier} at {sh.minX},{sh.minY} {sh.width}x{sh.height}，库存 {iw}x{ih}");
                        }
                        else
                        {
                            try { item.shape = sh; item.modifiedShape = sh; } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:142", __ex); }
                            // 绝对槽位形状下，背景锚点应为 0（以前写成 sh.minX 会二次偏移 → 贴图和点击错位）
                            try { item.modifiedXOrigin = 0; item.modifiedYOrigin = 0; } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:143", __ex); }
                            bool okSlot = inv.UncheckedAccept(item);
                            bool ownSlot = false;
                            if (okSlot) { try { GeneralHelper.SetItemOwned(item, true); ownSlot = GeneralHelper.IsItemOwned(item); } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:146", __ex); } }
                            bool verified = VerifiedIn(inv, item);
                            Debug.Log(LogTag + $"TryPlace(游戏槽位) {item.identifier} at {sh.minX},{sh.minY} ok={okSlot} owned={ownSlot} verified={verified}");
                            if (verified) return true;
                            // 不信任的槽位：能收回就收回，然后走手动扫描
                            if (okSlot && item != null && item.parentInventory == inv) { try { inv.Expel(item); } catch { } }
                        }
                    }
                }
                catch (Exception e) { Debug.LogWarning(LogTag + "TryPlace 游戏槽位失败，转兜底扫描：" + e.GetType().Name); }

                // 扫出背包已被占用的格子（子物品的矩形范围）
                var used = new bool[iw, ih];
                var kids = inv.childItems;
                if (kids != null)
                {
                    for (int i = 0; i < kids.Count; i++)
                    {
                        var it = kids[i];
                        if (it == null || it == item) continue;
                        var sh = it.modifiedShape ?? it.shape;
                        if (sh == null) continue;
                        for (int y = sh.minY; y < sh.minY + sh.height; y++)
                            for (int x = sh.minX; x < sh.minX + sh.width; x++)
                                if (x >= 0 && x < iw && y >= 0 && y < ih) used[x, y] = true;
                    }
                }

                string shp = "";
                for (int i = 0; kids != null && i < kids.Count && i < 6; i++)
                {
                    var it = kids[i];
                    if (it == null) continue;
                    var s1 = it.shape; var s2 = it.modifiedShape;
                    shp += $" [{it.identifier} shape={s1?.minX},{s1?.minY} {s1?.width}x{s1?.height} mod={s2?.minX},{s2?.minY} {s2?.width}x{s2?.height}]";
                }
                Debug.Log(LogTag + "占用采样" + shp);

                for (int y = 0; y <= ih - h; y++)
                {
                    for (int x = 0; x <= iw - w; x++)
                    {
                        bool free = true;
                        for (int yy = y; yy < y + h && free; yy++)
                            for (int xx = x; xx < x + w; xx++)
                                if (used[xx, yy] || !CellAllowed(inv, xx, yy)) { free = false; break; }
                        if (!free) continue;

                        var sb = new GridShapeBuilder(w, h);
                        sb.SetDataFill(w, h, 1);
                        sb.SetPosition(x, y);
                        var sh2 = sb.Build();
                        item.shape = sh2;
                        item.modifiedShape = sh2;
                        try { item.modifiedXOrigin = 0; item.modifiedYOrigin = 0; } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:196", __ex); }
                        bool ok = inv.UncheckedAccept(item);
                        bool own = false;
                        if (ok) { try { GeneralHelper.SetItemOwned(item, true); own = GeneralHelper.IsItemOwned(item); } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:199", __ex); } }
                        bool verified = VerifiedIn(inv, item);
                        Debug.Log(LogTag + $"TryPlace {item.identifier} at {x},{y} ok={ok} owned={own} verified={verified}");
                        if (verified) return true;
                        if (ok && item != null && item.parentInventory == inv) { try { inv.Expel(item); } catch { } }
                        // 这个槽位不行就继续找下一个（以前直接 return false → 明明有空间也报“放不下”）
                        continue;
                    }
                }
                Debug.Log(LogTag + "TryPlace 没有空位 " + item.identifier);
                return false;
            }
            catch (Exception e) { Debug.LogError(LogTag + "放置异常：" + e.GetType().Name); return false; }
        }

        private static Dictionary<string, Catalog.Def> _map;

        private static string DefectText(string[] defects)
        {
            if (defects == null || defects.Length == 0) return "部件";
            var map = new Dictionary<string, string>
            {
                ["color"] = "上色", ["sakura"] = "樱花枝", ["gohei"] = "御币", ["lantern"] = "灯笼图案",
                ["face"] = "脸", ["pattern"] = "花纹", ["bandage"] = "绷带", ["sword"] = "剑",
                ["mouth"] = "嘴", ["winder"] = "发条", ["left_hand"] = "左手", ["skirt"] = "裙摆",
                ["finger"] = "手指", ["star"] = "星", ["toenail"] = "脚趾甲", ["horn"] = "角",
                ["bell"] = "铃", ["mask"] = "面具", ["grass"] = "草", ["heart"] = "心",
            };
            var parts = new List<string>();
            foreach (var d in defects) parts.Add(map.TryGetValue(d, out var zh) ? zh : d);
            return string.Join("、", parts.ToArray());
        }

        private static void OnTooltip(RichTextBuilder b, GameItem item)
        {
            try
            {
                if (b == null || item == null) return;
                if (_map == null)
                {
                    _map = new Dictionary<string, Catalog.Def>();
                    foreach (var d in Catalog.All) _map[d.Id] = d;
                    foreach (var d in StarRailData.ToCatalog()) _map[d.Id] = d;
                    foreach (var d in ZzzData.ToCatalog()) _map[d.Id] = d;
                }
                if (_map.TryGetValue(item.identifier, out var def))
                {
                    // 属性行（星铁【同谐·火】/ 绝区零【电｜击破｜阵营】）单独一行，避免被自动换行拆开
                    string meta = Defs.Meta(def.Id);
                    if (!string.IsNullOrEmpty(meta))
                        b.AddLine(meta, true, RenderHandler.ColorPalette.LightBlue, true, false, false, false, RenderHandler.ColorPalette.None, RenderHandler.ColorPalette.None, RenderHandler.ColorPalette.None);
                    // 稀有度五档（消费/军规/受限/隐秘/违禁），颜色统一走 Tier.cs
                    b.AddLine(Tier.Label(def.Star), true, Tier.Palette(def.Star), true, false, false, false, RenderHandler.ColorPalette.None, RenderHandler.ColorPalette.None, RenderHandler.ColorPalette.None);
                    int attr = Math.Max(1, def.Star) * 5;
                    b.AddLine($"模组效果：吸引力 +{attr}（{Tier.Of(def.Star)}）", true, RenderHandler.ColorPalette.LightGreen, false, false, false, false, RenderHandler.ColorPalette.None, RenderHandler.ColorPalette.None, RenderHandler.ColorPalette.None);
                    if (!string.IsNullOrEmpty(def.Desc))
                        b.AddLine(def.Desc, true, RenderHandler.ColorPalette.LightGray, false, true, false, false, RenderHandler.ColorPalette.None, RenderHandler.ColorPalette.None, RenderHandler.ColorPalette.None);
                    // 厂牌行：原神·米哈游 / 星穹铁道·米哈游 / 绝区零·米哈游（不带“公司”二字）
                    string company = Defs.Company(def.Id);
                    if (!string.IsNullOrEmpty(company))
                        b.AddLine(company, true, RenderHandler.ColorPalette.Gray, false, false, false, false, RenderHandler.ColorPalette.None, RenderHandler.ColorPalette.None, RenderHandler.ColorPalette.None);
                }

                // 三态一行：正品 / 赝品 / 未鉴定（用户定：全部只放工具提示，名字不加后缀）
                try
                {
                    string auth = FigurineAuth.TooltipLine(item);
                    if (!string.IsNullOrEmpty(auth))
                        b.AddLine(auth, true, FigurineAuth.TooltipColor(item), true, false, false, false,
                            RenderHandler.ColorPalette.None, RenderHandler.ColorPalette.None, RenderHandler.ColorPalette.None);
                }
                catch (System.Exception __ex) { Safe.Warn("ModEntry.Tooltip.Auth", __ex); }

                int fv;
                if (FigurineFake.IsFake(item, out fv) && FigurineFake.IsExposed(item))
                {
                    var defects = FigurineFake.Defects(item.identifier, fv);
                    // 变体 1 = 反色；变体 2..N = 赝品立绘的部位（对应鉴定面板的「哪里不对」）
                    string defectText = fv <= 0 ? "反色" : (DefectText(defects) + "不对");
                    b.AddLine($"赝品（已戳穿）：{defectText}", true, RenderHandler.ColorPalette.LightRed, false, false, false, false, RenderHandler.ColorPalette.None, RenderHandler.ColorPalette.None, RenderHandler.ColorPalette.None);
                }
            }
            catch (Exception e) { Debug.LogError(LogTag + "tooltip 异常：" + e.GetType().Name); }
        }

        private static int _spriteDbg;

        public static GameItem BuildFigurine(Catalog.Def def)
        {
            try
            {
                var item = ItemDirectory.CreateEmptyItem(def.Id);
                if (item == null) return null;
                item.name = def.Zh;
                item.unitCount = 1;
                try { item.EnableTag("IS_OWNED_TAG", true); item.SyncModifiedState(); } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:278", __ex); }
                item.unitValue = def.Value;
                item.unitBaseValue = def.Value;
                item.SetSpriteAndShapeFromMod("figurine.gs", def.Id);
                try
                {
                    var sb = new GridShapeBuilder(def.W, def.H);
                    sb.SetDataFill(def.W, def.H, 1);
                    item.SetShape(sb.Build());
                }
                catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:288", __ex); }
                try { if (item.uniqueId == 0) item.uniqueId = UnityEngine.Random.Range(1, int.MaxValue); } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:289", __ex); }
                // 诊断：贴图像素 vs 占格（只打前 3 件，避免刷屏）
                try
                {
                    if (_spriteDbg < 3)
                    {
                        _spriteDbg++;
                        var mgr = CustomUIManager.Instance;
                        var sp = mgr != null ? mgr.GetModSprite("figurine.gs", def.Id) : null;
                        string sz = sp != null ? (sp.rect.width + "×" + sp.rect.height) : "null";
                        int gs = 0;
                        try { gs = UISettings.current != null ? UISettings.current.gridSize : 0; } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:300", __ex); }
                        Debug.Log(LogTag + "贴图诊断 " + def.Id + " 贴图=" + sz + " 占格=" + def.W + "×" + def.H
                            + " gridSize=" + gs + " 期望像素=" + (def.W * gs) + "×" + (def.H * gs));
                    }
                }
                catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:305", __ex); }
                // ★ 可鉴定：不打这个 tag，游戏判定「这件不可检查」，放大镜面板根本不弹。
                //   beta 里是 ItemRegistration 建物品时调 InspectableHelper.InitInspectableItem(item)。
                try { InspectableHelper.InitInspectableItem(item); }
                catch { try { item.EnableTag("INSPECTABLE_TAG", true); } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:309", __ex); } }
                try { item.SetGameItemType("ART"); } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:310", __ex); }
                if (def.Star >= 4) { try { item.SetGameItemType("LUXURY_ITEM"); } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:311", __ex); } }

                return item;
            }
            catch (Exception e)
            {
                Debug.LogError(LogTag + "BuildFigurine 失败 " + def.Id + "：" + e.GetType().Name);
                return null;
            }
        }

        /// <summary>
        /// 掉落来源的建物入口（游戏经 `dir.Add` 的 creator 建物时走这里）：
        /// 先按正常流程建手办，再按「掉落」来源掷三态（真身 7:3、标签 25% 错标）。
        /// </summary>
        private static GameItem BuildLoot(Catalog.Def def)
        {
            var item = BuildFigurine(def);
            if (item == null) return null;
            try { FigurineAuth.Apply(item, FigurineAuth.RollFor(FigurineAuth.Source.Loot, def.Id)); }
            catch (System.Exception __ex) { Safe.Warn("ModEntry.BuildLoot", __ex); }
            return item;
        }

        private static void OnLootTablesLoaded()
        {
            try
            {
                Debug.Log(LogTag + "战利品表加载完成 → 重算掉落权重");
                LootInjection.RetryIfNeeded();
            }
            catch (Exception e) { Debug.LogWarning(LogTag + "掉落重算失败：" + e.GetType().Name); }
        }

        private static void OnGameLoaded()
        {
            try { Haizi.ResetSession(); } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:334", __ex); }
            try { Doro.ResetSession(); } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:335", __ex); }
            Debug.Log(LogTag + "OnGameLoadedNormal，已注册=" + _registered);
            try { EnsureDirectory(); } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:337", __ex); }
            // 运行时补丁自检已移除：它用反射遍历补丁目标，新版本会拒绝反射能力。

            // 调试：Mods\FigurinePort\selftest.txt 存在时，自动跑一遍鉴定面板自测
            try { FigurineSelfTest.RunIfRequested(); } catch (System.Exception __ex) { Safe.Warn("ModEntry.SelfTest", __ex); }
            // 注意：OnGameLoadedNormal 在**物品解码之前**触发（原版先 Fire 再 DecodeSaveItem），
            // 所以所有依赖"存档物品已加载"的补刷（证书检测/邮箱占格/名字/价格/可鉴定标记）
            // 都放到 OnGameLoadedLate（解码 + 特征重挂之后）执行，见 OnGameLoadedLate。
            try { FigurineEvents.EnsureRegistered(); } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:339", __ex); }
            try { TradeBoard.Invalidate(); } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:340", __ex); }
            // 掉落：首帧分组可能还没加载，读档时再试一次，并核对实际占比
            try { LootInjection.RetryIfNeeded(); } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:342", __ex); }
            try { Gacha.ResetPityCache(); } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:343", __ex); }
        }

        /// <summary>
        /// 原版 `OnGameLoadedNormal` 在物品解码**之前**触发；`OnGameLoadedLate` 在
        /// `DecodeSaveItem` + 特征重挂之后触发。所有依赖「存档物品已加载」的补刷必须放这里。
        /// </summary>
        private static void OnGameLoadedLate()
        {
            Debug.Log(LogTag + "OnGameLoadedLate：读档后补刷（名字/证书/邮箱/价格/可鉴定）");
            // 名字按标签刷新：老存档/老版本的名字可能与标签不一致（鉴定前也会读到）
            try { FigurineAuth.RefreshAllNames(); } catch (System.Exception __ex) { Safe.Warn("ModEntry.NameFix", __ex); }
            // 证书检测：必须在物品解码后，否则「档案箱里有证书」也检测不到 → 每次读档无限补发
            try { FigurineCert.EnsureCertItem(); } catch (System.Exception __ex) { Safe.Warn("ModEntry.CertEnsure", __ex); }
            // 老存档邮箱/名片描述 + 3×3 邮箱占格
            try { DoroItems.FixDescriptions(); } catch (System.Exception __ex) { Safe.Warn("ModEntry.DescFix", __ex); }
            try { DoroItems.FixShapes(); } catch (System.Exception __ex) { Safe.Warn("ModEntry.ShapeFix", __ex); }
            // 可鉴定标记/价格重算（旧版本存档）
            try { BackfillInspectable(); } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:349", __ex); }
            try { RebaseValues(); } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:352", __ex); }
#if DEBUG
            try { CleanupLegacyTickets(); } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:347", __ex); }
#endif
        }

        /// <summary>
        /// 把存档里所有手办物品的价格重算成「当前档位对应的价格」。
        /// 只在价格真的变了的时候写回，避免每帧同步。
        /// </summary>
        private static void RebaseValues()
        {
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null) return;
                int n = 0;
                foreach (var it in ps.FindAllItem(true))
                {
                    if (it == null) continue;
                    string id = null;
                    try { id = it.identifier; } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:370", __ex); }
                    if (string.IsNullOrEmpty(id)) continue;
                    if (!id.StartsWith("figurine_", StringComparison.Ordinal)) continue;
                    if (id.StartsWith("figurine_gs_const", StringComparison.Ordinal)) continue;   // 券
                    if (id.StartsWith("figurine_gs_blindbox", StringComparison.Ordinal)) continue;
                    if (id.StartsWith("figurine_gs_display", StringComparison.Ordinal)) continue;
                    if (id.StartsWith("figurine_gs_mailbox", StringComparison.Ordinal)) continue;

                    var def = Defs.Find(id);
                    if (string.IsNullOrEmpty(def.Id) || def.Value <= 0) continue;
                    try
                    {
                        if (it.unitValue == def.Value && it.unitBaseValue == def.Value) continue;
                        it.unitValue = def.Value;
                        it.unitBaseValue = def.Value;
                        it.SyncModifiedState();
                        n++;
                    }
                    catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:388", __ex); }
                }
                if (n > 0) Debug.Log(LogTag + "读档重算价格：" + n + " 件");
            }
            catch (Exception e) { Debug.LogWarning(LogTag + "重算价格失败：" + e.GetType().Name); }
        }
        /// <summary>
        /// 旧存档里的手办是**没有 INSPECTABLE_TAG** 时存下去的，读档后补一遍，
        /// 否则放大镜对它们依旧「不可鉴定」。
        /// </summary>
        private static void BackfillInspectable()
        {
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null) return;
                var all = ps.FindAllItem(true);
                if (all == null) return;

                int n = 0;
                foreach (var it in all)
                {
                    if (it == null) continue;
                    string id = null;
                    try { id = it.identifier; } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:412", __ex); }
                    if (string.IsNullOrEmpty(id) || !id.StartsWith("figurine_gs_", StringComparison.Ordinal)) continue;

                    bool has = false;
                    try { has = it.IsTag("INSPECTABLE_TAG"); } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:416", __ex); }
                    if (has) continue;

                    try { it.EnableTag("INSPECTABLE_TAG", true); it.SyncModifiedState(); n++; } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:419", __ex); }
                }
                if (n > 0) Debug.Log(LogTag + "读档补 INSPECTABLE_TAG：" + n + " 件");
            }
            catch (Exception e) { Debug.LogWarning(LogTag + "补 INSPECTABLE_TAG 失败：" + e.GetType().Name); }
        }

#if DEBUG
        /// <summary>
        /// 抽奖券已移除（抽奖改成直接扣钱）：把旧存档背包/仓库里遗留的券删掉。
        /// 只在 Debug 构建里存在 —— 发布包不需要。
        /// </summary>
        private static void CleanupLegacyTickets()
        {
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null) return;
                int killed = 0;
                foreach (var it in ps.FindAllItem(true))
                {
                    if (it == null) continue;
                    string id = null;
                    try { id = it.identifier; } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:442", __ex); }
                    if (string.IsNullOrEmpty(id)) continue;
                    if (id != "figurine_gs_const_generic" && id != "figurine_gs_const_generic_plus") continue;
                    try { it.Destroy(); killed++; } catch (System.Exception __ex) { Safe.Warn("ModEntry.cs:445", __ex); }
                }
                if (killed > 0) Debug.Log(LogTag + "读档清理：删除遗留抽奖券 " + killed + " 件");
            }
            catch (Exception e) { Debug.LogWarning(LogTag + "清理抽奖券失败：" + e.GetType().Name); }
        }
#endif
    }
}
