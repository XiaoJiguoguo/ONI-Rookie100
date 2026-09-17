# Rookie100 新手百天助手（Oxygen Not Included 模组）

以 B站 UP 主[大叔追云彩](https://space.bilibili.com/433159168)的《缺氧新手活过100天》34 集系列视频为内容蓝本，
打造的**游戏内任务系统模组**：从入坑第一间厕所到火箭升空通关太空，34 个环环相扣的任务引导萌新一步步玩懂缺氧。

## 功能

- **34 任务主线**：四阶段（活下来 → 工业化 → 深度开发 → 太空拓展），线性任务链
- **任务状态机**：锁定 → 可接取 → 已接取 → 进行中 → 可领取 → 已领取
- **建造检测**：实时扫描殖民地建筑，达成目标自动完成任务并弹游戏内通知
- **打印舱奖励**：任务奖励以游戏原生补给包形式（CarePackageInfo）从打印舱投放
- **进度持久化**：quest_progress.json，跨启动/跨存档保留，可一键重置
- **官方级 UI**：StorageNetwork 风格浅色面板 + 红色官方描边 + web_box/web_button 官方精灵 + 拖拽位置记忆 + 游戏资源图标 + 迷你进度条
- **视频章节定位**：每任务附带对应视频的分段索引，点击直达 B 站对应时刻

## 构建

```
需求：.NET SDK 8+ / 缺氧（Steam）安装在默认库或通过参数指定
.\build.ps1          # 编译即部署到 mods\Dev\Rookie100
.\watch-log.ps1      # 实时监视 Player.log 中的模组日志
```

编译链无需 Visual Studio（csproj 含 ReferenceAssemblies 包）。
游戏路径可在 Rookie100.csproj 或 build.ps1 的 `-GamePath` 参数中配置。

## 内容来源与致谢

- 教学大纲内容源自 B 站视频《缺氧新手活过100天》（[合集 · 大叔追云彩](https://www.bilibili.com/video/BV1JP7N6oEsW)），仅做提纲式提炼与引用，非原视频转载
- UI 框架模式参考开源模组 [StorageNetwork](https://github.com/ChiYuKe/ONI-Mods/tree/master/StorageNetwork)（ChiYuKe 出品）
- 本模组仅为教学辅助，与 Klei / B站 UP 主无隶属关系

## 目录结构

```
Rookie100.csproj        编译即部署 (net48)
ModEntry.cs             模组入口
QuestScanner.cs         场景建筑扫描（任务目标检测）
QuestTracker.cs         任务追踪器（2s 周期检测 + 通知 + 领取）
RewardsService.cs       打印舱补给包投放
Content/QuestModels.cs  任务数据模型
Content/QuestStore.cs   任务仓库 + 状态机 + 进度持久化
UI/QuestPanel.cs        主面板（任务树/详情/领取/视频章节）
UI/ManagementMenuInstaller.cs  管理菜单按钮注入
UI/WindowDrag.cs        拖拽 + 位置记忆
UI/LayoutStore.cs       UI 布局持久化
Patches/                Harmony 补丁（菜单/生命周期/存活检查）
quests.json             34 任务内容包（目标/奖励/视频章节）
```