# 任务界面角色动画

本地集成基于 upstream main `9733cd46a463aa0a9c69e1240cfa8d73938db88f`。

任务列表顶部新增透明角色，不参与任务列表的滚动和重建。面板打开时循环待机，任务完成或成功领取奖励时播放一次欢呼；同一轮检测的多个事件不会反复重启动画。关闭面板后解除订阅并停止更新，再次打开恢复待机。采用真实时间播放，游戏暂停不影响 UI 动画。

资源 `Resources/guide_dupe.png` 是单张 2048×1536 RGBA 图集，16 列、8 行，每格 128×192，前 41 帧待机、后 83 帧欢呼，30 FPS。所有帧共用裁切范围与脚部原点。贴图和动作来自本机游戏；不包含样片的背景、字幕和调试标记。

PNG 嵌入 `Rookie100.dll`，运行时只创建一个 Texture2D，通过 RawImage.uvRect 切帧，不逐帧创建精灵或纹理。解码后释放 CPU 贴图副本，面板销毁时释放 GPU 纹理。加载失败仅隐藏角色并记录日志，不阻止任务面板打开。

构建方式：原项目 `dotnet build Rookie100.csproj -c Debug /p:GamePath=C:\STEAM\steamapps\common\OxygenNotIncluded`，或当前动画工作区的 `scripts/build_mod.ps1`（便携 Roslyn，无需系统 SDK）。Release 原项目会直接输出到 Dev 模组目录，检查代码时请使用 Debug。

验证：完整源码已用本机游戏 Managed DLL 编译通过；透明图集关键帧已检查。真实组件在离线 Unity 替身中通过资源加载、真实时间播放、完成/领取事件、重复事件不重启、欢呼结束回待机、五次关闭/重开、纹理与事件释放检查。这不替代 Unity 游戏内验收；实际面板的布局、事件和关闭/重开行为仍需进游戏验收。没有修改上游 GitHub 仓库。

安装：关闭游戏，备份现有模组目录，将输出包的 Rookie100.dll、mod.yaml、mod_info.yaml、quests.json 放入原 Rookie100 模组目录。不要同时启用另一个 Rookie100 副本，也不要删除原来的 quest_progress.json。重新开启游戏后打开百天任务面板。

验收：打开面板检查顶部待机角色；完成或领取一个任务检查欢呼；暂停游戏检查动画继续；滚动列表检查角色固定；关闭再打开检查恢复待机；切换存档检查正常重建。

游戏素材归 Klei Entertainment。动画导出使用 kanimtool：https://github.com/tsangwpx/kanimtool 。
