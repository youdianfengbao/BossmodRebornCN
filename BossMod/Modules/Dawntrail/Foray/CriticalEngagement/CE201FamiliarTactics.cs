namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE201FamiliarTactics;

public enum OID : uint
{
    Boss = 0x4BD9, // R2.5, BNpcName 14508 (elm gigas)
    AlabasterBlade = 0x4BDA, // R1.25, moving persistent hazard
    Helper = 0x233C
}

public enum AID : uint
{
    AutoAttack = 50851, // boss->player, no cast, single-target
    HyperconductivePlasma = 47528, // Boss->self, 5.0s cast, raidwide
    BatteringArms = 47529, // Boss->self, 6.0s cast, tankbuster visual

    UnbowedSpiritVisual = 47530, // Boss->self, 3.0s cast, summons moving blades
    UnbowedSpirit = 47531, // blade->self, no cast, range 4 circle

    InspiritedCycloneVisual = 47532, // Boss->self, 5.0s cast, single-target visual
    InspiritedCrosswindsVisual = 47533, // Boss->self, 6.0s cast, single-target visual
    InspiritedCyclone = 47534, // blade/helper->self, 6.0s cast, range 12 circle
    InspiritedCrosswinds = 47535, // blade/helper->self, 6.0s cast, range 60 width 8 cross

    InspiritedHurricaneVisual = 47536, // Boss->self, 4.3s cast, single-target visual
    InspiritedHurricaneCircle = 47537, // blade/helper->self, 5.0s cast, range 12 circle
    InspiritedHurricaneCross = 47538, // blade/helper->self, 5.0s cast, range 60 width 10 cross
    Gale = 47539, // blade->self, no cast, range 4 circle

    AncientAero = 47540, // blade/helper->self, 3.0s cast, range 70 width 6 rect
    SpinningSweep = 47541, // Boss->self, 6.0s cast, range 40 120-degree cone

    InspiritedImpactVisual = 47542, // Boss->self, 3.0s cast, single-target visual
    InspiritedImpact = 47543, // helper->self, 9.6s cast, range 25 circle

    AncientStorm = 47544, // boss->self, raidwide visual
    AncientStormHit = 48041 // helpers, raidwide damage
}

sealed class FamiliarRaidwides(BossModule module) : Components.RaidwideCasts(module, [(uint)AID.HyperconductivePlasma, (uint)AID.AncientStorm]);
sealed class BatteringArms(BossModule module) : Components.SingleTargetDelayableCast(module, (uint)AID.BatteringArms);
sealed class SpinningSweep(BossModule module) : Components.SimpleAOEs(module, (uint)AID.SpinningSweep, new AOEShapeCone(40f, 60f.Degrees()));

// The blades remain dangerous while travelling. Their no-cast action effects (47531/47539)
// only report contact after it happened, so the live actor positions are the useful warning.
sealed class UnbowedSpirit(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCircle Shape = new(4f);
    private static readonly AOEShapeCircle AIShape = new(5.5f);
    // 2026-08-02 user request: shrink the movement-prediction capsule 8y -> 6.5y (half-width 5.5f
    // and the 5.5y body circle stay unchanged) - the 8y lead over-reacted to fast blade sweeps
    // and pushed the AI out of the safe pocket early.
    private const float PredictionLength = 6.5f;
    private readonly List<Actor> _blades = module.Enemies((uint)OID.AlabasterBlade);
    private readonly List<AOEInstance> _active = [with(8)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _active.Clear();
        foreach (var blade in _blades)
            AddBlade(blade);
        return CollectionsMarshal.AsSpan(_active);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var live = _blades.Where(blade => !blade.IsDeadOrDestroyed).ToArray();
        foreach (var blade in live)
        {
            hints.AddForbiddenZone(AIShape, blade.Position);
            if (blade.LastFrameMovement.LengthSq() > 0.0001f)
                hints.AddForbiddenZone(new SDCapsule(blade.Position, blade.LastFrameMovement.Normalized(), PredictionLength, 5.5f));
        }

        if (live.Length != 0)
            hints.GoalZones.Add(position => live.All(blade => !position.InCircle(blade.Position, 7f)) ? 10f : 0f);
    }

    private void AddBlade(Actor blade)
    {
        if (!blade.IsDeadOrDestroyed)
        {
            var origin = blade.Position;
            // Persistent moving hazards must be drawn as imminent danger, otherwise the light-yellow
            // preview color reads as non-risky and automation has no reason to avoid the blade.
            _active.Add(new(Shape, origin, color: Colors.Danger, actorID: blade.InstanceID, shapeDistance: Shape.Distance(origin, default)));
        }
    }
}

// 2026-08-02 cyclone dodge zone (replay 22_29_52.log): the boss reads 47536
// (InspiritedHurricaneVisual, ~4.0s), then EIGHT 0x4BDA blades spawn on the cardinal/diagonal
// cross and rotate clockwise around the boss (= arena center) at ~11.5°/s, sweeping the whole
// arena on orbit rings ~r10/18/26; the pattern resolves when 47532/47534 (InspiritedCyclone
// visual + circle) land. The safe pocket for the whole rotation is the r<6 zone under the boss
// (2026-08-03 user test: shrunk from r8 - the 8y circle over-covered the pocket and misled the
// pilot, while r4 was too small; r6 is the compromise). Show a green circle once the 47536 cast
// starts while blades are alive, and drive the
// AI into it with a high-weight goal (above UnbowedSpirit's 10 and BladePatterns' 20, so the AI
// actually heads into the pocket instead of just keeping its distance). 47530
// (UnbowedSpiritVisual) also spawns 0x4BDA blades but without a center pocket - it must NOT show
// the zone, so the trigger is the 47536 cast start gated on blades being alive. The flag clears
// when the boss starts reading 47532 (InspiritedCycloneVisual - the pattern-resolution signal),
// dropping the zone immediately even if blades are still alive.
sealed class BladeDodgeZone(BossModule module) : BossComponent(module)
{
    private const float SafeRadius = 6f; // boss's feet: safe for the whole rotation (2026-08-03 user test: 8y over-covered, 4y too small, 6y compromise)
    private const float GoalWeight = 30f; // beats the other CE201 goal weights

    private bool _hurricaneActive; // set by 47536 cast start, cleared by 47532 cast start; the 47530 blade wave must NOT show the zone

    private bool BladesActive => Module.Enemies((uint)OID.AlabasterBlade).Any(blade => !blade.IsDeadOrDestroyed);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.InspiritedHurricaneVisual)
            _hurricaneActive = true;
        else if (spell.Action.ID == (uint)AID.InspiritedCycloneVisual)
            _hurricaneActive = false; // boss reads 47532 = pattern resolution signal; drop the zone immediately even if blades are still alive
    }

    public override void DrawArenaBackground(int pcSlot, Actor pc)
    {
        if (_hurricaneActive && BladesActive)
            Arena.ZoneCircleOutline(Arena.Center, SafeRadius, Colors.Safe);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_hurricaneActive && BladesActive)
            hints.GoalZones.Add(AIHints.GoalSingleTarget(Arena.Center, SafeRadius, GoalWeight));
    }
}

// All blade patterns are driven by real helper cast-start packets. In particular, cross AOEs
// must not be predicted from the boss visual: the moving blades can stop at arbitrary positions
// and rotations. Track action + instance + activation so duplicate/late packets cannot remove a
// different blade or a later wave from the same caster.
// 2026-08-02 fix: mixed waves (cross/circle + aero + impact sequence) are displayed fully. The
// old per-branch filters hid any non-aero entry beyond the 0.5s wave window and any non-impact
// entry during the impact sequence, leaving real resolving AOEs invisible on the radar ("safe on
// radar, actually lethal"); OnCastFinished also removed the warning on resync finish packets
// before the AOE actually resolved.
sealed class BladePatterns(BossModule module) : Components.GenericAOEs(module)
{
    private const double WaveWindow = 0.5d;
    private const double ImpactSequenceWindow = 8d;
    private const double EventResolveTolerance = 0.5d;
    private const double TombstoneWindow = 1d;
    private const double ExpireDelay = 2d;

    private static readonly AOEShapeCircle Circle12 = new(12f);
    private static readonly AOEShapeCross Cross8 = new(60f, 4f);
    private static readonly AOEShapeCross Cross10 = new(60f, 5f);
    private static readonly AOEShapeRect AncientAeroRect = new(70f, 3f);
    private static readonly AOEShapeCircle ImpactCircle = new(25f);
    private static readonly AOEShapeCircle ImpactAIShape = new(26f);

    private sealed class PendingAOE(uint actionID, AOEInstance aoe)
    {
        public readonly uint ActionID = actionID;
        public AOEInstance AOE = aoe;
    }

    private readonly record struct ResolvedCast(uint ActionID, ulong ActorID, DateTime Activation, DateTime ExpiresAt);

    private readonly List<PendingAOE> _pending = [with(16)];
    private readonly List<AOEInstance> _displayed = [with(8)];
    private readonly List<ResolvedCast> _resolved = [with(8)];
    private readonly HashSet<uint> _seenGlobalSequences = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        if (_pending.Count == 0)
        {
            return [];
        }

        // Impact helpers start one after another over roughly 7.2s. Keep the complete four-circle
        // sequence visible, but make only the next three circles forbidden so the AI can progress
        // through it (the fourth circle becomes forbidden once the earlier ones resolve). Other
        // blade actions mixed into the sequence are graded by their own activation (imminent =
        // danger, later = translucent preview) instead of being hidden - a hidden cross/circle
        // still resolves and would read as "safe on the radar" while actually lethal. The display
        // window covers one full sequence/wave; later entries belong to a following mechanic.
        var sequenceDeadline = _pending[0].AOE.Activation.AddSeconds(ImpactSequenceWindow);
        var waveDeadline = _pending[0].AOE.Activation.AddSeconds(WaveWindow);
        var impactDisplayed = 0;
        foreach (var entry in _pending)
        {
            if (entry.AOE.Activation > sequenceDeadline || _displayed.Count == 8)
                break; // beyond one sequence/wave, or display cap (4-blade wave + impact circles)

            if (entry.ActionID == (uint)AID.InspiritedImpact)
            {
                var aoe = entry.AOE;
                aoe.Risky = impactDisplayed < 3;
                aoe.Color = aoe.Risky ? Colors.Danger : Colors.AOE;
                _displayed.Add(aoe);
                ++impactDisplayed;
            }
            else if (entry.AOE.Activation <= waveDeadline)
            {
                var aoe = entry.AOE;
                aoe.Color = Colors.Danger;
                aoe.Risky = true;
                _displayed.Add(aoe);
            }
            else
            {
                // later steps (aero rects or other blades): translucent, non-risky preview
                var aoe = entry.AOE;
                aoe.Color = Colors.AOE;
                aoe.Risky = false;
                _displayed.Add(aoe);
            }
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => PruneExpired();

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Impact circles (25y displayed) are forbidden with a wider 26y shape so the pathfinding
        // boundary around their edge cannot squeeze the player into the 25y damage radius.
        var risky = ActiveAOEs(slot, actor).ToArray().Where(aoe => aoe.Risky).ToArray();
        foreach (var aoe in risky)
        {
            if (aoe.Shape == ImpactCircle)
                hints.AddForbiddenZone(ImpactAIShape, aoe.Origin, aoe.Rotation, aoe.Activation);
            else
                hints.AddForbiddenZone(aoe.ShapeDistance ?? aoe.Shape.Distance(aoe.Origin, aoe.Rotation), aoe.Activation);
        }
        if (risky.Length != 0)
            hints.GoalZones.Add(position => risky.All(aoe => !aoe.Check(position)) ? 20f : 0f);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        var shape = ShapeFor(spell.Action.ID);
        if (shape == null)
        {
            return;
        }

        PruneExpired();
        var activation = Module.CastFinishAt(spell);
        if (spell.EventHappened || activation <= WorldState.CurrentTime || WasRecentlyResolved(spell.Action.ID, caster.InstanceID, activation))
        {
            return;
        }

        AddOrRefresh(spell.Action.ID, shape, caster.InstanceID, spell.LocXZ, spell.Rotation, activation);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (ShapeFor(spell.Action.ID) != null)
        {
            var now = WorldState.CurrentTime;
            var activation = Module.CastFinishAt(spell);
            // Only a genuine resolution removes the warning. CastInfo resynchronization emits
            // finish -> start while the cast is still in progress; removing the warning there
            // leaves the radar "safe" while the AOE still resolves on the player. The tombstone
            // below then guards the corrected re-start only when this finish actually resolved.
            if (spell.EventHappened || activation <= now.AddSeconds(EventResolveTolerance))
            {
                RemoveAll(spell.Action.ID, caster.InstanceID);
                RememberResolved(spell.Action.ID, caster.InstanceID, activation, now);
            }
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (ShapeFor(spell.Action.ID) != null)
        {
            if (spell.GlobalSequence != 0 && !_seenGlobalSequences.Add(spell.GlobalSequence))
            {
                return;
            }

            var now = WorldState.CurrentTime;
            ++NumCasts;
            var activation = RemoveResolvedByEvent(spell.Action.ID, caster.InstanceID, now) ?? now;
            RememberResolved(spell.Action.ID, caster.InstanceID, activation, now);
        }
    }

    public override void OnActorDeath(Actor actor) => RemoveActor(actor.InstanceID);
    public override void OnActorDestroyed(Actor actor) => RemoveActor(actor.InstanceID);

    private static AOEShape? ShapeFor(uint actionID) => actionID switch
    {
        (uint)AID.InspiritedCyclone or (uint)AID.InspiritedHurricaneCircle => Circle12,
        (uint)AID.InspiritedCrosswinds => Cross8,
        (uint)AID.InspiritedHurricaneCross => Cross10,
        (uint)AID.AncientAero => AncientAeroRect,
        (uint)AID.InspiritedImpact => ImpactCircle,
        _ => null
    };

    private void AddOrRefresh(uint actionID, AOEShape shape, ulong actorID, WPos origin, Angle rotation, DateTime activation)
    {
        var replacement = new AOEInstance(shape, origin, rotation, activation, actorID: actorID, shapeDistance: shape.Distance(origin, rotation));
        // One actor cannot cast the same action concurrently. Re-sync packets can shift the
        // activation by more than a small epsilon, so replace the key unconditionally.
        RemoveAll(actionID, actorID);
        _pending.Add(new(actionID, replacement));
        SortPending();
    }

    private DateTime? RemoveResolvedByEvent(uint actionID, ulong actorID, DateTime now)
    {
        DateTime? activation = null;
        for (var i = _pending.Count - 1; i >= 0; --i)
        {
            var entry = _pending[i];
            if (entry.ActionID == actionID && entry.AOE.ActorID == actorID && entry.AOE.Activation <= now.AddSeconds(EventResolveTolerance))
            {
                activation = activation == null || entry.AOE.Activation < activation ? entry.AOE.Activation : activation;
                _pending.RemoveAt(i);
            }
        }
        return activation;
    }

    private bool WasRecentlyResolved(uint actionID, ulong actorID, DateTime activation)
    {
        foreach (var resolved in _resolved)
        {
            if (resolved.ActionID == actionID && resolved.ActorID == actorID && Math.Abs((resolved.Activation - activation).TotalSeconds) <= TombstoneWindow)
            {
                return true;
            }
        }
        return false;
    }

    private void RememberResolved(uint actionID, ulong actorID, DateTime activation, DateTime now)
    {
        _resolved.RemoveAll(resolved => resolved.ActionID == actionID && resolved.ActorID == actorID);
        _resolved.Add(new(actionID, actorID, activation, now.AddSeconds(TombstoneWindow)));
    }

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        _pending.RemoveAll(entry => now > entry.AOE.Activation.AddSeconds(ExpireDelay));
        _resolved.RemoveAll(resolved => now > resolved.ExpiresAt);
    }

    private void RemoveAll(uint actionID, ulong actorID) => _pending.RemoveAll(entry => entry.ActionID == actionID && entry.AOE.ActorID == actorID);
    private void RemoveActor(ulong instanceID)
    {
        _pending.RemoveAll(entry => entry.AOE.ActorID == instanceID);
        _resolved.RemoveAll(entry => entry.ActorID == instanceID);
    }
    private void SortPending() => _pending.Sort((left, right) => left.AOE.Activation.CompareTo(right.AOE.Activation));
}

sealed class FamiliarTacticsStates : StateMachineBuilder
{
    public FamiliarTacticsStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<FamiliarRaidwides>()
            .ActivateOnEnter<BatteringArms>()
            .ActivateOnEnter<UnbowedSpirit>()
            .ActivateOnEnter<BladeDodgeZone>()
            .ActivateOnEnter<BladePatterns>()
            .ActivateOnEnter<SpinningSweep>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(FamiliarTacticsStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement,
    GroupID = 1093u,
    NameID = 58u,
    SortOrder = 0)]
// Crosswind recordings place every unharmed player on the outer safe pockets at roughly 28.5y
// from center. A 20y pathfinding boundary makes those legitimate solutions unreachable.
// 2026-08-02 user request: shrink the arena boundary 0.5y - the 30y circle overstates the real
// playable floor, so pathfinding can route the AI (or the pilot) past where the fence actually
// kills. 28.5y safe pockets still fit inside the 29.5y boundary.
public sealed class FamiliarTactics(WorldState ws, Actor primary) : BossModule(ws, primary, new(-390f, 700f), new ArenaBoundsCircle(29.5f))
{
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
    }
}
