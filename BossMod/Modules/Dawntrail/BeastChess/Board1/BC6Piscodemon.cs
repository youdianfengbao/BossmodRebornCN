// 斗兽奇弈·低段第一盘（Board1）分支场：刺鱼魔（用户未选到该路线，参数为上游值未回放验证）
using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.BeastChess.Board1.BC6Piscodemon;

// 刺鱼魔场（D 槽，方 20x20，中心 (120,0)）。机制池：冰暴 exaflare 直线推进、核爆型全屏距离衰减、十字雷、月环风、R100 全屏。
// OID/AID 与形状来自上游 CombatReborn PiscodemonPiece.cs（wen，CrucibleOfTheUnbroken FirstBoard），并经 Kano 7.5.6.1 实测 DLL 交叉校验：
// - Kano dict_shapes：46889=圆 R5 / 46892=十字 50x5 / 46897=月环 5-60 / 49886=圆 R5，与上游全部一致；
// - 46895 Kano 以 R100 实心圆显示且不进 AI 禁区（CastHints："核爆：尽量远离落点，距离越远伤害越低"），
//   上游 Proximity 28f 为自注估计值，不采用（见 VoidFlareStarNuke）；
// - exaflare 每跳 6y / 1.1s x6 段为上游自注估计值（Kano 字典仅记录首段 R5，无展开数据），待回放验证。
public enum OID : uint
{
    Boss = 0x4B8A, // 奇子·刺鱼魔（NameID 14535，槽位 D）
    Helper = 0x233C,
}

public enum AID : uint
{
    Thunder = 50745, // 雷电：boss->player，no-cast，自动攻击单体
    VoidBlizzardIIIAnimation = 46887, // 虚空暴雪：boss->self，2.5+0.5s cast，引导 visual（无 AOE）
    VoidBlizzardIIICircle = 49886, // 虚空暴雪：Helper->location，4.5s cast，圆 R5（冰块掷出落点 = exaflare 首爆点标记）
    VoidBlizzardIII = 46888, // 虚空暴雪：Helper->location，3.0s cast，单体伤害（不画）
    VoidBlizzardIII2Animation = 49887, // 虚空暴雪：boss->self，no-cast，引导 visual 2（无 AOE）
    VoidBlizzardIIIFirst = 46889, // 虚空暴雪：Helper->self，6.0s cast，圆 R5（exaflare 首段）
    VoidBlizzardIIIRest = 46890, // 虚空暴雪：Helper->self，no-cast，圆 R5（exaflare 后续段，按 cast event 推进）
    ClearMind = 46898, // 凝神：boss->self，4.0s cast，自身伤害提升 buff（无 AOE）
    ArcaneBlast = 46899, // 玄奥爆发：boss->self，8.0s cast，R100 全屏伤害
    VoidFlareStarAnimation = 46893, // 虚空耀星：boss->self，2.5+0.5s cast，引导 visual（无 AOE）
    VoidFlareStar = 46894, // 虚空耀星：Helper->location，3.0s cast，单体伤害（不画）
    VoidFlareStar1 = 46895, // 虚空耀星：Helper->self，6.0s cast，核爆型全屏距离衰减（以落点为中心，越远伤害越低）
    VoidThunderIIIAnimation = 46891, // 虚空震雷：boss->self，3.0s cast，引导 visual（无 AOE）
    VoidThunderIII = 46892, // 虚空震雷：Helper->self，5.0s cast，十字 50x5
    VoidAeroIIIAnimation = 46896, // 虚空劲风：boss->self，5.2+0.8s cast，引导 visual（无 AOE）
    VoidAeroIII = 46897, // 虚空劲风：Helper->self，6.0s cast，月环 5-60
}

// 冰块落点标记：exaflare 的首爆点（Kano dict_shapes 49886 同为圆 R5 落点定向）
sealed class VoidBlizzardIIICircles(BossModule module) : ReplayValidatedCastAOEs(module)
{
    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.VoidBlizzardIIICircle => new(new AOEShapeCircle(5f), LocationTargeted: true),
        _ => null,
    };
}

// 冰暴 exaflare：从落点沿直线逐段推进爆炸（半径 R5、每跳 6y、1.1s/跳、6 段；上游估计值，Kano 无展开记录）
sealed class VoidBlizzardIIIExaflares(BossModule module) : Components.SimpleExaflare(module, 5f, (uint)AID.VoidBlizzardIIIFirst, (uint)AID.VoidBlizzardIIIRest, 6f, 1.1d, 6, 2, castEvent: true);

// 玄奥爆发：8s 读条 R100 全屏
sealed class ArcaneBlast(BossModule module) : Components.RaidwideCast(module, (uint)AID.ArcaneBlast);

// 虚空耀星核爆：以落点为中心的全屏距离衰减（Kano 实测处理：R100 实心圆显示 + 不进 AI 禁区；
// 上游 Proximity 28f 估计值不采用）。全场无绝对安全区，AI 无法规划避让，仅显示提示玩家远离落点。
sealed class VoidFlareStarNuke(BossModule module) : ReplayValidatedCastAOEs(module)
{
    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.VoidFlareStar1 => new(new AOEShapeCircle(100f), LocationTargeted: true),
        _ => null,
    };

    protected override int MaxRisky => 0; // 与 Kano 一致：Risky=false，不进 AI 禁区
}

sealed class VoidThunderIIICross(BossModule module) : ReplayValidatedCastAOEs(module)
{
    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.VoidThunderIII => new(new AOEShapeCross(50f, 5f)),
        _ => null,
    };
}

sealed class VoidAeroIIIDonut(BossModule module) : ReplayValidatedCastAOEs(module)
{
    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.VoidAeroIII => new(new AOEShapeDonut(5f, 60f)),
        _ => null,
    };
}

sealed class BC6PiscodemonStates : StateMachineBuilder
{
    public BC6PiscodemonStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<VoidBlizzardIIICircles>()
            .ActivateOnEnter<VoidBlizzardIIIExaflares>()
            .ActivateOnEnter<ArcaneBlast>()
            .ActivateOnEnter<VoidFlareStarNuke>()
            .ActivateOnEnter<VoidThunderIIICross>()
            .ActivateOnEnter<VoidAeroIIIDonut>()
            .Raw.Update = () => ((BeastChessModule)Module).EnemiesAllDead();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed, // 参数为上游值未回放验证（用户未选到该分支路线）
    StatesType = typeof(BC6PiscodemonStates),
    ConfigType = null,
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Uncategorized,
    GroupType = BossModuleInfo.GroupType.CFC,
    GroupID = 1088u,
    NameID = 14535u, // 奇子·刺鱼魔（Kano XBMB03 / 上游 PiscodemonPiece 一致）
    SortOrder = 6,
    PlanLevel = 0)]
public sealed class BC6Piscodemon(WorldState ws, Actor primary) : BeastChessModule(ws, primary)
{
    public override uint[] EnemiesOfInterest => [(uint)OID.Boss]; // 场内无小怪（上游/Kano 均未登记其他敌人）

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actors(this, EnemiesOfInterest);
}
