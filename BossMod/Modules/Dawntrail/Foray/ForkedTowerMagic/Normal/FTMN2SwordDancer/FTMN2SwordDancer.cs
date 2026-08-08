// 魔之塔（The Forked Tower: Magic，国服"两岐塔 魔之塔"）Normal 第 2 战：剑舞者（Sword Dancer）。
// 场地中心 (600, 704)、boss 模型 0x4D76（BNpcName 14820）等实体数据来自 2026-08-06 国服回放实测
// （ZoneID 1346 新月岛北部）。OID/AID/SID 枚举由 The Combat Reborn Team (LTS) 数据导入生成。
namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN2SwordDancer;

[ModuleInfo(BossModuleInfo.Maturity.Dummy, // 发版隐藏（2026-08-09）
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
    public SwordDancer(WorldState ws, Actor primary) : base(ws, primary, new(600f, 704f), new ArenaBoundsCircle(24f))
    {
        // 舞动之剑全部列为敌人（2026-08-07 用户要求：方便查询对应情况）——
        // 预填充 RelevantEnemies：4D77 投剑突进/回旋、4D79 回转、4D7A 跃进步法四剑、4D7C 八剑突进
        Enemies(new uint[] { (uint)OID.DancingSword4, (uint)OID.DancingSword3, (uint)OID.DancingSword2, (uint)OID.DancingSword });
        ActivateComponent<CycloswordsPreview>(); // 风旋剑出鞘提前预警（跨相位常驻）
        ActivateComponent<ThrownSwords>(); // 投剑短/长矩形预警（跨相位常驻）
    }

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

// 投剑（2026-08-07 用户参数：50525 短 11×7 / 50526 长 21.5×7，剑面向为基准；回放落点验证长度）：
// 4D77 剑（实体在中心 (600,704)）与 boss 49559/49560 投剑同帧施放（波1 一对、波2/3 两对）；
// 方向=剑面向（读条 rotation）；矩形从剑位置向前（lengthFront=短/长、半宽 3.5）。
// 回放落点验证（0557 场）：波2/3 50525→落点距中心 11.5、50526→21.5（与用户参数一致）；
// 波1（开战 06:05:14）50525→21.5/50526→11.5 互换反常，按用户参数与波2/3 为准。
// AI 预警由 GenericAOEs 基类自动处理（risky）。
sealed class ThrownSwords(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeRect ShortShape = new(11f, 3.5f); // 50525 短（半宽 3.5、长约 11）
    private static readonly AOEShapeRect LongShape = new(21.5f, 3.5f); // 50526 长（半宽 3.5、长约 21.5）
    private static readonly AOEShapeRect AiShape = new(24f, 3.5f); // AI 统一长矩形（覆盖到墙）
    private readonly List<AOEInstance> _aoes = [with(8)];

    public override bool KeepOnPhaseChange => true; // 每轮投剑均触发，跨相位常驻（模块构造激活）

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_aoes);

    // AI 视觉统一长矩形（2026-08-07 用户要求：50525/50526 存在反常互换，AI 两种 id 都按 24 长覆盖到场边驱赶 AI，
    // 更符合人为控制走位；雷达保持短/长区分）
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var a in _aoes)
        {
            hints.AddForbiddenZone(AiShape, a.Origin, a.Rotation, a.Activation); // 剑面向 24 长半宽 3.5，AI 避开整个路径被驱赶到两侧
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        var shape = spell.Action.ID switch
        {
            (uint)AID.Rush => ShortShape, // 50525 短投剑
            (uint)AID.Rush1 => LongShape, // 50526 长投剑
            _ => null
        };
        if (shape != null)
        {
            // 突进方向改用落点（2026-08-07 修复：剑 rotation 字段恒定 -180，方向编码在落点 dest——
            // 回放 CST+ 落点为 8 个基点 ±11.5/±21.5；Angle.FromDirection 为几何方向，无游戏 rotation 的 180° 换算问题；
            // 雷达（短/长）与 AI（统一 24）均使用该方向——AddAIHints 取 a.Rotation）
            var dir = Angle.FromDirection(spell.LocXZ - caster.Position);
            _aoes.Add(new(shape, caster.Position, dir, Module.CastFinishAt(spell), actorID: caster.InstanceID, shapeDistance: shape.Distance(caster.Position, dir)));
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.Rush or (uint)AID.Rush1)
        {
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
        }
    }
}

// 秘法剑：boss 位移到 4 边中点（±11.5/21.5y）后 Helper 49585 在落点放 96x96 矩形（Rect 48x48，读条 5.5s）。
// 回放实测（筱筱/Ucey 受击位置）确认矩形从落点沿 cast 方向延伸、半宽 48；玩家站矩形覆盖的半场对面即可
// （XML 提示"去左手侧/右手侧"），AI 禁入区自动引导避让。
sealed class MartialMystique(BossModule module) : Components.SimpleAOEs(module, (uint)AID.MartialMystique2, new AOEShapeRect(48f, 48f));

// 回转-月环：4D79 剑在自身位置放 donut（贴剑内 5y 内安全，读条 1.0s）。回放实测：剑在中心时全员站中心
// 无受击；剑在东 11.5y 时 20.5y 外玩家受击。
// 月环内缘=钢铁外缘（2026-08-07 用户实测修正）：钢铁 R15（SpinOut）→ 月环 donut 内缘 15（原 5-60 内缘偏小）
sealed class SpinRing(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Spin, new AOEShapeDonut(15f, 60f));

// 回转-钢铁：4D79 剑在自身位置放 R15 圆（远离 15y，读条 1.0s）。回放实测：剑在中心时 4.3y 处玩家受击
// 确认 R15 覆盖；剑在西 11.5y 时 10.6y 处玩家受击同样在圆内。
sealed class SpinOut(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Spin1, 15f);

// 回转-钢铁（R20）：XML 标注 WeaponId 7/1F 时 R20（C1B9），三场回放未出现，按数据备用
sealed class SpinOutFar(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Spin2, 20f);

// 风旋剑出鞘提前预警（2026-08-07 用户实测）：49586"风旋剑出鞘"读条开始即画剑位（场地中心 4D79 剑，
// 回放 49589/49592 均施法于 (600,704)）R15 钢铁预警圈；49587"风旋剑"结算后由 SpinRing/SpinOut
// 按实际轮次（钢铁/月环随机）接管绘制；预警激活时间=出鞘读完+4.2s（49587 3s + 剑读条 1.0s 近似）
sealed class CycloswordsPreview(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCircle Shape = new(15f);
    private readonly List<AOEInstance> _displayed = [with(4)];

    public override bool KeepOnPhaseChange => true; // 每轮风旋剑出鞘均触发，跨相位常驻（模块构造激活）

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_displayed);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.CycloswordsUnsheathed)
        {
            _displayed.Clear();
            _displayed.Add(new(Shape, new(600f, 704f), default, Module.CastFinishAt(spell).AddSeconds(4.2d)));
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.CycloswordsUnsheathed)
        {
            _displayed.Clear(); // 由 SpinRing/SpinOut 接管
        }
    }
}

// 剑舞分层预警（2026-08-07 用户实测，替换 SimpleAOEs 版）：49609"剑舞"后 Helper 49614 依次劈下 4 道米字直条
// （Rect 60x20=半宽 10；回放 0557：06:06:36.35/38.90/41.38/43.92 劈下、间隔 2.55s），各读条 1.5s 后依次结算。
// 方向动态（2026-08-07 用户要求：顺逆时针未确认固定，不硬编码）——每道矩形的位置/朝向从劈下事件动态读取
// （caster.Position / spell.Rotation），按实际劈下事件顺序记录并分层，不做顺/逆时针、45° 间隔假设。
// 雷达分层：1 道已劈→普通；2 道→最早深黄+1 普通；3 道→1 深黄+2/3 普通；4 道全劈（1 未结算）→
// 1 深黄+2/3 普通、第 4 道暂不显示；1 结算后→2 深黄+3/4 普通（4 恢复）……深黄=最先生效（最早劈下）。
// AI 视觉：前 2 道劈下不显示（a/b 阶段）；第 3 道劈下起显示全部已劈（c/d 阶段 123）；有结算后与雷达同步（e 起）。
sealed class SwordDance(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeRect Shape = new(60f, 10f);
    private readonly List<(AOEInstance aoe, DateTime slash)> _slashes = [with(4)]; // 按劈下顺序（=结算顺序）
    private readonly List<AOEInstance> _displayed = [with(4)]; // 雷达
    private readonly List<AOEInstance> _ai = [with(4)]; // AI 禁区
    private int _totalSlash; // 本轮累计劈下数（含已结算移除）

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_displayed);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.SwordDance1) // 49609 剑舞：新一轮开始
        {
            _slashes.Clear();
            _totalSlash = 0;
        }
        else if (spell.Action.ID == (uint)AID.SwordDance6) // 49614 直条劈下
        {
            // 方向修正（2026-08-07 修复"半个矩形"）：spell.Rotation 为游戏角度（0=北），BossMod 角度 0=南，差 180°——
            // 不加修正矩形会画到剑痕相反侧（视觉错位成"半个"）；回放落点验证：施法者=场地中心 (600,704)、
            // 落点=中心沿方向 30y（总长 60 的矩形中点），AOEShapeRect(60,10) 从中心向前覆盖完整
            _slashes.Add((new(Shape, caster.Position, spell.Rotation + 180f.Degrees(), Module.CastFinishAt(spell), actorID: caster.InstanceID), Module.CastFinishAt(spell)));
            ++_totalSlash;
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.SwordDance6)
        {
            _slashes.RemoveAll(s => s.aoe.ActorID == caster.InstanceID); // 结算移除（对应矩形消失）
        }
    }

    public override void Update()
    {
        _displayed.Clear();
        _ai.Clear();
        var count = _slashes.Count;
        if (count == 0)
        {
            return;
        }

        var resolved = _totalSlash - count; // 已结算数
        var hideLast = _totalSlash >= 4 && resolved == 0 && count >= 4; // d 阶段：第 4 道暂不显示
        for (var i = 0; i < count; ++i)
        {
            if (hideLast && i == count - 1)
            {
                continue;
            }

            var aoe = _slashes[i].aoe;
            var urgent = i == 0; // 最先生效（最早劈下）深黄
            _displayed.Add(urgent ? aoe with { Color = Colors.Danger, Risky = true } : aoe with { Color = default, Risky = false });
        }

        if (resolved > 0 || _totalSlash >= 3) // AI：c 阶段（第 3 道劈下）起显示已劈全部；a/b 不显示；有结算后同雷达
        {
            foreach (var a in _displayed)
            {
                _ai.Add(a with { Risky = true });
            }
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var a in _ai)
        {
            hints.AddForbiddenZone(a.Shape.Distance(a.Origin, a.Rotation), a.Activation);
        }
    }
}

// 戳地：跃进步法后 4 把 4D7A 剑在 4 边中点（±18y）同时放 R5 圆（读条 3.6s，贴剑 5y 外）
sealed class Pierce(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Pierce, 5f);

// 跃进步法击退链+引导区（2026-08-07 用户实测确认：ABCD 顺序动态、30° 引导扇、半径=距离−21 余量 3y）
// 4D7A 剑 49599（+50359 Helper 成对）按跳跃顺序结算击退 24y（来源=对应剑，远离剑方向，顶墙停止）；
// ABCD 动态记录：按 49599 读条顺序（=boss 跳跃到达顺序；点位固定（四边中点 ±18）但顺序不固定，
// 回放 0557：南→东→西→北，不作顺序假设）；
// 击退箭头链（boss1 样式：黄线+最终落点标记）：当前+下一个来源连续（A+B → A 结算后 B+C → C+D → D 单箭头，
// 由未结算列表自然递进）；中间点只画线、最终落点画标记；
// 绿色引导区：以当前来源为圆心、指向下一来源方向、全角 30°（半角 15°）、半径 {距离−21} 扇形
// （GoalZones 引导 AI 站引导区 + ZoneConeOutline 绿色绘制）；最后一段（D）无引导；
// 其他区域：AB 阶段 AOE 预警（普通紧迫度=第一批结算时刻）→ 首个来源结算后禁区（activation=default 立即死区）。
sealed class Swordspear(BossModule module) : Components.GenericKnockback(module, stopAtWall: true)
{
    private readonly List<Knockback> _casters = [with(4)]; // 按 49599 读条顺序（=ABCD），结算时移除
    private int _resolved; // 已结算来源数（紧迫度递进）

    public bool Active => _casters.Count != 0;

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor) => CollectionsMarshal.AsSpan(_casters);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.Steelsbreath1) // 49599 4D7A 剑（按跳跃顺序记录）
        {
            _casters.Add(new(caster.Position, 24f, Module.CastFinishAt(spell), kind: Kind.AwayFromOrigin, actorID: caster.InstanceID));
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.Steelsbreath1)
        {
            var idx = _casters.FindIndex(k => k.ActorID == caster.InstanceID);
            if (idx >= 0)
            {
                _casters.RemoveAt(idx); // 该来源击退已结算
                ++_resolved;
            }
        }
    }

    // 引导扇绿色绘制 + 击退箭头链（boss1 样式：黄线+最终落点标记，中间点只画线、最终落点画标记）
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (_casters.Count >= 2)
        {
            var a = _casters[0].Origin;
            var b = _casters[1].Origin;
            var radius = Math.Max(1f, (b - a).Length() - 21f);
            Arena.ZoneConeOutline(a, default, radius, Angle.FromDirection(b - a), 15f.Degrees(), Colors.Safe); // 绿色引导扇（全角 30°）
        }

        var count = _casters.Count;
        if (count == 0)
        {
            return;
        }

        var drawCount = Math.Min(2, count);
        var from = pc.Position;
        for (var i = 0; i < drawCount; ++i)
        {
            var origin = _casters[i].Origin;
            var away = from - origin;
            if (away == default)
            {
                return;
            }

            var to = from + _casters[i].Distance * away.Normalized();
            if (i == drawCount - 1)
            {
                DrawKnockback(from, to, pc.Rotation, Arena); // 最终落点标记（黄线+投影）
            }
            else
            {
                Arena.AddLine(from, to); // 中间点只画黄线
            }

            from = to;
        }
    }

    // 绿色引导区（GoalZones）+ 其他区域禁区
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_casters.Count < 2)
        {
            return; // 最后一个（D）无引导扇
        }

        var a = _casters[0].Origin;
        var b = _casters[1].Origin;
        var dir = (b - a).Normalized();
        var angle = Angle.FromDirection(dir);
        var radius = Math.Max(1f, (b - a).Length() - 21f); // 余量 3y（24 击退留 3）

        // 引导扇（全角 30°）：GoalZones 引导 AI 站扇内
        var cone = new AOEShapeCone(radius, 15f.Degrees());
        hints.GoalZones.Add(p => cone.Check(p, a, angle) ? 1f : 0f);

            // 其他区域禁区（引导扇外=inverted cone）：AB 阶段普通紧迫度（第一批结算时刻）→ 首个结算后立即死区
        var inverted = new AOEShapeCone(radius, 15f.Degrees(), invertForbiddenZone: true);
        hints.AddForbiddenZone(inverted, a, angle, _resolved > 0 ? default : _casters[0].Activation);
    }
}

// 突进：4D7C 剑 8 把同时放 Rect 30x6（半宽 3，读条 4.0s）。
// 回放实测：横排波次（中心线 x=579~621 间隔 6y）交替朝南/朝北（每半场 4 条宽 6 间隔 6，站空隙）；
// 竖排波次（x=600 线 z=683~725 间隔 6y）全部朝 -90°（西），覆盖西半场，全员去东半场（该波无人受击）。
// 剑位置在中心线、方向沿 cast rotation 延伸 30y——回放受击者（迷途砂/埃拉诺尔/·银杏子·等）逐一验证。
sealed class Rush(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Rush2, new AOEShapeRect(30f, 3f), maxCasts: 8);

// 回旋扇形：Helper（233C 舞动之剑）在场地中心（600,704）放扇形（4D77 回旋剑只做动画无伤害），
// 每轮回旋 2~4 个扇形（读条 3.5s），方向 = cast rotation（回放实测 ±45/±135 等，与剑位置相关）。
// 扇形参数（2026-08-07 回放查证修正）：fan90/fan65/fan57/fan54 为**全角**（AOEShapeCone 的 halfAngle=一半）；
// 映射：49575=fan90 R14、49577=fan90 R24、49578=fan65 R14、49580=fan54 R24、
// 49883=fan65 R14、49889=fan90 R24（同帧剑距 11.5→R14、21.5→R24 推断确认）；
// 49576/49579（fan90 R19/fan57 R19）ACT 表有、回放零出现，仅兜底不触发。
// 回放出现 AID：49575/49577/49578/49580/49883/49889（233C Helper 施放）。
sealed class Turn(BossModule module) : Components.GenericAOEs(module, warningText: "躲避回旋扇形")
{
    private static AOEShape Shape(uint aid) => aid switch
    {
        (uint)AID.Turn1 => new AOEShapeCone(14f, 45f.Degrees()), // 49575 fan90 R14（全角 90 → 半角 45，不变）
        (uint)AID.Turn2 => new AOEShapeCone(24f, 45f.Degrees()), // 49577 fan90 R24（2026-08-07 回放查证修正）
        (uint)AID.Turn5 => new AOEShapeCone(14f, 32.5f.Degrees()), // 49578 fan65 R14（2026-08-07 回放查证修正）
        (uint)AID.Turn7 => new AOEShapeCone(24f, 27f.Degrees()), // 49580 fan54 R24（半角 27，不变）
        (uint)AID.Turn8 => new AOEShapeCone(14f, 32.5f.Degrees()), // 49883 fan65 R14（2026-08-07 回放查证修正）
        (uint)AID.Turnabout => new AOEShapeCone(24f, 45f.Degrees()), // 49889 fan90 R24（2026-08-07 回放查证修正）
        (uint)AID.TurnFan90R19 => new AOEShapeCone(19f, 45f.Degrees()), // 49576 fan90 R19（ACT 表有、回放零出现，兜底）
        (uint)AID.TurnFan57R19 => new AOEShapeCone(19f, 28.5f.Degrees()), // 49579 fan57 R19（ACT 表有、回放零出现，兜底）
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
