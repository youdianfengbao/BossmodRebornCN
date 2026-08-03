using BossMod.Dawntrail.Foray.CriticalEngagement;
using static BossMod.Components.GenericKnockback;

namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE206RebelliousFamiliar;

public enum OID : uint
{
    Boss = 0x4C4F, // R3.8, BNpcName 14791, cornered gemstone
    YellowGem = 0x4C50,
    BoundaryController = 0x4D88, // non-targetable controller at arena center
    Helper = 0x233C
}

public enum AID : uint
{
    AutoAttack = 0xC6A4, // boss->player, no cast, single-target
    LethalBoundary = 0xBFD0, // controller, persistent out-of-bounds kill field
    YellowGemstones = 0xBC98,
    YellowGemActiveVisual = 0xBC99, // yellow gem->location, 3.0s cast, no damage event
    TopazRay = 0xBC9A, // yellow gem->location, 3.0s cast, range 4 circle
    RubyLight = 0xBC9C,
    RubyReflectionShort = 0xBC9D, // helper, 20y long, 20y wide rect
    RubyReflectionLong1 = 0xBC9E, // helper, 40y long, 40y wide rect
    RubyReflectionLong2 = 0xBC9F,
    CircularKnockbackTelegraph = 0xBCA0, // helper, 60y circle; resolves as 30y away knockback
    KnockAsideTelegraph = 0xBCA1, // helper, 40y long, 60y wide rect; resolves as 15y source-left/right knockback per target side
    RavenousGods = 0xBCA3,
    RavenousGodsSecond = 0xBCA4,
    ClawThenTail = 0xBCA6, // 45y 180-degree cone
    TailThenClaw = 0xBCA7, // 40y 180-degree cone
    ClawThenTailSecond = 0xBCA8,
    TailThenClawSecond = 0xBCA9,
    Howl = 0xBCAA,
    ComboEndVisual = 0xBCAB, // boss, no targets/effects; animation-only combo terminator
    RubyOuterReflection = 0xC4F2,
    RevertModel = 0xC51D, // boss, model-state reset after the claw/tail sequence
    RubyGlowHit = 0xC5CD, // helpers, split packets for the Ruby Light raidwide
    HowlAlt = 0xC161,
    RavenousGodsCircleHit = 0xC162,
    RavenousGodsAsideHit = 0xC163
}


sealed class ClawTailCombo(BossModule module) : ReplayValidatedOppositeAOEs(module)
{
    private static readonly AOEShapeCone Claw = new(45f, 90f.Degrees());
    private static readonly AOEShapeCone Tail = new(40f, 90f.Degrees());

    protected override SequenceConfig? ConfigFor(uint firstActionID) => firstActionID switch
    {
        (uint)AID.ClawThenTail => new(Claw, Tail, (uint)AID.ClawThenTailSecond, 2d),
        // Replay-verified: even though the boss visually spins around before the first hit lands,
        // both first hits resolve centered on the cast-start rotation (hits within +-90 deg of it)
        // and both second hits resolve on the opposite half. No rotation offset for either combo.
        (uint)AID.TailThenClaw => new(Tail, Claw, (uint)AID.TailThenClawSecond, 2d),
        _ => null
    };
}

sealed class TopazRay(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCircle Shape = new(4f);

    protected override AOEConfig? ConfigFor(uint actionID) => actionID == (uint)AID.TopazRay ? new(Shape, true) : null;
}

sealed class LethalBoundary(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeRect Shape = new(24f, 0.5f, 24f);
    private static readonly AOEShapeRect AIShape = new(24f, 1f, 24f);
    private readonly AOEInstance[] _aoes = Build(module.Arena.Center);

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _aoes;

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in _aoes)
            hints.AddForbiddenZone(AIShape.Distance(aoe.Origin, aoe.Rotation));
    }

    private static AOEInstance[] Build(WPos center)
    {
        var result = new AOEInstance[4];
        for (var i = 0; i < result.Length; ++i)
        {
            var normal = (i * 90f).Degrees().ToDirection();
            var rotation = Angle.FromDirection(normal.OrthoL());
            var origin = center + 24f * normal;
            result[i] = new(Shape, origin, rotation, color: Colors.Danger, risky: false, shapeDistance: Shape.Distance(origin, rotation));
        }
        return result;
    }
}

// Reflection helpers use either axis of the square grid: two diagonal helpers own the 20x20
// pattern, while three helpers on a five-yalm row or column own the 40x40 pattern.
sealed class RubyReflection(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeRect Short = new(20f, 10f);
    private static readonly AOEShapeRect Long = new(40f, 20f);
    private readonly List<AOEInstance> _displayed = [with(3)];
    private DateTime _activation;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _displayed.Clear();
        if (_activation == default)
            return CollectionsMarshal.AsSpan(_displayed);

        foreach (var helper in Module.Enemies((uint)OID.Helper))
        {
            if (helper.IsDeadOrDestroyed)
                continue;
            var offset = helper.Position - Arena.Center;
            var diagonal = MathF.Abs(MathF.Abs(offset.X) - 10f) < 0.75f && MathF.Abs(MathF.Abs(offset.Z) - 10f) < 0.75f;
            var column = MathF.Abs(MathF.Abs(offset.X) - 5f) < 0.75f
                && (MathF.Abs(offset.Z) < 0.75f || MathF.Abs(MathF.Abs(offset.Z) - 20f) < 0.75f);
            var row = MathF.Abs(MathF.Abs(offset.Z) - 5f) < 0.75f
                && (MathF.Abs(offset.X) < 0.75f || MathF.Abs(MathF.Abs(offset.X) - 20f) < 0.75f);
            var shape = diagonal ? Short : column || row ? Long : null;
            if (shape != null)
                _displayed.Add(new(shape, helper.Position, helper.Rotation, _activation, Colors.Danger, true,
                    helper.InstanceID, shape.Distance(helper.Position, helper.Rotation)));
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update()
    {
        if (_activation != default && WorldState.CurrentTime > _activation.AddSeconds(1d))
            _activation = default;
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.RubyOuterReflection && !spell.EventHappened)
            _activation = Module.CastFinishAt(spell, 4d);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.RubyReflectionShort or (uint)AID.RubyReflectionLong1 or (uint)AID.RubyReflectionLong2)
        {
            _activation = default;
            ++NumCasts;
        }
    }
}

static class KnockbackGeometry
{
    // C163 row 90 is SourceRight and row 91 is SourceLeft. The server chooses the row from
    // the target's side of the helper's facing axis, so this must be evaluated at each candidate.
    public static WDir AsideDirection(WPos point, WPos origin, WDir facing)
    {
        var right = facing.OrthoR();
        return (point - origin).Dot(right) >= 0f ? right : -right;
    }
}

sealed class SDAsideKnockbackInAABBSquare(WPos center, WPos origin, WDir facing, float distance, float halfWidth) : ShapeDistance
{
    public override bool Contains(in WPos p)
        => !(p + distance * KnockbackGeometry.AsideDirection(p, origin, facing)).InSquare(center, halfWidth);

    public override float Distance(in WPos p) => Contains(p) ? 0f : 1f;

    public override bool RowIntersectsShape(WPos rowStart, WDir dx, float width, float cushion = default) => true;
}

sealed class SDAsideThenRadialKnockbackInAABBSquare(WPos center, WPos asideOrigin, WDir asideFacing, float asideDistance, WPos circleOrigin, float circleDistance, float halfWidth) : ShapeDistance
{
    public override bool Contains(in WPos p)
    {
        var p1 = p + asideDistance * KnockbackGeometry.AsideDirection(p, asideOrigin, asideFacing);
        if (!p1.InSquare(center, halfWidth))
            return true;

        var radial = (p1 - circleOrigin).Normalized();
        var p2 = radial == default ? p1 : p1 + circleDistance * radial;
        return !p2.InSquare(center, halfWidth);
    }

    public override float Distance(in WPos p) => Contains(p) ? 0f : 1f;

    public override bool RowIntersectsShape(WPos rowStart, WDir dx, float width, float cushion = default) => true;
}

// BCA0 resolves as a 30y radial knockback away from its helper about six seconds after the
// telegraph. Keep it visible for the full setup so both knockbacks are solved as one route.
sealed class CircularKnockback(BossModule module) : Components.GenericKnockback(module)
{
    private static readonly AOEShapeCircle Shape = new(60f);
    private const float Distance = 30f;
    private const float SafeHalfWidth = 23f;
    private const double HitDelay = 6.0d;
    private readonly List<Knockback> _casters = [];
    private readonly List<Knockback> _displayed = [with(4)];

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        foreach (var kb in _casters)
            _displayed.Add(kb);
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => PruneExpired();

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var aside = Module.FindComponent<KnockAside>();
        foreach (var kb in _casters)
        {
            // Before C163, solve both hits from the candidate start: the first displacement can
            // differ by side even for two players in the same packet. After C163, only C162 remains.
            if (aside == null || !aside.AddCombinedAIHint(kb, hints))
                hints.AddForbiddenZone(new SDKnockbackInAABBSquareAwayFromOrigin(Arena.Center, kb.Origin, kb.Distance, SafeHalfWidth), kb.Activation);
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.CircularKnockbackTelegraph)
        {
            _casters.RemoveAll(k => k.ActorID == caster.InstanceID);
            _casters.Add(new(spell.LocXZ, Distance, Module.CastFinishAt(spell).AddSeconds(HitDelay), Shape, spell.Rotation, Kind.AwayFromOrigin, actorID: caster.InstanceID));
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.RavenousGodsCircleHit)
        {
            _casters.Clear();
            ++NumCasts;
        }
    }

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        _casters.RemoveAll(k => now > k.Activation.AddSeconds(1d));
    }
}

// BCA1 resolves first as a 15y lateral shove. C163 chooses SourceRight or SourceLeft separately
// for every target from that target's side of the helper's facing axis.
sealed class KnockAside(BossModule module) : Components.GenericKnockback(module)
{
    private static readonly AOEShapeRect Shape = new(40f, 30f);
    private const float Distance = 15f;
    private const float SafeHalfWidth = 23f;
    private const double HitDelay = 5.1d;

    private sealed class AsideSource(WPos asidePos, WPos circlePos, Angle facing, DateTime activation, ulong actorID)
    {
        public readonly WPos AsidePos = asidePos;
        public readonly WPos CirclePos = circlePos;
        public readonly Angle Facing = facing;
        public readonly DateTime Activation = activation;
        public readonly ulong ActorID = actorID;

        public Kind KindFor(WPos point) => (point - AsidePos).Dot(Facing.ToDirection().OrthoR()) >= 0f ? Kind.DirRight : Kind.DirLeft;
    }

    private readonly List<AsideSource> _sources = [];
    private readonly List<(WPos AsidePos, Angle Facing, DateTime Activation, ulong ActorID)> _pendingAside = [];
    private readonly List<Knockback> _displayed = [with(4)];

    public bool AddCombinedAIHint(Knockback circle, AIHints hints)
    {
        foreach (var source in _sources)
            if (source.Activation < circle.Activation && source.CirclePos.AlmostEqual(circle.Origin, 0.5f))
            {
                hints.AddForbiddenZone(new SDAsideThenRadialKnockbackInAABBSquare(Arena.Center, source.AsidePos,
                    source.Facing.ToDirection(), Distance, circle.Origin, circle.Distance, SafeHalfWidth), source.Activation);
                return true;
            }
        return false;
    }

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        foreach (var source in _sources)
            _displayed.Add(new(source.AsidePos, Distance, source.Activation, Shape, source.Facing, source.KindFor(actor.Position), actorID: source.ActorID));
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => PruneExpired();

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var source in _sources)
            hints.AddForbiddenZone(new SDAsideKnockbackInAABBSquare(Arena.Center, source.AsidePos, source.Facing.ToDirection(), Distance, SafeHalfWidth), source.Activation);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        switch (spell.Action.ID)
        {
            case (uint)AID.KnockAsideTelegraph:
                _pendingAside.RemoveAll(p => p.ActorID == caster.InstanceID);
                _pendingAside.Add((caster.Position, spell.Rotation, Module.CastFinishAt(spell).AddSeconds(HitDelay), caster.InstanceID));
                break;
            case (uint)AID.CircularKnockbackTelegraph:
                // The circle helper arrives a couple of seconds after the aside telegraph; pair it
                // with the pending aside so AI can evaluate both landings from one candidate point.
                for (var i = _pendingAside.Count - 1; i >= 0; --i)
                {
                    var p = _pendingAside[i];
                    _sources.RemoveAll(s => s.ActorID == p.ActorID);
                    _sources.Add(new(p.AsidePos, spell.LocXZ, p.Facing, p.Activation, p.ActorID));
                    _pendingAside.RemoveAt(i);
                }
                break;
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.RavenousGodsAsideHit)
        {
            _sources.Clear();
            _pendingAside.Clear();
            ++NumCasts;
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        _sources.RemoveAll(s => s.ActorID == actor.InstanceID);
        _pendingAside.RemoveAll(p => p.ActorID == actor.InstanceID);
    }

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        _sources.RemoveAll(s => now > s.Activation.AddSeconds(1d));
        _pendingAside.RemoveAll(p => now > p.Activation.AddSeconds(1d));
    }
}
sealed class GemstoneRaidwides(BossModule module) : Components.RaidwideCasts(module, [(uint)AID.RubyLight, (uint)AID.RavenousGods, (uint)AID.Howl]);
sealed class RubyReflectionHint(BossModule module) : Components.CastHint(module, (uint)AID.RubyLight, "Ruby reflection - watch the gemstone lines");

sealed class RebelliousFamiliarStates : StateMachineBuilder
{
    public RebelliousFamiliarStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<ClawTailCombo>()
            .ActivateOnEnter<TopazRay>()
            .ActivateOnEnter<LethalBoundary>()
            .ActivateOnEnter<RubyReflection>()
            .ActivateOnEnter<CircularKnockback>()
            .ActivateOnEnter<KnockAside>()
            .ActivateOnEnter<GemstoneRaidwides>()
            .ActivateOnEnter<RubyReflectionHint>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(RebelliousFamiliarStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement,
    GroupID = 1093u,
    NameID = 56u,
    SortOrder = 5)]
// The electric fence is a square: replay player positions reach the corners and BFD0 lethal hits
// cluster at |x|/|z| ~= 24 from center, so the arena and knockback safety checks use a 24y square.
public sealed class RebelliousFamiliar(WorldState ws, Actor primary) : BossModule(ws, primary, new(238f, 352f), new ArenaBoundsSquare(24f));
