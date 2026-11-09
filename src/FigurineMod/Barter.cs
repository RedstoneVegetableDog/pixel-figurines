using System;
using System.Collections.Generic;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 以物易物（按 0.8.0-beta BuildOfferPlan/KickBarterOffer/PlacePlannedSet）：
    /// 4 组货，每组目标 = 基准价 × Random(0.9,1.3)；最多 12 件，50% 手办 / 50% 原版物品。
    /// 手办的真假不在计划阶段掷，摆货时按三态掷（真身 7:3、标签 0% 错标）。
    /// </summary>
    internal static class Barter
    {
        private class PlanItem
        {
            public string id;
            public bool isFigurine;
            public long value;
            /// <summary>手办：三态在计划阶段就掷好，摆货时照这个落，组值也按它的价格算。</summary>
            public FigurineAuth.Roll roll;
        }

        private static List<List<PlanItem>> _plan;
        private static List<string> _vanillaPool;
        private static int _baseValue;

        public static void Start()
        {
            try
            {
                string target = FirstOwnedFigurine();
                if (string.IsNullOrEmpty(target)) { Debug.Log(FigurineMod.LogTag + "以物易物：背包里没有手办"); return; }
                CustomerMira.TrySchedule(StoreClient.ClientIntent.BARTER, null, target);
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "以物易物异常：" + e.GetType().Name); }
        }

        private static string FirstOwnedFigurine()
        {
            try
            {
                var ps = PlayerStore.Instance;
                if (ps == null) return null;
                foreach (var it in ps.FindAllItem(true))
                {
                    if (it == null) continue;
                    foreach (var d in Defs.All()) if (d.Id == it.identifier) return d.Id;
                }
            }
            catch (System.Exception __ex) { Safe.Warn("Barter.cs:48", __ex); }
            return null;
        }

        public static void Attach(StoreClient client, string targetId)
        {
            try
            {
                var def = FindDef(targetId);
                string zh = def.Id != null ? def.Zh : targetId;
                int baseValue = def.Id != null ? def.Value : 300;

                var offer = new BarterOffer();
                offer.ownerClient = client;
                offer.barterTextBody = "我带了点东西，想换《" + zh + "》。";
                offer.acceptedResponse = "就这个，成交。";
                offer.deniedResponse = "不，我要的是《" + zh + "》。";

                var cond = new BarterCondition();
                cond.requiredItemId = targetId;
                cond.requiredDisplay = zh;
                offer.conditions = new List<BarterCondition> { cond };

                offer.getBarterResult = (Func<BarterResult>)(() =>
                {
                    var r = new BarterResult();
                    r.isAccepted = true;
                    r.response = "就这个，成交。";
                    return r;
                });

                BuildPlan(baseValue);
                for (int i = 0; i < 4; i++)
                {
                    int idx = i;
                    try { offer.AddOfferSet((Action)(() => PlaceSet(idx))); } catch (System.Exception __ex) { Safe.Warn("Barter.cs:83", __ex); }
                }

                try { client.SetBarterOffer(offer); }
                catch { try { client.barterOffer = offer; client.clientIntent = StoreClient.ClientIntent.BARTER; } catch (System.Exception __ex) { Safe.Warn("Barter.cs:87", __ex); } }
                Debug.Log(FigurineMod.LogTag + "以物易物已挂载：想要 " + zh + "，4 组货，基准 " + baseValue);
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "挂载以物易物失败：" + e.GetType().Name + " | " + e.Message); }
        }

        private static void BuildPlan(int baseValue)
        {
            _baseValue = baseValue;
            _plan = new List<List<PlanItem>>();
            for (int i = 0; i < 4; i++)
            {
                float mul = UnityEngine.Random.Range(0.9f, 1.3f);
                long target = (long)(baseValue * mul);
                long sum = 0;
                var set = new List<PlanItem>();
                for (int j = 0; j < 12 && sum < target; j++)
                {
                    var item = UnityEngine.Random.value < 0.5f ? RollFigurine() : RollVanilla();
                    if (item == null) item = RollVanilla();
                    if (item == null) break;
                    set.Add(item);
                    sum += item.value;
                }
                _plan.Add(set);
                Debug.Log(FigurineMod.LogTag + "易物第 " + (i + 1) + " 组：目标 " + target + " → " + set.Count + " 件 / " + sum);
            }
        }

        private static PlanItem RollFigurine()
        {
            try
            {
                var pool = Defs.All();
                if (pool.Count == 0) return null;
                var d = pool[UnityEngine.Random.Range(0, pool.Count)];
                // 三态在这里就掷好（用户 2026-11-04：以物易物 标签 0% 错标），
                // 组值按**现版本的赝品价**算（标赝品 60%），摆货时照同一次掷骰落。
                var roll = FigurineAuth.RollFor(FigurineAuth.Source.Barter, d.Id);
                int pct = FigurineAuth.PricePercentForRoll(roll);
                long v = (long)d.Value * pct / 100;
                return new PlanItem { id = d.Id, isFigurine = true, value = v, roll = roll };
            }
            catch { return null; }
        }

        private static PlanItem RollVanilla()
        {
            try
            {
                var pool = VanillaPool();
                if (pool.Count == 0) return null;
                for (int i = 0; i < 12; i++)
                {
                    string id = pool[UnityEngine.Random.Range(0, pool.Count)];
                    if (string.IsNullOrEmpty(id)) continue;
                    long v = VanillaValueOf(id);
                    if (v > 0) return new PlanItem { id = id, isFigurine = false, value = v };
                }
            }
            catch (System.Exception __ex) { Safe.Warn("Barter.cs:143", __ex); }
            return null;
        }

        private static List<string> VanillaPool()
        {
            if (_vanillaPool != null) return _vanillaPool;
            _vanillaPool = new List<string>();
            try
            {
                var cats = DirectoryMaster.GetDictionaryClassNameList<GameItem>();
                if (cats == null) return _vanillaPool;
                foreach (var cat in cats)
                {
                    if (string.IsNullOrEmpty(cat)) continue;
                    List<string> ids = null;
                    try { ids = DirectoryMaster.GetIdentifierList<GameItem>(cat); } catch (System.Exception __ex) { Safe.Warn("Barter.cs:159", __ex); }
                    if (ids == null) continue;
                    foreach (var id in ids)
                        if (!string.IsNullOrEmpty(id) && !id.StartsWith("figurine_", StringComparison.Ordinal))
                            _vanillaPool.Add(id);
                }
            }
            catch (Exception e) { Debug.LogWarning(FigurineMod.LogTag + "易物枚举原版物品失败：" + e.GetType().Name); }
            return _vanillaPool;
        }

        private static long VanillaValueOf(string id)
        {
            try
            {
                var it = DirectoryMaster.Item(id, false);
                return it != null ? it.unitValue : 0L;
            }
            catch { return 0L; }
        }

        private static void PlaceSet(int index)
        {
            try
            {
                if (_plan == null || index < 0 || index >= _plan.Count) return;
                var ps = PlayerStore.Instance;
                if (ps == null) return;
                foreach (var p in _plan[index])
                {
                    GameItem item = null;
                    if (p.isFigurine)
                    {
                        var d = FindDef(p.id);
                        if (d.Id == null) continue;
                        item = FigurineMod.BuildFigurine(d);
                        // 照计划阶段掷好的三态落（组值就是按它算的）
                        if (item != null)
                        {
                            try { FigurineAuth.Apply(item, p.roll); }
                            catch (System.Exception __ex) { Safe.Warn("Barter.Auth", __ex); }
                        }
                    }
                    else
                    {
                        try { item = DirectoryMaster.Item(p.id, false); } catch (System.Exception __ex) { Safe.Warn("Barter.cs:199", __ex); }
                    }
                    if (item == null) continue;
                    try { ps.AddDirectSellingItemToTable(item, false); } catch (System.Exception __ex) { Safe.Warn("Barter.cs:202", __ex); }
                }
                Debug.Log(FigurineMod.LogTag + "易物：摆出第 " + (index + 1) + " 组");
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "摆易物组失败：" + e.GetType().Name); }
        }

        private static Catalog.Def FindDef(string id)
        {
            { var dd = Defs.Find(id); if (!string.IsNullOrEmpty(dd.Id)) return dd; }
            return default;
        }
    }
}
