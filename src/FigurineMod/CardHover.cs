using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FigurinePort
{
    /// <summary>卡片悬停：底色变亮 + 轻微放大。</summary>
    internal class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Image _bg;
        private Color _base;
        private Color _hover;
        private RectTransform _rt;

        public void Init(Image bg, Color baseColor, Color hoverColor, RectTransform rt)
        {
            _bg = bg;
            _base = baseColor;
            _hover = hoverColor;
            _rt = rt;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            try
            {
                if (_bg != null) _bg.color = _hover;
                if (_rt != null) _rt.localScale = new Vector3(1.03f, 1.03f, 1f);
            }
            catch (System.Exception __ex) { Safe.Warn("CardHover.cs:30", __ex); }
        }

        public void OnPointerExit(PointerEventData e)
        {
            try
            {
                if (_bg != null) _bg.color = _base;
                if (_rt != null) _rt.localScale = Vector3.one;
            }
            catch (System.Exception __ex) { Safe.Warn("CardHover.cs:40", __ex); }
        }
    }
}
