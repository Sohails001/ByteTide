namespace ByteTide.App;

/// <summary>Asks for a date range whose recorded history should be deleted (to repair bad days).</summary>
public sealed class DeleteRangeForm : Form
{
    private readonly DateTimePicker _from = new() { Format = DateTimePickerFormat.Short, Width = 140 };
    private readonly DateTimePicker _to = new() { Format = DateTimePickerFormat.Short, Width = 140 };

    public DateOnly From => DateOnly.FromDateTime(_from.Value.Date);
    public DateOnly To => DateOnly.FromDateTime(_to.Value.Date);

    public DeleteRangeForm()
    {
        Text = "Delete days from history";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(440, 236);
        BackColor = Theme.Surface;
        Font = Theme.Ui();

        var info = new Label
        {
            Text = "Permanently deletes the recorded usage for every day in this range, for all adapters. " +
                   "Use it to remove days that were measured incorrectly. This cannot be undone.",
            AutoSize = false,
            Size = new Size(396, 62),
            Location = new Point(22, 20),
            ForeColor = Theme.TextMuted
        };

        var fromLabel = new Label { Text = "From", AutoSize = true, Location = new Point(22, 100) };
        _from.Location = new Point(22, 122);
        var toLabel = new Label { Text = "To", AutoSize = true, Location = new Point(200, 100) };
        _to.Location = new Point(200, 122);

        _from.Value = DateTime.Today.AddDays(-7);
        _to.Value = DateTime.Today;

        var ok = new Button { Text = "Delete days", DialogResult = DialogResult.OK, Location = new Point(236, 182) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(344, 182) };
        Theme.StyleButton(ok, danger: true);
        Theme.StyleButton(cancel);

        Controls.AddRange(new Control[] { info, fromLabel, _from, toLabel, _to, ok, cancel });
        AcceptButton = cancel; // safer default than the destructive button
        CancelButton = cancel;
    }
}
