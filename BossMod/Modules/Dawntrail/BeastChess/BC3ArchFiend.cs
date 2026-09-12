using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.BeastChess.BC3ArchFiend;

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
// 依次开始读条——回放实测各列 CST+ 间隔约 1s，扫击方向逐波交替（东→西 / 西→东）；
// 每列两个 Helper 同时读条：46878（3.7s 先击）+ 46879（4.2s 后击），同列两击间隔 0.5~1s。
// 组件直接挂 Helper 读条，activation=CastFinishAt 天然逐列步进；1.5s 内将生效的段标 danger，
// AI 按时间轴先躲早生效段再躲晚段。
//
// 第 5 列（x≈536）按第 4 列（x≈528）同刻处理（activation 覆写对齐，用户实测）：原样逐列步进时 AI 会在
// 第 4 列区域等待、第 5 列激活后被赶去第 5 列、又因第 4 列将生效而折返，来回拉扯；对齐后危险批次为
// 列1→列2→列3→列4+5（同刻），AI 在列 3 区域等待即可一次覆盖最后两列。仅改显示/AI 判定时序，游戏结算不变。
// 两列读条起始顺序逐波交替，故双向绑定：第 4 列先出现则回填第 5 列，反之第 5 列挂起待第 4 列出现后对齐。
sealed class DismemberColumns(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private const float FifthColumnX = 534f; // 第 5 列 Helper x≈536（≥534 判定）；第 4 列 x≈528
    private static readonly AOEShapeRect Dismember = new(35f, 4f);

    private readonly Dictionary<uint, ulong> _fifthByAction = []; // actionID -> 第 5 列 caster InstanceID（每波每 action 至多一个第 5 列）
    private readonly Dictionary<uint, DateTime> _fourthActivation = []; // actionID -> 第 4 列完成时刻

    protected override int MaxDisplayed => 10; // 每波 5 列 x 2 击

    // 窗口同时控制 danger 红标出现时机与（基类默认的）禁区挂载；本组件禁区已全程挂载（见下），
    // 窗口只留显示语义：生效前 0.5s 标红（原 1.5s 过大，实测会提前 1.5s 把后续列染红误导 AI/玩家）
    protected override double RiskyActivationWindow => 0.5d;

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.DismemberFirst or (uint)AID.DismemberSecond => new(Dismember),
        _ => null,
    };

    // AI 层：全部 pending 从读条起挂禁区（与 RiskyActivationWindow 解耦，用户实测修正）：
    // 列间间隔仅 0.4~1.32s，若按窗口挂载则多列同时禁区、全场无处可去；全程挂载后 activation
    // 时间语义（禁区分值随时间逼近递增）驱动 AI 提前规划、始终落后扫击一列逐列迁移。
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var pending = Pending; // 基类保证按 activation 升序；从 CST+ 起即入列（3.94s 全程可见）
        for (var i = 0; i < pending.Length; ++i)
        {
            ref readonly var entry = ref pending[i];
            hints.AddForbiddenZone(entry.AOE.ShapeDistance ?? entry.AOE.Shape.Distance(entry.AOE.Origin, entry.AOE.Rotation), entry.AOE.Activation);
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        base.OnCastStarted(caster, spell);
        if (ConfigFor(spell.Action.ID) == null)
            return;

        var x = caster.Position.X;
        if (x >= FifthColumnX)
        {
            _fifthByAction[spell.Action.ID] = caster.InstanceID;
            if (_fourthActivation.TryGetValue(spell.Action.ID, out var fourth))
                AlignFifth(spell.Action.ID, fourth);
        }
        else if (x >= FifthColumnX - 8f)
        {
            var activation = Module.CastFinishAt(spell);
            _fourthActivation[spell.Action.ID] = activation;
            AlignFifth(spell.Action.ID, activation);
        }
    }

    private void AlignFifth(uint actionID, DateTime activation)
    {
        if (_fifthByAction.TryGetValue(actionID, out var fifth))
            AdjustPendingActivation(actionID, fifth, activation);
    }

    public override void OnActorDestroyed(Actor actor)
    {
        // 字典 key=actionID / value=casterID，按 casterID 反查移除，防跨波累积
        foreach (var (actionID, casterID) in _fifthByAction)
        {
            if (casterID == actor.InstanceID)
            {
                _fifthByAction.Remove(actionID);
                break;
            }
        }
        base.OnActorDestroyed(actor);
    }
}

sealed class BC3ArchFiendStates : StateMachineBuilder
{
    public BC3ArchFiendStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<ArchFiendAOEs>()
            .ActivateOnEnter<DismemberColumns>();
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
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(this, [(uint)OID.AbyssalLance], Colors.Object); // 站桩地标，用物件色绘制
    }
}
