using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE215ManyMouthsToFeed;

public enum OID : uint
{
    Boss = 0x4BCA, // R7.0, BNpcName 14747, 提蔛 (Many Mouths to Feed)
    BossClone = 0x4BCC, // R0.5, BNpcName 14747, spits venom during flood phase
    VenomPuddle = 0x4BCD, // R2.0, BNpcName 108, persistent venom voidzone
    Helper = 0x233C
}

public enum AID : uint
{
    AutoAttack = 0xC6A2, // boss->player, no cast, single-target

    CentralWhipVisual = 0xB872, // boss->self, 4.7s cast, visual for CentralWhip
    SideWhipVisual = 0xB873, // boss->self, 4.7s cast, visual for SideWhip
    CentralWhip = 0xB874, // helper, 5.7s cast, 中央鞭打, through-body line (52y long, 10y wide)
    SideWhip = 0xB875, // helper, 5.7s cast, 侧方鞭打（正侧，实测 2026-08-15：圆心=helper 沿 rotation-90° 偏移 5y、180° 半圆 r30，平分线朝外）
    SideWhip2 = 0xC241, // helper, 5.7s cast, 侧方鞭打（反侧，实测 2026-08-15：圆心=helper 沿 rotation+90° 偏移 5y、180° 半圆 r30，平分线朝外）

    PollenScatter = 0xB876, // boss->self, 3.7s cast, 花粉飞散, visual precursor for Predation
    Predation = 0xB877, // boss, 6.7s cast, 捕食, 10y circle
    PoisonMistVisualA = 0xB87A, // boss->self, 4.7s cast, visual for PoisonMist
    PoisonMistVisualB = 0xB87B, // boss->self, 4.7s cast, visual for PoisonMist
    PoisonMist = 0xB87C, // helper, 5.7s cast, 毒雾喷射, 90-degree cone (30y)
    PoisonMist2 = 0xC573, // helper, 5.7s cast, 毒雾喷射, 90-degree cone (30y)
    PoisonMist3 = 0xC574, // helper, 5.7s cast, 毒雾喷射, 90-degree cone (30y)
    PoisonMist4 = 0xC575, // helper, 5.7s cast, 毒雾喷射, 90-degree cone (30y)

    VenomBlobVisual = 0xB87D, // boss->self, 3.7s cast, visual for VenomBlob
    VenomBlob = 0xB87E, // helper, 2.7s cast, 毒液块, 5y circle scattered puddles

    SecreteVenom = 0xC242, // boss->self, 2.7s cast, 分泌毒液, visual precursor for Venom
    Venom = 0xB870, // helper, 4.8s cast, 毒液, 2y circle (initial cardinal puddles)
    VenomSpread = 0xB871, // four fixed helpers, no cast, staggered spreading poison (geometry not safely established)

    PoisonRainVisual = 0xB87F, // boss->self, 4.7s cast, 毒雨, raidwide visual
    PoisonRain = 0xB880, // helper, no cast, 毒雨, raidwide damage

    SpitVenom = 0xB86E, // clone (0x4BCC), no cast, persistent outer venom boundary
    SecreteVenomVisualA = 0xB86F, // boss->self, no cast, 分泌毒液 visual
    SecreteVenomVisualB = 0xC2DD // boss->self, no cast, 分泌毒液 visual
}

// B86E has no cast packet, but repeats for the entire encounter and kills targets in the outer
// band. ARR hit positions start just outside 24.5y and the Action sheet gives a 30y outer radius.
sealed class VenomBoundary(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeDonut Shape = new(24.5f, 30f);
    private readonly AOEInstance[] _aoe = [new(Shape, module.Arena.Center)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _aoe;
}

// Cast rotations are replay-verified: each helper cast already carries the packet rotation pointing
// at its own slice, so we register every slice as an independent AOE off the caster position.
sealed class ManyMouthsAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    // 52y long / 10y wide line, centered on the caster (extends front and back).
    private static readonly AOEShapeRect CentralLine = new(26f, 5f, 26f);
    // 侧方鞭打已迁至 SideLashes（2026-08-15 回放实测：左右两个 180° 半圆，圆心偏移 5y，非原 135° 锥），此处不再注册。
    // Poison mist fills 3 of the 4 quadrants with 90-degree cones (45-degree half-angle).
    private static readonly AOEShapeCone Mist = new(30f, 45f.Degrees());
    private static readonly AOEShapeCircle Predation = new(10f);
    private static readonly AOEShapeCircle Blob = new(5f);
    private static readonly AOEShapeCircle VenomCircle = new(2f);

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.CentralWhip => new(CentralLine),
        (uint)AID.PoisonMist or (uint)AID.PoisonMist2 or (uint)AID.PoisonMist3 or (uint)AID.PoisonMist4 => new(Mist),
        (uint)AID.Predation => new(Predation),
        (uint)AID.VenomBlob => new(Blob),
        (uint)AID.Venom => new(VenomCircle),
        _ => null
    };
}

// 侧方鞭打（2026-08-15 回放实测修正，二次修复画同侧）：实际伤害区域是左右两个 180° 半圆
// （Cone r30 半角 90°），圆心 = helper 沿 (面向 ± 90°) 方向偏移 5y 处，扇形平分线朝外。
// 47221 沿 面向-90°、49729 沿 面向+90°——两 helper 与 boss 视觉 47219 同毫秒 CST+ 成对施放
// （5 次回放全部如此），按单侧分配：每个 AID 只画一侧。
// 回放验证（2026-08-15 根因）：spell.Rotation 实际存的是"施法者→CastLocation 落点方向角"
// （47221=-57.292°、49729=122.711°，互差 180°），即已经是 面向±90° 的结果；若再叠加 offset
// 会把两侧算成同向（-147.292 ≡ 212.711 mod 360）→ 两半圆画到同侧。故基准改用 caster（helper）
// 实时面向 caster.Rotation（两 helper 面向相同且稳定，与 boss 面向无关）。
// 47221/49729 具体对应哪侧无机制意义（两侧对称打击），仅需保证两半圆分居两侧。
sealed class SideLashes(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCone HalfCircle = new(30f, 90f.Degrees()); // 180° 半圆，r30

    private static Angle FacingOffsetFor(uint actionID) => actionID switch
    {
        (uint)AID.SideWhip => -90f.Degrees(), // 一侧（AID 对应哪侧无机制意义，仅保证分居两侧）
        (uint)AID.SideWhip2 => 90f.Degrees(), // 另一侧
        _ => default
    };

    private sealed class Lash(uint actionID, AOEInstance aoe)
    {
        public readonly uint ActionID = actionID;
        public readonly AOEInstance AOE = aoe;
    }

    private readonly List<Lash> _lashes = [with(8)];
    private readonly List<AOEInstance> _displayed = [with(8)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        foreach (var lash in _lashes)
        {
            _displayed.Add(lash.AOE);
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => PruneExpired();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        var offset = FacingOffsetFor(spell.Action.ID);
        if (offset == default || spell.EventHappened)
            return;

        var activation = Module.CastFinishAt(spell);
        if (activation <= WorldState.CurrentTime)
            return;

        // 基准 = caster（helper）实时面向 caster.Rotation；圆心与扇形平分线沿 (面向 ± 90°) 方向偏移 5y 朝外。
        // 切勿用 spell.Rotation——它存的是"施法者→落点方向角"（= 面向±90°），再叠 offset 会画到同侧（见类头注释）
        var dir = (caster.Rotation + offset).ToDirection();
        var origin = caster.Position + dir * 5f;
        var aoe = new AOEInstance(HalfCircle, origin, caster.Rotation + offset, activation, risky: true, actorID: caster.InstanceID, shapeDistance: HalfCircle.Distance(origin, caster.Rotation + offset));
        var duplicate = _lashes.FindIndex(entry => entry.ActionID == spell.Action.ID && entry.AOE.ActorID == caster.InstanceID);
        if (duplicate >= 0)
            _lashes[duplicate] = new(spell.Action.ID, aoe);
        else
            _lashes.Add(new(spell.Action.ID, aoe));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is not ((uint)AID.SideWhip or (uint)AID.SideWhip2))
            return;

        // 命中结算：按 施法者+动作 清除对应半圆（重复事件第二次找不到，天然幂等）
        _lashes.RemoveAll(entry => entry.ActionID == spell.Action.ID && entry.AOE.ActorID == caster.InstanceID);
        ++NumCasts;
    }

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        _lashes.RemoveAll(entry => now > entry.AOE.Activation.AddSeconds(2d));
    }
}

// Persistent venom puddles (0x4BCD) are initially created at (0, 0), then moved to their real
// cardinal positions. Ignore the uninitialized spawn coordinates so they cannot create a bogus
// off-arena forbidden zone. These live actors are the later persistent puddles near the arena rim;
// they are separate from the four fixed helpers used by VenomSpread.
sealed class VenomPuddles(BossModule module) : Components.Voidzone(module, 2f,
    static module => module.Enemies((uint)OID.VenomPuddle).Where(actor => !actor.IsDeadOrDestroyed && actor.Position.InCircle(module.Arena.Center, 30f)));

// Each pair of cardinal helpers starts with a 2y Venom cast, then emits eight B871 pulses at
// ~1.07s intervals. Replay hit distances establish an expanding circle: the first pulse reaches
// about 5y and each subsequent pulse grows by roughly 2.5y. Predict the first spread from the
// visible B870 cast instead of waiting until players have already been hit.
sealed class VenomSpread(BossModule module) : Components.GenericAOEs(module)
{
    private sealed class Spread(ulong actorID, WPos origin, DateTime activation)
    {
        public readonly ulong ActorID = actorID;
        public readonly WPos Origin = origin;
        public DateTime Activation = activation;
        public int Pulse;
    }

    private const int PulseCount = 8;
    private const float InitialRadius = 5f;
    private const float RadiusStep = 2.5f;
    private const float FinalRadius = InitialRadius + RadiusStep * (PulseCount - 1);
    private const double FirstDelayAfterVenom = 2.45d;
    private const double PulseInterval = 1.07d;
    private readonly List<Spread> _spreads = [with(4)];
    private readonly List<AOEInstance> _displayed = [with(4)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        Prune();
        _displayed.Clear();
        foreach (var spread in _spreads)
        {
            var shape = new AOEShapeCircle(InitialRadius + RadiusStep * spread.Pulse);
            _displayed.Add(new(shape, spread.Origin, activation: spread.Activation,
                actorID: spread.ActorID, shapeDistance: shape.Distance(spread.Origin, default)));
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => Prune();

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        foreach (var spread in _spreads)
        {
            if (spread.Pulse >= PulseCount - 1)
                continue;

            var finalActivation = spread.Activation.AddSeconds((PulseCount - 1 - spread.Pulse) * PulseInterval);
            hints.AddForbiddenZone(new SDCircle(spread.Origin, FinalRadius), finalActivation);
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.Venom || spell.EventHappened)
            return;

        var activation = Module.CastFinishAt(spell, (float)FirstDelayAfterVenom);
        _spreads.RemoveAll(spread => spread.ActorID == caster.InstanceID);
        _spreads.Add(new(caster.InstanceID, caster.Position, activation));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != (uint)AID.VenomSpread)
            return;

        var spread = _spreads.FirstOrDefault(entry => entry.ActorID == caster.InstanceID);
        if (spread == null)
        {
            // Mid-mechanic activation/replay recovery: the event source is the actual circle center.
            spread = new(caster.InstanceID, caster.Position, WorldState.FutureTime(PulseInterval)) { Pulse = 1 };
            _spreads.Add(spread);
        }
        else if (++spread.Pulse >= PulseCount)
        {
            _spreads.Remove(spread);
            ++NumCasts;
            return;
        }
        else
        {
            spread.Activation = WorldState.FutureTime(PulseInterval);
        }
        ++NumCasts;
    }

    private void Prune()
    {
        var now = WorldState.CurrentTime;
        _spreads.RemoveAll(spread => now > spread.Activation.AddSeconds(2d));
    }
}

sealed class PoisonRain(BossModule module) : Components.RaidwideCasts(module, [(uint)AID.PoisonRainVisual]);

sealed class ManyMouthsToFeedStates : StateMachineBuilder
{
    public ManyMouthsToFeedStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<VenomBoundary>()
            .ActivateOnEnter<ManyMouthsAOEs>()
            .ActivateOnEnter<SideLashes>()
            .ActivateOnEnter<VenomPuddles>()
            .ActivateOnEnter<VenomSpread>()
            .ActivateOnEnter<PoisonRain>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(ManyMouthsToFeedStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement,
    GroupID = 1093u,
    NameID = 49u,
    SortOrder = 14)]
public sealed class ManyMouthsToFeed(WorldState ws, Actor primary) : BossModule(ws, primary, new(-870f, -560f), new ArenaBoundsCircle(30f))
{
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.BossClone));
    }
}
