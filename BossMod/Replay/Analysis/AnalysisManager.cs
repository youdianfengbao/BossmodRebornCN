using Dalamud.Bindings.ImGui;

namespace BossMod.ReplayAnalysis;

sealed class AnalysisManager : IDisposable
{
    private class Lazy<T>(Func<T> init)
    {
        private readonly Func<T> _init = init;
        private T? _impl;

        public T Get() => _impl ??= _init();
    }

    private sealed class Global(List<Replay> replays)
    {
        private readonly Lazy<UnknownActionEffects> _unkEffects = new(() => new(replays));
        private readonly Lazy<ParticipantInfo> _participantInfo = new(() => new(replays));
        private readonly Lazy<AbilityInfo> _abilityInfo = new(() => new(replays));
        private readonly Lazy<ClassDefinitions> _classDefinitions = new(() => new(replays));
        private readonly Lazy<ClientActions> _clientActions = new(() => new(replays));
        private readonly Lazy<EffectResultMispredict> _effectResultMissing = new(() => new(replays, true));
        private readonly Lazy<EffectResultMispredict> _effectResultUnexpected = new(() => new(replays, false));
        private readonly Lazy<EffectResultReorder> _effectResultReorder = new(() => new(replays));

        public void Draw(UITree tree)
        {
            foreach (var n in tree.Node("未知动作效果"))
            {
                _unkEffects.Get().Draw(tree);
            }

            foreach (var n in tree.Node("参与者信息", false, Colors.TextColor1, () => _participantInfo.Get().DrawContextMenu()))
            {
                _participantInfo.Get().Draw(tree);
            }

            foreach (var n in tree.Node("技能信息", false, Colors.TextColor1, () => _abilityInfo.Get().DrawContextMenu()))
            {
                _abilityInfo.Get().Draw(tree);
            }

            foreach (var n in tree.Node("玩家职业定义"))
            {
                _classDefinitions.Get().Draw(tree);
            }

            foreach (var n in tree.Node("客户端动作异常"))
            {
                _clientActions.Get().Draw(tree);
            }

            foreach (var n in tree.Node("效果结果：缺少确认"))
            {
                _effectResultMissing.Get().Draw(tree);
            }

            foreach (var n in tree.Node("效果结果：意外确认"))
            {
                _effectResultUnexpected.Get().Draw(tree);
            }

            foreach (var n in tree.Node("效果结果：乱序"))
            {
                _effectResultReorder.Get().Draw(tree);
            }
        }
    }

    private class PerEncounter
    {
        private readonly Lazy<StateTransitionTimings> _transitionTimings;
        private readonly Lazy<ParticipantInfo> _participantInfo;
        private readonly Lazy<AbilityInfo> _abilityInfo;
        private readonly Lazy<StatusInfo> _statusInfo;
        private readonly Lazy<IconInfo> _iconInfo;
        private readonly Lazy<TetherInfo> _tetherInfo;
        private readonly Lazy<MapEffectInfo> _mapEffectInfo;
        private readonly Lazy<DirectorInfo> _directorInfo;
        private readonly Lazy<ArenaBounds> _arenaBounds;
        private readonly Lazy<TEASpecific>? _teaSpecific;
        private readonly Lazy<TOPSpecific>? _topSpecific;

        public PerEncounter(List<Replay> replays, uint oid)
        {
            _transitionTimings = new(() => new(replays, oid));
            _participantInfo = new(() => new(replays, oid));
            _abilityInfo = new(() => new(replays, oid));
            _statusInfo = new(() => new(replays, oid));
            _iconInfo = new(() => new(replays, oid));
            _tetherInfo = new(() => new(replays, oid));
            _mapEffectInfo = new(() => new(replays, oid));
            _directorInfo = new(() => new(replays, oid));
            _arenaBounds = new(() => new(replays, oid));
            if (oid == (uint)Shadowbringers.Ultimate.TEA.OID.BossP1)
            {
                _teaSpecific = new(() => new(replays, oid));
            }

            if (oid == (uint)Endwalker.Ultimate.TOP.OID.Boss)
            {
                _topSpecific = new(() => new(replays, oid));
            }
        }

        public void Draw(UITree tree)
        {
            foreach (var n in tree.Node("状态转换时间"))
            {
                _transitionTimings.Get().Draw(tree);
            }

            foreach (var n in tree.Node("参与者信息", false, Colors.TextColor1, () => _participantInfo.Get().DrawContextMenu()))
            {
                _participantInfo.Get().Draw(tree);
            }

            foreach (var n in tree.Node("技能信息", false, Colors.TextColor1, () => _abilityInfo.Get().DrawContextMenu()))
            {
                _abilityInfo.Get().Draw(tree);
            }

            foreach (var n in tree.Node("状态信息", false, Colors.TextColor1, () => _statusInfo.Get().DrawContextMenu()))
            {
                _statusInfo.Get().Draw(tree);
            }

            foreach (var n in tree.Node("标记信息", false, Colors.TextColor1, () => _iconInfo.Get().DrawContextMenu()))
            {
                _iconInfo.Get().Draw(tree);
            }

            foreach (var n in tree.Node("连线信息", false, Colors.TextColor1, () => _tetherInfo.Get().DrawContextMenu()))
            {
                _tetherInfo.Get().Draw(tree);
            }

            foreach (var n in tree.Node("地图效果信息", false, Colors.TextColor1))
            {
                _mapEffectInfo.Get().Draw(tree);
            }

            foreach (var n in tree.Node("演出控制更新信息", false, Colors.TextColor1))
            {
                _directorInfo.Get().Draw(tree);
            }

            foreach (var n in tree.Node("场地边界", false, Colors.TextColor1, () => _arenaBounds.Get().DrawContextMenu()))
            {
                _arenaBounds.Get().Draw(tree);
            }

            if (_teaSpecific != null)
            {
                foreach (var n in tree.Node("TEA 专属分析"))
                {
                    _teaSpecific.Get().Draw(tree);
                }
            }

            if (_topSpecific != null)
            {
                foreach (var n in tree.Node("TOP 专属分析"))
                {
                    _topSpecific.Get().Draw(tree);
                }
            }
        }
    }

    private readonly List<Replay> _replays;
    private readonly Global _global;
    private readonly Dictionary<uint, PerEncounter> _perEncounter = []; // key = encounter OID
    private readonly UITree _tree = new();

    public AnalysisManager(List<Replay> replays)
    {
        _replays = replays;
        _global = new(_replays);
        InitEncounters();
    }

    public void Dispose()
    {
    }

    public void Draw()
    {
        ImGui.TextUnformatted($"发现 {_replays.Count} 个日志");
        foreach (var n in _tree.Node("全局分析"))
        {
            _global.Draw(_tree);
        }
        foreach (var n in _tree.Nodes(_perEncounter, kv => new($"战斗分析: {kv.Key:X} ({BossModuleRegistry.FindByOID(kv.Key)?.ModuleType.Name})")))
        {
            n.Value.Draw(_tree);
        }
    }

    private void InitEncounters()
    {
        foreach (var replay in _replays)
        {
            foreach (var e in replay.Encounters)
            {
                if (!_perEncounter.ContainsKey(e.OID))
                {
                    _perEncounter[e.OID] = new(_replays, e.OID);
                }
            }
        }
    }
}
