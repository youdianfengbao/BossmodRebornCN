using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.BeastChess.BC2KnightBishop;

// 双 boss 场（骑士 primary，主教同场需击杀）。
// AID 数据来自 2026-09-12 两份回放（10_21_47 / 10_59_25）交叉验证 + xivapi Action 表（CastType/EffectRange/XAxisModifier），
// 形状映射遵循 AIHintsBuilder.GuessShape 的仓库权威约定。
public enum OID : uint
{
    Boss = 0x4B86, // 奇子·骑士（primary，HP 8424，R0.9，槽位 A）
    Bishop = 0x4B87, // 奇子·主教（HP 5616，R0.9，同场敌人）
    Helper = 0x233C,
}

public enum AID : uint
{
    ForwardGuard = 46864, // 前线护卫：骑士->self，4.7s cast，自身增益（CastType1/ER0，无伤害形状）
    KnightUnknown = 46865, // (无名)：骑士，no cast，事件占位（两回放各 1 次）
    Tumulus = 46866, // 古墓：骑士->self，4.7s cast，圆形 R6（CastType2/ER6）
    DeathSpiralVisual = 46867, // 死亡螺旋：主教->self，4.7s cast，visual（伤害见 46868）
    DeathSpiral = 46868, // 死亡螺旋：Helper->self，5.7s cast，月环 3-40（CastType10/ER40）
    AncientAeroVisual = 46869, // 古代疾风：主教->self，4.7s cast，visual（伤害见 46870）
    AncientAero = 46870, // 古代疾风：Helper->self，5.4s cast，矩形 40x8（CastType12/ER40/XAxisMod8）
    Ossify = 46871, // 骨化：骑士->self，7.7s cast，自身增益（无伤害形状）
    Skullsplinter = 46872, // 粉身碎骨：骑士->player，4.7s cast，单体（回放实测命中距离 2.5-2.9y）
    BlackEruptionVisual = 46873, // 黑火喷发：主教->self，4.7s cast，visual（伤害见 46874/46900）
    BlackEruption = 46874, // 黑火喷发：Helper->location，5.7s cast，地面圆 R5（CastType2/ER5）
    BlackEruptionMinor = 46900, // 黑火喷发（小）：Helper->location，1.2s cast，地面圆 R5；多 Helper 八方位高频施放（两回放 48/56 次）
    KnightAutoAttack = 50784, // 骑士自动攻击：no cast（两回放 28/27 次）
    BlizzardAutoAttack = 50788, // 冰结：主教自动攻击，no cast 单体（两回放 14/14 次）
}

sealed class KnightAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCircle Tumulus = new(6f);
    private static readonly AOEShapeDonut DeathSpiral = new(3f, 40f);
    private static readonly AOEShapeRect AncientAero = new(40f, 4f);
    private static readonly AOEShapeCircle BlackEruption = new(5f);

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.Tumulus => new(Tumulus),
        (uint)AID.DeathSpiral => new(DeathSpiral),
        (uint)AID.AncientAero => new(AncientAero),
        (uint)AID.BlackEruption => new(BlackEruption, LocationTargeted: true),
        _ => null,
    };
}

// 黑火小圈流：多个 Helper（八方位）几乎同时在散布位置施放，读条仅 1.2s
sealed class BlackEruptionChains(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCircle Shape = new(5f);

    protected override int MaxDisplayed => 8;

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.BlackEruptionMinor => new(Shape, LocationTargeted: true),
        _ => null,
    };
}

sealed class Skullsplinter(BossModule module) : Components.SingleTargetCast(module, (uint)AID.Skullsplinter);

sealed class BC2KnightBishopStates : StateMachineBuilder
{
    public BC2KnightBishopStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<KnightAOEs>()
            .ActivateOnEnter<BlackEruptionChains>()
            .ActivateOnEnter<Skullsplinter>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(BC2KnightBishopStates),
    ConfigType = null,
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Uncategorized,
    GroupType = BossModuleInfo.GroupType.CFC,
    GroupID = 1088u,
    NameID = 14531u, // 奇子·骑士（回放 ACT+ 提取）
    SortOrder = 2,
    PlanLevel = 0)]
public sealed class BC2KnightBishop(WorldState ws, Actor primary) : BeastChessModule(ws, primary)
{
    public static readonly uint[] EnemiesOfInterest = [(uint)OID.Boss, (uint)OID.Bishop];

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actors(this, EnemiesOfInterest);
}
