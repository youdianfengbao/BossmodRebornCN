using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace BossMod;

[ConfigDisplay(Name = "技能调整", Order = 4)]
public sealed class ActionTweaksConfig : ConfigNode
{
    // TODO: consider exposing max-delay to config; 0 would mean 'remove all delay', max-value would mean 'disable'
    [PropertyDisplay("从瞬发技能中移除额外的延迟诱导动画锁定延迟（阅读提示！）", tooltip: "请勿与XivAlexander或NoClippy一起使用 - 检测到它们时应自动禁用此功能，但请先双重检查！")]
    public bool RemoveAnimationLockDelay = false;

    [PropertyDisplay("动画锁定最大模拟延迟（阅读提示！）", tooltip: "配置使用动画锁定移除时的最大模拟延迟（毫秒）- 这是必需的且不能减少到零。设置为20ms将在使用自动循环时启用三连编织。移除三连编织的最小设置为26ms。FFLogs已接受20ms的最小值，且不应导致日志问题。")]
    [PropertySlider(20, 50, Speed = 0.1f)]
    public int AnimationLockDelayMax = 20;

    [PropertyDisplay("移除额外的帧率诱导冷却延迟", tooltip: "动态调整冷却和动画锁定，确保排队动作立即解决，无论帧率限制如何")]
    public bool RemoveCooldownDelay = false;

    [PropertyDisplay("施法时禁止移动", tags: ["slidecast"])]
    public bool PreventMovingWhileCasting = false;

    public enum ModifierKey
    {
        [PropertyDisplay("无")]
        None,
        [PropertyDisplay("Control")]
        Ctrl,
        [PropertyDisplay("Alt")]
        Alt,
        [PropertyDisplay("Shift")]
        Shift,
        [PropertyDisplay("鼠标左键+右键")]
        M12
    }

    [PropertyDisplay("按住此键以允许施法时移动（需要启用上述设置）", tooltip: "需要同时勾选上述设置", tags: ["slidecast"])]
    public ModifierKey MoveEscapeHatch = ModifierKey.None;

    [PropertyDisplay("目标死亡时自动取消施法")]
    public bool CancelCastOnDeadTarget = false;

    [PropertyDisplay("Prevent movement and action execution when pyretic-like mechanics are imminent (set to 0 to disable, otherwise increase threshold depending on your ping).")]
    [PropertySlider(0, 10, Speed = 0.01f)]
    public float PyreticThreshold = 1.0f;

    [PropertyDisplay("自动错误定向：如果正常运动和错误定向之间的角度大于此阈值，则防止在错误定向下运动（设置为 180 可关闭此功能）。")]
    [PropertySlider(0, 180)]
    public float MisdirectionThreshold = 180f;

    [PropertyDisplay("使用技能后恢复角色朝向")]
    public bool RestoreRotation = false;

    [PropertyDisplay("对鼠标悬停目标使用技能")]
    public bool PreferMouseover = false;

    public bool SmartTargeting = false;

    [PropertyDisplay("对手动按下的动作使用自定义队列", tooltip: "此设置允许与自动循环更好地集成，并在自动循环进行时按下治疗技能时防止三连编织或GCD漂移")]
    public bool UseManualQueue = false;

    [PropertyDisplay("Try to prevent dashing into AOEs", tooltip: "Prevent automatic use of targeted dashes (like WAR Onslaught) if they would move you into a dangerous area. May not work as expected in instances that do not have modules.\n\nThis option will also apply to manually pressed dashes if you have \"Use custom queueing for manually pressed actions\" enabled.")]
    public bool DashSafety = true;

    [PropertyDisplay("将上一选项应用于所有冲刺，而非仅突进技能", tooltip: "包括后退冲刺（如武士的燕返）、传送（如忍者的残影）和固定距离冲刺（如龙骑士的回避跳跃）")]
    public bool DashSafetyExtra = true;

    [PropertyDisplay("自动管理自动攻击", tooltip: "此设置可防止在倒计时期间过早开始自动攻击，在开怪时、切换目标时以及使用任何不会明确取消自动攻击的技能时自动启动自动攻击。")]
    public bool AutoAutos = false;

    [PropertyDisplay("自动下坐骑以执行技能")]
    public bool AutoDismount = true;

    public enum GroundTargetingMode
    {
        [PropertyDisplay("通过额外点击手动选择位置（正常游戏行为）")]
        Manual,

        [PropertyDisplay("在鼠标当前位置施放")]
        AtCursor,

        [PropertyDisplay("在所选目标位置施放")]
        AtTarget
    }
    [PropertyDisplay("地面目标技能的自动目标选择")]
    public GroundTargetingMode GTMode = GroundTargetingMode.Manual;

    public bool ActivateAnticheat = true;

    private static bool IsRSREnabled()
    {
        try
        {
            const string rsrName = "Rotation Solver Reborn";
            foreach (var p in Service.PluginInterface.InstalledPlugins)
            {
                if ((p.Name.Equals(rsrName, StringComparison.OrdinalIgnoreCase) || p.InternalName.Equals(rsrName, StringComparison.OrdinalIgnoreCase)) && p.IsLoaded)
                {
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    public override void DrawCustom(UITree tree, WorldState ws)
    {
        ImGui.AlignTextToFramePadding();
        UIMisc.HelpMarker("如果常规（鼠标悬停/主要）目标对动作无效，则自动选择下一个最佳目标（例如给副坦使用退避）");
        ImGui.SameLine();
        var rsrEnabled = IsRSREnabled();
        using var color = ImRaii.PushColor(ImGuiCol.Text, 0xFF0000FFu, rsrEnabled);
        if (ImGui.Checkbox("智能技能目标选择（不要与 RSR 同时使用）", ref SmartTargeting))
        {
            Modified.Fire();
        }
    }
}
