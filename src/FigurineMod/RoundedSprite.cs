using UnityEngine;

namespace FigurinePort
{
    /// <summary>生成一张白色圆角矩形 Sprite（9-slice），用于卡片/面板圆角。</summary>
    internal static class RoundedSprite
    {
        private static Sprite _card;
        private static Sprite _pixel;
        private static Sprite _gradient;

        public static Sprite Card
        {
            get
            {
                if (_card == null) _card = Create(64, 18f);
                return _card;
            }
        }

        public static Sprite Gradient
        {
            get
            {
                if (_gradient == null) _gradient = CreateGradient(32, 6);
                return _gradient;
            }
        }

        public static Sprite CreateGradient(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float t = y / (float)(size - 1);
                var c = Color.Lerp(new Color(0.09f, 0.10f, 0.15f), new Color(0.165f, 0.19f, 0.28f), t);
                for (int x = 0; x < size; x++)
                {
                    int cx = Mathf.Min(x, size - 1 - x);
                    int cy = Mathf.Min(y, size - 1 - y);
                    bool inside = true;
                    if (cx < radius && cy < radius)
                    {
                        int dx = radius - cx, dy = radius - cy;
                        inside = dx * dx + dy * dy <= radius * radius;
                    }
                    px[y * size + x] = inside ? (Color32)c : new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            var border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        public static Sprite Pixel
        {
            get
            {
                if (_pixel == null) _pixel = CreatePixel(16, 5);
                return _pixel;
            }
        }

        public static Sprite CreatePixel(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int cx = Mathf.Min(x, size - 1 - x);
                    int cy = Mathf.Min(y, size - 1 - y);
                    bool inside = true;
                    if (cx < radius && cy < radius)
                    {
                        int dx = radius - cx, dy = radius - cy;
                        inside = dx * dx + dy * dy <= radius * radius;
                    }
                    px[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            var border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        public static Sprite Create(int size, float radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            float r = radius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Min(x + 0.5f, size - x - 0.5f);
                    float cy = Mathf.Min(y + 0.5f, size - y - 0.5f);
                    float a = 1f;
                    if (cx < r && cy < r)
                    {
                        float dx = r - cx, dy = r - cy;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        a = Mathf.Clamp01(r - d + 0.5f);
                    }
                    byte alpha = (byte)Mathf.RoundToInt(a * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            var border = new Vector4(r, r, r, r);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }
    }
}
