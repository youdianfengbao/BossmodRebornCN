// 斗兽奇弈·低段第一盘（Board1）
using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.BeastChess.Board1.BC4ScorpionQueen;

// 骨架+机制预警：祸蛛蝎场（A 槽，圆 R20）。场内灵蛛蝎 0x4B8C x9（可被玩家魅惑助战，46909 激光瞄准 Enemy/boss，对玩家无威胁）。
// AID 数据来自 2026-09-12 两份回放（10_28_16 / 11_27_07）交叉验证 + xivapi Action 表（CastType/EffectRange/Omen），
// 形状映射遵循 AIHintsBuilder.GuessShape 的仓库权威约定。
public enum OID : uint
{
    Boss = 0x4B8B, // 奇子·祸蛛蝎（HP 13634，槽位 A）
    SpiritScorpion = 0x4B8C, // 灵蛛蝎 ×9（R15 八方位环出生，可被魅惑）
    Helper = 0x233C,
}

public enum AID : uint
{
    BedrockUpliftVisual = 46901, // 地面隆起：boss->Enemy，3.7s cast，boss 引导 visual；实际伤害由同圆心的 4 个 Helper 46902-46905 承载
    BedrockUplift1 = 46902, // 地面隆起(一段)：Helper->self，4.7s cast，实心圆 R6（CastType2/ER6，omen general_1bf 实心）
    BedrockUplift2 = 46903, // 地面隆起(二段)：Helper->self，6.7s cast，月环 6-12（CastType10/ER12；内径=前段外径）
    BedrockUplift3 = 46904, // 地面隆起(三段)：Helper->self，8.7s cast，月环 12-18（CastType10/ER18）
    BedrockUplift4 = 46905, // 地面隆起(四段)：Helper->self，10.7s cast，月环 18-24（CastType10/ER24）
    DeadlyThrust = 46906, // 致命尾刺：boss->player，4.7s cast，单体（命中 rel_ang -138~+95 无朝向规律，dist 2.1-8.0）
    VenomWebVisual = 46907, // 毒蛛网：boss->self，2.7s cast，visual（伤害由 46908 承载）
    VenomWeb = 46908, // 毒蛛网：Helper->location，5.7s cast，圆 R9（CastType2/ER9）；每波 9 个落点 = boss 圆心 1 + R15 环 8；圆心先炸，环上 8 圈按顺/逆时针交替序贯点击（约 1s/圈，起点方位逐波旋转）
    Silkscreen = 46909, // 蛛网屏：灵蛛蝎->Enemy(boss)，4.7s cast，矩形 80x4（CastType12/ER40/XMod4）；被魅惑的灵蛛蝎对 boss 输出的激光（回放实测恒指 boss，用户要求仍画出）
    ScorpionAutoAttack = 49682, // 灵蛛蝎自动攻击：no-cast（两场 34/15 次）
    BossAutoAttack = 50398, // 祸蛛蝎自动攻击：no-cast（两场 43/36 次，命中 dist 4.2-6.6y rel_ang≈0）
    Unknown46914 = 46914, // ???: 台账占位，两份祸蛛蝎回放均未出现，归属待复核
}

// 地面隆起四段扩张：boss 读 46901 引导结束后，4 个 Helper 在同一圆心（boss 身旁约 6y 机制选点）同时开始读条，
// 时长 4.7/6.7/8.7/10.7s —— 实测结算间隔精确 2s/段。空间无缝：段1 实心 R6，段2-4 月环内径=前段外径、外径=本段 ER。
// 组件直接挂 4 条 Helper 读条，activation=CastFinishAt 天然逐段 +2s；圆心=Helper 位置（四 Helper 同点，锚定一致）。
// AI 层只挂 activation 最早的 1 段（用户实测修正：4 段无缝覆盖 R0-24，若多段同时进 AI 则全场禁区无处可去；
// 段1 结算移除后自然轮到段2 —— 段1 圆 R6 时 AI 站 R6 外，轮到段2 月环 6-12 时圆心已空 AI 可进内圈）。
sealed class BedrockUplift(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCircle Stage1 = new(6f);
    private static readonly AOEShapeDonut Stage2 = new(6f, 12f);
    private static readonly AOEShapeDonut Stage3 = new(12f, 18f);
    private static readonly AOEShapeDonut Stage4 = new(18f, 24f);

    // 显示层与 AI 对齐：四段序贯扩张（R6 圆 + 月环 6-12/12-18/18-24 无缝相接覆盖 R0-24 > 场地 R20），
    // 全画会铺满雷达（用户实测）——只显示当前段（pending 按 activation 升序取第一项），
    // 段 1 结算移除后自动切到段 2；全量预览没有价值反而遮场。
    protected override int MaxDisplayed => 1;

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.BedrockUplift1 => new(Stage1),
        (uint)AID.BedrockUplift2 => new(Stage2),
        (uint)AID.BedrockUplift3 => new(Stage3),
        (uint)AID.BedrockUplift4 => new(Stage4),
        _ => null,
    };

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var pending = Pending; // 基类保证按 activation 升序，仅最近一段进预警
        if (pending.Length != 0)
        {
            ref readonly var entry = ref pending[0];
            hints.AddForbiddenZone(entry.AOE.ShapeDistance ?? entry.AOE.Shape.Distance(entry.AOE.Origin, entry.AOE.Rotation), entry.AOE.Activation);
        }
    }
}

// 毒蛛网九圈序贯：每波 9 个圆 R9（boss 圆心 1 + R15 八方位 8），9 个 Helper 各自读条 5.7s（显示层 9 圈全画，
// activation=CastFinishAt 天然按序推进，临近 1s 生效的圈标 danger）。回放实测生效顺序：圆心先炸，随后环上
// 8 圈按顺/逆时针交替、起点方位逐波旋转依次点击，相邻圈约 1s（回放秒级精度）。
// AI 层：非场心波全部未结算圈挂禁区（用户实测修正：曾跳过最早 2 个导致中心圈无禁区）；
// 场心波只挂首圈 activation+1.2s 内的最早段并加 R11 内圈引导（对齐 Kano XBMB04；用户实测：
// 全挂会导致 AI 在"最后炸的圈"位置干等 7-8s、炸完后被默认 uptime 目标拉回场心静止）。
sealed class VenomWebs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    protected override int MaxDisplayed => 9;

    protected override double RiskyActivationWindow => 1d;

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.VenomWeb => new(new AOEShapeCircle(9f), LocationTargeted: true),
        _ => null,
    };

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var pending = Pending; // 基类保证按 activation 升序
        var centeredWave = pending.Length > 0 && pending[0].AOE.Origin.AlmostEqual(Module.Center, 1f);
        DateTime firstActivation = centeredWave ? pending[0].AOE.Activation : default;
        for (var i = 0; i < pending.Length; ++i)
        {
            ref readonly var entry = ref pending[i];
            // 对齐 Kano XBMB04：场心波只挂最早段（首圈 activation+1.2s 内），远圈不进 AI，避免 AI 干等最后炸的圈
            if (centeredWave && entry.AOE.Activation > firstActivation.AddSeconds(1.2d))
                continue;
            hints.AddForbiddenZone(entry.AOE.ShapeDistance ?? entry.AOE.Shape.Distance(entry.AOE.Origin, entry.AOE.Rotation), entry.AOE.Activation);
        }
        if (centeredWave && pending.Length > 1)
        {
            // 对齐 Kano XBMB04：内圈引导——首圈炸前 1s 把圆心 R11 设为禁区，把 AI 赶到 R11~R15 环带
            hints.AddForbiddenZone(new SDInvertedCircle(pending[0].AOE.Origin, 11f), firstActivation.AddSeconds(-1d));
        }
    }
}

// 蛛网屏：被魅惑的灵蛛蝎对 boss 的矩形激光（回放实测 target=Enemy 指 boss），用户要求仍画出
sealed class SilkscreenLasers(BossModule module) : ReplayValidatedCastAOEs(module)
{
    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.Silkscreen => new(new AOEShapeRect(40f, 2f)), // CT12/ER40/XMod4 同 46876 映射
        _ => null,
    };
}

sealed class DeadlyThrust(BossModule module) : Components.SingleTargetCast(module, (uint)AID.DeadlyThrust);

sealed class BC4ScorpionQueenStates : StateMachineBuilder
{
    public BC4ScorpionQueenStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<BedrockUplift>()
            .ActivateOnEnter<VenomWebs>()
            .ActivateOnEnter<SilkscreenLasers>()
            .ActivateOnEnter<DeadlyThrust>()
            .Raw.Update = () => ((BeastChessModule)Module).EnemiesAllDead();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(BC4ScorpionQueenStates),
    ConfigType = null,
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Uncategorized,
    GroupType = BossModuleInfo.GroupType.CFC,
    GroupID = 1088u,
    NameID = 14536u, // 奇子·祸蛛蝎（回放 ACT+ 提取）
    SortOrder = 4,
    PlanLevel = 0)]
public sealed class BC4ScorpionQueen(WorldState ws, Actor primary) : BeastChessModule(ws, primary)
{
    public override uint[] EnemiesOfInterest => [(uint)OID.Boss, (uint)OID.SpiritScorpion];

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actors(this, EnemiesOfInterest);
}
