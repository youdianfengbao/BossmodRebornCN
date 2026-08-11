// 魔之塔（The Forked Tower: Magic，国服"两岐塔 魔之塔"）Normal 第 1 战：双头怪鸟（Two-Headed Aevis）。
// 场地中心 (-900, 700)、boss 模型 0x4C11（BNpcName 14489）等实体数据来自 2026-08-06 国服回放实测
// （ZoneID 1346 新月岛北部）。OID/AID/SID 枚举由 The Combat Reborn Team (LTS) 数据导入生成。
namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN1TwoHeadedAevis;

[ModuleInfo(BossModuleInfo.Maturity.Contributed, // boss1 实测通过（2026-08-07），保持发布状态
    StatesType = typeof(TwoHeadedAevisStates),
    ConfigType = null, // 如需要可替换为 typeof(TwoHeadedAevisConfig)
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    StatusIDType = typeof(SID),
    TetherIDType = typeof(TetherID),
    IconIDType = typeof(IconID),
    PrimaryActorOID = (uint)OID.TwoHeadedAevis,
    Contributors = "The Combat Reborn Team (LTS)",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CFC,
    GroupID = 1093u,
    NameID = 14489u,
    SortOrder = 1,
    PlanLevel = 0)]
[SkipLocalsInit]
// 主 boss 0x4C11 为双头怪鸟本体（回放实测）；0x4C12/0x4C13 是绿/蓝头（战斗开始前即在场，为可战斗实体）。
// 回放实测（2026-08-06 三场）：本体 0x4C11 全程不可目标化（tgt=False），默认 CheckPull 永不成立，
// 故 override CheckPull 用绿/蓝头可目标化作为拉怪条件；出战斗端本体 DIE+（HP 归零）触发 DeathPhase 结束，
// 无需改 States、无需改回 PrimaryActorOID（0x4C12 无 DIE+/ACT-，改回反而出战斗不识别）。
// 场地方形半宽 20f：用户实测 20f 方形（边长 40）；回放实测玩家贴西墙停点 x=-918.4（相对中心 -900 偏移 18.4）
// 仍全落在墙 ±20 内，半宽 18 会越界。
public sealed class TwoHeadedAevis : BossModule
{
    public TwoHeadedAevis(WorldState ws, Actor primary) : base(ws, primary, new(-900f, 700f), new ArenaBoundsSquare(20f))
    {
        ActivateComponent<BlazeGuide>(); // 钢铁月环绿圈引导（KeepOnPhaseChange，跨相位常驻）
        ActivateComponent<WeakGuide>(); // 弱引导矩形（KeepOnPhaseChange，跨相位常驻）
    }

    protected override bool CheckPull() => PrimaryActor.InCombat && (PrimaryActor.IsTargetable || IsAnyActorTargetable((uint)OID.GreenHead1) || IsAnyActorTargetable((uint)OID.BlueHead1));
}

// ==================== 组件（形状/时机均来自 2026-08-06 三场回放实测） ====================

// 弱引导矩形（2026-08-07 用户实测）：对角 (-888,708)-(-912,687)，最弱正向引导，AI 无其他干扰时倾向进入
sealed class WeakGuide(BossModule module) : BossComponent(module)
{
    private static readonly WPos Center = new(-900f, 697.5f); // x 中心 -900（半宽 12，2026-08-09 用户调整）、z 中心 697.5（半宽 10.5）
    private const float HalfX = 12f; // x ∈ [-912, -888]
    private const float HalfZ = 10.5f; // z ∈ [687, 708]

    public override bool KeepOnPhaseChange => true; // 常驻弱引导

    // 最弱正向引导（weight 0.1，先例 DeepDungeon GoalSingleTarget(_, 3, 0.1f)）：
    // GoalZones 各得分相加，其他机制的强制目标（1f 及以上）会完全覆盖 0.1f，故仅无干扰时 AI 倾向进入矩形
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        hints.GoalZones.Add(p => Math.Abs(p.X - Center.X) <= HalfX && Math.Abs(p.Z - Center.Z) <= HalfZ ? 0.1f : 0f);
    }
}

// 决战（开战全屏 AoE）：本体 49727 + 双头 49726 同步读条 4.7s，回放确认全屏无落点
sealed class OpeningClash(BossModule module) : Components.RaidwideCast(module, (uint)AID.Ability_DecisiveClash1, "决战：全屏伤害");

// 剧毒吐息：Helper 47617 在场地中心放 R18 大圈（回放实测 loc=中心 (-900,700)，R18>半宽 17.5，四角安全）。
// 诅咒复合（2026-08-09 用户方案，参照 Clusters）：定时诅咒复合时 AI 禁区 = R18 圈沿击退反方向平移 20f
// （等价变换：落点(P+D)∈C ⟺ P∈(C−D)；5403 东风→平移东 20f、5404 西风→平移西 20f）；
// 雷达显示（ActiveAOEs）保持原始位置不动；无诅咒 → 基类常规危险区（不平移）。
sealed class PoisonBreath(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Ability_PoisonBreath, 18f)
{
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var curse = Module.FindComponent<CursedTimer>()?.KnockbackFor(actor);
        if (curse != null && curse.Value.Kind == Components.GenericKnockback.Kind.DirForward)
        {
            // 有诅咒：禁区 = R18 圈沿击退反方向平移 20f（C−D 等价变换），不叠加原始禁区
            var shift = -(curse.Value.Distance * curse.Value.Direction.ToDirection());
            var aoes = ActiveAOEs(slot, actor);
            var len = aoes.Length;
            for (var i = 0; i < len; ++i)
            {
                ref readonly var aoe = ref aoes[i];
                if (aoe.Risky)
                {
                    hints.AddForbiddenZone(new AOEShapeCircle(18f), aoe.Origin + shift); // 平移圆（单圆）
                }
            }
        }
        else
        {
            base.AddAIHints(slot, actor, assignment, hints); // 无诅咒：常规危险区（不平移）
        }
    }
}

// 风暴吐息击退（二段）：绿头 Helper 48243 以中心为原点向外击退（施法落点回放验证 = 场地中心 (-900,700)；
// 距离按用户实测 2026-08-07 修正：位移 (-10.7,-9.0)≈14.0m，取 14f；R30 覆盖全场）。
// 二连击退箭头链（2026-08-07 用户要求，参考 CE206 宝石兽 CircularKnockback）：一段（定时诅咒 20y 定向）未结算时，
// 二段箭头接在一段箭头末尾连续延伸；一段结算（buff 消失）后，二段箭头从玩家实时位置出发。
// 回放 0804 场：诅咒 06:05:16.94 挂 buff（12.956s）→ 06:05:29.9 结算（位移 20.0y：(-881.88,699.38)→(-901.89,699.26)，向西）；
// 风暴 48243 06:05:24.38 读条（7.7s）→ 06:05:32.1 生效（从一段终点沿远离中心方向继续），两段间隔 2.2s，一段终点即二段起点。
sealed class StormBreath(BossModule module) : Components.GenericKnockback(module, stopAtWall: true)
{
    private readonly List<Knockback> _casters = [with(4)];

    // 是否有活跃的二段击退（供 CursedTimer 判定一段终点是否为最终落点，2026-08-07 用户补充细化）
    public bool Active => _casters.Count != 0;

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor) => CollectionsMarshal.AsSpan(_casters);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.Ability_StormsBreathAOE)
        {
            _casters.Add(new(spell.LocXZ, 14f, Module.CastFinishAt(spell), kind: Kind.AwayFromOrigin, actorID: caster.InstanceID));
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.Ability_StormsBreathAOE)
        {
            _casters.RemoveAll(kb => kb.ActorID == caster.InstanceID);
        }
    }

    // 二段箭头链式绘制（样式改回基类黄线+落点，2026-08-07 用户要求：CE206 绿色箭头不符合习惯，两段统一）：
    // 一段未结算 → 起点=一段终点（玩家位置+诅咒 20y 定向）；已结算 → 起点=玩家实时位置；
    // 落点标记画在二段（最终）终点（用户补充细化：链式事件仅显示最后落点，中间点只画黄线）
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var kb in _casters)
        {
            var from = pc.Position;
            var curse = Module.FindComponent<CursedTimer>()?.KnockbackFor(pc);
            if (curse != null && curse.Value.Kind == Kind.DirForward)
            {
                from = pc.Position + curse.Value.Distance * curse.Value.Direction.ToDirection(); // 一段终点 = 二段起点
            }

            var away = from - kb.Origin;
            if (away == default)
            {
                continue;
            }

            DrawKnockback(from, from + kb.Distance * away.Normalized(), pc.Rotation, Arena); // 黄线 + 最终落点标记
        }
    }

    // 风暴带（2026-08-07 用户修正：半宽 6f~40f 超大方形环带）：
    // 差集 = 距带中心 6~40 的方形环带——覆盖整个场地（除中心 6×6 区），风暴读条期间持续禁入
    // （activation=default 立即死区）。
    // 诅咒复合平移（2026-08-07 用户补充）：复合时带沿上风口（诅咒反方向）平移 20y——
    // 5403 东风（向西吹）→ 带中心东移 (-880,700)（孔在东侧，AI 站孔内被吹向西后落点在安全区）；
    // 5404 西风 → 中心 (-920,700)（孔在西侧）；无诅咒 → 中心 (-900,700)。
    // 三带静态（AOEShapeCustom SDF 首次初始化后固定，AddForbiddenZone origin 与带中心恒定一致）；仅 AI 视觉，雷达不变
    private static readonly AOEShapeCustom StormBand = new(
        [new Rectangle(new(-900f, 700f), 40f, 40f)], // 外框（超大，覆盖全场）
        [new Rectangle(new(-900f, 700f), 3.5f, 3.5f)]); // 中心安全区（差集=3.5~40 环带；内径 6→3.5，2026-08-07 用户要求：增加余量容错）
    private static readonly AOEShapeCustom StormBandEast = new(
        [new Rectangle(new(-880f, 700f), 40f, 40f)], // 5403 东风复合：带上风口东移 20y（孔在东侧）
        [new Rectangle(new(-880f, 700f), 3.5f, 3.5f)]);
    private static readonly AOEShapeCustom StormBandWest = new(
        [new Rectangle(new(-920f, 700f), 40f, 40f)], // 5404 西风复合：带上风口西移 20y（孔在西侧）
        [new Rectangle(new(-920f, 700f), 3.5f, 3.5f)]);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // 选带：有诅咒（未结算）→ 沿上风口平移的带；无诅咒 → 原带（中心 -900）
        var curse = Module.FindComponent<CursedTimer>()?.KnockbackFor(actor);
        var band = StormBand;
        var bandCenter = new WPos(-900f, 700f);
        if (curse is { Kind: Kind.DirForward } curseKb)
        {
            var eastShift = curseKb.Direction.Rad < 0; // 5403 东风（向西吹）→ 带东移
            band = eastShift ? StormBandEast : StormBandWest;
            bandCenter = eastShift ? new WPos(-880f, 700f) : new WPos(-920f, 700f);
        }

        foreach (var kb in _casters)
        {
            hints.AddForbiddenZone(band, bandCenter); // 风暴读条期间持续禁入（default=立即死区）
            break;
        }
    }
}

// 雷霜暴风雨：全屏 AoE（本体 47736，双头 47735 同步读条 4.7s）
sealed class ThunderfrostTempest(BossModule module) : Components.RaidwideCast(module, (uint)AID.Ability_ThunderfrostTempest, "雷霜暴风雨：全屏伤害");

// 定时诅咒·东/西风：玩家获得 5403（东风）/5404（西风）状态后被定向击退 20y。
// 定时诅咒方向按 buff 区分（2026-08-07 用户实测确认）：东风 5403 从东向西击退 → 画西向箭头（-x）；
// 西风 5404 从西向东击退 → 画东向箭头（+x）；无 buff 不画。方向映射已按此实现，BossMod 角度 0=南、方向向量 (sin a, cos a)：
// -90°→(-1,0) 西 ✓、+90°→(1,0) 东 ✓（回放 0557 场 05:58:06.70 同批 8 人挂 buff：5403×3 + 5404×5，东西向箭头并存属正确表现，
// 本地玩家视角只画自己的一条击退线）。
// 回放实测（0804/0557 场）：完整位移 20.1y（0804 冰糖玩玩）/19.8y（0804 萧恪之）/20.0y（0557 冰糖玩玩）；
// 结算时刻=状态获得+剩余时长（STA+12.979s）；与风暴吐息（48243 击退）构成二连击退：诅咒先结算、风暴后生效，间隔约 2.2-2.4s。
// DestinationUnsafe 参考 CE207：击退落点若落在当前激活的危险 AOE 内则拒绝，保证 AI 预计落点安全。
sealed class CursedTimer(BossModule module) : Components.GenericKnockback(module, stopAtWall: true)
{
    private readonly List<Knockback> _displayed = [with(8)];
    private readonly List<Knockback> _filtered = [with(8)];

    // 只返回该玩家自己的诅咒击退线（2026-08-07 修复：原实现返回全部玩家的击退，8 人队伍 5403/5404 并存时
    // 本地视角会同时看到东西两个方向的箭头——实测确认本地视角也是双箭头，根因在此）
    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        _filtered.Clear();
        foreach (var kb in _displayed)
        {
            if (kb.ActorID == actor.InstanceID)
            {
                _filtered.Add(kb);
            }
        }
        return CollectionsMarshal.AsSpan(_filtered);
    }

    // 二连击退箭头链（2026-08-07 用户要求，参考 CE206 宝石兽）：供 StormBreath 查询本玩家的诅咒击退——
    // 未结算时返回非 null（二段箭头接一段箭头末尾）；结算（buff 消失，OnStatusLose 移除）后返回 null（二段从玩家实时位置出发）
    public Knockback? KnockbackFor(Actor actor)
    {
        foreach (var kb in _displayed)
        {
            if (kb.ActorID == actor.InstanceID)
            {
                return kb;
            }
        }
        return null;
    }

    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        var dir = status.ID switch
        {
            (uint)SID.EasterlyReprise => -90f.Degrees(), // 东风 5403 → 吹向西（-x），画西向箭头
            (uint)SID.WesterlyReprise => 90f.Degrees(),  // 西风 5404 → 吹向东（+x），画东向箭头
            _ => default
        };
        if (dir == default || Raid.FindSlot(actor.InstanceID) < 0)
        {
            return;
        }

        _displayed.RemoveAll(kb => kb.ActorID == actor.InstanceID);
        _displayed.Add(new(actor.Position, 20f, status.ExpireAt, kind: Kind.DirForward, direction: dir, actorID: actor.InstanceID));
    }

    public override void OnStatusLose(Actor actor, ref ActorStatus status)
    {
        if (status.ID is (uint)SID.EasterlyReprise or (uint)SID.WesterlyReprise)
        {
            _displayed.RemoveAll(kb => kb.ActorID == actor.InstanceID);
        }
    }

    // 击退落点安全验证：场外（stopAtWall=true 时 base 恒 false，墙会挡下玩家）或落在任一激活的危险 AOE 内则不安全
    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos)
    {
        if (base.DestinationUnsafe(slot, actor, pos))
        {
            return true;
        }

        foreach (var comp in Module.Components)
        {
            if (comp is Components.GenericAOEs aoes)
            {
                foreach (var aoe in aoes.ActiveAOEs(slot, actor))
                {
                    if (aoe.Risky && aoe.Shape.Check(pos, aoe.Origin, aoe.Rotation))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    // 一段箭头绘制（2026-08-07 用户补充细化：链式事件仅显示最后落点）：
    // 黄线照画；落点标记仅当二段（风暴吐息）无活跃击退时画（一段终点=最终落点）；
    // 二段在读条时一段终点是链中间点，不画落点标记（最终落点由 StormBreath 画在二段终点）
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        var movements = CalculateMovements(pcSlot, pc);
        var hasSecond = Module.FindComponent<StormBreath>()?.Active == true;
        foreach (var m in movements)
        {
            Arena.AddLine(m.from, m.to); // 黄线箭头
            if (!hasSecond)
            {
                Arena.ActorProjected(m.from, m.to, pc.Rotation, Colors.Danger); // 最终落点标记
            }
        }
    }

    // 防出界机制（2026-08-07 用户要求：所有击退事件）——定时诅咒定向击退 20y 的带形禁区
    // （通用几何：落点 P+D 出界 ⟺ P 不在"场地沿击退方向平移 D 后的区域"内 → 危险带=原场地−平移后场地；
    // FTMN1 特例：方形半宽 20、中心 (-900,700)——5403 东风→西带 x∈[-920,-900]、5404 西风→东带 x∈[-900,-880]，
    // 从墙内缘向场内延伸 20y、z 全宽 40；激活时间=诅咒结算时刻；自 Clusters 迁移至此（击退组件归属正确，
    // 无簇 AOE 时同样生效）；仅 AI 视觉（AddForbiddenZone），雷达不变
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var kb = KnockbackFor(actor);
        if (kb == null || kb.Value.Kind != Kind.DirForward)
        {
            return;
        }

        // 诅咒禁入区（2026-08-07 用户修正）：带诅咒期间下风口半场持续禁入——
        // 5403 东风（向西吹）→ 下风口=西 → 西半区带 x∈[-920,-900]；5404 西风 → 东半区带 x∈[-900,-880]；
        // 无条件添加（持续到诅咒结算/消失）；activation=default → 栅格 G=0 立即死区，AI 全程避开（避免靠近才被推开）
        var east = kb.Value.Direction.Rad >= 0; // 5404 西风 → 向东击退 → 东带；5403 东风 → 西带
        hints.AddForbiddenZone(new AOEShapeRect(20f, 20f), new WPos(-900f, 700f), (east ? 90f : -90f).Degrees());
    }
}

// 双头恐惧三列：Helper 50658 在列中心画南北向 Rect 宽 10 长 40（列中心 z=679.99=场地北缘外 2.5y，朝南 40y 覆盖全场南北；
// 半宽 5 由 4 个命中玩家点位验证（斐涅 -898.9/埃攸特 -892.3/茉攸诺 -888.7/群願 -908.5 均在半宽 5 内、半宽 2.5 外）；
// 两侧列 x=-915/-885 或中间列 x=-905/-895 交替，顺序随机；中间两列半宽 5 相接无缝隙）
sealed class TwoTerrors(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Ability_TwoTerrors1, new AOEShapeRect(40f, 5f));

// 雷簇/冰簇连线：Helper 50697（雷）/50698（冰）在连线处 Circle R15（回放实测与小头 4C14/4C15 施法位置一致，绿雷蓝冰）。
// 深黄色（2026-08-07 用户实测配色：簇自身 AOE 深黄）。
// 簇危险区击退平移（2026-08-07 用户方案，替换落点计算方案）：诅咒击退复合簇 AOE 时，
// AI 视角危险区 = 簇 AOE 中心沿击退反方向平移 20y（等价变换：落点(P+D)∈C ⟺ P∈(C−D)）；
// 每个 AI 按自己的诅咒方向分别处理（5403 东风→平移东 20y、5404 西风→平移西 20y）；
// 无诅咒 → 常规危险区（不平移）。机制为"先击退后结算"，AI 只需避开平移区即可精确保证落点安全。
sealed class Clusters : Components.SimpleAOEGroups
{
    public Clusters(BossModule module) : base(module, [(uint)AID.Ability_LightningCluster, (uint)AID.Ability_IceCluster1], 15f)
    {
        // 颜色按紧迫度机制（2026-08-07 用户修正：非自定义色值）——簇读条中=高紧迫，用 Colors.Danger
        // （与雷球/冰球爆炸瞬间禁入区域色一致=深黄参考）
        Color = Colors.Danger;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var curse = Module.FindComponent<CursedTimer>()?.KnockbackFor(actor);
        if (curse != null && curse.Value.Kind == Components.GenericKnockback.Kind.DirForward)
        {
            // 有诅咒：禁区 = 簇 AOE 沿击退反方向平移 20y（C−D 等价变换；2026-08-07 回滚两圆并集为单圆平移）。
            // 禁入区持久化：activation=default 立即死区；诅咒防出界见 CursedTimer.AddAIHints
            var dir = curse.Value.Direction.ToDirection();
            var shift = -(curse.Value.Distance * dir);
            var aoes = ActiveAOEs(slot, actor);
            var len = aoes.Length;
            for (var i = 0; i < len; ++i)
            {
                ref readonly var aoe = ref aoes[i];
                if (aoe.Risky)
                {
                    hints.AddForbiddenZone(new AOEShapeCircle(15f), aoe.Origin + shift); // 平移圆（单圆）
                }
            }
        }
        else
        {
            base.AddAIHints(slot, actor, assignment, hints); // 无诅咒：常规危险区（不平移）
        }
    }
}

// 球爆炸（召唤阶段）：冰球 4C17（47707 冰碎）/雷球 4C16（47706 放电）实时位置 R15。
// 球预警配色（2026-08-07 用户实测）：簇命中球预亮浅黄 / 雷霜暴风雨（47735/47736=ACT AoE 阶段）后剩余球浅黄 /
// 球读条（即将爆炸）深黄强预警（Colors.Danger 现有样式不动）。
// 颜色按紧迫度机制（2026-08-07 用户修正：非自定义色值）——预亮低紧迫用默认色（Colors.AOE 浅黄，与钢铁月环一致）、
// 球读条高紧迫用 Colors.Danger（深黄参考）。
// 预亮判定：冰簇/雷簇（50698 冰/50697 雷）落点 15m 邻域内同属性未激活球 → 浅黄（回放 0557：冰簇 (-910,700) 命中西侧 2 冰球=下一批）；
// 雷霜暴风雨读条开始 → 场上剩余未激活球 → 浅黄（窗口=读条结束+7.5s，对应 ACT t=7.5；
// 回放 0557：雷霜 05:58:45.55 → 次批球 05:58:51.45 激活，窗口覆盖；球读条激活后深黄覆盖）。
sealed class Orbs(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCircle Shape = new(15f);
    private readonly List<AOEInstance> _displayed = [with(16)];
    private readonly List<(WPos pos, bool ice, DateTime expire)> _clusters = [with(4)]; // 冰雷线读条（位置/属性/预亮窗口到期）
    private DateTime _stormExpire = default; // 雷霜暴风雨（AoE 阶段）剩余球浅黄窗口到期（读条结束+7.5s）

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_displayed);

    // 预亮球 AI 禁区（2026-08-07 用户修正）：雷达浅黄预亮（预测下一组）的球同步加 AI 禁区
    // （Circle R15、activation=default 立即禁入）——AI 视觉与雷达同步，AI 不会走进预亮区；激活球（risky）禁区由基类处理。
    // 第一组球复合平移（2026-08-07 用户要求：簇+第一组球一起平移）——落点 P+D 在球内 ⟺ P 在 (B−D) 内：
    // 簇 15m 邻域命中的第一组球禁区中心 = 球位置 − D（5403 东风向西吹 → 球禁区东移 20y；5404 → 西移）；
    // 雷霜组球（无簇邻域）保持原位；无诅咒全部原位（curseShift=零向量）
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        var curse = Module.FindComponent<CursedTimer>()?.KnockbackFor(actor);
        var curseShift = curse is { Kind: Components.GenericKnockback.Kind.DirForward } c
            ? -(c.Distance * c.Direction.ToDirection()) // −D（击退位移反方向）
            : default;

        foreach (var orb in Module.Enemies((uint)OID.BallLightning).Concat(Module.Enemies((uint)OID.SwirlingOrb)))
        {
            if (orb.IsDeadOrDestroyed)
            {
                continue;
            }

            var cast = orb.CastInfo;
            if (cast != null && cast.Action.ID is (uint)AID.Ability_Shock or (uint)AID.Ability_HypothermalCombustion)
            {
                continue; // 激活球（基类禁区）
            }

            // 预亮状态判定（与 Update 一致）：簇 15m 邻域同属性（第一组）或雷霜暴风雨窗口（其余球）
            var isIce = orb.OID == (uint)OID.SwirlingOrb;
            var clusterHit = false;
            foreach (var cl in _clusters)
            {
                if (cl.ice == isIce && (orb.Position - cl.pos).LengthSq() <= 15f * 15f)
                {
                    clusterHit = true;
                    break;
                }
            }

            if (!clusterHit && _stormExpire == default)
            {
                continue; // 不在预亮状态
            }

            hints.AddForbiddenZone(Shape, clusterHit ? orb.Position + curseShift : orb.Position); // 第一组复合平移 / 雷霜组原位
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        var ice = spell.Action.ID == (uint)AID.Ability_IceCluster1;
        if (ice || spell.Action.ID == (uint)AID.Ability_LightningCluster)
        {
            _clusters.Add((spell.LocXZ, ice, Module.CastFinishAt(spell).AddSeconds(15d))); // 预亮窗口：读条结束+15s（覆盖到球激活）
        }
        else if (spell.Action.ID is (uint)AID.Ability_ThunderfrostTempest or (uint)AID.Ability_4) // 47735 双头/47736 本体 雷霜暴风雨
        {
            _stormExpire = Module.CastFinishAt(spell).AddSeconds(7.5d); // 剩余球浅黄窗口（ACT t=7.5）
        }
    }

    public override void Update()
    {
        var now = WorldState.CurrentTime;
        _clusters.RemoveAll(c => c.expire < now);
        if (_stormExpire < now)
        {
            _stormExpire = default;
        }

        _displayed.Clear();
        foreach (var orb in Module.Enemies((uint)OID.BallLightning).Concat(Module.Enemies((uint)OID.SwirlingOrb)))
        {
            if (orb.IsDeadOrDestroyed)
            {
                continue;
            }

            var cast = orb.CastInfo;
            if (cast != null && cast.Action.ID is (uint)AID.Ability_Shock or (uint)AID.Ability_HypothermalCombustion)
            {
                _displayed.Add(new(Shape, orb.Position, default, Module.CastFinishAt(cast), Colors.Danger)); // 即将爆炸：深黄强预警（现有样式）
                continue;
            }

            // 浅黄预亮：簇 15m 邻域同属性（冰球 4C17/雷球 4C16）或 雷霜暴风雨窗口内剩余球
            var predict = _stormExpire != default;
            if (!predict)
            {
                var isIce = orb.OID == (uint)OID.SwirlingOrb;
                foreach (var c in _clusters)
                {
                    if (c.ice == isIce && (orb.Position - c.pos).LengthSq() <= 15f * 15f)
                    {
                        predict = true;
                        break;
                    }
                }
            }

            if (predict)
            {
                // 颜色按紧迫度机制（2026-08-07 用户修正：非自定义色值）——预亮=低紧迫，color 用默认（绘制 Colors.AOE 浅黄，与钢铁月环显示色一致）
                _displayed.Add(new(Shape, orb.Position, default, default, default, false)); // 浅黄预亮（预测下一组）
            }
        }
    }
}

// 冰焰凝环-小圈：Helper 50703/50704/50705 在落点 R5（钢铁，先炸 5.7s 读条）
sealed class BlazeFlames(BossModule module) : Components.SimpleAOEGroups(module, [(uint)AID.Ability_Blaze1, (uint)AID.Ability_Blaze3, (uint)AID.Ability_Blaze5], 5f);

// 冰焰凝环-大环：Helper 47660 donut 5-60（月环，延迟 ~6s 后 2.2s 读条，落点中心 5y 内安全，先站小圈外再进圈躲月环）。
// 红色禁区由基类自动处理；月环无需引导圈（donut 外圈全为禁入区，AI 躲避红区自然进入内圈安全区，2026-08-07 移除引导）。
sealed class BlazeLoop(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Ability_Blazeloop1, new AOEShapeDonut(5f, 60f));

// 钢铁月环绿圈引导（2026-08-07 用户实测，0804 场 06:06:53 机制）：
// 钢铁（R5 实心圆，先炸）读条（本体 47656/47655、头 47661，5.0-6.0s）开始 → 在钢铁落点画 R7 绿色圈
// （钢铁边缘+2f，2026-08-07 用户调整：R5→R7），持续 6s（用户实测），引导 AI 靠近钢铁边缘外侧——
// 钢铁炸后月环（donut 5-60）安全区=钢铁区域（无空隙：钢铁外缘 R5=月环内缘 5，同心）。
// 钢铁落点 = 同刻冰焰读条落点（47659/47663/47664，回放 0804 场与钢铁读条同毫秒触发：06:06:35.531/06:06:49.648/06:06:53.648）。
// 钢铁/月环 AOE 绘制由 BlazeFlames（R5）/BlazeLoop（donut 5-60）负责，本组件仅绿圈+AI 引导。
sealed class BlazeGuide(BossModule module) : BossComponent(module)
{
    private readonly List<(WPos pos, DateTime expire)> _circles = [with(4)];

    public override bool KeepOnPhaseChange => true;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.Ability_Blaze or (uint)AID.Ability_Blaze2 or (uint)AID.Ability_Blaze4)
        {
            _circles.Add((spell.LocXZ, WorldState.FutureTime(6d))); // 绿圈持续 6s（用户实测）
        }
    }

    public override void Update()
    {
        var now = WorldState.CurrentTime;
        _circles.RemoveAll(c => c.expire < now);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var c in _circles)
        {
            Arena.ZoneCircleOutline(c.pos, 7f, Colors.Safe); // 钢铁边缘+2f 绿圈 R7（2026-08-07 用户调整）
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // AI 引导优先级（2026-08-07 用户补充）：二连钢铁月环并存时只引导 AI 去最先触发/最接近生效的绿圈
        // （expire 最小者=触发最早、最接近爆炸），后执行的绿圈只画圈不引导，避免 AI 被引到后执行的钢铁月环
        if (_circles.Count == 0)
        {
            return;
        }

        var soon = _circles[0].expire;
        foreach (var c in _circles)
        {
            if (c.expire < soon)
            {
                soon = c.expire;
            }
        }

        foreach (var c in _circles)
        {
            if (c.expire <= soon)
            {
                hints.GoalZones.Add(AIHints.GoalSingleTarget(c.pos, 7f)); // 仅最先生效的绿圈引导 AI 到钢铁边缘外 2f
            }
        }
    }
}

// 魔阵光（终局）：16 个立体魔法阵 4B73 立于场地中心十字线（z=700 行朝南、x=-900 列朝西，各 8 个）发射 Rect 5x60 光束。
// 两侧贯穿（2026-08-07 用户实测修正）：魔法阵位于矩形中心，向两侧各贯穿 30y（回放落点偏移恰为 30y=range 60 一半）；
// 原单向 60y 实现漏掉内侧 30y 且场外多画 30y。竖列光束东西向贯穿 x∈[-930,-870]、横排光束南北向贯穿 z∈[670,730]。
// 方向用 cast 落点与 Font 位置推导（回放 rotation 为游戏原值，不宜直用）
sealed class ArcaneBeacon(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<AOEInstance> _aoes = [];

    // 紧迫度分级（2026-08-07 用户要求）：最接近生效的一批（前组 8 个）深黄（Colors.Danger）+risky 触发 AI 规避，
    // 其余批次（后组 8 个，3s 后生效）淡色（Colors.AOE 默认）risky=false 仅提示，避免全场封锁
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var soon = DateTime.MaxValue;
        foreach (var a in _aoes)
        {
            if (a.Activation < soon)
            {
                soon = a.Activation;
            }
        }

        for (var i = 0; i < _aoes.Count; ++i)
        {
            var a = _aoes[i];
            var urgent = soon != DateTime.MaxValue && a.Activation <= soon.AddSeconds(0.5f);
            _aoes[i] = urgent ? a with { Color = Colors.Danger, Risky = true } : a with { Color = Colors.AOE, Risky = false };
        }

        return CollectionsMarshal.AsSpan(_aoes);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.Ability_ArcaneBeacon)
        {
            return;
        }

        var dir = spell.LocXZ - caster.Position;
        if (dir.LengthSq() < 1f)
        {
            return;
        }

        _aoes.Add(new(new AOEShapeRect(30f, 2.5f, 30f), caster.Position, Angle.FromDirection(dir), Module.CastFinishAt(spell), actorID: caster.InstanceID)); // 两侧贯穿：向落点 30y + 向内贯穿 30y
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.Ability_ArcaneBeacon)
        {
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.Ability_ArcaneBeacon)
        {
            ++NumCasts;
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
        }
    }
}

