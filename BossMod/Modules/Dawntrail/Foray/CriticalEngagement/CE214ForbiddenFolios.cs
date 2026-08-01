using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE214ForbiddenFolios;

public enum OID : uint
{
    Boss = 0x4BD3, // R6.0, BNpcName 14520, forbidden folios
    Pages64 = 0x4BD4, // R1.0, 64 pages (base knowledge level 6)
    Pages16 = 0x4BD5, // R1.0, 16 pages (base knowledge level 4)
    Pages8 = 0x4BD6, // R1.0, 8 pages (base knowledge level 3)
    Pages512 = 0x4BD7, // R1.0, 512 pages (base knowledge level 9)
    BookTrap = 0x4BD8, // R1.0, book-drop trap
    Helper = 0x233C
}

public enum AID : uint
{
    KnowledgeLevelCorrectionVisual = 0xB8C0, // boss->self, 5.0s cast, applies correction statuses
    KnowledgeLevelCorrection = 0xB8C1, // helper->players, no cast, correction application
    BlotVisual = 0xB8C4, // boss->self, 3.0s cast, visual
    Blot = 0xB8C5, // helper->location, 8.0s cast, range 15 circle
    CoverToCoverFirst = 0xB8C6, // boss->self, 4.0s cast, range 30 180-degree cone
    CoverToCoverSecond = 0xB8C7, // boss->self, 1.0s cast, range 30 180-degree cone
    ArcaneRule = 0xB8C8, // boss->self, 6.0s cast, visual
    QuadRule = 0xB8C9, // boss->self, grid-rule visual
    HorizontalRule = 0xB8CA, // helper->location, range 50 width 12 rect
    SummonPages = 0xB8CB, // helper->location, page summon visual

    KnowledgeLevel4HolyWide = 0xB8CE, // helper->self, range 25 180-degree cone
    KnowledgeLevel5Death = 0xB8CF, // helper->self, range 25 120-degree cone
    KnowledgeLevel3Flare = 0xB8D0, // helper->self, range 25 120-degree cone
    KnowledgeLevel4Holy = 0xB8D1, // helper->self, range 25 120-degree cone
    PrimeKnowledgeLevelDeath = 0xB8D2, // helper->self, range 25 120-degree cone
    PageLevel5Visual = 0xB8D3, // page->self, visual
    PageLevel3Visual = 0xB8D4, // page->self, visual
    PageLevel4Visual = 0xB8D5, // page->self, visual
    PagePrimeVisual = 0xB8D6, // page->self, visual
    BookDropVisual = 0xB8D7, // boss->self, visual
    BookDrop = 0xB8DA, // book trap->self, 8.0s cast, range 3 circle
    ThunderII = 0xB8DC, // helper->self, 4.0s cast, range 50 width 5 rect
    FireII = 0xB8DD, // helper->self, 5.0s cast, range 60 45-degree cone
    FireIIVisual = 0xB8DE, // boss->self, visual
    MarginaliaHit = 0xB8DF, // helper->players, duplicate raidwide damage
    Marginalia = 0xB8E0, // boss->self, 5.0s cast, raidwide visual

    UnknownBC76 = 0xBC76, // observed boss event/cleanup
    SummonVisual = 0xBF9F, // boss->self, summon visual
    AutoAttack = 0xBFA0, // boss->player, no cast, single-target
    UnboundInk = 0xC154, // boss->self, 4.0s cast, range 9 circle
    PrimeKnowledgeLevelDeathWide = 0xC2D7, // helper->self, range 25 180-degree cone

    KnowledgeLevel4HolyWideAlt = 0xC57C, // helper->self, duplicate of B8CE
    KnowledgeLevel5DeathAlt = 0xC57D, // helper->self, duplicate of B8CF
    KnowledgeLevel3FlareAlt = 0xC57E, // helper->self, duplicate of B8D0
    KnowledgeLevel4HolyAlt = 0xC57F, // helper->self, duplicate of B8D1
    PrimeKnowledgeLevelDeathAlt = 0xC580, // helper->self, duplicate of B8D2
    PrimeKnowledgeLevelDeathWideAlt = 0xC581 // helper->self, duplicate of C2D7
}

public enum SID : uint
{
    Correction1 = 0x1396, // knowledge level +1
    Correction2 = 0x1397, // knowledge level +2
    Correction3 = 0x1398, // knowledge level +3
    Correction4 = 0x1399, // knowledge level +4
    Correction5 = 0x139A // knowledge level +5
}

// These location/self casts expose authoritative warning packets, including the initial cross
// writing and the four-yalm page landing circles.
sealed class BasicAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCircle Blot = new(9.5f);
    private static readonly AOEShapeCircle BookDrop = new(3f);
    private static readonly AOEShapeCircle SummonPages = new(4f);
    private static readonly AOEShapeCross QuadRule = new(25f, 5f);
    private static readonly AOEShapeCone FireII = new(60f, 22.5f.Degrees());

    // Blot/book-drop grids expose several waves up front at two-second intervals. With the
    // corrected 9.5y ink radius the adjacent waves leave real gaps, so planning two seconds ahead
    // no longer covers the arena and automation can weave through.
    // Both batches resolve two seconds apart; leaving the second batch risky only 0.25s early gave
    // automation no time to dodge. The lanes sit at the arena frame, so planning both batches
    // together still leaves the center safe and does not oscillate.
    protected override double RiskyActivationWindow => 2.0d;

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.Blot => new(Blot, true),
        (uint)AID.QuadRule => new(QuadRule, true),
        (uint)AID.SummonPages => new(SummonPages),
        (uint)AID.BookDrop => new(BookDrop),
        (uint)AID.FireII => new(FireII),
        _ => null
    };
}

// Cover to Cover sweeps one half first, then the opposite half roughly four seconds later. The
// second sweep's own cast is only 0.7s, which automation cannot react to, so publish the second
// sweep's danger zone from the moment the first sweep resolves (replay: 16 victims in the second
// sweep because it appeared too late).
sealed class CoverToCoverSequence(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCone Shape = new(30f, 90f.Degrees());
    private const double SecondResolveDelay = 4.2d;
    private readonly List<AOEInstance> _displayed = [with(2)];
    private AOEInstance? _first;
    private AOEInstance? _second;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        if (_first is { } first)
            _displayed.Add(first);
        if (_second is { } second)
            _displayed.Add(second);
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => PruneExpired();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened)
            return;

        switch (spell.Action.ID)
        {
            case (uint)AID.CoverToCoverFirst:
                _first = new(Shape, caster.Position, spell.Rotation, Module.CastFinishAt(spell), Colors.Danger, true, caster.InstanceID, Shape.Distance(caster.Position, spell.Rotation));
                break;
            case (uint)AID.CoverToCoverSecond:
                _second = new(Shape, caster.Position, spell.Rotation, Module.CastFinishAt(spell), Colors.Danger, true, caster.InstanceID, Shape.Distance(caster.Position, spell.Rotation));
                break;
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        var now = WorldState.CurrentTime;
        switch (spell.Action.ID)
        {
            case (uint)AID.CoverToCoverFirst:
                _first = null;
                // The first half is now swept; warn about the opposite half until the second sweep lands.
                var predictedRotation = spell.Rotation + 180f.Degrees();
                _second = new(Shape, caster.Position, predictedRotation, now.AddSeconds(SecondResolveDelay), Colors.Danger, true, caster.InstanceID, Shape.Distance(caster.Position, predictedRotation));
                break;
            case (uint)AID.CoverToCoverSecond:
                _second = null;
                break;
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (_first is { ActorID: var firstID } && firstID == actor.InstanceID)
            _first = null;
        if (_second is { ActorID: var secondID } && secondID == actor.InstanceID)
            _second = null;
    }

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        if (_first is { Activation: var firstAct } && now > firstAct.AddSeconds(1d))
            _first = null;
        if (_second is { Activation: var secondAct } && now > secondAct.AddSeconds(1d))
            _second = null;
    }
}

// Thunder II arrives in two batches, two seconds apart. Draw both batches for planning while only
// making the earliest simultaneous set risky; otherwise automation sees the complete square as
// forbidden and oscillates. Each helper's origin and rotation are the actual lane geometry.
sealed class ThunderII(BossModule module) : ReplayValidatedCastAOEs(module)
{
    // Hit reconstruction puts every confirmed target within 2.74y of the lane center (including
    // player hitbox) while non-targets begin at the same boundary. A 5y-wide lane leaves the
    // intended 5y gaps between helpers spaced 10y apart; width 10 falsely tiles the whole arena.
    private static readonly AOEShapeRect Shape = new(50f, 2.5f);
    // Both batches resolve two seconds apart. Widening the risk window to cover both at once made
    // the full lane frame leave no safe cell (regression: noSafeFrames), so the second batch must
    // stay preview until the first resolves; the 0.25s tail is unavoidable without a per-batch
    // deadline. The "three-through-one" weave the operator reported is the ink grid, not thunder.
    protected override double RiskyActivationWindow => 0.25d;
    protected override DateTime? CompetingActivation => Module.FindComponent<BasicAOEs>()?.EarliestActivation;
    protected override AOEConfig? ConfigFor(uint actionID) => actionID == (uint)AID.ThunderII ? new(Shape) : null;
}

// Quad Rule emits four waves at roughly two-second intervals. Unlike ordinary self AOEs, B8CA's
// facing points away from its location target in the recording, so derive the lane direction from
// source -> LocXZ. The fixed 50-yalm length intentionally extends to the arena edge.
sealed class HorizontalRule(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeRect Shape = new(50f, 6f);
    private const double EventResolveTolerance = 0.5d;
    private const double ExpireDelay = 2d;
    private readonly List<AOEInstance> _pending = [with(16)];
    private readonly List<AOEInstance> _displayed = [with(16)];
    private readonly HashSet<uint> _seenGlobalSequences = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        if (_pending.Count == 0)
            return CollectionsMarshal.AsSpan(_displayed);

        var deadline = _pending[0].Activation.AddSeconds(0.25d);
        foreach (var source in _pending)
        {
            var aoe = source;
            var imminent = aoe.Activation <= deadline;
            aoe.Color = imminent ? Colors.Danger : Colors.AOE;
            aoe.Risky = imminent;
            _displayed.Add(aoe);
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => PruneExpired();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.HorizontalRule || spell.EventHappened)
            return;

        PruneExpired();
        var activation = Module.CastFinishAt(spell);
        var direction = spell.LocXZ - caster.Position;
        if (activation <= WorldState.CurrentTime || direction.LengthSq() < 0.01f)
            return;

        _pending.RemoveAll(aoe => aoe.ActorID == caster.InstanceID);
        var rotation = Angle.FromDirection(direction);
        _pending.Add(new(Shape, caster.Position, rotation, activation, actorID: caster.InstanceID, shapeDistance: Shape.Distance(caster.Position, rotation)));
        _pending.Sort((left, right) => left.Activation.CompareTo(right.Activation));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.HorizontalRule && (spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime.AddSeconds(EventResolveTolerance)))
            _pending.RemoveAll(aoe => aoe.ActorID == caster.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != (uint)AID.HorizontalRule || spell.GlobalSequence != 0 && !_seenGlobalSequences.Add(spell.GlobalSequence))
            return;

        _pending.RemoveAll(aoe => aoe.ActorID == caster.InstanceID);
        ++NumCasts;
    }

    public override void OnActorDeath(Actor actor) => _pending.RemoveAll(aoe => aoe.ActorID == actor.InstanceID);
    public override void OnActorDestroyed(Actor actor) => _pending.RemoveAll(aoe => aoe.ActorID == actor.InstanceID);

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        _pending.RemoveAll(aoe => now > aoe.Activation.AddSeconds(ExpireDelay));
    }
}

// Page counts are powers of two, so their base knowledge levels are log2(page count): 8->3,
// 16->4, 64->6 and 512->9. The player's correction status is added to that base, and a sector is
// dangerous only when the resulting personal level fails that sector's rule. This must remain a
// per-player ActiveAOEs calculation; globally painting every sector red is mechanically wrong.
sealed class KnowledgeSectors(BossModule module) : Components.GenericAOEs(module)
{
    private enum SectorKind { Level3, Level4, Level4Wide, Level5, Prime, PrimeWide }
    private readonly record struct SectorConfig(SectorKind Kind, AOEShape Shape);

    private sealed class PendingSector(SectorKind kind, AOEShape shape, Angle rotation, DateTime activation, int? baseLevel, ulong casterID)
    {
        public readonly SectorKind Kind = kind;
        public readonly AOEShape Shape = shape;
        public readonly Angle Rotation = rotation;
        public readonly DateTime Activation = activation;
        public int? BaseLevel = baseLevel;
        public readonly HashSet<ulong> Casters = [casterID];
    }

    private static readonly AOEShapeCone Sector120 = new(25f, 60f.Degrees());
    private static readonly AOEShapeCone Sector180 = new(25f, 90f.Degrees());
    private readonly List<PendingSector> _pending = [with(6)];
    private readonly List<AOEInstance> _displayed = [with(6)];
    private readonly HashSet<uint> _seenGlobalSequences = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        var correction = Correction(actor);
        if (correction == 0)
            return CollectionsMarshal.AsSpan(_displayed);

        foreach (var sector in _pending)
        {
            sector.BaseLevel ??= BaseLevelForRotation(sector.Rotation);
            if (sector.BaseLevel is not int baseLevel || SatisfiesRule(baseLevel + correction, sector.Kind))
                continue;

            _displayed.Add(new(sector.Shape, Module.Arena.Center, sector.Rotation, sector.Activation,
                actorID: sector.Casters.FirstOrDefault(), shapeDistance: sector.Shape.Distance(Module.Arena.Center, sector.Rotation)));
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        base.AddHints(slot, actor, hints);
        if (_pending.Count == 0)
            return;

        var correction = Correction(actor);
        hints.Add(correction == 0 ? "Knowledge correction unavailable" : $"Knowledge correction +{correction}", correction == 0);
    }

    public override void Update() => PruneExpired();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (ConfigFor(spell.Action.ID) is not { } config || spell.EventHappened)
            return;

        PruneExpired();
        var activation = Module.CastFinishAt(spell);
        if (activation <= WorldState.CurrentTime)
            return;

        var existing = _pending.FirstOrDefault(sector => sector.Kind == config.Kind
            && sector.Rotation.AlmostEqual(spell.Rotation, 2f.Degrees().Rad)
            && Math.Abs((sector.Activation - activation).TotalSeconds) <= 0.25d);
        if (existing != null)
        {
            existing.Casters.Add(caster.InstanceID);
            return;
        }

        _pending.Add(new(config.Kind, config.Shape, spell.Rotation, activation, BaseLevelForRotation(spell.Rotation), caster.InstanceID));
        _pending.Sort((left, right) => left.Activation.CompareTo(right.Activation));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (ConfigFor(spell.Action.ID) != null)
            RemoveCaster(caster.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (ConfigFor(spell.Action.ID) == null || spell.GlobalSequence != 0 && !_seenGlobalSequences.Add(spell.GlobalSequence))
            return;

        RemoveCaster(caster.InstanceID);
        ++NumCasts;
    }

    public override void OnActorDeath(Actor actor) => RemoveCaster(actor.InstanceID);
    public override void OnActorDestroyed(Actor actor) => RemoveCaster(actor.InstanceID);

    private static SectorConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.KnowledgeLevel3Flare or (uint)AID.KnowledgeLevel3FlareAlt => new(SectorKind.Level3, Sector120),
        (uint)AID.KnowledgeLevel4Holy or (uint)AID.KnowledgeLevel4HolyAlt => new(SectorKind.Level4, Sector120),
        (uint)AID.KnowledgeLevel4HolyWide or (uint)AID.KnowledgeLevel4HolyWideAlt => new(SectorKind.Level4Wide, Sector180),
        (uint)AID.KnowledgeLevel5Death or (uint)AID.KnowledgeLevel5DeathAlt => new(SectorKind.Level5, Sector120),
        (uint)AID.PrimeKnowledgeLevelDeath or (uint)AID.PrimeKnowledgeLevelDeathAlt => new(SectorKind.Prime, Sector120),
        (uint)AID.PrimeKnowledgeLevelDeathWide or (uint)AID.PrimeKnowledgeLevelDeathWideAlt => new(SectorKind.PrimeWide, Sector180),
        _ => null
    };

    private static int Correction(Actor actor)
    {
        if (actor.FindStatus((uint)SID.Correction1) != null) return 1;
        if (actor.FindStatus((uint)SID.Correction2) != null) return 2;
        if (actor.FindStatus((uint)SID.Correction3) != null) return 3;
        if (actor.FindStatus((uint)SID.Correction4) != null) return 4;
        if (actor.FindStatus((uint)SID.Correction5) != null) return 5;
        return 0;
    }

    // Replay-verified: the sectors are named 知见3级核爆 / 知见4级神圣 / 知见5级即死 / 知见质数即死,
    // and every recorded victim died in a sector whose condition their final knowledge level satisfied.
    // The sector is therefore SAFE only when the condition does NOT hold.
    private static bool SatisfiesRule(int level, SectorKind kind) => kind switch
    {
        SectorKind.Level3 => level % 3 != 0,
        SectorKind.Level4 or SectorKind.Level4Wide => level % 4 != 0,
        SectorKind.Level5 => level % 5 != 0,
        SectorKind.Prime or SectorKind.PrimeWide => !IsPrime(level),
        _ => false
    };

    private static bool IsPrime(int value)
    {
        if (value < 2)
            return false;
        for (var divisor = 2; divisor * divisor <= value; ++divisor)
            if (value % divisor == 0)
                return false;
        return true;
    }

    private int? BaseLevelForRotation(Angle rotation)
    {
        int? result = null;
        var bestDelta = float.MaxValue;
        foreach (var page in WorldState.Actors.Actors.Values)
        {
            var level = page.OID switch
            {
                (uint)OID.Pages8 => 3,
                (uint)OID.Pages16 => 4,
                (uint)OID.Pages64 => 6,
                (uint)OID.Pages512 => 9,
                _ => 0
            };
            if (level == 0 || page.IsDeadOrDestroyed)
                continue;

            var direction = page.Position - Module.Arena.Center;
            if (direction.LengthSq() < 0.01f)
                continue;
            var delta = Math.Abs((Angle.FromDirection(direction) - rotation).Normalized().Rad);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                result = level;
            }
        }
        return result;
    }

    private void RemoveCaster(ulong casterID)
    {
        for (var i = _pending.Count - 1; i >= 0; --i)
        {
            var sector = _pending[i];
            if (sector.Casters.Remove(casterID) && sector.Casters.Count == 0)
                _pending.RemoveAt(i);
        }
    }

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        _pending.RemoveAll(sector => now > sector.Activation.AddSeconds(2d));
    }
}

// Unbound Ink is a soak tower for a single player; drawing it as a red avoidable circle made
// automation run away from it. CastTowers renders it as a tower and steers one player inside.
sealed class UnboundInkTower(BossModule module) : Components.CastTowers(module, (uint)AID.UnboundInk, 9f, 1, 1);

// The three B8DF helpers carry duplicate damage packets; the boss cast is the stable warning.
sealed class Marginalia(BossModule module) : Components.RaidwideCast(module, (uint)AID.Marginalia);

sealed class ForbiddenFoliosStates : StateMachineBuilder
{
    public ForbiddenFoliosStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<BasicAOEs>()
            .ActivateOnEnter<CoverToCoverSequence>()
            .ActivateOnEnter<ThunderII>()
            .ActivateOnEnter<HorizontalRule>()
            .ActivateOnEnter<KnowledgeSectors>()
            .ActivateOnEnter<UnboundInkTower>()
            .ActivateOnEnter<Marginalia>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(ForbiddenFoliosStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement,
    GroupID = 1093u,
    NameID = 52u,
    SortOrder = 13)]
// Replay-verified circular arena: 14k+ player position samples cluster inside r20 with zero
// occupancy in square corners, and book traps/mechanics stop at r~20. The Horizontal Rule lanes
// are projected from outside (r26-36), which previously misled the bounds into a 25y square -
// that made automation run for corner "safe spots" that are actually out of bounds.
public sealed class ForbiddenFolios(WorldState ws, Actor primary) : BossModule(ws, primary, new(659f, 659f), new ArenaBoundsCircle(20f))
{
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.Pages8));
        Arena.Actors(Enemies((uint)OID.Pages16));
        Arena.Actors(Enemies((uint)OID.Pages64));
        Arena.Actors(Enemies((uint)OID.Pages512));
    }
}
