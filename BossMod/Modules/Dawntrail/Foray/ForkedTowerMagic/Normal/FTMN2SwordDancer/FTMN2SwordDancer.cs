// 魔之塔（The Forked Tower: Magic，国服"两岐塔 魔之塔"）Normal 第 2 战：剑舞者（Sword Dancer）。
// 场地中心 (600, 704)、boss 模型 0x4D76（BNpcName 14820）等实体数据来自 2026-08-06 国服回放实测
// （ZoneID 1346 新月岛北部）。OID/AID/SID 枚举由 The Combat Reborn Team (LTS) 数据导入生成。
namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN2SwordDancer;

[ModuleInfo(BossModuleInfo.Maturity.Dummy, // boss2 恢复开发隐藏（2026-08-07：boss1 实测通过，boss2 待实测）
    StatesType = typeof(SwordDancerStates),
    ConfigType = null, // 如需要可替换为 typeof(SwordDancerConfig)
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    StatusIDType = typeof(SID),
    TetherIDType = typeof(TetherID),
    IconIDType = null, // 如需要可替换为 typeof(IconID)
    PrimaryActorOID = (uint)OID.SwordDancer,
    Contributors = "The Combat Reborn Team (LTS)",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CFC,
    GroupID = 1093u,
    NameID = 14820u,
    SortOrder = 2,
    PlanLevel = 0)]
[SkipLocalsInit]
// 场地圆形 R24：2026-08-06 回放实测，原 Circle(25f) 外扩 1y，按实测修正。
// boss 可目标化（回放 ATG+），CheckPull 默认即可。
// 死亡兜底（2026-08-07 深查修复）：boss 死亡（DIE+）时强制结束状态机（StateMachine.Reset），
// 保证模块被 BMM 卸载（BMM 仅当 ActiveState==null 时卸载）——覆盖状态机卡在中间相位、
// boss 提前死亡等场景，避免雷达被 boss2 持续占用挡掉后续 boss3/4。
public sealed class SwordDancer : BossModule
{
    public SwordDancer(WorldState ws, Actor primary) : base(ws, primary, new(600f, 704f), new ArenaBoundsCircle(24f)) { }

    protected override void UpdateModule()
    {
        if (PrimaryActor.IsDeadOrDestroyed && StateMachine.ActiveState != null)
        {
            StateMachine.Reset();
        }
    }
}

// ==================== 组件（形状/时机均来自 2026-08-06 三场回放实测核对） ====================

// 剑技风暴：全屏 AoE（开战/循环收尾，回放确认全屏无落点，读条 5.0s）
sealed class SwordStorm(BossModule module) : Components.RaidwideCast(module, (uint)AID.SwordStorm1, "剑技风暴：全屏伤害");

// 秘法剑：boss 位移到 4 边中点（±11.5/21.5y）后 Helper 49585 在落点放 96x96 矩形（Rect 48x48，读条 5.5s）。
// 回放实测（筱筱/Ucey 受击位置）确认矩形从落点沿 cast 方向延伸、半宽 48；玩家站矩形覆盖的半场对面即可
// （XML 提示"去左手侧/右手侧"），AI 禁入区自动引导避让。
sealed class MartialMystique(BossModule module) : Components.SimpleAOEs(module, (uint)AID.MartialMystique2, new AOEShapeRect(48f, 48f));

// 回转-月环：4D79 剑在自身位置放 donut 5-60（贴剑 5y 内安全，读条 1.0s）。回放实测：剑在中心时全员站中心
// 无受击；剑在东 11.5y 时 20.5y 外玩家受击，确认 donut(5, 60)。
sealed class SpinRing(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Spin, new AOEShapeDonut(5f, 60f));

// 回转-钢铁：4D79 剑在自身位置放 R15 圆（远离 15y，读条 1.0s）。回放实测：剑在中心时 4.3y 处玩家受击
// 确认 R15 覆盖；剑在西 11.5y 时 10.6y 处玩家受击同样在圆内。
sealed class SpinOut(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Spin1, 15f);

// 回转-钢铁（R20）：XML 标注 WeaponId 7/1F 时 R20（C1B9），三场回放未出现，按数据备用
sealed class SpinOutFar(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Spin2, 20f);

// 剑舞：boss 49609 读条后 Helper 49610 提示 x2，随后 Helper 49614 依次放 4 条直条 Rect 60x20
// （读条 1.5s、间隔 2.5s，落点分别在中心北/东北/东/东南 21.2~30y 处，方向均指向场地中心——回放
// 以落点+方向几何验证）。riskyWithSecondsLeft=2.5 让 AI 只避临近生效的直条（逐条让位）。
sealed class SwordDance(BossModule module) : Components.SimpleAOEs(module, (uint)AID.SwordDance6, new AOEShapeRect(60f, 10f), maxCasts: 8, riskyWithSecondsLeft: 2.5f);

// 戳地：跃进步法后 4 把 4D7A 剑在 4 边中点（±18y）同时放 R5 圆（读条 3.6s，贴剑 5y 外）
sealed class Pierce(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Pierce, 5f);

// 剑气冲击：剑技爆发/跃进步法后 4 把 4D7A 剑依次放（49599 剑 + 50359 Helper 成对，间隔 ~2.5s，读条 2.0s）。
// 回放确认全屏击退（CST! 含全队 14 人 target，效果 00E1），方向从剑位置向外（玩家站剑与中心之间被推回中心）；
// 距离回放位移约 10y+，取 11（与 FTMN1 风暴吐息一致），顶墙停止。
sealed class Swordspear(BossModule module) : Components.SimpleKnockbackGroups(module, [(uint)AID.Steelsbreath1, (uint)AID.Steelsbreath], 11f, stopAtWall: true);

// 突进：4D7C 剑 8 把同时放 Rect 30x6（半宽 3，读条 4.0s）。
// 回放实测：横排波次（中心线 x=579~621 间隔 6y）交替朝南/朝北（每半场 4 条宽 6 间隔 6，站空隙）；
// 竖排波次（x=600 线 z=683~725 间隔 6y）全部朝 -90°（西），覆盖西半场，全员去东半场（该波无人受击）。
// 剑位置在中心线、方向沿 cast rotation 延伸 30y——回放受击者（迷途砂/埃拉诺尔/·银杏子·等）逐一验证。
sealed class Rush(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Rush2, new AOEShapeRect(30f, 3f), maxCasts: 8);

// 回旋扇形：Helper 在场地中心（600,704）放扇形（4D77 回旋剑只做动画无伤害，50525/50526 突进为剑移动也无伤害）。
// 每轮回旋 2~4 个扇形（读条 3.5s），方向 = cast rotation（回放实测 ±45/±135 等，与剑位置相关）。
// 形状按 XML 数据映射（Fan 90/65/57/54°、半径 14/19/24）；C2DB/C2E1 仅出现在秘法剑回合，
// 形状与同组变体一致（C2DB=T1 形、C2E1=T2 形），待回放进一步核对。
sealed class Turn(BossModule module) : Components.GenericAOEs(module, warningText: "躲避回旋扇形")
{
    private static AOEShape Shape(uint aid) => aid switch
    {
        (uint)AID.Turn1 => new AOEShapeCone(14f, 90f.Degrees()), // C1A7 Fan 90° R14
        (uint)AID.Turn2 => new AOEShapeCone(19f, 65f.Degrees()), // C1A9 Fan 65° R19
        (uint)AID.Turn5 => new AOEShapeCone(24f, 57f.Degrees()), // C1AA Fan 57° R24
        (uint)AID.Turn7 => new AOEShapeCone(24f, 54f.Degrees()), // C1AC Fan 54° R24
        (uint)AID.Turn8 => new AOEShapeCone(14f, 90f.Degrees()), // C2DB 与 C1A7 同形
        (uint)AID.Turnabout => new AOEShapeCone(19f, 65f.Degrees()), // C2E1 与 C1A9 同形
        _ => null!,
    };

    private readonly List<AOEInstance> _aoes = [];
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_aoes);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        var shape = Shape(spell.Action.ID);
        if (shape == null)
        {
            return;
        }

        _aoes.Add(new(shape, spell.LocXZ, spell.Rotation, Module.CastFinishAt(spell), actorID: caster.InstanceID, shapeDistance: shape.Distance(spell.LocXZ, spell.Rotation)));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (Shape(spell.Action.ID) != null)
        {
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (Shape(spell.Action.ID) != null)
        {
            ++NumCasts;
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
        }
    }
}
