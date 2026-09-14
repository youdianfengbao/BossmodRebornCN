// 斗兽奇弈·低段第一盘（Board1）分支场：螳螂核（用户未选到该路线，参数为上游值未回放验证）
using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.BeastChess.Board1.BC7MantiCore;

// 螳螂核场（A 槽，圆 R20，中心 (120,-420)——与食人魔/祸蛛蝎同槽，按 primary OID 区分激活）。
// 单 boss 场（上游/Kano 均只登记 0x4C53 一个敌人）。机制池：左右手发光锥、头尾 180° 双向锥、锤跃 R30、
// 死刑攫握、链式冲锋（4 段宽 8 矩形接力 + 终点左右手 90° 锥）。
// OID/AID 与形状来自上游 CombatReborn MantiCorePiece.cs（Equilius，SecondBoard 目录但 NameID 14545 与本场同怪），
// 并经 Kano 7.5.6.1 实测 DLL 交叉校验：Kano dict_shapes 48137=圆 R30；手部锥 Cone(30, 半角 90°)、
// 头尾锥 Cone(40, 半角 90°)、冲锋路径矩形半宽 4，与上游全部一致，无参数分歧。
public enum OID : uint
{
    Boss = 0x4C53, // 奇子·螳螂核（NameID 14545，槽位 A）
    Helper = 0x233C,
}

public enum AID : uint
{
    AutoAttack = 49680, // 自动攻击：boss->player，no-cast，单体
    Teleport = 48126, // 瞬移：boss->location，no-cast，位移（无 AOE）
    ArmAndHammerRightGlow = 48122, // 右手发光：boss->self，5.0+0.6s cast，visual（右手侧锥预警，实际 AOE 由 48123 承载）
    ArmAndHammerRight = 48123, // 右手锤击：Helper->self，5.6s cast，锥 R30 半角 90°（右侧）
    ArmAndHammerLeftGlow = 48124, // 左手发光：boss->self，5.0+0.6s cast，visual（左侧锥预警，实际 AOE 由 48125 承载）
    ArmAndHammerLeft = 48125, // 左手锤击：Helper->self，5.6s cast，锥 R30 半角 90°（左侧）
    ChargeAndHammer = 48127, // 冲锋锤击：boss->self，10.0s cast，机制总读条（链式冲锋 + 终点双手锥）
    WildChargeVisual = 48128, // 冲锋路径：Helper->location，1.5s cast，宽 8 矩形（4 段接力，每段显示比实际爆炸段远一格）
    WildChargeTeleport = 48129, // 冲锋位移：boss->location，no-cast，位移（无 AOE）
    WildCharge = 48130, // 冲锋：Helper->location，1.1s cast，宽 8 矩形（各段实际伤害）
    ArmAndHammerVisual = 48133, // 冲锋终点锤击：boss->self，no-cast，visual（无 AOE）
    ArmAndHammer1 = 48134, // 冲锋终点锤击(甲)：Helper->self，0.6s cast，锥 R30 半角 90°
    ArmAndHammerVisual1 = 48131, // 冲锋终点锤击：boss->self，no-cast，visual（无 AOE）
    ArmAndHammer2 = 48132, // 冲锋终点锤击(乙)：Helper->self，0.6s cast，锥 R30 半角 90°
    DeadlyHold = 48138, // 致死攫握：boss->player，5.0s cast，单体死刑（图标 218）
    HammerleapTeleport = 48135, // 锤跃位移：boss->location，7.0+1.1s cast，位移（无 AOE）
    Hammerleap = 48137, // 锤跃：Helper->self，8.1s cast，圆 R30
    HeadsAndTailsFrontVisual = 48139, // 头尾(前)：boss->self，3.5+0.4s cast，visual（实际 AOE 由 48140 承载）
    HeadsAndTailsFront = 48140, // 头尾(前)：Helper->self，3.9s cast，锥 R40 半角 90°（前向 180°）
    HeadsAndTailsBackVisual = 48141, // 头尾(后)：boss->self，2.0+0.4s cast，visual（实际 AOE 由 48142 承载）
    HeadsAndTailsBack = 48142, // 头尾(后)：Helper->self，2.4s cast，锥 R40 半角 90°（后向 180°）
    TailsAndHeadsBackVisual = 50410, // 尾头(后)：boss->self，3.5+0.4s cast，visual（实际 AOE 由 50411 承载）
    TailsAndHeadsBack = 50411, // 尾头(后)：Helper->self，3.9s cast，锥 R40 半角 90°（后向 180°）
    TailsAndHeadsFrontVisual = 50412, // 尾头(前)：boss->self，2.0+0.4s cast，visual（实际 AOE 由 50413 承载）
    TailsAndHeadsFront = 50413, // 尾头(前)：Helper->self，2.4s cast，锥 R40 半角 90°（前向 180°）
}

public enum SID : uint
{
    LeftHandGlow = 2193, // 左手发光（决定冲锋终点左侧锥）
    RightHandGlow = 2056, // 右手发光（决定冲锋终点右侧锥）
}

public enum IconID : uint
{
    TankBuster = 218, // 致死攫握点图标（player->self）
}

// 左右手锤击：发光 visual 后同侧锥（Kano 同 Cone(30, 半角 90°)）
sealed class ArmAndHammerCones(BossModule module) : ReplayValidatedCastAOEs(module)
{
    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.ArmAndHammerLeft or (uint)AID.ArmAndHammerRight => new(ArmCone),
        _ => null,
    };

    private static readonly AOEShapeCone ArmCone = new(30f, 90f.Degrees());
}

sealed class DeadlyHold(BossModule module) : Components.SingleTargetCast(module, (uint)AID.DeadlyHold);

sealed class Hammerleap(BossModule module) : ReplayValidatedCastAOEs(module)
{
    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.Hammerleap => new(new AOEShapeCircle(30f)),
        _ => null,
    };
}

// 头尾/尾头：前后 180° 双向锥（读条只暴露第一击，前后两击各由独立 AID 承载；Kano 同 Cone(40, 半角 90°)）
sealed class HeadsAndTailsCones(BossModule module) : ReplayValidatedCastAOEs(module)
{
    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.HeadsAndTailsFront or (uint)AID.HeadsAndTailsBack
            or (uint)AID.TailsAndHeadsBack or (uint)AID.TailsAndHeadsFront => new(HeadsTailsCone),
        _ => null,
    };

    private static readonly AOEShapeCone HeadsTailsCone = new(40f, 90f.Degrees());
}

// 链式冲锋：总读条 48127 后，4 个 Helper 依次读 48128（visual）标出 4 段路径（每段宽 8 矩形，从 boss 位置
// 或前段终点续接到本段落点）；路径完整后按 boss 身上左右手发光状态（SID 2193/2056）在终点两侧追加 90° 锥。
// 无精确 cast 时刻可挂（上游同样以事件驱动、activation 空缺持续显示），最近 2 段高亮，按 cast event 消费移除。
// 上游自注：显示比实际爆炸段远一格（帮玩家提前离开即将爆炸的段）。
sealed class WildChargeChains(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<AOEInstance> _aoes = [];
    private readonly List<ActorStatus> _armGlows = [];
    private WPos _lastTarget;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.WildChargeVisual)
            return;

        // 首段从 boss 当前位置出发，后续段从前一段终点续接
        var origin = _aoes.Count == 0 ? Module.PrimaryActor.Position : _lastTarget;
        var target = spell.LocXZ;
        var direction = target - origin;
        _aoes.Add(new(new AOEShapeRect(direction.Length(), 4f), origin, Angle.FromDirection(direction)));
        _lastTarget = target;

        // 4 段路径集齐后，按发光状态在终点两侧生成双手锥
        if (_aoes.Count == 4)
        {
            foreach (var glow in _armGlows)
            {
                var side = glow.ID == (uint)SID.LeftHandGlow ? 90f.Degrees() : -90f.Degrees();
                _aoes.Add(new(new AOEShapeCone(30f, 90f.Degrees()), _lastTarget, Angle.FromDirection(direction) + side));
            }
        }
    }

    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        if (status.ID is (uint)SID.LeftHandGlow or (uint)SID.RightHandGlow)
            _armGlows.Add(status);
    }

    public override void OnStatusLose(Actor actor, ref ActorStatus status)
    {
        if (status.ID is (uint)SID.LeftHandGlow or (uint)SID.RightHandGlow && _armGlows.Count > 0)
            _armGlows.RemoveAt(0);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        // 冲锋各段与终点双手锥按 cast 事件顺序消费
        if (spell.Action.ID is (uint)AID.WildCharge or (uint)AID.ArmAndHammer1 or (uint)AID.ArmAndHammer2 && _aoes.Count > 0)
            _aoes.RemoveAt(0);
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var count = _aoes.Count;
        if (count == 0)
            return [];

        // 最多预览 2 段：最近一段 danger 高亮并作为 AI 禁区，次段普通色
        var span = CollectionsMarshal.AsSpan(_aoes);
        var max = Math.Min(count, 2);
        for (var i = 0; i < max; ++i)
        {
            span[i].Color = i == 0 ? Colors.Danger : Colors.AOE;
            span[i].Risky = i == 0;
        }
        return span[..max];
    }
}

sealed class BC7MantiCoreStates : StateMachineBuilder
{
    public BC7MantiCoreStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<ArmAndHammerCones>()
            .ActivateOnEnter<DeadlyHold>()
            .ActivateOnEnter<Hammerleap>()
            .ActivateOnEnter<HeadsAndTailsCones>()
            .ActivateOnEnter<WildChargeChains>()
            .Raw.Update = () => ((BeastChessModule)Module).EnemiesAllDead();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed, // 参数为上游值未回放验证（用户未选到该分支路线）
    StatesType = typeof(BC7MantiCoreStates),
    ConfigType = null,
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    StatusIDType = typeof(SID),
    PrimaryActorOID = (uint)OID.Boss,
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Uncategorized,
    GroupType = BossModuleInfo.GroupType.CFC,
    GroupID = 1088u,
    NameID = 14545u, // 奇子·螳螂核（Kano XBMB07 / 上游 MantiCorePiece 一致）
    SortOrder = 7,
    PlanLevel = 0)]
public sealed class BC7MantiCore(WorldState ws, Actor primary) : BeastChessModule(ws, primary)
{
    public override uint[] EnemiesOfInterest => [(uint)OID.Boss]; // 单 boss 场（上游/Kano 均未登记其他敌人）

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actors(this, EnemiesOfInterest);
}
