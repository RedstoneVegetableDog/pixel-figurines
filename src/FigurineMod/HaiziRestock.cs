using System;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>
    /// 海子「补货」：玩家把柜台上他带来的**盲盒 / 展示柜**买走后先记一笔，
    /// 只要海子本人还在窗口就补回柜台中间（服务说明里的「买走即补货」）。
    ///
    /// 补货**不记具体盒 id**：从全部 8 种盲盒里重新随机一款补回（用户 2026-11-05 定）。
    ///
    /// 调用关系：
    /// ```text
    /// HaiziPatches（买走判定）→ Register(id)
    /// Haizi.Tick()（每帧）      → Flush()
    /// ```
    /// </summary>
    internal static class HaiziRestock
    {
        private static int _pendingBox;
        private static int _pendingCab;

        /// <summary>登记一次待补货（任意盲盒 id + 展示柜）。</summary>
        public static void Register(string id)
        {
            try
            {
                if (BlindBox.IsBoxId(id))
                {
                    _pendingBox++;
                    Debug.Log(FigurineMod.LogTag + "海子盲盒被买走（" + id + "）→ 登记补货");
                }
                else if (id == Cabinet.Id)
                {
                    _pendingCab++;
                    Debug.Log(FigurineMod.LogTag + "海子展示柜被买走 → 登记补货");
                }
            }
            catch (Exception e) { Safe.Warn("HaiziRestock.Register", e); }
        }

        /// <summary>海子在窗口时，把欠的货补回柜台中间（买走即补货）。</summary>
        public static void Flush()
        {
            try
            {
                if (_pendingBox <= 0 && _pendingCab <= 0) return;
                var ps = PlayerStore.Instance;
                if (ps == null) return;
                // 必须是海子本人还在窗口：以前等 currentClientInstance == null 再补，
                // 补出来的货没有顾客能买，还会被下一位顾客当成「他带来的货」。
                var inst = ps.currentClientInstance;
                var bp = inst != null ? inst.GetClientBlueprint() : null;
                if (bp == null || bp.identifier != Haizi.ClientId) return;

                while (_pendingBox > 0)
                {
                    // 不记原盒 id，从全部 8 种盲盒里重新随机
                    var it = BlindBox.Build(BlindBox.RandomId());
                    if (it == null) break;
                    if (!Placement.TryFront(it, false)) { try { it.Destroy(); } catch { } break; }   // 放不下就下次再补，不静默丢
                    _pendingBox--;
                }
                while (_pendingCab > 0)
                {
                    var it = Cabinet.Build();
                    if (it == null) break;
                    if (!Placement.TryFront(it, false)) { try { it.Destroy(); } catch { } break; }
                    _pendingCab--;
                }
            }
            catch (Exception e) { Safe.Warn("HaiziRestock.Flush", e); }
        }
    }
}
