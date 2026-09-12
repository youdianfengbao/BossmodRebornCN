using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.BeastChess.BC5Padso;

// 骨架+机制预警：魅惑女妖场（B 槽，矩形 24x38）。boss 读 46927 召唤梦魔法师×1+梦魔骑士×2 助战。
// AID 数据来自 2026-09-12 两份回放（03_01_44 / 12_21_41）交叉验证 + xivapi Action 表（CastType/EffectRange/XAxisModifier/Omen），
// 形状映射遵循 AIHintsBuilder.GuessShape 的仓库权威约定。两份回放玩家未被任何 AOE 命中（全部躲开），
// AOE 形状依据 xivapi+omen；单体技能以 target=Player + CT1/ER0 + 实测命中佐证。
public enum OID : uint
{
    Boss = 0x4B90, // 魅惑女妖·帕德索（HP 19617，槽位 B）
    DreamMage = 0x4B91, // 梦魔法师 ×1（46927 召出）
    DreamKnight = 0x4B92, // 梦魔骑士 ×2（46927 召出）
    Helper = 0x233C,
}

public enum AID : uint
{
    BloodRainDonutVisual = 46923, // 血雨：boss->self，4.7s cast，visual（CT1/ER0；伤害由 46924 承载）
    BloodRainDonut = 46924, // 血雨：Helper->self（与 boss 同位同朝向），5.7s cast，月环 3-40（CT10/ER40，omen gl_sircle_4008ah1）
    BloodRainCircleVisual = 46925, // 血雨：boss->self，5.1s cast，visual（CT1/ER0；伤害由 46926 承载）
    BloodRainCircle = 46926, // 血雨：Helper->self（与 boss 同位同朝向），5.7s cast，实心圆 R8（CT2/ER8，omen general_1bf）
    Summon = 46927, // 召唤：boss->self，3.7s cast，召出梦魔法师×1+梦魔骑士×2（CT1/ER0 无伤害形状，不画）
    Fanaticism = 46928, // 盲信：梦魔法师->boss，5.7s cast，对 boss 的增益（CT1/ER0 无伤害形状，不画）
    VoidFireII = 46929, // 虚空烈炎：梦魔法师->self，3.7s cast，圆 R10（CT2/ER10，omen general_1bf）
    SweetSteel = 46930, // 甜钢：梦魔骑士->self，3.7s cast，扇形 120° R10（CT13/ER10，omen gl_fan120_1bf）
    Unknown46931 = 46931, // (无名)：boss no-cast 事件占位（两回放各 2 次，CT1/ER0）
    VoidAeroIILine = 46932, // 虚空烈风：boss->self，3.7s cast，正面直线矩形 60x8（用户实测有伤害；CT12/ER60/XMod8 → Rect(60,4) 参照 46870 映射，boss 朝向即矩形方向）；46933 七扇为同机制衍生
    VoidAeroII = 46933, // 虚空烈风：Helper->self，2.7s cast，扇形 20° R60（CT13/ER60，omen gl_fan020_0f）；7 个 Helper 同点 25° 等角扇面齐发（150° 扇区）
    BloodSword = 46934, // 嗜血剑：boss->player，5.7s cast，单体（CT1/ER0）
    ColdCaress = 46935, // 寒毒接触：boss->player，4.7s cast，单体（CT1/ER0；实测命中 dist 2.6-5.1y rel_ang 无规律）
    AutoAttack = 50396, // 自动攻击：boss/梦魔骑士 no-cast（两场 32/33 次）
    AeroAutoAttack = 50746, // 疾风：梦魔法师自动攻击，no-cast 单体（两场 2/4 次）
}

// 血雨两种形态：Helper 与 boss 同位，月环 3-40（靠近）或实心圆 R8（远离），每次施放二选一。
// 两场实测节奏相同：开场月环、召唤后圆各一次，间隔约 64-68s，各自独立非序贯，直接挂 Helper 读条即可。
sealed class BloodRain(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeDonut Donut = new(3f, 40f);
    private static readonly AOEShapeCircle Circle = new(8f);

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.BloodRainDonut => new(Donut),
        (uint)AID.BloodRainCircle => new(Circle),
        _ => null,
    };
}

// 虚空烈风：boss 读 46932 正面直线矩形 60x8（用户实测有伤害，CT12 映射 Rect(60,4)，同 46870 古代疾风），
// 结束后 7 个 Helper 在同一点同时读 46933（2.7s 条，扇形 20° R60，25° 等角排开跨 150°，起点方位逐波旋转）。
// 同机制两段挂同一组件，activation=CastFinishAt 天然先矩形后七扇；每波 1+7=8 条，全部同时段结算。
sealed class VoidAeroFan(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeRect VoidAeroLine = new(60f, 4f);
    private static readonly AOEShapeCone Shape = new(60f, 10f.Degrees()); // omen fan020 = 20° 全角

    protected override int MaxDisplayed => 8; // 每波 1 矩形 + 7 扇

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.VoidAeroIILine => new(VoidAeroLine),
        (uint)AID.VoidAeroII => new(Shape),
        _ => null,
    };
}

// 小怪技能（用户特别要求画出）：梦魔法师虚空烈炎圆 R10（自身圆心）、梦魔骑士甜钢 120° 扇 R10（自身朝向）
sealed class DreamAddAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCircle VoidFire = new(10f);
    private static readonly AOEShapeCone SweetSteel = new(10f, 60f.Degrees()); // omen fan120 = 120° 全角

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.VoidFireII => new(VoidFire),
        (uint)AID.SweetSteel => new(SweetSteel),
        _ => null,
    };
}

sealed class BloodSword(BossModule module) : Components.SingleTargetCast(module, (uint)AID.BloodSword);
sealed class ColdCaress(BossModule module) : Components.SingleTargetCast(module, (uint)AID.ColdCaress);

sealed class BC5PadsoStates : StateMachineBuilder
{
    public BC5PadsoStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<BloodRain>()
            .ActivateOnEnter<VoidAeroFan>()
            .ActivateOnEnter<DreamAddAOEs>()
            .ActivateOnEnter<BloodSword>()
            .ActivateOnEnter<ColdCaress>()
            .Raw.Update = () => ((BeastChessModule)Module).EnemiesAllDead();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(BC5PadsoStates),
    ConfigType = null,
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Uncategorized,
    GroupType = BossModuleInfo.GroupType.CFC,
    GroupID = 1088u,
    NameID = 14541u, // 魅惑女妖·帕德索（回放 ACT+ 提取）
    SortOrder = 5,
    PlanLevel = 0)]
public sealed class BC5Padso(WorldState ws, Actor primary) : BeastChessModule(ws, primary)
{
    public override uint[] EnemiesOfInterest => [(uint)OID.Boss, (uint)OID.DreamMage, (uint)OID.DreamKnight];

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actors(this, EnemiesOfInterest);
}
