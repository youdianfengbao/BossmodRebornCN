using BossMod.Dawntrail.Foray.CriticalEngagement;
using static BossMod.Components.GenericKnockback;

namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE207IslandKidnapper;

public enum OID : uint
{
    Boss = 0x4BE1, // R3.2, BNpcName 14505, kidnapper
    Hurricane = 0x4BE2,
    Emitter = 0x4BE3, // wind spirit, spawns on the R16 ring facing center, casts the B953 ice-flower circles
    Anchor = 0x4BE4, // non-targetable arena controller at center
    Helper = 0x233C,
    WindWall = 0x1EBFA9 // event object on the arena rim playing the gust-wall animation; appears at the same moment as the breeze system-log message (11388), ~7.1s before the BC7A gust resolves
}

public enum AID : uint
{
    IdleVisual = 0xB949, // boss->event target, no effects
    AutoAttack = 0xB94A, // boss->player, no cast, single-target
    WindBoundary = 0xB94B, // anchor, persistent 20-30y outer deathwall
    HurricaneVisual = 0xB94C,
    HurricaneKnockback = 0xB94D, // 5y directional knockback; the event rotation is the actual push direction, which sweeps ~10 deg/s
    RendingWindVisual = 0xB94E,
    RendingWind = 0xB94F, // range 60, 8y wide cross; two rotated crosses form the eight-way pattern
    GustHit = 0xB950, // raidwide damage and 24y forward knockback
    GaleBlade = 0xB951, // 60y 180-degree cone
    ScatterFeathers = 0xB952,
    WindBloom = 0xB953, // emitter self-centered 13y circle; four-six of them ring the arena and rotate wave to wave into a moving "ice flower", safe pocket near dead center
    DispersingGalesVisual = 0xB954,
    DispersingGales = 0xB955, // 60y 60-degree cone
    DownburstVisual = 0xB956,
    CycloneRingVisual = 0xB957,
    Downburst = 0xB958, // location, 15y circle
    CycloneRing = 0xB959, // 5-60y donut
    HurricaneHit = 0xBBF8, // helpers, no cast, raidwide damage
    GustTelegraph = 0xBC7A // helper, 60y long, 60y wide rect
}

sealed class WindBoundary(BossModule module) : Components.GenericAOEs(module)
{
    // The wall is lethal from 19y outward; draw it accurately for the human overlay but mark it
    // non-risky so the AI zone below can use a tighter inner radius.
    private static readonly AOEShapeDonut Visual = new(19f, 30f);
    // Give the AI a 2y buffer inside the true wall. The rotating WindBloom ice-flowers are 13y
    // circles emitted from the 16y ring, so the only safe pocket is near dead center; without a
    // buffer, squeezing away from a bloom can round the destination onto the 19y deathwall. Keeping
    // the AI at or inside 17y guarantees it never clips the wall while dodging blooms.
    private static readonly AOEShapeDonut Forbidden = new(17f, 30f);
    private readonly AOEInstance[] _aoe = [new(Visual, module.Arena.Center, risky: false)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _aoe;

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
        => hints.AddForbiddenZone(Forbidden, Module.Arena.Center);
}

sealed class KidnapperAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCone Half = new(60f, 90f.Degrees());
    private static readonly AOEShapeCone Cone = new(60f, 30f.Degrees());
    private static readonly AOEShapeCross Rending = new(60f, 4f);
    private static readonly AOEShapeCircle Downburst = new(15f);
    private static readonly AOEShapeCircle Bloom = new(13f);
    private static readonly AOEShapeDonut Ring = new(5f, 60f);

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.GaleBlade => new(Half),
        (uint)AID.DispersingGales => new(Cone),
        (uint)AID.RendingWind => new(Rending),
        (uint)AID.WindBloom => new(Bloom),
        (uint)AID.Downburst => new(Downburst, true),
        (uint)AID.CycloneRing => new(Ring),
        _ => null
    };
}

// GenericKnockback only renders displacement and does not add an AI forbidden zone. The moving
// hurricane body is itself the four-yalm contact AOE, so publish a slightly padded live hazard too.
// The storm pairs orbit the arena on two rings: the R20 ring at ~3.0y/s (~8.6 deg/s) and the R12
// ring at ~1.5y/s (~7.2 deg/s), both uniform circular motion - but which way (clockwise vs
// counterclockwise) each pair turns is chosen randomly per encounter, so it cannot be hardcoded.
// Enemy positions only refresh on the ~5s MOVE packets, so each storm's position is extrapolated
// from its registration time instead; the baseline is re-anchored whenever a fresh packet drifts,
// and the rotation direction is measured from the initial position + first MOVE displacement.
sealed class HurricaneHazards(BossModule module) : Components.GenericAOEs(module)
{
    private const float OuterRing = 20f;
    private const float InnerRing = 12f;
    private const float RingThreshold = 16f;
    private const float ContactRadius = 4.5f;
    private static readonly Angle TrackHalfAngle = 15f.Degrees(); // 30 degrees of track ahead of the storm (half-angle 15 deg)
    private const float BaselineDriftSq = 9f; // 3y of extrapolation error before re-anchoring
    private const float DetectMoveSq = 1f; // 1y of first MOVE displacement is enough to measure the turn direction
    private const double MinDirectionDt = 1d; // require at least 1s between registration and the first MOVE
    private static readonly AOEShapeCircle Shape = new(ContactRadius);
    private static readonly AOEShapeDonutSector OuterTrack = new(OuterRing - ContactRadius, OuterRing + ContactRadius, TrackHalfAngle);
    private static readonly AOEShapeDonutSector InnerTrack = new(InnerRing - ContactRadius, InnerRing + ContactRadius, TrackHalfAngle);
    private readonly List<AOEInstance> _displayed = [with(16)];

    public readonly record struct MotionInfo(WPos StartPos, DateTime StartTime, float RingRadius, float AngularSpeed, float Sign)
    {
        public readonly WPos Predict(WPos center, DateTime now)
        {
            var angle = Angle.FromDirection(StartPos - center) + Sign * AngularSpeed * (float)(now - StartTime).TotalSeconds * 1f.Radians();
            return center + RingRadius * angle.ToDirection();
        }
    }

    private readonly Dictionary<ulong, MotionInfo> _motion = [];
    private readonly Dictionary<ulong, DateTime> _lastBaseline = [];
    private readonly Dictionary<ulong, (WPos Pos, DateTime Time)> _initial = []; // registration position/time, used to measure the turn direction from the first MOVE
    private readonly HashSet<ulong> _directionKnown = []; // storms whose turn direction has been measured; Register keeps it instead of the default

    public bool TryGetMotion(ulong instanceID, out MotionInfo info) => _motion.TryGetValue(instanceID, out info);

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.Hurricane)
        {
            _initial[actor.InstanceID] = (actor.Position, WorldState.CurrentTime);
            _directionKnown.Remove(actor.InstanceID);
            Register(actor.InstanceID, actor.Position, WorldState.CurrentTime);
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.OID == (uint)OID.Hurricane)
        {
            _motion.Remove(actor.InstanceID);
            _lastBaseline.Remove(actor.InstanceID);
            _initial.Remove(actor.InstanceID);
            _directionKnown.Remove(actor.InstanceID);
        }
    }

    private void Register(ulong id, WPos pos, DateTime time)
    {
        var ring = (pos - Module.Arena.Center).Length() > RingThreshold ? OuterRing : InnerRing;
        // keep a measured direction once established; otherwise fall back to the default (outer
        // clockwise / inner counterclockwise) until the first MOVE allows measuring it for real
        var known = _directionKnown.Contains(id);
        var speed = known ? _motion[id].AngularSpeed : ring >= RingThreshold ? 3f / OuterRing : 1.5f / InnerRing;
        var sign = known ? _motion[id].Sign : ring >= RingThreshold ? 1f : -1f;
        _motion[id] = new(pos, time, ring, speed, sign);
        _lastBaseline[id] = time;
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _displayed.Clear();
        var center = Module.Arena.Center;
        var now = WorldState.CurrentTime;
        foreach (var hurricane in Module.Enemies((uint)OID.Hurricane))
        {
            if (hurricane.IsDeadOrDestroyed)
                continue;
            // re-anchor the extrapolation whenever a fresh MOVE packet disagrees with it
            if (_motion.TryGetValue(hurricane.InstanceID, out var info) && (hurricane.Position - info.Predict(center, now)).LengthSq() > BaselineDriftSq
                && now > _lastBaseline[hurricane.InstanceID].AddSeconds(2d))
            {
                Register(hurricane.InstanceID, hurricane.Position, now);
            }
            // once the first MOVE packet arrives, measure the actual turn direction from the
            // registration position + first displacement; the direction is random per encounter
            if (!_directionKnown.Contains(hurricane.InstanceID) && _initial.TryGetValue(hurricane.InstanceID, out var initial)
                && now > initial.Time.AddSeconds(MinDirectionDt) && (hurricane.Position - initial.Pos).LengthSq() > DetectMoveSq)
            {
                TryEstablishDirection(hurricane.InstanceID, initial, hurricane.Position, now);
            }
            // the storm body itself is the contact AOE
            _displayed.Add(new(Shape, hurricane.Position, actorID: hurricane.InstanceID,
                shapeDistance: Shape.Distance(hurricane.Position, default)));
            // the arc of track it is about to sweep through (pairs live 50-65s, so stale registrations are simply not drawn)
            if (_motion.TryGetValue(hurricane.InstanceID, out info) && now <= info.StartTime.AddSeconds(70d))
            {
                var angle = Angle.FromDirection(info.Predict(center, now) - center);
                var rot = angle + info.Sign * TrackHalfAngle;
                var track = info.RingRadius >= RingThreshold ? OuterTrack : InnerTrack;
                _displayed.Add(new(track, center, rot, actorID: hurricane.InstanceID,
                    shapeDistance: track.Distance(center, rot)));
            }
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    private void TryEstablishDirection(ulong id, (WPos Pos, DateTime Time) initial, WPos current, DateTime now)
    {
        if (!_motion.TryGetValue(id, out var info))
            return;
        var center = Module.Arena.Center;
        // signed angular displacement of the two points around the center; its sign is exactly the
        // Sign used by MotionInfo.Predict (positive = the positive Angle direction)
        var diff = (Angle.FromDirection(current - center) - Angle.FromDirection(initial.Pos - center)).Normalized();
        if (Math.Abs(diff.Rad) < 0.05f)
            return; // nearly radial first move (very unlikely): cannot measure a direction, keep the default
        var sign = diff.Rad > 0f ? 1f : -1f;
        var dt = (float)(now - initial.Time).TotalSeconds;
        var defaultSpeed = info.RingRadius >= RingThreshold ? 3f / OuterRing : 1.5f / InnerRing;
        var speed = dt >= 2f ? Math.Clamp(Math.Abs(diff.Rad) / dt, 0.05f, 0.5f) : defaultSpeed; // measured angular speed, clamped to sane bounds
        _motion[id] = new(initial.Pos, initial.Time, info.RingRadius, speed, sign);
        _directionKnown.Add(id);
        _lastBaseline[id] = now;
    }
}

// Hurricanes are persistent actors rather than cast bars. Their B94D contact event applies a
// five-yalm knockback inside the four-yalm hit area; the push direction is the tick event's own
// rotation (it sweeps ~10 deg/s instead of pointing away from the storm, which can differ by up
// to 160 degrees), so keep the live actor position but take the direction from the event.
sealed class HurricaneKnockbacks(BossModule module) : Components.GenericKnockback(module)
{
    // Contact is four yalms, but a warning only drawn inside the contact circle is invisible until
    // the player is already being knocked. Use a wider preview radius so the arrow appears as the
    // moving storm approaches; the separate HurricaneHazards circle still marks the lethal body.
    private static readonly AOEShapeCircle Shape = new(10f);
    private readonly List<Knockback> _displayed = [with(8)];
    private readonly Dictionary<ulong, Angle> _direction = [];

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster.OID == (uint)OID.Hurricane && spell.IsSpell(AID.HurricaneKnockback))
            _direction[caster.InstanceID] = spell.Rotation;
    }

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        _displayed.Clear();
        foreach (var hurricane in Module.Enemies((uint)OID.Hurricane))
        {
            if (hurricane.IsDeadOrDestroyed)
                continue;
            // fall back to the old away-from-origin estimate until the first tick is observed
            if (_direction.TryGetValue(hurricane.InstanceID, out var dir))
                _displayed.Add(new(hurricane.Position, 5f, WorldState.FutureTime(0.25d), Shape, dir, Kind.DirForward, actorID: hurricane.InstanceID));
            else
                _displayed.Add(new(hurricane.Position, 5f, WorldState.FutureTime(0.25d), Shape, default, Kind.AwayFromOrigin, actorID: hurricane.InstanceID));
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }
}

// ICON 506 on a hurricane telegraphs its RendingWind cross ~4.1s before the cast starts, and the
// storm keeps orbiting in between (up to 13y of travel), so the landing spot is extrapolated from
// the registered circular motion. The icon entry only records the deadline; every frame the landing
// spot is recomputed from HurricaneHazards' current motion data, so once the measured turn
// direction replaces the default (first MOVE), an already-drawn wrong prediction is redrawn
// automatically. The cast itself is still drawn by KidnapperAOEs; the predicted entry is dropped
// as soon as the cast starts so the two never duplicate.
sealed class RendingWindTelegraphs(BossModule module) : Components.GenericAOEs(module)
{
    private const float PredictionTime = 4.1f;
    private const uint TelegraphIcon = 506u;
    private static readonly AOEShapeCross Cross = new(60f, 4f);
    private readonly List<(ulong ActorID, DateTime Activation)> _icons = [];
    private readonly List<AOEInstance> _displayed = [with(8)];

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID != TelegraphIcon || actor.OID != (uint)OID.Hurricane)
            return;
        // a fresh icon overrides any previous prediction for the same storm
        _icons.RemoveAll(e => e.ActorID == actor.InstanceID);
        _icons.Add((actor.InstanceID, WorldState.FutureTime(PredictionTime)));
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        // the cast-bar AOE takes over from here, drop the predicted entry so it does not duplicate
        if (caster.OID == (uint)OID.Hurricane && spell.IsSpell(AID.RendingWind))
            _icons.RemoveAll(e => e.ActorID == caster.InstanceID);
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _displayed.Clear();
        var now = WorldState.CurrentTime;
        var hazards = Module.FindComponent<HurricaneHazards>();
        for (var i = _icons.Count - 1; i >= 0; --i)
        {
            var entry = _icons[i];
            if (now >= entry.Activation)
            {
                _icons.RemoveAt(i);
                continue;
            }
            // pull the current motion data every frame so a direction fix is picked up immediately
            if (hazards is not { } h || !h.TryGetMotion(entry.ActorID, out var motion))
                continue;
            var pos = motion.Predict(Module.Arena.Center, entry.Activation);
            // the two fixed rotations form the eight-way cross pattern
            var rot = (-180f).Degrees();
            _displayed.Add(new(Cross, pos, rot, entry.Activation, actorID: entry.ActorID,
                shapeDistance: Cross.Distance(pos, rot)));
            rot = (-135.005f).Degrees();
            _displayed.Add(new(Cross, pos, rot, entry.Activation, actorID: entry.ActorID,
                shapeDistance: Cross.Distance(pos, rot)));
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }
}

// The gust-wall event object (0x1EBFA9) spawns on the arena rim and plays its animation at the
// same moment as the breeze system-log message (11388), ~7.1s before the BC7A gust resolves - so
// it is already possible to tell which way the wind will blow while it is still a breeze. Instead
// of painting the whole downwind floor, show the very same knockback preview the cast-bar
// GustKnockback uses (60x30 rect toward the arena + push arrow, wind blowing from the wall across
// the arena) as soon as the wall appears. Once the BC7A telegraph starts, the existing
// GustKnockback takes over and this preview is dropped, so both never draw at the same time.
sealed class GustWallKnockbacks(BossModule module) : Components.GenericKnockback(module)
{
    private static readonly AOEShapeRect Shape = new(60f, 30f); // same rect as GustKnockback
    private readonly List<Knockback> _displayed = [with(1)];
    private WPos _wallPos;
    private bool _suppressed; // BC7A cast in progress - GustKnockback owns the preview

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.WindWall && !_suppressed)
            _wallPos = actor.Position;
    }

    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (actor.OID == (uint)OID.WindWall && !_suppressed)
            _wallPos = actor.Position;
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.OID == (uint)OID.WindWall)
            _wallPos = default;
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        // BC7A telegraph started: the cast-bar knockback preview takes over, drop ours
        if (spell.IsSpell(AID.GustTelegraph))
        {
            _suppressed = true;
            _wallPos = default;
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.IsSpell(AID.GustTelegraph))
            _suppressed = false;
    }

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        _displayed.Clear();
        if (_wallPos != default)
        {
            // the wind blows from the wall across the arena
            var dir = Angle.FromDirection(Module.Arena.Center - _wallPos);
            _displayed.Add(new(_wallPos, 24f, WorldState.FutureTime(7.1d), Shape, dir, Kind.DirForward));
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }
}

// BC7A is the cast-bar telegraph for B950, whose action effect is a 24y directional knockback.
// The gust comes from the wall on the main tank's side and flings everyone across the arena, so
// the helper's cast rotation already encodes the true push direction; the tank only decides which
// side the helper spawns on. Do not re-derive the direction from the tank's position - the helper
// rotation is authoritative (and stays valid even when the tank is mid-arena, which will carry the
// whole party out of bounds).
sealed class GustKnockback(BossModule module) : Components.GenericKnockback(module)
{
    private static readonly AOEShapeRect Shape = new(60f, 30f);
    private const float Distance = 24f;
    private const float SafeRadius = 19f;
    // Replay event timing is consistently about 0.60s after the helper cast finishes. Using the
    // old 1.05s estimate scheduled the safe-edge constraint roughly 0.4s after the real knockback.
    private const double HitDelay = 0.60d;
    private readonly List<Knockback> _casters = [with(2)];

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        PruneExpired();
        return CollectionsMarshal.AsSpan(_casters);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var kb in _casters)
            hints.AddForbiddenZone(new SDKnockbackInCircleFixedDirection(Arena.Center, Distance * kb.Direction.ToDirection(), SafeRadius), kb.Activation);
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        base.AddHints(slot, actor, hints);
        if (_casters.Count == 0)
            return;

        var tank = Module.PrimaryActor?.TargetID is ulong id && id != 0 ? WorldState.Actors.Find(id) : null;
        if (tank != null && !tank.IsDeadOrDestroyed && (tank.Position - Module.Arena.Center).Length() < 5f)
            hints.Add("主坦克站在中场——阵风会把全队推出边界！");
    }

    public override void Update() => PruneExpired();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.GustTelegraph || spell.EventHappened)
            return;

        _casters.RemoveAll(kb => kb.ActorID == caster.InstanceID);
        _casters.Add(new(spell.LocXZ, Distance, Module.CastFinishAt(spell, HitDelay), Shape, spell.Rotation, Kind.DirForward, actorID: caster.InstanceID));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != (uint)AID.GustHit)
            return;

        _casters.Clear();
        ++NumCasts;
    }

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        _casters.RemoveAll(kb => now > kb.Activation.AddSeconds(1d));
    }
}
// B94C resolves into the BBF8 helper raidwide about 0.9s after the boss cast. BC7A similarly
// resolves into B950 while applying the directional knockback.
sealed class KidnapperRaidwides(BossModule module) : Components.RaidwideCasts(module, [(uint)AID.HurricaneVisual, (uint)AID.GustTelegraph]);

sealed class IslandKidnapperStates : StateMachineBuilder
{
    public IslandKidnapperStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<WindBoundary>()
            .ActivateOnEnter<KidnapperAOEs>()
            .ActivateOnEnter<HurricaneHazards>()
            .ActivateOnEnter<HurricaneKnockbacks>()
            .ActivateOnEnter<RendingWindTelegraphs>()
            .ActivateOnEnter<GustWallKnockbacks>()
            .ActivateOnEnter<GustKnockback>()
            .ActivateOnEnter<KidnapperRaidwides>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(IslandKidnapperStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement,
    GroupID = 1093u,
    NameID = 61u,
    SortOrder = 6)]
public sealed class IslandKidnapper(WorldState ws, Actor primary) : BossModule(ws, primary, new(-150f, -860f), new ArenaBoundsCircle(20f));
