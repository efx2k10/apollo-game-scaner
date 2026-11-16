using ApolloGameScanner.Models;
using ApolloGameScanner.Services.Covers;

namespace ApolloGameScanner.Forms;

/// <summary>Окно поиска обложки у онлайн-провайдеров (SteamGridDB / DuckDuckGo).</summary>
public sealed class CoverPickerForm : Form
{
    private readonly IReadOnlyList<ICoverProvider> _providers;
    private readonly ComboBox _sourceBox;
    private readonly TextBox _queryBox;
    private readonly Button _searchButton;
    private readonly FlowLayoutPanel _resultsFlow;
    private readonly Label _statusLabel;
    private readonly Label _sourceHint;
    private readonly Button _okButton;
    private readonly SemaphoreSlim _thumbnailThrottle = new(4, 4);

    private CancellationTokenSource? _searchCts;
    private Panel? _selectedPanel;
    private CoverResult? _selected;

    public CoverResult? SelectedResult => _selected;

    public CoverPickerForm(IReadOnlyList<ICoverProvider> providers, string initialQuery)
    {
        _providers = providers;

        AutoScaleMode = AutoScaleMode.None;

        Text = "Поиск обложки";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(920, 640);
        MinimumSize = new Size(720, 480);
        MinimizeBox = false;
        ShowInTaskbar = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(8),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            AutoSize = true,
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        top.Controls.Add(new Label
        {
            Text = "Источник:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 6, 0),
        }, 0, 0);

        _sourceBox = new ComboBox
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DisplayMember = nameof(ICoverProvider.Name),
            Margin = new Padding(0, 4, 8, 4),
        };
        foreach (var provider in providers)
        {
            _sourceBox.Items.Add(provider);
        }

        if (_sourceBox.Items.Count > 0)
        {
            _sourceBox.SelectedIndex = 0;
        }

        _sourceBox.SelectedIndexChanged += (_, _) => UpdateSourceHint();

        top.Controls.Add(_sourceBox, 1, 0);
        top.Controls.Add(new Label
        {
            Text = "Название:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 6, 0),
        }, 2, 0);

        _queryBox = new TextBox
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Text = initialQuery,
            Margin = new Padding(0, 4, 8, 4),
        };
        _queryBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                _ = SearchAsync();
            }
        };

        top.Controls.Add(_queryBox, 3, 0);

        _searchButton = new Button
        {
            Text = "Найти",
            Width = 96,
            Height = 28,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 3, 0, 3),
        };
        _searchButton.Click += async (_, _) => await SearchAsync();
        AppIcons.Apply(_searchButton, IconKind.Search, 14);
        top.Controls.Add(_searchButton, 4, 0);

        _sourceHint = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 2, 0, 6),
        };

        _resultsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(2),
            BackColor = Color.FromArgb(248, 248, 248),
            BorderStyle = BorderStyle.FixedSingle,
        };

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0),
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Укажите название и нажмите «Найти».",
        };

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
        };

        _okButton = new Button
        {
            Text = "Выбрать",
            Width = 100,
            Height = 30,
            Enabled = false,
            DialogResult = DialogResult.None,
        };
        _okButton.Click += (_, _) =>
        {
            if (_selected is not null)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        };
        AppIcons.Apply(_okButton, IconKind.Check, 14);

        var cancel = new Button
        {
            Text = "Отмена",
            Width = 100,
            Height = 30,
            DialogResult = DialogResult.Cancel,
            Margin = new Padding(0, 0, 8, 0),
        };
        AppIcons.Apply(cancel, IconKind.Cancel, 14);

        buttons.Controls.Add(_okButton);
        buttons.Controls.Add(cancel);

        bottom.Controls.Add(_statusLabel, 0, 0);
        bottom.Controls.Add(buttons, 1, 0);

        root.Controls.Add(top, 0, 0);
        root.Controls.Add(_sourceHint, 0, 1);
        root.Controls.Add(_resultsFlow, 0, 2);
        root.Controls.Add(bottom, 0, 3);

        Controls.Add(root);
        CancelButton = cancel;

        UpdateSourceHint();
        Shown += (_, _) => _queryBox.Focus();
        FormClosing += (_, _) =>
        {
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = null;
        };
    }

    private void UpdateSourceHint()
    {
        _sourceHint.Text = _sourceBox.SelectedItem switch
        {
            GameDbProvider =>
                "GameDB — открытая база LizardByte на основе IGDB. Ключ не нужен, но нужен интернет.",
            DuckDuckGoProvider =>
                "DuckDuckGo — неофициальный источник, может работать нестабильно.",
            _ => "SteamGridDB — требуются API-ключ в настройках и доступ в интернет.",
        };
    }

    private async Task SearchAsync()
    {
        if (_sourceBox.SelectedItem is not ICoverProvider provider)
        {
            return;
        }

        var query = _queryBox.Text.Trim();
        if (query.Length == 0)
        {
            _statusLabel.Text = "Введите название игры.";
            return;
        }

        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        ClearResults();
        _searchButton.Enabled = false;
        _statusLabel.Text = "Поиск…";

        try
        {
            var results = await provider.SearchAsync(query, token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            RenderResults(results, token);
            _statusLabel.Text = results.Count == 0
                ? "Ничего не найдено. Попробуйте другое название или источник."
                : $"Найдено вариантов: {results.Count}. Выберите обложку.";
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "Поиск отменён.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Ошибка: " + ex.Message;
        }
        finally
        {
            _searchButton.Enabled = true;
        }
    }

    private void RenderResults(IReadOnlyList<CoverResult> results, CancellationToken token)
    {
        _resultsFlow.SuspendLayout();
        try
        {
            foreach (var result in results)
            {
                var item = new Panel
                {
                    Width = DpiScale.Px(132),
                    Height = DpiScale.Px(188),
                    Margin = DpiScale.Pad(6),
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = Color.White,
                    Cursor = Cursors.Hand,
                    Tag = result,
                };

                var picture = new PictureBox
                {
                    Dock = DockStyle.Top,
                    Height = DpiScale.Px(148),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.FromArgb(240, 240, 240),
                    Cursor = Cursors.Hand,
                };

                var caption = new Label
                {
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Text = result.Width > 0 && result.Height > 0
                        ? $"{result.Width}×{result.Height}"
                        : "изображение",
                    Cursor = Cursors.Hand,
                };

                item.Controls.Add(caption);
                item.Controls.Add(picture);

                item.Click += (_, _) => SelectResult(item, result);
                picture.Click += (_, _) => SelectResult(item, result);
                caption.Click += (_, _) => SelectResult(item, result);
                item.DoubleClick += (_, _) =>
                {
                    SelectResult(item, result);
                    if (_selected is not null)
                    {
                        DialogResult = DialogResult.OK;
                        Close();
                    }
                };
                picture.DoubleClick += (_, _) =>
                {
                    SelectResult(item, result);
                    if (_selected is not null)
                    {
                        DialogResult = DialogResult.OK;
                        Close();
                    }
                };

                _resultsFlow.Controls.Add(item);
                _ = LoadThumbnailAsync(picture, caption, result, token);
            }
        }
        finally
        {
            _resultsFlow.ResumeLayout(true);
        }
    }

    private void SelectResult(Panel panel, CoverResult result)
    {
        if (_selectedPanel is not null)
        {
            _selectedPanel.BorderStyle = BorderStyle.FixedSingle;
            _selectedPanel.BackColor = Color.White;
        }

        _selectedPanel = panel;
        panel.BorderStyle = BorderStyle.Fixed3D;
        panel.BackColor = Color.FromArgb(255, 248, 210);
        _selected = result;
        _okButton.Enabled = true;
    }

    private async Task LoadThumbnailAsync(PictureBox picture, Label caption, CoverResult result,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(result.ThumbUrl))
        {
            return;
        }

        try
        {
            await _thumbnailThrottle.WaitAsync(token);
            try
            {
                var bytes = await CoverService.DownloadBytesAsync(result.ThumbUrl, token);
                using var stream = new MemoryStream(bytes);
                var image = Image.FromStream(stream);
                if (picture.IsDisposed)
                {
                    image.Dispose();
                    return;
                }

                var old = picture.Image;
                picture.Image = image;
                old?.Dispose();

                if (!caption.IsDisposed && (result.Width <= 0 || result.Height <= 0))
                {
                    caption.Text = $"{image.Width}×{image.Height}";
                }
            }
            finally
            {
                _thumbnailThrottle.Release();
            }
        }
        catch
        {
            // превью не критично
        }
    }

    private void ClearResults()
    {
        _selectedPanel = null;
        _selected = null;
        _okButton.Enabled = false;

        foreach (Control control in _resultsFlow.Controls.Cast<Control>().ToList())
        {
            control.Dispose();
        }

        _resultsFlow.Controls.Clear();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        DpiScale.Refresh(this);
        DpiScale.ScaleTree(this);
        AppIcons.RefreshTree(this);
        Size = DpiScale.SizeOf(920, 640);
        MinimumSize = DpiScale.SizeOf(720, 480);
        DpiScale.FitToScreen(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _thumbnailThrottle.Dispose();
        }

        base.Dispose(disposing);
    }
}
