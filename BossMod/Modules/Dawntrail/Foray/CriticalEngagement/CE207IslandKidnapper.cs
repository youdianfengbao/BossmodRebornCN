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
    private static readonly AOEShapeDonut Shape = new(19f, 30f);
    private readonly AOEInstance[] _aoe = [new(Shape, module.Arena.Center)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _aoe;
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
// The storm pairs orbit the arena on two rings: the R20 ring clockwise at 3.0y/s (~8.6 deg/s) and
// the R12 ring counterclockwise at 1.5y/s (~7.2 deg/s), both uniform circular motion. Enemy
// positions only refresh on the ~5s MOVE packets, so each storm's position is extrapolated from
// its registration time instead, and the baseline is re-anchored whenever a fresh packet drifts.
sealed class HurricaneHazards(BossModule module) : Components.GenericAOEs(module)
{
    private const float OuterRing = 20f;
    private const float InnerRing = 12f;
    private const float RingThreshold = 16f;
    private const float ContactRadius = 4.5f;
    private static readonly Angle TrackHalfAngle = 60f.Degrees(); // 120 degrees of track ahead of the storm
    private const float BaselineDriftSq = 9f; // 3y of extrapolation error before re-anchoring
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

    public bool TryGetMotion(ulong instanceID, out MotionInfo info) => _motion.TryGetValue(instanceID, out info);

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.Hurricane)
            Register(actor.InstanceID, actor.Position, WorldState.CurrentTime);
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.OID == (uint)OID.Hurricane)
        {
            _motion.Remove(actor.InstanceID);
            _lastBaseline.Remove(actor.InstanceID);
        }
    }

    private void Register(ulong id, WPos pos, DateTime time)
    {
        var ring = (pos - Module.Arena.Center).Length() > RingThreshold ? OuterRing : InnerRing;
        _motion[id] = new(pos, time, ring, ring >= RingThreshold ? 3f / OuterRing : 1.5f / InnerRing, ring >= RingThreshold ? 1f : -1f);
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
            // the storm body itself is the contact AOE
            _displayed.Add(new(Shape, hurricane.Position, color: Colors.Danger, actorID: hurricane.InstanceID,
                shapeDistance: Shape.Distance(hurricane.Position, default)));
            // the arc of track it is about to sweep through (pairs live 50-65s, so stale registrations are simply not drawn)
            if (_motion.TryGetValue(hurricane.InstanceID, out info) && now <= info.StartTime.AddSeconds(70d))
            {
                var angle = Angle.FromDirection(info.Predict(center, now) - center);
                var rot = angle + info.Sign * TrackHalfAngle;
                var track = info.RingRadius >= RingThreshold ? OuterTrack : InnerTrack;
                _displayed.Add(new(track, center, rot, color: Colors.Danger, actorID: hurricane.InstanceID,
                    shapeDistance: track.Distance(center, rot)));
            }
        }
        return CollectionsMarshal.AsSpan(_displayed);
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
// the registered circular motion. The cast itself is still drawn by KidnapperAOEs; the predicted
// entry is dropped as soon as the cast starts so the two never duplicate.
sealed class RendingWindTelegraphs(BossModule module) : Components.GenericAOEs(module)
{
    private const float PredictionTime = 4.1f;
    private const uint TelegraphIcon = 506u;
    private static readonly AOEShapeCross Cross = new(60f, 4f);
    private readonly List<(ulong ActorID, WPos Position, DateTime Activation)> _icons = [];
    private readonly List<AOEInstance> _displayed = [with(8)];

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID != TelegraphIcon || actor.OID != (uint)OID.Hurricane || Module.FindComponent<HurricaneHazards>() is not { } hazards
            || !hazards.TryGetMotion(actor.InstanceID, out var motion))
            return;
        // a fresh icon overrides any previous prediction for the same storm
        _icons.RemoveAll(e => e.ActorID == actor.InstanceID);
        _icons.Add((actor.InstanceID, motion.Predict(Module.Arena.Center, WorldState.FutureTime(PredictionTime)), WorldState.FutureTime(PredictionTime)));
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
        for (var i = _icons.Count - 1; i >= 0; --i)
        {
            var entry = _icons[i];
            if (now >= entry.Activation)
            {
                _icons.RemoveAt(i);
                continue;
            }
            // the two fixed rotations form the eight-way cross pattern
            var rot = (-180f).Degrees();
            _displayed.Add(new(Cross, entry.Position, rot, entry.Activation, color: Colors.Danger, actorID: entry.ActorID,
                shapeDistance: Cross.Distance(entry.Position, rot)));
            rot = (-135.005f).Degrees();
            _displayed.Add(new(Cross, entry.Position, rot, entry.Activation, color: Colors.Danger, actorID: entry.ActorID,
                shapeDistance: Cross.Distance(entry.Position, rot)));
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }
}

// The gust-wall event object (0x1EBFA9) spawns on the arena rim and plays its animation at the
// same moment as the breeze system-log message (11388), ~7.1s before the BC7A gust resolves - so
// it is already possible to tell which way the wind will blow while it is still a breeze. The wind
// always blows from the wall across the arena, so mark the whole downwind 3/4 of the floor as
// dangerous as soon as the wall appears, keeping the upwind quarter safe. If the wall is never
// observed (e.g. a log without EANM lines), fall back to the BC7A telegraph helper as the wind
// source, which still gives the full cast lead time. The zone disappears when the gust resolves.
sealed class WindWallHazards(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCone Downwind = new(60f, 135f.Degrees());
    private readonly List<AOEInstance> _displayed = [with(2)];
    private WPos _wallPos;

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.WindWall)
            _wallPos = actor.Position;
    }

    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (actor.OID == (uint)OID.WindWall)
            _wallPos = actor.Position;
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.IsSpell(AID.GustTelegraph) && _wallPos == default)
            _wallPos = caster.Position;
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.IsSpell(AID.GustTelegraph))
            _wallPos = default;
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _displayed.Clear();
        if (_wallPos != default)
        {
            // the wind blows from the wall across the arena (270-degree downwind cone)
            var dir = Angle.FromDirection(Module.Arena.Center - _wallPos);
            _displayed.Add(new(Downwind, _wallPos, dir, color: Colors.Danger, shapeDistance: Downwind.Distance(_wallPos, dir)));
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }
}

// BC7A is the cast-bar telegraph for B950, whose action effect is a 24y SourceForward knockback.
sealed class GustKnockback(BossModule module) : Components.SimpleKnockbacks(module, (uint)AID.GustTelegraph, 24f, shape: new AOEShapeRect(60f, 30f), kind: Kind.DirForward);
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
            .ActivateOnEnter<WindWallHazards>()
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
