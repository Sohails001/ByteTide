using Microsoft.Data.Sqlite;

namespace ByteTide.Core;

/// <summary>
/// Single-file SQLite store for both live rollups and imported legacy
/// history. All public methods are serialized behind a lock since the
/// sampler timer and the UI can call in concurrently.
/// </summary>
public sealed class Storage : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly object _lock = new();

    public Storage(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        _conn = new SqliteConnection($"Data Source={dbPath}");
        _conn.Open();
        Initialize();
    }

    private void Initialize()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS daily_stats (
                date TEXT NOT NULL,
                adapter_name TEXT NOT NULL,
                mac TEXT NOT NULL,
                seconds_online INTEGER NOT NULL,
                bytes_received INTEGER NOT NULL,
                bytes_sent INTEGER NOT NULL,
                PRIMARY KEY (date, adapter_name, mac)
            );

            CREATE TABLE IF NOT EXISTS live_samples (
                ts TEXT NOT NULL,
                adapter_id TEXT NOT NULL,
                bytes_received INTEGER NOT NULL,
                bytes_sent INTEGER NOT NULL,
                down_bps REAL NOT NULL,
                up_bps REAL NOT NULL
            );

            CREATE TABLE IF NOT EXISTS app_settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_daily_date ON daily_stats(date);
            CREATE INDEX IF NOT EXISTS idx_live_ts ON live_samples(ts);
            """;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Generic settings store, used for persisting view/window preferences
    /// and notification config as JSON blobs, so every "remember this"
    /// feature shares one small table instead of its own file.
    /// </summary>
    public void SetSetting(string key, string value)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO app_settings (key, value) VALUES ($key, $value)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value
                """;
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", value);
            cmd.ExecuteNonQuery();
        }
    }

    public string? GetSetting(string key)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM app_settings WHERE key = $key";
            cmd.Parameters.AddWithValue("$key", key);
            return cmd.ExecuteScalar() as string;
        }
    }

    public void UpsertDailyStat(DailyStat s)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO daily_stats (date, adapter_name, mac, seconds_online, bytes_received, bytes_sent)
                VALUES ($date, $name, $mac, $sec, $rx, $tx)
                ON CONFLICT(date, adapter_name, mac) DO UPDATE SET
                    seconds_online = excluded.seconds_online,
                    bytes_received = excluded.bytes_received,
                    bytes_sent = excluded.bytes_sent
                """;
            cmd.Parameters.AddWithValue("$date", s.Date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("$name", s.AdapterName);
            cmd.Parameters.AddWithValue("$mac", s.MacAddress);
            cmd.Parameters.AddWithValue("$sec", s.SecondsOnline);
            cmd.Parameters.AddWithValue("$rx", s.BytesReceived);
            cmd.Parameters.AddWithValue("$tx", s.BytesSent);
            cmd.ExecuteNonQuery();
        }
    }

    public void InsertLiveSample(LiveSample s)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO live_samples (ts, adapter_id, bytes_received, bytes_sent, down_bps, up_bps)
                VALUES ($ts, $id, $rx, $tx, $down, $up)
                """;
            cmd.Parameters.AddWithValue("$ts", s.Timestamp.ToString("O"));
            cmd.Parameters.AddWithValue("$id", s.AdapterId);
            cmd.Parameters.AddWithValue("$rx", s.BytesReceived);
            cmd.Parameters.AddWithValue("$tx", s.BytesSent);
            cmd.Parameters.AddWithValue("$down", s.DownloadBps);
            cmd.Parameters.AddWithValue("$up", s.UploadBps);
            cmd.ExecuteNonQuery();
        }
    }

    public void PruneLiveSamplesOlderThan(TimeSpan age)
    {
        lock (_lock)
        {
            var cutoff = DateTime.Now - age;
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM live_samples WHERE ts < $cutoff";
            cmd.Parameters.AddWithValue("$cutoff", cutoff.ToString("O"));
            cmd.ExecuteNonQuery();
        }
    }

    public List<DailyStat> GetDailyStats(DateOnly from, DateOnly to)
    {
        lock (_lock)
        {
            var list = new List<DailyStat>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                SELECT date, adapter_name, mac, seconds_online, bytes_received, bytes_sent
                FROM daily_stats
                WHERE date BETWEEN $from AND $to
                ORDER BY date
                """;
            cmd.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("$to", to.ToString("yyyy-MM-dd"));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new DailyStat
                {
                    Date = DateOnly.Parse(reader.GetString(0)),
                    AdapterName = reader.GetString(1),
                    MacAddress = reader.GetString(2),
                    SecondsOnline = reader.GetInt64(3),
                    BytesReceived = reader.GetInt64(4),
                    BytesSent = reader.GetInt64(5)
                });
            }
            return list;
        }
    }

    /// <summary>
    /// Distinct adapter names seen anywhere in history (live or imported),
    /// used to populate the adapter picker / report filter.
    /// </summary>
    public List<string> GetKnownAdapterNames()
    {
        lock (_lock)
        {
            var names = new List<string>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT adapter_name FROM daily_stats ORDER BY adapter_name";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) names.Add(reader.GetString(0));
            return names;
        }
    }

    /// <summary>
    /// Deletes daily_stats rows. Pass null for adapterName to clear everything,
    /// or a specific name to clear just that adapter's history.
    /// </summary>
    public void DeleteDailyStats(string? adapterName)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            if (adapterName is null)
            {
                cmd.CommandText = "DELETE FROM daily_stats";
            }
            else
            {
                cmd.CommandText = "DELETE FROM daily_stats WHERE adapter_name = $name";
                cmd.Parameters.AddWithValue("$name", adapterName);
            }
            cmd.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _conn.Dispose();
        }
    }
}
