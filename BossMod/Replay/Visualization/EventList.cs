using BossMod.Autorotation;
using Dalamud.Bindings.ImGui;

namespace BossMod.ReplayVisualization;

sealed class EventList(Replay r, Action<DateTime> scrollTo, PlanDatabase planDB, ReplayDetailsWindow timelineSync)
{
    record struct Lists(OpList? Ops, IPCList? IPCs);

    private readonly UITree _tree = new();
    private Lists _listsRaw;
    private readonly Dictionary<Replay.Encounter, Lists> _listsFiltered = [];

    public void Draw()
    {
        foreach (var n in _tree.Node("完整数据"))
        {
            foreach (var no in _tree.Node("原始操作", contextMenu: () => OpListContextMenu(_listsRaw.Ops)))
            {
                _listsRaw.Ops ??= new(r, null, null, r.Ops, scrollTo);
                _listsRaw.Ops.Draw(_tree, r.Ops[0].Timestamp);
            }
            foreach (var no in _tree.Node("服务器 IPC", contextMenu: () => IPCListContextMenu(_listsRaw.IPCs, null)))
            {
                _listsRaw.IPCs ??= new(r, null, r.Ops, scrollTo);
                _listsRaw.IPCs.Draw(_tree, r.Ops[0].Timestamp);
            }

            DrawContents(null, null);
            DrawUserMarkers();
        }
        foreach (var e in _tree.Nodes(r.Encounters, e => new($"{BossModuleRegistry.FindByOID(e.OID)?.ModuleType.Name}: {e.InstanceID:X}, 区域={e.Zone}, 开始={e.Time.Start:O}, 时长={e.Time}, 开怪倒计时={e.CountdownOnPull:f3}")))
        {
            var moduleInfo = BossModuleRegistry.FindByOID(e.OID);
            ref var lists = ref CollectionsMarshal.GetValueRefOrAddDefault(_listsFiltered, e, out _);
            foreach (var n in _tree.Node("原始操作", contextMenu: () => OpListContextMenu(_listsFiltered[e].Ops)))
            {
                lists.Ops ??= new(r, e, moduleInfo, OpsInRange(r.Ops, e.Time.Start, e.Time.End), scrollTo);
                lists.Ops.Draw(_tree, e.Time.Start);
            }
            foreach (var n in _tree.Node("服务器 IPC", contextMenu: () => IPCListContextMenu(_listsFiltered[e].IPCs, moduleInfo)))
            {
                lists.IPCs ??= new(r, e, OpsInRange(r.Ops, e.Time.Start, e.Time.End), scrollTo);
                lists.IPCs.Draw(_tree, e.Time.Start);
            }

            DrawContents(e, moduleInfo);
            DrawEncounterDetails(e, TimePrinter(e.Time.Start));
            DrawTimelines(e);
        }
    }

    private void DrawContents(Replay.Encounter? filter, BossModuleRegistry.Info? moduleInfo)
    {
        var oidType = moduleInfo?.ObjectIDType;
        var aidType = moduleInfo?.ActionIDType;
        var sidType = moduleInfo?.StatusIDType;
        var tidType = moduleInfo?.TetherIDType;
        var iidType = moduleInfo?.IconIDType;
        var reference = filter?.Time.Start ?? r.Ops[0].Timestamp;
        var tp = TimePrinter(reference);
        var actions = filter != null ? r.EncounterActions(filter) : r.Actions;
        var statuses = filter != null ? r.EncounterStatuses(filter) : r.Statuses;
        var tethers = filter != null ? r.EncounterTethers(filter) : r.Tethers;
        var icons = filter != null ? r.EncounterIcons(filter) : r.Icons;
        var mapEffects = filter != null ? r.EncounterMapEffects(filter) : r.MapEffects;
        var dirus = filter != null ? r.EncounterDirectorUpdates(filter) : r.DirectorUpdates;

        foreach (var n in _tree.Node("参与者"))
        {
            if (filter == null)
            {
                DrawParticipants(r.Participants, actions, statuses, tp, reference, filter, aidType, sidType);
            }
            else
            {
                foreach (var (oid, list) in _tree.Nodes(filter.ParticipantsByOID, kv => new($"{kv.Key:X} '{oidType?.GetEnumName(kv.Key)}' ({kv.Value.Count} 个对象)")))
                {
                    DrawParticipants(list, actions, statuses, tp, reference, filter, aidType, sidType);
                }
            }
        }

        var boss = filter?.ParticipantsByOID[filter.OID].Find(p => p.InstanceID == filter.InstanceID);
        if (boss != null)
        {
            foreach (var n in _tree.Node("Boss 施法", boss.Casts.Count == 0))
            {
                DrawCasts(boss.Casts, reference, aidType);
            }
        }

        bool actionIsCrap(Replay.Action a) => a.Source.Type is ActorType.Player or ActorType.Pet or ActorType.Chocobo or ActorType.Buddy;
        var interestingActions = new List<Replay.Action>();
        var otherActions = new List<Replay.Action>();
        foreach (var a in actions)
        {
            (actionIsCrap(a) ? otherActions : interestingActions).Add(a);
        }

        foreach (var n in _tree.Node("重要动作", interestingActions.Count == 0))
        {
            DrawActions(interestingActions, tp, aidType);
        }
        foreach (var n in _tree.Node("其他动作", otherActions.Count == 0))
        {
            DrawActions(otherActions, tp, aidType);
        }
        bool statusIsCrap(Replay.Status s) => s.Source?.Type is ActorType.Player or ActorType.Pet or ActorType.Chocobo or ActorType.Buddy || s.Target.Type is ActorType.Pet or ActorType.Chocobo;
        var interestingStatuses = new List<Replay.Status>();
        var otherStatuses = new List<Replay.Status>();
        foreach (var s in statuses)
        {
            (statusIsCrap(s) ? otherStatuses : interestingStatuses).Add(s);
        }

        foreach (var n in _tree.Node("重要状态", interestingStatuses.Count == 0))
        {
            DrawStatuses(interestingStatuses, tp, sidType);
        }
        foreach (var n in _tree.Node("其他状态", otherStatuses.Count == 0))
        {
            DrawStatuses(otherStatuses, tp, sidType);
        }

        var haveTethers = false;
        foreach (var _ in tethers) { haveTethers = true; break; }
        foreach (var n in _tree.Node("连线", !haveTethers))
        {
            _tree.LeafNodes(tethers, t => $"{tp(t.Time.Start)} + {t.Time}: {t.ID} ({tidType?.GetEnumName(t.ID)}) @ {ReplayUtils.ParticipantString(t.Source, t.Time.Start)} -> {ReplayUtils.ParticipantString(t.Target, t.Time.Start)}");
        }

        var haveIcons = false;
        foreach (var _ in icons) { haveIcons = true; break; }
        foreach (var n in _tree.Node("标记", !haveIcons))
        {
            _tree.LeafNodes(icons, i => $"{tp(i.Timestamp)}: {i.ID} ({iidType?.GetEnumName(i.ID)}) @ {ReplayUtils.ParticipantString(i.Source, i.Timestamp)} -> {ReplayUtils.ParticipantString(i.Target, i.Timestamp)}");
        }

        var haveMapEffects = false;
        foreach (var _ in mapEffects) { haveMapEffects = true; break; }
        foreach (var n in _tree.Node("地图效果", !haveMapEffects))
        {
            if (haveMapEffects)
            {
                foreach (var n2 in _tree.Node("全部"))
                {
                    _tree.LeafNodes(mapEffects, ec => $"{tp(ec.Timestamp)}: {ec.Index:X2} = {ec.State:X8}");
                }
            }
            var mapEffectIndices = new SortedSet<byte>();
            foreach (var ec in mapEffects)
            {
                mapEffectIndices.Add(ec.Index);
            }

            foreach (var index in _tree.Nodes(mapEffectIndices, index => new($"索引 {index:X2}")))
            {
                var filtered = new List<Replay.MapEffect>();
                foreach (var ec in mapEffects)
                {
                    if (ec.Index == index)
                    {
                        filtered.Add(ec);
                    }
                }

                _tree.LeafNodes(filtered, ec => $"{tp(ec.Timestamp)}: {ec.Index:X2} = {ec.State:X8}");
            }
        }

        var haveDirus = false;
        foreach (var _ in dirus) { haveDirus = true; break; }
        foreach (var n in _tree.Node("演出控制更新", !haveDirus))
        {
            if (haveDirus)
            {
                foreach (var n2 in _tree.Node("全部"))
                {
                    _tree.LeafNodes(dirus, d => $"{tp(d.Timestamp)}: {d.UpdateID:X8} [0x{d.Param1:X}, 0x{d.Param2:X}, 0x{d.Param3:X}, 0x{d.Param4:X}]");
                }
            }

            var diruIds = new SortedSet<uint>();
            foreach (var d in dirus)
            {
                diruIds.Add(d.UpdateID);
            }

            foreach (var ix in _tree.Nodes(diruIds, index => new($"ID {index:X4}")))
            {
                var filteredDiru = new List<Replay.DirectorUpdate>();
                foreach (var d in dirus)
                {
                    if (d.UpdateID == ix)
                    {
                        filteredDiru.Add(d);
                    }
                }

                _tree.LeafNodes(filteredDiru, d => $"{tp(d.Timestamp)}: {d.UpdateID:X8} [0x{d.Param1:X}, 0x{d.Param2:X}, 0x{d.Param3:X}, 0x{d.Param4:X}]");
            }
        }
    }

    private void DrawParticipants(IEnumerable<Replay.Participant> list, IEnumerable<Replay.Action> actions, IEnumerable<Replay.Status> statuses, Func<DateTime, string> tp, DateTime reference, Replay.Encounter? filter, Type? aidType, Type? sidType)
    {
        foreach (var p in _tree.Nodes(list, p => new($"{ReplayUtils.ParticipantString(p, p.WorldExistence.Count > 0 ? p.WorldExistence[0].Start : default)}: 首次出现于 {tp(p.EffectiveExistence.Start)}, 最后出现于 {tp(p.EffectiveExistence.End)}")))
        {
            foreach (var n in _tree.Node("存在时间", p.WorldExistence.Count == 0))
            {
                _tree.LeafNodes(p.WorldExistence, r => $"{tp(r.Start)}-{tp(r.End)} ({r})");
            }
            foreach (var n in _tree.Node("施法", p.Casts.Count == 0))
            {
                DrawCasts(p.Casts, reference, aidType);
            }
            foreach (var an in _tree.Node("动作", !p.HasAnyActions))
            {
                var pActions = new List<Replay.Action>();
                foreach (var a in actions)
                {
                    if (a.Source == p)
                    {
                        pActions.Add(a);
                    }
                }

                DrawActions(pActions, tp, aidType);
            }
            foreach (var an in _tree.Node("受动作影响", !p.IsTargetOfAnyActions))
            {
                var pActions = new List<Replay.Action>();
                foreach (var a in actions)
                {
                    for (var ti = 0; ti < a.Targets.Count; ++ti)
                    {
                        if (a.Targets[ti].Target == p) { pActions.Add(a); break; }
                    }
                }
                DrawActions(pActions, tp, aidType);
            }
            foreach (var an in _tree.Node("状态", !p.HasAnyStatuses))
            {
                var pStatuses = new List<Replay.Status>();
                foreach (var s in statuses)
                {
                    if (s.Target == p)
                    {
                        pStatuses.Add(s);
                    }
                }

                DrawStatuses(pStatuses, tp, sidType);
            }
            foreach (var an in _tree.Node("可选中", p.TargetableHistory.Count == 0))
            {
                _tree.LeafNodes(p.TargetableHistory, r => $"{tp(r.Key)} = {r.Value}");
            }
            foreach (var an in _tree.Node("EObj 动画", p.EventObjectAnimation.Count == 0))
            {
                _tree.LeafNodes(p.EventObjectAnimation, r => $"{tp(r.Key)} = {r.Value:X8}");
            }
            foreach (var an in _tree.Node("事件状态", p.EventState.Count == 0))
            {
                _tree.LeafNodes(p.EventState, r => $"{tp(r.Key)} = {r.Value}");
            }
            foreach (var an in _tree.Node("动作时间轴事件", p.ActionTimeline.Count == 0))
            {
                _tree.LeafNodes(p.ActionTimeline, r => $"{tp(r.Key)} = {r.Value:X4}");
            }
        }
    }

    private string CastString(Replay.Cast c, DateTime reference, DateTime prev, Type? aidType) => $"{new Replay.TimeRange(reference, c.Time.Start)} ({new Replay.TimeRange(prev, c.Time.Start)}) + {c.ExpectedCastTime + 0.3f:f2} ({c.Time}): {c.ID} ({aidType?.GetEnumName(c.ID.ID)}) @ {ReplayUtils.ParticipantPosRotString(c.Target, c.Time.Start)} / {Utils.Vec3String(c.Location)} / {c.Rotation}";

    private void DrawCasts(IEnumerable<Replay.Cast> list, DateTime reference, Type? aidType)
    {
        var prev = reference;
        foreach (var c in _tree.Nodes(list, c => new(CastString(c, reference, prev, aidType), true)))
        {
            prev = c.Time.End;
        }
    }

    private string ActionString(Replay.Action a, Func<DateTime, string> tp, Type? aidType) => $"{tp(a.Timestamp)}: {a.ID} ({aidType?.GetEnumName(a.ID.ID)}): {ReplayUtils.ParticipantPosRotString(a.Source, a.Timestamp)} -> {ReplayUtils.ParticipantString(a.MainTarget, a.Timestamp)} {Utils.Vec3String(a.TargetPos)} ({a.Targets.Count} 个受影响) #{a.GlobalSequence}";

    private void DrawActions(IEnumerable<Replay.Action> list, Func<DateTime, string> tp, Type? aidType)
    {
        foreach (var a in _tree.Nodes(list, a => new(ActionString(a, tp, aidType), a.Targets.Count == 0)))
        {
            foreach (var t in _tree.Nodes(a.Targets, t => new(ReplayUtils.ActionTargetString(t, a.Timestamp))))
            {
                _tree.LeafNodes(t.Effects.ValidEffects(), ReplayUtils.ActionEffectString);
            }
        }
    }

    private string StatusString(Replay.Status s, Func<DateTime, string> tp, Type? sidType) => $"{tp(s.Time.Start)} + {s.InitialDuration:f2} / {s.Time}: {Utils.StatusString(s.ID)} ({sidType?.GetEnumName(s.ID)}) ({s.StartingExtra:X}) @ {ReplayUtils.ParticipantString(s.Target, s.Time.Start)} 来源 {ReplayUtils.ParticipantString(s.Source, s.Time.Start)}";

    private void DrawStatuses(IEnumerable<Replay.Status> statuses, Func<DateTime, string> tp, Type? sidType) => _tree.LeafNodes(statuses, s => StatusString(s, tp, sidType));

    private void DrawEncounterDetails(Replay.Encounter enc, Func<DateTime, string> tp)
    {
        foreach (var n in _tree.Node("状态转换", enc.States.Count == 0))
        {
            var enter = enc.Time.Start;
            foreach (var s in _tree.Nodes(enc.States, s => new($"{s.FullName:X}: {tp(enter)} - {tp(s.Exit)} = {new Replay.TimeRange(enter, s.Exit)} (预期 {s.ExpectedDuration:f1})", true)))
            {
                enter = s.Exit;
            }
        }

        foreach (var n in _tree.Node("错误", enc.Errors.Count == 0))
        {
            _tree.LeafNodes(enc.Errors, error => $"{tp(error.Timestamp)} [{error.CompType}] {error.Message}");
        }
    }

    private void DrawUserMarkers()
    {
        foreach (var n in _tree.Node("用户标记", r.UserMarkers.Count == 0))
        {
            _tree.LeafNodes(r.UserMarkers, kv => $"{kv.Key:O}: {kv.Value}");
        }
    }

    private Func<DateTime, string> TimePrinter(DateTime start) => t => new Replay.TimeRange(start, t).ToString();

    private void OpenTimeline(Replay.Encounter enc, BitMask showPlayers) => _ = new ReplayTimelineWindow(r, enc, showPlayers, planDB, timelineSync);

    private void DrawTimelines(Replay.Encounter enc)
    {
        if (ImGui.Button("显示时间轴"))
        {
            OpenTimeline(enc, new());
        }

        ImGui.SameLine();
        for (var i = 0; i < enc.PartyMembers.Count; ++i)
        {
            var (p, c, l) = enc.PartyMembers[i];
            if (ImGui.Button($"{c}{l} {(p.NameHistory.Count > 0 ? p.NameHistory.Values[0].name : "")}"))
            {
                OpenTimeline(enc, new(1u << i));
            }

            ImGui.SameLine();
        }
        if (ImGui.Button("全部"))
        {
            OpenTimeline(enc, new((1u << enc.PartyMembers.Count) - 1));
        }
    }

    private void OpListContextMenu(OpList? list)
    {
        if (list == null)
        {
            return;
        }

        if (ImGui.MenuItem("清除过滤"))
        {
            list.ClearFilters();
        }
        if (ImGui.MenuItem("显示单位大小事件", "", list.ShowActorSizeEvents, true))
        {
            list.ShowActorSizeEvents = !list.ShowActorSizeEvents;
        }
        if (ImGui.MenuItem("显示 CLMV 事件", "", list.ShowCLMVEvents, true))
        {
            list.ShowCLMVEvents = !list.ShowCLMVEvents;
        }
        if (ImGui.MenuItem("弹出窗口"))
        {
            var windowName = $"原始操作: {r.Path}, {(list.Encounter != null ? $"{list.ModuleInfo?.ModuleType.Name}: {list.Encounter.InstanceID:X} @ {list.Encounter.Time.Start} + {list.Encounter.Time}" : "完整")}";
            _ = new UISimpleWindow(windowName, () => list.Draw(new(), list.Encounter?.Time.Start ?? r.Ops[0].Timestamp), true, new(1000, 1000));
        }
    }

    private void IPCListContextMenu(IPCList? list, BossModuleRegistry.Info? moduleInfo)
    {
        if (list == null)
        {
            return;
        }

        if (ImGui.MenuItem("清除过滤"))
        {
            list.ClearFilters();
        }
        if (ImGui.MenuItem("弹出窗口"))
        {
            var windowName = $"服务器 IPC: {r.Path}, {(list.Encounter != null ? $"{moduleInfo?.ModuleType.Name}: {list.Encounter.InstanceID:X} @ {list.Encounter.Time.Start} + {list.Encounter.Time}" : "完整")}";
            _ = new UISimpleWindow(windowName, () => list.Draw(new(), list.Encounter?.Time.Start ?? r.Ops[0].Timestamp), true, new(1000, 1000));
        }
    }
    private static IEnumerable<WorldState.Operation> OpsInRange(List<WorldState.Operation> ops, DateTime start, DateTime end)
    {
        var startIdx = ops.FindIndex(o => o.Timestamp >= start);
        if (startIdx < 0)
        {
            yield break;
        }

        for (var i = startIdx; i < ops.Count; ++i)
        {
            var op = ops[i];
            if (op.Timestamp > end)
            {
                break;
            }

            yield return op;
        }
    }
}
