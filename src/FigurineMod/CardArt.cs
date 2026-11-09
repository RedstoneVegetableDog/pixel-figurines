using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 商店卡片的**高清图按需加载**。
    ///
    /// 为什么需要：
    ///   · 物品贴图是 48×48（3×3 占格、16px/格），商店卡片却要画到 ~205px，
    ///     彩绘放大后糊成一片（原神那种像素画还能看，星铁立绘就不行了）；
    ///   · 但也不能把 2048² 放进 `Textures\`——框架启动时会把每张 PNG 都解码成
    ///     RGBA32 不压缩纹理（98 张托盘图就吃过 600 MB 的亏）。
    ///
    /// 做法：高清卡片图放 `Mods\FigurinePort\Big\`（不在 `Textures\` 下，不会被预载），
    /// 卡片要显示时再读这一张，LRU 缓存 <see cref="CacheSize"/> 张，超了就销毁纹理。
    /// 现在店一页 8 张卡，缓存 12 张足够、常驻内存约 12 MB。
    /// </summary>
    internal static class CardArt
    {
        /// <summary>
        /// 缓存上限。**不淘汰**已加载的图 —— 曾经用 LRU 淘汰，
        /// 结果商店一页十几张卡时会销毁仍在显示中的纹理，卡片变成白块。
        /// 256×256 RGBA 一张约 256 KB，93 张全载也就 ~24 MB，直接全留着。
        /// </summary>
        private const int CacheSize = 200;

        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static readonly List<string> _order = new List<string>();
        private static readonly HashSet<string> _missing = new HashSet<string>();
        private static string _dir;

        public static string Dir
        {
            get
            {
                if (_dir != null) return _dir;
                _dir = ResolveDir();
                return _dir;
            }
        }

        private static string ResolveDir()
        {
            try
            {
                var mods = ModLoader.Mods;
                if (mods != null)
                {
                    foreach (var m in mods)
                    {
                        if (m == null || m.Manifest == null || m.Manifest.ID != "figurine.gs") continue;
                        if (!string.IsNullOrEmpty(m.FolderPath)) return Path.Combine(m.FolderPath, "Big");
                    }
                }
            }
            catch (System.Exception __ex) { Safe.Warn("CardArt.cs:60", __ex); }
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                if (asm != null && !string.IsNullOrEmpty(asm.Location))
                    return Path.Combine(Path.GetDirectoryName(asm.Location), "Big");
            }
            catch (System.Exception __ex) { Safe.Warn("CardArt.cs:67", __ex); }
            return "Big";
        }

        /// <summary>拿高清卡片图；没有这张文件返回 null（调用方自己回落小图）。</summary>
        public static Sprite Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            Sprite cached;
            if (_cache.TryGetValue(key, out cached) && cached != null) { Touch(key); return cached; }
            if (_missing.Contains(key)) return null;

            var sp = Load(key);
            if (sp == null) return null;

            _cache[key] = sp;
            if (!_order.Contains(key)) _order.Add(key);
            Trim(key);
            return sp;
        }

        public static int FileCount()
        {
            try { return System.IO.Directory.GetFiles(Dir, "*.png").Length; }
            catch { return 0; }
        }

        // ───────────────────────── 内部 ─────────────────────────

        private static void Touch(string key)
        {
            int i = _order.IndexOf(key);
            if (i < 0) return;
            _order.RemoveAt(i);
            _order.Add(key);
        }

        private static void Trim(string keep)
        {
            while (_order.Count > CacheSize)
            {
                string victim = null;
                for (int i = 0; i < _order.Count; i++)
                {
                    if (_order[i] == keep) continue;
                    victim = _order[i];
                    break;
                }
                if (victim == null) return;
                _order.Remove(victim);

                Sprite sp;
                if (_cache.TryGetValue(victim, out sp))
                {
                    _cache.Remove(victim);
                    try
                    {
                        var tex = sp != null ? sp.texture : null;
                        if (sp != null) UnityEngine.Object.Destroy(sp);
                        if (tex != null) UnityEngine.Object.Destroy(tex);
                    }
                    catch (System.Exception __ex) { Safe.Warn("CardArt.cs:129", __ex); }
                }
            }
        }

        private static Sprite Load(string key)
        {
            try
            {
                var path = Path.Combine(Dir, key + ".png");
                if (!File.Exists(path)) { _missing.Add(key); return null; }

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(File.ReadAllBytes(path)))
                {
                    UnityEngine.Object.Destroy(tex);
                    _missing.Add(key);
                    return null;
                }
                tex.filterMode = FilterMode.Bilinear;   // 彩绘，不要像素风锐化
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.Apply(false, false);

                var sp = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                return sp;
            }
            catch (Exception e)
            {
                Debug.LogWarning(FigurineMod.LogTag + "卡片高清图加载失败：" + key + " " + e.GetType().Name);
                _missing.Add(key);
                return null;
            }
        }
    }
}
