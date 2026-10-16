using ByteTide.Core;

namespace ByteTide.App;

public sealed class ImportForm : Form
{
    private readonly Storage _storage;
    private readonly TextBox _path = new() { Width = 360, BorderStyle = BorderStyle.FixedSingle };
    private readonly Button _browse = new() { Text = "Browse..." };
    private readonly Button _import = new() { Text = "Import" };
    private readonly CheckBox _replace = new()
    {
        Text = "Replace days that already have data (use this to correct days ByteTide measured imperfectly)",
        AutoSize = false,
        Size = new Size(500, 40)
    };
    private readonly Label _status = new()
    {
        AutoSize = false,
        Size = new Size(500, 90),
        ForeColor = Theme.TextMuted
    };

    public ImportForm(Storage storage)
    {
        _storage = storage;

        Text = "Import RAS Graph & Stats history";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(548, 330);
        BackColor = Theme.Surface;
        Font = Theme.Ui();
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;

        var heading = new Label
        {
            Text = "Import legacy history",
            Font = Theme.UiSemibold(15f),
            AutoSize = true,
            Location = new Point(24, 18)
        };

        var help = new Label
        {
            Text = "Choose the folder that contains your RAS Graph & Stats .dat archive files. " +
                   "Importing is safe to repeat; the same day is never added twice.",
            AutoSize = false,
            Size = new Size(500, 40),
            Location = new Point(24, 54),
            ForeColor = Theme.TextMuted
        };

        _path.Location = new Point(24, 108);
        _browse.Location = new Point(394, 104);
        Theme.StyleButton(_browse);

        _replace.Location = new Point(24, 148);
        _import.Location = new Point(24, 196);
        Theme.StyleButton(_import, primary: true);

        _status.Location = new Point(24, 244);

        _browse.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK) _path.Text = dlg.SelectedPath;
        };
        _import.Click += (_, _) => RunImport();

        Controls.AddRange(new Control[] { heading, help, _path, _browse, _replace, _import, _status });
    }

    private void RunImport()
    {
        if (!Directory.Exists(_path.Text))
        {
            MessageBox.Show(this, "Please choose a valid folder.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        bool replace = _replace.Checked;
        if (replace)
        {
            var confirm = MessageBox.Show(
                this,
                "Every day found in these files will be deleted from ByteTide first (for all adapters) and replaced by the imported numbers.\n\nContinue?",
                "Replace existing days",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;
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
                summary = LegacyImporter.ImportFolder(folder, _storage, replace);
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
