using System.Text.Json.Nodes;

namespace BossMod.Autorotation;

public static class PlanPresetConverter
{
    public static readonly VersionedJSONSchema PlanSchema = BuildSchema(true);
    public static readonly VersionedJSONSchema PresetSchema = BuildSchema(false);

    private static VersionedJSONSchema BuildSchema(bool plan)
    {
        var res = new VersionedJSONSchema();
        res.Converters.Add((j, _, _) => // v1: StandardWAR -> VeynVAR rename
        {
            Dictionary<string, string> moduleRenames = new() { ["BossMod.Autorotation.StandardWAR"] = "BossMod.Autorotation.VeynWAR" };
            foreach (var m in EnumerateEntriesModules(j, plan))
                RenameKeys(m, moduleRenames);
            return j;
        });
        return res;
    }

    record struct OptionRename(string Module, string Option, string Before, string After);

    // returns always 1 element for plans, or multiple (1 per preset) for preset database
    private static IEnumerable<JsonObject> EnumerateEntriesModules(JsonNode root, bool plan)
    {
        // 键名用 "Modules"（与 JsonPresetConverter 的 nameof(Preset.Modules) 序列化键一致；汉化勿改此协议键）
        // 防御：第三方（如 AutoDuty IPC）传入的 JSON 缺模块键/元素为 null 时跳过，不抛异常
        if (plan)
        {
            if (root?["Modules"] is { } modules)
            {
                yield return modules.AsObject();
            }
        }
        else
        {
            if (root is not JsonArray arr)
            {
                yield break;
            }

            foreach (var preset in arr)
            {
                if (preset?["Modules"] is { } modules)
                {
                    yield return modules.AsObject();
                }
            }
        }
    }

    private static void RenameKeys(JsonObject j, Dictionary<string, string> renames)
    {
        // TODO: net9 - use indexed access to simplify & speed up implementation
        for (int i = 0, cnt = j.Count; i < cnt; ++i)
        {
            var (k, v) = j.First();
            j.Remove(k);
            j.Add(renames.TryGetValue(k, out var renamed) ? renamed : k, v);
        }
    }
}
