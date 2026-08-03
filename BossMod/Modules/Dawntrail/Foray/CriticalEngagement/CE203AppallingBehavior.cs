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
    // 2026-08-03 user request: the AI forbidden cone is 3y shorter (47f) than the displayed one
    // (50f), opening the cone tip at the keeper's spot so pathfinding can walk through - players
    // survive crossing there, the AI must too. Display/AI split precedent: CE201 ImpactAIShape.
    private static readonly AOEShapeCone BadBreathAI = new(47f, 50f.Degrees());
    // 2026-08-03 architecture rewrite: the keeper TYPE (cone/circle) is random per round
    // (replay 04_28_33.log: normal mapping 4/6, inverted 2/6), so previews cannot predict it -
    // they only mark the landing POSITION with a neutral 1y dot; the real cast packet (CST+
    // 49777 cone / 49779 circle) replaces the marker by position ~2.7s before it resolves.
    private static readonly AOEShapeCircle KeeperMarker = new(1f);
    private static readonly AOEShapeCircle PlaincrackerLarge = new(30f);
    private static readonly AOEShapeCircle PlaincrackerSmall = new(15f);
    private static readonly AOEShapeCone Lilliputian = new(40f, 90f.Degrees());
    private static readonly AOEShapeCircle Hammer = new(8f);
    private static readonly AOEShapeCircle Missile = new(6f);
    private readonly List<Pending> _pending = [];
    private readonly List<AOEInstance> _displayed = [];
    private readonly HashSet<uint> _seenSequences = [];

    // Esoteric Instruction preview: the four Pallkeepers fire in the learned TETH 14 line order
    // (source keeper -> boss; lines arrive during the instruction cast, ~3s apart, in release
    // order; fallback S->E->N->W until a line arrives), and the AoE type is decided by the
    // keeper's LANDING POSITION (normal round: N/S = bad breath cone aimed into the arena,
    // E/W = plaincracker circle; the reverse-learning round C26E flips it: N/S = circle,
    // E/W = cone - driven by _reverse, independent of the TETH 207 landing swap; replay
    // 22_42_09.log). Schedule all four at the instruction cast start instead of waiting for the
    // 2.7s keeper casts, so the party gets a full 12-16s warning.
    private static readonly WPos[] KeeperPos = [new(807f, -582f), new(827f, -562f), new(807f, -542f), new(787f, -562f)]; // S E N W
    private readonly ulong[] _keeperIIDs = new ulong[4]; // position index (0-3 = S E N W) -> keeper instance id
    private readonly int[] _learnOrder = [0, 1, 2, 3]; // release slot (0-3) -> keeper STARTING position, learned from TETH 14 lines
    private int _learnedCount; // number of release slots confirmed by TETH 14 lines; the rest stay positional
    private readonly int[] _keeperFinalDir = [0, 1, 2, 3]; // STARTING position index -> final landing position after the TETH 207 polarity swap
    private readonly HashSet<(ulong, ulong)> _swapPairs = [];
    private ulong _bossID;
    private DateTime _firstKeeperActivation;
    private bool _reverse; // C26E: reverse-learning round; NOTE: the keeper type (cone/circle) is random per round (replay 04_28_33.log), so _reverse no longer predicts types
    private bool _swapApplied;
    private bool _roundStarted; // first real 4-chain cast packet (CST+ 49777 cone / 49779 circle) seen; switches the reverse-round ring from center-pinned to step tracking
    // 2026-08-03 architecture rewrite (replay 04_28_33.log): the keeper type is random per
    // round (normal mapping 4/6, inverted 2/6), so predicting it made every normal-distribution
    // round fully wrong (duplicate preview+real entries, circles lost, wrong ring). Previews
    // now mark landing POSITIONS only with a neutral 1y dot; real cast packets (CST+, ~2.7s
    // early) replace them by position and draw the real shape. Player pattern: stand center
    // during the telegraph, run within the 2.7s cast window. The ring: REVERSE round while
    // telegraphing -> pinned at the arena center; after the first real cast -> tracks the
    // CURRENT step's real type (the packet aid IS the type): circle -> away from its landing
    // spot (spot + 180); cone -> spot direction leaned 65 deg toward the next step's safe
    // direction (100-deg cone = 50 deg half-width + 15 deg margin). Last step resolving and
    // beyond: no ring (leave the danger zone). Normal rounds: no ring (shapes are enough).
    private const float SafeCircleRadius = 4.5f; // drawn ring / AI goal radius
    private const float SafeCircleDist = 16f; // ring center ~16y from arena center, outside the 100-deg cone sweep
    private int _resolved; // instruction AOEs resolved this round (0..4); retained for bookkeeping
    private int _hammerResolved; // magic-hammer batches resolved (C282 action effects); drives the hammer goal zone
    private int _missileResolved; // missile rounds resolved (C285 action effects); drives the missile goal zone
    // resolved-step landing spots recorded on resolution (their entries are removed then, so the
    // goal target must be captured before removal): resolved spots are safe ground - the AI walks
    // there while the next step is forbidden; newest entries last.
    private readonly List<WPos> _hammerResolvedSpots = [];
    private readonly List<WPos> _missileResolvedSpots = [];

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
        // 2026-08-03 group quotas: the 4-chain (C271/C273 real packets + position markers)
        // shows only the nearest TWO real steps; markers never consume the quota and are
        // skipped once a real packet covers their landing spot. The magic hammer (C282) and
        // missiles (C285) are NOT 4-chain steps and show ALL their entries - the old two-entry
        // cap silently dropped most of them.
        // Risk grading restored to the original window scheme (2026-08-03): hammer batches are
        // one second apart, so a 1.25s window makes the current + next batch deep yellow
        // (danger/forbidden) and the last one pale (the AI stands there until it is current).
        // Missiles (replay 04_28_33.log) resolve in THREE rounds of four, ~2s apart (CST+
        // 45.4/47.4/49.4, resolutions 49.4/51.4/53.5, 3.7s cast), so a 2.25s window grades the
        // current + next round deep yellow and the last round pale - same stepped structure as
        // the hammer. Everything else uses a 0.25s window (only the earliest entry is danger).
        // The 4-chain steps are 4.5s apart, so the same rule keeps just the first deep yellow.
        var riskWindow = ordered.Length > 0 && ordered[0].ActionID == (uint)AID.MagicHammerAOE ? 1.25d
            : ordered.Length > 0 && ordered[0].ActionID == (uint)AID.OccultMissileAOE ? 2.25d
            : 0.25d;
        var riskyDeadline = ordered.Length > 0 ? ordered[0].Activation.AddSeconds(riskWindow) : DateTime.MinValue;
        var chainShown = 0;
        foreach (var pending in ordered)
        {
            var isMarker = pending.ActorID == _bossID;
            var isChainReal = !isMarker && pending.ActionID is (uint)AID.BadBreathInstruction or (uint)AID.PlaincrackerInstruction;
            if (isChainReal)
            {
                if (chainShown >= 2)
                    continue;
                ++chainShown;
            }
            else if (isMarker)
            {
                // marker only while its landing spot has no real packet yet (the robust
                // replacement in OnCastStarted already removes it; this is a safety net with a
                // 5y tolerance (2026-08-03, up from 3y) - the keeper's live position can drift
                // from the KeeperPos grid, and a leftover marker next to a real shape reads as
                // a confusing second warning)
                if (_pending.Any(p => p.ActorID != _bossID && (p.Origin - pending.Origin).LengthSq() < 25f))
                    continue;
            }
            // hammer/missile and everything else: shown fully
            var source = pending.FollowCaster ? WorldState.Actors.Find(pending.ActorID) : null;
            var origin = source?.Position ?? pending.Origin;
            var rotation = source?.Rotation ?? pending.Rotation;
            var imminent = pending.Activation <= riskyDeadline;
            // cones forbid the AI with the 3y-shorter shape (tip opens at the keeper's spot);
            // the display keeps the full 50f cone
            var distance = pending.Shape == BadBreath
                ? BadBreathAI.Distance(origin, rotation)
                : pending.Shape.Distance(origin, rotation);
            _displayed.Add(new(pending.Shape, origin, rotation, pending.Activation,
                imminent ? Colors.Danger : Colors.AOE, imminent, pending.ActorID, distance));
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
        // 2026-08-03 architecture rewrite: previews are type-agnostic position markers, so the
        // real packet replaces by POSITION only (no ActionID match - the type is unknown until
        // the CST+ arrives; replay 04_28_33.log). The first real 4-chain cast also marks the
        // round as started, turning the reverse-round center ring off.
        // 2026-08-03 CORE FIX: the ActionID restriction on this RemoveAll was lost during the
        // rewrite - the 4-chain is cast by the same helper instance pool, so an unrestricted
        // caster match deleted the PREVIOUS step's real entry on every new cast start, leaving
        // only the newest real packet (the second step's pale warning never existed) and making
        // SafeCenter's real-array empty after each resolution (ring vanished). Restore the
        // original same-action match: only a stale entry of THIS action + caster is replaced.
        _pending.RemoveAll(p => p.ActionID == aid && p.ActorID == caster.InstanceID);
        // 2026-08-03 robust marker replacement: drop the _bossID marker NEAREST to the real
        // origin - the keeper's live position can drift from the KeeperPos grid by more than
        // 1y, so a fixed tolerance left the marker lingering beside the real shape (which the
        // two-entry display then cut off: "only small dots on the rim"). No tolerance magic.
        var bestIndex = -1;
        var bestDist = float.MaxValue;
        for (var i = 0; i < _pending.Count; ++i)
        {
            if (_pending[i].ActorID != _bossID)
                continue;
            var d = (_pending[i].Origin - origin).LengthSq();
            if (d < bestDist)
            {
                bestDist = d;
                bestIndex = i;
            }
        }
        if (bestIndex >= 0)
            _pending.RemoveAt(bestIndex);
        if (aid is (uint)AID.BadBreathInstruction or (uint)AID.PlaincrackerInstruction)
            _roundStarted = true;
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
            {
                // resolve: drop the real cast entry and any same-spot preview (see OnEventCast -
                // position-mismatched previews must not outlive their hit's forbidden zone)
                _pending.RemoveAll(p => p.ActionID == spell.Action.ID
                    && (p.ActorID == caster.InstanceID || p.ActorID == _bossID && (p.Origin - caster.Position).LengthSq() < 1f));
                // NOTE: _resolved is NOT incremented here. The same resolution also arrives as
                // an OnEventCast action effect (replay-verified 22_42_09.log / user 22:42 run:
                // every step fired BOTH the CST! and the action effect), so counting here too
                // double-counted every step (+2) and the green circle skipped phases or vanished
                // early. OnEventCast is the single counter, deduped by GlobalSequence.
            }
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (ConfigFor(spell.Action.ID) == null || spell.GlobalSequence != 0 && !_seenSequences.Add(spell.GlobalSequence))
            return;

        // record the resolved step's landing spot BEFORE removal - the entry is dropped now, but
        // its spot is the AI's next safe ground (walk to the resolved step while the next one is
        // forbidden)
        var resolvedSpot = _pending.Where(p => p.ActionID == spell.Action.ID && p.ActorID == caster.InstanceID)
            .Select(p => p.Origin).FirstOrDefault();
        if (spell.Action.ID == (uint)AID.MagicHammerAOE && resolvedSpot != default)
            _hammerResolvedSpots.Add(resolvedSpot);
        if (spell.Action.ID == (uint)AID.OccultMissileAOE && resolvedSpot != default)
            _missileResolvedSpots.Add(resolvedSpot);

        // resolve: drop the real cast entry AND any preview sitting on the same landing spot.
        // Previews carry _bossID while real packets carry the caster's instance id; a preview
        // whose position never matched during the cast-start replacement would otherwise linger
        // and keep its forbidden zone ~1s past the hit (AI would refuse to walk through).
        _pending.RemoveAll(p => p.ActionID == spell.Action.ID
            && (p.ActorID == caster.InstanceID || p.ActorID == _bossID && (p.Origin - caster.Position).LengthSq() < 1f));
        // the four instruction AOEs resolve as C271/C273 action effects, one per step - this is
        // the SINGLE counter for the safe-circle phase: the same resolution also arrives as a
        // cast-finish (CST!) event, which removes the AOE but must NOT count (see
        // OnCastFinished), otherwise every step would be counted twice. GlobalSequence guard
        // above dedups duplicate effects.
        if (spell.Action.ID is (uint)AID.BadBreathInstruction or (uint)AID.PlaincrackerInstruction)
            ++_resolved;
        if (spell.Action.ID == (uint)AID.MagicHammerAOE)
            ++_hammerResolved; // resolved batch index for the hammer goal zone (deduped above)
        if (spell.Action.ID == (uint)AID.OccultMissileAOE)
            ++_missileResolved; // resolved round for the missile goal zone (same-round entries arrive together)
        ++NumCasts;
    }

    public override void OnActorDestroyed(Actor actor) => _pending.RemoveAll(p => p.ActorID == actor.InstanceID);

    public override void DrawArenaBackground(int pcSlot, Actor pc)
    {
        base.DrawArenaBackground(pcSlot, pc);
        if (SafeCenter() is { } center)
            Arena.ZoneCircleOutline(center, SafeCircleRadius, Colors.Safe);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (SafeCenter() is { } center)
            hints.GoalZones.Add(AIHints.GoalSingleTarget(center, SafeCircleRadius, 30f));
        // 2026-08-03 magic-hammer goal (user-described pattern): before any batch resolves the
        // AI waits at the LAST batch's spot (the only one never forbidden early); after each
        // resolution the just-resolved batch's spot is safe ground - walk there (the spot is
        // recorded on resolution because the entry is removed right away). Weight 20: below the
        // 4-chain ring (30) and the GaleBlade gap (25), above the center bias (5); a forbidden
        // batch always wins (hard constraint), so the goal never sits inside an imminent batch.
        var hammers = _pending.Where(p => p.ActionID == (uint)AID.MagicHammerAOE)
            .OrderBy(p => p.Activation).ToArray();
        if (hammers.Length != 0)
        {
            var target = _hammerResolvedSpots.Count > 0 ? _hammerResolvedSpots[^1] : hammers[^1].Origin;
            hints.GoalZones.Add(AIHints.GoalSingleTarget(target, 8f, 20f));
        }
        // 2026-08-03 missile goal (replay 04_28_33.log: THREE rounds of FOUR missiles, ~2s
        // apart - the same stepped structure as the hammer): before any round resolves the AI
        // waits at the LAST round's spots; after each resolution the resolved spots are safe
        // ground - every recorded resolved spot gets a goal so the AI picks any of them. Same
        // 20 weight rationale as the hammer goal.
        var missiles = _pending.Where(p => p.ActionID == (uint)AID.OccultMissileAOE)
            .OrderBy(p => p.Activation).ToArray();
        if (missiles.Length != 0)
        {
            if (_missileResolvedSpots.Count > 0)
            {
                foreach (var spot in _missileResolvedSpots)
                    hints.GoalZones.Add(AIHints.GoalSingleTarget(spot, 6f, 20f));
            }
            else
            {
                var lastRound = missiles.GroupBy(m => m.Activation).OrderBy(g => g.Key).Last();
                foreach (var m in lastRound)
                    hints.GoalZones.Add(AIHints.GoalSingleTarget(m.Origin, 6f, 20f));
            }
        }
    }

    // 2026-08-03: the ring is only for the REVERSE round. While the telegraph is still running
    // (!_roundStarted) it is pinned at the arena center (player pattern: stand center, run when
    // the casts start). After the first real cast, it tracks the CURRENT step's REAL type (the
    // packet aid IS the type): circle -> away from its landing spot (spot + 180); cone -> spot
    // direction leaned 65 deg toward the next step's safe direction (100-deg cone = 50 deg
    // half-width + 15 deg margin). Last step resolving (phase 3) and beyond: no ring. Normal
    // rounds never show a ring (the real shapes are enough).
    private WPos? SafeCenter()
    {
        if (_firstKeeperActivation == default || !KeepersKnown())
            return null;
        if (!_reverse)
            return null;
        if (!_roundStarted)
            return Module.Arena.Center;
        // real 4-chain packets in release order (their aid IS the real type)
        var real = _pending.Where(p => p.ActorID != _bossID
            && p.ActionID is (uint)AID.BadBreathInstruction or (uint)AID.PlaincrackerInstruction)
            .OrderBy(p => p.Activation).ToArray();
        // 2026-08-03 CORE FIX: index real[0], not real[_resolved] - `real` only holds the
        // NOT-yet-resolved packets (entries are removed on resolution, so the array SHRINKS),
        // while _resolved counts TOTAL resolutions (it GROWS). Indexing by _resolved walked
        // off the array right after the first resolution (ring vanished: `_resolved >=
        // real.Length` turned true) and, when entries remained, skipped the current step and
        // pointed at a later packet (wrong position). real[0] is the current step by
        // construction: activation-sorted, unresolved only. `_resolved >= 3` still gates the
        // last step (leave the danger zone).
        if (_resolved >= 3 || real.Length == 0)
            return null;
        var cur = real[0];
        var baseDir = Angle.FromDirection(cur.Origin - Module.Arena.Center);
        Angle center;
        if (cur.ActionID == (uint)AID.PlaincrackerInstruction)
        {
            center = baseDir + 180f.Degrees(); // circle: away from the landing spot
        }
        else
        {
            // next step's safe direction (raw): circle spots contribute their opposite direction
            Angle nextSafe;
            if (_resolved + 1 < real.Length)
            {
                var nextDir = Angle.FromDirection(real[_resolved + 1].Origin - Module.Arena.Center);
                nextSafe = real[_resolved + 1].ActionID == (uint)AID.PlaincrackerInstruction
                    ? nextDir + 180f.Degrees() : nextDir;
            }
            else
            {
                nextSafe = default;
            }
            var leanLeft = baseDir + 65f.Degrees();
            var leanRight = baseDir - 65f.Degrees();
            center = MathF.Abs((leanLeft - nextSafe).Normalized().Rad) <= MathF.Abs((leanRight - nextSafe).Normalized().Rad)
                ? leanLeft : leanRight;
        }
        return Module.Arena.Center + SafeCircleDist * center.ToDirection();
    }

    // TETH 14 lines (Pallkeeper -> Boss) arrive in RELEASE order during the instruction cast
    // (~3s apart); each confirms the next release slot, so the preview sequence is learned live
    // and re-scheduled as lines arrive (recorded rounds 1-3 all came in S->E->N->W, but the
    // mechanism is dynamic - do not hardcode it). TETH 207 pairings (during Reverse Polarity /
    // C26F) trade landing positions; the pairings are random per round (replay 22_42_09.log:
    // round 2 S<->W + E<->N diagonal cross, round 3 S<->N + E<->W opposite sides) -
    // ApplySwapIfReady swaps each pair generically.
    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (tether.ID == 14 && source.OID == (uint)OID.Pallkeeper)
        {
            LearnReleaseOrder(source.InstanceID);
            return;
        }
        if (!_reverse || _swapApplied || tether.ID != 207)
            return;
        var (a, b) = source.InstanceID < tether.Target ? (source.InstanceID, tether.Target) : (tether.Target, source.InstanceID);
        if (_swapPairs.Add((a, b)))
            ApplySwapIfReady();
    }

    // TETH 14 line arrival: append the keeper's starting position to the learned release order
    // (dedup), then re-sequence any previews already scheduled so activations follow the real
    // release order. Previews scheduled before the line arrive keep the positional fallback for
    // the not-yet-confirmed slots; the real cast packets later replace each entry anyway.
    private void LearnReleaseOrder(ulong keeperIID)
    {
        var pos = IndexOfKeeper(keeperIID);
        if (pos < 0 || _learnedCount >= 4)
            return;
        for (var i = 0; i < _learnedCount; ++i)
            if (_learnOrder[i] == pos)
                return; // already learned this keeper
        _learnOrder[_learnedCount++] = pos;
        if (_pending.Any(IsPreview))
        {
            _pending.RemoveAll(IsPreview);
            AddKeeperPreview(_firstKeeperActivation);
        }
    }

    private void ScheduleKeeperPreview(DateTime firstActivation, bool reverse)
    {
        _reverse = reverse;
        _swapApplied = false;
        _swapPairs.Clear();
        _roundStarted = false;
        for (var i = 0; i < 4; ++i)
            _learnOrder[i] = i;
        _learnedCount = 0;
        // reset the safe-circle phase; only a fully scheduled round shows the circle
        _resolved = 0;
        _firstKeeperActivation = default;
        // 2026-08-03 architecture rewrite: drop EVERY 4-chain entry from the previous round
        // (markers AND any real packets whose resolution was missed), so a fresh round never
        // starts with stale previews/forbidden zones; the global-sequence dedup also resets.
        _pending.RemoveAll(p => p.ActionID is (uint)AID.BadBreathInstruction or (uint)AID.PlaincrackerInstruction);
        _seenSequences.Clear();
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
            var start = _learnOrder[n]; // release slot n -> keeper's STARTING position
            var dir = _keeperFinalDir[start]; // that keeper's final LANDING position after the TETH 207 swap
            var pos = KeeperPos[dir];
            // 2026-08-03 architecture rewrite: no type prediction (the keeper type is random per
            // round, replay 04_28_33.log) - the preview is a neutral 1y position marker; the real
            // cast packet (CST+ 49777 cone / 49779 circle, ~2.7s early) replaces it by position
            // and draws the real shape. Marker activation keeps the 4.5s release ladder. The
            // marker uses the 0xC271 aid as a placeholder (matching IsPreview); it never reaches
            // a resolution because the real packet replaces it first.
            _pending.Add(new((uint)AID.BadBreathInstruction, _bossID, KeeperMarker, pos, default,
                firstActivation.AddSeconds(4.5d * n), false));
        }
    }

    private void ApplySwapIfReady()
    {
        if (!_reverse || _swapApplied || _swapPairs.Count < 2 || !KeepersKnown())
            return;
        // Generic pair resolution: every TETH 207 pair swaps the two keepers' landing positions
        // (da/db are STARTING position indices; _keeperFinalDir[start] = landing position), so
        // any random pairing (diagonal cross or opposite sides) resolves to the correct final
        // spots. Types do NOT change here - AddKeeperPreview grades the landing position with
        // the _reverse mapping; only landing spots move.
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
