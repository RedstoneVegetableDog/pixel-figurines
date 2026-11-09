using System;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>DORO 相关道具：名片（2×1）、货物邮箱（2×2，占位容器）。</summary>
    internal static class DoroItems
    {
        public const string CardId = "dk_doro_card";
        public const string MailboxId = "figurine_gs_mailbox";

        /// <summary>货物邮箱容器容量：9 格宽 × 12 格长（用户 2026-11-04 定）。</summary>
        public const int MailboxW = 9;
        public const int MailboxH = 12;

        /// <summary>把克隆来的容器（storage_bay_large）内部网格改成指定格数。</summary>
        public static void ResizeContainer(GameItem item, int w, int h, string label = "容器")
        {
            try
            {
                var cw = item != null ? item.contentWindow : null;
                if (cw == null || cw.children == null || cw.children.Count == 0) return;
                var inv = cw.children[0] as GameInventory;
                if (inv == null) return;

                var grid = inv as GameGridInventory;
                if (grid == null)
                {
                    Debug.LogWarning(FigurineMod.LogTag + "邮箱：内容库存不是 GameGridInventory，跳过改容量");
                    return;
                }
                // 公开 API：GameGridInventory.SetShape(width,height)（2026-11-10 去反射）
                grid.SetShape(w, h);
                try { grid.Validate(); } catch (System.Exception __ex) { Safe.Warn("DoroItems.Validate", __ex); }
                Debug.Log(FigurineMod.LogTag + label + "容量：" + w + "×" + h + " 格");
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "改邮箱容量失败：" + e.GetType().Name + " " + e.Message); }
        }

        public static void Register(ModItemDirectory dir)
        {
            if (dir == null) return;
            try { dir.Add(CardId, (Func<GameItem>)(() => Build(CardId, "DORO 名片", 0, 2, 1))); } catch (System.Exception __ex) { Safe.Warn("DoroItems.cs:15", __ex); }
            try { dir.Add(MailboxId, (Func<GameItem>)(() => Build(MailboxId, "货物邮箱", 400, 2, 2))); } catch (System.Exception __ex) { Safe.Warn("DoroItems.cs:16", __ex); }
        }

        public static GameItem Build(string id, string zh, int value, int w, int h)
        {
            try
            {
                // 货物邮箱贴图是 2×2：无论调用方传什么，占格都强制 2×2（2026-11-06 用户报「2×2 贴图 3×3 位置」）
                if (id == MailboxId) { w = 2; h = 2; }
                GameItem item = null;
                if (id == MailboxId)
                {
                    try { item = DirectoryMaster.Create<GameItem>("storage_bay_large"); } catch (System.Exception __ex) { Safe.Warn("DoroItems.cs:26", __ex); }
                    if (item != null) { try { item.identifier = id; } catch (System.Exception __ex) { Safe.Warn("DoroItems.cs:27", __ex); } }
                    // 容量：照用户 2026-11-04 的定值 —— 9 格宽 × 12 格长
                    try { ResizeContainer(item, MailboxW, MailboxH, "货物邮箱"); } catch (System.Exception __ex) { Safe.Warn("DoroItems.Resize", __ex); }
                }
                if (item == null) item = ItemDirectory.CreateEmptyItem(id);
                if (item == null) return null;
                item.name = zh;
                item.unitCount = 1;
                item.unitValue = value;
                item.unitBaseValue = value;
                // 容器是克隆 storage_bay_large 来的，描述会带原版的「扩建后的物品存放区域。」
                // 给每个道具写自己的描述（用户 2026-11-04：货物邮箱介绍不对）
                try
                {
                    if (id == MailboxId) item.shortDescription = "DORO 寄货用的邮箱。寄出的货会先放到这里，双击打开。";
                    else if (id == CardId) item.shortDescription = "DORO 的名片。";
                }
                catch (System.Exception __ex) { Safe.Warn("DoroItems.cs:desc", __ex); }
                item.SetSpriteAndShapeFromMod("figurine.gs", id);
                // 名片：占格完全交给贴图推导，避免「精灵画在一处、占格在另一处」的错位
                if (id != CardId)
                {
                    try
                    {
                        var sb = new GridShapeBuilder(w, h);
                        sb.SetDataFill(w, h, 1);
                        var built = sb.Build();
                        // 容器是从 storage_bay_large 克隆来的，它的 modifiedShape 还留着原版尺寸（实测 3×3），
                        // 只改 shape 不改 modifiedShape 的话，格子里显示的还是 3×3 → 和 2×2 的贴图对不上。
                        item.SetShape(built);
                        try { item.modifiedShape = built.Clone(); } catch (System.Exception __ex) { Safe.Warn("DoroItems.modifiedShape", __ex); }
                        try { item.SyncModifiedState(); } catch (System.Exception __ex) { Safe.Warn("DoroItems.SyncModifiedState", __ex); }
                        if (id == MailboxId)
                            Debug.Log(FigurineMod.LogTag + "邮箱形状：shape=" + item.shape.width + "x" + item.shape.height
                                + " modifiedShape=" + (item.modifiedShape != null ? item.modifiedShape.width + "x" + item.modifiedShape.height : "null"));
                    }
                    catch (System.Exception __ex) { Safe.Warn("DoroItems.cs:45", __ex); }
                }
                try { if (id == CardId) { try { item.SetGameItemType("DOCUMENT"); } catch { try { item.EnableTag("DOCUMENT", true); } catch (System.Exception __ex) { Safe.Warn("DoroItems.cs:47", __ex); } } } } catch (System.Exception __ex) { Safe.Warn("DoroItems.cs:47", __ex); }
                try { GeneralHelper.SetItemOwned(item, true); } catch (System.Exception __ex) { Safe.Warn("DoroItems.cs:48", __ex); }
                return item;
            }
            catch { return null; }
        }

        /// <summary>老存档里已经建出来的邮箱/名片：描述还是克隆来的「扩建后的物品存放区域。」，
        /// 读档时统一改成自己的描述。</summary>
        public static void FixDescriptions()
        {
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null) return;
                foreach (var it in ps.FindAllItem(true))
                {
                    if (it == null) continue;
                    try
                    {
                        if (it.identifier == MailboxId)
                            it.shortDescription = "DORO 寄货用的邮箱。寄出的货会先放到这里，双击打开。";
                        else if (it.identifier == CardId)
                            it.shortDescription = "DORO 的名片。";
                    }
                    catch (System.Exception __ex) { Safe.Warn("DoroItems.FixDesc.item", __ex); }
                }
            }
            catch (System.Exception __ex) { Safe.Warn("DoroItems.FixDescriptions", __ex); }
        }

        /// <summary>老存档里按 3×3 建出来的货物邮箱：读档时统一改回 2×2（贴图是 2×2）。</summary>
        public static void FixShapes()
        {
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null) return;
                int fixedCount = 0;
                foreach (var it in ps.FindAllItem(false))
                {
                    if (it == null || !string.Equals(it.identifier, MailboxId, StringComparison.Ordinal)) continue;
                    try
                    {
                        var sb = new GridShapeBuilder(2, 2);
                        sb.SetDataFill(2, 2, 1);
                        var sh = sb.Build();
                        it.SetShape(sh);
                        try { it.modifiedShape = sh.Clone(); } catch (System.Exception __ex) { Safe.Warn("DoroItems.FixShape.mod", __ex); }
                        try { it.SyncModifiedState(); } catch (System.Exception __ex) { Safe.Warn("DoroItems.FixShape.sync", __ex); }
                        fixedCount++;
                    }
                    catch (System.Exception __ex) { Safe.Warn("DoroItems.FixShape.item", __ex); }
                }
                if (fixedCount > 0)
                    Debug.Log(FigurineMod.LogTag + "货物邮箱占格已修正为 2×2（" + fixedCount + " 件）");
            }
            catch (System.Exception __ex) { Safe.Warn("DoroItems.FixShapes", __ex); }
        }

        public static bool HasCard()
        {
            // 卡片可能被摆在柜台/桌上（不在背包里），存档里记过「已发放」也算持有
            try { if (ModData.Get("doro_card", "") == "1") return true; } catch (System.Exception __ex) { Safe.Warn("DoroItems.cs:57", __ex); }
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null) return false;
                foreach (var it in ps.FindAllItem(true))
                    if (it != null && string.Equals(it.identifier, CardId, StringComparison.Ordinal)) return true;
            }
            catch (System.Exception __ex) { Safe.Warn("DoroItems.cs:65", __ex); }
            return false;
        }

        /// <summary>是否持有 DORO 终端机（仓库扩容的前置条件）。</summary>
        public static bool HasTerminal()
        {
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null) return false;
                foreach (var it in ps.FindAllItem(true))
                    if (it != null && string.Equals(it.identifier, TerminalItem.Id, StringComparison.Ordinal)) return true;
            }
            catch (System.Exception __ex) { Safe.Warn("DoroItems.cs:79", __ex); }
            return false;
        }
        /// <summary>是否持有货物邮箱。</summary>
        public static bool HasMailbox()
        {
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null) return false;
                foreach (var it in ps.FindAllItem(true))
                    if (it != null && string.Equals(it.identifier, MailboxId, StringComparison.Ordinal)) return true;
            }
            catch (System.Exception __ex) { Safe.Warn("DoroItems.cs:92", __ex); }
            return false;
        }
    }
}
