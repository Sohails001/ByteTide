using ByteTide.Core;

namespace ByteTide.App;

public sealed class ImportForm : Form
{
    private readonly Storage _storage;
    private readonly TextBox _path = new() { Width = 320 };
    private readonly Button _browse = new() { Text = "Browse..." };
    private readonly Button _import = new() { Text = "Import" };
    private readonly Label _status = new()
    {
        AutoSize = false,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.TopLeft,
        Padding = new Padding(10)
    };

    public ImportForm(Storage storage)
    {
        _storage = storage;

        Text = "Import Legacy RAS Graph & Stats Data";
        Width = 620;
        Height = 240;
        StartPosition = FormStartPosition.CenterParent;

        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10)
        };
        top.Controls.Add(new Label { Text = "Folder with .dat files:", AutoSize = true, Padding = new Padding(0, 6, 6, 0) });
        top.Controls.Add(_path);
        top.Controls.Add(_browse);

        _browse.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK) _path.Text = dlg.SelectedPath;
        };

        var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Padding = new Padding(10) };
        buttonPanel.Controls.Add(_import);
        _import.Click += (_, _) => RunImport();

        Controls.Add(_status);
        Controls.Add(buttonPanel);
        Controls.Add(top);
    }

    private void RunImport()
    {
        if (!Directory.Exists(_path.Text))
        {
            MessageBox.Show(this, "Please choose a valid folder.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _import.Enabled = false;
        _browse.Enabled = false;
        _status.Text = "Importing...";

        var folder = _path.Text;

        Task.Run(() =>
        {
            ImportSummary summary;
            Exception? error = null;
            try
            {
                summary = LegacyImporter.ImportFolder(folder, _storage);
            }
            catch (Exception ex)
            {
                summary = default;
                error = ex;
            }

            if (IsDisposed) return;

            BeginInvoke(new Action(() =>
            {
                _status.Text = error is not null
                    ? $"Import failed: {error.Message}"
                    : $"Done.\n\nFiles processed: {summary.FilesProcessed}\nDay/adapter records imported: {summary.RecordsImported}\nFiles that failed to parse: {summary.FilesFailed}";

                _import.Enabled = true;
                _browse.Enabled = true;
            }));
        });
    }
}
