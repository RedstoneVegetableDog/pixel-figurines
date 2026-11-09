using System;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>把绝区零 58 位注册成手办物品（3×3 占格，和原神/星铁一致）。</summary>
    internal static class ZzzItems
    {
        public static void Register(ModItemDirectory dir)
        {
            if (dir == null) return;
            int n = 0;
            foreach (var e in ZzzData.All)
            {
                var entry = e;
                try
                {
                    if (dir.Add(entry.Id, (Func<GameItem>)(() => FigurineAuth.Loot(Build(entry))))) n++;
                }
                catch (Exception ex) { Debug.LogError(FigurineMod.LogTag + "绝区零注册失败：" + entry.Id + " " + ex.GetType().Name); }
            }
            Debug.Log(FigurineMod.LogTag + "绝区零手办注册 " + n + "/" + ZzzData.All.Count);
        }

        public static GameItem Build(ZzzData.Entry def)
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
                catch (System.Exception __ex) { Safe.Warn("ZzzItems.cs:42", __ex); }
                try { item.SetGameItemType("ART"); } catch (System.Exception __ex) { Safe.Warn("ZzzItems.cs:43", __ex); }
                if (ZzzData.IsLuxury(def.Star)) { try { item.SetGameItemType("LUXURY_ITEM"); } catch (System.Exception __ex) { Safe.Warn("ZzzItems.cs:44", __ex); } }
                try { InspectableHelper.InitInspectableItem(item); } catch { try { item.EnableTag("INSPECTABLE_TAG", true); } catch (System.Exception __ex) { Safe.Warn("ZzzItems.cs:45", __ex); } }
                try { GeneralHelper.SetItemOwned(item, true); } catch (System.Exception __ex) { Safe.Warn("ZzzItems.cs:46", __ex); }
                return item;
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "绝区零构建失败：" + e.GetType().Name); return null; }
        }
    }
}
