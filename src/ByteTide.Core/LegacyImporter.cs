using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ByteTide.Core;

/// <summary>
/// Reads the original "RAS Graph &amp; Stats" .dat archive format.
///
/// Each file is a plain-text INI-like layout:
///
///   [39630]
///   Broadcom 440x ... Miniport @ 00-11-43-68-28-0A=86124
///   Hours=86124
///   Recv=1805957090
///   Broadcom 440x ... Miniport @ 00-11-43-68-28-0A-Recv=1805957090
///   Xmit=88092758
///   Broadcom 440x ... Miniport @ 00-11-43-68-28-0A-Xmit=88092758
///
/// - "[N]" is an OLE Automation date serial (days since 1899-12-30),
///   the same epoch DateTime.FromOADate uses.
/// - Within a section, each adapter's Hours/Recv/Xmit are written twice:
///   once as a bare "Hours=/Recv=/Xmit=" key (last-adapter-wins, unreliable
///   when multiple adapters appear in the same day) and once namespaced as
///   "&lt;Name&gt; @ &lt;MAC&gt;[-Recv|-Xmit]=&lt;value&gt;". Only the namespaced
///   form is parsed here, since it's unambiguous per adapter.
/// - A single file can span many days and is not necessarily contiguous
///   (days with no connection are simply absent).
/// </summary>
public static class LegacyImporter
{
    private static readonly Regex SectionRegex = new(@"^\[(\d+)\]$", RegexOptions.Compiled);

    private static readonly Regex EntryRegex = new(
        @"^(?<name>.+) @ (?<mac>[0-9A-Fa-f]{2}(?:-[0-9A-Fa-f]{2}){5})(?<suffix>-Recv|-Xmit)?=(?<val>-?\d+)$",
        RegexOptions.Compiled);

    public static List<DailyStat> ParseFile(string path)
    {
        var results = new List<DailyStat>();

        // The original app was written in an era of ANSI/Latin-1 text files;
        // Latin1 also happily round-trips plain ASCII, which is all that
        // ever appears here in practice (adapter names, digits, MAC bytes).
        var lines = File.ReadAllLines(path, Encoding.Latin1);

        DateOnly? currentDate = null;
        var adapters = new Dictionary<(string Name, string Mac), (long Hours, long Recv, long Xmit)>();

        void Flush()
        {
            if (currentDate is null) return;
            foreach (var kv in adapters)
            {
                results.Add(new DailyStat
                {
                    Date = currentDate.Value,
                    AdapterName = kv.Key.Name,
                    MacAddress = kv.Key.Mac,
                    SecondsOnline = kv.Value.Hours,
                    BytesReceived = kv.Value.Recv,
                    BytesSent = kv.Value.Xmit
                });
            }
            adapters.Clear();
        }

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            var sectionMatch = SectionRegex.Match(line);
            if (sectionMatch.Success)
            {
                Flush();
                double serial = double.Parse(sectionMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                currentDate = DateOnly.FromDateTime(DateTime.FromOADate(serial));
                continue;
            }

            if (currentDate is null) continue;

            var entryMatch = EntryRegex.Match(line);
            if (!entryMatch.Success) continue;

            var name = entryMatch.Groups["name"].Value;
            var mac = entryMatch.Groups["mac"].Value;
            var suffix = entryMatch.Groups["suffix"].Value;
            var value = long.Parse(entryMatch.Groups["val"].Value, CultureInfo.InvariantCulture);

            var key = (name, mac);
            adapters.TryGetValue(key, out var cur);

            adapters[key] = suffix switch
            {
                "-Recv" => (cur.Hours, value, cur.Xmit),
                "-Xmit" => (cur.Hours, cur.Recv, value),
                _ => (value, cur.Recv, cur.Xmit) // no suffix => the Hours-online value
            };
        }

        Flush();
        return results;
    }

    /// <summary>
    /// Imports every .dat file in a folder (non-recursive) into storage.
    /// Safe to re-run: rows are upserted by (date, adapter, mac).
    /// </summary>
    public static ImportSummary ImportFolder(string folder, Storage storage)
    {
        int filesProcessed = 0, recordsImported = 0, filesFailed = 0;

        foreach (var file in Directory.EnumerateFiles(folder, "*.dat"))
        {
            try
            {
                var stats = ParseFile(file);
                foreach (var s in stats)
                {
                    storage.UpsertDailyStat(s);
                    recordsImported++;
                }
                filesProcessed++;
            }
            catch
            {
                filesFailed++;
            }
        }

        return new ImportSummary(filesProcessed, recordsImported, filesFailed);
    }
}
