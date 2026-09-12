using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.BeastChess.BC1Ogre;

// 奇子·食人魔场（A 槽，圆 R20）。
// AID 数据来自 2026-09-12 两份回放（02_58_36 / 11_38_08）交叉验证 + xivapi Action 表（CastType/EffectRange/XAxisModifier/Omen），
// 形状映射遵循 AIHintsBuilder.GuessShape 的仓库权威约定。
// 小怪：妖火 0x4B8E x6 / 大妖火 0x4DD4 x2（被击杀时自爆 Scorched Earth，ER60 近全屏，无法规避故不画）/ 火球 0x4B8F。
public enum OID : uint
{
    Boss = 0x4B8D, // 奇子·食人魔（HP 12074，槽位 A）
    FaerieFire = 0x4B8E, // 妖火 ×6
    GreaterFaerieFire = 0x4DD4, // 大妖火 ×2
    Fireball = 0x4B8F, // 火球
    Helper = 0x233C,
}

public enum AID : uint
{
    ScorchingSmiteVisual = 46913, // 燃烧猛击：boss->self，4.7s cast，引导 visual；实际扇形由同位同时读条的 Helper 46912 承载（两场同刻 CST+ 实证）
    ScorchingSmiteCone = 46912, // 燃烧猛击：Helper->self，5.7s cast，扇形 40 半角60（CastType13/ER40/omen gl_fan120）；Helper 与 boss 同位同朝向（朝场心），天然画在 boss 冲锋后的新位置
    ScorchingSmiteLongCone = 49688, // 燃烧猛击：Helper->self（对面火圈位），9.0s cast，同款扇形 40 半角60；boss 在一侧火圈读 46910 时另一侧 Helper 施放
    ScorchingSmiteCharge = 46910, // 燃烧猛击：boss->self，5.7s cast，冲锋后的引导 visual（无形状数据）；不画
    ScorchingSmiteBurst = 46911, // 燃烧猛击：boss no-cast 自身结算（ER0 无形状）；不画
    ChargeJump = 46914, // (无名)：boss no-cast 冲锋事件，loc=落点（实测两火圈 (104,-420)/(136,-420) 与场心间跳移，0.1-0.7s 完成位移）；位移本身不画
    Allfire = 46915, // 猛火喷发：boss->self，3.7s cast，圆 R40 全场级 AOE（raidwide）
    Magma = 46916, // 熔岩：Helper->location，2.7s cast，圆 R3（CastType2/ER3）；每波 20 圈在 boss 前方扇区齐发（紧随 46915 结算）
    MagmaFireball = 46917, // 熔岩：Helper->location，2.7s cast，圆 R5（CastType2/ER5）；两场各 2 个，落点=东西火圈 (104,-420)/(136,-420)，结算后地面残留持续伤害圈
    BurningWard = 46918, // 火灵的守护：boss->self，2.7s cast，自身增益；不画
    ScorchedEarthFaerie = 46919, // 大火焰：妖火自爆，no-cast，ER60 近全屏（无法规避）；不画
    ScorchedEarthGreater = 49728, // 大火焰：大妖火自爆，no-cast，ER60 近全屏；不画
    FireCall = 46921, // 火球生成：boss->self，3.7s cast，召唤 visual；不画
    ArmOfPurgatory = 46922, // 延烧：火球->Enemy，0.7s cast，圆 R10（CastType2/ER10），火球本体位置爆炸
    AutoAttack = 49682, // 食人魔自动攻击：no-cast（两场 12/11 次，命中 dist 2.8-5.1y rel_ang≈0）
}

// 燃烧猛击扇形：46912（跟随 46913 引导）+ 49688（对面火圈 9s 长读条）
sealed class ScorchingSmiteCones(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCone Cone = new(40f, 60f.Degrees()); // omen fan120 = 120° 全角

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.ScorchingSmiteCone or (uint)AID.ScorchingSmiteLongCone => new(Cone),
        _ => null,
    };
}

// 猛火喷发后的熔岩小圆流：每波 20 圈 R3 在 boss 前方扇区齐发
sealed class MagmaBursts(BossModule module) : ReplayValidatedCastAOEs(module)
{
    protected override int MaxDisplayed => 20;

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.Magma => new(new AOEShapeCircle(3f), LocationTargeted: true),
        _ => null,
    };
}

// 火球爆炸（延烧）
sealed class FireballBlast(BossModule module) : ReplayValidatedCastAOEs(module)
{
    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.ArmOfPurgatory => new(new AOEShapeCircle(10f)),
        _ => null,
    };
}

// 残留火圈：46917 结算后在东西火圈落点 (104,-420)/(136,-420) 留持续伤害圈。
// 持续 25s（用户目测定值）：回放不可实测——全场无玩家/宠物踩圈受击记录（唯一火圈内命中是宠物吃 49682 平砍），
// 落地后亦无周期性 tick 事件；xivapi Action 表无地面残留时长字段。
sealed class MagmaFireballs(BossModule module) : BossComponent(module)
{
    private const float Radius = 5f;
    private const double Duration = 25d;

    private readonly List<(WPos Origin, DateTime ExpiresAt)> _zones = [];

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.MagmaFireball)
            _zones.Add((spell.TargetXZ, WorldState.CurrentTime.AddSeconds(Duration)));
    }

    public override void Update()
    {
        var now = WorldState.CurrentTime;
        _zones.RemoveAll(zone => now > zone.ExpiresAt);
    }

    public override void DrawArenaBackground(int pcSlot, Actor pc)
    {
        foreach (var zone in _zones)
            Arena.ZoneCircle(zone.Origin, Radius, Colors.AOE);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var zone in _zones)
            hints.AddForbiddenZone(new SDCircle(zone.Origin, Radius), zone.ExpiresAt);
    }
}

sealed class AllfireRaidwide(BossModule module) : Components.RaidwideCast(module, (uint)AID.Allfire);

sealed class BC1OgreStates : StateMachineBuilder
{
    public BC1OgreStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<ScorchingSmiteCones>()
            .ActivateOnEnter<MagmaBursts>()
            .ActivateOnEnter<MagmaFireballs>()
            .ActivateOnEnter<FireballBlast>()
            .ActivateOnEnter<AllfireRaidwide>()
            .Raw.Update = () => ((BeastChessModule)Module).EnemiesAllDead();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(BC1OgreStates),
    ConfigType = null,
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Uncategorized,
    GroupType = BossModuleInfo.GroupType.CFC,
    GroupID = 1088u,
    NameID = 14538u, // 奇子·食人魔（回放 ACT+ 提取）
    SortOrder = 1,
    PlanLevel = 0)]
public sealed class BC1Ogre(WorldState ws, Actor primary) : BeastChessModule(ws, primary)
{
    public override uint[] EnemiesOfInterest => [(uint)OID.Boss, (uint)OID.FaerieFire, (uint)OID.GreaterFaerieFire, (uint)OID.Fireball];

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actors(this, EnemiesOfInterest);
}
