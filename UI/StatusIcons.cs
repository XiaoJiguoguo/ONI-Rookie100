using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rookie100.UI
{
    /// <summary>
    /// 集中式游戏原生精灵映射：所有状态/段落图标一律用 Image+Sprite 渲染，
    /// 不依赖 TMP 字体字形（emoji 在 LiberationSans SDF 中缺字，显示为豆腐块）。
    /// 精灵名全部来自运行时枚举 Assets.SpriteAssets 验证过的 1920 个实存精灵。
    /// 替换入口唯一：需要什么语义图标只在这里加常量。
    /// </summary>
    public static class StatusIcons
    {
        // ---- 任务状态徽章 ----
        public const string Locked = "lock";
        public const string Available = "circle_small";
        public const string Accepted = "ic_checklist";
        public const string InProgress = "icon_clock";
        public const string ReadyClaim = "icon_star";
        public const string Claimed = "web_checkmark";

        // ---- 目标行小状态 ----
        public const string ObjectiveDone = "web_checkmark";
        public const string ObjectiveTodo = "circle_small";

        // ---- 段落/功能标题 ----
        public const string Brief = "codexTipsAndInfo";
        public const string Objectives = "targetIcon";
        public const string Rewards = "printing_pod_whiteline";
        public const string Video = "codexVideo";
        public const string Knowledge = "codexIconLessons";
        public const string GuideBuilding = "codexIconBuildings";
        public const string GuideTech = "codexIconResearch";
        public const string Unlocks = "icon_category_base";
        public const string Materials = "overlay_materials";
        public const string Layout = "icon_display_screen_blueprint";
        public const string Positive = "icon_positive";

        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> missingReported = new HashSet<string>();

        /// <summary>按精灵名取游戏原生 Sprite（仅查 UI 精灵表 Assets.GetSprite），结果缓存。</summary>
        public static Sprite Get(string spriteKey)
        {
            if (string.IsNullOrEmpty(spriteKey))
            {
                return null;
            }

            if (cache.TryGetValue(spriteKey, out Sprite cached))
            {
                return cached;
            }

            Sprite sp = null;
            try
            {
                sp = Assets.GetSprite(spriteKey);
            }
            catch
            {
                sp = null;
            }

            cache[spriteKey] = sp;
            if (sp == null && missingReported.Add(spriteKey))
            {
                ModLogger.Warn("[Icons] 精灵缺失: Assets.GetSprite('" + spriteKey + "') = null");
            }

            return sp;
        }

        /// <summary>
        /// 在父节点下创建一个 LayoutGroup 友好的图标 Image（正方形，preserveAspect，不吃点击）。
        /// 精灵缺失时 Image 禁用（不占位视觉，仅留布局宽度）。着色由调用方设置 color。
        /// </summary>
        public static Image CreateIconImage(string name, Transform parent, string spriteKey, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = size;
            layout.preferredHeight = size;
            layout.minWidth = size;
            layout.minHeight = size;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            Image img = go.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            Sprite sp = Get(spriteKey);
            if (sp != null)
            {
                img.sprite = sp;
                img.enabled = true;
            }
            else
            {
                img.enabled = false;
            }

            return img;
        }
    }
}
