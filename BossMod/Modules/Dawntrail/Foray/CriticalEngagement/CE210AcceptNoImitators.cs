using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE210AcceptNoImitators;

public enum OID : uint
{
    Boss = 0x4C77, // R3.0, BNpcName 14801, morphing mage
    BoundaryController = 0x4DFD, // non-targetable controller at arena center
    Helper = 0x233C
}

public enum AID : uint
{
    AutoAttack = 0xBCCE,
    BlackenedRainVisual = 0xBCCF, // boss->self, 4.0s cast, raidwide visual
    BlackenedRain = 0xBCD0, // three helpers->players, 5.0s cast, raidwide damage
    DarkDealing = 0xBCD1, // boss->player, 5.0s cast, tankbuster
    ChangeFire = 0xBCD2, // boss->self, 4.0s cast, visual
    ChangeWind = 0xBCD3, // boss->self, 4.0s cast, visual
    Revert = 0xBCD4, // boss->self, no cast, visual
    TongueOfFlame = 0xBCD5, // boss->self, 4.0s cast, range 15 circle
    HellfireFetchVisual = 0xBCD6,
    HellwardBound = 0xBCD7, // boss->location, 6.0s cast, visual
    HellwardBoundHit = 0xBCD8,
    HellfireFetch = 0xBCD9, // helper->location, 7.0s cast, range 6 circle
    HellishBreathVisual = 0xBCDA, // boss->self, 6.0s cast, visual
    HellishBreathShort = 0xBCDB, // helper->self, 2.0s cast, range 60 60-degree cone
    HellishBreathMedium = 0xBCDC, // helper->self, 4.0s cast, range 60 60-degree cone
    HellishBreathLong = 0xBCDD, // helper->self, 6.0s cast, range 60 60-degree cone
    HellishBreathHit1 = 0xBCDE,
    HellishBreathHit2 = 0xBCDF,
    HellishBreathHit3 = 0xBCE0,
    HellishBreathHit4 = 0xBCE1,
    HellishBreathQuickCenter = 0xBE16, // helper, 1.1s cast, range 60 60-degree cone
    HellishBreathQuickLeft = 0xBE17, // helper, 1.1s cast, range 60 60-degree cone
    HellishBreathQuickRight = 0xC5F5, // helper, 1.1s cast, range 60 60-degree cone
    CyclonicRing = 0xBCE2, // boss->self, 4.0s cast, range 10-30 donut
    ShapeshiftingSupercellVisual1 = 0xBCE3, // boss->self, 5.5s cast, visual
    ShapeshiftingSupercellVisual2 = 0xBCE4, // boss->self, 5.5s cast, visual
    ShapeshiftingSupercellConeLong = 0xBCE5, // helper->self, 6.0s cast, range 60 90-degree cone
    ShapeshiftingSupercellResolve = 0xBCE6,
    ShapeshiftingSupercellConeShort = 0xBCE7, // helper->self, 1.5s cast, range 60 90-degree cone
    ShapeshiftingSupercellCircle = 0xBCE8, // helper->self, 6.0s cast, range 8 circle
    ShapeshiftingSupercellDonutInner = 0xBCE9, // helper->self, 6.0s cast, range 10-16 donut
    ShapeshiftingSupercellDonutOuter = 0xBCEA, // helper->self, 6.0s cast, range 16-30 donut
    ShapeshiftingSupercellExtraCircle = 0xC64F, // helper->self, 6.0s cast, range 8 circle
    MadeMagicVisual = 0xBCEB, // boss->self, 4.0s cast, visual
    MadeMagic = 0xBCEC, // helper pulses; radius is modified by status 1909
    CycloneCrossingVisual = 0xBCED, // boss->self, 10.5s cast, visual
    CycloneCrossing = 0xBCEE, // helper->self, 11.5s cast, range 60 width 16 cross
    LethalBoundary = 0xBCEF, // controller, persistent 20-30 donut
    UnknownTarget1 = 0xBCF0,
    UnknownTarget2 = 0xBCF1,
    UnknownLocation = 0xC620
}

public enum SID : uint
{
    AreaOfInfluenceUp = 1909 // Made Magic helper, extra 1-7; circle radius = extra * 2.5y
}

// The real arena is a 25y circle (player p99.9 radius 24.8, boundary hit at 24.6, charge targets at
// 25), so mark the persistent electric fence with a thin ring at the edge instead of a 20-30 donut.
// The outer radius extends to 30 (players have been killed as far out as 29y), so the warning also
// covers the out-of-bounds zone the fence guards.
sealed class LethalBoundary(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeDonut Shape = new(24.5f, 30f);
    private readonly AOEInstance[] _aoe = [new(Shape, module.Arena.Center)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _aoe;
}

// Every avoidable AOE below has an authoritative cast-start packet from the actor that owns the
// shape. The helpers also carry their actual origin/rotation, so none of the patterns are inferred
// from the boss transformation visuals.
sealed class MorphingMageAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCircle Tongue = new(15f);
    private static readonly AOEShapeCircle Hellfire = new(6f);
    private static readonly AOEShapeCone HellishBreath = new(60f, 30f.Degrees());
    private static readonly AOEShapeDonut CyclonicRing = new(10f, 30f);
    private static readonly AOEShapeCone SupercellCone = new(60f, 45f.Degrees());
    private static readonly AOEShapeCircle SupercellCircle = new(8f);
    private static readonly AOEShapeDonut SupercellInner = new(10f, 16f);
    private static readonly AOEShapeDonut SupercellOuter = new(16f, 30f);
    private static readonly AOEShapeCross CycloneCross = new(60f, 8f);

    // Several patterns expose the full sequence at once (notably the 2/4/6s breath cones).
    // Keep simultaneous casts dangerous, but later preview steps must not block AI movement yet.
    protected override double RiskyActivationWindow => 0.5d;

    // The 2/4/6s breath cones start on the same frame, so grade them by order instead of time:
    // only the first two are dangerous until the third becomes imminent on its own. The 1.1s
    // "quick" triple (center/left/right) is handled by HellishBreathQuickSequence instead: those
    // casts are pruned before they hit under the generic framework.
    protected override bool RiskyByOrder(uint actionID) => actionID is (uint)AID.HellishBreathShort or (uint)AID.HellishBreathMedium or (uint)AID.HellishBreathLong;
    protected override int RiskyCountByOrder => 2;

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.TongueOfFlame => new(Tongue),
        (uint)AID.HellfireFetch => new(Hellfire, true),
        (uint)AID.HellishBreathShort or (uint)AID.HellishBreathMedium or (uint)AID.HellishBreathLong => new(HellishBreath),
        (uint)AID.CyclonicRing => new(CyclonicRing),
        (uint)AID.ShapeshiftingSupercellConeLong or (uint)AID.ShapeshiftingSupercellConeShort => new(SupercellCone),
        (uint)AID.ShapeshiftingSupercellCircle or (uint)AID.ShapeshiftingSupercellExtraCircle => new(SupercellCircle),
        (uint)AID.ShapeshiftingSupercellDonutInner => new(SupercellInner),
        (uint)AID.ShapeshiftingSupercellDonutOuter => new(SupercellOuter),
        (uint)AID.CycloneCrossing => new(CycloneCross),
        _ => null
    };
}

// Hellish Breath "quick" triple (replay-verified 12_08_47.log, two rounds): the boss telegraphs
// the whole round with a long 0xBCDA cast (5.7s, CST+ previews the round, CST! hits), then a
// helper fires three 60y/60-degree cones with 0.8s casts ~2.1s apart - 0xBE16 center / 0xC5F5
// right / 0xBE17 left, directions fixed relative to the boss facing (center 0 deg, right +60 deg,
// left -60 deg). The ORDER IS NOT FIXED (round 1: center->right->left; round 2: left->right->center).
// Each breath resolves via its quick cast CST! (1.10s delay; the boss also emits 0xBCDE-0xBCE0 on
// the same tick). The 0.8s casts are so short that the generic cast-AOE framework prunes them
// ~0.77s before they actually hit, so this component records the first actual quick direction and
// predicts the two remaining fixed directions as pale placeholders, then refines each step as its
// own quick cast arrives (order-independent: refinement targets the first unconfirmed step).
sealed class HellishBreathQuickSequence(BossModule module) : Components.GenericAOEs(module)
{
    private sealed class BreathStep(Angle rotation, DateTime activation)
    {
        public uint ActionID; // 0 while a predicted placeholder
        public Angle Rotation = rotation;
        public DateTime Activation = activation;
        public ulong ActorID; // 0 = placeholder, set once its quick cast lands
    }

    private static readonly AOEShapeCone Shape = new(60f, 30f.Degrees()); // matches MorphingMageAOEs.HellishBreath
    private const double FollowupInterval = 2.1d; // replay-verified spacing between breaths
    private const double RiskWindow = 2.2d; // first + next steps dangerous, the last preview-only
    private const double ResolveTolerance = 1.5d; // CST! settles ~1.1s after CST+ (0.8s cast)
    private readonly List<BreathStep> _steps = [with(3)];
    private readonly List<AOEInstance> _displayed = [with(3)];
    private Angle _bossFacing;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        if (_steps.Count == 0)
            return CollectionsMarshal.AsSpan(_displayed);

        var deadline = _steps[0].Activation.AddSeconds(RiskWindow);
        foreach (var step in _steps)
        {
            var risky = step.Activation <= deadline;
            _displayed.Add(new(Shape, Module.Arena.Center, step.Rotation, step.Activation,
                risky ? Colors.Danger : Colors.AOE, risky, step.ActorID, Shape.Distance(Module.Arena.Center, step.Rotation)));
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => PruneExpired();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.HellishBreathVisual)
        {
            if (!spell.EventHappened)
            {
                _steps.Clear();
                _bossFacing = spell.Rotation; // caster is the boss
            }
            return;
        }

        if (spell.Action.ID is not ((uint)AID.HellishBreathQuickCenter) and not ((uint)AID.HellishBreathQuickLeft) and not ((uint)AID.HellishBreathQuickRight))
            return;

        if (spell.EventHappened)
        {
            // CST! settles this breath; remove the matching (or earliest unsettled) step
            var index = _steps.FindIndex(step => step.ActorID != 0 && step.ActionID == spell.Action.ID && step.Activation <= WorldState.FutureTime(ResolveTolerance));
            if (index < 0)
                index = _steps.FindIndex(step => step.ActorID != 0 && step.Activation <= WorldState.FutureTime(ResolveTolerance));
            if (index >= 0)
                _steps.RemoveAt(index);
            ++NumCasts;
            return;
        }

        if (_steps.Count == 0)
        {
            // First quick cast: publish the whole round - this actual direction first, then the
            // two remaining fixed directions (relative to boss facing) as unconfirmed placeholders.
            var firstOffset = (spell.Rotation - _bossFacing).Normalized();
            var firstActivation = Module.CastFinishAt(spell);
            _steps.Add(new(spell.Rotation, firstActivation) { ActionID = spell.Action.ID, ActorID = caster.InstanceID });
            var stepIndex = 1;
            foreach (var offset in new[] { -60f.Degrees(), 0f.Degrees(), 60f.Degrees() })
            {
                if (MathF.Abs((offset - firstOffset).Deg) < 1f)
                    continue;
                _steps.Add(new(_bossFacing + offset, firstActivation.AddSeconds(FollowupInterval * stepIndex++)));
            }
            return;
        }

        // Later quick cast: refine the first unconfirmed placeholder with actual direction/time.
        var refineIndex = _steps.FindIndex(step => step.ActorID == 0 && step.ActionID == spell.Action.ID);
        if (refineIndex < 0)
            refineIndex = _steps.FindIndex(step => step.ActorID == 0);
        if (refineIndex >= 0)
        {
            _steps[refineIndex].ActionID = spell.Action.ID;
            _steps[refineIndex].Rotation = spell.Rotation;
            _steps[refineIndex].Activation = Module.CastFinishAt(spell);
            _steps[refineIndex].ActorID = caster.InstanceID;
            _steps.Sort((left, right) => left.Activation.CompareTo(right.Activation));
        }
    }

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        _steps.RemoveAll(step => now > step.Activation.AddSeconds(1d));
    }
}

// Made Magic creates four fixed helpers 7.8y from center (cardinal on one cast, diagonal on the
// next). They pulse every ~0.6s while status 1909 grows from extra 1 through 7; each pulse is a
// thin expanding ring whose edge sits at extra*2.5y (verified in replay: hits only ever land on
// the 2.5y band at the current radius, never inside). The wave stops at extra 7 = 17.5y.
//
// Both the drawn shape and the AI hint are fixed 17.5y filled circles around every active helper
// (the previous expanding thin ring was hard to read and never disappeared until the next mechanic,
// so it is replaced with a stable solid warning). The union of four 17.5y circles still leaves the
// four arena-edge pockets that sit >20y from every helper (the exact spots survivors stand on),
// so the AI is parked in a pocket that is safe for the whole sequence and never has to cross a
// ring - guaranteeing it is never clipped by the poison.
// 2026-08-02 fix: Remove() keeps _extra (see Remove) - clearing it made Update()'s recovery loop
// resurrect swept rings forever (status-loss packets can be lost in replays and 1909 outlives the
// wave in live), which pinned the four 17.5y circles on the radar until the next mechanic.
sealed class MadeMagic(BossModule module) : Components.GenericAOEs(module)
{
    private const float MaxRadius = 17.5f; // extra 7 * 2.5
    private const double SequenceMaxDuration = 180d; // status 1909 lasts ~163s; a shorter window would clear the ring early
    private DateTime? _expireAt; // hard expiry for the pulse activation so rings cannot outlive the mechanic
    private readonly Dictionary<ulong, AOEInstance> _pending = [];
    private readonly Dictionary<ulong, int> _extra = [];
    private readonly List<AOEInstance> _displayed = new(4);
    private readonly HashSet<uint> _seenGlobalSequences = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _displayed.Clear();
        _displayed.AddRange(_pending.Values);
        return CollectionsMarshal.AsSpan(_displayed);
    }

    // Drive AI avoidance off fixed filled circles: forbid everything out to the 17.5y maximum
    // around every active helper, so the four edge pockets stay open.
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var (_, aoe) in _pending)
            hints.AddForbiddenZone(new AOEShapeCircle(MaxRadius), aoe.Origin);
    }

    public override void Update()
    {
        // Components can be activated after the helpers already received their growth status.
        // Recover that live state instead of waiting for a status-gain packet that will never repeat.
        var seenAlive = new HashSet<ulong>();
        foreach (var helper in Module.Enemies((uint)OID.Helper))
        {
            var status = helper.FindStatus((uint)SID.AreaOfInfluenceUp);
            if (status is { } current && current.Extra is >= 1 and <= 7)
            {
                seenAlive.Add(helper.InstanceID);
                if (!_extra.TryGetValue(helper.InstanceID, out var knownExtra) || knownExtra != current.Extra)
                {
                    SetRing(helper, current.Extra);
                }
            }
        }

        // Some replays lose the status-loss packet entirely (the helper simply stops carrying the
        // status). Fall back on the live status: any pending ring whose helper no longer has it is
        // gone and must be cleared instead of lingering until the next mechanic.
        foreach (var id in _pending.Keys.Where(id => !seenAlive.Contains(id)).ToArray())
            Remove(id);

        // The four helpers can persist after the mechanic without a status-loss packet (replays and
        // live packet loss both do this). If no pulse has refreshed a ring for a while, it is stale
        // and must not remain drawn until the next mechanic.
        var now = WorldState.CurrentTime;
        foreach (var key in _pending.Keys.Where(key => now > _pending[key].Activation.AddSeconds(1.5d)).ToArray())
            Remove(key);
    }

    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        if (actor.OID != (uint)OID.Helper || !actor.Position.InCircle(Arena.Center, 30f)
            || status.ID != (uint)SID.AreaOfInfluenceUp || status.Extra is < 1 or > 7)
            return;

        SetRing(actor, status.Extra);
    }

    private void SetRing(Actor actor, int extra)
    {
        // Fixed filled circle at the wave's maximum radius instead of per-step thin rings: the
        // previous expanding donut visually misled (never cleared until the next mechanic) and
        // offered no surf path. Visual only (risky: false); avoidance is handled by AddAIHints.
        var shape = new AOEShapeCircle(MaxRadius);
        _pending[actor.InstanceID] = new(shape, actor.Position, color: Colors.Danger, risky: false,
            activation: WorldState.FutureTime(0.3f), actorID: actor.InstanceID, shapeDistance: shape.Distance(actor.Position, default));
        _extra[actor.InstanceID] = extra;
        // Status 1909 lives ~163s (until the next mechanic); give the pulse activation a hard cap
        // well beyond that so a late refresh cannot extend the ring past the sequence.
        _expireAt = WorldState.CurrentTime.AddSeconds(SequenceMaxDuration);
    }

    public override void OnStatusLose(Actor actor, ref ActorStatus status)
    {
        if (status.ID == (uint)SID.AreaOfInfluenceUp)
            Remove(actor.InstanceID);
    }

    // the helper is truly gone - drop the extra memory too so a recycled instance id cannot revive it
    public override void OnActorDeath(Actor actor) => RemoveCompletely(actor.InstanceID);
    public override void OnActorDestroyed(Actor actor) => RemoveCompletely(actor.InstanceID);

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != (uint)AID.MadeMagic
            || spell.GlobalSequence != 0 && !_seenGlobalSequences.Add(spell.GlobalSequence)
            || !_pending.TryGetValue(caster.InstanceID, out var current))
            return;

        // Every status step normally pulses twice (extra 7 pulses three times). The first event is
        // therefore not the end of the ring; move the same warning to the next observed cadence.
        // A new status replaces its geometry, and status loss performs the final cleanup. The
        // refresh is capped at _expireAt so the ring cannot outlive the mechanic.
        var next = WorldState.FutureTime(0.58d);
        _pending[caster.InstanceID] = current with { Activation = (_expireAt is { } expire && next > expire) ? expire : next };
    }

    // Note: _extra is deliberately NOT cleared here. The Update() recovery loop calls SetRing when
    // a helper has no _extra entry, so clearing it together with _pending would let a stale status
    // (status-loss packet lost in replays, 1909 outliving the wave in live) resurrect the ring on
    // the very next frame - the clear/revive loop that kept the tornado warning on the radar
    // forever. Keeping _extra as the "last known extra" makes the recovery check short-circuit
    // (knownExtra == current.Extra), so a swept wave stays cleared until a real status change.
    private void Remove(ulong id)
    {
        _pending.Remove(id);
    }

    private void RemoveCompletely(ulong id)
    {
        _pending.Remove(id);
        _extra.Remove(id);
    }
}

// The three BCD0 helpers split one raidwide across the participant list. The boss cast is the
// stable, non-duplicated warning and starts one second before the helper cast bars.
sealed class BlackenedRain(BossModule module) : Components.RaidwideCast(module, (uint)AID.BlackenedRainVisual);
sealed class DarkDealing(BossModule module) : Components.SingleTargetDelayableCast(module, (uint)AID.DarkDealing);

// The 48343 HellwardBound cast telegraphs nothing useful: the boss teleports to the start of
// segment 1 while casting, so a charge rectangle drawn at cast start points the wrong way (the
// old HellwardBound ChargeAOEs and the old movement-tracking ChargeDashes both misled - the
// teleport registered as a dash segment). Instead consume each 48344 HellwardBoundHit event,
// which carries the authoritative dash segment: start = caster position, end = spell target,
// rect half-width 5 (10y total width). The three segments resolve in order ~2.2s apart.
sealed class HellwardBoundDashes(BossModule module) : Components.GenericAOEs(module)
{
    private const float HalfWidth = 5f;
    private const double DashLifetime = 8d; // covers the whole 3-segment sequence (~5.3s) plus margin
    private readonly List<AOEInstance> _dashes = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        PruneExpired();
        var dashes = CollectionsMarshal.AsSpan(_dashes);
        var count = dashes.Length;
        // Segments resolve in order; grade by sequence index: only the last two are the current
        // and next hit, earlier ones already resolved and are dimmed for context.
        for (var i = 0; i < count; ++i)
        {
            ref var aoe = ref dashes[i];
            if (i >= count - 2)
            {
                aoe.Color = Colors.Danger;
                aoe.Risky = true;
            }
            else
            {
                aoe.Color = Colors.AOE;
                aoe.Risky = false;
            }
        }
        return dashes;
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != (uint)AID.HellwardBoundHit)
            return;
        // AIE+ clients replicate the hit without a target position; those events carry no segment.
        if (spell.TargetXZ == default)
            return;

        var start = caster.Position;
        var dir = spell.TargetXZ - start;
        var length = dir.Length();
        if (length < 0.01f)
            return;
        var shape = new AOEShapeRect(length, HalfWidth);
        var rotation = Angle.FromDirection(dir);
        _dashes.Add(new(shape, start, rotation, WorldState.CurrentTime, Colors.AOE, false, caster.InstanceID,
            shapeDistance: shape.Distance(start, rotation)));
    }

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        _dashes.RemoveAll(entry => now > entry.Activation.AddSeconds(DashLifetime));
    }
}

sealed class AcceptNoImitatorsStates : StateMachineBuilder
{
    public AcceptNoImitatorsStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<LethalBoundary>()
            .ActivateOnEnter<MorphingMageAOEs>()
            .ActivateOnEnter<HellishBreathQuickSequence>()
            .ActivateOnEnter<MadeMagic>()
            .ActivateOnEnter<BlackenedRain>()
            .ActivateOnEnter<DarkDealing>()
            .ActivateOnEnter<HellwardBoundDashes>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(AcceptNoImitatorsStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement,
    GroupID = 1093u,
    NameID = 63u,
    SortOrder = 9)]
// Replay-verified 25y circular arena (players reach r24.8, the charge ends at r25 and the fence
// kills at 24.6); the old 20y circle clipped the charge and misplaced the boundary drawing.
public sealed class AcceptNoImitators(WorldState ws, Actor primary) : BossModule(ws, primary, new(500f, -310f), new ArenaBoundsCircle(25f));
