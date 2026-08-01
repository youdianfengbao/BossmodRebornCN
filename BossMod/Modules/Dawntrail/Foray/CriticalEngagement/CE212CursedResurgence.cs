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
    Catching = 0xBC8B, // zombie gas->self, no cast, range 30 width 10 rect
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
    DirectionalImmunity = 1125 // MagicBarrier: directional immunity sides are encoded in Extra
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

// Grave Mold turns its resolved locations into long-lived actors. Their lifetime in the replay
// varies with the pattern (roughly 32-70 seconds), so actor presence is more reliable than a timer.
sealed class ZombieGas(BossModule module) : Components.Voidzone(module, 5f,
    static module => module.Enemies((uint)OID.ZombieGas).Where(actor => !actor.IsDeadOrDestroyed));

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

sealed class NecrohazeBoundary(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeDonut Shape = new(20f, 30f);
    private readonly AOEInstance[] _aoe = [new(Shape, module.Arena.Center)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _aoe;
}

sealed class MagicBarrierDirectionalParry(BossModule module) : Components.DirectionalParry(module,
    [(uint)OID.MagicBarrier], forbiddenPriority: AIHints.Enemy.PriorityInvincible)
{
    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        if (actor.OID == (uint)OID.MagicBarrier && status.ID == (uint)SID.DirectionalImmunity)
            ActorStates[actor.InstanceID] = status.Extra & 0xF;
        else
            base.OnStatusGain(actor, ref status);
    }

    public override void OnStatusLose(Actor actor, ref ActorStatus status)
    {
        if (actor.OID == (uint)OID.MagicBarrier && status.ID == (uint)SID.DirectionalImmunity)
            UpdateState(actor.InstanceID, ActorState(actor.InstanceID) & ~0xF);
        else
            base.OnStatusLose(actor, ref status);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var barrier in Module.Enemies((uint)OID.MagicBarrier))
        {
            if (!barrier.IsDeadOrDestroyed && ActorStates.ContainsKey(barrier.InstanceID))
                hints.SetPriority(barrier, 1);
        }

        base.AddAIHints(slot, actor, assignment, hints);
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
            .ActivateOnEnter<ZombieGas>()
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
public sealed class CursedResurgence(WorldState ws, Actor primary) : BossModule(ws, primary, new(-688f, 150f), new ArenaBoundsCircle(20f));
