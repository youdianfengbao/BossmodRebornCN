namespace BossMod.Autorotation;

[ConfigDisplay(Name = "自动循环配置(插件暂不支持 by Combat Reborn)", Order = 5)]
public sealed class AutorotationConfig : ConfigNode
{
    [PropertyDisplay("显示游戏内界面")]
    public bool ShowUI = false;

    public enum DtrStatus
    {
        [PropertyDisplay("禁用")]
        None,
        [PropertyDisplay("仅文字")]
        TextOnly,
        [PropertyDisplay("带图标")]
        Icon
    }

    [PropertyDisplay("在服务器信息栏显示当前预设")]
    public DtrStatus ShowDTR = DtrStatus.None;

    [PropertyDisplay("隐藏VBM默认预设方案", tooltip: "如果你已创建自定义预设方案且不再需要内置默认预设，勾选此选项将使其不再显示在自动旋转和预设编辑窗口中。")]
    public bool HideDefaultPresets = true;

    public bool SuggestHealerAI = true;

    [PropertyDisplay("Show positional hints in world", tooltip: "Show tips for positional abilities, indicating to move to the flank or rear of your target")]
    public bool ShowPositionals = false;

    [PropertyDisplay("跟随 RotationSolverReborn 请求的身位", tooltip: "启用后，“杂项 AI：移动到指定身位”循环模块会覆盖自身身位设置，改为通过 IPC 使用 RotationSolverReborn 当前请求的身位（不适用于木桩）")]
    public bool FollowRSRDesiredPositional = true;

    [PropertyDisplay("死亡时自动禁用自动循环")]
    public bool ClearPresetOnDeath = true;

    [PropertyDisplay("脱战后自动禁用自动循环")]
    public bool ClearPresetOnCombatEnd = false;

    [PropertyDisplay("Automatically disable autorotation if a Luring Trap is triggered", tooltip: "Only applicable in Deep Dungeons")]
    public bool ClearPresetOnLuring = false;

    [PropertyDisplay("Automatically reenable force-disabled autorotation when exiting combat")]
    public bool ClearForceDisableOnCombatEnd = true;

    [PropertyDisplay("提前开怪判定阈值", tooltip: "当队伍成员在倒计时剩余时间超过此值时进入战斗，将被判定为提前开怪并强制禁用自动循环")]
    [PropertySlider(0, 30, Speed = 1)]
    public float EarlyPullThreshold = 1.5f;

    [PropertyDisplay("无倒计时开怪时禁用自动循环", tooltip: "仅在激活冷却计划时适用。")]
    public bool PlannedPullSafety = true;
}
