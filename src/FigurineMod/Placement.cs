using System;

namespace FigurinePort
{
    /// <summary>
    /// 柜台摆放兜底（2026-11-05）：
    /// 原版 `AddDirectSellingItemToTableMiddle` 内部是 `TryAcceptAllMid` + `DestroyIfUnplaced`，
    /// 中间区域被占/放不下时会**静默销毁**物品（日志只留一行 Destroyed unplaced items）。
    /// 这里统一成「中间优先 → 普通位置兜底 → 返回是否摆上」，调用方负责失败日志/重试。
    /// </summary>
    internal static class Placement
    {
        public static bool TryFront(GameItem item, bool owned = false, bool preferMiddle = true)
        {
            try
            {
                if (item == null) return false;
                var ee = EmporiumEntry.Instance;
                if (ee == null) return false;
                GeneralHelper.SetItemOwned(item, owned);
                if (preferMiddle)
                {
                    GraphUtils.TryAcceptAllMid(ee.frontInvinvElement, item);
                    if (item.parentInventory != null) return true;
                }
                int left = GraphUtils.TryAcceptAll(ee.frontInvinvElement, item);
                return left == 0 && item.parentInventory != null;
            }
            catch (Exception e)
            {
                Safe.Warn("Placement.TryFront", e);
                return false;
            }
        }
    }
}
