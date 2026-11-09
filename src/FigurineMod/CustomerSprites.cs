using System;
using System.Collections.Generic;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 顾客立绘：米拉/白鹭/雀儿各 4 张作者图（288×211，PPU 100）。
    /// 每次进店随机取 1 张，离开不保留（下次进店重新随机）。
    /// </summary>
    internal static class CustomerSprites
    {
        private static string Prefix(string id)
        {
            if (id == CustomerSellers.BailuId) return "bailu";
            if (id == CustomerSellers.QueerId) return "queer";
            return "mila";
        }

        public static string Get(string id)
        {
            string prefix = Prefix(id);
            int n = UnityEngine.Random.Range(1, 5);
            string key = "cust_" + prefix + "_" + n;
            try
            {
                var dict = SpriteDict.Instance.spriteDictionary;
                if (dict != null && !dict.ContainsKey(key))
                {
                    var tex = LoadTexture(key);
                    if (tex != null)
                    {
                        // 这组立绘只有 288×211，游戏里会放大到 300px 以上；
                        // 默认双线性 = 糊，改成 Point 让像素边缘保持锐利
                        try { tex.filterMode = FilterMode.Point; tex.wrapMode = TextureWrapMode.Clamp; tex.Apply(false, false); } catch (System.Exception __ex) { Safe.Warn("CustomerSprites.cs:35", __ex); }
                        var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                        dict[key] = sp;
                    }
                }
                if (dict != null && dict.ContainsKey(key)) return key;
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "顾客立绘注入失败：" + e.GetType().Name); }

            try { return SpriteDict.GetRandomLowerLevelSpriteName(); } catch (System.Exception __ex) { Safe.Warn("CustomerSprites.cs:44", __ex); }
            return null;
        }

        private static Texture2D LoadTexture(string key)
        {
            try
            {
                var mods = ModLoader.Mods;
                if (mods == null) return null;
                foreach (var m in mods)
                {
                    if (m == null || m.Manifest == null || m.Manifest.ID != "figurine.gs") continue;
                    if (m.Textures != null && m.Textures.TryGetValue(key, out var t)) return t;
                }
            }
            catch (System.Exception __ex) { Safe.Warn("CustomerSprites.cs:60", __ex); }
            return null;
        }
    }
}
