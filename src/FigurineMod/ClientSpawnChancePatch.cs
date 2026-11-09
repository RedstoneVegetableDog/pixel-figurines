using HarmonyLib;

namespace FigurinePort
{
    /// <summary>模组提高原版顾客生成量：生成概率 ×1.5（原版按概率刷客，无固定名额字段）。</summary>
    [HarmonyPatch(typeof(StoreClientManager), nameof(StoreClientManager.GetRandomClientSpawnChance))]
    internal static class ClientSpawnChancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref int __result)
        {
            try
            {
                if (__result <= 0) return;
                int boosted = __result * 3 / 2;
                __result = boosted > 100 ? 100 : boosted;
            }
            catch (System.Exception __ex) { Safe.Warn("ClientSpawnChancePatch.cs:18", __ex); }
        }
    }
}
