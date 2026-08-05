using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE212CursedResurgence;

public enum OID : uint
{
    Boss = 0x4C46, // R5.0, BNpcName 14787, red dragon
    ZombieGas = 0x4C47, // R1.5, persistent Necrohaze source
    MagicBarrier = 0x4C48, // R7.0, non-targetable intermission object
    Clone = 0x4D25, // R1.0, non-targetable animation controller
    Helper = 0x233C
}

public enum AID : uint
{
    BreathInThreesFast = 0xBC78, // boss->self, 2.5s cast, range 60 120-degree cone
    AutoAttack = 0xBC83, // boss->player, no cast, single-target
    SnakingNecrobreath = 0xBC84, // boss->self, 6.0s cast, range 60 270-degree cone
    GraveMoldVisual = 0xBC85, // boss->self, 5.0s cast, visual
    GraveMold = 0xBC86, // helpers->self, 6.0s cast, range 8 circle
    NecrohazeGas = 0xBC87, // zombie gas->self, no cast, range 5 persistent damage
    CauterizeVisual = 0xBC88, // boss->self, 6.0s cast, visual
    Cauterize = 0xBC89, // helper->self, 7.0s cast, range 40 width 10 rect
    CauterizeEnd = 0xBC8A, // boss->self, no cast, model-state reset
    Catching = 0xBC8B, // zombie gas->self, no cast, range 30 width 10 rect (never casted in replays -> not previewable, architecture limitation)
    NecrohazeSweep = 0xBC8C, // moving helpers->location, repeated range 5 circles
    NecrohazeCenter = 0xBC8D, // center helper->self, repeated range 5 circle
    BreathInThreesLong = 0xBC8E, // boss->self, 5.0s cast, range 60 120-degree cone
    AetherialWard = 0xBC8F, // boss->self, 4.0s cast, intermission visual
    MortalStormVisual = 0xBC90, // boss->self, 4.0s cast, raidwide visual
    MortalStormCast = 0xBC91, // helpers->self, 4.5s cast, range 60 raidwide
    MortalStormHit = 0xBC92, // helpers->players, no cast, range 60 raidwide damage
    AetherialWardActivate = 0xBC93, // boss->self, no cast, ward/model activation
    AetherialWardDeactivate = 0xBC94, // boss->self, no cast, ward/model deactivation
    HowlingDarknessVisual = 0xBC95, // boss->self, 5.0s cast, raidwide visual
    HowlingDarknessHit = 0xBC96, // helpers->players, no cast, range 60 raidwide damage
    ClonePulse = 0xBC97, // clone->self, no cast, animation/controller pulse
    CauterizeModelTransition = 0xBCAE, // boss->self, no cast, model-state transition
    NecrohazeCast = 0xC534, // helper->self, 4.0s cast, range 5 circle
    Soar = 0xC538 // boss->self, 4.0s cast, movement visual
}

public enum SID : uint
{
    DirectionalImmunity = 1125 // MagicBarrier: three immune sides; Extra is always 0, so the safe side has to be learned from action effects
}

// All casted avoidable attacks expose the actor that owns the real shape. In particular, the
// Grave Mold helpers are already placed at the eventual gas locations, while Cauterize's helper
// carries the actual lane origin and rotation independently of the boss visual.
sealed class CursedResurgenceAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCone Breath = new(60f, 60f.Degrees());
    private static readonly AOEShapeCone SnakingBreath = new(60f, 135f.Degrees());
    private static readonly AOEShapeCircle GraveMold = new(8f);
    private static readonly AOEShapeRect Cauterize = new(40f, 5f);
    private static readonly AOEShapeCircle Necrohaze = new(5f);

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.BreathInThreesFast or (uint)AID.BreathInThreesLong => new(Breath),
        (uint)AID.SnakingNecrobreath => new(SnakingBreath),
        (uint)AID.GraveMold => new(GraveMold),
        (uint)AID.Cauterize => new(Cauterize),
        (uint)AID.NecrohazeCast => new(Necrohaze),
        _ => null
    };
}

// Cauterize (BC89) crosses the entire square and activates every gas actor touched by its 10y-wide
// lane. The activated gas fires BC8B about one second after the dive, without its own cast start;
// its live rotation already points along the resulting 30x10 line.
sealed class CauterizedNecrohaze(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeRect CauterizeLane = new(40f, 5.5f);
    private static readonly AOEShapeRect Catching = new(30f, 5f);
    private readonly List<AOEInstance> _displayed = [with(10)];
    private readonly HashSet<ulong> _resolved = [];
    private WPos _laneOrigin;
    private Angle _laneRotation;
    private DateTime _activation;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _displayed.Clear();
        if (_activation == default)
            return CollectionsMarshal.AsSpan(_displayed);

        foreach (var gas in Module.Enemies((uint)OID.ZombieGas))
        {
            if (!gas.IsDeadOrDestroyed && !_resolved.Contains(gas.InstanceID) && CauterizeLane.Check(gas.Position, _laneOrigin, _laneRotation))
                _displayed.Add(new(Catching, gas.Position, gas.Rotation, _activation, Colors.Danger, true,
                    gas.InstanceID, Catching.Distance(gas.Position, gas.Rotation)));
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update()
    {
        if (_activation != default && WorldState.CurrentTime > _activation.AddSeconds(1d))
        {
            _activation = default;
            _resolved.Clear();
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.Cauterize)
        {
            _laneOrigin = caster.Position;
            _laneRotation = spell.Rotation;
            _activation = Module.CastFinishAt(spell, 1d);
            _resolved.Clear();
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.Catching)
        {
            _resolved.Add(caster.InstanceID);
            ++NumCasts;
        }
    }
}

// Grave Mold turns its resolved locations into long-lived actors. Their lifetime in the replay
// varies with the pattern (roughly 32-70 seconds), so actor presence is more reliable than a timer.
sealed class ZombieGas(BossModule module) : Components.Voidzone(module, 5f,
    static module => module.Enemies((uint)OID.ZombieGas).Where(actor => !actor.IsDeadOrDestroyed));

// Frost dive knockback poison rectangles (replay-verified 13_42_39.log): the boss reads 0xBC88
// (CauterizeVisual, ~5.7s, 3 casts) while standing on the dive line z=140; on CST! the knocked
// zombie-gas orbs (0x4C47, the persistent 48263 grid entities, ~10y grid spacing, ~5y radius)
// each spread 0xBC8B (Catching) ~1.8s later as a one-shot judgement. Note 0xBC8B has NO cast bar
// (only the CST! resolution arrives), so no per-orb confirmation is possible. Every knocked orb
// carves a 50y-long, ~11y-wide rectangle ALONG THE Z AXIS (rotation 0 = +Z) from its dive position
// to outside the arena: orbs north of the dive line (z < 140) up toward z-50, orbs south (z > 140)
// down toward z+50. Only orbs within 10y of the dive line get a lane (replay: knocked orbs sit at
// line ±5y, never-knocked ones at ±15y/±25y), so the snapshot at the dive cast start never draws
// fake lanes for orbs that were not knocked. On the 0xBC8B resolution everything clears;
// PruneExpired is the fallback. Displayed translucent (Colors.AOE, non-risky): pure warning - the
// AI does not dodge.
sealed class DiveKnockbackToxins(BossModule module) : Components.GenericAOEs(module)
{
    private const double ResolveDelay = 2.1d; // Catching judgement lands ~2.1s after the dive's CST! (replay ~1.8s)
    private const double ExpireAfterResolve = 1d; // keep the rectangles briefly after the judgement
    private const float KnockbackBand = 10f; // orbs within this z-band of the dive line are knocked (replay: ±5 knocked, ±15/±25 not)
    private static readonly AOEShapeRect Shape = new(25f, 5.5f, 25f); // 50y long (25+25), ~11y wide; length runs along z
    private readonly List<AOEInstance> _rects = [];
    private readonly List<AOEInstance> _displayed = [with(8)];
    private readonly HashSet<uint> _seenGlobalSequences = [];
    private float _diveLineZ; // boss's z while casting 0xBC88 (stays at 140)
    private DateTime _activation;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        _displayed.AddRange(_rects);
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.CauterizeVisual || spell.EventHappened)
            return;

        // Snapshot the gas orbs near the dive line before the knockback moves them; orbs farther
        // than the band are never knocked and must not get a (fake) lane.
        _diveLineZ = caster.Position.Z; // boss stays at z=140 for the whole dive
        _activation = Module.CastFinishAt(spell).AddSeconds(ResolveDelay);
        _rects.Clear();
        foreach (var gas in Module.Enemies((uint)OID.ZombieGas))
        {
            if (gas.IsDeadOrDestroyed || MathF.Abs(gas.Position.Z - _diveLineZ) > KnockbackBand)
                continue;
            AddRect(gas.Position);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != (uint)AID.Catching
            || spell.GlobalSequence != 0 && !_seenGlobalSequences.Add(spell.GlobalSequence))
            return;
        _rects.Clear();
        ++NumCasts;
    }

    private void AddRect(WPos orb)
    {
        // Lane runs along z (rotation 0 = +Z): north orbs (z < dive line) up toward z-50, south
        // orbs down toward z+50, centered 25y toward the arena edge + 25y back.
        var off = orb.Z < _diveLineZ ? -25f : 25f; // north of the line -> up (z-), south -> down (z+)
        var origin = orb + new WDir(0f, off);
        _rects.Add(new(Shape, origin, default, _activation, color: Colors.AOE,
            shapeDistance: Shape.Distance(origin, default)));
    }

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        _rects.RemoveAll(rect => now > rect.Activation.AddSeconds(ExpireAfterResolve));
    }
}

// During Aetherial Ward, six helpers move continuously and emit BC8C every ~0.58s; the center
// helper emits BC8D on the same cadence. Keep each helper dangerous until the next expected pulse
// and use its live position, rather than freezing hundreds of already-resolved event circles.
sealed class MovingNecrohaze(BossModule module) : Components.GenericAOEs(module)
{
    private readonly record struct EventKey(uint Sequence, uint ActionID, ulong ActorID);
    private static readonly AOEShapeCircle Shape = new(5.5f);
    private const double PulseLifetime = 0.9d;
    private readonly Dictionary<ulong, DateTime> _active = [];
    private readonly List<AOEInstance> _displayed = [with(8)];
    private readonly HashSet<EventKey> _seenEvents = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        foreach (var (actorID, _) in _active)
        {
            if (WorldState.Actors.Find(actorID) is { IsDeadOrDestroyed: false } source)
            {
                _displayed.Add(new(Shape, source.Position, activation: WorldState.CurrentTime,
                    actorID: actorID, shapeDistance: Shape.Distance(source.Position, default)));
            }
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => PruneExpired();

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.AetherialWardDeactivate)
        {
            _active.Clear();
            return;
        }

        if (spell.Action.ID is not ((uint)AID.NecrohazeSweep) and not ((uint)AID.NecrohazeCenter)
            || spell.GlobalSequence != 0 && !_seenEvents.Add(new(spell.GlobalSequence, spell.Action.ID, caster.InstanceID)))
        {
            return;
        }

        _active[caster.InstanceID] = WorldState.FutureTime(PulseLifetime);
        ++NumCasts;
    }

    public override void OnActorDeath(Actor actor) => _active.Remove(actor.InstanceID);
    public override void OnActorDestroyed(Actor actor) => _active.Remove(actor.InstanceID);

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        foreach (var actorID in _active.Where(entry => now > entry.Value).Select(entry => entry.Key).ToArray())
        {
            _active.Remove(actorID);
        }
    }
}

// Square arena (half-side 20): the persistent deathwall hugs the four edges, so warn with four
// edge bands 10y thick (20..30 from center, matching the old circular donut) instead of a 20-30
// circle, which would wrongly flag the square's interior corners as dead.
// The rect length runs along its rotation axis (Angle 0 = +Z here): north/south bands (origin
// z=±25) must run along X (rotation 90), east/west bands (origin x=±25) along Z (rotation 0) -
// with the rotations swapped the four bands crossed the arena center as a cross (user-verified).
sealed class NecrohazeBoundary(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeRect Shape = new(30f, 5f, 30f); // full edge plus corner overlap
    private readonly AOEInstance[] _aoe =
    [
        new(Shape, module.Arena.Center + new WDir(0f, 25f), 90f.Degrees()), // south edge band: z 20..30, x -30..30
        new(Shape, module.Arena.Center + new WDir(0f, -25f), 90f.Degrees()), // north edge band: z -30..-20
        new(Shape, module.Arena.Center + new WDir(25f, 0f), default), // east edge band: x 20..30, z -30..30
        new(Shape, module.Arena.Center + new WDir(-25f, 0f), default) // west edge band: x -30..-20
    ];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _aoe;
}

sealed class MagicBarrierDirectionalParry(BossModule module) : Components.DirectionalParry(module,
    [(uint)OID.MagicBarrier], forbiddenPriority: AIHints.Enemy.PriorityInvincible)
{
    private sealed class Evidence(DateTime startedAt)
    {
        public readonly DateTime StartedAt = startedAt;
        public readonly HashSet<(ulong SourceID, uint GlobalSequence)> SeenSequences = [];
        public Side InvulnerableSides;
        public Side SafeSide;
    }

    private readonly Dictionary<ulong, Evidence> _evidence = [];

    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        if (actor.OID == (uint)OID.MagicBarrier && status.ID == (uint)SID.DirectionalImmunity)
        {
            UpdateState(actor.InstanceID, (int)Side.All);
            _evidence[actor.InstanceID] = new(WorldState.CurrentTime);
        }
        else
            base.OnStatusGain(actor, ref status);
    }

    public override void OnStatusLose(Actor actor, ref ActorStatus status)
    {
        if (actor.OID == (uint)OID.MagicBarrier && status.ID == (uint)SID.DirectionalImmunity)
        {
            UpdateState(actor.InstanceID, ActorState(actor.InstanceID) & ~0xF);
            _evidence.Remove(actor.InstanceID);
        }
        else
            base.OnStatusLose(actor, ref status);
    }

    public override void OnActorDeath(Actor actor) => Reset(actor.InstanceID);
    public override void OnActorDestroyed(Actor actor) => Reset(actor.InstanceID);

    // BossModule intentionally filters player actions from component OnEventCast callbacks. The
    // module-level subscription forwards them here so the safe side can be inferred from the same
    // damage/invulnerability results that the game uses for the barrier.
    public void OnPlayerCastEvent(Actor source, ActorCastEvent spell)
    {
        if (source.Type is not ActorType.Player and not ActorType.Pet and not ActorType.Chocobo and not ActorType.Buddy || !spell.IsSpell())
            return;

        foreach (var target in spell.Targets)
        {
            if (!_evidence.TryGetValue(target.ID, out var evidence) || evidence.SafeSide != Side.None
                || WorldState.CurrentTime <= evidence.StartedAt
                || spell.GlobalSequence != 0 && !evidence.SeenSequences.Add((source.InstanceID, spell.GlobalSequence)))
            {
                continue;
            }

            var barrier = WorldState.Actors.Find(target.ID);
            if (barrier == null || barrier.IsDeadOrDestroyed)
                continue;

            var successfulDamage = false;
            var invulnerable = false;
            foreach (ref readonly var effect in target.Effects.ValidEffects())
            {
                successfulDamage |= effect.Type is ActionEffectType.Damage or ActionEffectType.BlockedDamage or ActionEffectType.ParriedDamage
                    && !effect.AtSource && effect.DamageHealValue > 0 && (effect.Param4 & 0x10) == 0;
                invulnerable |= effect.Type is ActionEffectType.Invulnerable or ActionEffectType.PartialInvulnerable;
            }

            if (!successfulDamage && !invulnerable)
                continue;

            var side = SideAt(source.Position, barrier);
            if (side == Side.None)
                continue;

            if (successfulDamage)
            {
                // A single positive action effect is conclusive: ID 1125 makes exactly three
                // sides immune, so the side that dealt damage is the sole opening. Waiting for a
                // second hit keeps automation idle for an unnecessary extra GCD and can leave it
                // attacking from stale geometry after the barrier rotates to the next pattern.
                LockSafeSide(target.ID, evidence, side);
            }
            else
            {
                evidence.InvulnerableSides |= side;
                var remaining = Side.All & ~evidence.InvulnerableSides;
                if (IsSingleSide(remaining))
                    LockSafeSide(target.ID, evidence, remaining);
            }
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var barrier in Module.Enemies((uint)OID.MagicBarrier))
        {
            if (barrier.IsDeadOrDestroyed || !ActorStates.TryGetValue(barrier.InstanceID, out var state))
                continue;

            var forbidden = (Side)(state & 0xF);
            if (forbidden == Side.All)
            {
                // Direction is still unknown. Stop attacking, but do not cover the arena with four
                // forbidden cones: that would make pathfinding oscillate before evidence arrives.
                hints.SetPriority(barrier, AIHints.Enemy.PriorityInvincible);
                continue;
            }

            var currentSide = SideAt(actor.Position, barrier);
            hints.SetPriority(barrier, (forbidden & currentSide) != 0 ? AIHints.Enemy.PriorityInvincible : 1);
            AddForbiddenSide(hints, barrier, forbidden, Side.Front, default);
            AddForbiddenSide(hints, barrier, forbidden, Side.Left, 90f.Degrees());
            AddForbiddenSide(hints, barrier, forbidden, Side.Back, 180f.Degrees());
            AddForbiddenSide(hints, barrier, forbidden, Side.Right, 270f.Degrees());

            // 引导 AI 到安全面攻击: 只设 Invincible 会让停在无敌面的 AI 发呆, 不绕到安全侧。
            var safe = (Side)(Side.All & ~forbidden);
            if (IsSingleSide(safe))
            {
                var facing = barrier.Rotation.ToDirection();
                var dir = safe switch
                {
                    Side.Front => facing,
                    Side.Back => -facing,
                    Side.Left => facing.OrthoL(),
                    Side.Right => facing.OrthoR(),
                    _ => default
                };
                if (dir != default)
                {
                    var goal = barrier.Position + dir * 8f;
                    hints.GoalZones.Add(AIHints.GoalSingleTarget(goal, 4f, 20f));
                }
            }
        }
    }

    private void LockSafeSide(ulong barrierID, Evidence evidence, Side safeSide)
    {
        evidence.SafeSide = safeSide;
        UpdateState(barrierID, (int)(Side.All & ~safeSide));
    }

    private void Reset(ulong actorID)
    {
        _evidence.Remove(actorID);
        UpdateState(actorID, 0);
    }

    private static void AddForbiddenSide(AIHints hints, Actor barrier, Side forbidden, Side side, Angle offset)
    {
        if ((forbidden & side) != 0)
            hints.AddForbiddenZone(new SDCone(barrier.Position, 100f, barrier.Rotation + offset, 45f.Degrees()), DateTime.MaxValue);
    }

    private static Side SideAt(WPos position, Actor barrier)
    {
        var offset = position - barrier.Position;
        if (offset.LengthSq() < 0.01f)
            return Side.None;

        var direction = offset.Normalized();
        var facing = barrier.Rotation.ToDirection();
        var forward = direction.Dot(facing);
        return forward > 0.7071067f ? Side.Front
            : forward < -0.7071067f ? Side.Back
            : direction.Dot(facing.OrthoL()) > 0f ? Side.Left : Side.Right;
    }

    private static bool IsSingleSide(Side side)
    {
        var bits = (int)side;
        return bits != 0 && (bits & bits - 1) == 0;
    }
}

// During Aetherial Ward the boss raises a reflecting magic barrier (the non-targetable MagicBarrier
// object). Any damage dealt to the boss while that barrier stands bounces straight back and wipes
// the automated party, so mark the boss un-attackable for as long as the barrier actor exists. This
// also frees the AI to keep dodging the moving Necrohaze "saw" circles instead of standing still to
// attack the warded boss. Keying off the barrier actor's presence self-resets when it despawns and
// fails safe (no barrier detected -> normal attacking).
sealed class AetherialWardBarrier(BossModule module) : Components.GenericAOEs(module)
{
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => [];

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var warded = false;
        foreach (var barrier in Module.Enemies((uint)OID.MagicBarrier))
        {
            if (!barrier.IsDeadOrDestroyed)
            {
                warded = true;
                break;
            }
        }
        if (!warded)
            return;

        var count = hints.PotentialTargets.Count;
        for (var i = 0; i < count; ++i)
        {
            var e = hints.PotentialTargets[i];
            if (e.Actor.OID == (uint)OID.Boss)
                e.Priority = AIHints.Enemy.PriorityInvincible;
        }
    }
}

// Damage is split between helpers; the boss visuals are the stable, non-duplicated warnings.
sealed class CursedResurgenceRaidwides(BossModule module) : Components.RaidwideCasts(module,
    [(uint)AID.MortalStormVisual, (uint)AID.HowlingDarknessVisual]);

sealed class CursedResurgenceStates : StateMachineBuilder
{
    public CursedResurgenceStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<CursedResurgenceAOEs>()
            .ActivateOnEnter<CauterizedNecrohaze>()
            .ActivateOnEnter<ZombieGas>()
            .ActivateOnEnter<DiveKnockbackToxins>()
            .ActivateOnEnter<MovingNecrohaze>()
            .ActivateOnEnter<NecrohazeBoundary>()
            .ActivateOnEnter<MagicBarrierDirectionalParry>()
            .ActivateOnEnter<AetherialWardBarrier>()
            .ActivateOnEnter<CursedResurgenceRaidwides>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(CursedResurgenceStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement,
    GroupID = 1093u,
    NameID = 53u,
    SortOrder = 11)]
// 2026-08-02 replay-verified: the arena is a 40y square, not a 20y-radius circle. A death at
// x=-708.279 vs center x=-688.000 gives a 20.28y half-side, and the boss's boundary-leap points
// (x=-708/-668) sit exactly 20.000y from center - both match the user's visual measurement. The
// old circle clipped the square's corners.
public sealed class CursedResurgence : BossModule
{
    private readonly EventSubscription _playerCastEvents;

    public CursedResurgence(WorldState ws, Actor primary) : base(ws, primary, new(-688f, 150f), new ArenaBoundsSquare(20f))
    {
        _playerCastEvents = ws.Actors.CastEvent.Subscribe(OnAnyCastEvent);
    }

    protected override void Dispose(bool disposing)
    {
        _playerCastEvents.Dispose();
        base.Dispose(disposing);
    }

    private void OnAnyCastEvent(Actor source, ActorCastEvent spell)
        => FindComponent<MagicBarrierDirectionalParry>()?.OnPlayerCastEvent(source, spell);
}
