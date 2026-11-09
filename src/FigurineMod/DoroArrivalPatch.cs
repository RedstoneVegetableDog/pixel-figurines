using System;
using HarmonyLib;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>DORO 到店（TryCallNextClient 后置）：电话预约的终端机在这时交付。</summary>
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.TryCallNextClient))]
    internal static class DoroArrivalPatch
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerStore __instance)
        {
            try
            {
                if (__instance == null || __instance.currentClientInstance == null) return;
                string cid = null;
                try { var bp = __instance.currentClientInstance.GetClientBlueprint(); if (bp != null) cid = bp.identifier; } catch (System.Exception __ex) { Safe.Warn("DoroArrivalPatch.cs:18", __ex); }
                if (cid == Doro.ClientId)
                {
                    Doro.OnArrived();
                    try { __instance.RemoveClientSellingItemFromTable(); } catch (System.Exception __ex) { Safe.Warn("DoroArrivalPatch.cs:22", __ex); }
                    try { __instance.RemoveClientSellingItemFromTable(); } catch (System.Exception __ex) { Safe.Warn("DoroArrivalPatch.cs:23", __ex); }
                }
                else if (cid == Haizi.ClientId) Haizi.OnArrived();
                else if (cid == CustomerSellers.BailuId || cid == CustomerSellers.QueerId || cid == CustomerMira.ClientId) CustomerSellers.OnArrived(cid);
                else if (cid == FigurineAppraisal.ClientId) FigurineAppraisal.OnArrived();
            }
            catch (System.Exception __ex) { Safe.Warn("DoroArrivalPatch.cs:27", __ex); }
        }
    }
}
