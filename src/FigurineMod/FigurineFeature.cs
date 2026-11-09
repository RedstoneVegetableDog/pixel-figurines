using System;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>手办「正品/赝品」原版特性（旧版 FigurineFeature 精简移植）。</summary>
    internal static class FigurineFeature
    {
        public const string Category = "CATEGORY_FIGURINE_AUTH";
        public const string GenuineId = "figurine_gs_genuine";
        public const string FakeId = "figurine_gs_fake_label";
        public const string DisplayGenuine = "正品";
        public const string DisplayFake = "赝品";

        public static bool Has(GameItem item, string id)
        {
            try { return item != null && item.IsAlreadyContainFeatureWithId(id); } catch { return false; }
        }

        public static void AttachGenuine(GameItem item)
        {
            try
            {
                if (item == null) return;
                try { item.RemoveItemFeatureByID(FakeId); } catch (System.Exception __ex) { Safe.Warn("FigurineFeature.cs:26", __ex); }
                if (Has(item, GenuineId)) return;
                Add(item, GenuineId, DisplayGenuine, true);
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "挂正品标签失败：" + e.GetType().Name); }
        }

        public static void AttachFake(GameItem item)
        {
            try
            {
                if (item == null) return;
                try { item.RemoveItemFeatureByID(GenuineId); } catch (System.Exception __ex) { Safe.Warn("FigurineFeature.cs:38", __ex); }
                if (Has(item, FakeId)) return;
                Add(item, FakeId, DisplayFake, false);
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "挂赝品标签失败：" + e.GetType().Name); }
        }

        private static void Add(GameItem item, string id, string text, bool shown)
        {
            var f = new ItemFeature(ItemFeature.FeatureType.Special);
            f.identifier = id;
            f.category = Category;
            // 公开 setter（2026-11-10 去反射）；initiallyShown 默认就是 true
            f.SetPublicDisplay(text);
            f.SetActualDisplay(text);
            f.SetValueModifier(0);
            f.isPublicHidden = false;
            item.AddItemFeature(f);
        }

        public static void Expose(GameItem item)
        {
            try
            {
                var f = item != null ? item.FindItemFeatureByID(FakeId) : null;
                if (f == null) return;
                f.isPublicHidden = false;
                try { f.ExposeFeature(false, false); } catch (System.Exception __ex) { Safe.Warn("FigurineFeature.cs:75", __ex); }
            }
            catch (System.Exception __ex) { Safe.Warn("FigurineFeature.cs:77", __ex); }
        }
    }
}
