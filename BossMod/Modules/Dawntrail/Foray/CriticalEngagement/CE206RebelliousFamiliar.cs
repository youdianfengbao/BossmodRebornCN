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
    KnockAsideTelegraph = 0xBCA1, // helper, 40y long, 60y wide rect; resolves as 15y left knockback
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
    private static readonly AOEShapeRect Shape = new(24f, 0.75f, 24f);
    private readonly AOEInstance[] _aoes = Build(module.Arena.Center);

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _aoes;

    private static AOEInstance[] Build(WPos center)
    {
        var result = new AOEInstance[4];
        for (var i = 0; i < result.Length; ++i)
        {
            var normal = (i * 90f).Degrees().ToDirection();
            var rotation = Angle.FromDirection(normal.OrthoL());
            var origin = center + 23.25f * normal;
            result[i] = new(Shape, origin, rotation, color: Colors.Danger, shapeDistance: Shape.Distance(origin, rotation));
        }
        return result;
    }
}

// C4F2 reveals the reflection pattern four seconds before it resolves. The live helpers are
// already parked on the square floor grid: two diagonal helpers own the 20x20 pattern, while
// three helpers on the five-yalm columns own the 40x40 pattern.
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
            var shape = diagonal ? Short : column ? Long : null;
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

// BCA0 telegraphs a 60y radial knockback that resolves as C162 when Ravenous Gods completes,
// ~6s after the short telegraph ends. Replay displacement shows every player is pulled TOWARD the
// circle helper (not pushed away): wave1 helper at (238,332) carried players 15y north, wave2
// helper at (218,352) carried them 15y west. Keep the warning visible for the full setup so
// automation can solve both knockbacks as one route instead of reacting during the final two
// seconds.
sealed class CircularKnockback(BossModule module) : Components.GenericKnockback(module)
{
    private static readonly AOEShapeCircle Shape = new(60f);
    private const float Distance = 15f;
    // The electric fence kills at ~23.6y from center (replay death point), so reserve just half a
    // yalm inside the 24y square for hitbox/interpolation tolerance.
    private const float SafeHalfWidth = 23.5f;
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
        // The lateral (aside) shove resolves ~3.4s before this radial push, so the player is already
        // displaced by it when the circle resolves. If we evaluate the safe square from the current
        // position we get the wrong side of the origin (the aside can carry the player past the
        // circle center, flipping the radial escape direction into the fence). While the aside is
        // still pending, offset both the square and the push origin by that displacement so the
        // forbidden zone describes the post-aside push. Show it for the whole cast (no 2s gate) so
        // automation can pre-position for the combined knockback rather than react after the shove.
        var aside = Module.FindComponent<KnockAside>();
        foreach (var kb in _casters)
        {
            var center = Arena.Center;
            var origin = kb.Origin;
            if (aside != null && aside.TryGetPendingAsidePush(kb.Activation, out var a))
            {
                center -= a;
                origin -= a;
            }
            hints.AddForbiddenZone(new SDKnockbackInAABBSquareTowardsOrigin(center, origin, kb.Distance, SafeHalfWidth), kb.Activation);
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.CircularKnockbackTelegraph)
        {
            _casters.RemoveAll(k => k.ActorID == caster.InstanceID);
            _casters.Add(new(spell.LocXZ, Distance, Module.CastFinishAt(spell).AddSeconds(HitDelay), Shape, spell.Rotation, Kind.TowardsOrigin, actorID: caster.InstanceID));
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

// BCA1 telegraphs the first knockback; the real hit (C163) lands ~5.1s after the short telegraph
// ends. Replay displacement shows it also pulls every player TOWARD the aside helper: wave1 helper
// at (258,352) carried players ~10-15y east, wave2 helper at (238,332) carried them north. Keep the
// arrow visible for the full setup and add a square-wall forbidden zone so automation starts from a
// position that stays inside after the pull.
sealed class KnockAside(BossModule module) : Components.GenericKnockback(module)
{
    private static readonly AOEShapeRect Shape = new(40f, 30f);
    private const float Distance = 15f;
    private const float SafeHalfWidth = 23.5f;
    private const double HitDelay = 5.1d;

    private sealed class AsideSource(WPos asidePos, WPos circlePos, DateTime activation, ulong actorID)
    {
        public readonly WPos AsidePos = asidePos;
        public readonly WPos CirclePos = circlePos;
        public readonly DateTime Activation = activation;
        public readonly ulong ActorID = actorID;

    }

    private readonly List<AsideSource> _sources = [];
    private readonly List<(WPos AsidePos, DateTime Activation, ulong ActorID)> _pendingAside = [];
    private readonly List<Knockback> _displayed = [with(4)];

    // Exposes the lateral push displacement (15y * direction) that will resolve before the given
    // circular-knockback activation and has not yet been applied, so CircularKnockback can offset
    // its safe square by it. Returns false when no such aside is still pending.
    public bool TryGetPendingAsidePush(DateTime circleActivation, out WDir push)
    {
        var now = WorldState.CurrentTime;
        foreach (var source in _sources)
            if (source.Activation < circleActivation && now < source.Activation)
            {
                // The pull displacement is radial toward the aside helper, so the same offset
                // applies to every player during this wave.
                push = Distance * (source.AsidePos - Arena.Center).Normalized();
                return true;
            }
        push = default;
        return false;
    }

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        foreach (var source in _sources)
        {
            _displayed.Add(new(source.AsidePos, Distance, source.Activation, Shape, default, Kind.TowardsOrigin, actorID: source.ActorID));
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => PruneExpired();

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var source in _sources)
        {
            hints.AddForbiddenZone(new SDKnockbackInAABBSquareTowardsOrigin(Arena.Center, source.AsidePos, Distance, SafeHalfWidth), source.Activation);
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        switch (spell.Action.ID)
        {
            case (uint)AID.KnockAsideTelegraph:
                _pendingAside.RemoveAll(p => p.ActorID == caster.InstanceID);
                _pendingAside.Add((caster.Position, Module.CastFinishAt(spell).AddSeconds(HitDelay), caster.InstanceID));
                break;
            case (uint)AID.CircularKnockbackTelegraph:
                // The circle helper arrives a couple of seconds after the aside telegraph; pair the
                // latest pending aside with it to resolve the lateral push direction.
                for (var i = _pendingAside.Count - 1; i >= 0; --i)
                {
                    var p = _pendingAside[i];
                    _sources.RemoveAll(s => s.ActorID == p.ActorID);
                    _sources.Add(new(p.AsidePos, spell.LocXZ, p.Activation, p.ActorID));
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
