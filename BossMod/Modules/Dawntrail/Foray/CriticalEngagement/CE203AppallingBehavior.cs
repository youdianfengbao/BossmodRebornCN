using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE203AppallingBehavior;

public enum OID : uint
{
    Boss = 0x4D8F, // R3.0, BNpcName 14714, Pallmagia
    Pallkeeper = 0x4D90, // BNpcName 14715
    Anchor = 0x4D91, // non-targetable Pallmagia controller
    Helper = 0x233C
}

public enum AID : uint
{
    ElectricBoundary = 0xC26B, // anchor, persistent arena-control pulse (not an 18-25y donut)
    Summon = 0xC26C,
    EsotericInstruction = 0xC26D,
    EsotericInstructionReverse = 0xC26E,
    ReversePolarity = 0xC26F,

    BadBreathKeeperVisual = 0xC270, // Pallkeeper, self-only visual immediately after C271 resolves
    BadBreathInstruction = 0xC271, // helper, 50y 100-degree cone
    PlaincrackerKeeperVisual = 0xC272, // Pallkeeper, self-only visual immediately after C273 resolves
    PlaincrackerInstruction = 0xC273, // helper, 30y circle

    SwapOpposites = 0xC278, // four Pallkeepers teleport to the opposite cardinal point
    SwapClockwise = 0xC279, // north/south Pallkeepers teleport clockwise
    SwapCounterclockwise = 0xC27A, // east/west Pallkeepers teleport counterclockwise

    Roulette = 0xC27B,
    RouletteCenter = 0xC27C, // helper, 5y center cell
    RouletteInner = 0xC27D, // helper, 5-12y 120-degree donut sector; two opposite helpers
    RouletteOuter = 0xC27E, // helper, 12-20y 90-degree donut sector; two opposite helpers

    LilliputianLyric = 0xC27F,
    LilliputianLyricAOE = 0xC280, // helper, 40y 180-degree cone
    MagicHammer = 0xC281,
    MagicHammerAOE = 0xC282, // helper->location, 8y circle
    OccultMissile = 0xC283,
    OccultMissileAOE = 0xC285, // helper->location, 6y circle
    GreatWhirlwind = 0xC286,
    GreatWhirlwindVisual = 0xC287,
    GreatWhirlwindHit = 0xC512,

    BadBreath = 0xC53A,
    BadBreathAOE = 0xC53B, // helper, 50y 100-degree cone
    Plaincracker = 0xC53C,
    PlaincrackerAOE = 0xC53D, // helper, 15y circle
    AutoAttack = 0xC53E
}

// Helpers can be teleported (and, after Reverse Polarity, swapped to the opposite keeper) between
// cast-start and effect. Keep the activation from the cast packet, but follow the live helper for
// self-targeted shapes instead of freezing the initial, often deliberately fake, coordinates.
sealed class AppallingAOEs(BossModule module) : Components.GenericAOEs(module)
{
    private readonly record struct AOEConfig(AOEShape Shape, bool LocationTargeted = false);
    private sealed record Pending(uint ActionID, ulong ActorID, AOEShape Shape, WPos Origin, Angle Rotation, DateTime Activation, bool FollowCaster);

    private static readonly AOEShapeCone BadBreath = new(50f, 50f.Degrees());
    private static readonly AOEShapeCircle PlaincrackerLarge = new(30f);
    private static readonly AOEShapeCircle PlaincrackerSmall = new(15f);
    private static readonly AOEShapeCone Lilliputian = new(40f, 90f.Degrees());
    private static readonly AOEShapeCircle Hammer = new(8f);
    private static readonly AOEShapeCircle Missile = new(6f);
    private readonly List<Pending> _pending = [];
    private readonly List<AOEInstance> _displayed = [];
    private readonly HashSet<uint> _seenSequences = [];

    // Esoteric Instruction preview: the four Pallkeepers fire in the fixed tether order S->E->N->W,
    // and the AoE type is decided by the keeper's position (N/S = bad breath cone aimed into the
    // arena, E/W = plaincracker circle). Schedule all four at the instruction cast start instead of
    // waiting for the 2.7s keeper casts, so the party gets a full 12-16s warning.
    private static readonly WPos[] KeeperPos = [new(807f, -582f), new(827f, -562f), new(807f, -542f), new(787f, -562f)]; // S E N W
    private readonly ulong[] _keeperIIDs = new ulong[4]; // instance ids in cast order: S E N W
    private readonly int[] _keeperFinalDir = [0, 1, 2, 3]; // cast-order slot -> final position index after the polarity swap
    private readonly HashSet<(ulong, ulong)> _swapPairs = [];
    private ulong _bossID;
    private DateTime _firstKeeperActivation;
    private bool _reverse; // C26E: keepers swap positions, pairings delivered as TETH 207 during Reverse Polarity
    private bool _swapApplied;

    private static AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.BadBreathInstruction or (uint)AID.BadBreathAOE => new(BadBreath),
        (uint)AID.PlaincrackerInstruction => new(PlaincrackerLarge),
        (uint)AID.PlaincrackerAOE => new(PlaincrackerSmall),
        (uint)AID.LilliputianLyricAOE => new(Lilliputian),
        (uint)AID.MagicHammerAOE => new(Hammer, true),
        (uint)AID.OccultMissileAOE => new(Missile, true),
        _ => null
    };

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        Prune();
        _displayed.Clear();
        var ordered = _pending.OrderBy(p => p.Activation).ToArray();
        // Magic Hammer resolves as three one-second-spaced batches. If only the current batch is
        // risky, AI picks a technically safe point that is already covered by the second batch and
        // cannot leave the 8y circle in time. Keep the current and next batch forbidden so it plans
        // the step pattern; including all three at once would incorrectly erase every useful route.
        var riskWindow = ordered.Length > 0 && ordered[0].ActionID == (uint)AID.MagicHammerAOE ? 1.25d : 0.25d;
        var riskyDeadline = ordered.Length > 0 ? ordered[0].Activation.AddSeconds(riskWindow) : DateTime.MinValue;
        foreach (var pending in ordered)
        {
            var source = pending.FollowCaster ? WorldState.Actors.Find(pending.ActorID) : null;
            var origin = source?.Position ?? pending.Origin;
            var rotation = source?.Rotation ?? pending.Rotation;
            var imminent = pending.Activation <= riskyDeadline;
            _displayed.Add(new(pending.Shape, origin, rotation, pending.Activation,
                imminent ? Colors.Danger : Colors.AOE, imminent, pending.ActorID, pending.Shape.Distance(origin, rotation)));
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => Prune();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        var aid = spell.Action.ID;
        if ((aid is (uint)AID.EsotericInstruction or (uint)AID.EsotericInstructionReverse) && !spell.EventHappened)
        {
            _bossID = caster.InstanceID;
            ScheduleKeeperPreview(Module.CastFinishAt(spell, aid == (uint)AID.EsotericInstruction ? 3.4f : 10.0f), aid == (uint)AID.EsotericInstructionReverse);
            return;
        }
        if (aid == (uint)AID.ReversePolarity && !spell.EventHappened)
        {
            // The TETH 207 pairings usually arrive while this cast is running; apply them if already complete.
            ApplySwapIfReady();
            return;
        }
        if (ConfigFor(aid) is not { } config || spell.EventHappened)
            return;

        var activation = Module.CastFinishAt(spell);
        if (activation <= WorldState.CurrentTime)
            return;

        var origin = config.LocationTargeted ? spell.LocXZ : caster.Position;
        // Replace a pre-scheduled preview at the same spot with the precise cast packet (two keepers
        // share the bad breath action, so match the preview's fixed position as well).
        _pending.RemoveAll(p => p.ActionID == aid && (p.ActorID == caster.InstanceID
            || p.ActorID == _bossID && (p.Origin - origin).LengthSq() < 1f));
        _pending.Add(new(aid, caster.InstanceID, config.Shape, origin, spell.Rotation, activation, !config.LocationTargeted));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened)
        {
            // Interrupted instruction: the keepers never fire, so drop the previews.
            if (spell.Action.ID is (uint)AID.EsotericInstruction or (uint)AID.EsotericInstructionReverse)
                _pending.RemoveAll(IsPreview);
            else
                _pending.RemoveAll(p => p.ActionID == spell.Action.ID && p.ActorID == caster.InstanceID);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (ConfigFor(spell.Action.ID) == null || spell.GlobalSequence != 0 && !_seenSequences.Add(spell.GlobalSequence))
            return;

        _pending.RemoveAll(p => p.ActionID == spell.Action.ID && p.ActorID == caster.InstanceID);
        ++NumCasts;
    }

    public override void OnActorDestroyed(Actor actor) => _pending.RemoveAll(p => p.ActorID == actor.InstanceID);

    // During Reverse Polarity each pair of keepers connected by a TETH 207 tether trades positions;
    // the AoE type follows the new position while the cast order stays the original S->E->N->W.
    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (!_reverse || _swapApplied || tether.ID != 207)
            return;
        var (a, b) = source.InstanceID < tether.Target ? (source.InstanceID, tether.Target) : (tether.Target, source.InstanceID);
        if (_swapPairs.Add((a, b)))
            ApplySwapIfReady();
    }

    private void ScheduleKeeperPreview(DateTime firstActivation, bool reverse)
    {
        _reverse = reverse;
        _swapApplied = false;
        _swapPairs.Clear();
        if (!CollectKeepers())
            return; // keepers not fully resolved; the real cast packets will handle it
        _firstKeeperActivation = firstActivation;
        _pending.RemoveAll(IsPreview);
        AddKeeperPreview(firstActivation);
    }

    private bool CollectKeepers()
    {
        for (var i = 0; i < 4; ++i)
            _keeperIIDs[i] = 0;
        foreach (var a in WorldState.Actors)
        {
            if (a.OID != (uint)OID.Pallkeeper)
                continue;
            var best = -1;
            var bestDist = float.MaxValue;
            for (var i = 0; i < 4; ++i)
            {
                var d = (a.Position - KeeperPos[i]).LengthSq();
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }
            if (best >= 0)
                _keeperIIDs[best] = a.InstanceID;
        }
        for (var i = 0; i < 4; ++i)
            if (_keeperIIDs[i] == 0)
                return false;
        return true;
    }

    private void AddKeeperPreview(DateTime firstActivation)
    {
        for (var n = 0; n < 4; ++n)
        {
            var dir = _keeperFinalDir[n];
            var pos = KeeperPos[dir];
            var cone = dir is 0 or 2; // N/S positions always fire the bad breath cone
            AOEShape shape = cone ? BadBreath : PlaincrackerLarge;
            var rotation = cone ? Angle.FromDirection(Module.Arena.Center - pos) : default;
            var aid = cone ? (uint)AID.BadBreathInstruction : (uint)AID.PlaincrackerInstruction;
            _pending.Add(new(aid, _bossID, shape, pos, rotation, firstActivation.AddSeconds(4.5d * n), false));
        }
    }

    private void ApplySwapIfReady()
    {
        if (!_reverse || _swapApplied || _swapPairs.Count < 2 || !KeepersKnown())
            return;
        for (var i = 0; i < 4; ++i)
            _keeperFinalDir[i] = i;
        foreach (var (a, b) in _swapPairs)
        {
            var da = IndexOfKeeper(a);
            var db = IndexOfKeeper(b);
            if (da < 0 || db < 0)
                return;
            _keeperFinalDir[da] = db;
            _keeperFinalDir[db] = da;
        }
        _swapApplied = true;
        _pending.RemoveAll(IsPreview);
        AddKeeperPreview(_firstKeeperActivation);
    }

    private bool KeepersKnown()
    {
        for (var i = 0; i < 4; ++i)
            if (_keeperIIDs[i] == 0)
                return false;
        return true;
    }

    private int IndexOfKeeper(ulong iid)
    {
        for (var i = 0; i < 4; ++i)
            if (_keeperIIDs[i] == iid)
                return i;
        return -1;
    }

    private bool IsPreview(Pending p) => p.ActorID == _bossID
        && (p.ActionID == (uint)AID.BadBreathInstruction || p.ActionID == (uint)AID.PlaincrackerInstruction);

    private void Prune()
    {
        var now = WorldState.CurrentTime;
        _pending.RemoveAll(p => now > p.Activation.AddSeconds(1d));
    }
}

// C26B is the persistent electric fence at the arena edge: the only clean boundary hit is 20.1y
// from center and the walkable area is a 20y circle, so mark the edge with a thin danger ring.
sealed class ElectricBoundary(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeDonut Shape = new(19.5f, 21f);
    private readonly AOEInstance[] _aoe = [new(Shape, module.Arena.Center)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _aoe;
}

// Death Roulette is a polar grid, not the C26B 18-25y donut that used to be drawn here. ARR v5
// recordings show five fixed helpers owned by the boss: boss+6 is the center, boss+37/+38 are the
// opposite inner sectors and boss+39/+40 the opposite outer sectors. Their 0x022A movement packets
// update position/rotation immediately before the five consecutive C27C-C27E effects, so build the
// cells from the live actors and clear the whole snapshot atomically after all five sequences.
sealed class DeathRouletteGrid(BossModule module) : Components.GenericAOEs(module)
{
    // Keep a small movement margin around the replay-verified action geometry. Roulette resolves
    // as five almost consecutive effects, so aiming exactly at a cell edge is not reliable for AI.
    private static readonly AOEShapeCircle CenterCell = new(5.5f);
    // Replay hit coordinates put inner-ring victims as far as 56.1 degrees from the helper's
    // facing. The action sectors are therefore 120/90 degrees wide (the shape API takes a
    // half-angle), rather than the accidentally halved 60/45-degree display used previously.
    private static readonly AOEShapeDonutSector InnerCell = new(4.5f, 12.5f, 62f.Degrees());
    private static readonly AOEShapeDonutSector OuterCell = new(11.5f, 20.5f, 47f.Degrees());
    private readonly List<AOEInstance> _displayed = [];
    private readonly HashSet<uint> _seenSequences = [];
    private readonly Dictionary<ulong, Angle> _orientationBaseline = [];
    private DateTime _activation;
    private int _resolvedCells;
    private bool _armed;
    private bool _directionsFresh;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        Prune();
        _displayed.Clear();
        if (!_armed)
            return CollectionsMarshal.AsSpan(_displayed);

        var inner1 = Helper(37);
        var inner2 = Helper(38);
        var outer1 = Helper(39);
        var outer2 = Helper(40);
        if (inner1 == null || inner2 == null || outer1 == null || outer2 == null)
            return CollectionsMarshal.AsSpan(_displayed);

        Add(CenterCell, default, true);
        UpdateDirectionFreshness(inner1, inner2, outer1, outer2);
        if (_directionsFresh)
        {
            Add(InnerCell, inner1.Rotation, true, inner1.InstanceID);
            Add(InnerCell, inner2.Rotation, true, inner2.InstanceID);
            Add(OuterCell, outer1.Rotation, true, outer1.InstanceID);
            Add(OuterCell, outer2.Rotation, true, outer2.InstanceID);
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void AddGlobalHints(GlobalHints hints)
    {
        if (_armed)
            hints.Add("Death roulette: watch the polar grid");
    }

    public override void Update() => Prune();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.Roulette && !spell.EventHappened)
            Arm(Module.CastFinishAt(spell, 14.68f));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.Roulette)
        {
            if (!_armed)
                Arm(WorldState.FutureTime(14.68d));
            return;
        }

        if (spell.Action.ID is < (uint)AID.RouletteCenter or > (uint)AID.RouletteOuter || spell.GlobalSequence != 0 && !_seenSequences.Add(spell.GlobalSequence))
            return;

        if (++_resolvedCells >= 5)
            Clear();
        ++NumCasts;
    }

    private Actor? Helper(ulong offset)
    {
        var actor = WorldState.Actors.Find(Module.PrimaryActor.InstanceID + offset);
        return actor?.OID == (uint)OID.Helper ? actor : null;
    }

    private void Add(AOEShape shape, Angle rotation, bool risky, ulong actorID = 0)
        => _displayed.Add(new(shape, Module.Arena.Center, rotation, _activation, risky ? Colors.Danger : Colors.AOE, risky, actorID, shape.Distance(Module.Arena.Center, rotation)));

    private void Arm(DateTime activation)
    {
        _armed = true;
        _activation = activation;
        _resolvedCells = 0;
        _seenSequences.Clear();
        _orientationBaseline.Clear();
        foreach (var offset in new ulong[] { 37, 38, 39, 40 })
            if (Helper(offset) is { } helper)
                _orientationBaseline[helper.InstanceID] = helper.Rotation;
        _directionsFresh = false;
    }

    private void Clear()
    {
        _armed = false;
        _resolvedCells = 0;
        _displayed.Clear();
        _orientationBaseline.Clear();
        _directionsFresh = false;
    }

    private void UpdateDirectionFreshness(params Actor[] helpers)
    {
        if (_directionsFresh || _orientationBaseline.Count != 4)
            return;

        _directionsFresh = helpers.All(helper => _orientationBaseline.TryGetValue(helper.InstanceID, out var baseline)
            && Math.Abs((helper.Rotation - baseline).Normalized().Rad) > 1f.Degrees().Rad);
    }

    private void Prune()
    {
        if (_armed && WorldState.CurrentTime > _activation.AddSeconds(1d))
            Clear();
    }
}

// The three C512 helper casts each hit the raid; the boss cast is the stable warning packet.
sealed class GreatWhirlwind(BossModule module) : Components.RaidwideCast(module, (uint)AID.GreatWhirlwind);

sealed class AppallingBehaviorStates : StateMachineBuilder
{
    public AppallingBehaviorStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<ElectricBoundary>()
            .ActivateOnEnter<AppallingAOEs>()
            .ActivateOnEnter<DeathRouletteGrid>()
            .ActivateOnEnter<GreatWhirlwind>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(AppallingBehaviorStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement,
    GroupID = 1093u,
    NameID = 59u,
    SortOrder = 2)]
public sealed class AppallingBehavior(WorldState ws, Actor primary) : BossModule(ws, primary, new(807f, -562f), new ArenaBoundsCircle(20f))
{
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.Pallkeeper));
    }
}
