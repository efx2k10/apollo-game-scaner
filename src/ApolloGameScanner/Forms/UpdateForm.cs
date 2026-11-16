using ApolloGameScanner.Models;
using ApolloGameScanner.Services;

namespace ApolloGameScanner.Forms;

/// <summary>Диалог обновления: описание релиза, загрузка и установка.</summary>
public sealed class UpdateForm : Form
{
    private readonly UpdateService _service;
    private readonly UpdateInfo _info;
    private readonly CancellationTokenSource _cts = new();

    private readonly TextBox _notes;
    private readonly ProgressBar _progress;
    private readonly Label _status;
    private readonly Button _install;
    private readonly Button _release;
    private readonly Button _close;

    private bool _busy;

    public bool Applied { get; private set; }

    public UpdateForm(UpdateService service, UpdateInfo info)
    {
        _service = service;
        _info = info;

        Text = "Обновление Apollo Game Scanner";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        ShowIcon = false;
        MinimizeBox = false;
        AutoScaleMode = AutoScaleMode.None;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new Label
        {
            Text = $"Доступна версия {_info.VersionText}\n" +
                   $"Установлена версия {UpdateService.CurrentVersionText}",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 11f, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8),
        };

        var notesTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
        };
        notesTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        notesTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        notesTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var notesLabel = new Label
        {
            Text = "Что нового:",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4),
        };

        _notes = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = true,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Text = string.IsNullOrWhiteSpace(_info.Notes)
                ? "Описание не указано."
                : _info.Notes.Replace("\r\n", "\n").Replace("\n", "\r\n"),
            Margin = new Padding(0),
        };

        notesTable.Controls.Add(notesLabel, 0, 0);
        notesTable.Controls.Add(_notes, 0, 1);

        var progressPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0),
        };
        progressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        progressPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        progressPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _status = new Label
        {
            Text = "Файлы settings.json, covers и cache не изменяются.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 0, 0, 4),
        };

        _progress = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Height = 20,
            Minimum = 0,
            Maximum = 100,
            Style = ProgressBarStyle.Continuous,
            Visible = false,
            Margin = new Padding(0),
        };

        progressPanel.Controls.Add(_status, 0, 0);
        progressPanel.Controls.Add(_progress, 0, 1);

        var linkPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0),
        };

        _release = new Button
        {
            Text = "Открыть страницу релиза",
            AutoSize = true,
            Height = 30,
        };
        _release.Click += (_, _) => UpdateService.OpenReleasePage(_info.ReleaseUrl);
        AppIcons.Apply(_release, IconKind.Open);

        linkPanel.Controls.Add(_release);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0),
        };

        _install = new Button
        {
            Text = "Скачать и установить",
            AutoSize = true,
            Height = 30,
            Margin = new Padding(0, 0, 8, 0),
        };
        _install.Click += Install_Click;
        AppIcons.Apply(_install, IconKind.Refresh);

        _close = new Button
        {
            Text = "Закрыть",
            Width = 110,
            Height = 30,
        };
        _close.Click += Close_Click;
        AppIcons.Apply(_close, IconKind.Cancel);

        buttons.Controls.Add(_install);
        buttons.Controls.Add(_close);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(notesTable, 0, 1);
        root.Controls.Add(progressPanel, 0, 2);
        root.Controls.Add(linkPanel, 0, 3);
        root.Controls.Add(buttons, 0, 4);

        Controls.Add(root);

        AcceptButton = _install;
        CancelButton = _close;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        DpiScale.Refresh(this);
        DpiScale.ScaleTree(this);
        AppIcons.RefreshTree(this);
        Size = DpiScale.SizeOf(760, 620);
        MinimumSize = DpiScale.SizeOf(640, 480);
        DpiScale.FitToScreen(this);

        _notes.SelectionStart = 0;
        _notes.SelectionLength = 0;
        ActiveControl = _install;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_busy)
        {
            _cts.Cancel();
            _close.Enabled = false;
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    private async void Install_Click(object? sender, EventArgs e)
    {
        if (_busy)
        {
            return;
        }

        if (!_service.CanWriteInstallDirectory())
        {
            var open = MessageBox.Show(this,
                "Нет прав на запись рядом с программой, поэтому автоматическое обновление невозможно." +
                Environment.NewLine + Environment.NewLine +
                "Открыть страницу релиза, чтобы скачать архив вручную?",
                "Обновление", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (open == DialogResult.Yes)
            {
                UpdateService.OpenReleasePage(_info.ReleaseUrl);
            }

            return;
        }

        var confirm = MessageBox.Show(this,
            $"Скачать обновление {_info.VersionText} и перезапустить программу?" +
            Environment.NewLine + Environment.NewLine +
            "Текущий exe будет заменён. settings.json, обложки и кэш сохранятся.",
            "Обновление", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes)
        {
            return;
        }

        SetBusy(true);
        _status.Text = "Скачивание… 0%";

        var progress = new Progress<double>(value =>
        {
            var percent = (int)Math.Round(value * 100);
            _progress.Value = Math.Clamp(percent, 0, 100);
            _status.Text = $"Скачивание… {percent}%";
        });

        try
        {
            var dir = await _service.DownloadAndExtractAsync(_info, progress, _cts.Token);

            _status.Text = "Установка и перезапуск…";
            _service.ApplyAndRestart(dir);

            Applied = true;
            _busy = false;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Загрузка отменена.";
            SetBusy(false);
        }
        catch (Exception ex)
        {
            _status.Text = "Не удалось обновить.";
            MessageBox.Show(this, ex.Message, "Обновление", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetBusy(false);
        }
    }

    private void Close_Click(object? sender, EventArgs e)
    {
        if (_busy)
        {
            _cts.Cancel();
            _close.Enabled = false;
            return;
        }

        Close();
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _install.Enabled = !busy;
        _release.Enabled = !busy;
        _progress.Visible = busy;
        _progress.Value = 0;
        _close.Text = busy ? "Отмена" : "Закрыть";
        _close.Enabled = true;
    }
}
