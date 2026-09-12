using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.BeastChess.BC1Ogre;

// 奇子·食人魔场（A 槽，圆 R20）。
// AID 数据来自 2026-09-12 两份回放（02_58_36 / 11_38_08）交叉验证 + xivapi Action 表（CastType/EffectRange/XAxisModifier/Omen），
// 形状映射遵循 AIHintsBuilder.GuessShape 的仓库权威约定。
// 小怪：妖火 0x4B8E x6 / 大妖火 0x4DD4 x2（被击杀时自爆 Scorched Earth，ER60 近全屏，无法规避故不画）/ 火球 0x4B8F。
public enum OID : uint
{
    Boss = 0x4B8D, // 奇子·食人魔（HP 12074，槽位 A）
    FaerieFire = 0x4B8E, // 妖火 ×6
    GreaterFaerieFire = 0x4DD4, // 大妖火 ×2
    Fireball = 0x4B8F, // 火球
    Helper = 0x233C,
}

public enum AID : uint
{
    ScorchingSmiteVisual = 46913, // 燃烧猛击：boss->self，4.7s cast，引导 visual；实际扇形由同位同时读条的 Helper 46912 承载（两场同刻 CST+ 实证）
    ScorchingSmiteCone = 46912, // 燃烧猛击：Helper->self，5.7s cast，扇形 40 半角60（CastType13/ER40/omen gl_fan120）；Helper 与 boss 同位同朝向（朝场心），天然画在 boss 冲锋后的新位置
    ScorchingSmiteLongCone = 49688, // 燃烧猛击：Helper->self（对面火圈位），9.0s cast，同款扇形 40 半角60；boss 在一侧火圈读 46910 时另一侧 Helper 施放
    ScorchingSmiteCharge = 46910, // 燃烧猛击：boss->self，5.7s cast，冲锋后的引导 visual（无形状数据）；不画
    ScorchingSmiteBurst = 46911, // 燃烧猛击：boss no-cast 自身结算（ER0 无形状）；不画
    ChargeJump = 46914, // (无名)：boss no-cast 冲锋事件，loc=落点（实测两火圈 (104,-420)/(136,-420) 与场心间跳移，0.1-0.7s 完成位移）；位移本身不画
    Allfire = 46915, // 猛火喷发：boss->self，3.7s cast，圆 R40 全场级 AOE（raidwide）
    Magma = 46916, // 熔岩：Helper->location，2.7s cast，圆 R3（CastType2/ER3）；每波 20 圈 = 4 轮 x 5 个（轮间隔约 1.0s，读条 2.7s 交叉重叠），落点在开场 46915 所在方向前方扇形密布
    MagmaFireball = 46917, // 熔岩：Helper->location，2.7s cast，圆 R5（CastType2/ER5）；两场各 2 个，落点=东西火圈 (104,-420)/(136,-420)，结算后地面残留持续伤害圈
    BurningWard = 46918, // 火灵的守护：boss->self，2.7s cast，自身增益 = 无敌召唤期开始信号（结算 ~1s 后 boss 不可选中，妖火/大妖火打 boss 并依次自爆，约 36s 后恢复可选中）；读条完毕时 boss 脚下生成 R4 火圈（见 SummonFireCircle）
    ScorchedEarthFaerie = 46919, // 大火焰：妖火自爆，no-cast，ER60 近全屏（无法规避）；不画
    ScorchedEarthGreater = 49728, // 大火焰：大妖火自爆，no-cast，ER60 近全屏；不画
    FireCall = 46921, // 火球生成：boss->self，3.7s cast，召唤 visual；不画
    ArmOfPurgatory = 46922, // 延烧：火球->Enemy，0.7s cast，圆 R10（CastType2/ER10），火球本体位置爆炸
    AutoAttack = 49682, // 食人魔自动攻击：no-cast（两场 12/11 次，命中 dist 2.8-5.1y rel_ang≈0）
}

// 燃烧猛击扇形：46912（跟随 46913 引导）+ 49688（对面火圈 9s 长读条）
sealed class ScorchingSmiteCones(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCone Cone = new(40f, 60f.Degrees()); // omen fan120 = 120° 全角

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.ScorchingSmiteCone or (uint)AID.ScorchingSmiteLongCone => new(Cone),
        _ => null,
    };
}

// 猛火喷发后的熔岩小圆流：每波 20 圈 R3，实测 4 轮 x 5 个（轮间隔约 1.0s、读条 2.7s 相互重叠），
// 与 46917 大火圈同刻起跑，故 MaxDisplayed 需容纳重叠峰值 20 条
sealed class MagmaBursts(BossModule module) : ReplayValidatedCastAOEs(module)
{
    protected override int MaxDisplayed => 20;

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.Magma => new(new AOEShapeCircle(3f), LocationTargeted: true),
        _ => null,
    };
}

// 火球机制（用户游戏画面确认的完整时序）：46921 读条 4s 结算 → 火球 0x4B8F 出生在 boss 脚下 →
// 火球向"出生瞬间玩家所在位置"移动（旧场实测漂移 ~0.9y，爆炸点=玩家出生瞬间位置附近）→
// 出生后 ~5.7s 火球开始读 46922 延烧（1.0s）→ 爆炸（圆 R10）。
// 预警时序（用户定稿）：出生时 snapshot 玩家位置 → 出生+4s 起画 R10 预警圈（圆心=snapshot）→
// 46922 开始读条时圆心由火球本体接管（每帧跟随），activation=读条完成 → 爆炸后移除。
// 旧场唯一完整样本：出生 10828.66 → 画圈 10832.66 → 读条接管 10834.38 → 爆炸 10835.36。
// snapshot 双路：46921 CST! 结算先建状态（保底，新场回放有）；0x4B8F ACT+ 出生时刷新精确位置；
// 30s 兜底重置防事件缺失卡死（吸取五列扫击跨波字典残留教训，全事件驱动+Update 兜底，无跨波字典）。
sealed class FireballBlast(BossModule module) : BossComponent(module)
{
    private const float Radius = 10f;
    private const double WarningDelay = 4d;     // 出生到预警出现
    private const double EstimatedBlast = 6.7d; // 出生到爆炸的实测总时长（预警期预估紧迫度用）
    private const double FailsafeReset = 30d;   // 出生后无后续事件的强制重置窗口
    private static readonly AOEShapeCircle Shape = new(Radius);

    private enum Stage { Idle, Tracking, Takeover }
    private Stage _stage;
    private WPos _target;         // snapshot 的玩家位置（=预计爆炸圆心），接管阶段切换为火球实时位置
    private DateTime _bornAt;
    private DateTime _activation; // 接管后 = 46922 读条完成时刻

    private Actor? Fireball => Module.Enemies((uint)OID.Fireball).FirstOrDefault(f => !f.IsDeadOrDestroyed);
    private bool WarningActive => _stage != Stage.Idle && (_stage == Stage.Takeover || WorldState.CurrentTime >= _bornAt.AddSeconds(WarningDelay));

    private static WPos SnapshotPlayer(BossModule module) => module.WorldState.Party.Player()?.Position ?? module.PrimaryActor.Position;

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.FireCall) // 46921 结算：火球即将出现，先按当前玩家位置建状态（保底）
        {
            _target = SnapshotPlayer(Module);
            _bornAt = WorldState.CurrentTime;
            _stage = Stage.Tracking;
        }
        else if (spell.Action.ID == (uint)AID.ArmOfPurgatory) // 46922 爆炸
        {
            Reset();
        }
    }

    public override void OnActorCreated(Actor actor)
    {
        // 火球 actor 实际出现：刷新 snapshot 到出生瞬间（比 CST! 晚 ~0.9s，更精确）
        if (actor.OID == (uint)OID.Fireball && _stage == Stage.Tracking)
        {
            _target = SnapshotPlayer(Module);
            _bornAt = WorldState.CurrentTime;
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        // 46922 开始读条：圆心由火球本体接管（Update 每帧跟随），activation=读条完成
        if (_stage != Stage.Idle && spell.Action.ID == (uint)AID.ArmOfPurgatory)
        {
            _stage = Stage.Takeover;
            _activation = Module.CastFinishAt(spell);
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.OID == (uint)OID.Fireball && _stage != Stage.Idle)
            Reset();
    }

    public override void Update()
    {
        if (_stage == Stage.Idle)
            return;
        if (WorldState.CurrentTime > _bornAt.AddSeconds(FailsafeReset))
        {
            Reset();
            return;
        }
        if (_stage == Stage.Takeover)
        {
            var fireball = Fireball;
            if (fireball == null)
            {
                Reset();
                return;
            }
            _target = fireball.Position;
        }
    }

    public override void DrawArenaBackground(int pcSlot, Actor pc)
    {
        if (WarningActive)
            Arena.ZoneCircle(_target, Radius, Colors.AOE);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (!WarningActive)
            return;
        // 预警期用实测总时长做预估紧迫度；接管期切精确读条完成时刻
        hints.AddForbiddenZone(Shape.Distance(_target, default), _stage == Stage.Takeover ? _activation : _bornAt.AddSeconds(EstimatedBlast));
    }

    private void Reset() => _stage = Stage.Idle;
}

// 残留火圈（46917，东西落点 (104,-420)/(136,-420)，R5）：
// 读条期即显示预警（落点在 CST+ 行即确定）——activation=CastFinishAt 的标准读条语义；
// 结算后无缝转为 25s 持续伤害圈（用户目测定值：回放不可实测——全场无玩家/宠物踩圈受击记录
// （唯一火圈内命中是宠物吃 49682 平砍），落地后亦无周期 tick；xivapi Action 表无残留时长字段）。
// 用户实测修正：游戏内 46915 结算同刻（=46916/46917 读条开始，旧场 46917 CST+ t=10749.96 vs 46915 结算
// t≈10749.11）大火圈预警就已出现；此前组件只在 46917 结算后才显示，晚约 3s，观感落在 46916 第四轮小火圈。
sealed class MagmaFireballs(BossModule module) : Components.GenericAOEs(module)
{
    private const float Radius = 5f;
    private const double Duration = 25d;
    private const double CastExpireGrace = 1d; // 读条条目预期生效后仍无事件时的兜底清理窗口
    private static readonly AOEShapeCircle Shape = new(Radius);

    private readonly List<AOEInstance> _casts = [with(4)]; // 读条期预警：activation = 结算时刻
    private readonly List<AOEInstance> _zones = [with(4)]; // 结算后残留：activation = 失效时刻
    private readonly List<AOEInstance> _displayed = [with(8)];

    private static AOEInstance Make(WPos origin, DateTime activation) => new(Shape, origin, default, activation, shapeDistance: Shape.Distance(origin, default));

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _displayed.Clear();
        _displayed.AddRange(_casts);
        _displayed.AddRange(_zones);
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.MagmaFireball)
            _casts.Add(Make(spell.LocXZ, Module.CastFinishAt(spell)));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.MagmaFireball)
            _casts.RemoveAll(cast => cast.Origin.AlmostEqual(spell.LocXZ, 1f));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != (uint)AID.MagmaFireball)
            return;
        ++NumCasts;
        _casts.RemoveAll(cast => cast.Origin.AlmostEqual(spell.TargetXZ, 1f));
        _zones.Add(Make(spell.TargetXZ, WorldState.CurrentTime.AddSeconds(Duration)));
    }

    public override void Update()
    {
        var now = WorldState.CurrentTime;
        _zones.RemoveAll(zone => now > zone.Activation);
        _casts.RemoveAll(cast => now > cast.Activation.AddSeconds(CastExpireGrace));
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        for (var i = 0; i < _casts.Count; ++i)
            hints.AddForbiddenZone(_casts[i].ShapeDistance ?? Shape.Distance(_casts[i].Origin, default), _casts[i].Activation);
        for (var i = 0; i < _zones.Count; ++i)
            hints.AddForbiddenZone(_zones[i].ShapeDistance ?? Shape.Distance(_zones[i].Origin, default), _zones[i].Activation);
    }
}

sealed class AllfireRaidwide(BossModule module) : Components.RaidwideCast(module, (uint)AID.Allfire);

// 无敌召唤期火圈：boss 读 46918（火灵的守护）进入无敌召唤期（结算 ~1s 后 ATG- 不可选中约 36s，
// 期间妖火/大妖火围攻 boss 并依次自爆），读条完毕时 boss 脚下（=场中）生成 R4 火圈（用户实测：读条期间无火圈）；
// 全部小怪自爆完毕、boss 恢复可选中（ATG+，回放新场 41980.50）约 1s 后火圈消失（用户实测规则）。
// 回放中火圈为纯视觉对象（无 actor/事件行），圆心取 46918 读条时 boss 位置（新场 (119.98,-420.00)=场心，两场一致）。
// 注意与 46922 延烧区分：延烧是召唤产物火球 0x4B8F 的爆炸伤害（R10 瞬间，恢复后阶段）；本组件是
// 无敌期的持续地面效果（R4 全程），两者同属召唤窗口的不同阶段。
sealed class SummonFireCircle(BossModule module) : BossComponent(module)
{
    private const float Radius = 4f;
    private const double LingeringAfterRecover = 1d; // boss 恢复可选中后火圈再滞留 1s（用户实测）

    private WPos _origin;
    private bool _active;
    private DateTime _clearAt; // boss 恢复可选中 + 1s 后的清除时刻

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        // 46918 在回放中只有 CST+ / CST! / AIE+，无 CST- —— OnCastFinished（挂 CST-）永不触发（实战 bug 已踩）。
        // 改用 CST! 结算事件（与 46917 火圈残留同款钩子）；CST! 的 targetPos 为 0,0,0 无落点，圆心取 caster 当时位置。
        if (spell.Action.ID == (uint)AID.BurningWard)
        {
            _origin = caster.Position;
            _active = true;
            _clearAt = default;
        }
    }

    public override void OnActorTargetable(Actor actor)
    {
        // 无敌期结束时 boss 重新可选中：火圈再停留 1s
        if (_active && actor.InstanceID == Module.PrimaryActor.InstanceID && actor.IsTargetable && _clearAt == default)
            _clearAt = WorldState.CurrentTime.AddSeconds(LingeringAfterRecover);
    }

    public override void Update()
    {
        if (_active && _clearAt != default && WorldState.CurrentTime > _clearAt)
        {
            _active = false;
            _clearAt = default;
        }
    }

    public override void DrawArenaBackground(int pcSlot, Actor pc)
    {
        if (_active)
            Arena.ZoneCircle(_origin, Radius, Colors.AOE);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (!_active)
            return;
        // 无敌期未结束前为持续死区（DateTime.MaxValue）；恢复后收窄到清除时刻
        hints.AddForbiddenZone(new SDCircle(_origin, Radius), _clearAt != default ? _clearAt : DateTime.MaxValue);
    }
}

sealed class BC1OgreStates : StateMachineBuilder
{
    public BC1OgreStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<ScorchingSmiteCones>()
            .ActivateOnEnter<MagmaBursts>()
            .ActivateOnEnter<MagmaFireballs>()
            .ActivateOnEnter<FireballBlast>()
            .ActivateOnEnter<SummonFireCircle>()
            .ActivateOnEnter<AllfireRaidwide>()
            .Raw.Update = () => ((BeastChessModule)Module).EnemiesAllDead();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(BC1OgreStates),
    ConfigType = null,
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Uncategorized,
    GroupType = BossModuleInfo.GroupType.CFC,
    GroupID = 1088u,
    NameID = 14538u, // 奇子·食人魔（回放 ACT+ 提取）
    SortOrder = 1,
    PlanLevel = 0)]
public sealed class BC1Ogre(WorldState ws, Actor primary) : BeastChessModule(ws, primary)
{
    public override uint[] EnemiesOfInterest => [(uint)OID.Boss, (uint)OID.FaerieFire, (uint)OID.GreaterFaerieFire, (uint)OID.Fireball];

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actors(this, EnemiesOfInterest);
}
