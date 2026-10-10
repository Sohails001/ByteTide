namespace ByteTide.Core;

/// <summary>
/// Logs traffic rates to a plain text or CSV file at a configurable
/// interval, for one adapter or all combined -- equivalent to the original
/// app's "Traffic Rates Recorder".
/// </summary>
public sealed class RatesRecorder : IDisposable
{
    private readonly NetworkSampler _sampler;
    private readonly string? _adapterId; // null = all adapters combined
    private readonly TimeSpan _interval;
    private readonly string _filePath;
    private readonly bool _csv;

    private readonly List<(double Down, double Up)> _buffer = new();
    private DateTime _lastWrite = DateTime.MinValue;
    private bool _headerWritten;

    public RatesRecorder(NetworkSampler sampler, string? adapterId, TimeSpan interval, string filePath, bool csv)
    {
        _sampler = sampler;
        _adapterId = adapterId;
        _interval = interval;
        _filePath = filePath;
        _csv = csv;
        _sampler.SamplesReady += OnSamples;
    }

    private void OnSamples(IReadOnlyList<LiveSample> samples)
    {
        var relevant = _adapterId is null ? samples : samples.Where(s => s.AdapterId == _adapterId).ToList();
        if (relevant.Count == 0) return;

        double down = relevant.Sum(s => s.DownloadBps);
        double up = relevant.Sum(s => s.UploadBps);
        _buffer.Add((down, up));

        var now = DateTime.Now;
        if (_lastWrite == DateTime.MinValue) _lastWrite = now;
        if (now - _lastWrite < _interval) return;

        double avgDown = _buffer.Average(b => b.Down);
        double avgUp = _buffer.Average(b => b.Up);
        _buffer.Clear();
        _lastWrite = now;

        WriteRow(now, avgDown, avgUp);
    }

    private void WriteRow(DateTime ts, double down, double up)
    {
        try
        {
            if (_csv && !_headerWritten && !File.Exists(_filePath))
                File.AppendAllText(_filePath, "Timestamp,DownloadBytesPerSec,UploadBytesPerSec\r\n");
            _headerWritten = true;

            var line = _csv
                ? $"{ts:yyyy-MM-dd HH:mm:ss},{down:0},{up:0}"
                : $"{ts:yyyy-MM-dd HH:mm:ss}  Down: {FormatRate(down)}  Up: {FormatRate(up)}";

            File.AppendAllText(_filePath, line + Environment.NewLine);
        }
        catch
        {
            // Best-effort logging -- a transient file lock or permissions
            // issue shouldn't take down live monitoring.
        }
    }

    private static string FormatRate(double bytesPerSec)
    {
        if (bytesPerSec >= 1024 * 1024) return $"{bytesPerSec / (1024 * 1024):0.00} MB/s";
        if (bytesPerSec >= 1024) return $"{bytesPerSec / 1024:0.0} KB/s";
        return $"{bytesPerSec:0} B/s";
    }

    public void Dispose() => _sampler.SamplesReady -= OnSamples;
}
