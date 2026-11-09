using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 展示柜（自建简化容器）：物品 4×3、最多存 8 件手办；F11 打开窗口放入/取出。
    /// 柜内手办照常计入吸引力（通过仍算"已拥有"实现：取出才恢复物品）。
    /// </summary>
    internal static class Cabinet
    {
        public const string Id = "figurine_gs_display_cabinet";

        /// <summary>展示柜容器容量：12 格宽 × 15 格长（用户 2026-11-04 定）。</summary>
        public const int CabinetW = 12;
        public const int CabinetH = 15;
        public const int Capacity = 24 * 20;   // 0.8.0-beta 内层网格 24×20
        private const string WindowId = "figurine.gs.cabinet";

        private static List<string> _items;
        private static string _hint = "";

        private static string FilePath
        {
            get { try { return Path.Combine(Application.persistentDataPath, "figurine_cabinet.txt"); } catch { return "figurine_cabinet.txt"; } }
        }

        private static int Slot
        {
            get { try { return PlayerStore.Instance != null ? PlayerStore.Instance.saveSlotId : 0; } catch { return 0; } }
        }

        /// <summary>找到场景/背包里的展示柜实例（拖拽放入用的就是它）。</summary>
        public static GameItem FindCabinet()
        {
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null) return null;
                foreach (var it in ps.FindAllItem(true))
                    if (it != null && string.Equals(it.identifier, Id, StringComparison.Ordinal)) return it;
            }
            catch (System.Exception __ex) { Safe.Warn("Cabinet.FindCabinet", __ex); }
            return null;
        }

        /// <summary>展示柜的内层库存（真实容器，玩家把东西拖进来的地方）。</summary>
        public static GameInventory ContentOf(GameItem cab)
        {
            try
            {
                var cw = cab != null ? cab.contentWindow : null;
                if (cw == null || cw.children == null || cw.children.Count == 0) return null;
                return cw.children[0] as GameInventory;
            }
            catch (System.Exception __ex) { Safe.Warn("Cabinet.ContentOf", __ex); return null; }
        }

        /// <summary>
        /// 柜内手办快照：优先读**真实容器**里的子物品（玩家直接拖进去的那些）；
        /// 容器不存在时退回旧的虚拟列表（老存档兼容）。
        /// </summary>
        public static List<string> Snapshot()
        {
            var list = new List<string>();
            try
            {
                var inv = ContentOf(FindCabinet());
                if (inv != null && inv.childItems != null)
                {
                    foreach (var it in inv.childItems)
                        if (it != null && !string.IsNullOrEmpty(it.identifier)) list.Add(it.identifier);
                    return list;
                }
            }
            catch (System.Exception __ex) { Safe.Warn("Cabinet.Snapshot", __ex); }
            try { return new List<string>(Items); } catch { return list; }
        }

        private static List<string> Items
        {            get
            {
                if (_items != null) return _items;
                _items = new List<string>();
                try
                {
                    if (ModData.GetLines("cabinet").Length > 0)
                        foreach (var line in ModData.GetLines("cabinet"))
                        {
                            if (string.IsNullOrEmpty(line)) continue;
                            var p = line.Split('|');
                            _items.Add(p.Length > 1 ? p[1] : p[0]);
                        }
                }
                catch (System.Exception __ex) { Safe.Warn("Cabinet.cs:52", __ex); }
                return _items;
            }
        }

        private static void Save()
        {
            try
            {
                var all = new List<string>();
                if (ModData.GetLines("cabinet").Length > 0)
                    foreach (var line in ModData.GetLines("cabinet"))
                    {
                        var p = line.Split('|');
                        if (p.Length >= 2 && p[0] != Slot.ToString()) all.Add(line);
                    }
                foreach (var id in Items) all.Add(id);
                ModData.Set("cabinet", string.Join("\n", all.ToArray()));
            }
            catch (System.Exception __ex) { Safe.Warn("Cabinet.cs:71", __ex); }
        }

        public static void Register(ModItemDirectory dir)
        {
            if (dir == null) return;
            try { dir.Add(Id, (Func<GameItem>)(() => Build())); } catch (System.Exception __ex) { Safe.Warn("Cabinet.cs:77", __ex); }
        }

        public static GameItem Build()
        {
            try
            {
                GameItem item = null;
                try { item = DirectoryMaster.Create<GameItem>("storage_bay_large"); } catch (System.Exception __ex) { Safe.Warn("Cabinet.cs:85", __ex); }
                if (item == null) item = ItemDirectory.CreateEmptyItem(Id);
                try { item.identifier = Id; } catch (System.Exception __ex) { Safe.Warn("Cabinet.cs:87", __ex); }
                // 容量：照用户 2026-11-04 的定值 —— 12 格宽 × 15 格长
                try { DoroItems.ResizeContainer(item, CabinetW, CabinetH, "展示柜"); } catch (System.Exception __ex) { Safe.Warn("Cabinet.Resize", __ex); }

                if (item == null) return null;
                item.name = "手办收纳柜";
                item.unitCount = 1;
                item.unitValue = 800;
                item.unitBaseValue = 800;
                try { item.shortDescription = "专门用来收纳手办的柜子（只储物，不提供吸引力）"; } catch (System.Exception __ex) { Safe.Warn("Cabinet.cs:94", __ex); }
                item.SetSpriteAndShapeFromMod("figurine.gs", Id);
                try
                {
                    var sb = new GridShapeBuilder(4, 3);
                    sb.SetDataFill(4, 3, 1);
                    var built = sb.Build();
                    item.SetShape(built);
                    // 克隆容器的 modifiedShape 还是原版尺寸，不一起改就会「占格和贴图对不上」
                    try { item.modifiedShape = built.Clone(); } catch (System.Exception __ex) { Safe.Warn("Cabinet.modifiedShape", __ex); }
                    try { item.SyncModifiedState(); } catch (System.Exception __ex) { Safe.Warn("Cabinet.SyncModifiedState", __ex); }
                    Debug.Log(FigurineMod.LogTag + "收纳柜形状：shape=" + item.shape.width + "x" + item.shape.height
                        + " modifiedShape=" + (item.modifiedShape != null ? item.modifiedShape.width + "x" + item.modifiedShape.height : "null"));
                }
                catch (System.Exception __ex) { Safe.Warn("Cabinet.cs:102", __ex); }

                try
                {
                    var feats = item.itemFeatures;
                    if (feats != null)
                    {
                        for (int i = feats.Count - 1; i >= 0; i--)
                        {
                            var f = feats[i];
                            if (f == null) continue;
                            string disp = null;
                            try { disp = f.GetPublicDisplay(); } catch (System.Exception __ex) { Safe.Warn("Cabinet.cs:114", __ex); }
                            string fid = null;
                            try { fid = f.identifier; } catch (System.Exception __ex) { Safe.Warn("Cabinet.cs:116", __ex); }
                            if ((disp != null && disp.IndexOf("切换", StringComparison.Ordinal) >= 0) ||
                                (fid != null && fid.IndexOf("toggle", StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                try { item.RemoveItemFeatureByID(fid); } catch (System.Exception __ex) { Safe.Warn("Cabinet.cs:120", __ex); }
                                Debug.Log(FigurineMod.LogTag + "展示柜已移除切换特性 " + fid);
                            }
                            // 用户 2026-11-04：展示柜不再额外加吸引力 → 把容器自带的吸引力特性也摘掉
                            else if (disp != null && disp.IndexOf("吸引力", StringComparison.Ordinal) >= 0)
                            {
                                try { item.RemoveItemFeatureByID(fid); } catch (System.Exception __ex) { Safe.Warn("Cabinet.cs:吸引力", __ex); }
                                Debug.Log(FigurineMod.LogTag + "展示柜已移除吸引力特性 " + fid + "（" + disp + "）");
                            }
                        }
                    }
                }
                catch (System.Exception __ex) { Safe.Warn("Cabinet.cs:126", __ex); }
                try { GeneralHelper.SetItemOwned(item, true); } catch (System.Exception __ex) { Safe.Warn("Cabinet.cs:127", __ex); }
                return item;
            }
            catch { return null; }
        }

        // 用户 2026-11-04：取消「展示柜额外加吸引力」——柜内手办按普通持有计入档位封顶即可，
        // 不再有柜子那份额外加成（原来的 AttractionBonus 已删）。


        public static void Toggle()
        {
            try
            {
                var mgr = CustomUIManager.Instance;
                if (mgr == null) return;
                if (mgr.GetWindow(WindowId) != null) { mgr.CloseWindow(WindowId); return; }
                Show();
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "展示柜异常：" + e.GetType().Name); }
        }

        private static void Show()
        {
            var contents = Snapshot();
            bool real = ContentOf(FindCabinet()) != null;
            var b = CustomUIManager.Window(WindowId, "手办收纳柜", CustomUIManager.LAYER_OVERLAY)
                .SetSize(560, 460)
                .Center()
                .AddLabel($"柜内 {contents.Count} 件" + (real ? "（直接把背包里的手办拖到展示柜上即可）" : $" / {UsedCells()}/{Capacity} 格"))
                .AddLabel(_hint);

            if (contents.Count == 0)
                b.AddLabel("（空）");
            else
            {
                var shown = new List<string>(contents);
                for (int i = 0; i < shown.Count; i++)
                {
                    string id = shown[i];
                    var def = FindDef(id);
                    b.BeginRow()
                        .AddLabel((def.Id != null ? def.Zh : id))
                        .AddButton("取出", () =>
                        {
                            var item = def.Id != null ? FigurineMod.BuildFigurine(def) : null;
                            var inv = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.invElement : null;
                            if (item != null && FigurineMod.TryPlace(inv, item))
                            {
                                Items.Remove(id);
                                Save();
                                CustomUIManager.Instance.CloseWindow(WindowId);
                                Show();
                            }
                        })
                        .End();
                }
            }

            b.BeginRow()
                .AddButton("放入一件手办", () => PutOne())
                .AddButton("关闭", () => CustomUIManager.Instance.CloseWindow(WindowId))
                .End()
                .Show();
        }

        private static void PutOne()
        {
            try
            {
                var ps = PlayerStore.Instance;
                Debug.Log(FigurineMod.LogTag + "展示柜放入：ps=" + (ps != null));
                if (ps == null) return;
                foreach (var it in ps.FindAllItem(true))
                {
                    if (it == null) continue;
                    Debug.Log(FigurineMod.LogTag + "展示柜扫描物品 " + it.identifier + " catalog=" + Catalog.All.Length);
                    var def = FindDef(it.identifier);
                    if (def.Id == null) continue;
                    if (UsedCells() + Math.Max(1, def.W * def.H) > Capacity) continue;
                    Debug.Log(FigurineMod.LogTag + "展示柜放入候选 " + it.identifier + " cells=" + (def.W * def.H));
                    Items.Add(it.identifier);
                    Save();
                    it.Destroy();
                    _hint = "";
                    CustomUIManager.Instance.CloseWindow(WindowId);
                    Show();
                    return;
                }
            }
            catch (System.Exception __ex) { Safe.Warn("Cabinet.cs:223", __ex); }
            _hint = "背包里没有手办：先去仓库取出，再放进展柜。";
            CustomUIManager.Instance.CloseWindow(WindowId);
            Show();
        }

        private static int CellsOf(string id)
        {
            var d = FindDef(id);
            return d.Id != null ? Math.Max(1, d.W * d.H) : 1;
        }

        private static int UsedCells()
        {
            int sum = 0;
            foreach (var id in Items) sum += CellsOf(id);
            return sum;
        }

        private static Catalog.Def FindDef(string id)
        {
            { var dd = Defs.Find(id); if (!string.IsNullOrEmpty(dd.Id)) return dd; }
            return default;
        }
    }
}
