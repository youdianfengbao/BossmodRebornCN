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

// 元素地板（字典）：1EC008 火 / 1EC009 冰 / 1EC00A 雷 地板实体（EventObj，位于场地中心），每轮元素控制（48394）
// 布置时重新生成并写入 rotation（回放 08-11：一轮 rot 火0/雷60/冰120、二轮 火120/雷0/冰60——每轮重新布置，动态读取）。
// 台子方向校准（回放 08-11 两轮验证）：BossMod 台子方向 = 地板游戏 rotation + 180°（对侧 +180°，与 spell.Rotation 同换算）
sealed class ElementFloor(BossModule module) : BossComponent(module)
{
    // 0 火 / 1 雷 / 2 冰 → BossMod 台子基方向（BossMod 角，0=南）；实时读取地板实体（每轮布置后自动反映新值）
    public Angle? GetDir(int prop)
    {
        var oid = prop switch
        {
            0 => (uint)OID.Actor1ec008, // 火地板
            1 => (uint)OID.Actor1ec00a, // 雷地板
            2 => (uint)OID.Actor1ec009, // 冰地板
            _ => default
        };
        foreach (var f in Module.Enemies(oid))
        {
            if (!f.IsDeadOrDestroyed)
            {
                return f.Rotation + 180f.Degrees();
            }
        }
        return null;
    }
}

// 元素球 cone（2026-08-11 机制查清后重写，替换原 ElementBalls 的 R15 猜测圈）：
// 4B64 冰 / 4B65 火 / 4B66 雷球在元素创造（48400）读条结束后生成（回放无 ACT+、仅有 COM+/TETH 事件，故用 Update 轮询检测），
// 球顺时针旋转至同属性台子（地板）时，场地中心 Helper 对该台子方向打 Fan60 R30 cone（对侧双扇，中心=场地中心 (0,-628)）。
// 延迟按 ACT 模板（墨汁塔普通.xml 3a/3b 触发器）：三球同现按三波 7.3/9.8/12.3s（间隔 2.5s，与 08-11 回放实测
// 雷 7.87/冰 10.37/火 12.90 的间隔完全吻合；单波场景 ACT 3a 用 9.7s，此处统一 3b 模板首波 7.3s，实测后校准）；
// 球到达台子（同类 Tether 363/364/365 断开）后 +0.63s cone 施放（回放实测恒定），OnUntethered 校准 activation。
// 紧迫度：最先生效的波次 Colors.Danger，其余 Colors.AOE（参考 ArcaneBeacon 紧迫度分级）。
sealed class ElementOrbs(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCone Cone = new(30f, 30f.Degrees());
    private readonly List<AOEInstance> _aoes = [];
    private readonly HashSet<ulong> _known = [];
    private readonly bool[] _added = new bool[3]; // 每属性已添加（对侧双扇只画一次）
    private readonly ulong[] _ballActor = new ulong[3]; // 每属性首球 InstanceID（Tether 校准匹配用）
    private DateTime _spawnTime; // 本轮首次球生成时刻（波次延迟基准）
    private int _wave;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.OmniElements) // 元素控制读条开始：新一轮布置，重置状态
        {
            _aoes.Clear();
            _known.Clear();
            _added[0] = _added[1] = _added[2] = false;
            _wave = 0;
        }
    }

    public override void Update()
    {
        var floor = Module.FindComponent<ElementFloor>();
        foreach (var oid in new[] { (uint)OID.BallOfFire, (uint)OID.BallOfLevin, (uint)OID.SwirlingOrb })
        {
            foreach (var b in Module.Enemies(oid))
            {
                if (b.IsDeadOrDestroyed || !_known.Add(b.InstanceID))
                {
                    continue;
                }

                var prop = b.OID == (uint)OID.BallOfFire ? 0 : b.OID == (uint)OID.BallOfLevin ? 1 : 2;
                if (_added[prop] || floor?.GetDir(prop) is not { } dir)
                {
                    continue;
                }
                _added[prop] = true;
                _ballActor[prop] = b.InstanceID;

                if (_wave == 0)
                {
                    _spawnTime = WorldState.CurrentTime;
                }

                ++_wave;
                var activation = _spawnTime.AddSeconds(7.3f + (_wave - 1) * 2.5f); // ACT 模板 3b（三波 7.3/9.8/12.3；单波 3a 为 9.7s，统一此模板，实测后校准）
                _aoes.Add(new(Cone, Module.Arena.Center, dir, activation, actorID: b.InstanceID));
                _aoes.Add(new(Cone, Module.Arena.Center, dir + 180f.Degrees(), activation, actorID: b.InstanceID));
            }
        }
    }

    // 球到达台子（同类 Tether 断开）→ +0.63s cone 施放（回放实测恒定），校准预判 activation
    public override void OnUntethered(Actor source, in ActorTetherInfo tether)
    {
        var prop = tether.ID switch
        {
            (uint)TetherID.Tether_chn_m0947_t1_p => 1, // 雷 363
            (uint)TetherID.Tether_chn_m0947_i1_p => 2, // 冰 364
            (uint)TetherID.Tether_chn_m0947_f1_p => 0, // 火 365
            _ => -1
        };
        if (prop < 0 || source.InstanceID != _ballActor[prop])
        {
            return;
        }

        var activation = WorldState.FutureTime(0.63d); // 断开 +0.63s
        var len = _aoes.Count;
        for (var i = 0; i < len; ++i)
        {
            if (_aoes[i].ActorID == source.InstanceID)
            {
                _aoes[i] = _aoes[i] with { Activation = activation };
            }
        }
    }

    // 紧迫度：最先生效的波次深黄（Danger+risky），其余浅黄（AOE、risky=false）——参考 ArcaneBeacon
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var soon = DateTime.MaxValue;
        var len = _aoes.Count;
        for (var i = 0; i < len; ++i)
        {
            if (_aoes[i].Activation < soon)
            {
                soon = _aoes[i].Activation;
            }
        }

        for (var i = 0; i < len; ++i)
        {
            var a = _aoes[i];
            var urgent = soon != DateTime.MaxValue && a.Activation <= soon.AddSeconds(0.5f);
            _aoes[i] = urgent ? a with { Color = Colors.Danger, Risky = true } : a with { Color = Colors.AOE, Risky = false };
        }
        return CollectionsMarshal.AsSpan(_aoes);
    }
}

// 扩散环（元素展开）：环实体 1EC00B 火 / 1EC00C 冰 / 1EC00D 雷（EventObj，出现于场地中心 (0,-628)）依次出现，
// 圆环扩大至对应属性平台中心时 boss 对该平台打 Fan60 R30 cone（origin=场地中心，方向=同属性台子=ElementFloor 字典）。
// 环 → cone 延迟 6.7s 恒定（08-11 回放两轮五组 6.65~6.75s 实测；ACT 模板 6.5s 参考）；环间隔 慢 4.0s / 快 2.0s
// （连续咏唱后），施放顺序 = 环出现顺序。紧迫度按 activation 分级（最先生效波次 Danger，其余 AOE）。
sealed class ElementRings(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCone Cone = new(30f, 30f.Degrees());
    private readonly List<AOEInstance> _aoes = [];
    private readonly HashSet<ulong> _known = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.OmniElements) // 元素控制读条开始：新一轮布置，重置状态
        {
            _aoes.Clear();
            _known.Clear();
        }
    }

    public override void OnActorCreated(Actor actor)
    {
        var prop = actor.OID switch
        {
            (uint)OID.Actor1ec00b => 0, // 火环
            (uint)OID.Actor1ec00d => 1, // 雷环
            (uint)OID.Actor1ec00c => 2, // 冰环
            _ => -1
        };
        if (prop < 0 || !_known.Add(actor.InstanceID))
        {
            return;
        }

        var dir = Module.FindComponent<ElementFloor>()?.GetDir(prop);
        if (dir == null)
        {
            return;
        }

        var activation = WorldState.FutureTime(6.7d); // 环出现 → cone 6.7s（回放实测；ACT 模板 6.5s）
        _aoes.Add(new(Cone, Module.Arena.Center, dir.Value, activation, actorID: actor.InstanceID));
        _aoes.Add(new(Cone, Module.Arena.Center, dir.Value + 180f.Degrees(), activation, actorID: actor.InstanceID));
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var time = WorldState.CurrentTime;
        _aoes.RemoveAll(a => a.Activation.AddSeconds(0.5d) < time); // 爆炸后清除

        var soon = DateTime.MaxValue;
        var len = _aoes.Count;
        for (var i = 0; i < len; ++i)
        {
            if (_aoes[i].Activation < soon)
            {
                soon = _aoes[i].Activation;
            }
        }

        for (var i = 0; i < len; ++i)
        {
            var a = _aoes[i];
            var urgent = soon != DateTime.MaxValue && a.Activation <= soon.AddSeconds(0.5f);
            _aoes[i] = urgent ? a with { Color = Colors.Danger, Risky = true } : a with { Color = Colors.AOE, Risky = false };
        }
        return CollectionsMarshal.AsSpan(_aoes);
    }
}

// 飞翔指令击退安全引导（2026-08-11 用户方案）：boss 读条飞翔指令（48403）→ 三分身（4B6F）落固定三角位
// （北 (0,-612.5)/南西 (-13.423,-635.75)/南东 (13.423,-635.75)，R15.5）→ 分身 R15 圆击退 9y（AwayFromOrigin，用户实测）。
// 绿色引导区（仅 AI 视觉 GoalZones，无 AOE 警戒区组件——现状即无击退预警，需求 1 无删除项）：
// 站进引导区的玩家被 9y 击退后仍在战斗场地内（异形场地 + 内圈即死区挖洞，用 Module.Arena.InBounds 判定）；
// 击退后位置 p' = p + 9 × normalize(p − 分身位置)；三分身各一引导区（重叠区自然叠加得分）。权重 0.5（可调）。
sealed class FlyingDecreeGuide(BossModule module) : BossComponent(module)
{
    private const float KnockbackDistance = 9f; // 击退距离（用户实测）
    private const float GuideRadius = 15f; // 分身击退圆半径（圈内才被击退，引导圈与之匹配）
    private const float Weight = 0.5f; // 引导权重（可调）
    private readonly List<WPos> _phantomPos = [];
    private bool _active;
    private DateTime _expire;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.PropulsiveProphecy) // 飞翔指令读条开始：激活引导（读条 2.7s + 分身跳跃 + 击退结算，10s 窗口）
        {
            _active = true;
            _expire = WorldState.FutureTime(10d);
        }
    }

    public override void Update()
    {
        if (_active && WorldState.CurrentTime >= _expire)
        {
            _active = false;
        }

        // 轮询分身实体位置（4B6F 常驻实体，飞翔指令后落固定三角位；与 ElementOrbs 球检测同模式）
        _phantomPos.Clear();
        foreach (var p in Module.Enemies((uint)OID.TranscribedIndex))
        {
            if (!p.IsDeadOrDestroyed)
            {
                _phantomPos.Add(p.Position);
            }
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (!_active || _phantomPos.Count == 0)
        {
            return;
        }

        foreach (var phantom in _phantomPos)
        {
            // 引导区：p 在分身击退圈内（距分身 ≤ R15）且被 9y 击退（AwayFromOrigin）后仍在场地内（含即死区挖洞）→ 给分
            hints.GoalZones.Add(p =>
            {
                var to = p - phantom;
                if (to.LengthSq() > GuideRadius * GuideRadius)
                {
                    return 0f; // 圈外不会被击退
                }
                var dest = p + to.Normalized() * KnockbackDistance;
                return Module.Arena.InBounds(dest) ? Weight : 0f;
            });
        }
    }
}

// 飞翔指令击退箭头（2026-08-11 用户方案）：boss 读条飞翔指令（48403）→ 三分身（4B6F）落固定三角位
// （北 (0,-612.5)/南西 (-13.423,-635.75)/南东 (13.423,-635.75)，R15.5）→ 分身 R15 圆击退 9y（AwayFromOrigin，用户实测）。
// 雷达视图：仅对"距分身 15y 以内"的玩家显示击退箭头（来源=分身、距离 9f、AwayFromOrigin）；R15 圈本身由游戏 omen 显示、不画；
// AI 视觉由 FlyingDecreeGuide 绿色引导负责。多来源：玩家位于多个分身圈内时返回多个击退（基类按顺序依次应用）。
// 箭头绘制由 GenericKnockback 基类 DrawArenaForeground 自动完成（黄线 + 落点）。
sealed class FlyingDecreeKnockbacks(BossModule module) : Components.GenericKnockback(module)
{
    private const float KnockbackDistance = 9f; // 击退距离（用户实测）
    private const float CircleRadius = 15f; // 分身击退圆半径（圈内玩家被击退）
    private readonly List<WPos> _phantomPos = [];
    private readonly List<Knockback> _knockbacks = [with(4)];
    private bool _active;
    private DateTime _expire;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.PropulsiveProphecy) // 飞翔指令读条开始：激活（2.7s 读条 + 分身跳跃 + 击退结算，10s 窗口）
        {
            _active = true;
            _expire = WorldState.FutureTime(10d);
        }
    }

    public override void Update()
    {
        if (_active && WorldState.CurrentTime >= _expire)
        {
            _active = false;
        }

        // 轮询分身实体位置（4B6F 常驻实体，飞翔指令后落固定三角位；与 FlyingDecreeGuide 同模式）
        _phantomPos.Clear();
        foreach (var p in Module.Enemies((uint)OID.TranscribedIndex))
        {
            if (!p.IsDeadOrDestroyed)
            {
                _phantomPos.Add(p.Position);
            }
        }
    }

    // 圈内玩家（距分身 ≤ 15y）显示击退箭头；圈外不显示；多圈内返回多个来源（基类按顺序依次应用）
    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        _knockbacks.Clear();
        if (!_active)
        {
            return CollectionsMarshal.AsSpan(_knockbacks);
        }

        var count = _phantomPos.Count;
        for (var i = 0; i < count; ++i)
        {
            if ((actor.Position - _phantomPos[i]).LengthSq() <= CircleRadius * CircleRadius)
            {
                _knockbacks.Add(new(_phantomPos[i], KnockbackDistance, WorldState.CurrentTime, kind: Kind.AwayFromOrigin));
            }
        }
        return CollectionsMarshal.AsSpan(_knockbacks);
    }
}

// 元素机制 AI 提前等待引导（2026-08-11 用户方案）：元素阶段（球 cone / 环 cone）扇形 AOE 从场地中心向
// 六等分台子方向（0/60/120/180/240/300°）打 Fan60 R30——AI 提前站到两个相邻扇形交界处等待，
// 预警一出只需一步跨到安全侧（符合玩家操作习惯）。
// 交界点 = 六等分中间角方向（30/90/150/210/270/330°）@ Radius 20y（R30 覆盖到 30y，20y 处正站在两扇之间；可调）。
// 激活：元素控制（48394）读条开始 → 窗口 12s（覆盖整轮元素阶段：创造/球 + 展开/环；超时或下一轮自动重置）。
// 权重 1.0f（高于 CenterGoal 的 0.1——元素阶段优先交界点；窗口过期后 AI 回到中心弱引导）。
// 得分 = 到 6 个交界点最近距离 ≤ 2.5f → 1.0f（取 min 避免多目标叠加糊权重）。
sealed class ElementWaitGuide(BossModule module) : BossComponent(module)
{
    private const float Radius = 20f; // 交界点距场地中心距离（可调；R30 cone 覆盖到 30y，20y 处站在两扇之间）
    private const float AcceptRadius = 2.5f; // 交界点判定半径（可调）
    private const float Weight = 1.0f; // 引导权重（高于 CenterGoal 0.1，元素阶段优先交界点）
    private readonly WPos[] _spots = new WPos[6];
    private bool _active;
    private DateTime _expire;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.OmniElements) // 元素控制读条开始：激活整轮元素阶段
        {
            _active = true;
            _expire = WorldState.FutureTime(12d);
            var center = Module.Arena.Center;
            for (var i = 0; i < 6; ++i)
            {
                _spots[i] = center + (30f + 60f * i).Degrees().ToDirection() * Radius; // 六等分中间角（台子方向之间）
            }
        }
    }

    public override void Update()
    {
        if (_active && WorldState.CurrentTime >= _expire)
        {
            _active = false; // 窗口过期：AI 回到中心弱引导
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (!_active)
        {
            return;
        }

        hints.GoalZones.Add(p =>
        {
            var best = float.MaxValue;
            for (var i = 0; i < 6; ++i)
            {
                var d = (p - _spots[i]).LengthSq();
                if (d < best)
                {
                    best = d;
                }
            }
            return best <= AcceptRadius * AcceptRadius ? Weight : 0f;
        });
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
