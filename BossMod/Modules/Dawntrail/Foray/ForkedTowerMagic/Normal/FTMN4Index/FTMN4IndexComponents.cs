// 目录战组件（形状/时机均来自 2026-08-06 三场回放实测）：
// 组件按机制分组：核爆全屏 / 封印武器（远离+靠近+AI 引导）/ 元素球（只绘制）/ 元素整合 rect /
// 圣枪冲击波 / 二连召唤连招斩击 / 全知烈火分散 / 预言（陨石+天崩地裂）。
// 元素地板/球机制判定复杂（球无读条无伤害事件，仅 tether 连线 ~10s 后消失），暂只绘制不引导；
// ReplayValidatedCastAOEs 用于读条型 AoE（replay 加速去重）。
using System.Runtime.InteropServices;
using BossMod.Dawntrail.Foray.CriticalEngagement;
namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN4Index;

// 核爆（全屏 AoE）：本体 48415 读条 4.7s；连续咏唱（48407）后本体 48416 二段核爆（no cast 事件，双核爆）
sealed class FlareCasts(BossModule module) : Components.RaidwideCasts(module, [(uint)AID.Flare, (uint)AID.Flare2], "核爆：全屏伤害");

// 封印武器·远离：本体 48384 读条 + Helper 48385 爱之歌中心 R15 圈（7.0s），玩家需离开中心 15y 之外
sealed class SealedImplementsAway(BossModule module) : Components.SimpleAOEs(module, (uint)AID.RomeosBallad, 15f);

// 封印武器·靠近：本体 48386 读条 + Helper 48387 盯准 R11 圈（7.1s）@ 场边 R20.5
// （常规 3 个三角位 / 元素阶段后 6 个六方位），圈内危险，玩家需靠近中心。
// AI：圈禁区（基类自动）+ Goal 引导站中心 R9.5 内（回放实测玩家均站中心附近）
sealed class SealedImplementsNear(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Aim, 11f)
{
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (ActiveAOEs(slot, actor).Length > 0)
            hints.GoalZones.Add(AIHints.GoalSingleTarget(Module.Center, 9.5f, 2f));
    }
}

// 元素球：4B64 冰/4B65 火/4B66 雷球（模型 R1.5）在 4 个固定点位（R20.5）+ 南北 2 点位生成，类型/组合随机，
// 同类配对 tether（363/364/365）约 10s 后消失。球无读条无伤害事件，判定半径未知（回放实测玩家全程躲球），
// 暂只绘制 R15 影响圈（risky=false，不参与 AI 禁区），待后续确认伤害半径再补引导
sealed class ElementBalls(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCircle Shape = new(15f);
    private readonly List<AOEInstance> _displayed = [with(8)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _displayed.Clear();
        foreach (var oid in new[] { (uint)OID.SwirlingOrb, (uint)OID.BallOfFire, (uint)OID.BallOfLevin })
        {
            foreach (var b in Module.Enemies(oid))
            {
                if (!b.IsDeadOrDestroyed)
                {
                    _displayed.Add(new(Shape, b.Position, color: Colors.AOE, risky: false, actorID: b.InstanceID,
                        shapeDistance: Shape.Distance(b.Position, default)));
                }
            }
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }
}

// 元素整合：Helper 48905 rect 15x15 ×3 @ 场地边缘三角（(0,-656)/(±24.249,-614)，R28 边缘），
// 与本体 48401 元素整合同步读条 6.0s（回放实测 2 次/轮，CST! 无目标=无人中招）
sealed class ElementaryChemistryRects(BossModule module) : ReplayValidatedCastAOEs(module)
{
    protected override AOEConfig? ConfigFor(uint actionID)
        => actionID == (uint)AID.UnknownWeaponskill2 ? new(new AOEShapeRect(15f, 7.5f)) : null;
}

// 圣枪冲击波：3 圣枪 4B62 固定三角位（(0,-612.5)/(±13.423,-635.75)）同步读条 48405（5.0s），
// 以圣枪位为圆心 R15（回放实测：玩家聚集中心北侧时仅北枪覆盖，南两枪距离 >15 不覆盖）
sealed class HolyLanceShockwaves(BossModule module) : ReplayValidatedCastAOEs(module)
{
    protected override AOEConfig? ConfigFor(uint actionID)
        => actionID == (uint)AID.Shockwave1 ? new(new AOEShapeCircle(15f)) : null;
}

// 二连召唤·封印武器连招斩击：本体 48390 读条同时 Helper 48391 镰鼬之风 ×3（方向 180/-60/60），
// 48390 结束后 Helper 48389 居合斩 ×3（方向 -120/120/0），均为 60° cone R30、6.0s 读条（回放实测）
sealed class SlashCombos(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCone Cone = new(30f, 30f.Degrees());

    protected override AOEConfig? ConfigFor(uint actionID)
        => actionID is (uint)AID.WindSlash or (uint)AID.Iainuki ? new(Cone) : null;
}

// 全知烈火分散：本体 48418 读条 4.7s，结束后 Helper 48420 全知劫火对全体玩家按当前站位 R6 分散判定
// （no cast 事件，分 3 批约 +0.2/+3.2/+6.2s，回放实测 10 目标=全部玩家）。
// 绘制其他玩家位置的 R6 圈；AI 禁区排除自己（与队友保持距离）
sealed class AllKnowingFlamesSpread(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCircle Shape = new(6f);
    private DateTime _resolve = default; // 判定开始（48418 读条结束 +0.2s）
    private DateTime _clear = default; // 全部批次结束后清空
    private readonly List<AOEInstance> _displayed = [with(12)];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.AllKnowingFlames)
        {
            _resolve = Module.CastFinishAt(spell).AddSeconds(0.2f);
            _clear = _resolve.AddSeconds(7.5f); // 3 批约 6.5s 内判定完毕
        }
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        if (WorldState.CurrentTime > _clear)
        {
            _resolve = default;
        }

        _displayed.Clear();
        if (_resolve == default)
        {
            return CollectionsMarshal.AsSpan(_displayed);
        }

        foreach (var p in Raid.WithoutSlot())
        {
            _displayed.Add(new(Shape, p.Position, default, _resolve, actorID: p.InstanceID,
                shapeDistance: Shape.Distance(p.Position, default)));
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var aoes = ActiveAOEs(slot, actor);
        var len = aoes.Length;
        for (var i = 0; i < len; ++i)
        {
            ref readonly var aoe = ref aoes[i];
            if (aoe.Risky && aoe.ActorID != actor.InstanceID)
            {
                hints.AddForbiddenZone(aoe.ShapeDistance ?? aoe.Shape.Distance(aoe.Origin, aoe.Rotation), aoe.Activation);
            }
        }
    }
}

// 预言：本体 48412 读条后生成预言现象 4B63 ×3（初始 120° 分布 R9），瞬移至落点后 0.5s 读条：
// 48413 陨石 R10 ×2 @ 南侧 (±13.4,-635.8)、48414 天崩地裂 R5-15 donut ×1 @ 北侧 (0,-612.5)（回放实测）
sealed class ProphecyMeteors(BossModule module) : ReplayValidatedCastAOEs(module)
{
    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.Starfall => new(new AOEShapeCircle(10f)),
        (uint)AID.Cleansing => new(new AOEShapeDonut(5f, 15f)),
        _ => null
    };
}

// 异形场地周期切换（2026-08-07 用户实测修正：元素控制读条完毕生成 / 元素整合读条完毕回收）：
// 初始 3 平台（南/东北/西北）→ 元素控制（48394）读条结束 → 6 平台；
// 元素整合（48401）读条期间额外 3 平台（东南/西南/北）红色禁入提示 → 读条结束 → 切回 3 平台并清提示。
// 回放验证（0557 场）：48394 读条 06:21:11→15 / 06:24:08→12；48401 读条 06:22:11→15 / 06:25:22→26；
// 爆弹怪两轮（06:22:45 / 06:25:56）在整合结束后约 30s 的机制堆叠阶段生成于初始 3 平台外缘中点，与场地回收一致。
sealed class ArenaShapes(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCustom ExtraShape = new(IndexArena.ExtraShapes); // 元素整合期间禁入的额外 3 正方形
    private readonly List<AOEInstance> _extra = [with(1)];

    public override bool KeepOnPhaseChange => true;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.ElementaryChemistry) // 元素整合读条开始：额外 3 平台禁入
        {
            _extra.Clear();
            _extra.Add(new(ExtraShape, IndexArena.Center, color: Colors.Danger, shapeDistance: ExtraShape.Distance(IndexArena.Center, default)));
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        var id = spell.Action.ID;
        if (id == (uint)AID.OmniElements && Arena.Bounds != IndexArena.FullBounds) // 元素控制读条结束：展开全部 6 平台
        {
            Arena.Bounds = IndexArena.FullBounds;
            Arena.Center = IndexArena.FullBounds.Center;
        }
        else if (id == (uint)AID.ElementaryChemistry) // 元素整合读条结束：回收额外 3 平台，清禁入提示
        {
            _extra.Clear();
            if (Arena.Bounds != IndexArena.InitialBounds)
            {
                Arena.Bounds = IndexArena.InitialBounds;
                Arena.Center = IndexArena.InitialBounds.Center;
            }
        }
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_extra);
}
