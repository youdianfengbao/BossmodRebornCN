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
    WindBoundary = 0xB94B, // anchor, persistent outer deathwall; ARR player-center kills start at ~23y
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
    // The wall is lethal from ~24.5y outward (replay 10_55_27.log: farthest survivor 22.6y,
    // wall-kill deaths 24.9-26.2y, anchor B94B death-wall check every second); draw it accurately
    // for the human overlay but mark it non-risky so the AI zone below can use a tighter radius.
    // 2026-08-03: re-measured 24.5f inner radius (was 19f under the old 20y-arena assumption).
    private static readonly AOEShapeDonut Visual = new(24.5f, 30f);
    // Give the AI a 2y buffer inside the true wall. The rotating WindBloom ice-flowers are 13y
    // circles emitted from the 16y ring, so dodging a bloom can squeeze the AI outward toward the
    // wall; keeping it at or inside 22.5y guarantees it never clips the 24.5y deathwall while
    // dodging blooms. 2026-08-03: 22.5f from replay re-measure (was 17f for the old 19y wall).
    private static readonly AOEShapeDonut Forbidden = new(22.5f, 30f);
    // 2026-08-02 GaleBlade (0xB951): the boss teleports to the arena rim and sweeps a 180-degree
    // cone over most of the floor (user-verified); the only safe pocket is the rim BEHIND the boss
    // (~r18-24), which the flat 22.5-30y forbidden donut covers - so the AI sees no valid path and
    // flounders mid-arena. While the cast is live, replace the donut with two 140-degree sectors
    // whose union leaves an ~80-degree gap (gapCenter +/- 40deg) behind the boss. The 22.5y inner
    // radius stays everywhere else, preserving the WindBloom wall buffer; GaleBlade's own cone
    // forbidden zone blocks the arena inside, funneling the AI into the gap. A goal zone on the
    // gap (25 weight, above the other CE207 goals) drives the AI there explicitly - forbidden
    // zones are hard constraints the AI must leave, goals are what it heads toward.
    // 2026-08-03: gap band re-measured 18-24y (safe rim behind the boss reaches the 24.5y wall).
    private const float GaleInner = 18f;
    private const float GaleOuter = 24f;
    private const double GaleSafeWindow = 8d; // read is ~5-6s; keep the gap ~2s after it resolves
    private static readonly AOEShapeDonutSector GaleSector = new(GaleInner, GaleOuter, 140f.Degrees());
    private readonly AOEInstance[] _aoe = [new(Visual, module.Arena.Center, risky: false)];
    private bool _galeActive;
    private DateTime _galeUntil;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _aoe;

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_galeActive)
        {
            var gapCenter = Angle.FromDirection(Module.PrimaryActor.Position - Module.Arena.Center) + 180f.Degrees();
            hints.AddForbiddenZone(GaleSector, Module.Arena.Center, gapCenter + 90f.Degrees());
            hints.AddForbiddenZone(GaleSector, Module.Arena.Center, gapCenter - 90f.Degrees());
            // explicit goal in the gap: ring 18-24y behind the boss, within 45 degrees of the gap
            // center (the arena bounds still cap pathfinding at the 25y rim; the wall is 24.5y)
            hints.GoalZones.Add(position =>
            {
                var offset = position - Module.Arena.Center;
                var dist = offset.Length();
                var angleDiff = MathF.Abs((Angle.FromDirection(offset) - gapCenter).Normalized().Deg);
                return dist is >= 18f and <= 24f && angleDiff <= 45f ? 25f : 0f;
            });
        }
        else
        {
            hints.AddForbiddenZone(Forbidden, Module.Arena.Center);
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.GaleBlade && !spell.EventHappened)
        {
            _galeActive = true;
            _galeUntil = WorldState.CurrentTime.AddSeconds(GaleSafeWindow);
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.GaleBlade)
            _galeActive = false;
    }

    public override void Update()
    {
        if (_galeActive && WorldState.CurrentTime > _galeUntil)
            _galeActive = false;
    }

    // Upstream 7.5.5.22 addition absorbed (2026-08-05 user request): fill the band between the
    // kill-ring (23.5y) and the arena rim (25y, ArenaBoundsCircle radius) so the deathwall reads
    // as a solid ring on the radar instead of a thin outline; independent of the GaleBlade gap
    // logic above.
    public override void DrawArenaBackground(int pcSlot, Actor pc)
        => Arena.ZoneDonut(Arena.Center, 23.5f, 25f, Colors.Danger);
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
        (uint)AID.Downburst => new(Downburst, true),
        (uint)AID.CycloneRing => new(Ring),
        _ => null
    };
}

// 冰花: emitter 的 B953 cast 事件偶发缺失 (ARR 第 4 波无 cast), 依赖 cast 会漏画。
// 改为直接从存活 emitter 实时画 13y 圈, 不依赖 cast 事件。
sealed class WindBloomAOEs(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCircle Shape = new(13f);
    private readonly List<AOEInstance> _displayed = [with(8)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _displayed.Clear();
        foreach (var emitter in Module.Enemies((uint)OID.Emitter))
            if (!emitter.IsDeadOrDestroyed)
                _displayed.Add(new(Shape, emitter.Position, color: Colors.Danger, actorID: emitter.InstanceID,
                    shapeDistance: Shape.Distance(emitter.Position, default)));
        return CollectionsMarshal.AsSpan(_displayed);
    }
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
    // 2026-08-03: re-anchor threshold widened 3y -> 5y. The R20 ring storms move at ~3.0y/s, so
    // the per-second position packets step ~3y - exactly the old 3y threshold - which re-anchored
    // every packet and hard-cut StartPos/StartTime, making RendingWindTelegraphs' Predict(activation)
    // jump once per second (user-verified "blinking"). With 5y the per-second step never trips it;
    // Predict stays continuous frame-to-frame and re-anchoring only happens on real drift (every
    // 2-5s at most).
    private const float BaselineDriftSq = 25f; // 5y of extrapolation error before re-anchoring
    private const float DetectMoveSq = 1f; // 1y of first MOVE displacement is enough to measure the turn direction
    private const double MinDirectionDt = 1d; // require at least 1s between registration and the first MOVE
    private static readonly AOEShapeCircle Shape = new(ContactRadius);
    // Forward arc capsule anchored to the storm body's live position (2026-08-06 user request):
    // sweeps +/- 15 deg along the measured turn direction instead of the old center-anchored
    // donut-sector track, so the warning reads as a leading arc that never misaligns with the
    // body circle. AngularLength/OrbitCenter are ctor-only (rotation is ignored at draw time),
    // so each turn direction needs its own instance; the orbit center must come from the module
    // ctor parameter, because a field initializer runs before the base-class Module property.
    private readonly AOEShapeArcCapsule _fwdArc = new(ContactRadius, 15f.Degrees(), module.Arena.Center);
    private readonly AOEShapeArcCapsule _revArc = new(ContactRadius, -15f.Degrees(), module.Arena.Center);
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
            // forward arc capsule: the live body position is the arc start, swept +/- 15 deg along
            // the measured turn direction; same refresh rhythm as the body circle right above, so
            // the two move as one and never misalign (rotation is ignored by ArcCapsule)
            var arc = info.Sign >= 0f ? _fwdArc : _revArc;
            _displayed.Add(new(arc, hurricane.Position, default, actorID: hurricane.InstanceID,
                shapeDistance: arc.Distance(hurricane.Position, default)));
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

// ICON 506 on a hurricane telegraphs its RendingWind cross ~4.1s before the cast starts. The
// landing spot is the storm position at the effect moment, computed ONCE from a snapshot taken at
// the icon moment: icon position rotated around the arena center by (measured direction x the
// ring's fixed landing angle). Because the spot never depends on per-frame extrapolation, it can
// never jump as HurricaneHazards re-anchors. The cast itself is still drawn by KidnapperAOEs; the
// predicted entry is dropped as soon as the cast starts so the two never duplicate.
sealed class RendingWindTelegraphs(BossModule module) : Components.GenericAOEs(module)
{
    private const float PredictionTime = 4.1f;
    private const uint TelegraphIcon = 506u;
    // Fixed landing angles (degrees), replay-measured 2026-08-06 from 21 events / 4 encounter
    // instances (TextVerbose_2026_08_06_00_10_22.log + TextVerbose_2026_08_02_10_55_27.log):
    // ICON->effect total angular travel = 40.24 deg +/- 0.46 on the R20 ring and 34.74 +/- 0.63
    // on the R12 ring (arc error +/- 0.16y / +/- 0.13y). The values already include the cast-window
    // deceleration segment (2.48 deg on both rings), so the cross lands on the real effect spot.
    private const float OuterLandingAngle = 40.2f;
    private const float InnerLandingAngle = 34.7f;
    private const float RingThreshold = 16f;
    // half-width 4f + 0.2f margin covers the measured landing-spot error
    private static readonly AOEShapeCross Cross = new(60f, 4.2f);
    private readonly List<(ulong ActorID, DateTime Activation, WPos IconPos, float IconRadius, float Sign)> _icons = [];
    private readonly List<AOEInstance> _displayed = [with(8)];

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID != TelegraphIcon || actor.OID != (uint)OID.Hurricane)
            return;
        // a fresh icon overrides any previous prediction for the same storm
        _icons.RemoveAll(e => e.ActorID == actor.InstanceID);
        // snapshot: icon position, ring radius, and the measured turn direction (random per
        // encounter, so it must come from HurricaneHazards, never hardcoded)
        var center = Module.Arena.Center;
        var radius = (actor.Position - center).Length();
        var sign = Module.FindComponent<HurricaneHazards>()?.TryGetMotion(actor.InstanceID, out var motion) == true
            ? motion.Sign : radius >= RingThreshold ? 1f : -1f;
        _icons.Add((actor.InstanceID, WorldState.FutureTime(PredictionTime), actor.Position, radius, sign));
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
            // fixed landing spot: icon position rotated around the arena center by the measured
            // turn direction times the ring's constant landing angle (same angle convention as
            // HurricaneHazards.MotionInfo.Predict, so Sign semantics match by construction)
            var angle = Angle.FromDirection(entry.IconPos - Module.Arena.Center) + entry.Sign * (entry.IconRadius >= RingThreshold ? OuterLandingAngle : InnerLandingAngle).Degrees();
            var pos = Module.Arena.Center + entry.IconRadius * angle.ToDirection();
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
    // 2026-08-03 user request: the breeze preview must dodge like the real gust - same push and
    // wall-radius as GustKnockback (24y / 24.5y wall), direction = wall across the arena.
    private const float Distance = 24f;
    private const float SafeRadius = 24.5f;
    private readonly List<Knockback> _displayed = [with(1)];
    private WPos _wallPos;
    private bool _suppressed; // BC7A cast in progress - GustKnockback owns the preview

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_wallPos == default || _suppressed)
            return;
        var dir = Angle.FromDirection(Module.Arena.Center - _wallPos); // wind blows wall -> arena
        hints.AddForbiddenZone(new SDKnockbackInCircleFixedDirection(Arena.Center, Distance * dir.ToDirection(), SafeRadius),
            WorldState.FutureTime(7.1d)); // same activation as the preview knockback
    }

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
    // 2026-08-03 user request: 25.5y (+1.5y) gives the AI more landing tolerance past the
    // telegraph's nominal 24y push; the SDKnockbackInCircleFixedDirection forbidden zone in
    // AddAIHints consumes this constant, so display and AI stay in sync.
    private const float Distance = 25.5f;
    // Knockback landing radius that stays inside the death wall: the wall inner edge is 24.5y
    // (replay 10_55_27.log, re-measured 2026-08-03), so any landing point within 24.5y is safe.
    // 19f was the old 19y-wall value - keeping it would ban every landing beyond 19y and strand
    // the AI off the outer rim (landings up to 24.5y are actually safe now).
    private const float SafeRadius = 24.5f;
    // Upstream knockback-destination safety: DestinationUnsafe rejects landings beyond
    // LethalRadius (kept at the re-measured wall, 24.5f).
    private const float LethalRadius = 24.5f;
    // Upwind guidance (2026-08-06 user request): a big weak goal zone upwind of the arena center
    // so the AI walks upwind and lands mid-arena after the 24y push instead of idling in the safe
    // middle. All points inside (r 15.5-22.5 along the push direction, +/-8y across) land within
    // ~9.4y of center; weight 8 sits above CenterBias (5, suppressed during gusts) and below the
    // GaleBlade gap goal (25).
    private const float UpwindDistance = 19f;
    private const float UpwindHalfLength = 3.5f;
    private const float UpwindHalfWidth = 8f;
    private const float UpwindGoalWeight = 8f;
    private static readonly AOEShapeRect UpwindRect = new(UpwindHalfLength, UpwindHalfWidth, UpwindHalfLength); // center-symmetric
    // Landing on a hurricane is lethal: the storm body is a 4.5y contact AOE, add the player
    // half-width and a small margin -> 6y danger radius around each storm's predicted position.
    private const float HurricaneLandingRadius = 6f;
    // Replay event timing is consistently about 0.60s after the helper cast finishes. Using the
    // old 1.05s estimate scheduled the safe-edge constraint roughly 0.4s after the real knockback.
    private const double HitDelay = 0.60d;
    private readonly List<Knockback> _casters = [with(2)];
    // true while a gust telegraph is active - CenterBias checks this to suppress the center goal
    // during gusts so the upwind rect is the only weak goal the AI follows
    public bool AnyActive => _casters.Count > 0;

    // Each hurricane's position at the given time, extrapolated from its measured circular motion.
    private IEnumerable<WPos> HurricanePositionsAt(DateTime time)
    {
        var hazards = Module.FindComponent<HurricaneHazards>();
        if (hazards == null)
            yield break;
        foreach (var hurricane in Module.Enemies((uint)OID.Hurricane))
        {
            if (hurricane.IsDeadOrDestroyed || !hazards.TryGetMotion(hurricane.InstanceID, out var motion))
                continue; // unknown motion: skip rather than misjudge
            yield return motion.Predict(Arena.Center, time);
        }
    }

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        PruneExpired();
        return CollectionsMarshal.AsSpan(_casters);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var kb in _casters)
        {
            var displacement = Distance * kb.Direction.ToDirection();
            var center = Arena.Center;
            hints.AddForbiddenZone(new SDKnockbackInCircleFixedDirection(center, displacement, SafeRadius), kb.Activation);
            // big weak upwind goal: the AI walks upwind so the push lands it mid-arena
            var upwindCenter = center - UpwindDistance * kb.Direction.ToDirection();
            hints.GoalZones.Add(position => UpwindRect.Check(position, upwindCenter, kb.Direction) ? UpwindGoalWeight : 0f);
            // Landings on a hurricane are lethal too: forbid each storm's predicted position at
            // the knockback time so the AI plans around it (unknown motion is skipped).
            foreach (var stormPos in HurricanePositionsAt(kb.Activation))
                hints.AddForbiddenZone(new SDCircle(stormPos, HurricaneLandingRadius), kb.Activation);
        }
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

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos)
    {
        if (!pos.InCircle(Arena.Center, LethalRadius))
            return true;
        // landing on a hurricane is lethal: reject any candidate inside a storm's predicted
        // position at the knockback time (unknown motion is skipped)
        foreach (var kb in _casters)
            foreach (var stormPos in HurricanePositionsAt(kb.Activation))
                if (pos.InCircle(stormPos, HurricaneLandingRadius))
                    return true;
        return false;
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
// 2026-08-03 user request: a weak center bias (4y circle, weight 5) so the AI drifts toward the
// middle when nothing else guides it. Higher-weight goals override it (WindBoundary's gap goal
// is 25, knockback dodges are stricter) and forbidden zones always win, so the bias never fights
// a mechanic. The center is always safe: the wall forbids r>22.5.
sealed class CenterBias(BossModule module) : BossComponent(module)
{
    private const float Radius = 4f;
    private const float Weight = 5f;

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // during a gust the upwind rect (weight 8) must be the only weak goal: suppress the
        // center bias so the AI does not idle in the safe middle instead of walking upwind
        if (Module.FindComponent<GustKnockback>()?.AnyActive == true)
            return;
        hints.GoalZones.Add(AIHints.GoalSingleTarget(Arena.Center, Radius, Weight));
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
            .ActivateOnEnter<WindBloomAOEs>()
            .ActivateOnEnter<HurricaneHazards>()
            .ActivateOnEnter<HurricaneKnockbacks>()
            .ActivateOnEnter<RendingWindTelegraphs>()
            .ActivateOnEnter<GustWallKnockbacks>()
            .ActivateOnEnter<GustKnockback>()
            .ActivateOnEnter<CenterBias>()
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
// 2026-08-03: arena radius re-measured 25y (replay 10_55_27.log: farthest survivor 22.6y,
// wall-kill deaths 24.9-26.2y) - the old 20f boundary was smaller than the real playable floor,
// which made the AI pathfind too conservatively around the rim pockets.
public sealed class IslandKidnapper(WorldState ws, Actor primary) : BossModule(ws, primary, new(-150f, -860f), new ArenaBoundsCircle(25f));
