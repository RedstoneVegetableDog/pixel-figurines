using System;
using System.Collections.Generic;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 手办价格事件（按 0.8.0-beta FigurineEvents）：8 条蓝图（涨/跌 × 5/15/25/45%），
    /// 权重 10、持续 3 天、冷却 7 天、importance 5；报纸文案直接来自旧 tsv。
    /// </summary>
    internal static class FigurineEvents
    {
        public const string PriceTag = "figurine_gs_item";

        public static readonly string[] AllIds =
        {
            "figurine_hype_t05", "figurine_hype_t15", "figurine_hype_t25", "figurine_hype_t45",
            "figurine_slump_t05", "figurine_slump_t15", "figurine_slump_t25", "figurine_slump_t45",
        };

        private static bool _registered;

        private static StoreEvent Make(string id, string[][] texts, int pct)
        {
            var t = texts[UnityEngine.Random.Range(0, texts.Length)];
            var ev = new StoreEvent();
            ev.identifier = id;
            ev.newsName = t[0];
            ev.displayName = t[1];
            ev.newsDescription = t[2];
            ev.duration = 3;
            ev.totalDuration = 3;
            ev.cooldown = 7;
            ev.importance = 5;
            ev.isDurationVisible = true;
            var list = new List<NegociationData>();
            list.Add(new NegociationData(PriceTag, pct, "手办", "手办价格"));
            try { ev.negociationDatas = list; } catch (System.Exception __ex) { Safe.Warn("FigurineEvents.cs:38", __ex); }
            return ev;
        }

        private static StoreEvent Create(string id, string[][] texts, int pct)
        {
            return Make(id, texts, pct);
        }

        public static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;
            try
            {
                var list = StoreEventManager.normalEventBlueprints;
                if (list == null) return;
                Add(list, "figurine_hype_t05", FigurineEventTexts.Texts_hype_t05, 5);
                Add(list, "figurine_hype_t15", FigurineEventTexts.Texts_hype_t15, 15);
                Add(list, "figurine_hype_t25", FigurineEventTexts.Texts_hype_t25, 25);
                Add(list, "figurine_hype_t45", FigurineEventTexts.Texts_hype_t45, 45);
                Add(list, "figurine_slump_t05", FigurineEventTexts.Texts_slump_t05, -5);
                Add(list, "figurine_slump_t15", FigurineEventTexts.Texts_slump_t15, -15);
                Add(list, "figurine_slump_t25", FigurineEventTexts.Texts_slump_t25, -25);
                Add(list, "figurine_slump_t45", FigurineEventTexts.Texts_slump_t45, -45);
                Debug.Log(FigurineMod.LogTag + "手办事件：已注册 8 条蓝图（共 " + list.Count + " 条日常事件）");
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "手办事件注册失败：" + e.GetType().Name); }
        }

        private static void Add(List<StoreEventBlueprint> list, string id, string[][] texts, int pct)
        {
            try
            {
                foreach (var b in list) if (b != null && b.identifier == id) return;
            }
            catch (System.Exception __ex) { Safe.Warn("FigurineEvents.cs:74", __ex); }
            try
            {
                int p = pct;
                var txt = texts;
                list.Add(new StoreEventBlueprint((Func<StoreEvent>)(() => Create(id, txt, p)), 10, id));
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "手办事件蓝图添加失败：" + id + " " + e.GetType().Name); }
        }
    }
}
