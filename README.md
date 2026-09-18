# Rookie100 百天任务指引（Oxygen Not Included 模组）

> A free, non-commercial educational quest-guide mod for *Oxygen Not Included* (ONI). It turns the staged teaching outline of Bilibili creator **「大叔追云彩」(*Uncle Chasing Clouds*)** — *《缺氧新手活过100天》(Survive 100 Days in ONI)*, a 34-episode series — into an executable in-game quest tree, so new players learn the full beginner-to-space progression by playing, without alt-tabbing to videos.

## 功能特性

- **6 阶段 34 任务主线**：活下来 → 工业化 → 深度开发 → 太空拓展，线性任务链，前一任务领取奖励后解锁下一任务
- **任务状态机**：锁定 → 可接取 → 已接取 → 进行中 → 可领取 → 已领取，状态徽章全部使用**游戏原生精灵**（无字体豆腐块）
- **实时建造检测**：每 2 秒扫描殖民地建筑，目标达成自动通知；面板打开时签名门控实时刷新徽章与迷你进度条
- **打印舱奖励**：奖励以游戏原生补给包机制（`CarePackageInfo.Deliver`）投放，不修改游戏平衡
- **建筑引导卡**：任务目标建筑的大图标 + 解锁所需的科技链（研究状态/研究点需求）+ 一键打开研究面板
- **视频章节定位**：每个任务附带对应视频的分段索引，点击直达 B 站对应时刻
- **材料侧栏**：所选任务建筑的材料清单（按建筑类别解析可用元素）与推荐布局示意
- **进度按存档隔离**：`quest_progress.json` 多档案注册表（v3），每个殖民地独立进度（claimed/accepted），切换存档互不干扰；旧版共享进度自动迁移到首个载入的存档
- **语言跟随**：界面语言自动跟随游戏本体（中文/英文），无手动开关
- **视觉原生**：浅色官方主题面板 + 红色官方描边 + `web_box`/`web_button` 官方精灵 + 拖拽位置记忆

## 安装

- **创意工坊（推荐）**：Steam 创意工坊搜索订阅「Rookie100 百天任务指引」。
- **本地手动安装**：将发布包解压至 `%USERPROFILE%\Documents\Klei\OxygenNotIncluded\mods\Local\Rookie100`，游戏内开启。
- 开发部署目录：`mods\Dev\Rookie100`（见下）。

## 构建与开发

```powershell
.\build.ps1      # Release 编译并直接部署到 mods\Dev\Rookie100
.\watch-log.ps1  # 实时跟踪 Player.log 中的 [Rookie100] 日志
```

- 需求：**.NET SDK 8+**（编译链无需 Visual Studio，csproj 通过 `Microsoft.NETFramework.ReferenceAssemblies` 提供 net48 引用程序集）
- 游戏路径在 `Rookie100.csproj` 中配置（环境变量 `ONI_GAME_PATH` 或命令行 `/p:GamePath=...`，默认 Steam 库路径）
- 游戏运行中只能**仅编译检查**：`dotnet build Rookie100.csproj`（复制 DLL 会因文件锁失败）；正式部署前请关闭游戏
- 日志位置：`%USERPROFILE%\AppData\LocalLow\Klei\Oxygen Not Included\Player.log`，搜索 `[Rookie100]`

## 项目结构

```
Rookie100.csproj            SDK 风格项目（net48 / C# 9 / 编译即部署）
ModEntry.cs                 模组入口（KMod.UserMod2：内容加载 + Harmony PatchAll）
ModLogger.cs                统一日志（写入 Player.log，[Rookie100] 前缀）
QuestScanner.cs             场景建筑扫描 → PrefabID 计数（目标检测数据源）
QuestTracker.cs             任务追踪器（2s 周期检测 + 通知 + 领取奖励）
RewardsService.cs           打印舱补给包投放（CarePackageInfo）
Content/QuestModels.cs      任务数据模型（阶段/任务/目标/奖励/视频章节）
Content/QuestStore.cs       任务仓库 + 状态机 + 元素 id 别名纠正 + 进度持久化
UI/QuestPanel.cs            主面板：任务树 / 详情 / 引导模态窗 / 材料侧栏（~2700 行）
UI/StatusIcons.cs           状态与段落图标的集中精灵映射（Image 渲染，替代 emoji）
UI/Lang.cs                  界面语言探测（跟随游戏 locale）+ zh→en 文案表
UI/LayoutStore.cs           窗口布局持久化
UI/WindowDrag.cs            面板拖拽
UI/ManagementMenuInstaller.cs 管理菜单按钮注入
Patches/                    Harmony 补丁：游戏生命周期挂载 / 主菜单存活确认 / 管理菜单面板入口
quests.json                 34 任务内容包（目标/前置/奖励/视频分段，中英双语字段）
mod.yaml / mod_info.yaml    模组元数据（静态 ID / 标题 / 双语文案描述 / 最低版本）
```

### 核心数据流

```
quests.json ─加载→ QuestStore（状态机 + 按存档进度档案）
                                │
Game.OnSpawn ─激活档案→ ActivateColony(saveFolder)（每存档独立 claimed/accepted）
                                │
QuestScanner.CountAllBuildings()┤（每 2s）
                                ▼
QuestTracker.Update → 目标达成 → 游戏内通知 + QuestCompleted 事件
                                │
                    QuestPanel（订阅事件 + 打开时签名门控实时刷新）
                                │ 点击「领取奖励」
                                ▼
RewardsService → CarePackageInfo.Deliver（打印舱补给包）→ MarkClaimed → 事件刷新
```

## 技术栈选型

| 层 | 选择 | 理由 |
|---|---|---|
| 语言/运行时 | C# 9.0 / .NET Framework 4.8（net48） | 与游戏 Mono 运行时 ABI 一致 |
| 运行时补丁 | Harmony 2.4.2（游戏内置 0Harmony.dll，MIT） | ONI modding 标准补丁方案 |
| JSON | Newtonsoft.Json 7.0.1（游戏内置，MIT） | 内容包解析，复用游戏已加载程序集、零额外分发 |
| 编译 | dotnet SDK + ReferenceAssemblies 包（MIT） | 免 VS 编译链；版本快照可复现 |
| UI | uGUI + TextMeshPro + 游戏原生 Sprite 表 | 与官方界面观感一致，规避字体缺字问题 |
| 内容驱动 | 单一 `quests.json`（中英双语字段） | 任务可改数据不改代码 |

## 关键挑战与解决方案

1. **emoji 渲染为豆腐块**：TMP 默认字体缺字形 → 全部状态/段落图标改用游戏原生 Sprite（运行时枚举 1920 个实存精灵后集中映射 `StatusIcons`），仅保留字体支持的 ▶▼✕ 等字符。
2. **`Missing prefab: Coal` 与奖励投放失败**：U59 中煤的元素 id 是 `Carbon`（`Coal` 只是 oreTag）→ 数据层修正 + `QuestStore.CanonicalElementId` 别名表双保险，`unknown` 灰色兜底图不再视为命中。
3. **建筑白模图标显示为灰白**：`Def.GetUISprite` 返回 `Tuple<Sprite, Color>`，必须应用第二项着色；解析链 `Assets.GetSprite → Def.GetUISprite` 按 key 诊断一次日志。
4. **面板开着时进度不刷新**：事件只覆盖"达成/领取"，部分进度推进无事件 → 面板 `Update` 每 2s 重扫建筑计数做**签名比对**，仅变化才重建 UI（避免滚动位置被重置）。
5. **界面语言**：自研切换开关被否决 → 反射探测游戏 locale（候选链 `GetLocale/GetCurrentLanguageCode/...`），`zh*` 走中文否则英文，建筑/科技/元素名一律查游戏 `STRINGS` 本地化。
6. **运行时 API 不确定性**：Golden rule = 所有可疑 API 先写一次性运行时探针（反射枚举类型/字段/方法并落 Player.log）验证后落地正式代码，探针用完即删。
7. **研究面板打开路径**：反射循环误命中属性 getter 造成假成功 → 实锤公开方法 `ManagementMenu.OpenResearch()` 直调。
8. **网络与发布**：git HTTPS 直连 GitHub 在本机被掐断 → 走 `gh` + Git Data API（blobs→tree→commit→ref）通道；安全策略不允许直改 main → 固定采用"新分支 + Pull Request"工作流。
9. **PowerShell 编码**：PS 5.1 + GBK 会毁中文脚本 → 脚本一律纯 ASCII（必要时 UTF8 BOM）。
10. **多存档进度混淆**：进度原为模组级单文件、所有存档共享 → 改为按存档（`SaveLoader.saveFolder`）分档案的 v3 注册表，载入殖民地时激活对应档案，读写真只作用于当前档案，旧数据一次性迁移。

## 分支管理策略（GitHub Flow）

- `main`：唯一稳定分支，任何变更**禁止直推**，一律经 Pull Request 合并
- 功能分支：`feature/*`（新功能）、`docs/*`（文档/描述）、`fix/*`（修复）
- 每次 PR 一个主题、一条描述性提交信息（`type: summary` 约定）
- 里程碑发布：合并后打 `vX.Y.Z` 轻量 tag 并创建 GitHub Release（附 changelog）

## 版本历史

- **v0.5.0**（当前）：任务进度**按存档隔离**（每殖民地独立档案 + 旧数据自动迁移）
- **v0.4.0**：游戏原生精灵徽章（emoji 豆腐块清零）、Coal→Carbon 奖励修复、面板实时刷新、语言跟随、双语文案与免责声明
- **v0.3.x**：官方级 UI 升级（红色描边窗口、建筑图标+着色、资料卡化详情区）、建筑引导模态窗
- **v0.1–v0.2**：垂直切片（4 任务全链路验证）→ 34 任务内容包与四阶段主线

## 未来优化方向

- [ ] 随阶段切换的生态主题背景（轻量=色调渐变+生物群系精灵点缀；重量=AI 绘制风格插画）
- [ ] 推荐布局从 ASCII 升级为游戏精灵网格，并覆盖全部 22 个建筑任务
- [ ] 34 任务描述文本扩充（更多细节与避坑提示）
- [ ] DLC（Spaced Out! 等）组合兼容性验证
- [ ] 模组设置页（热度/界面亮度等个性化选项）

## 致谢与许可

- 教学大纲内容源自 B 站视频《缺氧新手活过100天》（[合集 · 大叔追云彩](https://www.bilibili.com/video/BV1JP7N6oEsW)），仅做**提纲式引用**（时间戳与小标题），不含任何影像/音频/逐字稿，相关权利归原作者
- UI 布局与配色风格参考社区模组 **StorageNetwork（作者 pether-pg）**（[GitHub 镜像 ChiYuKe/ONI-Mods](https://github.com/ChiYuKe/ONI-Mods/tree/master/StorageNetwork)），未使用其素材与代码
- 感谢 Harmony 与缺氧 modding 社区的公开教程
- 代码采用 [MIT License](LICENSE)；本模组与 Klei Entertainment 及 B 站 UP 主无隶属关系