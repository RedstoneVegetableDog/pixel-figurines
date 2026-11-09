using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>模组仓库：抽奖/购买产出先入仓，玩家在仓库页取出到背包。</summary>
    internal static class Warehouse
    {
        private static Dictionary<int, Dictionary<string, int>> _slots;

        private static string FilePath
        {
            get { try { return Path.Combine(Application.persistentDataPath, "figurine_warehouse.txt"); } catch { return "figurine_warehouse.txt"; } }
        }

        private static int SlotId
        {
            get { try { return PlayerStore.Instance != null ? PlayerStore.Instance.saveSlotId : 0; } catch { return 0; } }
        }

        private static Dictionary<string, int> Cur
        {
            get
            {
                if (_slots == null)
                {
                    _slots = new Dictionary<int, Dictionary<string, int>>();
                    try
                    {
                        if (ModData.GetLines("warehouse").Length > 0)
                        {
                            foreach (var line in ModData.GetLines("warehouse"))
                            {
                                var p = line.Split('|');
                                if (p.Length < 3) continue;
                                if (!int.TryParse(p[0], out var slot)) continue;
                                if (!int.TryParse(p[2], out var qty) || qty <= 0) continue;
                                if (!_slots.TryGetValue(slot, out var d)) { d = new Dictionary<string, int>(); _slots[slot] = d; }
                                d[p[1]] = (d.TryGetValue(p[1], out var old) ? old : 0) + qty;
                            }
                        }
                    }
                    catch (System.Exception __ex) { Safe.Warn("Warehouse.cs:45", __ex); }
                }
                int sid = SlotId;
                if (!_slots.TryGetValue(sid, out var cur)) { cur = new Dictionary<string, int>(); _slots[sid] = cur; }
                return cur;
            }
        }

        // ───────────────────────── 容量 ─────────────────────────

        /// <summary>初始容量（件）。同款多件各算 1 件。</summary>
        public const int BaseCapacity = 40;

        /// <summary>容量档位：40 → 60 → 80 → 100（DORO 电话每档 10000）。</summary>
        private static readonly int[] Tiers = new int[] { 40, 60, 80, 100 };

        public static int Capacity
        {
            get
            {
                int cap = BaseCapacity;
                try { if (int.TryParse(ModData.Get("wh_cap", BaseCapacity.ToString()), out var v)) cap = v; } catch (System.Exception __ex) { Safe.Warn("Warehouse.cs:66", __ex); }
                return Normalize(cap);
            }
        }

        /// <summary>直接从仓库扣掉 qty 件（不生成实物、不进背包）。返回是否扣成功。</summary>
        public static bool Consume(string id, int qty = 1)
        {
            try
            {
                if (string.IsNullOrEmpty(id) || qty <= 0) return false;
                var d = Cur;
                if (!d.TryGetValue(id, out var have) || have < qty) return false;
                int left = have - qty;
                if (left > 0) d[id] = left; else d.Remove(id);
                Save();
                return true;
            }
            catch { return false; }
        }

        /// <summary>已占件数（所有款式数量之和）。</summary>
        public static int Used
        {
            get
            {
                int n = 0;
                foreach (var kv in Cur) n += kv.Value;
                return n;
            }
        }

        public static bool CanUpgrade { get { return Capacity < Tiers[Tiers.Length - 1]; } }

        public static int NextCapacity
        {
            get
            {
                int c = Capacity;
                foreach (var t in Tiers) if (t > c) return t;
                return c;
            }
        }

        /// <summary>升一档。返回是否真的升了。</summary>
        public static bool Upgrade()
        {
            try
            {
                if (!CanUpgrade) return false;
                ModData.Set("wh_cap", NextCapacity.ToString());
                Debug.Log(FigurineMod.LogTag + "仓库扩容 → " + Capacity + " 件");
                return true;
            }
            catch { return false; }
        }

        private static int Normalize(int v)
        {
            foreach (var t in Tiers) if (v <= t) return t;
            return Tiers[Tiers.Length - 1];
        }

        public static void Save()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                foreach (var kv in _slots)
                    foreach (var item in kv.Value)
                        if (item.Value > 0) sb.Append(kv.Key).Append('|').Append(item.Key).Append('|').Append(item.Value).Append('\n');
                ModData.Set("warehouse", sb.ToString());
            }
            catch (System.Exception __ex) { Safe.Warn("Warehouse.cs:139", __ex); }
        }

        public static int Add(string id, int qty = 1)
        {
            try
            {
                if (string.IsNullOrEmpty(id) || qty <= 0) return 0;
                int free = Capacity - Used;
                if (free <= 0)
                {
                    Debug.LogWarning(FigurineMod.LogTag + "仓库已满（" + Used + "/" + Capacity + "），" + id + " 放不进去");
                    return 0;
                }
                int add = qty < free ? qty : free;
                var d = Cur;
                d[id] = (d.TryGetValue(id, out var n) ? n : 0) + add;
                Save();
                return add;
            }
            catch { return 0; }
        }

        /// <summary>入仓；仓满的余量直接进背包（放不下就丢日志，不会静默吞掉）。</summary>
        public static void AddOrBag(string id, int qty = 1)
        {
            int added = Add(id, qty);
            int rest = qty - added;
            if (rest <= 0) return;

            Debug.Log(FigurineMod.LogTag + "仓库满，余量 " + rest + " 件转背包：" + id);
            var inv = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.invElement : null;
            for (int i = 0; i < rest; i++)
            {
                // 原神 + 星铁合表查（星铁 ID 不在 Catalog.All 里，以前会退回占位物品）
                var def = Defs.Find(id);
                if (string.IsNullOrEmpty(def.Id)) return;   // 未知 id：不再用券占位，直接跳过
                GameItem item = FigurineMod.BuildFigurine(def);
                if (item == null) { Debug.LogWarning(FigurineMod.LogTag + "仓库溢出建物失败：" + id); return; }
                if (!FigurineMod.TryPlace(inv, item))
                {
                    Debug.LogWarning(FigurineMod.LogTag + "仓库满了、背包也放不下：" + id);
                    return;
                }
            }
        }

        public static Dictionary<string, int> Snapshot()
        {
            var copy = new Dictionary<string, int>();
            foreach (var kv in Cur) if (kv.Value > 0) copy[kv.Key] = kv.Value;
            return copy;
        }

        public static int Count(string id)
        {
            return Cur.TryGetValue(id, out var n) ? n : 0;
        }

        /// <summary>取出 qty 件到背包，返回实际取出数量。</summary>
        public static int Take(string id, int qty)
        {
            try
            {
                if (string.IsNullOrEmpty(id) || qty <= 0) return 0;
                var d = Cur;
                if (!d.TryGetValue(id, out var have) || have <= 0) return 0;
                int want = Math.Min(qty, have);
                var inv = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.invElement : null;
                int taken = 0, failed = 0;
                for (int i = 0; i < want; i++)
                {
                    var def = Defs.Find(id);
                    if (string.IsNullOrEmpty(def.Id)) break;   // 未知 id：不再用券占位，直接停
                    GameItem item = FigurineMod.BuildFigurine(def);
                    if (item == null) break;
                    if (!FigurineMod.TryPlace(inv, item)) { failed++; break; }
                    // 硬校验：TryPlace 返回 true 不代表物品真的还在背包里（实测有「取出后消失」）
                    bool verified = false;
                    try { verified = inv != null && inv.childItems != null && inv.childItems.Contains(item) && item.parentInventory == inv; } catch { }
                    if (!verified)
                    {
                        failed++;
                        try { if (item != null && item.parentInventory == null) item.Destroy(); } catch { }
                        Debug.LogWarning(FigurineMod.LogTag + "仓库取出校验失败，物品未进背包：" + id);
                        break;
                    }
                    taken++;
                }
                Debug.Log(FigurineMod.LogTag + "仓库取出 " + id + " → 成功 " + taken + "/" + want
                    + "，失败 " + failed
                    + "，背包占用 " + (inv != null && inv.childItems != null ? inv.childItems.Count : -1)
                    + "/" + (inv != null ? (inv.widthPixels / 16) + "x" + (inv.heightPixels / 16) : "?"));
                if (taken > 0)
                {
                    int left = have - taken;
                    if (left > 0) d[id] = left; else d.Remove(id);
                    Save();
                }
                return taken;
            }
            catch { return 0; }
        }
    }
}
