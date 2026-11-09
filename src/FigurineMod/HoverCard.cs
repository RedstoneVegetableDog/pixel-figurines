using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace FigurinePort
{
    /// <summary>
    /// 入口页卡片的悬停反馈：鼠标移上去图标轻微放大 + 名字变金色，移开还原。
    ///
    /// 顺带把「点击范围」撑满整张卡：卡片自己挂一个**全透明 Image** 专门吃射线，
    /// 这样点图标以外的地方（卡片空白处）也能触发按钮 ——
    /// 之前只有图标/文字那一小块有 Graphic，点边缘是没反应的。
    /// </summary>
    internal class HoverCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public RectTransform icon;
        public TextMeshProUGUI label;

        private static readonly Color HoverText = new Color(1f, 0.84f, 0.37f, 1f);
        private static readonly Color NormalText = Color.white;
        private const float HoverScale = 1.07f;

        public void OnPointerEnter(PointerEventData e)
        {
            try
            {
                if (icon != null) icon.localScale = new Vector3(HoverScale, HoverScale, 1f);
                if (label != null) label.color = HoverText;
            }
            catch (System.Exception __ex) { Safe.Warn("HoverCard.cs:31", __ex); }
        }

        public void OnPointerExit(PointerEventData e)
        {
            try
            {
                if (icon != null) icon.localScale = Vector3.one;
                if (label != null) label.color = NormalText;
            }
            catch (System.Exception __ex) { Safe.Warn("HoverCard.cs:41", __ex); }
        }
    }
}
