# 宿舍场景教程短片

`bedroom.mp4` 是本 Mod 编排的教学短片，采用完整场景、镜头推进和原版动作素材；并非官方发布的视频。36 秒，960×540，30 fps，H.264/yuv420p，无音轨。字幕没有烧入画面，由 `bedroom.lesson.json` 根据游戏语言显示；数字步骤标记为通用画面元素。

五个镜头：0 秒预留与放置、6 秒取料与搬运、12 秒施工床铺、20 秒墙门围合、28 秒房间检查。17～20 秒缩短重复建造其他床铺的过程，并在字幕明确说明。场景和光束是教学编排，房间着色不表示玩家存档已完成任务。

游戏端：LessonFilmPlayer 使用 Unity VideoPlayer（原版教程的视频路径也使用此组件），从本地 Mod 目录加载 MP4。使用 UnscaledGameTime，支持暂停、重播、步骤跳转、暂停后恢复画面、关闭面板暂停和错误提示。现在所有36个运行任务都加载各自的教程短片，完整45项教学目录均有短片资源。详见 ALL_TASK_FILMS.md。

验证：全部 1080 帧已通过 FFmpeg 解码，时长 36 秒；检查编码后的五个关键镜头。游戏 DLL 编译 0 警告、0 错误；72 项逻辑检查通过，包括字幕边界、单语言、最终字幕、步骤时间与字幕缺口拒绝。Unity 游戏内解码、步骤跳转和窗口适配仍需实际体验确认。

制作脚本：先用 `extract_assets.py` 从本机安装目录提取所需素材到 `.tools/lesson-assets`，再运行 `make_bedroom.py`。工作依赖为 UnityPy、Pillow、numpy、imageio-ffmpeg；当前使用 `.tools/unitypy` 和 `.tools/video-deps` 下的本地依赖。原始提取素材留在 `.tools`，安装包只包含成片与字幕清单。

本次画面调整：参考本机原版 Digging 教程的蓝色渐变、淡蓝图纹理，重新绘制背景；施工使用 dig_fwd 动作与 constructor_gun 手部挂点，搬运使用胸前挂点。砂岩用于床铺和地砖、铜矿用于门，素材来自对应原版 KAnim。施工光束仍为合成效果，地砖边缘尚未复刻原版。若需完整原版粒子、地形边缘及真实搬运施工行为，下一步应使用固定测试存档录制游戏场景，再接入现有播放器与字幕。

门使用 door_manual 的 closed 状态，地面与墙砖改用 floor_basic 原版素材。墙砖与门仍是压缩展示，而非逐块模拟施工，字幕已对应说明。`bedroom-preview-zh.mp4` 是烧录中文字幕的独立预览；游戏继续使用无烧录成片和按语言切换的 JSON 字幕，避免重复字幕。`bedroom.zh.srt` / `bedroom.en.srt` 用于外部播放器。生成预览命令：`make_bedroom.py --subtitles`。

尺寸依据本机 U59 游戏 Assembly-CSharp：BedConfig 为2×2，ManualPressureDoorConfig 为1×2；Database.RoomTypes.Barracks 使用 HAS_BED / NO_INDUSTRIAL_MACHINERY / MINIMUM_SIZE_12 / MAXIMUM_SIZE_64，不含 CEILING_HEIGHT_4。示例内部 x180..796（14格）、y300..432（3格），共42格；门在右侧边界，墙砖不计入内部。数字标注保持语言中立，解释通过本地化字幕显示。床铺画面按2×2格调整。
压缩保持960×540、30fps、H.264/yuv420p与faststart，改用CRF23、slow预设；实际大小见构建验证记录。播放器现有16:9 FitInParent、RenderTexture及非缩放游戏时间播放逻辑继续适用；未通过桌面工具实际操控游戏验证。

本次成片从2,072,634字节缩至1,308,993字节，约减少36.8%；两份视频均通过完整解码，检查3秒/25秒/31秒压缩后画面。

全任务扩展：见 [ALL_TASK_FILMS.md](ALL_TASK_FILMS.md)。新增任务短片为24fps，独立JSON语言字幕；宿舍保持30fps和现有五镜头时间轴。
