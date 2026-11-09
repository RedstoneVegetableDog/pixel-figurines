using System;
using HarmonyLib;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>终端机物品：右键「打开/使用」或双击 → 打开商店入口。</summary>
    internal static class TerminalItem
    {
        public const string Id = "dk_terminal";
        public const string Zh = "终端机";

        public static void Register(ModItemDirectory dir)
        {
            if (dir == null) return;
            try { dir.Add(Id, (Func<GameItem>)(() => Build())); }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "终端机注册失败：" + e.GetType().Name); }
        }

        public static GameItem Build()
        {
            try
            {
                var item = ItemDirectory.CreateEmptyItem(Id);
                if (item == null) return null;
                item.name = Zh;
                item.unitCount = 1;
                item.unitValue = 1200;
                item.unitBaseValue = 1200;
                item.SetSpriteAndShapeFromMod("figurine.gs", Id);
                try
                {
                    var sb = new GridShapeBuilder(4, 3);
                    sb.SetDataFill(4, 3, 1);
                    item.SetShape(sb.Build());
                }
                catch (System.Exception __ex) { Safe.Warn("TerminalItem.cs:37", __ex); }
                try { item.SetGameItemType("MACHINE"); } catch (System.Exception __ex) { Safe.Warn("TerminalItem.cs:38", __ex); }
                try { GeneralHelper.SetItemOwned(item, true); } catch (System.Exception __ex) { Safe.Warn("TerminalItem.cs:39", __ex); }
                return item;
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "终端机构建失败：" + e.GetType().Name); return null; }
        }

        [HarmonyPatch(typeof(ItemMouseDoubleClickHandler), "DoubleClickAction")]
        internal static class DoubleClickPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(GameItem __0)
            {
                try
                {
                    Debug.Log(FigurineMod.LogTag + "终端机双击检查 id=" + (__0 != null ? __0.identifier : "null"));
                    if (__0 != null && string.Equals(__0.identifier, Id, StringComparison.Ordinal))
                    {
                        ShopUI.Toggle();
                        return false;
                    }
                }
                catch (System.Exception __ex) { Safe.Warn("TerminalItem.cs:60", __ex); }
                return true;
            }
        }
    }
}
