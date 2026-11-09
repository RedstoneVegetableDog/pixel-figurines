namespace FigurinePort
{
    /// <summary>
    /// 打包开关。
    ///
    /// 用户 2026-11-03 要求：**发布包屏蔽「赝品」相关玩法**（放大镜鉴定仍然可用）。
    /// 做法是编译期开关，不是运行时开关——Release 构建会定义 `NO_FAKES`，
    /// 于是赝品生成/对质/赝品估价这些代码整段被关掉（Debug 构建仍然保留，方便自己测）。
    ///
    /// 受影响：
    /// ```
    /// · 卖方顾客不再带赝品（CustomerSellers 的 20% 掷骰）
    /// · 以物易物不再出赝品（Barter 的 25% 掷骰）
    /// · F10 对质面板直接拒绝
    /// · FigurineFake.IsFake 恒为 false → 工具提示/估价/放大镜一律按正品走
    /// ```
    /// 不受影响：放大镜（InspectorView 仍显示托盘立绘，只是永远是正品图）。
    /// </summary>
    internal static class FeatureFlags
    {
#if NO_FAKES
        /// <summary>赝品玩法总开关（Release 包 = false）。</summary>
        public const bool Fakes = false;
#else
        /// <summary>赝品玩法总开关（Debug 包 = true）。</summary>
        public const bool Fakes = true;
#endif
    }
}
