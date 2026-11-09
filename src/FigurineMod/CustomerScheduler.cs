using System;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 1:1 复刻旧版 FigurineCustomer.TryScheduleVisit 的排期：
    /// 每次开店 35% 概率来 1 位；方向权重 买45% / 卖35% / 只卖不买20%；追加到 clientStack 末尾。
    /// </summary>
    internal static class CustomerScheduler
    {
        public static void OnShopOpened()
        {
            try
            {
                var mgr = PlayerStore.Instance != null ? PlayerStore.Instance.storeClientManager : null;
                if (mgr == null) return;

                if (UnityEngine.Random.Range(0f, 100f) >= 90f)
                {
                    Debug.Log(FigurineMod.LogTag + "今天顾客没来（概率 90%）");
                    return;
                }

                // 照 0.8.0 `FigurineCustomer.RollDirection()`：每次到店掷 35/35/30
                //   <35 → BUY(1)   米拉这种「买」的身份也可能变成卖，靠摆货兜住
                //   <70 → SELL(2)
                //   其它 → SELLNBUY(3)
                float v = UnityEngine.Random.Range(0f, 100f);
                StoreClient.ClientIntent intent = v < 35f ? StoreClient.ClientIntent.BUY
                    : v < 70f ? StoreClient.ClientIntent.SELL
                    : StoreClient.ClientIntent.SELLNBUY;

                float r = UnityEngine.Random.Range(0f, 100f);
                if (r < 45f) CustomerMira.TrySchedule(intent);
                else if (r < 80f) CustomerSellers.TryBailu(intent);
                else CustomerSellers.TryQueer(intent);
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "顾客调度异常：" + e.GetType().Name); }
        }
    }
}
