# 新 IP 手办 DLC 开发教程（像素手办 / 深空当铺正式版）

> 适用：v1.0.3 代码基线（2026-11-05）
> 目标：你提供一套新 IP 的角色立绘 + 角色表，把它接成模组里的第 4 个系列（手办物品 / 商店 / 顾客 / 掉落 / 盲盒 / 抽奖 / 交易板 / 鉴定 / 托盘图全链路）
> 附带工具：`手办MOD\tools\gen_ip_assets.py`（批量出 48 / 256 / 512 贴图 + 反色赝品图 + 可编译的 `XxxData.g.cs`）

---

## 0. 先做选择：并入主模组，还是独立 DLC？

| | 方案 A：并入主模组（推荐先做） | 方案 B：独立 DLC 包 |
|---|---|---|
| 做法 | 新 IP 的数据/贴图/代码直接进 `像素手办` 这个包 | 单独一个 mod（单独工坊物品），依赖主模组 |
| 改动量 | 小：新增 2 个 `.cs` + 改 8 处小地方 | 大：主模组要先做「系列注册表 + DLC 扫描」重构 |
| 玩家安装 | 照常订阅一个物品 | 要同时订阅主模组 + DLC，并在改装件里都启用/排序 |
| 工坊 | 主物品更新 | 需要新建第二个工坊物品（`Creator.exe`） |
| 适合 | 新 IP 是长期内容、和主包一起发 | 新 IP 体积大 / 版权上想拆开 / 想单独收费或单独更新 |

**结论**：建议先把新 IP 按方案 A 跑通（本教程第 1~5 节），确认所有系统正常后再决定要不要拆成方案 B（第 6 节）。

---

## 1. 你需要提供的输入

### 1.1 角色立绘（每人一张）
- 格式：PNG（透明背景最好）/ JPG / WebP；彩色、最好是**方形**（1024×1024 或 2048×2048）
- 非方形也能用：脚本会补透明边（`--fit contain`，默认）或裁切铺满（`--fit cover`）
- 文件名随意（中文也行），但**角色表里要能对上**
- 不要用游戏截图/带水印图；像素手办的物品图会被缩到 48×48，脸和轮廓要清晰

### 1.2 角色表（CSV 或 XLSX，一行一个角色）
列名（中英文都认）：

| 列 | 必填 | 说明 |
|---|---|---|
| `file` / `文件` / `立绘` | ✅ | 立绘文件名（相对 `--src` 目录） |
| `id` / `标识` / `短id` | ✅ | 短 slug（只写 `amiya`，不要写前缀）；完整 id = `figurine_ark_amiya` |
| `name` / `名字` / `名称` | ✅ | 显示名（中文） |
| `star` / `星级` / `稀有度` | 可选 | 1~5；**留空 = 按价格自动金字塔分档**（推荐，和星铁/绝区零一致） |
| `value` / `价格` / `售价` | 可选 | 留空 = 按星级默认（5★1200 / 4★800 / 3★500 / 2★300 / 1★200） |
| `element` / `属性` / `元素` | 可选 | 工具提示里单独一行【属性】，原神写元素、星铁写「命途 · 元素」 |
| `desc` / `介绍` / `描述` | 可选 | 工具提示里的角色介绍，一句话即可 |

示例 CSV（`star`/`value` 留空 → 自动金字塔分档 + 按星级默认价）：
```csv
file,id,name,star,value,element,desc
阿米娅立绘.png,amiya,阿米娅,,,术师,罗德岛的公开领袖。
迷迭香立绘.png,rosmontis,迷迭香,,,狙击,罗德岛的精英干员。
```
> 如果表格列名就是中文（文件/名字/星级/价格/属性/介绍），脚本也认。

### 1.3 系列信息
- 系列中文名（如「明日方舟」）
- 系列短名（2~4 个小写字母，如 `ark`）→ 物品 id 前缀 `figurine_ark_`、托盘目录 `Tray\Ark\`
- 入口页 logo：一张 512×512 左右透明 PNG，命名 `logo_ark.png`，放 `Textures\Items\`（跟 GS/SR 一致）或 `Textures\` 根（跟 ZZZ 一致）都行

### 1.4 版权确认（重要）
工坊是公开内容。新 IP 的官方立绘/商标如果没有授权，不要直接打包进公开工坊（或只做私自分发）。
本教程只负责技术接入。

---

## 2. 素材规格与目录（必须严格按这个来）

### 2.1 发布目录结构（`Mods\PixelFigurines\`）
```
PixelFigurines\
  FigurineMod.dll
  manifest.xml
  Textures\
    logo_zzz.png                        ← 入口页 logo 之一（放根目录 = 加载成 texture）
    Items\
      logo_genshin.png / logo_starrail.png   ← 另外两个 logo 实际放在 Items（加载成 sprite）
      figurine_ark_amiya.png            ← 物品贴图 48×48（3×3 格 × 16px）
      ...
    （其它顾客立绘 / 原版物品图，略）
  Big\
    figurine_ark_amiya.png              ← 商店卡片高清图 256×256（按需加载）
  Tray\
    Genshin\ / StarRail\ / Zzz\
    Ark\                                ← 新 IP 托盘目录（名字要和 TrayArt 里写的一致）
      figurine_tray_ark_amiya.png           ← 512×512（按需加载）
      Fake\
        figurine_tray_ark_amiya_fake1.png   ← 512×512 反色赝品图
```
> 入口页 logo 放 `Textures\Items\logo_ark.png`（和原神/星铁一致）或 `Textures\logo_ark.png`（和绝区零一致）都可以——
> `ShopUI.ModSprite()` 会先查 `Sprites`（来自 `Textures\Items`）再查 `Textures`（来自 `Textures` 根）。
> 推荐跟 GS/SR 一样放 `Textures\Items\logo_ark.png`。

### 2.2 三种图的硬性规格

| 图 | 路径 | 尺寸 | 加载方式 | 注意 |
|---|---|---|---|---|
| 物品贴图 | `Textures\Items\<完整id>.png` | **48×48**（=`W×H` 格 × `gridSize 16`） | 启动时全部解码成 Sprite（压成 3×3 格） | 透明背景；alpha>0 的格算占格；**必须是正方形**（当前三 IP 统一 3×3） |
| 商店高清图 | `Big\<完整id>.png` | 256×256 | 卡片显示时按需读盘 | 没有会回落到物品贴图（会糊） |
| 放大镜托盘图 | `Tray\<IP>\figurine_tray_<短id>.png` | 512×512 | 放大镜打开时按需读盘（LRU 3） | 短 id 规则见 §4.9；正品/赝品分开 |
| 赝品托盘图 | `Tray\<IP>\Fake\..._fake1.png` | 512×512 | 同上 | 脚本自动生成**反色**（RGB 取反、alpha 保留） |

⚠️ **不要把 Big / Tray 的图塞进 `Textures\`**：`ModLoader.LoadTextures` 会把 `Textures\` 根和 `Textures\Items\` 下的每张 PNG 在启动时全部解码成 RGBA32 不压缩纹理；大图放进去会白吃内存（历史上吃过 +600MB 的亏）。`Big\` / `Tray\` 是模组自己按需读的，框架不管。

### 2.3 命名规则
- 完整 id：`figurine_<系列>_<slug>`，例：`figurine_ark_amiya`
- 短 id（托盘图用）：`<系列>_<slug>`，例 `ark_amiya` → 文件 `figurine_tray_ark_amiya.png`
- 所有 id 必须全局唯一、小写字母/数字/下划线
- 物品贴图文件名 = 完整 id；Big 文件名 = 完整 id；Tray 文件名 = `figurine_tray_` + 短 id

---

## 3. 一键生成贴图（已实测）

工具：`手办MOD\tools\gen_ip_assets.py`（需要 Pillow；XLSX 还要 openpyxl）。

```powershell
$py = 'C:\Users\Lenovo\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe'  # 或系统 python
& $py 'E:\深空当铺正式版mod\手办MOD\tools\gen_ip_assets.py' `
    --src  'E:\新IP立绘' `
    --table 'E:\新IP角色表.xlsx' `
    --series ark --dir-name Ark `
    --out 'E:\深空当铺正式版mod\手办MOD\资源\新IP' `
    --gen-cs 'E:\深空当铺正式版mod\手办MOD\src\FigurineMod\ArkData.g.cs'
```

参数说明：

| 参数 | 默认 | 作用 |
|---|---|---|
| `--src` | 必填 | 立绘文件夹（会递归找同名文件） |
| `--table` | 必填 | CSV / XLSX 角色表 |
| `--series` | 必填 | 系列短名，用于托盘文件名 `figurine_tray_<series>_*` |
| `--prefix` | `figurine_<series>_` | 完整 id 前缀 |
| `--dir-name` | series 首字母大写 | `Tray\<dir-name>\` 目录名 |
| `--out` | 必填 | 输出根目录，生成 `<out>\PixelFigurines\...` |
| `--items` / `--big` / `--tray` | 48 / 256 / 512 | 三种图边长 |
| `--fit` | contain | `contain` 保留完整立绘（补透明边）；`cover` 裁切铺满 |
| `--trim` | 关 | 先裁掉立绘四周透明边再缩放（有的立绘留白很多时用） |
| `--no-fake` | 关 | 不生成赝品反色托盘图 |
| `--gen-cs` | 关 | 顺便生成可编译的 `XxxData.g.cs` |
| `--class` | series 首字母大写 | C# 类名前缀（`Ark` → `ArkRaw` / `ArkData`） |

脚本会输出：
```
PixelFigurines\Textures\Items\figurine_ark_xxx.png    48×48
PixelFigurines\Big\figurine_ark_xxx.png               256×256
PixelFigurines\Tray\Ark\figurine_tray_ark_xxx.png     512×512
PixelFigurines\Tray\Ark\Fake\figurine_tray_ark_xxx_fake1.png
```

实测（2 个样例）：48/256/512 尺寸正确，反色图 RGB 正确取反、alpha 保留，退出码 0。

**非方形立绘怎么办**：默认 `contain` 会在左右/上下补透明边，物品贴图会显得小；如果立绘是竖长条又想要满格，用 `--fit cover`（会裁掉一部分上下）。也可以在作图时先裁成方形。

**生成完的分发**：
```powershell
# 覆盖到游戏部署目录（先关游戏）
Copy-Item 'E:\深空当铺正式版mod\手办MOD\资源\新IP\PixelFigurines\*' 'E:\steam\steamapps\common\Probably Stolen\Mods\PixelFigurines\' -Recurse -Force
# 以后发版时，从发布包解包→替换→再打包（见 §7）
```

---

## 4. 代码接入（方案 A：并入主模组）

先记住当前 3 个系列的写法，新 IP 照抄其中「星铁 / 绝区零」的模式即可：

| 层 | 原神 | 星铁 | 绝区零 | 新 IP（你要做的） |
|---|---|---|---|---|
| 数据文件 | `Catalog.g.cs` | `StarRailData.cs` + `StarRailData.g.cs` | `ZzzData.cs` + `ZzzData.g.cs` | `ArkData.g.cs`（脚本生成，类名前缀可改） |
| 物品注册 | `ModEntry.OnItemDirectoryInit` 直接循环 | `StarRailItems.cs` | `ZzzItems.cs` | `ArkItems.cs` |
| 前缀 | `figurine_gs_` | `figurine_sr_` | `figurine_zzz_` | `figurine_ark_` |

### 4.1 数据文件：`ArkData.g.cs`（脚本生成，可编译）

脚本 `--gen-cs` 已经生成完整文件，结构就是：
```csharp
public static class ArkRaw
{
    public const string Prefix = "figurine_ark_";
    public static readonly string[][] Rows = new string[][]
    {
        new string[] { "amiya", "阿米娅", "5", "1200", "术师", "罗德岛的公开领袖。" },
        // ...
    };
}
internal static class ArkData
{
    public struct Entry { public string Id; public string Zh; public int Star; public int Value; public string Element; public string Desc; }
    public static List<Entry> All { get; }        // 懒加载 + 自动金字塔分档 + 价格默认
    public static int PriceOf(int star);
    public static Catalog.Def[] ToCatalog();      // 给 Defs / 掉落 / 商店用
    public static bool IsLuxury(int star) { return star >= 4; }
}
```
> 想手写也行：照 `StarRailData.cs` 的 `Entry/All/ToCatalog` 抄一份，只改前缀和字段即可。

### 4.2 物品注册：新增 `ArkItems.cs`

照抄 `StarRailItems.cs`，只改 3 处：类名、数据源、日志。完整模板：

```csharp
using System;
using UnityEngine;

namespace FigurinePort
{
    /// <summary>把新 IP 的角色注册成手办物品（3×3 占格，和现有三 IP 一致）。</summary>
    internal static class ArkItems
    {
        public static void Register(ModItemDirectory dir)
        {
            if (dir == null) return;
            int n = 0;
            foreach (var e in ArkData.All)
            {
                var entry = e;
                try
                {
                    // 和星铁/绝区零一样：creator 只在「游戏自己建物」（掉落表）时被调，按掉落来源掷三态
                    if (dir.Add(entry.Id, (Func<GameItem>)(() => FigurineAuth.Loot(Build(entry))))) n++;
                }
                catch (Exception ex) { Debug.LogError(FigurineMod.LogTag + "新IP注册失败：" + entry.Id + " " + ex.GetType().Name); }
            }
            Debug.Log(FigurineMod.LogTag + "新IP手办注册 " + n + "/" + ArkData.All.Count);
        }

        public static GameItem Build(ArkData.Entry def)
        {
            try
            {
                var item = ItemDirectory.CreateEmptyItem(def.Id);
                if (item == null) return null;
                item.name = def.Zh;
                item.unitCount = 1;
                item.unitValue = def.Value;
                item.unitBaseValue = def.Value;
                // ★ 关键：贴图必须已经在 Textures\Items\<完整id>.png，且是 48×48
                item.SetSpriteAndShapeFromMod("figurine.gs", def.Id);
                try
                {
                    var sb = new GridShapeBuilder(3, 3);
                    sb.SetDataFill(3, 3, 1);
                    item.SetShape(sb.Build());
                }
                catch (System.Exception __ex) { Safe.Warn("ArkItems.cs:42", __ex); }
                try { item.SetGameItemType("ART"); } catch (System.Exception __ex) { Safe.Warn("ArkItems.cs:43", __ex); }
                if (ArkData.IsLuxury(def.Star)) { try { item.SetGameItemType("LUXURY_ITEM"); } catch (System.Exception __ex) { Safe.Warn("ArkItems.cs:44", __ex); } }
                try { InspectableHelper.InitInspectableItem(item); }
                catch { try { item.EnableTag("INSPECTABLE_TAG", true); } catch (System.Exception __ex) { Safe.Warn("ArkItems.cs:46", __ex); } }
                try { GeneralHelper.SetItemOwned(item, true); } catch (System.Exception __ex) { Safe.Warn("ArkItems.cs:45", __ex); }
                return item;
            }
            catch (Exception e) { Debug.LogError(FigurineMod.LogTag + "新IP构建失败：" + e.GetType().Name); return null; }
        }
    }
}
```

### 4.3 `ModEntry.cs`：注册入口

`OnItemDirectoryInit` 里，在 `ZzzItems.Register(dir);` 后面加一行：

```csharp
ArkItems.Register(dir);   // 新 IP
```

同时把工具提示的查表加上（**这一步漏了会导致工具提示里没有档位/属性/厂牌/介绍**）：

`ModEntry.OnTooltip` 里 `_map` 初始化处：
```csharp
foreach (var d in Catalog.All) _map[d.Id] = d;
foreach (var d in StarRailData.ToCatalog()) _map[d.Id] = d;
foreach (var d in ZzzData.ToCatalog()) _map[d.Id] = d;
foreach (var d in ArkData.ToCatalog()) _map[d.Id] = d;   // ← 新增
```

### 4.4 `Defs.cs`：让「全模组合表」认识新 IP

`Defs.All()`：
```csharp
foreach (var d in Catalog.All) ...
foreach (var d in StarRailData.ToCatalog()) ...
foreach (var d in ZzzData.ToCatalog()) ...
foreach (var d in ArkData.ToCatalog()) if (!string.IsNullOrEmpty(d.Id)) list.Add(d);   // ← 新增
```
`Defs.Ensure()`（id→Def 字典）同样加一行：
```csharp
foreach (var d in ArkData.ToCatalog()) if (!string.IsNullOrEmpty(d.Id)) map[d.Id] = d;
```

`Defs.Meta()`（工具提示的属性行）加分支：
```csharp
else if (id.StartsWith("figurine_ark_", StringComparison.Ordinal))
{
    foreach (var e in ArkData.All)
        if (e.Id == id) return "【" + (string.IsNullOrEmpty(e.Element) ? "无" : e.Element) + "】";
}
```
`Defs.Company()`（厂牌行）加分支：
```csharp
if (id.StartsWith("figurine_ark_", StringComparison.Ordinal)) return "明日方舟·鹰角网络";
```

> `Defs.All()` 被这些系统直接使用，接上以后**自动打通**：
> 米拉/白鹭/雀儿的收购清单、海子的收货清单、任务抽取池、易物池、交易板、鉴定客的未鉴定件、价格/鉴定查表、仓库/购物车结算、吸引力统计等。

### 4.5 稀有度

两种做法：
- **自动金字塔（推荐）**：角色表 `star` 列留空，`ArkData.All` 会按价格降序用 `TierQuota.StarsTopDown()` 切 30/25/20/15/10%。和星铁/绝区零一致。
- **手动星级**：表里填 1~5，代码原样使用；注意价格和星级要匹配（`RuntimeSelfCheck` 会检查价格档位一致性：5★ 1001~1200、4★ 801~1000、3★ 601~800、2★ 401~600、1★ 201~400）。

### 4.6 掉落表：`LootInjection.BuildTables()`

在 `ZzzData` 那行后面加：
```csharp
foreach (var d in ArkData.ToCatalog()) if (d.Star == star && !string.IsNullOrEmpty(d.Id)) entries.Add((d.Id, 1f));
```
> 掉落表是「每个星级一张表、表内每件权重 1」。新 IP 加入后，该星级表里每个角色等概率；如果新 IP 人数很多，会明显稀释原三 IP，必要时改权重（`entries.Add((d.Id, 0.5f))`）。

### 4.7 盲盒：最简单是并入「普通混合盒」

`BlindBox.PoolFor()` 的 `IdNormal` 分支加一行：
```csharp
else if (id == IdNormal)
{
    list.AddRange(Catalog.All);
    list.AddRange(StarRailData.ToCatalog());
    list.AddRange(ZzzData.ToCatalog());
    list.AddRange(ArkData.ToCatalog());   // ← 新增
}
```
这样「普通盲盒（三 IP 混抽）」会变成四 IP 混抽，开盒动画/发奖/概率全部自动走。

**可选：加一个专属盲盒（工作量大，按需）**，需要同时改：
1. `BlindBox.cs`：新 id 常量（如 `IdArk = "figurine_gs_blindbox_ark"`）、`AllIds`、`PriceOf`、`SpriteKeyOf`、`EmptySpriteFor`、`NameOf`、`WeightsOf`、`PoolFor` 分支；
2. 两张贴图：`Textures\Items\figurine_gs_blindbox_ark.png`（64×48）和空盒 `..._ark_empty.png`；
3. `LootInjection` 第 6/7 张表（普通档/高级档）加入新盒；
4. 海子随机盲盒池 / 首日礼物池（`Haizi.ShopBoxIds`、`Haizi.GiftBoxIds`）要不要加。
> 不推荐第一版就加专属盒，先把「普通混合盒」跑通。

### 4.8 抽奖 + 商店入口（ShopUI / Gacha）

`Gacha.PoolOf()` 加系列：
```csharp
if (series == "sr") list.AddRange(StarRailData.ToCatalog());
else if (series == "zzz") list.AddRange(ZzzData.ToCatalog());
else if (series == "ark") list.AddRange(ArkData.ToCatalog());   // ← 新增
else list.AddRange(Catalog.All);
```
`ShopUI.SeriesName()`：
```csharp
if (series == "ark") return "明日方舟";
```
`ShopUI.DrawEntry` 的**选择游戏页（page == 1）**加第 4 张卡（4 张一排，间距 270）：
```csharp
AddCard(tr, "原神",     -405, 60, font, () => DrawEntry(2), "logo_genshin");
AddCard(tr, "星穹铁道", -135, 60, font, () => DrawEntry(3), "logo_starrail");
AddCard(tr, "绝区零",    135, 60, font, () => DrawEntry(4), "logo_zzz");
AddCard(tr, "明日方舟",  405, 60, font, () => DrawEntry(5), "logo_ark");     // ← 新增
```
再加 page 5 分支（注意：现在 `DrawEntry` 的最后一段是 `else { /* page 4 绝区零 */ }`，要先把它改成 `else if (page == 4)`，再在它后面加 page 5）：
```csharp
else if (page == 4)
{
    AddCard(tr, "手办", -230, 60, font, () => { CloseEntry(); OpenWith(ZzzData.ToCatalog()); });
    AddCard(tr, "抽奖", 230, 60, font, () => DrawGachaPage("zzz"));
    AddBack(tr, font, () => DrawEntry(1));
}
else if (page == 5)   // ← 新增
{
    AddCard(tr, "手办", -230, 60, font, () => { CloseEntry(); OpenWith(ArkData.ToCatalog()); });
    AddCard(tr, "抽奖", 230, 60, font, () => DrawGachaPage("ark"));
    AddBack(tr, font, () => DrawEntry(1));
}
```
`logo_ark.png` 放 `Textures\Items\`（和 logo_genshin / logo_starrail 一样，加载成 sprite）或 `Textures\` 根（和 logo_zzz 一样，加载成 texture）都可以，`ShopUI.ModSprite` 两个字典都会查。

> 不想改入口页也行：把 `AddCard(tr, "手办", ..., () => OpenWith(Defs.All().ToArray()))` 改成全系列合表，新 IP 就会出现在「手办」页；但没有独立系列卡、也没独立抽奖池。

### 4.9 托盘图（放大镜）路径

`TrayArt.PathOf()` 加分支：
```csharp
if (key.StartsWith("figurine_tray_sr_", StringComparison.Ordinal)) sub = "StarRail";
else if (key.StartsWith("figurine_tray_zzz_", StringComparison.Ordinal)) sub = "Zzz";
else if (key.StartsWith("figurine_tray_ark_", StringComparison.Ordinal)) sub = "Ark";   // ← 新增
else sub = "Genshin";
```
`FigurineFake.TrayShortId()` 加分支（短 id → 托盘文件名）：
```csharp
const string gs = "figurine_gs_", sr = "figurine_sr_", zzz = "figurine_zzz_", ark = "figurine_ark_";
if (id.StartsWith(gs, StringComparison.Ordinal)) return id.Substring(gs.Length);
if (id.StartsWith(sr, StringComparison.Ordinal)) return "sr_" + id.Substring(sr.Length);
if (id.StartsWith(zzz, StringComparison.Ordinal)) return "zzz_" + id.Substring(zzz.Length);
if (id.StartsWith(ark, StringComparison.Ordinal)) return "ark_" + id.Substring(ark.Length);   // ← 新增
```
> 这三处必须和脚本生成的文件名一致：`Tray\Ark\figurine_tray_ark_<slug>.png`。

### 4.10 可选：赝品缺件数据

当前 `FeatureFlags`/`Variant2Ready = false`，赝品只有「反色」一种缺陷，新 IP **不需要**补缺陷数据也能玩。
以后若启用「缺件」变体（`FigurineAuth.Variant2Ready = true`），需要：
- `FigurineDefects.Parts` 里补 `短id → 缺件名`（例 `ark_amiya → 戒指`）；
- `FakeData.g.cs` 里补该角色的缺陷条目；
- 每个变体一张托盘图（反色除外）。

### 4.11 自检日志（可选但推荐）

`RuntimeSelfCheck.Run()` 里：
```csharp
int ark = ArkData.ToCatalog().Length;
...
Debug.Log(FigurineMod.LogTag + "自检：原神手办=" + catalog + " 星铁=" + sr + " 新IP=" + ark + ...);
...
try { CheckPriceTiers("新IP", new System.Collections.Generic.List<Catalog.Def>(ArkData.ToCatalog())); } catch ...
```
不影响功能，但看日志一眼知道新 IP 注册了多少件。

### 4.12 不需要改、自动打通的系统（确认清单）

- ✅ 顾客买卖：`CustomerMira/CustomerSellers` 的 `clientBuyingIdList = Defs.All()`
- ✅ 海子收货：`Haizi.Queue()` 的 `Defs.All()`
- ✅ 任务清单：`TaskList` 的 `Defs.All()`
- ✅ 以物易物：`Barter` 的 `Defs.All()`
- ✅ 鉴定客 / 估价：`FigurineAppraisal` 的 `Defs.All()` + `Defs.Find`
- ✅ 鉴定（缺损面板）/ 三态 / 赝品 / 标签机：`Defs.Find` + `FigurineAuth`（不认 IP）
- ✅ 仓库 / 购物车 / 交易板结算：`Defs.Find`
- ✅ 吸引力：`FigurineAttractionPatch` 的 `Defs.Find`
- ✅ 商店 5★ 一律不上架：`ShopUI` 按 `d.Star < 5` 过滤，自动适用于新 IP（5★ 只从抽奖 / 盲盒 / 顾客来货）
- ✅ 抽奖 / 交易板 / 顾客来货的建物：`FigurineMod.BuildFigurine(def)` 是通用函数，只依赖 `Catalog.Def`，新 IP 不用另写
- ⚠️ 交易板还有一个历史小问题：`TradeBoard.Generate` 先 `Defs.All()` 又手动 `AddRange(StarRail/Zzz)`，星铁/绝区零被加了**两遍**（新 IP 只通过 `Defs.All()` 进，不会重复）。顺手可把两行手动 AddRange 删掉。

---

## 5. 构建 / 部署 / 验收

### 5.1 构建 + 部署
```powershell
cd 'E:\深空当铺正式版mod\手办MOD\src\FigurineMod'
dotnet build -c Release -v minimal --nologo        # 0 error

# 先关游戏，再部署
Copy-Item bin\Release\FigurineMod.dll 'E:\steam\steamapps\common\Probably Stolen\Mods\PixelFigurines\FigurineMod.dll' -Force
# 新 IP 贴图：按 §3 覆盖到 Textures\Items / Big / Tray
```
> 注意：`src\ShowPage.cs` 是开发中的展示页，不在 `FigurineMod` 文件夹里，不参与编译；打包前检查 DLL 里不要有 `ShowPage/show_stage/bg_galaxy`。

### 5.2 日志验收（进游戏后看 Player.log，搜 `[手办]`）
```
[ModLoader] Item sprite loaded: figurine_ark_xxx (像素手办)    ← 每个物品贴图都加载到了
[手办] 新IP手办注册 N/N
[手办] 自检：原神手办=120 星铁=96 新IP=N | ...
[手办] 自检·鉴定：...
[手办] 新IP 分档：1★… 2★… 3★… 4★… 5★…        ← 自动金字塔时
[手办] 掉落注入：建表 7/7...
```

### 5.3 游戏内验收清单（按系统逐项）
- [ ] 商店入口 → 新系列卡 → 手办页能看到全部角色，卡片大图不糊、名字/价格正确
- [ ] 工具提示：【属性】/【档位】/ 吸引力 / 介绍 / 厂牌都显示
- [ ] 购买/购物车/仓库取出/卖出（`Defs.Find` 链）正常
- [ ] 抽奖（新系列池）：单抽/十连/UP/保底
- [ ] 盲盒（普通混合盒）能开出新 IP
- [ ] 垃圾场掉落（对应星级表）能翻到新 IP
- [ ] 顾客（米拉/白鹭/雀儿）会收新 IP；海子也会按星级加价收
- [ ] 任务清单/交货、以物易物能抽到新 IP
- [ ] 交易板出现新 IP（价格/标签）
- [ ] 放大镜：托盘 512 图正常；赝品显示反色图
- [ ] 鉴定/估价：未鉴定件、赝品折扣
- [ ] 吸引力：持有新 IP 后店铺吸引力数字增加
- [ ] 存档 → 重读：新 IP 物品还在，名字/价格不丢

### 5.4 常见错误排查

| 现象 | 原因 |
|---|---|
| 日志 `Mod sprite 'figurine_ark_xxx' from mod 'figurine.gs' not found` | 图不在 `Textures\Items\`，或文件名 ≠ 完整 id，或 PNG 打不开 |
| 物品 48×48 但占格不显示 / 占格不对 | 贴图不是 48×48，或不是 3×3（`W/H` 与像素不对应） |
| 卡片白块 | `Big\<id>.png` 缺失（会回落小图，但不会白；白块通常是加载失败，检查是否 PNG） |
| 托盘图缺失 warning | `Tray\<IP>\` 目录名 / 短 id / 前缀三处不一致 |
| 掉落/抽奖没有新 IP | `LootInjection.BuildTables` 或 `Gacha.PoolOf` 没加 |
| 工具提示只有三态、没有档位/介绍 | `ModEntry.OnTooltip` 的 `_map` 没加新 IP |
| 顾客不收新 IP | `Defs.All()` 没加（顾客收购清单来自它） |
| 交易板/任务/易物没有 | 同上，`Defs.All()` |
| 图片被拉伸/裁掉 | `--fit` 选错；竖长立绘用 `cover` 或先裁方 |

---

## 6. 方案 B：独立 DLC 包（工作量大，先看第 0 节再决定）

### 6.1 为什么现在不能直接做独立 DLC
当前代码的「系列」是三处硬编码：
- `Defs.cs`：`Catalog.All + StarRailData + ZzzData` 三个来源写死
- `LootInjection.BuildTables`：三个来源写死
- `BlindBox.PoolFor / Gacha.PoolOf / ShopUI.DrawEntry / TrayArt.PathOf / FigurineFake.TrayShortId`
- `ModEntry.OnTooltip` 的 `_map`
- `RuntimeSelfCheck`

而且 `GameItem.SetSpriteAndShapeFromMod(modId, key)` 明确取「**某个 mod** 的 Sprites 字典」——新 IP 的贴图如果在独立 DLC mod 里，就必须用 DLC 的 modId 去建物品，不能再用 `"figurine.gs"`。

### 6.2 推荐做法：主模组「扫描式 DLC」
1. 主模组先重构成 `SeriesRegistry`（每个系列：`Key / Prefix / DisplayName / ModId / Data(ToCatalog) / TrayFolder / MetaLine / Company`），把上面所有硬编码点改成遍历注册表。
2. 主模组在 `OnGameLoadedEarly`（此时所有 mod 的 `OnEnable` 都跑过了）遍历 `ModLoader.Mods`，找 `Manifest.ID` 以 `figurine.dlc.` 开头的已启用 mod：
   - 读它的 `FolderPath\figurine_dlc.json`（系列名 / 前缀 / Tray 目录 / 角色行 / 星级 / 价格 / 属性 / 介绍）；
   - 用 `SetSpriteAndShapeFromMod(dlcModManifest.ID, id)` 建物品（贴图在 DLC 的 `Textures\Items\`，框架会替它加载）；
   - 把系列注册进 `SeriesRegistry`；`Defs.All()`、掉落、盲盒池、抽奖、商店入口全部改成遍历注册表。
3. DLC 包结构：
```
Mods\FigurineDlcArk\
  manifest.xml            ID=figurine.dlc.ark
                          <Prerequisites><Mod>figurine.gs</Mod></Prerequisites>
                          <Assemblies><Assembly>DlcArk.dll</Assembly></Assemblies>
                          <Capabilities>files, reflection-external</Capabilities>
  DlcArk.dll             只要实现一个空 IMod（框架要求 manifest 至少列一个 DLL）
  figurine_dlc.json      { series/prefix/trayDir/rows[...] }
  Textures\Items\figurine_ark_*.png
  Big\...（主模组 CardArt 目前只读自己目录，需要改成按注册表找 DLC 目录）
  Tray\Ark\...（TrayArt 同理）
```
4. 加载顺序：官方框架的 `<Prerequisites>` 只做**校验+警告**（`ModLoader.ValidatePrerequisites`），**不会强制排序**；改装件列表里的顺序是玩家可调的。所以 DLC 不要依赖「自己在主模组之后 OnEnable」——让**主模组主动扫描**（上面的做法）就与顺序无关。
5. 工坊发布：DLC 是**第二个工坊物品**（用 `Creator.exe` 新建，之后用 `Updater.exe <dlcId> <内容文件夹> <预览图>` 更新）；玩家必须同时订阅并启用主模组 + DLC。
6. 版权/体积：DLC 可单独下架/替换，适合版权敏感内容；主包体积不增加。

### 6.3 方案 B 的额外成本
- 主模组重构（约 8~10 个文件）+ 回归所有现有系统；
- DLC 数据契约（json schema、版本兼容、缺件/托盘路径）；
- 工坊双包维护、玩家安装说明；
- 排错面变大（DLC 没启用、顺序、版本不匹配）。

> 如果不是「必须拆开卖/拆开装」，**不要一开始就上方案 B**。

---

## 7. 发版（沿用现有流程）
1. 改 `src\FigurineMod\manifest.xml` 的 `ModVersion`（如 1.0.4）
2. `dotnet build -c Release` → 复制 DLL 到部署目录
3. 从上一版发布包解包到临时目录 → 替换 DLL / manifest / 更新日志 / 模组详情 + 新 IP 贴图
4. 检查临时目录：无 `show_stage.png` / `bg_galaxy.png`；DLL 无 `ShowPage` 字符串
5. `Updater.exe 3833497202 <临时目录\PixelFigurines> <预览图>` → 看到 `FINAL result=k_EResultOK`
6. 压缩本地包 `发布\像素手办-vX.Y.Z.zip`；Steam Web API 复核 `time_updated`（见交接 §20）

---

## 8. 改动清单速查（方案 A）

| 文件 | 必须? | 改什么 |
|---|---|---|
| `ArkData.g.cs` | ✅ | 角色数据（脚本生成） |
| `ArkItems.cs` | ✅ | 物品注册 + Build |
| `ModEntry.cs` | ✅ | `OnItemDirectoryInit` 注册；`OnTooltip` 的 `_map` |
| `Defs.cs` | ✅ | `All` / `Ensure` / `Meta` / `Company` |
| `LootInjection.cs` | ✅ | 掉落表加入新系列 |
| `BlindBox.cs` | ✅ | `IdNormal` 混合池加入（专属盒可选） |
| `Gacha.cs` | ✅* | 新系列抽奖池（不办独立系列可跳过） |
| `ShopUI.cs` | ✅* | 入口页系列卡 / 手办页 / 抽奖页（可先把新 IP 塞进「手办」合表） |
| `TrayArt.cs` | ✅ | 托盘目录分支 |
| `FigurineFake.cs` | ✅ | `TrayShortId` 前缀分支 |
| `RuntimeSelfCheck.cs` | 推荐 | 自检日志/价格检查加新 IP |
| `FigurineDefects.cs` / `FakeData.g.cs` | 可选 | 只有启用「缺件」变体才需要 |
| `manifest.xml` / 更新日志 / 模组详情 | ✅ | 版本号与描述 |

---

## 9. 一句话流程

**丢立绘到文件夹 → 写角色表 → 跑 `gen_ip_assets.py`（出 48/256/512 + 反色 + ArkData.g.cs）→ 加 `ArkItems.Register` → `Defs/Loot/BlindBox/Gacha/ShopUI/TrayArt/TrayShortId` 各加一处 → build → 按验收清单点一遍 → 发版。**

需要我拿到你的立绘和角色表后直接按这套接进去，也可以把表/目录路径发我。
