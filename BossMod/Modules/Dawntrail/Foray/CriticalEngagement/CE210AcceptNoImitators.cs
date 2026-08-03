using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE210AcceptNoImitators;

public enum OID : uint
{
    Boss = 0x4C77, // R3.0, BNpcName 14801, morphing mage
    BoundaryController = 0x4DFD, // non-targetable controller at arena center
    Helper = 0x233C,
    DiveArrow = 0x1EC09B // EventObj type, hellward-bound dash direction indicator (spawns at 48343 cast start)
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
    ShapeshiftingSupercellDonutInner = 0xBCE9, // helper->self, 6.0s cast, range 10-20 donut
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
// covers the out-of-bounds zone the fence guards. The official Action sheet (0xBCEF, eff=10 donut,
// xAxis=30) agrees: outer kill ring at 30y, walkable circle 25y.
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
    // Inner radius 8f, not 10f: the 8-10y band is lethal too (replay-indirect; user-measured
    // 2026-08-02 - the old inner radius 10 marked it safe). Outer edge 16f stays so inner
    // (8-16) and outer (16-30) donuts tile the ring without overlap.
    private static readonly AOEShapeDonut SupercellInner = new(8f, 16f);
    private static readonly AOEShapeDonut SupercellOuter = new(16f, 30f);
    private static readonly AOEShapeCross CycloneCross = new(60f, 8f);

    // Several patterns expose the full sequence at once (notably the 2/4/6s breath cones).
    // Keep simultaneous casts dangerous, but later preview steps must not block AI movement yet.
    protected override double RiskyActivationWindow => 0.5d;

    // 2026-08-02 user request: the CycloneCrossing cross telegraph paints the whole screen deep
    // yellow whenever it grades imminent (0.5s window); pin it pale yellow (Colors.AOE) like the
    // Made Magic circles. Risky grading stays on the framework's window.
    protected override bool FixedColor(uint actionID, out uint color)
    {
        if (actionID == (uint)AID.CycloneCrossing)
        {
            color = Colors.AOE;
            return true;
        }
        return base.FixedColor(actionID, out color);
    }

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.TongueOfFlame => new(Tongue),
        (uint)AID.HellfireFetch => new(Hellfire, true),
        (uint)AID.HellishBreathShort or (uint)AID.HellishBreathMedium or (uint)AID.HellishBreathLong
            or (uint)AID.HellishBreathQuickCenter or (uint)AID.HellishBreathQuickLeft or (uint)AID.HellishBreathQuickRight => new(HellishBreath),
        (uint)AID.CyclonicRing => new(CyclonicRing),
        (uint)AID.ShapeshiftingSupercellConeLong or (uint)AID.ShapeshiftingSupercellConeShort => new(SupercellCone),
        (uint)AID.ShapeshiftingSupercellCircle or (uint)AID.ShapeshiftingSupercellExtraCircle => new(SupercellCircle),
        (uint)AID.ShapeshiftingSupercellDonutInner => new(SupercellInner),
        (uint)AID.ShapeshiftingSupercellDonutOuter => new(SupercellOuter),
        (uint)AID.CycloneCrossing => new(CycloneCross),
        _ => null
    };
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
    private static readonly AOEShapeCircle FinalSweep = new(MaxRadius); // upstream: pre-built AI hint shape
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

    // Reserve the complete sweep as soon as the first growth status arrives (upstream version).
    // Expanding this hint pulse-by-pulse makes automation walk a few yalms after every hit; the
    // final 17.5y footprint sends it to one of the four edge pockets in a single route.
    // ActiveAOEs still draws only the current real radius, so the visual timing remains faithful
    // to the mechanic.
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in _pending.Values)
            hints.AddForbiddenZone(FinalSweep, aoe.Origin);
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
        // 2026-08-02 user request: four full-screen 17.5y circles in deep yellow (Colors.Danger)
        // are harsh to look at; draw them pale yellow (Colors.AOE) instead.
        var shape = new AOEShapeCircle(MaxRadius);
        _pending[actor.InstanceID] = new(shape, actor.Position, color: Colors.AOE, risky: false,
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

// Hellward-bound dash path - arrow-chain reconstruction (2026-08-02): the boss reads 48343
// (HellwardBound, ~5.7s; location field $6 = first dash start), teleports there, then dashes
// three 10y-wide segments ~2.25s apart, each resolved by a 48344 (HellwardBoundHit) event
// (start = caster position, end = spell target $8). Four 0x1EC09B EventObj arrows spawn at
// 48343 CST+ and mark the WHOLE path as a chain (user-verified in-game + RFLG): each arrow's
// heading POINTS AT THE NEXT path point (angle convention: 0 deg = south, CCW positive, CW
// negative - BossMod's own). The chain starts at the arrow nearest the boss (its reading
// position) and is strung along each heading: the next arrow is the unused one with the
// smallest angle deviation from the heading (within 30 deg); with no arrow ahead the lane
// extends along the heading to the arena edge. Round 1 (replay): arrow3(502.8,-312.8) h135 ->
// arrow4(517.7,-327.7) -> arrow1(482.3,-327.7) h45 -> arrow2(517.7,-292.3) -> edge ~(480,-292) -
// 4 segments (the first is the repositioning dash, which has no 48344; the 48344 trio covers
// the last three). Round 2: arrow2(500,-314) h-180 -> edge; arrow1(500,-335) h-45 ->
// arrow4(475,-310) h90 -> arrow3(525,-310) -> edge. Each 48344 drops the OLDEST segment in
// order (immune to activation-estimate drift - the old time-based removal lagged a segment by
// one round); after the last 48344 the final segment (no 48344 of its own) expires one dash
// interval later, exactly when that dash resolves. 48344 refines per-segment only when the
// chain failed to build. Risk grading by activation window: segments resolving within ~2.5s are
// danger, the rest preview - but ALL segments are forbidden ground for the AI.
sealed class HellwardBoundDashes(BossModule module) : Components.GenericAOEs(module)
{
    private const float HalfWidth = 5f;
    private const double FirstDashDelay = 2.2d; // CST! -> first dash (replay-measured 2.2-2.23s)
    private const double DashInterval = 2.25d; // segment spacing (replay ~2.25s), staggers activations
    private const double DashLifetime = 0.5d; // fallback margin only - segments are dropped by 48344 in order (was 2.5s, left the last one ~3s extra)
    // Covers the whole 4-segment chain (~9s): during the telegraph (cast start to first dash,
    // ~8.1s) all four rectangles show the danger marker; AI forbidden zones already cover every
    // segment, activation weights still make nearer segments more urgent.
    private const double RiskWindow = 9d;
    private const float ArrowMatchAngle = 30f; // degrees: the next arrow must lie within this of the heading
    private readonly List<AOEInstance> _dashes = [];
    private readonly List<(WPos Position, Angle Rotation)> _arrows = [with(4)]; // 0x1EC09B dash-direction arrows
    private WPos _bossPos; // 48343 caster position = chain head reference
    private WPos _diveStartLoc; // 48343's location field = first dash start (fallback)
    private DateTime _firstDashAt; // estimated first dash resolution time
    private bool _chainAttempted; // chain build attempted once per round
    private bool _chainBuilt; // full chain drawn from arrows; false -> 48344 fallback
    private int _resolvedCount; // 48344s seen this round; drives the order-based segment drops

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        PruneExpired();
        TryBuildChain();
        var dashes = CollectionsMarshal.AsSpan(_dashes);
        var count = dashes.Length;
        // Risk by activation window: the segment resolving within ~2.5s (current + next dash) is
        // danger, farther ones are translucent preview.
        var riskyDeadline = WorldState.CurrentTime.AddSeconds(RiskWindow);
        for (var i = 0; i < count; ++i)
        {
            ref var aoe = ref dashes[i];
            if (aoe.Activation <= riskyDeadline)
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

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Every dash rectangle is forbidden ground for the AI - the whole path is lethal while
        // each dash resolves (including the translucent preview segments, not just the danger
        // window ones).
        foreach (var aoe in ActiveAOEs(slot, actor))
            hints.AddForbiddenZone(aoe.ShapeDistance ?? aoe.Shape.Distance(aoe.Origin, aoe.Rotation), aoe.Activation);
    }

    public override void Update()
    {
        TryBuildChain();
        PruneExpired();
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.HellwardBound || spell.EventHappened)
            return;
        // New round: reset everything, then build the chain from the freshly spawned arrows (they
        // arrive around the cast start, so TryBuildChain retries in Update until they are there).
        _bossPos = caster.Position;
        _diveStartLoc = spell.LocXZ;
        _firstDashAt = Module.CastFinishAt(spell).AddSeconds(FirstDashDelay);
        _dashes.Clear();
        _arrows.Clear();
        _chainAttempted = false;
        _chainBuilt = false;
        _resolvedCount = 0;
        TryBuildChain();
    }

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.DiveArrow)
            _arrows.Add((actor.Position, actor.Rotation)); // path point + heading to the next point
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != (uint)AID.HellwardBoundHit)
            return;
        // AIE+ clients replicate the hit without a target position; those events carry no segment.
        if (spell.TargetXZ == default)
            return;

        // Each 48344 resolves the CURRENT dash: drop the oldest (front) segment immediately -
        // order-based removal is immune to activation-estimate drift (the old "activation <= now"
        // test lagged a segment by one round when the firstDashAt estimate was late by ~0.25s).
        // Chain mode: 48344#1/#2/#3 drop chain segments 1/2/3; after the last one the remaining
        // segment (the final dash, which has no 48344 of its own) is scheduled to expire one dash
        // interval later - exactly when that dash resolves. Fallback mode: each 48344 also draws
        // its own zero-inference segment (the dropped one is the previous dash's).
        var now = WorldState.CurrentTime;
        if (_dashes.Count != 0)
            _dashes.RemoveAt(0);
        if (!_chainBuilt)
        {
            AddSegment(caster.Position, spell.TargetXZ, now);
        }
        else if (_resolvedCount >= 2 && _dashes.Count != 0)
        {
            // last 48344 seen: the remaining segment resolves one dash interval from now
            var last = _dashes[^1];
            _dashes[^1] = last with { Activation = now.AddSeconds(DashInterval) };
        }
        ++_resolvedCount;
    }

    private void TryBuildChain()
    {
        if (_chainAttempted)
            return;
        if (_arrows.Count < 2)
            return; // arrows spawn around the cast start; retried every update
        _chainAttempted = true;
        _chainBuilt = BuildChain();
        if (!_chainBuilt)
            AddSegment(_bossPos, _diveStartLoc, _firstDashAt); // cast-data first segment fallback
    }

    private bool BuildChain()
    {
        // Chain head: the arrow nearest the boss (its reading position). Then each arrow points
        // at the next path point: pick the unused arrow with the smallest angle deviation from
        // the heading (within 30 deg); with none, extend along the heading to the arena edge.
        var count = _arrows.Count;
        var head = -1;
        var bestDist = float.MaxValue;
        for (var i = 0; i < count; ++i)
        {
            var dist = (_arrows[i].Position - _bossPos).LengthSq();
            if (dist < bestDist)
            {
                head = i;
                bestDist = dist;
            }
        }
        if (head < 0)
            return false;

        var used = new bool[count];
        var current = head;
        var order = 0;
        var built = 0;
        while (current >= 0)
        {
            used[current] = true;
            var (pos, rot) = _arrows[current];
            var next = -1;
            var bestAngle = float.MaxValue;
            for (var j = 0; j < count; ++j)
            {
                if (used[j])
                    continue;
                var delta = _arrows[j].Position - pos;
                var len = delta.Length();
                if (len < 0.01f)
                    continue;
                var angleDiff = MathF.Abs((delta.ToAngle() - rot).Normalized().Deg);
                if (angleDiff <= ArrowMatchAngle && angleDiff < bestAngle)
                {
                    next = j;
                    bestAngle = angleDiff;
                }
            }
            if (next >= 0)
            {
                if (AddChainSegment(pos, _arrows[next].Position, order++))
                    ++built;
                current = next;
            }
            else
            {
                // no arrow ahead: extend along the heading to the arena edge
                var edge = ExtendToArenaEdge(pos, rot.ToDirection());
                if (AddChainSegment(pos, edge, order++))
                    ++built;
                current = -1;
            }
        }
        return built > 0;
    }

    private bool AddChainSegment(WPos start, WPos end, int order)
    {
        var dir = end - start;
        var length = dir.Length();
        if (length < 0.01f)
            return false;
        var shape = new AOEShapeRect(length, HalfWidth);
        var rotation = Angle.FromDirection(dir);
        _dashes.Add(new(shape, start, rotation, _firstDashAt.AddSeconds(order * DashInterval), Colors.AOE, false, default,
            shapeDistance: shape.Distance(start, rotation)));
        return true;
    }

    private WPos ExtendToArenaEdge(WPos start, WDir dir)
    {
        // march along the heading until leaving the arena (25y radius; cap well beyond)
        var pos = start;
        for (var i = 0; i < 100; ++i)
        {
            var next = pos + dir * 0.5f;
            if (!Arena.InBounds(next))
                break;
            pos = next;
        }
        return pos;
    }

    private void AddSegment(WPos start, WPos end, DateTime activation)
    {
        var dir = end - start;
        var length = dir.Length();
        if (length < 0.01f)
            return;
        var shape = new AOEShapeRect(length, HalfWidth);
        var rotation = Angle.FromDirection(dir);
        _dashes.Add(new(shape, start, rotation, activation, Colors.AOE, false, default,
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
public sealed class AcceptNoImitators(WorldState ws, Actor primary) : BossModule(ws, primary, new(500f, -310f), new ArenaBoundsCircle(25f))
{
    // NOTE: the upstream HellwardBound geometric-dash component (and its CalculateModuleAIHints
    // GoalProximity) was not kept - the CN build uses the local HellwardBoundDashes arrow-chain
    // component (0x1EC09B), which already forbids every dash lane via AddAIHints.
}
