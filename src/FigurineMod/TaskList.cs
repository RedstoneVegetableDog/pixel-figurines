using System;
using System.Collections.Generic;
using System.Text;

namespace FigurinePort
{
    /// <summary>海子委托清单：每天 1~3 件（从全部手办抽，权重 5★2 / 4★4 / 3★6 / 其余 1），当天固定。</summary>
    internal static class TaskList
    {
        private static readonly Dictionary<string, (int day, List<Catalog.Def> list)> _cache
            = new Dictionary<string, (int, List<Catalog.Def>)>();

        private static float Weight(Catalog.Def d)
        {
            switch (d.Star) { case 5: return 2f; case 4: return 4f; case 3: return 6f; default: return 1f; }
        }

        public static List<Catalog.Def> Today(string ownerId)
        {
            int day = 1;
            try { day = Math.Max(1, StoreStation.GetDayCounter()); } catch (System.Exception __ex) { Safe.Warn("TaskList.cs:21", __ex); }
            if (_cache.TryGetValue(ownerId, out var hit) && hit.day == day) return hit.list;

            var pool = new List<Catalog.Def>();
            foreach (var d in Defs.All()) pool.Add(d);   // 抽取池 = 全部手办（三 IP 合表）
            var picked = new List<Catalog.Def>();
            int want = UnityEngine.Random.Range(1, 4);
            for (int k = 0; k < want && pool.Count > 0; k++)
            {
                float total = 0f;
                foreach (var d in pool) total += Weight(d);
                float r = UnityEngine.Random.Range(0f, total);
                int idx = 0;
                for (int i = 0; i < pool.Count; i++)
                {
                    r -= Weight(pool[i]);
                    if (r <= 0f) { idx = i; break; }
                }
                picked.Add(pool[idx]);
                pool.RemoveAt(idx);
            }
            _cache[ownerId] = (day, picked);
            return picked;
        }

        public static string Text(string ownerName, List<Catalog.Def> list)
        {
            var sb = new StringBuilder();
            sb.Append(ownerName).Append("想要：");
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append("、");
                sb.Append(list[i].Zh);
            }
            sb.Append("\n都给钱也行，换货也行。");
            return sb.ToString();
        }

        /// <summary>叫委托人上门：只收清单第一件（旧版任务结算模式）。</summary>
        public static string FirstId(string ownerId)
        {
            var list = Today(ownerId);
            return list.Count > 0 ? list[0].Id : null;
        }
    }
}
