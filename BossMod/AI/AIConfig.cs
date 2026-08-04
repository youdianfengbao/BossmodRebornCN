namespace BossMod.AI;

[ConfigDisplay(Name = "AI 配置 (AI 处于非常实验阶段，请自行承担风险！)", Order = 7)]
sealed class AIConfig : ConfigNode
{
    [PropertyDisplay("在 DTR 条中显示状态")]
    public bool ShowDTR = false;

    [PropertyDisplay("显示 AI 界面")]
    public bool DrawUI = false;

    [PropertyDisplay("将目标领袖设为焦点")]
    public bool FocusTargetMaster = false;

    [PropertyDisplay("将按键广播到其他窗口", tooltip: "在某些电脑上可能导致卡顿。仅在确实需要时启用！此功能只对多开玩家有用。")]
    public bool BroadcastToSlaves = false;

    [PropertyDisplay("跟随小队位置")]
    public int FollowSlot = 0;

    [PropertyDisplay("禁止动作")]
    public bool ForbidActions = false;

    [PropertyDisplay("手动目标选择")]
    public bool ManualTarget = false;

    [PropertyDisplay("禁止移动")]
    public bool ForbidMovement = false;

    [PropertyDisplay("战斗中跟随")]
    public bool FollowDuringCombat = true;

    [PropertyDisplay("在主动 Boss 模块期间跟随")]
    public bool FollowDuringActiveBossModule = true;

    [PropertyDisplay("战斗外跟随")]
    public bool FollowOutOfCombat = false;

    [PropertyDisplay("跟随目标")]
    public bool FollowTarget = true;

    [PropertyDisplay("跟随目标时期望位置(任意/侧面/背面/正面)")]
    [PropertyCombo(["Any", "Flank", "Rear", "Front"])]
    public Positional DesiredPositional = Positional.Any;

    [PropertyDisplay("到插槽的最大距离")]
    public float MaxDistanceToSlot = 1f;

    [PropertyDisplay("到目标的最大距离")]
    public float MaxDistanceToTarget = 2.6f;

    [PropertyDisplay("到碰撞箱的最小距离")]
    public float MinDistance = default;

    [PropertyDisplay("到禁止区域的偏好距离")]
    public float PreferredDistance = default;

    [PropertyDisplay("Enable auto AFK", tooltip: "Enables auto AFK if out of combat. While AFK AI will not use autorotation or target anything")]
    public bool AutoAFK = false;

    [PropertyDisplay("Auto AFK timer", tooltip: "Time in seconds out of combat until AFK mode enables. Any movement will reset timer or disable AFK mode if already active.")]
    public float AFKModeTimer = 10f;

    [PropertyDisplay("禁用障碍物地图加载", tooltip: "部分内容（如深层迷宫）可能需要启用此选项。")]
    public bool DisableObstacleMaps = false;

    [PropertyDisplay("移动决策延迟", tooltip: "谨慎修改此值并保持较低数值！过高可能导致无法及时应对某些机制。请注意根据不同内容调整此值。")]
    public double MoveDelay = default;

    [PropertyDisplay("骑乘时保持静止")]
    public bool ForbidAIMovementMounted = false;

    [PropertyDisplay("将斜杠命令回显到聊天")]
    public bool EchoToChat = true;

    [PropertyDisplay("跟随 RotationSolverReborn 请求的身位", tooltip: "启用后，自动移动会通过 IPC 使用 RotationSolverReborn 当前请求的身位")]
    public bool FollowRSRDesiredPositional = true;

    public string? AIAutorotPresetName;
}
