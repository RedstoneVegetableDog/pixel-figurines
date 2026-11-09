using System;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>把星穹铁道 11 位注册成手办物品（占位立绘，3×3）。</summary>
    internal static class StarRailItems
    {
        public static void Register(ModItemDirectory dir)
        {
            if (dir == null) return;
            int n = 0;
            foreach (var e in StarRailData.All)
            {
                var entry = e;
                try
                {
                    if (dir.Add(entry.Id, (Func<GameItem>)(() => FigurineAuth.Loot(Build(entry))))) n++;
                }
                catch (Exception ex) { Debug.LogError(FigurineMod.LogTag + "星铁注册失败：" + entry.Id + " " + ex.GetType().Name); }
            }
            Debug.Log(FigurineMod.LogTag + "星铁手办注册 " + n + "/" + StarRailData.All.Count);
        }

        public static GameItem Build(StarRailData.Entry def)
        {
            try
            {
                var item = ItemDirectory.CreateEmptyItem(def.Id);
                if (item == null) return null;
                item.name = def.Zh;
                item.unitCount = 1;
                item.unitValue = def.Value;
                item.unitBaseValue = def.Value;
                item.SetSpriteAndShapeFromMod("figurine.gs", def.Id);
                try
                {
                    var sb = new GridShapeBuilder(3, 3);
                    sb.SetDataFill(3, 3, 1);
                    item.SetShape(sb.Build());
                }
                catch (System.Exception __ex) { Safe.Warn("StarRailItems.cs:42", __ex); }
                try { item.SetGameItemType("ART"); } catch (System.Exception __ex) { Safe.Warn("StarRailItems.cs:43", __ex); }
                if (StarRailData.IsLuxury(def.Star)) { try { item.SetGameItemType("LUXURY_ITEM"); } catch (System.Exception __ex) { Safe.Warn("StarRailItems.cs:44", __ex); } }
                // 和原神/绝区零一致：不打 INSPECTABLE_TAG，放大镜判定「不可检查」
                try { InspectableHelper.InitInspectableItem(item); }
                catch { try { item.EnableTag("INSPECTABLE_TAG", true); } catch (System.Exception __ex) { Safe.Warn("StarRailItems.cs:46", __ex); } }
                try { GeneralHelper.SetItemOwned(item, true); } catch (System.Exception __ex) { Safe.Warn("StarRailItems.cs:45", __ex); }
                return item;
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "星铁构建失败：" + e.GetType().Name); return null; }
        }
    }
}
