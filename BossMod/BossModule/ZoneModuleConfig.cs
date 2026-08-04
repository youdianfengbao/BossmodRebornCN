namespace BossMod;

[ConfigDisplay(Name = "任务 / 副本全自动模式", Order = 6)]
public sealed class ZoneModuleConfig : ConfigNode
{
    [PropertyDisplay("加载区域模块所需的最低完成度")]
    public BossModuleInfo.Maturity MinMaturity = BossModuleInfo.Maturity.Contributed;

    [PropertyDisplay("启用自动执行任务战斗/单人任务")]
    public bool EnableQuestBattles = false;

    [PropertyDisplay("在游戏世界中绘制路径点")]
    public bool ShowWaypoints = false;

    [PropertyDisplay("使用冲刺技能进行导航（速涂、回避跳跃, etc）")]
    public bool UseDash = true;

    [PropertyDisplay("显示xan调试UI")]
    public bool Lock = false;

    [PropertyDisplay("Make zone module windows transparent", tooltip: "Removes the black window around zone module windows; this will not work if you move the radar to a different monitor")]
    public bool TransparentMode = false;
}
