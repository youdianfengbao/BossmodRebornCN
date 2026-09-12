// 斗兽奇弈·低段第一盘（Board1）
using BossMod.Components;
using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.BeastChess.Board1.BC2KnightBishop;

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
    ForwardGuard = 46864, // 前线护卫：骑士->self，4.7s cast；结算后获得方向招架 buff（SID 680 extra=0x1 正面），正面攻击被招架，需绕背输出（见 ForwardGuard 组件）
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

// 前线护卫：骑士结算 46864 后获得方向招架 buff（SID 680，extra=0x1 正面），正面攻击被招架，需绕背输出。
// 回放实测（10_21_47 / 10_59_25）：46864 读条 4.7s 结算后立刻 gain（extra=0001=Front，剩余 998.983s≈战斗
// 剩余全程，两场均无 loss 行——持续到战斗结束）；读条期间 PredictParrySide 画"即将招架"暗色弧线。
// 组件自带：激活时正面 SDCone 禁区 + 目标优先级惩罚（AI 转打主教）+ 文字提示；追加背后 GoalZones
// （骑士背后 3y，权重 10 中等——引导绕背但不压过躲 AOE）。
sealed class ForwardGuard(BossModule module) : DirectionalParry(module, [(uint)OID.Boss])
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.ForwardGuard)
        {
            PredictParrySide(caster.InstanceID, Side.Front);
        }
    }

    public override void OnStatusLose(Actor actor, ref ActorStatus status)
    {
        if (status.ID == ParrySID)
        {
            UpdateState(actor.InstanceID, 0);
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (!Active)
            return;

        foreach (var (id, state) in ActorStates)
        {
            var target = WorldState.Actors.Find(id);
            if (target == null || target.IsDeadOrDestroyed || (state & (int)Side.Front) == 0)
                continue;
            var behind = target.Position - target.Rotation.ToDirection() * 3f;
            hints.GoalZones.Add(AIHints.GoalSingleTarget(behind, 2.5f, 10f));
        }
    }
}

sealed class Skullsplinter(BossModule module) : Components.SingleTargetCast(module, (uint)AID.Skullsplinter);

// 双 boss 场：骑士 primary 先死时主教仍在战斗——States 的 Raw.Update 覆写默认"primary 死亡即结束"，
// 改为全部敌人（骑士+主教）死亡才结束模块（基类 EnemiesAllDead，见 BeastChessModule 注释）
sealed class BC2KnightBishopStates : StateMachineBuilder
{
    public BC2KnightBishopStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<KnightAOEs>()
            .ActivateOnEnter<BlackEruptionChains>()
            .ActivateOnEnter<ForwardGuard>()
            .ActivateOnEnter<Skullsplinter>()
            .Raw.Update = () => ((BeastChessModule)Module).EnemiesAllDead();
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
    public override uint[] EnemiesOfInterest => [(uint)OID.Boss, (uint)OID.Bishop];

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actors(this, EnemiesOfInterest);
}
