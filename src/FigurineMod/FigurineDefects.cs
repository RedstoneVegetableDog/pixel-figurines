using System;

namespace FigurinePort
{
    /// <summary>（已废弃）旧的「缺件变体」部件字典。
    ///
    /// 新方案已并入 <c>FakeData.g.cs</c>（变体 1 = 反色；变体 2..N = 原神赝品立绘的部位，
    /// 由 <c>tools/gen_fake_trays.py</c> 从 `原神赝品立绘` 文件夹生成）。
    /// 鉴定面板已改为读 FakeData，本类不再参与判定，保留仅为旧接口兼容。
    /// </summary>
    internal static class FigurineDefects
    {
        /// <summary>
        /// 短 id → 缺件部件名（用户补）。例：`{ "klee", "耳饰" }`。
        /// 没填的返回 null → 鉴定面板显示占位的「缺件」。
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<string, string> Parts =
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
            {
                // 例： { "klee", "耳饰" },
            };

        /// <summary>变体 2 的缺件名；未补数据返回 null。</summary>
        public static string PartOf(GameItem item)
        {
            try
            {
                if (item == null || string.IsNullOrEmpty(item.identifier)) return null;
                string shortId = FigurineFake.TrayShortId(item.identifier);
                string part;
                return Parts.TryGetValue(shortId, out part) ? part : null;
            }
            catch (Exception __ex) { Safe.Warn("FigurineDefects.PartOf", __ex); return null; }
        }

        /// <summary>该手办是否已补缺件数据（决定变体 2 能不能真的出）。</summary>
        public static bool HasPart(GameItem item)
        {
            return !string.IsNullOrEmpty(PartOf(item));
        }
    }
}
