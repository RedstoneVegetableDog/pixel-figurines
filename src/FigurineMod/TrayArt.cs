using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 托盘立绘的**按需加载**。
    ///
    /// 背景：0.8.0-beta 的托盘图是原尺寸（1254×1254 / 1024×1536 …），98 张共 41 MB。
    /// 正式版框架的 `ModLoader` 会把 `Textures\Items\` 下**每张 PNG 在启动时全部解码成
    /// RGBA32 不压缩纹理**（15412 万像素 ≈ 588 MB 常驻，实测 WorkingSet +609 MB），
    /// 所以托盘图不能放 `Textures\` 里，而是放在模组根目录的 `Tray\`，用的时候再读。
    ///
    /// 目录布局：
    /// ```
    /// Mods\FigurinePort\
    ///   FigurineMod.dll
    ///   Tray\  figurine_tray_<短id>.png
    ///          figurine_tray_<短id>_fakeN.png
    ///   Textures\Items\  ← 只放物品贴图（小图）
    /// ```
    ///
    /// 缓存：最多同时留住 <see cref="CacheSize"/> 张，超出就按「最久没用且不是当前这张」淘汰并销毁纹理。
    /// 现在放大镜一次只显示一张，所以常驻内存 = 一张 ~6 MB。
    /// </summary>
    internal static class TrayArt
    {
        private const int CacheSize = 3;

        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static readonly List<string> _order = new List<string>();
        private static readonly HashSet<string> _missingLogged = new HashSet<string>();
        private static string _dir;

        /// <summary>托盘图目录：`<模组目录>\Tray`。</summary>
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
                        if (!string.IsNullOrEmpty(m.FolderPath)) return Path.Combine(m.FolderPath, "Tray");
                    }
                }
            }
            catch (System.Exception __ex) { Safe.Warn("TrayArt.cs:63", __ex); }
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                if (asm != null && !string.IsNullOrEmpty(asm.Location))
                    return Path.Combine(Path.GetDirectoryName(asm.Location), "Tray");
            }
            catch (System.Exception __ex) { Safe.Warn("TrayArt.cs:70", __ex); }
            return "Tray";
        }

        /// <summary>取托盘图；没有这张文件返回 null（调用方自己决定回落到什么）。</summary>
        /// <summary>
        /// 托盘图路径，按 IP 分目录：
        /// ```
        /// Tray\Genshin\     原神（含 Genshin\Fake\ 赝品）
        /// Tray\StarRail\    星穹铁道
        /// Tray\Zzz\         绝区零
        /// ```
        /// </summary>
        private static string PathOf(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            bool fake = key.IndexOf("_fake", StringComparison.Ordinal) >= 0;
            string sub;
            if (key.StartsWith("figurine_tray_sr_", StringComparison.Ordinal)) sub = "StarRail";
            else if (key.StartsWith("figurine_tray_zzz_", StringComparison.Ordinal)) sub = "Zzz";
            else sub = "Genshin";

            string dir = Path.Combine(Dir, sub);
            if (fake) dir = Path.Combine(dir, "Fake");
            return Path.Combine(dir, key + ".png");
        }
        public static Sprite Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            Sprite cached;
            if (_cache.TryGetValue(key, out cached) && cached != null)
            {
                Touch(key);
                return cached;
            }

            var sp = Load(key);
            if (sp == null) return null;

            _cache[key] = sp;
            if (!_order.Contains(key)) _order.Add(key);
            Trim(key);
            return sp;
        }

        /// <summary>文件是否在（不加载）。</summary>
        public static bool Exists(string key)
        {
            try { var pth = PathOf(key); return pth != null && File.Exists(pth); }
            catch { return false; }
        }

        /// <summary>目录里的托盘图张数（自检用）。</summary>
        /// <summary>托盘图里赝品（Fake\ 目录下）的数量，用于自检。</summary>
        public static int FakeFileCount()
        {
            try
            {
                int n = 0;
                foreach (var f in System.IO.Directory.GetFiles(Dir, "*.png", System.IO.SearchOption.AllDirectories))
                    if (f.IndexOf("\\Fake\\", StringComparison.OrdinalIgnoreCase) >= 0) n++;
                return n;
            }
            catch { return 0; }
        }

        public static int FileCount()
        {
            try
            {
                // 按 IP 分了子目录（Genshin / StarRail / Zzz + 各自 Fake），所以递归数
                return System.IO.Directory.GetFiles(Dir, "*.png", System.IO.SearchOption.AllDirectories).Length;
            }
            catch { return 0; }
        }

        public static void Clear()
        {
            foreach (var kv in _cache) DestroySprite(kv.Value);
            _cache.Clear();
            _order.Clear();
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
                    DestroySprite(sp);
                }
            }
        }

        private static void DestroySprite(Sprite sp)
        {
            try
            {
                if (sp == null) return;
                var tex = sp.texture;
                UnityEngine.Object.Destroy(sp);
                if (tex != null) UnityEngine.Object.Destroy(tex);
            }
            catch (System.Exception __ex) { Safe.Warn("TrayArt.cs:183", __ex); }
        }

        private static Sprite Load(string key)
        {
            string path = null;
            try
            {
                path = PathOf(key);
                if (!File.Exists(path))
                {
                    if (_missingLogged.Add(key))
                        Debug.LogWarning(FigurineMod.LogTag + "托盘图缺失：" + path);
                    return null;
                }

                var bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes))
                {
                    Debug.LogWarning(FigurineMod.LogTag + "托盘图解码失败：" + key);
                    UnityEngine.Object.Destroy(tex);
                    return null;
                }
                tex.filterMode = FilterMode.Point;   // 像素画，放大不要糊
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.Apply(false, false);

                var sp = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                Debug.Log(FigurineMod.LogTag + $"托盘图按需加载 {key} {tex.width}×{tex.height}（缓存 {_cache.Count + 1} 张）");
                return sp;
            }
            catch (Exception e)
            {
                Debug.LogWarning(FigurineMod.LogTag + "托盘图加载异常：" + key + " " + e.GetType().Name + ": " + e.Message + " path=" + path);
                return null;
            }
        }
    }
}
