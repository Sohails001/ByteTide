using System.Text.Json;
using ByteTide.Core;

namespace ByteTide.App;

/// <summary>Remembers which adapters the user chose to count (or not) in "All Connections".</summary>
internal static class AdapterSettings
{
    private const string ExcludedKey = "excludedAdapterIds";
    private const string IncludedKey = "includedAdapterIds";

    public static void Load(Storage storage, NetworkSampler sampler)
    {
        try
        {
            var excluded = storage.GetSetting(ExcludedKey);
            if (excluded is not null)
                sampler.SetExcludedAdapterIds(JsonSerializer.Deserialize<List<string>>(excluded) ?? new List<string>());

            var included = storage.GetSetting(IncludedKey);
            if (included is not null)
                sampler.SetForcedIncludedAdapterIds(JsonSerializer.Deserialize<List<string>>(included) ?? new List<string>());
        }
        catch
        {
            // Bad or missing settings just mean "use the defaults".
        }
    }

    public static void Save(Storage storage, NetworkSampler sampler)
    {
        try
        {
            storage.SetSetting(ExcludedKey, JsonSerializer.Serialize(sampler.ExcludedAdapterIds.ToList()));
            storage.SetSetting(IncludedKey, JsonSerializer.Serialize(sampler.ForcedIncludedAdapterIds.ToList()));
        }
        catch
        {
            // Preferences are best-effort.
        }
    }
}
