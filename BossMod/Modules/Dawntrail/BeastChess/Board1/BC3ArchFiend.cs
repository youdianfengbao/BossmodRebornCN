// 斗兽奇弈·低段第一盘（Board1）
using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.BeastChess.Board1.BC3ArchFiend;

// 骨架+机制预警：上级恶魔场（C 槽，矩形 40x30）。
// AID 数据来自 2026-09-12 两份回放（10_24_58 / 11_07_18）交叉验证 + xivapi Action 表（CastType/EffectRange/XAxisModifier/Omen），
// 形状映射遵循 AIHintsBuilder.GuessShape 的仓库权威约定。
public enum OID : uint
{
    Boss = 0x4B88, // 奇子·上级恶魔（HP 14040，槽位 C）
    AbyssalLance = 0x4B89, // 深渊之枪 ×32，站桩地标（会施放 46876）
    Helper = 0x233C,
}

public enum AID : uint
{
    AbyssalChargeVisual = 46875, // 深渊飞刺：boss->self，2.7s cast，visual（伤害由枪的 46876 承载）
    AbyssalCharge = 46876, // 深渊飞刺：枪->Enemy，2.7s cast，矩形 40x4（CastType12/ER40/XMod4）；枪站场缘朝场心刺，两场各 64 次
    DismemberVisual = 46877, // 肢解：boss->self，2.7s cast，boss 引导 visual；实际伤害由 5 个场缘 Helper 的 46878/46879 承载（见下）
    DismemberFirst = 46878, // 肢解(先击)：Helper->self，3.7s cast；xivapi CastType1 无形状数据，按同位同朝向的 46879 推定矩形
    DismemberSecond = 46879, // 肢解(后击)：Helper->self，4.2s cast，矩形 70x8（CastType12/ER35/XMod8）
    AbyssalTransfixionVisual = 46880, // 深渊贯穿：boss->self，2.7s cast，visual（伤害由 46882/46884 承载）
    AbyssalTransfixionSync = 46881, // (无名)：Helper no-cast 事件，与 46882 成对
    AbyssalTransfixion = 46882, // 深渊贯穿：Helper->self，3.4s cast，圆 R6（CastType2/ER6）；Helper 在固定预置位
    AbyssalTransfixionFollowupSync = 46883, // (无名)：Helper no-cast 事件，与 46884 成对
    AbyssalTransfixionFollowup = 46884, // 深渊贯穿(追击)：Helper->location，3.2s cast，圆 R3（CastType2/ER3）；逐个延迟出现在 boss 周围
    AbyssalSwingVisual = 46885, // 深渊回转：boss no-cast，visual（伤害由 46886 承载）
    AbyssalSwing = 46886, // 深渊回转：Helper->self（与 boss 同位同朝向），5.4s cast，半圆 40（CastType13/ER40，Omen gl_fan180 → 180°全角）
    AutoAttack = 49682, // 上级恶魔自动攻击：no-cast（两场 34/31 次，命中距离 5.6-5.7y）
}

// 常规 AOE：枪刺矩形 / 贯穿圆 / 追击小圆 / 回转半圆
sealed class ArchFiendAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeRect AbyssalCharge = new(40f, 2f);
    private static readonly AOEShapeCircle Transfixion = new(6f);
    private static readonly AOEShapeCircle TransfixionFollowup = new(3f);
    private static readonly AOEShapeCone AbyssalSwing = new(40f, 90f.Degrees()); // omen fan180 = 180° 全角

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.AbyssalCharge => new(AbyssalCharge),
        (uint)AID.AbyssalTransfixion => new(Transfixion),
        (uint)AID.AbyssalTransfixionFollowup => new(TransfixionFollowup),
        (uint)AID.AbyssalSwing => new(AbyssalSwing),
        _ => null,
    };
}

// 肢解五连：boss 读条 46877 结束后，5 个 Helper（x=504..536 等距 8y 站南缘，矩形沿自身朝向贯穿全场）
// 依次开始读条——回放实测各列 CST+ 间隔 0.4~1.3s 递增阶梯，扫击方向逐波交替（东→西 / 西→东）；
// 每列两个 Helper 同时读条同站位：46878（3.7s 先击）+ 46879（4.2s 后击），同列两击间隔 0.47~0.54s。
// 组件直接挂 Helper 读条，activation=CastFinishAt；显示层全程 10 条可见，生效前 0.5s 标红。
//
// AI 策略（用户定稿：一次移动到终点列站桩）：前 4 列挂 G=0 立即死区（activation=default，AI 绝不踏入、
// 也不逐列迁移——列间隔 0.4~1.3s 跑不完 8y 的旧问题直接消失），只有最后生效的列（终点列）挂各自真实
// activation 的正常紧迫度，AI 开场直奔终点列等扫完（与毒蛛网"往最后生效处躲"思路一致）。
// 历史教训：此前"第 5 列对齐第 4 列"方案依赖跨波字典，Helper actor 跨波复用不销毁导致残留时刻
// 污染新波列序（列 1 时机错乱），已整体移除；本方案无需任何跨条目状态。
// 分组按 Origin 聚类而非固定 index 配对：同列两击 Helper 站同一点（Origin 相同），结算推进会改变
// pending 奇偶而 Origin 稳定，免疫漂移。
sealed class DismemberColumns(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private const float SameColumnDistSq = 0.25f; // 同列判定：两击 Helper 站同一点，0.5y 内视为同列
    private static readonly AOEShapeRect Dismember = new(35f, 4f);

    protected override int MaxDisplayed => 10; // 每波 5 列 x 2 击

    // 不覆写 RiskyActivationWindow（基类默认 PositiveInfinity → ActiveAOEs 完全不做 danger 标红，
    // 10 条全部普通预警色）。用户实测修正：0.5s 提前标红会误导玩家提前踏入尚未结算的列；
    // AI 层本组件禁区完全自管（AddAIHints 全程挂载，与 Risky 标志无关），紧迫度仅由
    // 终点列禁区的真实 activation 时间语义驱动。

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.DismemberFirst or (uint)AID.DismemberSecond => new(Dismember),
        _ => null,
    };

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var pending = Pending; // 基类保证按 activation 升序；从 CST+ 起即入列（3.94s 全程可见）
        var count = pending.Length;
        if (count == 0)
            return;

        // 从尾向前找终点列（最后生效组）的起始下标：与队尾条目同位置（Origin 聚类）的连续段
        var groupStart = count - 1;
        while (groupStart > 0 && (pending[groupStart - 1].AOE.Origin - pending[count - 1].AOE.Origin).LengthSq() < SameColumnDistSq)
            --groupStart;

        for (var i = 0; i < count; ++i)
        {
            ref readonly var entry = ref pending[i];
            var shape = entry.AOE.ShapeDistance ?? entry.AOE.Shape.Distance(entry.AOE.Origin, entry.AOE.Rotation);
            // 终点列用真实 activation（正常紧迫度），前面各列 G=0 立即死区
            hints.AddForbiddenZone(shape, i < groupStart ? default : entry.AOE.Activation);
        }
    }
}

sealed class BC3ArchFiendStates : StateMachineBuilder
{
    public BC3ArchFiendStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<ArchFiendAOEs>()
            .ActivateOnEnter<DismemberColumns>()
            .Raw.Update = () => ((BeastChessModule)Module).EnemiesAllDead();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(BC3ArchFiendStates),
    ConfigType = null,
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Uncategorized,
    GroupType = BossModuleInfo.GroupType.CFC,
    GroupID = 1088u,
    NameID = 14533u, // 奇子·上级恶魔（回放 ACT+ 提取）
    SortOrder = 3,
    PlanLevel = 0)]
public sealed class BC3ArchFiend(WorldState ws, Actor primary) : BeastChessModule(ws, primary)
{
    // 单 boss 场（深渊之枪 0x4B89 是不可击杀地标，不算敌人）：primary 死=全灭，行为不变
    public override uint[] EnemiesOfInterest => [(uint)OID.Boss];

    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(this, [(uint)OID.AbyssalLance], Colors.Object); // 站桩地标，用物件色绘制
    }
}
