using TMPro;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>余额数字短暂变色（扣款红色 / 增加绿色）。</summary>
    internal class CashFlash : MonoBehaviour
    {
        private TextMeshProUGUI _text;
        private Color _normal = new Color(0.88f, 0.91f, 0.97f, 1f);
        private float _until;

        public void Init(TextMeshProUGUI text)
        {
            _text = text;
            try { _normal = text.color; } catch (System.Exception __ex) { Safe.Warn("CashFlash.cs:16", __ex); }
        }

        public void Flash(Color color, float seconds = 0.9f)
        {
            try
            {
                if (_text == null) return;
                _text.color = color;
                _until = Time.unscaledTime + seconds;
            }
            catch (System.Exception __ex) { Safe.Warn("CashFlash.cs:27", __ex); }
        }

        private void Update()
        {
            try
            {
                if (_text == null || _until <= 0f) return;
                if (Time.unscaledTime >= _until)
                {
                    _text.color = _normal;
                    _until = 0f;
                }
            }
            catch (System.Exception __ex) { Safe.Warn("CashFlash.cs:41", __ex); }
        }
    }
}
