#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
新 IP 手办 DLC 素材生成器（像素手办 / 深空当铺正式版）

把一个文件夹里的角色立绘 + 一张角色表（CSV / XLSX），批量生成模组需要的四种贴图：
  1) Textures\\Items\\<id>.png        48×48   物品贴图（3×3 格 × 16px）
  2) Big\\<id>.png                   256×256  商店卡片高清图
  3) Tray\\<IP>\\figurine_tray_<短id>.png          512×512  放大镜托盘图
  4) Tray\\<IP>\\Fake\\figurine_tray_<短id>_fake1.png  512×512  赝品托盘图（反色）

可选：--gen-cs 直接生成一份可编译的 `XxxData.g.cs`（含 Raw 行 + Entry/All/ToCatalog）。

用法示例：
  python tools/gen_ip_assets.py ^
      --src  "E:\\新IP立绘" ^
      --table "E:\\新IP角色表.xlsx" ^
      --series ark --dir-name Ark ^
      --out "E:\\深空当铺正式版mod\\手办MOD\\资源\\新IP" ^
      --gen-cs "E:\\深空当铺正式版mod\\手办MOD\\src\\FigurineMod\\ArkData.g.cs"

角色表列名（大小写/中英文都认）：
  file / 文件 / 立绘        立绘文件名（相对 --src）
  id / 标识 / 短id          短 id（只写 slug，如 amiya；完整 id = --prefix + slug）
  name / 名字 / 名称        显示名
  star / 星级 / 稀有度       1~5；留空 = 按价格自动金字塔分档
  value / 价格 / 售价        留空 = 按星级默认（5★1200 / 4★800 / 3★500 / 2★300 / 1★200）
  element / 属性 / 元素      工具提示里【属性】行用，可空
  desc / 介绍 / 描述         工具提示里的介绍，可空

输出目录结构（直接覆盖到 Mods\\PixelFigurines\\ 即可）：
  <out>\\PixelFigurines\\Textures\\Items\\...
  <out>\\PixelFigurines\\Big\\...
  <out>\\PixelFigurines\\Tray\\<IP>\\... 与 ...\\Fake\\...

依赖：Pillow（游戏环境已装）；XLSX 还需要 openpyxl。
"""

from __future__ import annotations

import argparse
import csv
import re
import sys
from pathlib import Path

try:
    from PIL import Image, ImageOps
except ImportError:
    print("缺少 Pillow：先 pip install pillow", file=sys.stderr)
    raise

# 角色表列名别名（统一转小写后匹配）
ALIASES = {
    "file": {"file", "文件", "立绘", "图片", "image", "filename", "原图"},
    "id": {"id", "标识", "短id", "slug", "key"},
    "name": {"name", "名字", "名称", "显示名"},
    "star": {"star", "星级", "稀有度", "rarity"},
    "value": {"value", "价格", "售价", "price"},
    "element": {"element", "属性", "元素", "type"},
    "desc": {"desc", "description", "介绍", "描述", "简介"},
}


def norm_header(h: str) -> str:
    h = (h or "").strip().lower().replace(" ", "")
    for key, names in ALIASES.items():
        if h in names:
            return key
    return h


def read_table(path: Path) -> list[dict]:
    ext = path.suffix.lower()
    raw: list[dict] = []
    if ext in (".xlsx", ".xlsm"):
        import openpyxl  # 延迟导入：只有用 xlsx 才需要
        wb = openpyxl.load_workbook(path, read_only=True, data_only=True)
        ws = wb.active
        it = ws.iter_rows(values_only=True)
        header = [norm_header(str(c) if c is not None else "") for c in next(it)]
        for vals in it:
            row = {}
            for i, v in enumerate(vals):
                if i < len(header) and header[i]:
                    row[header[i]] = "" if v is None else str(v).strip()
            raw.append(row)
    else:
        with open(path, "r", encoding="utf-8-sig", newline="") as f:
            rd = csv.DictReader(f)
            for r in rd:
                row = {}
                for k, v in r.items():
                    k2 = norm_header(k)
                    if k2:
                        row[k2] = (v or "").strip()
                raw.append(row)
    # 丢掉全空行
    return [r for r in raw if any((v or "").strip() for v in r.values())]


def make_square(img: Image.Image, size: int, fit: str, trim: bool) -> Image.Image:
    img = img.convert("RGBA")
    if trim:
        bbox = img.getbbox()
        if bbox:
            img = img.crop(bbox)
    if fit == "cover":
        return ImageOps.fit(img, (size, size), method=Image.LANCZOS, centering=(0.5, 0.5))
    w, h = img.size
    side = max(w, h)
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    canvas.paste(img, ((side - w) // 2, (side - h) // 2), img)
    return canvas.resize((size, size), Image.LANCZOS)


def invert_rgba(img: Image.Image) -> Image.Image:
    r, g, b, a = img.split()
    rgb = ImageOps.invert(Image.merge("RGB", (r, g, b)))
    return Image.merge("RGBA", (*rgb.split(), a))


def cs_escape(s: str) -> str:
    return (s or "").replace("\\", "\\\\").replace('"', '\\"').replace("\r", "").replace("\n", "\\n")


def gen_cs(class_name: str, prefix: str, rows: list[dict], out: Path) -> None:
    cls = re.sub(r"[^A-Za-z0-9_]", "", class_name or "NewIp")
    lines = []
    lines.append("// 自动生成：tools/gen_ip_assets.py --gen-cs；请勿手改")
    lines.append("using System;")
    lines.append("using System.Collections.Generic;")
    lines.append("")
    lines.append("namespace FigurinePort")
    lines.append("{")
    lines.append(f"    public static class {cls}Raw")
    lines.append("    {")
    lines.append(f"        /// <summary>物品 id 前缀：完整 id = Prefix + 短 id。</summary>")
    lines.append(f"        public const string Prefix = \"{cs_escape(prefix)}\";")
    lines.append("        /// <summary>短 id | 显示名 | 星级(0=自动金字塔) | 价格(0=按星级默认) | 属性 | 介绍</summary>")
    lines.append("        public static readonly string[][] Rows = new string[][]")
    lines.append("        {")
    for r in rows:
        cells = [
            r.get("id", ""),
            r.get("name", ""),
            r.get("star", "0") or "0",
            r.get("value", "0") or "0",
            r.get("element", ""),
            r.get("desc", ""),
        ]
        lines.append("            new string[] { " + ", ".join('"' + cs_escape(c) + '"' for c in cells) + " },")
    lines.append("        };")
    lines.append("    }")
    lines.append("")
    lines.append(f"    internal static class {cls}Data")
    lines.append("    {")
    lines.append("        public struct Entry")
    lines.append("        {")
    lines.append("            public string Id;")
    lines.append("            public string Zh;")
    lines.append("            public int Star;")
    lines.append("            public int Value;")
    lines.append("            public string Element;")
    lines.append("            public string Desc;")
    lines.append("        }")
    lines.append("")
    lines.append("        private static List<Entry> _all;")
    lines.append("")
    lines.append("        public static List<Entry> All")
    lines.append("        {")
    lines.append("            get")
    lines.append("            {")
    lines.append("                if (_all != null) return _all;")
    lines.append("                _all = new List<Entry>();")
    lines.append(f"                var rows = {cls}Raw.Rows;")
    lines.append("                if (rows == null) return _all;")
    lines.append("")
    lines.append("                foreach (var r in rows)")
    lines.append("                {")
    lines.append("                    if (r == null || r.Length < 2) continue;")
    lines.append("                    int star;")
    lines.append("                    if (!int.TryParse(r.Length > 2 ? r[2] : \"0\", out star) || star < 1 || star > 5) star = 0;")
    lines.append("                    int val;")
    lines.append("                    if (!int.TryParse(r.Length > 3 ? r[3] : \"0\", out val) || val <= 0) val = 0;")
    lines.append("                    _all.Add(new Entry")
    lines.append("                    {")
    lines.append(f"                        Id = {cls}Raw.Prefix + r[0],")
    lines.append("                        Zh = r[1],")
    lines.append("                        Star = star,")
    lines.append("                        Value = val,")
    lines.append("                        Element = r.Length > 4 ? r[4] : \"\",")
    lines.append("                        Desc = r.Length > 5 ? r[5] : \"\",")
    lines.append("                    });")
    lines.append("                }")
    lines.append("")
    lines.append("                // 星级：表格里全是空 → 按「价格金字塔」重分档（和星铁 / 绝区零一致）")
    lines.append("                bool auto = true;")
    lines.append("                foreach (var e in _all) if (e.Star > 0) { auto = false; break; }")
    lines.append("                if (auto)")
    lines.append("                {")
    lines.append("                    var order = new List<int>();")
    lines.append("                    for (int i = 0; i < _all.Count; i++) order.Add(i);")
    lines.append("                    order.Sort((a, b) =>")
    lines.append("                    {")
    lines.append("                        int c = _all[b].Value.CompareTo(_all[a].Value);")
    lines.append("                        if (c != 0) return c;")
    lines.append("                        return string.CompareOrdinal(_all[a].Id, _all[b].Id);")
    lines.append("                    });")
    lines.append("                    var stars = TierQuota.StarsTopDown(_all.Count);")
    lines.append("                    for (int k = 0; k < order.Count && k < stars.Length; k++)")
    lines.append("                    {")
    lines.append("                        var e = _all[order[k]];")
    lines.append("                        e.Star = stars[k];")
    lines.append("                        _all[order[k]] = e;")
    lines.append("                    }")
    lines.append("                }")
    lines.append("")
    lines.append("                // 价格：没填的按星级默认（5★1200 / 4★800 / 3★500 / 2★300 / 1★200）")
    lines.append("                for (int i = 0; i < _all.Count; i++)")
    lines.append("                {")
    lines.append("                    var e = _all[i];")
    lines.append("                    if (e.Star <= 0) e.Star = 1;")
    lines.append("                    if (e.Value <= 0) e.Value = PriceOf(e.Star);")
    lines.append("                    _all[i] = e;")
    lines.append("                }")
    lines.append("")
    lines.append("                var dist = new int[6];")
    lines.append("                foreach (var e in _all) dist[Math.Max(1, Math.Min(5, e.Star))]++;")
    lines.append(f"                UnityEngine.Debug.Log(FigurineMod.LogTag + \"{cls} 分档：1★\" + dist[1] + \" 2★\" + dist[2] + \" 3★\" + dist[3] + \" 4★\" + dist[4] + \" 5★\" + dist[5]);")
    lines.append("                return _all;")
    lines.append("            }")
    lines.append("        }")
    lines.append("")
    lines.append("        public static int PriceOf(int star)")
    lines.append("        {")
    lines.append("            return star >= 5 ? 1200 : star == 4 ? 800 : star == 3 ? 500 : star == 2 ? 300 : 200;")
    lines.append("        }")
    lines.append("")
    lines.append("        public static Catalog.Def[] ToCatalog()")
    lines.append("        {")
    lines.append("            var list = All;")
    lines.append("            var arr = new Catalog.Def[list.Count];")
    lines.append("            for (int i = 0; i < list.Count; i++)")
    lines.append("            {")
    lines.append("                var e = list[i];")
    lines.append("                arr[i] = new Catalog.Def { Id = e.Id, Zh = e.Zh, Star = e.Star, Value = e.Value, W = 3, H = 3, Element = e.Element, Desc = e.Desc };")
    lines.append("            }")
    lines.append("            return arr;")
    lines.append("        }")
    lines.append("")
    lines.append("        public static bool IsLuxury(int star) { return star >= 4; }")
    lines.append("    }")
    lines.append("}")
    out.write_text("\n".join(lines) + "\n", encoding="utf-8-sig")
    print(f"[cs] 已生成 {out}")


def main() -> int:
    ap = argparse.ArgumentParser(description="新 IP 手办素材生成器")
    ap.add_argument("--src", required=True, help="立绘文件夹")
    ap.add_argument("--table", required=True, help="角色表 CSV/XLSX")
    ap.add_argument("--series", default="newip", help="系列短名（用于默认前缀 / 日志），如 ark")
    ap.add_argument("--prefix", default=None, help="物品 id 前缀，默认 figurine_<series>_")
    ap.add_argument("--dir-name", default=None, help="Tray 子目录名，默认 = series 首字母大写，如 Ark")
    ap.add_argument("--out", required=True, help="输出根目录（会生成 <out>/PixelFigurines/...）")
    ap.add_argument("--items", type=int, default=48, help="物品贴图边长，默认 48（3×3 × 16px）")
    ap.add_argument("--big", type=int, default=256, help="商店高清图边长，默认 256")
    ap.add_argument("--tray", type=int, default=512, help="托盘图边长，默认 512")
    ap.add_argument("--fit", choices=["contain", "cover"], default="contain",
                    help="contain=留透明边保持完整（默认）；cover=裁切铺满")
    ap.add_argument("--trim", action="store_true", help="先裁掉立绘四周透明边再缩放")
    ap.add_argument("--no-fake", action="store_true", help="不生成赝品反色托盘图")
    ap.add_argument("--gen-cs", default=None, help="同时生成 <Xxx>Data.g.cs 到这个路径")
    ap.add_argument("--class", dest="class_name", default=None, help="生成 C# 的类名前缀，默认 = series 首字母大写")
    args = ap.parse_args()

    src = Path(args.src)
    out = Path(args.out)
    prefix = args.prefix or f"figurine_{args.series}_"
    dir_name = args.dir_name or (args.series[:1].upper() + args.series[1:])
    class_name = args.class_name or (args.series[:1].upper() + args.series[1:])

    if not src.is_dir():
        print(f"立绘目录不存在：{src}", file=sys.stderr)
        return 2
    rows = read_table(Path(args.table))
    if not rows:
        print("角色表是空的", file=sys.stderr)
        return 2

    root = out / "PixelFigurines"
    d_items = root / "Textures" / "Items"
    d_big = root / "Big"
    d_tray = root / "Tray" / dir_name
    d_fake = d_tray / "Fake"
    for d in (d_items, d_big, d_tray, d_fake):
        d.mkdir(parents=True, exist_ok=True)

    # 文件索引（大小写不敏感 + 允许子目录）
    index = {}
    for p in src.rglob("*"):
        if p.is_file() and p.suffix.lower() in (".png", ".jpg", ".jpeg", ".webp"):
            index.setdefault(p.name.lower(), p)

    used = set()
    errors = []
    done = 0
    gen_rows = []
    for i, r in enumerate(rows, 1):
        file = (r.get("file") or "").strip()
        slug = (r.get("id") or "").strip()
        name = (r.get("name") or "").strip()
        if not file or not slug or not name:
            errors.append(f"第 {i} 行缺 file/id/name：{r}")
            continue
        slug = re.sub(r"[^A-Za-z0-9_]", "_", slug).strip("_").lower()
        if not slug:
            errors.append(f"第 {i} 行 id 非法：{r.get('id')}")
            continue
        full_id = prefix + slug
        if full_id in used:
            errors.append(f"id 重复：{full_id}")
            continue
        used.add(full_id)

        p = index.get(Path(file).name.lower())
        if p is None:
            errors.append(f"找不到立绘：{file}")
            continue
        try:
            img = Image.open(p)
            sq = make_square(img, args.items, args.fit, args.trim)
            sq.save(d_items / f"{full_id}.png", "PNG")
            make_square(img, args.big, args.fit, args.trim).save(d_big / f"{full_id}.png", "PNG")
            tray = make_square(img, args.tray, args.fit, args.trim)
            tray.save(d_tray / f"figurine_tray_{args.series}_{slug}.png", "PNG")
            if not args.no_fake:
                invert_rgba(tray).save(d_fake / f"figurine_tray_{args.series}_{slug}_fake1.png", "PNG")
            done += 1
            gen_rows.append(r)
            print(f"[ok] {full_id}  <- {p.name}")
        except Exception as e:  # noqa: BLE001
            errors.append(f"{full_id} 处理失败：{e}")

    if args.gen_cs and gen_rows:
        gen_cs(class_name, prefix, gen_rows, Path(args.gen_cs))

    print()
    print(f"完成：{done}/{len(rows)} 个角色  →  {root}")
    if args.gen_cs and gen_rows:
        print(f"数据：{args.gen_cs}")
    if errors:
        print("\n有错误：", file=sys.stderr)
        for e in errors:
            print("  - " + e, file=sys.stderr)
        return 1

    print()
    print("下一步：")
    print(f"  1) 把 {root} 下的 Textures/Items、Big、Tray 覆盖到 Mods\\PixelFigurines\\ 对应目录")
    print("  2) 按教程接入 NewIpData / NewIpItems / Defs / LootInjection / BlindBox / Gacha / ShopUI / TrayArt")
    print("  3) dotnet build -c Release 后看 [手办] 日志")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
