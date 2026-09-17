using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rookie100.UI
{
    /// <summary>
    /// 把"新手百天助手"按钮注入原版管理菜单（顶部 Vitals/Schedule/... 按钮栏）。
    /// 手法：克隆原版按钮 prefab，改文案/图标/点击行为。（模式移植自 StorageNetwork）
    /// </summary>
    internal static class ManagementMenuInstaller
    {
        private const string ButtonName = "Rookie100ManagementButton";

        public static void Install(ManagementMenu menu)
        {
            if (menu == null)
            {
                return;
            }

            AddManagementButton(menu);
        }

        private static void AddManagementButton(ManagementMenu menu)
        {
            Transform parent = ResolveToggleParent(menu);
            if (parent == null)
            {
                ModLogger.Warn("管理菜单按钮注入失败：找不到 toggleParent");
                return;
            }

            if (parent.Find(ButtonName) != null)
            {
                return;
            }

            KToggle template = ResolveTemplate(menu);
            if (template == null)
            {
                ModLogger.Warn("管理菜单按钮注入失败：找不到可克隆的按钮模板");
                return;
            }

            GameObject buttonObject = Object.Instantiate(template.gameObject, parent, false);
            buttonObject.name = ButtonName;
            buttonObject.SetActive(true);

            KToggle button = buttonObject.GetComponent<KToggle>();
            button.ClearOnClick();
            button.group = null;
            button.isOn = false;
            button.interactable = true;

            ImageToggleState toggleState = buttonObject.GetComponent<ImageToggleState>();
            toggleState?.SetInactive();

            // 文案
            LocText label = buttonObject.GetComponentInChildren<LocText>(true);
            if (label != null)
            {
                label.key = string.Empty;
                label.enableAutoSizing = false;
                label.fontSize = Mathf.Min(label.fontSize, 15f);
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.SetText(Lang.T("百天助手"));
                label.text = Lang.T("百天助手");

                RectTransform labelRect = label.rectTransform();
                if (labelRect != null)
                {
                    labelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 84f);
                }

                RectTransform buttonRect = button.GetComponent<RectTransform>();
                if (buttonRect != null)
                {
                    buttonRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                        Mathf.Max(buttonRect.rect.width, 96f));
                }
            }

            // M0 阶段隐藏模板图标（M3 换 AI 生成的 dupe 图标）
            if (button.fgImage != null)
            {
                button.fgImage.enabled = false;
            }

            HierarchyReferences references = buttonObject.GetComponent<HierarchyReferences>();
            if (references != null)
            {
                DisableIfPresent(references, "ResearchIcon");
                DisableIfPresent(references, "AlertImage");
                DisableIfPresent(references, "GlowImage");
                DisableIfPresent(references, "CheckMark");
                DisableIfPresent(references, "Checkmark");
                DisableIfPresent(references, "Notification");
                DisableIfPresent(references, "TopRightIcon");
            }

            ToolTip toolTip = button.GetComponent<ToolTip>() ?? button.gameObject.AddComponent<ToolTip>();
            toolTip.SetSimpleTooltip(Lang.T("新手百天助手：从入门到太空的分阶段课程（大叔追云彩《活过100天》系列）"));

            button.onClick += () =>
            {
                button.isOn = false;
                toggleState?.SetInactive();
                KMonoBehaviour.PlaySound(GlobalAssets.GetSound("HUD_Click", false));
                ModLogger.Log("管理菜单按钮被点击");
                QuestPanel.Show();
            };

            // 插到星图按钮之前
            button.transform.SetSiblingIndex(GetInsertIndex(parent));
            ModLogger.Log("管理菜单按钮注入成功");
        }

        private static Transform ResolveToggleParent(ManagementMenu menu)
        {
            Traverse traverse = Traverse.Create(menu);
            Transform toggleParent = traverse.Field("toggleParent").GetValue<Transform>();
            return toggleParent != null ? toggleParent : menu.transform;
        }

        private static KToggle ResolveTemplate(ManagementMenu menu)
        {
            Traverse traverse = Traverse.Create(menu);
            var toggles = traverse.Field("toggles").GetValue<List<KToggle>>();
            KToggle liveTemplate = toggles != null
                ? toggles.FirstOrDefault(toggle =>
                    toggle != null &&
                    toggle.gameObject != null &&
                    toggle.gameObject.activeSelf &&
                    IsPrimaryManagementButton(toggle))
                : null;
            if (liveTemplate != null)
            {
                return liveTemplate;
            }

            KToggle template = traverse.Field("researchButtonPrefab").GetValue<KToggle>();
            if (template != null)
            {
                return template;
            }

            template = traverse.Field("smallPrefab").GetValue<KToggle>();
            if (template != null)
            {
                return template;
            }

            return toggles != null ? toggles.FirstOrDefault(toggle => toggle != null) : null;
        }

        private static int GetInsertIndex(Transform parent)
        {
            int starMapIndex = parent.childCount;
            for (int i = 0; i < parent.childCount; i++)
            {
                string name = parent.GetChild(i).name;
                if (name.Contains("STARMAP"))
                {
                    starMapIndex = i;
                    break;
                }
            }

            return Mathf.Clamp(starMapIndex, 0, parent.childCount);
        }

        private static void DisableIfPresent(HierarchyReferences references, string key)
        {
            Component component = references.GetReference(key);
            if (component != null)
            {
                component.gameObject.SetActive(false);
            }
        }

        private static bool IsPrimaryManagementButton(KToggle toggle)
        {
            LocText label = toggle.GetComponentInChildren<LocText>(true);
            if (label == null)
            {
                return false;
            }

            string text = label.text;
            return text == global::STRINGS.UI.VITALS ||
                   text == global::STRINGS.UI.CONSUMABLES ||
                   text == global::STRINGS.UI.JOBS ||
                   text == global::STRINGS.UI.SCHEDULE ||
                   text == global::STRINGS.UI.SKILLS;
        }
    }
}
