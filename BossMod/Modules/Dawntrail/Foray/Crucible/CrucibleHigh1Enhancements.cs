namespace BossMod.Dawntrail.Foray.Crucible;

// 高段第一盘机制补齐（对照 Kano 7.5.6.1 二进制反编译产物逐项移植，2026-09-14）。
// 本文件只新增组件；CrucibleHigh1Mechanics.cs / CrucibleDragonBaits.cs 的作者源码保持原样，
// 已在原文件里配置过的 AID/OID（如 48753、毒池 EObj 0x1EB704）不重复绘制。
// B22 鸮/羽毛拉线提示：被 19640（羽毛）或 19638（林鸮）连线时提醒随后的 25 米暴风击退。
sealed class CrucibleOwlTetherHint(BossModule module) : CrucibleTetherHint(module, "拉线：与博学林鸮/羽毛连线中，注意随后的暴风击退！", 19640u, 19638u);

// B22 48668：读条增伤（Kano 英文占位文案原样保留）。
sealed class CrucibleOwlMagicUpHint(BossModule module) : Components.CastHints(module, [48668u], "Magic Damage Up (single target)");

// B23 48680 点名：19644（腐汁）对玩家读粘液飞弹 → 锁定点名提示。
sealed class CrucibleSlimeTargetHint(BossModule module) : BossComponent(module)
{
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Module.Enemies(19644).Any(source => source is { IsDeadOrDestroyed: false, IsTargetable: true }
            && source.CastInfo is { Action.ID: 48680 } cast && cast.TargetID == actor.InstanceID))
            hints.Add("粘液飞弹是锁定点名，注意减伤！");
    }
}

// B23 五连旋转锥：Cone(50, 45°半角)×5，±45° 步进、间隔 2.1s。48671/48672 起手（左/右旋方向），
// 48673/48675 为每次实际落点的读条校正；显示最近两段，AI 在多段期间引导进第一段的圆心附近。
sealed class CrucibleFlowerSpinningCones(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCone Shape = new(50f, 45f.Degrees());
    private readonly List<AOEInstance> _aoes = [];
    private readonly HashSet<uint> _resolvedSequences = [];
    private ulong _casterID;
    private DateTime _firstActivation;
    private Angle _step;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened)
            return;
        var finish = Module.CastFinishAt(spell);
        if (spell.Action.ID is 48671 or 48672)
        {
            finish = finish.AddSeconds(0.5);
            if (_casterID != caster.InstanceID || Math.Abs((finish - _firstActivation).TotalSeconds) >= 0.75)
            {
                _casterID = caster.InstanceID;
                _firstActivation = finish;
                _step = (spell.Action.ID == 48671 ? -45f : 45f).Degrees();
                _aoes.Clear();
                for (var i = 0; i < 5; ++i)
                    _aoes.Add(new(Shape, spell.LocXZ, spell.Rotation + i * _step, finish.AddSeconds(i * 2.1d), risky: true));
            }
            return;
        }
        if (spell.Action.ID is 48673 or 48675)
        {
            var corrected = new AOEInstance(Shape, spell.LocXZ, spell.Rotation, finish, risky: true);
            var index = _aoes.FindIndex(aoe => Math.Abs((aoe.Activation - finish).TotalSeconds) < 1.0d);
            if (index >= 0)
                _aoes[index] = corrected;
            else if (_aoes.Count == 0)
                _aoes.Add(corrected);
        }
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.RemoveAll(aoe => aoe.Activation.AddSeconds(1.0d) < WorldState.CurrentTime);
        var shown = new List<AOEInstance>(2);
        var count = Math.Min(_aoes.Count, 2);
        for (var i = 0; i < count; ++i)
        {
            var aoe = _aoes[i];
            aoe.Risky = i == 0;
            aoe.Color = i == 0 ? Colors.Danger : Colors.AOE;
            shown.Add(aoe);
        }
        return CollectionsMarshal.AsSpan(shown);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is 48673 or 48675 && _resolvedSequences.Add(spell.GlobalSequence))
        {
            var index = _aoes.FindIndex(aoe => aoe.Activation <= WorldState.FutureTime(0.75d) && aoe.Origin.AlmostEqual(caster.Position, 1f));
            if (index >= 0)
                _aoes.RemoveAt(index);
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
            if (aoe.Risky || aoe.Activation <= WorldState.FutureTime(5.0d))
                CrucibleScorpionAI.Avoid(hints, aoe);
        if (_aoes.Count > 1)
            hints.AddForbiddenZone(new SDInvertedCircle(_aoes[0].Origin, 11f), _aoes[0].Activation.AddSeconds(-0.5d));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 48671 or 48672 && !spell.EventHappened && spell.NPCRemainingTime > 0.5f)
            _aoes.Clear(); // Interrupted opener
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor.InstanceID == _casterID)
            _aoes.Clear();
    }

    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}

// B23 50758 击退 10 米：落点需留在 19.5 米半径内、避开魔界花场地毒圈（6.5 米间隙）。
sealed class CrucibleFlowerKnockback(BossModule module) : HighCrucibleKnockback(module, 50758u, 10f)
{
    private Components.GenericAOEs.AOEInstance[] Voidzones(int slot, Actor actor)
    {
        var component = Module.FindComponent<CrucibleFlowerVoidzone>();
        return component != null ? component.ActiveAOEs(slot, actor).ToArray() : [];
    }

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos)
    {
        if (!pos.InCircle(Module.Center, 19.5f))
            return true;
        var voidzones = Voidzones(slot, actor);
        for (var i = 0; i < voidzones.Length; ++i)
            if (voidzones[i].Check(pos))
                return true;
        return false;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var source in ActiveKnockbacks(slot, actor))
            if (!IsImmune(slot, source.Activation))
                hints.AddForbiddenZone(new Landing(Module.Center, source, Voidzones(slot, actor)), source.Activation.AddSeconds(-0.4d));
    }

    // 评估整段位移后的落点：出 19.5 半径圈或落进毒圈 6.5 米范围内都视为不可行。
    private sealed class Landing(WPos center, Knockback source, Components.GenericAOEs.AOEInstance[] voidzones) : ShapeDistance
    {
        public override float Distance(in WPos p)
        {
            var landing = p + source.Distance * (p - source.Origin).Normalized();
            var clearance = 19.5f - (landing - center).Length();
            for (var i = 0; i < voidzones.Length; ++i)
                clearance = Math.Min(clearance, (landing - voidzones[i].Origin).Length() - 6.5f);
            return clearance;
        }
    }
}

// B23 魔界花场地障碍（EObj 2006848）：6 米圆持续禁区，导航按 6.4 米留边。
sealed class CrucibleFlowerVoidzone(BossModule module) : Components.Voidzone(module, 6f, static m => m.Enemies(2006848u).Where(a => a.EventState != 7 && !a.IsDeadOrDestroyed))
{
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var source in Sources(Module))
            hints.TemporaryObstacles.Add(new SDCircle(source.Position, 6.4f));
    }
}

// B23 状态读条提示（Kano 英文占位文案原样保留）。
sealed class CrucibleFlowerHeavyHint(BossModule module) : Components.CastHints(module, [48680u], "Heavy (single target)");
sealed class CrucibleFlowerKnockbackHint(BossModule module) : Components.CastHints(module, [50758u], "Knockback!");
sealed class CrucibleFlowerParalysisHint(BossModule module) : Components.CastHints(module, [50544u], "Paralysis (single target)");
sealed class CrucibleFlowerPoisonHint(BossModule module) : Components.CastHints(module, [50750u], "Poison (single target)");

// B24 尸生花/花苗/女王鹰蜂拉线提示。
sealed class CrucibleFlowerTetherHint(BossModule module) : CrucibleTetherHint(module, "拉线：被尸生花/花苗/女王鹰蜂连线中！", 19645u, 19646u, 19647u);

// B24 48685 吞噬读条提示。
sealed class CrucibleDevourHint(BossModule module) : Components.CastHints(module, [48685u], "Devour (single target)");

// B25 冰龙场地障碍（EObj 2004778）：9 米圆持续禁区。
sealed class CrucibleDragonVoidzone(BossModule module) : Components.Voidzone(module, 9f, static m => m.Enemies(2004778u).Where(a => a.EventState != 7 && !a.IsDeadOrDestroyed));

// B26 48719/48720/48721 钢铁 + 月环序列：Circle 13 → Donut(6, 30)。
// 48719 为两段序列起手（钢铁 +0.6s、月环再 +2s），48720/48721 为单段校正读条；
// 钢铁结算前月环只以描边显示（不 risky），AI 引导在钢铁结束后站到月环安全内环。
sealed class CrucibleGargoyleSteelDonut(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCircle Steel = new(13f);
    private static readonly AOEShapeDonut Ring = new(6f, 30f);
    private AOEInstance? _steel;
    private AOEInstance? _ring;
    private DateTime _steelResolvedAt;
    private DateTime _ringResolvedAt;

    public bool Active => _steel.HasValue || _ring.HasValue;

    public override void Update()
    {
        var now = WorldState.CurrentTime;
        if (_steel is { } steel && steel.Activation.AddSeconds(0.25d) < now)
            _steel = null;
        if (_ring is { } ring && ring.Activation.AddSeconds(1.0d) < now)
            _ring = null;
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened || spell.Action.ID is not (48719 or 48720 or 48721))
            return;
        Update();
        var finish = Module.CastFinishAt(spell).AddSeconds(spell.Action.ID switch
        {
            48719 => 0.6d,
            48721 => -2.0d,
            _ => 0.0d
        });
        var ringAt = finish.AddSeconds(2.0d);
        if (finish > WorldState.CurrentTime && _steelResolvedAt.AddSeconds(1.0d) < WorldState.CurrentTime && (spell.Action.ID == 48720 || !_steel.HasValue))
            _steel = new(Steel, spell.LocXZ, activation: finish, risky: true, actorID: spell.Action.ID == 48720 ? caster.InstanceID : 0);
        if (ringAt > WorldState.CurrentTime && _ringResolvedAt.AddSeconds(1.0d) < WorldState.CurrentTime && (spell.Action.ID == 48721 || !_ring.HasValue))
            _ring = new(Ring, spell.LocXZ, activation: ringAt, risky: true, actorID: spell.Action.ID == 48721 ? caster.InstanceID : 0);
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        Update();
        var aoes = new List<AOEInstance>(2);
        if (_steel is { } steel)
        {
            steel.Color = Colors.Danger;
            aoes.Add(steel);
        }
        if (_ring is { } ring)
        {
            ring.Risky = !_steel.HasValue;
            ring.Color = ring.Risky ? Colors.Danger : Colors.AOE;
            aoes.Add(ring);
        }
        return CollectionsMarshal.AsSpan(aoes);
    }

    public override void DrawArenaBackground(int pcSlot, Actor pc)
    {
        foreach (ref readonly var aoe in ActiveAOEs(pcSlot, pc))
            if (aoe.Risky)
                aoe.Shape.Draw(Arena, aoe.Origin, aoe.Rotation, aoe.Color);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (ref readonly var aoe in ActiveAOEs(pcSlot, pc))
            if (!aoe.Risky)
                aoe.Shape.Outline(Arena, aoe.Origin, aoe.Rotation, aoe.Color);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        Update();
        if (_steel is { } steel)
        {
            hints.AddForbiddenZone(new SDCircle(steel.Origin, 13.25f), steel.Activation.AddSeconds(-0.5d));
            if (_ring is { } ring)
            {
                // 钢铁未结算时先留在 14.5 米外；瘴气在场则沿钢铁圆心方向找避开 2.3 米的出口。
                hints.AddForbiddenZone(new SDInvertedCircle(steel.Origin, 14.5f), steel.Activation.AddSeconds(-0.5d));
                var mines = Module.FindComponent<CrucibleMiasma>()?.ArmedPositions().ToArray();
                if (mines is { Length: > 0 })
                    hints.AddForbiddenZone(new SteelExitRoute(ring.Origin, mines), steel.Activation.AddSeconds(-0.5d));
            }
        }
        else if (_ring is { } ring)
        {
            hints.AddForbiddenZone(new SDInvertedCircle(ring.Origin, 5.5f), ring.Activation.AddSeconds(-0.3d));
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        // 校正读条命中的施法者与挂起 AOE 的 actorID 匹配（0 表示未归属，任意命中都结算）才清除。
        if (spell.Action.ID == 48720 && (_steel is not { } steel || steel.ActorID == 0 || steel.ActorID == caster.InstanceID))
        {
            _steel = null;
            _steelResolvedAt = WorldState.CurrentTime;
        }
        if (spell.Action.ID == 48721 && (_ring is not { } ring || ring.ActorID == 0 || ring.ActorID == caster.InstanceID))
        {
            _ring = null;
            _ringResolvedAt = WorldState.CurrentTime;
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened || spell.NPCRemainingTime <= 0.5f)
            return;
        if (spell.Action.ID == 48719)
        {
            _steel = null;
            _ring = null;
        }
        if (spell.Action.ID == 48720 && _steel?.ActorID == caster.InstanceID)
            _steel = null;
        if (spell.Action.ID == 48721 && _ring?.ActorID == caster.InstanceID)
            _ring = null;
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor.OID == 19650)
        {
            _steel = null;
            _ring = null;
            return;
        }
        if (_steel?.ActorID == actor.InstanceID)
            _steel = null;
        if (_ring?.ActorID == actor.InstanceID)
            _ring = null;
    }

    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);

    // 钢铁→月环的位移段检查：从当前位置朝月环圆心推进，避开 2.3 米瘴气球。
    private sealed class SteelExitRoute(WPos origin, WPos[] mines) : ShapeDistance
    {
        public override float Distance(in WPos p)
        {
            var toOrigin = origin - p;
            var length = toOrigin.Length();
            if (length < 5.3f)
                return 1f;
            var direction = toOrigin / length;
            foreach (var mine in mines)
            {
                var toMine = mine - p;
                var along = Math.Clamp(toMine.Dot(direction), 0f, length - 5.3f);
                if (mine.InCircle(p + along * direction, 2.3f))
                    return 0f;
            }
            return 1f;
        }
    }
}

// B26 50932/48718 双头锥：两段 Cone(60, 90°半角) 朝向相对 180°，2.8s / 4.85s 落地；48718 为末段校正。
// AI 引导贴到第一锥起点后方 3 米；被突进拉线标记的玩家交给拉线组件处理。
sealed class CrucibleGargoyleDoubleCone(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCone Shape = new(60f, 90f.Degrees());
    private readonly List<AOEInstance> _aoes = [];
    private readonly HashSet<uint> _seenSequences = [];
    private ulong _casterID;

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 50932 && caster.OID == 19650 && spell.TargetXZ.InRect(Module.Center, 25f, 25f))
        {
            if (_seenSequences.Add(spell.GlobalSequence))
            {
                _casterID = caster.InstanceID;
                _aoes.Clear();
                _aoes.Add(new(Shape, spell.TargetXZ, spell.Rotation, WorldState.FutureTime(2.8d), risky: true));
                _aoes.Add(new(Shape, spell.TargetXZ, spell.Rotation + 180f.Degrees(), WorldState.FutureTime(4.85d), risky: true));
            }
        }
        else if (spell.Action.ID == 48718 && _seenSequences.Add(spell.GlobalSequence))
        {
            if (_aoes.Count == 2)
            {
                _aoes.RemoveAt(0);
                _aoes[0] = new(Shape, caster.Position, spell.Rotation + 180f.Degrees(), WorldState.FutureTime(2.05d), risky: true);
            }
            else
            {
                _aoes.Clear();
            }
        }
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.RemoveAll(aoe => aoe.Activation.AddSeconds(0.5d) < WorldState.CurrentTime);
        for (var i = 0; i < _aoes.Count; ++i)
        {
            var aoe = _aoes[i];
            aoe.Risky = i == 0;
            aoe.Color = i == 0 ? Colors.Danger : Colors.AOE;
            _aoes[i] = aoe;
        }
        return CollectionsMarshal.AsSpan(_aoes);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (Module.FindComponent<CrucibleGargoyleRushTether>() is { } rush && rush.IsRushBait(actor))
            return;
        var aoes = ActiveAOEs(slot, actor);
        if (aoes.Length == 0)
            return;
        ref readonly var first = ref aoes[0];
        CrucibleScorpionAI.Avoid(hints, first);
        var anchor = first.Origin - 3f * first.Rotation.ToDirection();
        hints.GoalZones.Add(p => Math.Max(0f, 20f - 3f * (p - anchor).Length()));
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor.InstanceID == _casterID)
            _aoes.Clear();
    }

    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}

// B26 突进（tether 57 + 48717）拉线引导：拉开 20 米后原地保持；AI 禁起点 20 米圆、
// 目标 21.2 米环（避开瘴气），达成 20.5 米后锁定终点。
sealed class CrucibleGargoyleRushTether(BossModule module) : CrucibleTetherHint(module, "拉线：拉开20米后原地保持，直到突进命中！", 19650u, 9020u)
{
    private sealed class Bait(Actor source, Actor target, DateTime expiresAt)
    {
        public readonly Actor Source = source;
        public readonly Actor Target = target;
        public readonly WPos StartPosition = source.Position;
        public DateTime ExpiresAt = expiresAt;
        public WPos? LockedPosition;
    }

    private readonly Dictionary<ulong, Bait> _baits = [];
    private readonly HashSet<ulong> _released = [];

    public bool IsRushBait(Actor actor) => _baits.Values.Any(bait => bait.Target == actor);

    private void RegisterBait(Actor source, Actor target)
    {
        if (!_baits.ContainsKey(source.InstanceID))
            _baits[source.InstanceID] = new(source, target, WorldState.FutureTime(12.0d));
    }

    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (source.NameID == 14608 && tether.ID == 57 && WorldState.Actors.Find(tether.Target) is { } target)
        {
            _released.Remove(source.InstanceID);
            RegisterBait(source, target);
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster.NameID != 14608 || spell.Action.ID != 48717 || spell.EventHappened)
            return;
        _released.Remove(caster.InstanceID);
        if (WorldState.Actors.Find(caster.Tether.Target) is { } target)
            RegisterBait(caster, target);
        foreach (var bait in _baits.Values)
            bait.ExpiresAt = Module.CastFinishAt(spell, 2.0d);
    }

    public override void Update()
    {
        base.Update();
        Tethers.RemoveAll(t => t.Source.NameID != 14608 || t.Source.Tether.ID != 57);
        foreach (var (source, target) in Tethers)
            if (!_released.Contains(source.InstanceID))
                RegisterBait(source, target);
        foreach (var (id, bait) in _baits.ToArray())
        {
            if (bait.Source.IsDeadOrDestroyed || bait.Target.IsDeadOrDestroyed || WorldState.CurrentTime >= bait.ExpiresAt)
            {
                _baits.Remove(id);
                _released.Add(id);
            }
            else if (!bait.LockedPosition.HasValue
                && (bait.Target.Position - bait.StartPosition).Length() >= 20.5f
                && bait.Target.Position.InRect(Module.Center, 19.2f, 19.2f))
            {
                bait.LockedPosition = bait.Target.Position;
            }
        }
    }

    private void ClearAll()
    {
        foreach (var id in _baits.Keys)
            _released.Add(id);
        foreach (var (source, _) in Tethers)
            _released.Add(source.InstanceID);
        _baits.Clear();
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48717 && !spell.EventHappened && spell.NPCRemainingTime > 0.5f)
            ClearAll();
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster.NameID == 14608 && spell.Action.ID == 50933)
            ClearAll();
    }

    // 锁定位置未达成时，在 21.2 米环上选一个不撞瘴气、离玩家最近的候选点。
    private WPos ResolveBaitPosition(Bait bait, Actor actor)
    {
        if (bait.LockedPosition is { } locked)
            return locked;
        var mines = Module.FindComponent<CrucibleMiasma>()?.ArmedPositions().ToArray() ?? [];
        var result = actor.Position;
        var best = float.MaxValue;
        for (var i = 0; i < 180; ++i)
        {
            var candidate = bait.StartPosition + 21.2f * (i * 2f).Degrees().ToDirection();
            if (candidate.InRect(Module.Center, 19f, 19f) && !mines.Any(mine => candidate.InCircle(mine, 2.3f)))
            {
                var dist = (candidate - actor.Position).LengthSq();
                if (dist < best)
                {
                    result = candidate;
                    best = dist;
                }
            }
        }
        return result;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (Module.FindComponent<CrucibleGargoyleSteelDonut>() is { Active: true })
            return;
        if (Module.FindComponent<CrucibleGargoyleKnockback>() is { } kb && kb.ActiveKnockbacks(slot, actor).Length > 0)
            return;
        foreach (var bait in _baits.Values.Where(bait => bait.Target == actor))
        {
            var destination = ResolveBaitPosition(bait, actor);
            hints.AddForbiddenZone(new SDCircle(bait.StartPosition, 20f));
            if (bait.LockedPosition.HasValue)
                hints.AddForbiddenZone(new SDInvertedCircle(destination, 0.6f));
            hints.GoalZones.Add(p => Math.Max(0f, 40f - 3f * (p - destination).Length()));
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_baits.Values.Any(bait => bait.Target == actor))
            hints.Add("拉开20米后原地保持，突进命中前不要返回！");
    }
}

// B28 48763 追击点名月环：48759 起手 +8s 预测窗口，48761 命中事件定位受击目标 → 6s 后 Donut(5, 50)。
sealed class CrucibleMinotaurChaseDonut(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeDonut Shape = new(5f, 50f);
    private AOEInstance? _pendingBait;
    private DateTime _predictionAnchor;

    protected override AOEConfig? ConfigFor(uint actionID) => actionID == 48763
        ? new(Shape, true)
        : null;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (!spell.EventHappened && spell.Action.ID is 48759 or 48760)
        {
            _predictionAnchor = spell.Action.ID == 48759 ? Module.CastFinishAt(spell, 8.0d) : default;
            _pendingBait = null;
        }
        if (spell.Action.ID == 48763)
            _pendingBait = null;
        base.OnCastStarted(caster, spell);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 48761 && _predictionAnchor > WorldState.CurrentTime
            && WorldState.Actors.Find(spell.MainTargetID) is { IsDeadOrDestroyed: false } target)
        {
            _pendingBait = new(Shape, target.Position, activation: WorldState.FutureTime(6.0d), risky: true, actorID: target.InstanceID);
        }
        else
        {
            if (spell.Action.ID == 48763)
                _pendingBait = null;
        }
        base.OnEventCast(caster, spell);
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        if (_pendingBait is { } bait)
        {
            if (bait.Activation.AddSeconds(1.0d) < WorldState.CurrentTime
                || WorldState.Actors.Find(bait.ActorID) is not { IsDeadOrDestroyed: false })
                _pendingBait = null;
        }
        var result = base.ActiveAOEs(slot, actor);
        if (result.Length > 0)
            return result;
        return _pendingBait is { } remaining ? new[] { remaining } : [];
    }

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
            CrucibleScorpionAI.Avoid(hints, aoe);
    }
}

// B30 未爆弹持续危险圈：19668 → Circle 6、19669 → Circle 10；自爆（48792/48794 命中事件）后从列表排除。
sealed class CrucibleBombFieldAOEs(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCircle Small = new(6f);
    private static readonly AOEShapeCircle Large = new(10f);
    private readonly HashSet<ulong> _exploded = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
        => Module.Enemies(19668).Concat(Module.Enemies(19669))
            .Where(a => !a.IsDeadOrDestroyed && !_exploded.Contains(a.InstanceID))
            .Select(a => new AOEInstance(a.OID == 19668 ? Small : Large, a.Position, risky: true, actorID: a.InstanceID))
            .ToArray();

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is 48792 or 48794)
            _exploded.Add(caster.InstanceID);
    }

    public override void OnActorDestroyed(Actor actor) => _exploded.Remove(actor.InstanceID);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
            CrucibleScorpionAI.Avoid(hints, aoe);
    }
}

// B30 48800/48801 冲撞：无读条，命中事件驱动 Rect(40, 6)，0.9s 后落地。
sealed class CrucibleBombCharge(BossModule module) : Components.GenericAOEs(module, 48801u)
{
    private AOEInstance? _charge;

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 48800)
            _charge = new(new AOEShapeRect(40f, 6f), caster.Position, spell.Rotation, WorldState.FutureTime(0.9d), risky: true);
        else if (spell.Action.ID == WatchedAction)
            _charge = null;
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        if (_charge is { } charge && charge.Activation.AddSeconds(1.0d) >= WorldState.CurrentTime)
            return new[] { charge };
        return [];
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
            CrucibleScorpionAI.Avoid(hints, aoe);
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor.OID == 19666)
            _charge = null;
    }

    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}

// B30 爆弹家族场地障碍（EObj 2002331）：6 米圆持续禁区。
sealed class CrucibleBombVoidzone(BossModule module) : Components.Voidzone(module, 6f, static m => m.Enemies(2002331u).Where(a => a.EventState != 7 && !a.IsDeadOrDestroyed));

// B30 提示：榴弹怪连续大爆炸 + 冰棘（Kano 英文占位文案原样保留）。
sealed class CrucibleGrenadeBoomHint(BossModule module) : Components.CastHints(module, [48790u], "榴弹怪连续大爆炸：优先处理榴弹怪，注意减伤！");
sealed class CrucibleIceSpikesHint(BossModule module) : Components.CastHints(module, [48793u], "Ice Spikes (single target)");

// B31 48808 毒雾追踪圈：48807 起手 +2.4s，之后每跳 6 段（48808 命中事件定位落点），
// 显示为 SafeFromAOE 色安全圈（圈外危险），AI 按倒置圆引导。
sealed class CrucibleDragonPoisonTrace(BossModule module) : Components.GenericAOEs(module, 48808u)
{
    private static readonly AOEShapeCircle Shape = new(6f);
    private readonly HashSet<uint> _seenSequences = [];
    private WPos _position;
    private DateTime _started;
    private DateTime _nextHit;
    private DateTime _windowEnd;
    private int _hits;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != 48807 || spell.EventHappened)
            return;
        var start = Module.CastFinishAt(spell, 2.4d);
        if (Math.Abs((start - _started).TotalSeconds) < 0.75d)
            return;
        _started = _nextHit = start;
        _windowEnd = start.AddSeconds(6.5d);
        _position = Module.Center - 20f * spell.Rotation.ToDirection();
        _hits = 0;
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction && _seenSequences.Add(spell.GlobalSequence))
        {
            _position = caster.Position;
            if (_windowEnd < WorldState.CurrentTime)
            {
                _windowEnd = WorldState.FutureTime(6.0d);
                _hits = 0;
            }
            _nextHit = WorldState.FutureTime(1.07d);
            if (++_hits >= 6)
                _windowEnd = default;
        }
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        if (_windowEnd > WorldState.CurrentTime)
            return new[] { new AOEInstance(Shape, _position, activation: _nextHit, color: Colors.SafeFromAOE, risky: false) };
        return [];
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_windowEnd > WorldState.CurrentTime)
            hints.AddForbiddenZone(new SDInvertedCircle(_position, 6f), _nextHit.AddSeconds(-0.5d));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48807 && !spell.EventHappened && spell.NPCRemainingTime > 0.5f)
            _windowEnd = default;
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor.OID == 19672)
            _windowEnd = default;
    }

    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}

// B31 有毒物质（OID 19674）移动预测障碍：2 米持续禁区（导航 2.3 米留边），
// 依据位置差分估算速度，短时间内沿速度方向加一段胶囊禁区。
sealed class CrucibleToxicSpikeVoidzone(BossModule module) : Components.Voidzone(module, 2f, static m => m.Enemies(19674u).Where(a => !a.IsDeadOrDestroyed))
{
    private readonly Dictionary<ulong, (WPos Position, DateTime At, WDir Velocity)> _movement = [];

    public override void Update()
    {
        foreach (var source in Sources(Module))
        {
            var now = WorldState.CurrentTime;
            if (!_movement.TryGetValue(source.InstanceID, out var previous))
            {
                _movement[source.InstanceID] = (source.Position, now, default);
            }
            else if (source.Position != previous.Position && now > previous.At)
            {
                var delta = source.Position - previous.Position;
                var velocity = delta / (float)(now - previous.At).TotalSeconds;
                if (velocity.LengthSq() > 16f || delta.LengthSq() > 25f)
                    velocity = default; // 传送/刷新等异常位移不外推
                _movement[source.InstanceID] = (source.Position, now, velocity);
            }
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var source in Sources(Module))
        {
            hints.TemporaryObstacles.Add(new SDCircle(source.Position, 2.3f));
            if (_movement.TryGetValue(source.InstanceID, out var previous)
                && WorldState.CurrentTime <= previous.At.AddSeconds(0.75d) && previous.Velocity.LengthSq() > 0.01f)
                hints.AddForbiddenZone(new SDCapsule(source.Position, Angle.FromDirection(previous.Velocity), previous.Velocity.Length() * 1.2f, 2.3f), WorldState.FutureTime(0.6d));
        }
    }

    public override void OnActorDestroyed(Actor actor) => _movement.Remove(actor.InstanceID);
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}

// B31 48821 全队大招提示（Kano 英文占位文案原样保留）。
sealed class CrucibleDeadlyPoisonHint(BossModule module) : Components.CastHints(module, [48821u], "Deadly Poison (raidwide)");

// 拉线提示基类：按源 OID 列表每帧重建“连线中”的 (源, 目标) 对，被连线玩家收到文字提示，
// 前台画连线（玩家自己 ≥20 米显示安全色，否则危险色）。
class CrucibleTetherHint(BossModule module, string hint, params uint[] sourceOIDs) : BossComponent(module)
{
    protected readonly List<(Actor Source, Actor Target)> Tethers = [];
    private readonly string _hint = hint;

    public override void Update()
    {
        Tethers.Clear();
        foreach (var source in Module.Enemies(sourceOIDs))
            if (!source.IsDeadOrDestroyed && source.Tether.Target != 0 && WorldState.Actors.Find(source.Tether.Target) is { IsDeadOrDestroyed: false } target)
                Tethers.Add((source, target));
    }

    public bool IsTethered(Actor actor) => Tethers.Any(t => t.Target == actor);

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (IsTethered(actor))
            hints.Add(_hint);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var (source, target) in Tethers)
        {
            var distance = (target.Position - source.Position).Length();
            var color = target == pc ? (distance >= 20f ? Colors.SafeFromAOE : Colors.Danger) : Colors.Object;
            Arena.AddLine(source.Position, target.Position, color);
            Arena.Actor(target, color);
        }
    }
}
