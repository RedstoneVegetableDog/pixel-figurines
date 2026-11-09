using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 手办稀有度档位（用户 2026-11-03 指定：按 CS 式五档命名 + 五种颜色）。
    ///
    /// ```
    /// 1★ 消费 Consumer      灰白  #D9DADF
    /// 2★ 军规 Mil-Spec      深蓝  #2A4A8C
    /// 3★ 受限 Restricted    亮紫  #9C4DFF
    /// 4★ 隐秘 Covert        红    #D6332F
    /// 5★ 违禁 Contraband    黄    #F0BC1C
    /// ```
    ///
    /// 全模组的星级文案/配色都从这里取，别再各处写死颜色。
    /// </summary>
    internal static class Tier
    {
        public static readonly string[] Name = { "消费", "军规", "受限", "隐秘", "违禁" };
        public static readonly string[] NameEn = { "Consumer", "Mil-Spec", "Restricted", "Covert", "Contraband" };

        private static readonly string[] HexCode = { "#D9DADF", "#2A4A8C", "#9C4DFF", "#D6332F", "#F0BC1C" };
        private static readonly Color[] UiColor =
        {
            new Color(0.851f, 0.855f, 0.875f),
            new Color(0.165f, 0.290f, 0.549f),
            new Color(0.612f, 0.302f, 1.000f),
            new Color(0.839f, 0.200f, 0.184f),
            new Color(0.941f, 0.737f, 0.110f),
        };

        public static int Idx(int star)
        {
            if (star < 1) star = 1;
            if (star > 5) star = 5;
            return star - 1;
        }

        /// <summary>中文档位名：消费 / 军规 / 受限 / 隐秘 / 违禁。</summary>
        public static string Of(int star) { return Name[Idx(star)]; }

        /// <summary>中文 + 英文：`消费 Consumer`。</summary>
        public static string OfFull(int star) { return Name[Idx(star)] + " " + NameEn[Idx(star)]; }

        /// <summary>带括号的标注：`【违禁】`（工具提示/开盒动画都不带英文）。</summary>
        public static string Label(int star) { return "【" + Name[Idx(star)] + "】"; }

        public static string Hex(int star) { return HexCode[Idx(star)]; }

        public static Color Ui(int star) { return UiColor[Idx(star)]; }

        /// <summary>工具提示用的调色板（原版富文本只认这个枚举）。</summary>
        public static RenderHandler.ColorPalette Palette(int star)
        {
            switch (Idx(star) + 1)
            {
                case 5: return RenderHandler.ColorPalette.Yellow;
                case 4: return RenderHandler.ColorPalette.Red;
                case 3: return RenderHandler.ColorPalette.LightPurple;
                case 2: return RenderHandler.ColorPalette.DarkBlue;
                default: return RenderHandler.ColorPalette.VeryLightGray;
            }
        }
    }
}
