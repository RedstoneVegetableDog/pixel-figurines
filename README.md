# 像素手办（PixelFigurines）

《深空当铺 / Probably Stolen》的像素手办模组：把 276 位像素手办、盲盒、鉴定、收藏和交易塞进你的当铺。

> 当前版本：**v1.1.1** ｜ 模组 ID：`figurine.gs` ｜ 工坊 ID：`3833497202`
> 游戏版本：`0.46D`（实测 `VERSION 100-REV13`）

## 这是什么

三套 IP 共 **276 个像素手办**：

| IP | 数量 |
|---|---|
| 原神 | 120 |
| 崩坏：星穹铁道 | 96 |
| 绝区零 | 60 |

五档稀有度（1★~5★），带完整的经济与鉴定玩法：

- **盲盒 / 抽奖**：普通盒与高级盒，双击开盒、开盒动画、重复件处理
- **正品 / 赝品 / 未鉴定**：鉴定前只显示标签（可能错标），用放大镜勾缺损揭晓真身；鉴定后价格跟标签走
- **鉴定客与鉴定证书**：顾客带未鉴定手办上门，收鉴定费；考取手办鉴定委员会证书，提高客源与收益
- **顾客买卖**：海子、米拉、白鹭、雀儿等专属顾客，各有台词与定价逻辑
- **垃圾场掉落 / 交易板 / 以物易物**
- **DORO 终端、货物邮箱（9×12 容器）、展示柜、手办收纳柜**

## 安装

1. 安装游戏《Probably Stolen》，并启用官方 ModLoader。
2. 把本模组文件夹放进游戏的 `Mods\` 目录（文件夹名任意，例如 `Mods\PixelFigurines\`），确保里面有：
   - `manifest.xml`
   - `FigurineMod.dll`
   - `Textures\`、`Localization\`、`Big\` 等资源目录
3. 启动游戏，在模组列表中启用「像素手办」。

Steam 创意工坊版本：<https://steamcommunity.com/sharedfiles/filedetails/?id=3833497202>

## 从源码构建

需要 .NET SDK（项目目标框架 `netstandard2.1`，引用游戏目录下的程序集）：

```powershell
cd src\FigurineMod
# GameDir 默认是 E:\steam\steamapps\common\Probably Stolen，装在别处就覆盖它：
dotnet build -c Release -v minimal -p:GameDir="D:\SteamLibrary\steamapps\common\Probably Stolen"
```

构建产物：`src\FigurineMod\bin\Release\FigurineMod.dll`。
把 DLL 和 `src\FigurineMod\manifest.xml`，连同本仓库 `资源\` 下打包后的贴图/本地化文件一起放进游戏 `Mods\<文件夹>\` 即可。

> 完整发布结构可参考 `资源\`、`docs\` 里的说明；构建只产出 DLL，贴图与本地化由 `tools\` 下的 Python 脚本生成/整理。

## 目录结构

```
src/FigurineMod/      C# 源码（Harmony 补丁 + 官方 IMod 接口）
资源/                 贴图、展示图、盲盒立绘等素材
tools/                生成贴图/占格/IP 素材的 Python 脚本
docs/                 开发文档、交接记录、新 IP 教程
manifest.xml          模组清单（src/FigurineMod/ 下）
```

## 兼容性说明

- v1.1.1 已移除全部外部反射（`reflection-external`），manifest 只声明 `files`，兼容新版 ModLoader 的能力扫描。
- 高清卡片图（`Big\`）按需读盘，所以需要 `files` 能力。
- 模组与其它 Mod 的兼容性以实测为准；遇到冲突请带 `Player.log` 反馈。

## 开源协议

本项目采用 **CC BY-NC 4.0（署名—非商业性使用 4.0 国际）**：
<https://creativecommons.org/licenses/by-nc/4.0/>

- ✅ 允许：个人使用、修改、分享、二次创作
- ❌ 禁止：任何商业用途（售卖、付费整合包、付费下载、以盈利为目的的再分发等）
- 必须保留署名与协议说明

完整条款见 [LICENSE](LICENSE)。

## 同人素材声明

- 《原神》《崩坏：星穹铁道》《绝区零》的角色、名称与相关美术版权归 **miHoYo / HoYoverse** 所有。
- 本模组是非商业同人作品，与米哈游、Questing Goose Studio 均无隶属或授权关系。
- 游戏本体及其素材版权归 **Questing Goose Studio** 所有；本仓库不包含游戏本体文件。
- 若版权方要求，作者会立即下架相关素材。
- **请勿将本模组或其中素材用于任何商业用途，也不要上传到付费平台/付费整合包。**

## 作者

红石菜狗（Steam：月关）

欢迎 Issue / PR 反馈 bug、平衡建议或新 IP 手办数据。
