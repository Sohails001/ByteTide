using ByteTide.Core;

namespace ByteTide.App;

/// <summary>
/// Traffic reports: Summary, Daily, Weekly, Monthly and Yearly breakdowns,
/// filterable by adapter and sortable by clicking any column header, with CSV
/// export, a way to delete bad days, and a clear-history action.
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

    private readonly ComboBox _adapterFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260, FlatStyle = FlatStyle.Flat };

    private readonly DataGridView _summaryGrid = MakeGrid();
    private readonly DataGridView _dailyGrid = MakeGrid();
    private readonly DataGridView _weeklyGrid = MakeGrid();
    private readonly DataGridView _monthlyGrid = MakeGrid();
    private readonly DataGridView _yearlyGrid = MakeGrid();

    private readonly List<(string Title, DataGridView Grid, Button Tab)> _tabs = new();
    private int _selectedTab = 1; // open on Daily, like the original program

    private readonly Button _export = new() { Text = "Export CSV..." };
    private readonly Button _deleteDays = new() { Text = "Delete days..." };
    private readonly Button _clear = new() { Text = "Clear all..." };
    private readonly Button _close = new() { Text = "Close" };

    public ReportsForm(Storage storage)
    {
        _storage = storage;

        Text = "Traffic Reports";
        ClientSize = new Size(900, 600);
        MinimumSize = new Size(720, 420);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Surface;
        Font = Theme.Ui();

        // ----- Header: title + adapter picker
        var header = new Panel { Dock = DockStyle.Top, Height = 74, BackColor = Theme.Surface };
        var title = new Label
        {
            Text = "Traffic Reports",
            Font = Theme.UiSemibold(16f),
            ForeColor = Theme.TextPrimary,
            AutoSize = true,
            Location = new Point(20, 12)
        };
        var subtitle = new Label
        {
            Text = "Time is the longest connected time among your adapters each day.",
            ForeColor = Theme.TextMuted,
            Font = Theme.Ui(8.5f),
            AutoSize = true,
            Location = new Point(22, 46)
        };
        var adapterLabel = new Label
        {
            Text = "Adapter",
            ForeColor = Theme.TextMuted,
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(ClientSize.Width - 340, 24)
        };
        _adapterFilter.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _adapterFilter.Location = new Point(ClientSize.Width - 280, 20);
        _adapterFilter.SelectedIndexChanged += (_, _) => LoadAll();
        header.Controls.AddRange(new Control[] { title, subtitle, adapterLabel, _adapterFilter });

        // ----- Tab strip
        var tabBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Theme.Surface,
            Padding = new Padding(14, 4, 0, 0),
            WrapContents = false
        };
        tabBar.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, 0, tabBar.Height - 1, tabBar.Width, tabBar.Height - 1);
        };

        // ----- Content: one grid per tab, only the selected one visible
        var content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(0) };

        SetupGrid(_summaryGrid, "Period");
        SetupGrid(_dailyGrid, "Date");
        SetupGrid(_weeklyGrid, "Week");
        SetupGrid(_monthlyGrid, "Month");
        SetupGrid(_yearlyGrid, "Year");

        AddTab(tabBar, content, "Summary", _summaryGrid);
        AddTab(tabBar, content, "Daily", _dailyGrid);
        AddTab(tabBar, content, "Weekly", _weeklyGrid);
        AddTab(tabBar, content, "Monthly", _monthlyGrid);
        AddTab(tabBar, content, "Yearly", _yearlyGrid);

        // ----- Footer buttons
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 62, BackColor = Theme.SurfaceAlt };
        footer.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
        };

        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            Width = 480,
            Padding = new Padding(14, 14, 0, 0),
            BackColor = Theme.SurfaceAlt,
            WrapContents = false
        };
        left.Controls.AddRange(new Control[] { _export, _deleteDays, _clear });

        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 130,
            Padding = new Padding(0, 14, 14, 0),
            BackColor = Theme.SurfaceAlt,
            FlowDirection = FlowDirection.RightToLeft
        };
        right.Controls.Add(_close);

        footer.Controls.Add(left);
        footer.Controls.Add(right);

        Theme.StyleButton(_export);
        Theme.StyleButton(_deleteDays);
        Theme.StyleButton(_clear, danger: false);
        Theme.StyleButton(_close, primary: true);

        _close.Click += (_, _) => Close();
        _export.Click += (_, _) => ExportCurrentTab();
        _deleteDays.Click += (_, _) => DeleteDays();
        _clear.Click += (_, _) => ClearHistory();

        Controls.Add(content);
        Controls.Add(footer);
        Controls.Add(tabBar);
        Controls.Add(header);

        PopulateAdapterFilter();
        LoadAll();
        SelectTab(_selectedTab);
    }

    private static DataGridView MakeGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            Visible = false
        };
        Theme.StyleGrid(grid);
        return grid;
    }

    private void AddTab(FlowLayoutPanel bar, Panel content, string title, DataGridView grid)
    {
        int index = _tabs.Count;
        var button = new Button
        {
            Text = title,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
            BackColor = Theme.Surface,
            Font = Theme.Ui(10f),
            Cursor = Cursors.Hand,
            AutoSize = false,
            Size = new Size(96, 36),
            Margin = new Padding(0, 0, 2, 0),
            TabStop = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Theme.SurfaceAlt;
        button.FlatAppearance.MouseDownBackColor = Theme.SurfaceAlt;
        button.Click += (_, _) => SelectTab(index);
        button.Paint += (_, e) =>
        {
            if (_selectedTab != index) return;
            using var brush = new SolidBrush(Theme.Accent);
            e.Graphics.FillRectangle(brush, 12, button.Height - 3, button.Width - 24, 3);
        };

        _tabs.Add((title, grid, button));
        bar.Controls.Add(button);
        content.Controls.Add(grid);
    }

    private void SelectTab(int index)
    {
        _selectedTab = index;
        for (int i = 0; i < _tabs.Count; i++)
        {
            var (_, grid, button) = _tabs[i];
            bool selected = i == index;
            grid.Visible = selected;
            button.ForeColor = selected ? Theme.Accent : Theme.TextMuted;
            button.Font = Theme.Ui(10f, selected ? FontStyle.Bold : FontStyle.Regular);
            button.Invalidate();
        }
    }

    /// <summary>
    /// Defines the shared column layout -- Period / DL / UL / DL & UL / Time --
    /// and wires up click-to-sort (with a sort arrow) on every column header.
    /// </summary>
    private void SetupGrid(DataGridView grid, string periodHeader)
    {
        AddColumn(grid, "Period", periodHeader, 30, false);
        AddColumn(grid, "DL", "DL", 17, true);
        AddColumn(grid, "UL", "UL", 17, true);
        AddColumn(grid, "Both", "DL & UL", 18, true);
        AddColumn(grid, "Time", "Time", 18, true);

        _gridStates[grid] = new GridState();
        grid.ColumnHeaderMouseClick += (_, e) => OnColumnHeaderClick(grid, e);
    }

    private static void AddColumn(DataGridView grid, string name, string header, float weight, bool numeric)
    {
        var col = new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = header,
            FillWeight = weight,
            SortMode = DataGridViewColumnSortMode.Programmatic
        };

        if (numeric)
        {
            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        }

        grid.Columns.Add(col);
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

        foreach (DataGridViewColumn c in grid.Columns)
            c.HeaderCell.SortGlyphDirection = SortOrder.None;

        var sortedColumn = grid.Columns[state.SortColumn];
        if (sortedColumn is not null)
            sortedColumn.HeaderCell.SortGlyphDirection = state.Ascending ? SortOrder.Ascending : SortOrder.Descending;
    }

    /// <summary>
    /// Online time for a set of rows. Each adapter keeps its own seconds, so
    /// adding them up would count the same hour twice whenever two adapters
    /// are connected; instead take the longest per day, then add up the days.
    /// </summary>
    private static long OnlineSeconds(IEnumerable<DailyStat> stats)
        => stats.GroupBy(s => s.Date).Sum(day => day.Max(s => s.SecondsOnline));

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
            OnlineSeconds(stats));
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
                OnlineSeconds(g)))
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
                OnlineSeconds(g)))
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
                OnlineSeconds(g)))
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
                OnlineSeconds(g)))
            .ToList();

        var state = _gridStates[_yearlyGrid];
        state.Rows = rows;
        RenderGrid(_yearlyGrid, state);
    }

    private DataGridView CurrentGrid() => _tabs[_selectedTab].Grid;

    private void ExportCurrentTab()
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv",
            FileName = $"{_tabs[_selectedTab].Title}Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
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

    private void DeleteDays()
    {
        using var dlg = new DeleteRangeForm();
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var from = dlg.From;
        var to = dlg.To;
        if (to < from) (from, to) = (to, from);

        var confirm = MessageBox.Show(
            this,
            $"Delete all recorded usage from {from:yyyy-MM-dd} to {to:yyyy-MM-dd}?\n\nThis cannot be undone.",
            "Delete days",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        _storage.DeleteDailyStatsBetween(from, to);
        PopulateAdapterFilter();
        LoadAll();
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
