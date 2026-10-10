using ByteTide.Core;

namespace ByteTide.App;

/// <summary>
/// Tabbed traffic reports window: Summary, Daily, Weekly, Monthly, and
/// Yearly breakdowns -- all filterable by adapter, sortable by clicking any
/// column header (click again to flip direction), with CSV export and a
/// clear-history action.
/// </summary>
public sealed class ReportsForm : Form
{
    /// <summary>One row of report data. SortKey drives chronological sort
    /// for the period column; the byte/second fields drive numeric sorts
    /// for the other columns.</summary>
    private sealed record ReportRow(string Period, DateTime SortKey, long BytesDown, long BytesUp, long SecondsOnline);

    private sealed class GridState
    {
        public List<ReportRow> Rows = new();
        public string SortColumn = "Period";
        public bool Ascending = false; // newest/largest first by default
    }

    private readonly Storage _storage;
    private readonly Dictionary<DataGridView, GridState> _gridStates = new();

    private readonly ComboBox _adapterFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };

    private readonly DataGridView _summaryGrid = MakeGrid();
    private readonly DataGridView _dailyGrid = MakeGrid();
    private readonly DataGridView _weeklyGrid = MakeGrid();
    private readonly DataGridView _monthlyGrid = MakeGrid();
    private readonly DataGridView _yearlyGrid = MakeGrid();

    private readonly Button _export = new() { Text = "Export..." };
    private readonly Button _clear = new() { Text = "Clear..." };
    private readonly Button _close = new() { Text = "Close" };

    public ReportsForm(Storage storage)
    {
        _storage = storage;

        Text = "Traffic Reports";
        Width = 840;
        Height = 560;
        StartPosition = FormStartPosition.CenterParent;

        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8)
        };
        top.Controls.Add(new Label { Text = "Adapter:", AutoSize = true, Padding = new Padding(0, 6, 6, 0) });
        top.Controls.Add(_adapterFilter);
        _adapterFilter.SelectedIndexChanged += (_, _) => LoadAll();

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8),
            FlowDirection = FlowDirection.RightToLeft
        };
        bottom.Controls.Add(_close);
        bottom.Controls.Add(_clear);
        bottom.Controls.Add(_export);
        _close.Click += (_, _) => Close();
        _export.Click += (_, _) => ExportCurrentTab();
        _clear.Click += (_, _) => ClearHistory();

        SetupGrid(_summaryGrid, "Period");
        SetupGrid(_dailyGrid, "Date");
        SetupGrid(_weeklyGrid, "Week");
        SetupGrid(_monthlyGrid, "Month");
        SetupGrid(_yearlyGrid, "Year");

        _tabs.TabPages.Add(MakeTabPage("Summary", _summaryGrid));
        _tabs.TabPages.Add(MakeTabPage("Daily Report", _dailyGrid));
        _tabs.TabPages.Add(MakeTabPage("Weekly Report", _weeklyGrid));
        _tabs.TabPages.Add(MakeTabPage("Monthly Report", _monthlyGrid));
        _tabs.TabPages.Add(MakeTabPage("Yearly Report", _yearlyGrid));

        Controls.Add(_tabs);
        Controls.Add(bottom);
        Controls.Add(top);

        PopulateAdapterFilter();
        LoadAll();
    }

    private static DataGridView MakeGrid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AutoGenerateColumns = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
    };

    /// <summary>
    /// Defines the shared column layout -- Period / DL / UL / DL & UL / Time
    /// -- and wires up click-to-sort on every column header.
    /// </summary>
    private void SetupGrid(DataGridView grid, string periodHeader)
    {
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Period", HeaderText = periodHeader });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "DL", HeaderText = "DL" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "UL", HeaderText = "UL" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Both", HeaderText = "DL & UL" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Time", HeaderText = "Time" });

        _gridStates[grid] = new GridState();
        grid.ColumnHeaderMouseClick += (_, e) => OnColumnHeaderClick(grid, e);
    }

    private void OnColumnHeaderClick(DataGridView grid, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex < 0) return;
        var colName = grid.Columns[e.ColumnIndex].Name;
        var state = _gridStates[grid];

        if (state.SortColumn == colName)
            state.Ascending = !state.Ascending;
        else
        {
            state.SortColumn = colName;
            state.Ascending = true;
        }

        RenderGrid(grid, state);
    }

    private void RenderGrid(DataGridView grid, GridState state)
    {
        IEnumerable<ReportRow> sorted = state.SortColumn switch
        {
            "DL" => state.Ascending ? state.Rows.OrderBy(r => r.BytesDown) : state.Rows.OrderByDescending(r => r.BytesDown),
            "UL" => state.Ascending ? state.Rows.OrderBy(r => r.BytesUp) : state.Rows.OrderByDescending(r => r.BytesUp),
            "Both" => state.Ascending ? state.Rows.OrderBy(r => r.BytesDown + r.BytesUp) : state.Rows.OrderByDescending(r => r.BytesDown + r.BytesUp),
            "Time" => state.Ascending ? state.Rows.OrderBy(r => r.SecondsOnline) : state.Rows.OrderByDescending(r => r.SecondsOnline),
            _ => state.Ascending ? state.Rows.OrderBy(r => r.SortKey) : state.Rows.OrderByDescending(r => r.SortKey)
        };

        grid.Rows.Clear();
        foreach (var r in sorted)
        {
            grid.Rows.Add(
                r.Period,
                FormatBytes(r.BytesDown),
                FormatBytes(r.BytesUp),
                FormatBytes(r.BytesDown + r.BytesUp),
                FormatDuration(r.SecondsOnline));
        }
    }

    private static TabPage MakeTabPage(string title, Control grid)
    {
        var page = new TabPage(title);
        grid.Dock = DockStyle.Fill;
        page.Controls.Add(grid);
        return page;
    }

    private void PopulateAdapterFilter()
    {
        _adapterFilter.Items.Clear();
        _adapterFilter.Items.Add("All Connections");
        foreach (var name in _storage.GetKnownAdapterNames())
            _adapterFilter.Items.Add(name);
        _adapterFilter.SelectedIndex = 0;
    }

    private string? SelectedAdapterName =>
        _adapterFilter.SelectedIndex <= 0 ? null : _adapterFilter.SelectedItem?.ToString();

    private List<DailyStat> LoadFilteredStats(DateOnly from, DateOnly to)
    {
        var stats = _storage.GetDailyStats(from, to);
        var adapter = SelectedAdapterName;
        return adapter is null ? stats : stats.Where(s => s.AdapterName == adapter).ToList();
    }

    private void LoadAll()
    {
        LoadSummary();
        LoadDaily();
        LoadWeekly();
        LoadMonthly();
        LoadYearly();
    }

    private void LoadSummary()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
        var startOfMonth = new DateOnly(today.Year, today.Month, 1);
        var startOfYear = new DateOnly(today.Year, 1, 1);
        var earliest = new DateOnly(1980, 1, 1);

        var rows = new List<ReportRow>
        {
            SummaryRow("Today", today, today, DateTime.MaxValue),
            SummaryRow("This Week", startOfWeek, today, DateTime.MaxValue.AddDays(-1)),
            SummaryRow("This Month", startOfMonth, today, DateTime.MaxValue.AddDays(-2)),
            SummaryRow("This Year", startOfYear, today, DateTime.MaxValue.AddDays(-3)),
            SummaryRow("Total", earliest, today, DateTime.MaxValue.AddDays(-4))
        };

        var state = _gridStates[_summaryGrid];
        state.Rows = rows;
        RenderGrid(_summaryGrid, state);
    }

    // sortKey lets "Today/This Week/This Month/This Year/Total" keep a sensible
    // fixed order when sorted by period, since they aren't real calendar dates.
    private ReportRow SummaryRow(string period, DateOnly from, DateOnly to, DateTime sortKey)
    {
        var stats = LoadFilteredStats(from, to);
        return new ReportRow(
            period,
            sortKey,
            stats.Sum(s => s.BytesReceived),
            stats.Sum(s => s.BytesSent),
            stats.Sum(s => s.SecondsOnline));
    }

    private void LoadDaily()
    {
        var stats = LoadFilteredStats(new DateOnly(1980, 1, 1), DateOnly.FromDateTime(DateTime.Today));

        var rows = stats
            .GroupBy(s => s.Date)
            .Select(g => new ReportRow(
                g.Key.ToString("yyyy-MM-dd"),
                g.Key.ToDateTime(TimeOnly.MinValue),
                g.Sum(x => x.BytesReceived),
                g.Sum(x => x.BytesSent),
                g.Sum(x => x.SecondsOnline)))
            .ToList();

        var state = _gridStates[_dailyGrid];
        state.Rows = rows;
        RenderGrid(_dailyGrid, state);
    }

    private void LoadWeekly()
    {
        var stats = LoadFilteredStats(new DateOnly(1980, 1, 1), DateOnly.FromDateTime(DateTime.Today));

        var rows = stats
            .GroupBy(s => s.Date.AddDays(-(int)s.Date.DayOfWeek)) // bucket by start-of-week (Sunday)
            .Select(g => new ReportRow(
                $"{g.Key:yyyy-MM-dd} -- {g.Key.AddDays(6):yyyy-MM-dd}",
                g.Key.ToDateTime(TimeOnly.MinValue),
                g.Sum(x => x.BytesReceived),
                g.Sum(x => x.BytesSent),
                g.Sum(x => x.SecondsOnline)))
            .ToList();

        var state = _gridStates[_weeklyGrid];
        state.Rows = rows;
        RenderGrid(_weeklyGrid, state);
    }

    private void LoadMonthly()
    {
        var stats = LoadFilteredStats(new DateOnly(1980, 1, 1), DateOnly.FromDateTime(DateTime.Today));

        var rows = stats
            .GroupBy(s => new DateOnly(s.Date.Year, s.Date.Month, 1))
            .Select(g => new ReportRow(
                g.Key.ToString("yyyy-MM"),
                g.Key.ToDateTime(TimeOnly.MinValue),
                g.Sum(x => x.BytesReceived),
                g.Sum(x => x.BytesSent),
                g.Sum(x => x.SecondsOnline)))
            .ToList();

        var state = _gridStates[_monthlyGrid];
        state.Rows = rows;
        RenderGrid(_monthlyGrid, state);
    }

    private void LoadYearly()
    {
        var stats = LoadFilteredStats(new DateOnly(1980, 1, 1), DateOnly.FromDateTime(DateTime.Today));

        var rows = stats
            .GroupBy(s => s.Date.Year)
            .Select(g => new ReportRow(
                g.Key.ToString(),
                new DateTime(g.Key, 1, 1),
                g.Sum(x => x.BytesReceived),
                g.Sum(x => x.BytesSent),
                g.Sum(x => x.SecondsOnline)))
            .ToList();

        var state = _gridStates[_yearlyGrid];
        state.Rows = rows;
        RenderGrid(_yearlyGrid, state);
    }

    private DataGridView CurrentGrid() => _tabs.SelectedTab?.Controls[0] as DataGridView ?? _summaryGrid;

    private void ExportCurrentTab()
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv",
            FileName = $"{_tabs.SelectedTab?.Text.Replace(" ", "")}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var grid = CurrentGrid();
        try
        {
            using var writer = new StreamWriter(dlg.FileName, false);

            var headers = grid.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText);
            writer.WriteLine(string.Join(",", headers));

            foreach (DataGridViewRow row in grid.Rows)
            {
                var cells = row.Cells.Cast<DataGridViewCell>().Select(c => EscapeCsv(c.Value?.ToString() ?? ""));
                writer.WriteLine(string.Join(",", cells));
            }

            MessageBox.Show(this, "Export complete.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Export failed: {ex.Message}", "Export", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private void ClearHistory()
    {
        var adapter = SelectedAdapterName;
        var target = adapter ?? "ALL adapters";

        var result = MessageBox.Show(
            this,
            $"This will permanently delete all stored history for {target}.\n\nThis cannot be undone. Continue?",
            "Clear Traffic History",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes) return;

        _storage.DeleteDailyStats(adapter);
        PopulateAdapterFilter();
        LoadAll();
    }

    private static string FormatBytes(long bytes)
    {
        double b = bytes;
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        while (b >= 1024 && i < units.Length - 1)
        {
            b /= 1024;
            i++;
        }
        return $"{b:0.##} {units[i]}";
    }

    /// <summary>Formats total seconds as H:MM:SS with unbounded hours (e.g. "2270:59:17"
    /// for a full year of uptime), matching the reference report's Time column.</summary>
    private static string FormatDuration(long totalSeconds)
    {
        long hours = totalSeconds / 3600;
        long minutes = (totalSeconds % 3600) / 60;
        long seconds = totalSeconds % 60;
        return $"{hours}:{minutes:00}:{seconds:00}";
    }
}
