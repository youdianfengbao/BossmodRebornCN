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
// 47541 SpinningSweep: 120-degree cone (r40) from the boss covering the whole arena.
// 2026-08-05 user request: pin the forbidden activation to now so the AI treats the cone as
// already active and bails out immediately instead of waiting for the cast to finish. The
// display keeps the real cast-finish activation - only the AI urgency is forced.
sealed class SpinningSweep(BossModule module) : Components.SimpleAOEs(module, (uint)AID.SpinningSweep, new AOEShapeCone(40f, 60f.Degrees()))
{
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Use cast-finish activation so urgency decays correctly over time and AncientAero (47540)
        // is not incorrectly overridden by a zero-urgency cone.
        foreach (var aoe in ActiveAOEs(slot, actor))
            hints.AddForbiddenZone(aoe.Shape, aoe.Origin, aoe.Rotation, aoe.Activation);
    }
}

// The blades remain dangerous while travelling. Their no-cast action effects (47531/47539)
// only report contact after it happened, so the live actor positions are the useful warning.
sealed class UnbowedSpirit(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCircle Shape = new(4f);
    private static readonly AOEShapeCircle AIShape = new(5.5f);
    private const float BladeSpeed = 2.9f; // y/s
    private const float ArenaRadius = 28.2f; // 场地边界 (R29.5 - blade r1.25 ≈ 28.2)
    private static readonly WPos ArenaCenter = new(-390f, 700f);
    private const double SampleDuration = 1.5; // 采样队列保留时长
    private const double DirectionUpdateInterval = 1.0; // 方向刷新间隔（秒）
    private const double PredictionDuration = 30.0; // 最大预测时长（长档）
    private const double SegmentDuration = 5.0; // 每段时长（5s）
    private const double MinTotalLength = 1.0; // 最小总长度阈值

    private readonly List<Actor> _blades = module.Enemies((uint)OID.AlabasterBlade);
    private readonly List<AOEInstance> _active = [with(8)];

    // 新增状态字段
    private DateTime? _predictedEnd; // 47530 cast完成时设为当前时间+30s
    private bool _settlementStarted; // 47533 cast started时设true
    private readonly Dictionary<ulong, BladeTrack> _bladeTracks = []; // key=InstanceID

    // 单个弹球盘的跟踪状态
    private sealed class BladeTrack
    {
        public readonly List<(DateTime Time, WPos Position)> SampleQueue = []; // 位置采样队列
        public Angle? LockedDir; // 锁定的方向
        public DateTime LastDirUpdate; // 上次方向更新时刻
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _active.Clear();
        foreach (var blade in _blades)
            AddBlade(blade);
        return CollectionsMarshal.AsSpan(_active);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        // 47533 cast started 时设 _settlementStarted = true，清空所有盘的段状态
        if (spell.Action.ID == (uint)AID.InspiritedCrosswindsVisual)
        {
            _settlementStarted = true;
            // 清空所有盘的段状态（锁定方向和预测段）
            foreach (var track in _bladeTracks.Values)
            {
                track.LockedDir = null;
                // 不重置冷却，让它们自然结束
            }
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        // 47530 cast 完成时设 _predictedEnd
        if (spell.Action.ID == (uint)AID.UnbowedSpiritVisual)
        {
            _predictedEnd = WorldState.CurrentTime.AddSeconds(PredictionDuration);
            _settlementStarted = false; // 新批次开始，重置结算标志
            // 清空旧的盘状态字典
            _bladeTracks.Clear();
        }
    }

    public override void Update()
    {
        // 每帧采样当前位置入队
        var now = WorldState.CurrentTime;
        foreach (var blade in _blades)
        {
            if (blade.IsDeadOrDestroyed)
                continue;

            if (!_bladeTracks.TryGetValue(blade.InstanceID, out var track))
            {
                track = new BladeTrack();
                _bladeTracks[blade.InstanceID] = track;
            }

            // 采样当前位置入队
            track.SampleQueue.Add((now, blade.Position));

            // 清理 >1.5s 的旧点
            var cutoff = now.AddSeconds(-SampleDuration);
            while (track.SampleQueue.Count > 0 && track.SampleQueue[0].Time < cutoff)
                track.SampleQueue.RemoveAt(0);
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var now = WorldState.CurrentTime;
        var spinning = Module.FindComponent<SpinningSweep>() is { } sweep && sweep.Casters.Count != 0;
        var hurricaneActive = Module.FindComponent<BladeDodgeZone>()?.HurricaneActive == true;

        foreach (var blade in _blades)
        {
            if (blade.IsDeadOrDestroyed)
                continue;

            // 即时圆始终保留
            hints.AddForbiddenZone(AIShape, blade.Position);

            // 飓风期保持现有逻辑不变
            if (hurricaneActive)
            {
                // 飓风期：现有分支原样保留（length=6f 胶囊 + 中心 goal 都不动）
                if (blade.LastFrameMovement.LengthSq() > 0.0001f)
                {
                    var activation = spinning ? now.AddSeconds(2d) : default;
                    hints.AddForbiddenZone(new SDCapsule(blade.Position, blade.LastFrameMovement.Normalized(), 6f, 4.5f), activation);
                }
                continue;
            }

            // _settlementStarted 后不再生成预测段
            if (_settlementStarted)
                continue;

        // 获取或创建该盘的跟踪状态
        if (!_bladeTracks.TryGetValue(blade.InstanceID, out var track))
        {
            track = new BladeTrack();
            track.LastDirUpdate = now.AddSeconds(-DirectionUpdateInterval); // 初始化为过期状态，首次立即更新
            _bladeTracks[blade.InstanceID] = track;
        }

        // 周期性刷新方向：每 1 秒检查一次
        if ((now - track.LastDirUpdate).TotalSeconds >= DirectionUpdateInterval)
        {
            var smoothDir = CalculateSmoothDirection(track, now);
            if (smoothDir != null)
            {
                // 平滑方向有效，更新锁定方向
                track.LockedDir = smoothDir.Value;
                track.LastDirUpdate = now;
            }
            // 方向无效时保持旧方向（不更新 LockedDir 和 LastDirUpdate）
        }

        // 只有拥有有效锁定方向时才生成预测段
        if (track.LockedDir == null)
            continue;

        // 计算预测段（每帧执行，起点=盘当前位置，方向=LockedDir）
        AddPredictionSegments(hints, blade.Position, track.LockedDir.Value, now, spinning);
        }
    }

    // 计算平滑方向：(当前pos - 约0.75s前pos) 归一化
    private Angle? CalculateSmoothDirection(BladeTrack track, DateTime now)
    {
        if (track.SampleQueue.Count < 2)
            return null;

        // 找到约0.75s前的点
        var targetTime = now.AddSeconds(-SampleDuration / 2); // 1.5s的一半即0.75s
        (DateTime, WPos) closestOld = default;
        var minDiff = double.MaxValue;

        foreach (var sample in track.SampleQueue)
        {
            var diff = Math.Abs((sample.Time - targetTime).TotalSeconds);
            if (diff < minDiff)
            {
                minDiff = diff;
                closestOld = sample;
            }
        }

        if (minDiff > 0.5) // 找不到足够接近的点
            return null;

        // 计算方向向量
        var currentPos = track.SampleQueue[^1].Position; // 最新位置
        var oldPos = closestOld.Item2;
        var delta = currentPos - oldPos;
        var distSq = delta.LengthSq();

        // 两点距离 <0.5y 视为方向无效
        if (distSq < 0.5f * 0.5f)
            return null;

        // 归一化方向并转换为Angle
        var dir = delta.Normalized();
        return Angle.FromDirection(dir);
    }

    // 添加预测段
    private void AddPredictionSegments(AIHints hints, WPos pos, Angle dirAngle, DateTime now, bool spinning)
    {
        // 剩余寿命
        if (_predictedEnd == null)
            return;
        var rem = (_predictedEnd.Value - now).TotalSeconds;
        if (rem <= 0)
            return;

        // 将 Angle 转换为 WDir
        var dir = dirAngle.ToDirection();

        // 计算射线与场边圆的交点距离
        var rayLen = RayCircleIntersection(pos, dir, ArenaCenter, ArenaRadius);
        if (rayLen < 0)
            rayLen = 0; // 当前已在边界外

        // 总长 = min(rayLen, 速度 × 剩余时间)
        var total = Math.Min(rayLen, BladeSpeed * rem);
        if (total < MinTotalLength)
            return; // 总长太短，不铺

        // 按5s/段切分
        var segmentLength = BladeSpeed * SegmentDuration; // 段长 = 2.9 × 5 = 14.5y
        var numSegments = (int)Math.Ceiling(total / segmentLength);

        for (var i = 0; i < numSegments; i++)
        {
            var startDist = i * segmentLength;
            if (startDist >= total)
                break;

            var endDist = Math.Min(startDist + segmentLength, total);
            var currentSegLength = (float)(endDist - startDist);

            if (currentSegLength < 0.1f)
                continue; // 段太短，跳过

            // 计算段头位置
            var segStart = pos + dir * (float)startDist;

            // 创建胶囊段：SDCapsule(pos + dir×段起点距离, dir, 段长+0.5, 4.5f)
            var capsule = new SDCapsule(segStart, dir, currentSegLength + 0.5f, 4.5f);

            // 计算激活时刻：段头时刻 + spinning软化
            var segmentTime = now.AddSeconds(startDist / BladeSpeed);
            if (spinning)
                segmentTime = segmentTime.AddSeconds(2d);

            hints.AddForbiddenZone(capsule, segmentTime);
        }
    }

    // 射线与圆求交，返回前方交点距离（若不相交返回负值）
    private float RayCircleIntersection(WPos rayOrigin, WDir rayDir, WPos circleCenter, float circleRadius)
    {
        var oc = rayOrigin - circleCenter;
        var a = rayDir.LengthSq(); // 应该是1.0（归一化向量）
        var b = 2f * oc.Dot(rayDir);
        var c = oc.LengthSq() - circleRadius * circleRadius;

        var discriminant = b * b - 4 * a * c;
        if (discriminant < 0)
            return -1f; // 不相交

        var sqrtD = (float)Math.Sqrt(discriminant);
        var t1 = (-b - sqrtD) / (2 * a);
        var t2 = (-b + sqrtD) / (2 * a);

        // 取正的最小值（前方交点）
        if (t1 >= 0 && t2 >= 0)
            return Math.Min(t1, t2);
        if (t1 >= 0)
            return t1;
        if (t2 >= 0)
            return t2;
        return -1f; // 都在后方
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

    // Exposed for UnbowedSpirit: shortens the movement capsule while the green zone is active.
    public bool HurricaneActive => _hurricaneActive;

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
