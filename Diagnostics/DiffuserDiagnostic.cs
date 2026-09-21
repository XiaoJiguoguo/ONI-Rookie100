using System;
using System.Text;
using Rookie100.UI;

namespace Rookie100.Diagnostics
{
    internal static class DiffuserDiagnostic
    {
        internal const string PrefabName = "MineralDeoxidizer";
        internal const string SelectionHint = "请先在游戏中选中一台已建成的氧气扩散器，再打开此引导并点击检查。";
        internal const string ScopeHint = "这是所选建筑的本次检查结果。多项问题可以同时存在；处理后让游戏运行片刻，再点击检查。结果较长时可上下滚动查看。";

        internal static string InspectSelected()
        {
            try
            {
                var selected = SelectTool.Instance == null ? null : SelectTool.Instance.selected;
                if (selected == null || !selected.gameObject.activeInHierarchy)
                    return Lang.T(SelectionHint);

                var building = selected.GetComponent<BuildingComplete>();
                var prefab = selected.GetComponent<KPrefabID>();
                if (building == null || prefab == null || prefab.PrefabTag.Name != PrefabName)
                    return Lang.T(SelectionHint);

                var operational = selected.GetComponent<Operational>();
                var energy = selected.GetComponent<EnergyConsumer>();
                var converter = selected.GetComponent<ElementConverter>();
                var electrolyzer = selected.GetComponent<Electrolyzer>();
                var health = selected.GetComponent<BuildingHP>();
                bool enabled;
                bool? manualDisabled = null;
                bool? automationDisabled = null;
                if (operational != null && operational.Flags != null)
                {
                    if (selected.GetComponent<BuildingEnabledButton>() != null &&
                        operational.Flags.TryGetValue(BuildingEnabledButton.EnabledFlag, out enabled))
                        manualDisabled = !enabled;
                    if (selected.GetComponent<LogicOperationalController>() != null &&
                        operational.Flags.TryGetValue(LogicOperationalController.LogicOperationalFlag, out enabled))
                        automationDisabled = !enabled;
                }
                // The native Electrolyzer state machine (also used by this prefab)
                // attaches PressureOk specifically while in its overpressure state.
                // Do not guess a pressure limit or read unrelated vent status items.
                var statusItems = Db.Get()?.BuildingStatusItems;
                bool? overpressure = electrolyzer == null || statusItems?.PressureOk == null
                    ? (bool?)null : selected.HasStatusItem(statusItems.PressureOk);
                var findings = DiffuserRules.Evaluate(
                    operational == null ? (bool?)null : operational.IsActive,
                    operational == null ? (bool?)null : operational.IsOperational,
                    energy == null ? (bool?)null : energy.HasWire,
                    energy == null ? (bool?)null : energy.IsPowered,
                    converter == null ? (bool?)null : converter.HasEnoughMassToStartConverting(true),
                    overpressure, manualDisabled, automationDisabled,
                    health == null ? (bool?)null : health.IsBroken);

                ModLogger.Log("[Diagnostics] Diffuser snapshot: " + string.Join(",", findings));

                var result = new StringBuilder();
                foreach (var finding in findings)
                {
                    if (result.Length > 0) result.Append("\n\n");
                    result.Append(Lang.T(Message(finding)));
                }
                result.Append("\n\n").Append(Lang.T(ScopeHint));
                return result.ToString();
            }
            catch (Exception ex)
            {
                ModLogger.Warn("[Diagnostics] Diffuser inspection unavailable: " + ex.GetType().Name);
                return Lang.T("暂时无法读取建筑状态，请重新选择建筑后再试。");
            }
        }

        private static string Message(DiffuserFinding finding)
        {
            switch (finding)
            {
                case DiffuserFinding.Running: return "【正在运行】\n当前正在制氧。单台建筑运行不代表整个殖民地氧气充足；请结合氧气分布和复制人的消耗判断。";
                case DiffuserFinding.NoWire: return "【未接电线】\n原因：建筑电力接口未接入电线，无法获得工作所需的电力。\n检查：打开电力视图，沿建筑电力接口检查电线是否连续接到供电电路。";
                case DiffuserFinding.NoPower: return "【已接线，但无电】\n原因：当前没有获得供电，不等同于手动或自动化关闭。\n检查：打开电力视图，查看同一电路的发电机是否工作、电池是否有电，以及电线是否断开或损坏。";
                case DiffuserFinding.NoInput: return "【建筑内藻类不足】\n原因：氧气扩散器消耗藻类制氧，建筑内的藻类不足以开始转换。有电也不能代替藻类。\n检查：先看建筑内容物和殖民地藻类库存；若有库存却未送达，再检查复制人能否到达、门的通行权限、运送工作安排和建筑优先级。这里没有判断殖民地是否已耗尽藻类。";
                case DiffuserFinding.Overpressure: return "【周围气压过高】\n原因：游戏已将这台建筑置于过压停机状态，暂时不能继续向周围排出氧气；这不代表缺电或缺藻类。\n检查：打开气体视图，查看建筑排气位置附近格子的气体质量，以及房间是否封闭、气体通道是否被砖块或门挡住。先确认有需要供氧的相邻空间，再改善气体流通；不要仅因过压就继续加建制氧设备。";
                case DiffuserFinding.ManualDisabled: return "【已被手动禁用】\n原因：建筑的手动开关当前为关闭，即使有电、有藻类也不会工作。\n检查：选中建筑，在原生操作面板中启用它；若操作需要复制人执行，等待操作完成后复查。";
                case DiffuserFinding.AutomationDisabled: return "【被自动化关闭】\n原因：自动化控制当前不允许建筑运行，通常对应控制端口的红色信号；这与电路没有电是两回事。\n检查：打开自动化视图，沿控制端口检查线路、传感器阈值和逻辑门输出。如果这是你设计的停机条件，可以保持关闭。";
                case DiffuserFinding.Broken: return "【建筑已损坏】\n原因：游戏判定建筑已损坏，无法正常工作。\n检查：查看建筑原生状态中的损坏原因、维修材料和复制人通路；先处理持续损坏的原因，再安排维修。";
                case DiffuserFinding.OtherConditions: return "【还有其他运行条件未满足】\n已检查项目中未找到明确原因，但游戏仍不允许建筑运行。请查看建筑原生状态提示，不要据此直接判定为缺电、缺藻类或过压。";
                case DiffuserFinding.Idle: return "【当前未在制氧】\n已检查项目中未发现明确阻碍。建筑可能正在切换状态；让游戏运行片刻后重新检查，并对照原生状态提示。";
                default: return "【部分状态未能读取】\n本次无法完成全部检查。已列出的原因仍可参考，未列出的原因不能据此排除；请结合建筑原生状态提示。";
            }
        }
    }
}
