using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
namespace BossMod;

// AEAssist 联动（2026-08-09 逆向 NiGuangOwO 7.5.5.36 复刻）：订阅 AEAssist 的期望身位（IPC），
// 供 AIHintsBuilder 在 RSR 无身位需求时兜底使用（结构同 RotationSolverRebornModule）。
public sealed class AEAssistModule : IDisposable
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly ICallGateSubscriber<byte> _getDesiredPositional;
    private readonly ICallGateSubscriber<byte, object> _desiredPositionalChanged;
    private const string aeassist = "AEAssistV3";
    private Positional _desiredPositional;

    // 当前 AEAssist 的期望身位（0=None/1=Rear/2=Flank/3=Front），getter 轮询 IPC 并在变化时触发事件
    public Positional DesiredPositional
    {
        get
        {
            var desiredPositional = GetDesiredPositional();
            if (desiredPositional != _desiredPositional)
            {
                _desiredPositional = desiredPositional;
                DesiredPositionalChanged?.Invoke(desiredPositional);
            }
            return _desiredPositional;
        }
    }

    public bool IsInstalled
    {
        get
        {
            foreach (var installedPlugin in _pluginInterface.InstalledPlugins)
            {
                if (installedPlugin.IsLoaded && installedPlugin.InternalName.Equals(aeassist, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }

    public event Action<Positional>? DesiredPositionalChanged;

    public AEAssistModule(IDalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
        _getDesiredPositional = pluginInterface.GetIpcSubscriber<byte>("AEAssist.GetDesiredPositional");
        _desiredPositionalChanged = pluginInterface.GetIpcSubscriber<byte, object>("AEAssist.ActionUpdater.DesiredPositionalChanged");
        try
        {
            _desiredPositionalChanged.Subscribe(OnDesiredPositionalChanged);
        }
        catch
        {
            // AEAssist not installed/loaded yet - ignore, we'll still be able to poll GetDesiredPositional() later
        }
        _desiredPositional = GetDesiredPositional();
    }

    public void Dispose()
    {
        try
        {
            _desiredPositionalChanged.Unsubscribe(OnDesiredPositionalChanged);
        }
        catch
        {
            // ignore
        }
    }

    // polls AEAssist's current desired positional; returns Any if AEAssist is not installed/loaded
    public Positional GetDesiredPositional()
    {
        try
        {
            return MapPositional(_getDesiredPositional.InvokeFunc());
        }
        catch
        {
            return Positional.Any;
        }
    }

    private void OnDesiredPositionalChanged(byte value)
    {
        var positional = MapPositional(value);
        if (positional != _desiredPositional)
        {
            _desiredPositional = positional;
            DesiredPositionalChanged?.Invoke(positional);
        }
    }

    private static Positional MapPositional(byte value) => value switch
    {
        1 => Positional.Rear,
        2 => Positional.Flank,
        3 => Positional.Front,
        _ => Positional.Any,
    };
}
