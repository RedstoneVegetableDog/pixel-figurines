using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 顾客立绘下移 16 **屏幕像素**（用户 2026-11-02 要求）。
    ///
    /// 作用对象：DORO / 米拉 / 雀儿 / 白鹭（`doro_courier` + `cust_{mila,queer,bailu}_N`）。
    ///
    /// 为什么要这么绕：
    ///   · 立绘是 `StoreClientMono.image`（**SpriteRenderer**，世界空间），
    ///     不是 UI Image，没有 anchoredPosition 可调；
    ///   · 顾客走进来的位置由 DOTweenPath 每帧改 transform.position，
    ///     直接挪 transform 会被 Tween 覆盖掉；
    ///   · 所以改成**换一张 pivot 下移过的 Sprite 副本**：SpriteRenderer 以 pivot 对准 transform 原点，
    ///     pivot.y 变大 → 画面整体下移，且不受 Tween 影响。
    ///
    /// 16 屏幕像素 → 世界单位：`16 * (2 * camera.orthographicSize / Screen.height)`，
    /// 再换算成 pivot 比例（除以立绘自身世界高度）。相机或缩放变了会按新值重算。
    /// </summary>
    internal static class PortraitOffset
    {
        /// <summary>下移量（屏幕像素）。先按 16px 落地，用户看过之后要求再+32 → 合计 48。</summary>
        public const float DownPixels = 48f;

        private static string _lastKey;
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static float _cachedPxPerWorld = -1f;

        /// <summary>是不是我们这四位顾客的立绘。</summary>
        public static bool IsOurPortrait(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (key == "doro_courier") return true;
            return key.StartsWith("cust_mila_", StringComparison.Ordinal)
                || key.StartsWith("cust_queer_", StringComparison.Ordinal)
                || key.StartsWith("cust_bailu_", StringComparison.Ordinal);
        }

        /// <summary>记录这次要显示的立绘 key（由 ShowClientSprite 前置拿到）。</summary>
        public static void NoteKey(string key) { _lastKey = key; }

        [HarmonyPatch(typeof(SpriteDict), "ShowClientSprite")]
        internal static class NoteKeyPatch
        {
            [HarmonyPrefix]
            private static void Prefix(string key) { try { _lastKey = key; } catch (System.Exception __ex) { Safe.Warn("PortraitOffset.cs:50", __ex); } }
        }

        /// <summary>顾客立绘刚被设置完之后再动手（此时 image.sprite 才是本次的图）。</summary>
        [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.GetNextClient))]
        internal static class ApplyPatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                try { Apply(); } catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "立绘下移失败：" + e.GetType().Name); }
            }
        }

        private static void Apply()
        {
            var mono = StoreClientMono.Instance;
            if (mono == null || mono.image == null) return;

            var cur = mono.image.sprite;
            if (cur == null) return;

            if (!IsOurPortrait(_lastKey)) { _cache.Clear(); return; }

            var shifted = Shifted(cur, _lastKey);
            if (shifted != null) mono.image.sprite = shifted;
        }

        private static Sprite Shifted(Sprite src, string key)
        {
            float pxPerWorld = PixelsPerWorld();
            if (pxPerWorld <= 0f) return null;

            Sprite cached;
            if (_cache.TryGetValue(key, out cached) && cached != null) return cached;

            try
            {
                float x = Mathf.Max(1f, src.rect.width);
                float y = Mathf.Max(1f, src.rect.height);

                // ⚠️ `Sprite.pivot` 是**像素**坐标；`Sprite.Create` 要的是 0~1 归一化，必须先除 rect
                float pivotNormX = src.pivot.x / x;
                float pivotNormY = src.pivot.y / y;

                // 16 屏幕像素 = 16 / pxPerWorld 世界单位；换算成 pivot 比例（相对立绘世界高度）
                float worldShift = DownPixels / pxPerWorld;
                float spriteWorldH = y / Mathf.Max(1f, src.pixelsPerUnit);
                float dNorm = Mathf.Clamp(worldShift / Mathf.Max(0.0001f, spriteWorldH), 0f, 0.25f);

                float py = Mathf.Clamp01(pivotNormY + dNorm);
                var sp = Sprite.Create(src.texture, src.rect, new Vector2(pivotNormX, py), src.pixelsPerUnit);
                sp.name = key + "_down16";
                _cache[key] = sp;
                Debug.Log(FigurineMod.LogTag + $"立绘下移 16px：{key} pivot(归一) {pivotNormY:0.000} → {py:0.000}  dNorm={dNorm:0.000}");
                return sp;
            }
            catch (Exception e)
            {
                Debug.LogWarning(FigurineMod.LogTag + "立绘 pivot 计算失败：" + e.GetType().Name);
                return null;
            }
        }

        /// <summary>当前画面：1 个世界单位等于多少屏幕像素。</summary>
        private static float PixelsPerWorld()
        {
            float now = -1f;
            try
            {
                var cam = Camera.main;
                if (cam != null)
                    now = Screen.height / Mathf.Max(0.0001f, 2f * cam.orthographicSize);
            }
            catch (System.Exception __ex) { Safe.Warn("PortraitOffset.cs:124", __ex); }

            if (now <= 0f) now = 1080f / 10f;   // 兜底：1080p + orthoSize 5

            if (Mathf.Abs(now - _cachedPxPerWorld) > 0.5f)
            {
                _cachedPxPerWorld = now;
                _cache.Clear();   // 缩放变了，pivot 要重算
            }
            return now;
        }
    }
}
