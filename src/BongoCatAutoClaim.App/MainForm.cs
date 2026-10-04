using BongoCatAutoClaim.Core;

namespace BongoCatAutoClaim.App;

public sealed class MainForm : Form
{
    private readonly GameDetector detector;
    private readonly AutoClaimManager manager;
    private readonly TextBox pathBox = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly Label statusLabel = new() { AutoSize = true, MaximumSize = new Size(620, 0) };
    private readonly Label detailLabel = new() { AutoSize = true, MaximumSize = new Size(620, 0) };
    private readonly Button installButton = new() { Text = "Install Auto Claim", AutoSize = true };
    private readonly Button restoreButton = new() { Text = "Restore Original", AutoSize = true };
    private readonly Button refreshButton = new() { Text = "Detect / Refresh", AutoSize = true };
    private readonly Button browseButton = new() { Text = "Choose Folder...", AutoSize = true };
    private GameInstallation? installation;

    public MainForm(GameDetector detector, AutoClaimManager manager)
    {
        this.detector = detector;
        this.manager = manager;
        Text = "Bongo Cat Auto Claim v1.0.1";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(700, 390);
        ClientSize = new Size(760, 430);
        Font = new Font("Segoe UI", 10F);

        var title = new Label
        {
            Text = "Bongo Cat Auto Claim",
            Font = new Font("Segoe UI Semibold", 19F),
            AutoSize = true
        };
        var explanation = new Label
        {
            Text = "Claims the normal and emote chests through the game's normal purchase path when their 30-minute timers finish. It does not change timers, clicks, or Steam inventory logic.",
            AutoSize = true,
            MaximumSize = new Size(680, 0)
        };

        var pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.Controls.Add(pathBox, 0, 0);
        pathRow.Controls.Add(browseButton, 1, 0);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.Add(refreshButton);
        buttons.Controls.Add(installButton);
        buttons.Controls.Add(restoreButton);

        var notice = new Label
        {
            Text = "Bongo Cat must be closed before installation or restoration. Unknown game builds are always rejected. A verified pristine backup is created before patching.",
            AutoSize = true,
            MaximumSize = new Size(680, 0),
            ForeColor = Color.DimGray
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28),
            AutoSize = true,
            RowCount = 8,
            ColumnCount = 1
        };
        for (var i = 0; i < 8; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(title);
        layout.Controls.Add(explanation);
        layout.Controls.Add(new Label { Text = "Bongo Cat installation", AutoSize = true, Padding = new Padding(0, 15, 0, 0) });
        layout.Controls.Add(pathRow);
        layout.Controls.Add(statusLabel);
        layout.Controls.Add(detailLabel);
        layout.Controls.Add(buttons);
        layout.Controls.Add(notice);
        Controls.Add(layout);

        refreshButton.Click += (_, _) => DetectAndRefresh();
        browseButton.Click += (_, _) => Browse();
        installButton.Click += async (_, _) => await RunOperationAsync("Install Auto Claim", () => manager.Install(RequireInstallation()));
        restoreButton.Click += async (_, _) => await RunOperationAsync("Restore Original", () => manager.Restore(RequireInstallation()));
        Shown += (_, _) => DetectAndRefresh();
    }

    private void DetectAndRefresh()
    {
        try
        {
            installation ??= detector.Detect().FirstOrDefault();
            RefreshStatus();
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void Browse()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the BongoCat game folder",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            installation = detector.FromSelectedFolder(dialog.SelectedPath);
            RefreshStatus();
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void RefreshStatus()
    {
        if (installation is null)
        {
            pathBox.Text = "Not detected";
            statusLabel.Text = "Bongo Cat was not detected. Choose its game folder manually.";
            detailLabel.Text = string.Empty;
            installButton.Enabled = restoreButton.Enabled = false;
            return;
        }

        pathBox.Text = installation.RootPath;
        var status = manager.GetStatus(installation);
        statusLabel.Text = status.Message;
        detailLabel.Text = $"Steam build: {installation.SteamBuildId ?? "unavailable"}{Environment.NewLine}Assembly SHA-256: {status.Sha256 ?? "unavailable"}";
        installButton.Enabled = status.AssemblyState == AssemblyState.SupportedOriginal;
        restoreButton.Enabled = status.AssemblyState == AssemblyState.AcceptedPatched;
    }

    private async Task RunOperationAsync(string title, Func<OperationResult> operation)
    {
        if (MessageBox.Show(this, $"Bongo Cat must be closed. Continue with {title}?", title,
                MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK)
            return;

        SetBusy(true);
        try
        {
            var result = await Task.Run(operation);
            MessageBox.Show(this, result.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
        finally
        {
            SetBusy(false);
            RefreshStatus();
        }
    }

    private GameInstallation RequireInstallation() => installation ?? throw new InvalidOperationException("No Bongo Cat installation is selected.");

    private void SetBusy(bool busy)
    {
        UseWaitCursor = busy;
        refreshButton.Enabled = browseButton.Enabled = installButton.Enabled = restoreButton.Enabled = !busy;
    }

    private void ShowError(Exception exception) => MessageBox.Show(this, exception.Message, "Bongo Cat Auto Claim",
        MessageBoxButtons.OK, MessageBoxIcon.Error);
}
