using System.Diagnostics;
using ApolloGameScanner.Forms;
using ApolloGameScanner.Models;
using ApolloGameScanner.Services;
using ApolloGameScanner.Services.Covers;

namespace ApolloGameScanner;

public sealed class MainForm : Form
{
    private readonly SettingsService _settingsService = new();
    private readonly AppsConfigService _configService = new();
    private readonly CoverService _coverService;

    private AppsConfig? _config;
    private string? _configPath;
    private List<DetectedGame> _games = new();
    private IReadOnlyList<ICoverProvider> _providers = Array.Empty<ICoverProvider>();

    private TextBox _configPathBox = null!;
    private TextBox _gamesPathBox = null!;
    private TextBox _filterBox = null!;
    private CheckBox _onlyNewCheck = null!;
    private Button _scanButton = null!;
    private TabControl _tabs = null!;
    private FlowLayoutPanel _gamesFlow = null!;
    private FlowLayoutPanel _configFlow = null!;
    private Label _statusLabel = null!;
    private Button _addButton = null!;
    private Button _saveButton = null!;
    private Button _saveAsButton = null!;
    private Button _batchCoverButton = null!;
    private Button _batchEditButton = null!;
    private Button _batchCancelButton = null!;
    private Button _configCoverButton = null!;
    private Button _configEditButton = null!;
    private Button _configCancelButton = null!;

    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _batchCts;
    private bool _configDirty;

    public MainForm()
    {
        _settingsService.Load();
        _coverService = new CoverService(_settingsService.Settings);

        AutoScaleMode = AutoScaleMode.None;

        Text = "Apollo Game Scanner";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1080, 760);
        MinimumSize = new Size(720, 480);
        Font = new Font("Segoe UI", 9f);

        BuildUi();
        ApplySettingsToUi();
        RefreshProviders();
        UpdateStatus();

        if (!string.IsNullOrEmpty(_settingsService.LastError))
        {
            BeginInvoke(() => MessageBox.Show(this, _settingsService.LastError, "Настройки",
                MessageBoxButtons.OK, MessageBoxIcon.Warning));
        }
    }

    // ------------------------------------------------------------------ UI

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var menu = BuildMenu();
        MainMenuStrip = menu;

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 3,
            Padding = new Padding(8, 6, 8, 2),
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        top.Controls.Add(new Label
        {
            Text = "Исходный конфиг:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 8, 0),
        }, 0, 0);

        _configPathBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 6, 4) };
        _configPathBox.Leave += (_, _) => TryLoadConfigFromTextBox();
        top.Controls.Add(_configPathBox, 1, 0);

        var configBrowse = new Button
        {
            Text = "Обзор…",
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Height = 26,
            Margin = new Padding(0, 3, 0, 3),
        };
        configBrowse.Click += (_, _) => BrowseConfig();
        AppIcons.Apply(configBrowse, IconKind.Folder);
        top.Controls.Add(configBrowse, 2, 0);

        top.Controls.Add(new Label
        {
            Text = "Папка с играми:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 8, 0),
        }, 0, 1);

        _gamesPathBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 6, 4) };
        _gamesPathBox.Leave += (_, _) => RememberGamesFolder();
        top.Controls.Add(_gamesPathBox, 1, 1);

        var gamesBrowse = new Button
        {
            Text = "Обзор…",
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Height = 26,
            Margin = new Padding(0, 3, 0, 3),
        };
        gamesBrowse.Click += (_, _) => BrowseGamesFolder();
        AppIcons.Apply(gamesBrowse, IconKind.Folder);
        top.Controls.Add(gamesBrowse, 2, 1);

        var scanRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 4, 0, 0),
        };
        scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        scanRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var leftFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0),
        };

        _scanButton = new Button
        {
            Text = "Сканировать",
            AutoSize = true,
            Height = 30,
            Margin = new Padding(0, 2, 10, 0),
        };
        _scanButton.Click += async (_, _) => await ScanAsync();
        AppIcons.Apply(_scanButton, IconKind.Scan);
        leftFlow.Controls.Add(_scanButton);

        _onlyNewCheck = new CheckBox
        {
            Text = "Только новые",
            AutoSize = true,
            Margin = new Padding(0, 9, 0, 0),
        };
        _onlyNewCheck.CheckedChanged += (_, _) => RebuildGameRows();
        leftFlow.Controls.Add(_onlyNewCheck);

        var rightFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0),
        };
        rightFlow.Controls.Add(new Label
        {
            Text = "Фильтр:",
            AutoSize = true,
            Margin = new Padding(0, 10, 6, 0),
        });

        _filterBox = new TextBox { Width = 240, Margin = new Padding(0, 6, 0, 0) };
        _filterBox.TextChanged += (_, _) => RebuildGameRows();
        rightFlow.Controls.Add(_filterBox);

        scanRow.Controls.Add(leftFlow, 0, 0);
        scanRow.Controls.Add(rightFlow, 1, 0);

        top.Controls.Add(scanRow, 0, 2);
        top.SetColumnSpan(scanRow, 3);

        _tabs = new TabControl { Dock = DockStyle.Fill };
        AppIcons.ApplyTabs(_tabs);
        _tabs.TabPages.Add(BuildGamesTab());
        _tabs.TabPages.Add(BuildConfigTab());

        var statusPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            Padding = new Padding(10, 6, 10, 8),
        };
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
        };

        _saveAsButton = new Button
        {
            Text = "Сохранить как…",
            AutoSize = true,
            Height = 30,
            Margin = new Padding(6, 0, 0, 0),
        };
        _saveAsButton.Click += (_, _) => SaveConfigAs();
        AppIcons.Apply(_saveAsButton, IconKind.SaveAs);

        _saveButton = new Button
        {
            Text = "Сохранить",
            AutoSize = true,
            Height = 30,
            Margin = new Padding(6, 0, 0, 0),
        };
        _saveButton.Click += (_, _) => SaveConfig();
        AppIcons.Apply(_saveButton, IconKind.Save);

        _addButton = new Button
        {
            Text = "Добавить отмеченные",
            AutoSize = true,
            Height = 30,
            Margin = new Padding(0),
        };
        _addButton.Click += (_, _) => AddSelectedGames();
        AppIcons.Apply(_addButton, IconKind.AddList);

        buttons.Controls.Add(_saveAsButton);
        buttons.Controls.Add(_saveButton);
        buttons.Controls.Add(_addButton);

        statusPanel.Controls.Add(_statusLabel, 0, 0);
        statusPanel.Controls.Add(buttons, 1, 0);

        root.Controls.Add(menu, 0, 0);
        root.Controls.Add(top, 0, 1);
        root.Controls.Add(_tabs, 0, 2);
        root.Controls.Add(statusPanel, 0, 3);

        Controls.Add(root);
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip
        {
            ImageScalingSize = new Size(DpiScale.Px(16), DpiScale.Px(16)),
        };

        var file = new ToolStripMenuItem("Файл");

        var newItem = new ToolStripMenuItem("Новый конфиг", null, (_, _) => NewConfig())
        {
            ShortcutKeys = Keys.Control | Keys.N,
        };
        AppIcons.Apply(newItem, IconKind.New);

        var openItem = new ToolStripMenuItem("Открыть конфиг…", null, (_, _) => BrowseConfig())
        {
            ShortcutKeys = Keys.Control | Keys.O,
        };
        AppIcons.Apply(openItem, IconKind.Open);

        var saveItem = new ToolStripMenuItem("Сохранить", null, (_, _) => SaveConfig())
        {
            ShortcutKeys = Keys.Control | Keys.S,
        };
        AppIcons.Apply(saveItem, IconKind.Save);

        var saveAsItem = new ToolStripMenuItem("Сохранить как…", null, (_, _) => SaveConfigAs());
        AppIcons.Apply(saveAsItem, IconKind.SaveAs);

        var exitItem = new ToolStripMenuItem("Выход", null, (_, _) => Close());
        AppIcons.Apply(exitItem, IconKind.Exit);

        file.DropDownItems.Add(newItem);
        file.DropDownItems.Add(openItem);
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(saveItem);
        file.DropDownItems.Add(saveAsItem);
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(exitItem);

        var settings = new ToolStripMenuItem("Настройки");
        var settingsItem = new ToolStripMenuItem("Настройки программы…", null, (_, _) => OpenSettings());
        AppIcons.Apply(settingsItem, IconKind.Settings);
        settings.DropDownItems.Add(settingsItem);

        var help = new ToolStripMenuItem("Справка");
        var aboutItem = new ToolStripMenuItem("О программе", null, (_, _) => ShowAbout());
        AppIcons.Apply(aboutItem, IconKind.Help);
        help.DropDownItems.Add(aboutItem);

        menu.Items.Add(file);
        menu.Items.Add(settings);
        menu.Items.Add(help);
        return menu;
    }

    private TabPage BuildGamesTab()
    {
        var page = new TabPage("Найденные игры") { Padding = new Padding(4), ImageIndex = 0 };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(4),
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        toolbar.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _batchCoverButton = new Button
        {
            Text = "Найти обложки…",
            Height = 30,
        };
        _batchCoverButton.Click += async (_, _) => await BatchFindCoversAsync();
        AppIcons.Apply(_batchCoverButton, IconKind.Image);

        _batchEditButton = new Button
        {
            Text = "Изменить отмеченные…",
            Height = 30,
        };
        _batchEditButton.Click += async (_, _) => await BatchEditSelectedGames();
        AppIcons.Apply(_batchEditButton, IconKind.Edit);

        _batchCancelButton = new Button
        {
            Text = "Отмена",
            Height = 30,
            Visible = false,
        };
        _batchCancelButton.Click += (_, _) => _batchCts?.Cancel();
        AppIcons.Apply(_batchCancelButton, IconKind.Cancel);

        var actions = new ActionRow(_batchCoverButton, _batchEditButton, _batchCancelButton);

        var hint = new Label
        {
            Text = "Действия применяются к отмеченным; если ничего не отмечено — ко всем новым играм. " +
                   "Массовая замена и очистка обложек — в «Изменить отмеченные…».",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 0),
        };
        toolbar.Resize += (_, _) =>
            hint.MaximumSize = new Size(Math.Max(DpiScale.Px(120), toolbar.ClientSize.Width - toolbar.Padding.Horizontal), 0);

        toolbar.Controls.Add(actions, 0, 0);
        toolbar.Controls.Add(hint, 0, 1);

        _gamesFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(6),
            BackColor = Color.FromArgb(250, 250, 250),
        };
        _gamesFlow.Resize += (_, _) => AdjustRowsWidth(_gamesFlow);

        layout.Controls.Add(toolbar, 0, 0);
        layout.Controls.Add(_gamesFlow, 0, 1);

        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildConfigTab()
    {
        var page = new TabPage("Текущий конфиг") { Padding = new Padding(4), ImageIndex = 1 };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _configCoverButton = new Button
        {
            Text = "Найти обложки…",
            Height = 30,
        };
        _configCoverButton.Click += async (_, _) => await BatchFindConfigCoversAsync();
        AppIcons.Apply(_configCoverButton, IconKind.Image);

        _configEditButton = new Button
        {
            Text = "Изменить отмеченные…",
            Height = 30,
        };
        _configEditButton.Click += async (_, _) => await BatchEditConfigEntries();
        AppIcons.Apply(_configEditButton, IconKind.Edit);

        _configCancelButton = new Button
        {
            Text = "Отмена",
            Height = 30,
            Visible = false,
        };
        _configCancelButton.Click += (_, _) => _batchCts?.Cancel();
        AppIcons.Apply(_configCancelButton, IconKind.Cancel);

        var selectAll = new Button
        {
            Text = "Отметить все",
            Height = 30,
        };
        selectAll.Click += (_, _) => SetAllConfigSelected(true);
        AppIcons.Apply(selectAll, IconKind.SelectAll);

        var clearAll = new Button
        {
            Text = "Снять все",
            Height = 30,
        };
        clearAll.Click += (_, _) => SetAllConfigSelected(false);
        AppIcons.Apply(clearAll, IconKind.ClearAll);

        var addManual = new Button
        {
            Text = "Добавить вручную…",
            Height = 30,
        };
        addManual.Click += (_, _) => AddManualEntry();
        AppIcons.Apply(addManual, IconKind.Add);

        var top = new ActionRow(
            _configCoverButton, _configEditButton, _configCancelButton, selectAll, clearAll, addManual);

        _configFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(6),
            BackColor = Color.FromArgb(250, 250, 250),
        };
        _configFlow.Resize += (_, _) => AdjustRowsWidth(_configFlow);

        layout.Controls.Add(top, 0, 0);
        layout.Controls.Add(_configFlow, 0, 1);

        page.Controls.Add(layout);
        return page;
    }

    private void ApplySettingsToUi()
    {
        var settings = _settingsService.Settings;
        _configPathBox.Text = settings.LastConfigPath ?? string.Empty;
        _gamesPathBox.Text = settings.LastGamesFolder ?? string.Empty;
        _onlyNewCheck.Checked = settings.ShowOnlyNew;
    }

    private void RefreshProviders()
    {
        var settings = _settingsService.Settings;
        _providers = new List<ICoverProvider>
        {
            new GameDbProvider(),
            new SteamGridDbProvider(settings.SteamGridDbKey),
            new DuckDuckGoProvider(),
        };
    }

    // ------------------------------------------------------------- actions

    private void BrowseConfig()
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = "Выберите apps.json Apollo",
            Filter = "Конфиг Apollo (apps.json)|apps.json|JSON (*.json)|*.json|Все файлы (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        LoadConfig(dialog.FileName);
    }

    private void TryLoadConfigFromTextBox()
    {
        var text = _configPathBox.Text.Trim();
        if (text.Length == 0 || !File.Exists(text))
        {
            return;
        }

        if (string.Equals(Path.GetFullPath(text), _configPath is null ? string.Empty : Path.GetFullPath(_configPath),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!ConfirmDiscardChanges())
        {
            _configPathBox.Text = _configPath ?? string.Empty;
            return;
        }

        LoadConfig(text);
    }

    private void BrowseGamesFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Папка с играми (каждая подпапка — игра)",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_gamesPathBox.Text) ? _gamesPathBox.Text : string.Empty,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _gamesPathBox.Text = dialog.SelectedPath;
            RememberGamesFolder();
        }
    }

    private void RememberGamesFolder()
    {
        var text = _gamesPathBox.Text.Trim();
        if (text.Length == 0)
        {
            return;
        }

        _settingsService.Settings.LastGamesFolder = text;
        SaveSettings();
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(_settingsService.Settings);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        SaveSettings();
        RefreshProviders();
        _onlyNewCheck.Checked = _settingsService.Settings.ShowOnlyNew;
        RebuildGameRows();
        UpdateStatus();
        _statusLabel.Text = "Настройки сохранены. Пересканируйте папку, чтобы применить новые шаблоны исключений.";
    }

    private void ShowAbout()
    {
        MessageBox.Show(this,
            "Apollo Game Scanner 1.0" + Environment.NewLine + Environment.NewLine +
            "Сканирует папку с играми и формирует apps.json для Apollo (Sunshine)." + Environment.NewLine +
            "Существующие записи не перезаписываются: уже добавленные игры помечаются и открываются для правки.",
            "О программе", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private bool SaveSettings()
    {
        if (_settingsService.Save())
        {
            return true;
        }

        MessageBox.Show(this, _settingsService.LastError, "Настройки",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    // ------------------------------------------------------------- config

    private void NewConfig()
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        _config = new AppsConfig();
        _configPath = null;
        _configPathBox.Text = string.Empty;
        _configDirty = true;
        GameMatcher.Apply(_games, _config);
        RebuildConfigRows();
        RebuildGameRows();
        UpdateTitle();
        UpdateStatus();
    }

    private void LoadConfig(string path, bool silent = false)
    {
        try
        {
            var config = _configService.Load(path);
            _config = config;
            _configPath = Path.GetFullPath(path);
            _configPathBox.Text = _configPath;
            _configDirty = false;

            _settingsService.Settings.LastConfigPath = _configPath;
            SaveSettings();

            GameMatcher.Apply(_games, _config);
            RebuildConfigRows();
            RebuildGameRows();
            UpdateTitle();
            UpdateStatus();

            if (!silent)
            {
                _statusLabel.Text = $"Конфиг загружен: {_configPath} (записей: {_config.Apps.Count})";
            }
        }
        catch (Exception ex)
        {
            if (silent)
            {
                _statusLabel.Text = "Не удалось загрузить прошлый конфиг: " + ex.Message;
            }
            else
            {
                MessageBox.Show(this, "Не удалось загрузить конфиг: " + ex.Message, "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private bool SaveConfig()
    {
        if (_config is null)
        {
            _config = new AppsConfig();
        }

        if (string.IsNullOrWhiteSpace(_configPath))
        {
            return SaveConfigAs();
        }

        return SaveConfigCore(_configPath);
    }

    private bool SaveConfigAs()
    {
        if (_config is null)
        {
            _config = new AppsConfig();
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Сохранить конфиг Apollo",
            FileName = "apps.json",
            Filter = "Конфиг Apollo (apps.json)|apps.json|JSON (*.json)|*.json|Все файлы (*.*)|*.*",
            OverwritePrompt = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return false;
        }

        _configPath = Path.GetFullPath(dialog.FileName);
        _configPathBox.Text = _configPath;
        return SaveConfigCore(_configPath);
    }

    private bool SaveConfigCore(string path)
    {
        try
        {
            _configService.Save(path, _config!);
            _configDirty = false;

            _settingsService.Settings.LastConfigPath = path;
            SaveSettings();

            GameMatcher.Apply(_games, _config!);
            RebuildConfigRows();
            RebuildGameRows();
            UpdateTitle();
            UpdateStatus();
            _statusLabel.Text = $"Сохранено: {path} (записей: {_config!.Apps.Count})";
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Не удалось сохранить конфиг: " + ex.Message + Environment.NewLine + Environment.NewLine +
                "Если Apollo запущен, он может удерживать файл. Закройте Apollo или используйте «Сохранить как…».",
                "Ошибка сохранения", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private bool ConfirmDiscardChanges()
    {
        if (!_configDirty)
        {
            return true;
        }

        var result = MessageBox.Show(this,
            "Есть несохранённые изменения конфига. Сохранить их?",
            "Apollo Game Scanner", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

        return result switch
        {
            DialogResult.Yes => SaveConfig(),
            DialogResult.No => true,
            _ => false,
        };
    }

    private void MarkDirty()
    {
        _configDirty = true;
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        var name = string.IsNullOrWhiteSpace(_configPath)
            ? "новый конфиг"
            : Path.GetFileName(_configPath);
        Text = $"Apollo Game Scanner — {name}{(_configDirty ? " *" : string.Empty)}";
    }

    // ------------------------------------------------------------- scan

    private async Task ScanAsync()
    {
        var root = _gamesPathBox.Text.Trim();
        if (root.Length == 0 || !Directory.Exists(root))
        {
            MessageBox.Show(this, "Укажите существующую папку с играми.", "Сканирование",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _settingsService.Settings.LastGamesFolder = root;
        SaveSettings();

        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        _scanButton.Enabled = false;
        _statusLabel.Text = "Сканирование…";

        try
        {
            var settings = _settingsService.Settings;
            var scanner = new GameScanner(settings, (folder, name) => _coverService.FindLocalCover(folder, name));
            var progress = new Progress<ScanProgress>(p =>
            {
                _statusLabel.Text = $"Сканирование {p.Current}/{p.Total}: {p.Folder}";
            });

            var games = await Task.Run(() => scanner.Scan(root, progress, token), token);
            token.ThrowIfCancellationRequested();

            _games = games;
            GameMatcher.Apply(_games, _config);

            RebuildGameRows();
            RebuildConfigRows();
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "Сканирование отменено.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Ошибка сканирования: " + ex.Message, "Сканирование",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            _statusLabel.Text = "Ошибка сканирования.";
        }
        finally
        {
            _scanButton.Enabled = true;
            UpdateStatus();
        }
    }

    // ------------------------------------------------------------- rows

    private void RebuildGameRows()
    {
        if (_gamesFlow.IsDisposed)
        {
            return;
        }

        _gamesFlow.SuspendLayout();
        try
        {
            ClearControls(_gamesFlow);

            var visible = _games.Where(MatchesFilter).ToList();
            if (visible.Count == 0)
            {
                _gamesFlow.Controls.Add(new Label
                {
                    Text = _games.Count == 0
                        ? "Укажите папку с играми и нажмите «Сканировать»."
                        : "Нет игр, подходящих под фильтр.",
                    AutoSize = true,
                    ForeColor = Color.DimGray,
                    Margin = new Padding(6, 10, 0, 0),
                });
            }

            foreach (var game in visible)
            {
                var row = new GameRowControl(game, _settingsService.Settings.WorkingDirMode);
                row.StateChanged += (_, _) => UpdateStatus();
                row.EditRequested += (_, _) => EditGame(game);
                _gamesFlow.Controls.Add(row);
            }
        }
        finally
        {
            _gamesFlow.ResumeLayout(true);
        }

        AdjustRowsWidth(_gamesFlow);
        UpdateStatus();
    }

    private bool MatchesFilter(DetectedGame game)
    {
        if (_onlyNewCheck.Checked && game.Status != GameStatus.New)
        {
            return false;
        }

        var filter = _filterBox.Text.Trim();
        if (filter.Length == 0)
        {
            return true;
        }

        return game.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
               game.FolderName.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
               game.Draft.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
    }

    private void RebuildConfigRows()
    {
        if (_configFlow.IsDisposed)
        {
            return;
        }

        _configFlow.SuspendLayout();
        try
        {
            ClearControls(_configFlow);

            if (_config is null || _config.Apps.Count == 0)
            {
                _configFlow.Controls.Add(new Label
                {
                    Text = _config is null
                        ? "Конфиг не выбран. Нажмите «Обзор…» или создайте новый (Файл → Новый конфиг)."
                        : "В конфиге пока нет записей.",
                    AutoSize = true,
                    ForeColor = Color.DimGray,
                    Margin = new Padding(6, 10, 0, 0),
                });
            }

            if (_config is not null)
            {
                for (var index = 0; index < _config.Apps.Count; index++)
                {
                    var entry = _config.Apps[index];
                    var row = new ConfigRowControl(entry, index, index > 0, index < _config.Apps.Count - 1);
                    var currentIndex = index;

                    row.EditRequested += (_, _) => EditConfigEntry(currentIndex);
                    row.DeleteRequested += (_, _) => DeleteConfigEntry(currentIndex);
                    row.MoveUpRequested += (_, _) => MoveConfigEntry(currentIndex, -1);
                    row.MoveDownRequested += (_, _) => MoveConfigEntry(currentIndex, 1);
                    row.SelectionChanged += (_, _) => UpdateStatus();

                    _configFlow.Controls.Add(row);
                }
            }
        }
        finally
        {
            _configFlow.ResumeLayout(true);
        }

        AdjustRowsWidth(_configFlow);
    }

    private static void ClearControls(Control parent)
    {
        foreach (var control in parent.Controls.Cast<Control>().ToList())
        {
            parent.Controls.Remove(control);
            control.Dispose();
        }
    }

    private static void AdjustRowsWidth(FlowLayoutPanel flow)
    {
        var width = flow.ClientSize.Width - flow.Padding.Horizontal - DpiScale.Px(24);
        if (width < DpiScale.Px(320))
        {
            width = DpiScale.Px(320);
        }

        foreach (Control control in flow.Controls)
        {
            control.Width = width;
        }
    }

    /// <summary>
    /// Строка кнопок-действий: кнопки всегда умещаются в одну строку и
    /// делят её пропорционально длине подписи. При нехватке места текст
    /// сокращается, переноса на новую строку не бывает.
    /// </summary>
    private sealed class ActionRow : Panel
    {
        private readonly Button[] _buttons;

        public ActionRow(params Button[] buttons)
        {
            _buttons = buttons;
            Dock = DockStyle.Fill;
            Height = DpiScale.Px(42);
            Padding = DpiScale.Pad(4);
            Margin = new Padding(0);

            foreach (var button in buttons)
            {
                button.AutoSize = false;
                button.AutoEllipsis = true;
                button.Margin = new Padding(0);
                button.VisibleChanged += (_, _) => PerformLayout();
                Controls.Add(button);
            }
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);

            var padding = Padding;
            var width = ClientSize.Width - padding.Horizontal;
            var height = ClientSize.Height - padding.Vertical;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var buttons = _buttons.Where(b => b.Visible).ToArray();
            if (buttons.Length == 0)
            {
                return;
            }

            var gap = buttons.Length > 1 ? DpiScale.Px(6) : 0;
            var available = width - gap * (buttons.Length - 1);
            if (available <= 0)
            {
                return;
            }

            var weights = new float[buttons.Length];
            var total = 0f;
            for (var i = 0; i < buttons.Length; i++)
            {
                weights[i] = Math.Max(DpiScale.Px(70), buttons[i].PreferredSize.Width);
                total += weights[i];
            }

            var x = padding.Left;
            for (var i = 0; i < buttons.Length; i++)
            {
                var buttonWidth = i == buttons.Length - 1
                    ? padding.Left + width - x
                    : (int)Math.Round(available * weights[i] / total);

                buttons[i].SetBounds(x, padding.Top, Math.Max(0, buttonWidth), height);
                x += buttonWidth + gap;
            }
        }
    }

    // ------------------------------------------------------------- editing

    private void EditGame(DetectedGame game)
    {
        if (game.Status == GameStatus.Existing && game.ExistingApp is not null)
        {
            var index = _config?.Apps.IndexOf(game.ExistingApp) ?? -1;
            if (index >= 0)
            {
                EditConfigEntry(index);
                return;
            }
        }

        var coversDirectory = _coverService.GetCoversDirectory(_configPath);
        using var dialog = new AppEditForm(game.Draft.Clone(), isNew: true, _coverService, coversDirectory,
            _providers, _settingsService.Settings.WorkingDirMode, game.FolderPath);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        game.Draft = dialog.Entry;
        game.Name = string.IsNullOrWhiteSpace(dialog.Entry.Name) ? game.Name : dialog.Entry.Name;

        if (game.SelectedCandidate is not null && string.IsNullOrWhiteSpace(dialog.Entry.Cmd))
        {
            game.Draft.Cmd = CommandLineUtil.Combine(game.SelectedCandidate.FullPath, null);
        }

        game.CoverPreviewPath = File.Exists(dialog.Entry.ImagePath) ? dialog.Entry.ImagePath : null;
        RebuildGameRows();
    }

    private void EditConfigEntry(int index)
    {
        if (_config is null || index < 0 || index >= _config.Apps.Count)
        {
            return;
        }

        var original = _config.Apps[index];
        var coversDirectory = _coverService.GetCoversDirectory(_configPath);

        using var dialog = new AppEditForm(original.Clone(), isNew: false, _coverService, coversDirectory,
            _providers, _settingsService.Settings.WorkingDirMode);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(dialog.Entry.Uuid))
        {
            dialog.Entry.Uuid = string.IsNullOrWhiteSpace(original.Uuid)
                ? Guid.NewGuid().ToString("D")
                : original.Uuid;
        }

        _config.Apps[index] = dialog.Entry;
        MarkDirty();
        GameMatcher.Apply(_games, _config);
        RebuildConfigRows();
        RebuildGameRows();
        UpdateStatus();
    }

    private void DeleteConfigEntry(int index)
    {
        if (_config is null || index < 0 || index >= _config.Apps.Count)
        {
            return;
        }

        var entry = _config.Apps[index];
        var result = MessageBox.Show(this,
            "Удалить запись «" + (string.IsNullOrWhiteSpace(entry.Name) ? "(без названия)" : entry.Name) + "»?",
            "Удаление", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result != DialogResult.Yes)
        {
            return;
        }

        _config.Apps.RemoveAt(index);
        MarkDirty();
        GameMatcher.Apply(_games, _config);
        RebuildConfigRows();
        RebuildGameRows();
        UpdateStatus();
    }

    private void MoveConfigEntry(int index, int delta)
    {
        if (_config is null)
        {
            return;
        }

        var target = index + delta;
        if (index < 0 || index >= _config.Apps.Count || target < 0 || target >= _config.Apps.Count)
        {
            return;
        }

        (_config.Apps[index], _config.Apps[target]) = (_config.Apps[target], _config.Apps[index]);
        MarkDirty();
        RebuildConfigRows();
    }

    private void AddManualEntry()
    {
        _config ??= new AppsConfig();

        var entry = new AppEntry
        {
            Uuid = Guid.NewGuid().ToString("D"),
            Name = string.Empty,
            AutoDetach = true,
            WaitAll = true,
            AllowClientCommands = true,
            ExitTimeout = 5,
            ScaleFactor = 100,
        };

        var coversDirectory = _coverService.GetCoversDirectory(_configPath);
        using var dialog = new AppEditForm(entry, isNew: true, _coverService, coversDirectory,
            _providers, _settingsService.Settings.WorkingDirMode);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _config.Apps.Add(dialog.Entry);
        MarkDirty();
        GameMatcher.Apply(_games, _config);
        RebuildConfigRows();
        RebuildGameRows();
        UpdateStatus();
    }

    private void AddSelectedGames()
    {
        _config ??= new AppsConfig();

        var coversDirectory = _coverService.GetCoversDirectory(_configPath);
        var added = 0;

        foreach (var game in _games.Where(g => g.Status == GameStatus.New && g.IsSelected))
        {
            var entry = game.Draft.Clone();

            if (string.IsNullOrWhiteSpace(entry.Uuid))
            {
                entry.Uuid = Guid.NewGuid().ToString("D");
            }

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                entry.Name = game.Name;
            }

            if (string.IsNullOrWhiteSpace(entry.WorkingDir))
            {
                entry.WorkingDir = WorkingDirUtil.Resolve(
                    _settingsService.Settings.WorkingDirMode,
                    game.FolderPath,
                    game.SelectedCandidate?.FullPath);
            }

            if (string.IsNullOrWhiteSpace(entry.Cmd) && game.SelectedCandidate is not null)
            {
                entry.Cmd = CommandLineUtil.Combine(game.SelectedCandidate.FullPath, null);
            }

            if (string.IsNullOrWhiteSpace(entry.ImagePath) && game.SelectedCandidate is not null)
            {
                entry.ImagePath = _coverService.ExtractExeIcon(
                    game.SelectedCandidate.FullPath, coversDirectory, entry.Name) ?? string.Empty;
            }

            _config.Apps.Add(entry);

            game.ExistingApp = entry;
            game.Status = GameStatus.Existing;
            game.IsSelected = false;
            game.CoverPreviewPath = File.Exists(entry.ImagePath) ? entry.ImagePath : game.CoverPreviewPath;
            added++;
        }

        if (added == 0)
        {
            MessageBox.Show(this, "Ни одна игра не отмечена галочкой.", "Добавление",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        MarkDirty();
        RebuildConfigRows();
        RebuildGameRows();
        UpdateStatus();
        _statusLabel.Text = $"Добавлено записей: {added}. Не забудьте сохранить конфиг.";
    }

    // ------------------------------------------------------------- batch

    private List<DetectedGame>? GetBatchTargets(string action)
    {
        var selected = _games
            .Where(g => g.Status == GameStatus.New && g.IsSelected)
            .ToList();
        if (selected.Count > 0)
        {
            return selected;
        }

        var candidates = _games
            .Where(g => g.Status != GameStatus.Existing)
            .ToList();
        if (candidates.Count == 0)
        {
            MessageBox.Show(this, "Нет новых игр для обработки.", "Пакетные действия",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }

        var result = MessageBox.Show(this,
            $"Ни одна игра не отмечена. {action} для всех новых игр ({candidates.Count})?",
            "Пакетные действия", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        return result == DialogResult.Yes ? candidates : null;
    }

    private async Task BatchEditSelectedGames()
    {
        var targets = GetBatchTargets("Применить редактирование");
        if (targets is null)
        {
            return;
        }

        using var dialog = new BatchEditForm(targets.Count);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        foreach (var game in targets)
        {
            var exe = game.SelectedCandidate?.FullPath ?? CommandLineUtil.ExtractExe(game.Draft.Cmd);
            dialog.ApplyTo(game.Draft, game.FolderPath, exe);
        }

        var coverResult = ApplyBatchCoverAction(dialog, targets);
        RebuildGameRows();

        if (dialog.CoverAction == BatchCoverAction.FindOnline)
        {
            await FindCoversForGamesAsync(targets);
            return;
        }

        _statusLabel.Text =
            $"Изменено черновиков: {targets.Count}{coverResult}. " +
            "Не забудьте добавить записи и сохранить конфиг.";
    }

    private async Task BatchFindCoversAsync()
    {
        var targets = GetBatchTargets("Искать обложки");
        if (targets is null)
        {
            return;
        }

        await FindCoversForGamesAsync(targets);
    }

    private async Task FindCoversForGamesAsync(List<DetectedGame> targets)
    {
        if (targets.Count == 0)
        {
            return;
        }

        RefreshProviders();
        var coversDirectory = _coverService.GetCoversDirectory(_configPath);

        _batchCts?.Cancel();
        _batchCts?.Dispose();
        _batchCts = new CancellationTokenSource();
        var token = _batchCts.Token;

        SetBatchBusy(true);
        var found = 0;
        var noResult = 0;
        var errors = 0;
        var skipped = 0;

        try
        {
            for (var index = 0; index < targets.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                var game = targets[index];
                var name = string.IsNullOrWhiteSpace(game.Draft.Name) ? game.Name : game.Draft.Name;
                _statusLabel.Text = $"Обложки {index + 1}/{targets.Count}: {name}";

                if (string.IsNullOrWhiteSpace(name))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    var saved = await FindAndSaveCoverAsync(name, coversDirectory, token);
                    if (saved is null)
                    {
                        noResult++;
                        continue;
                    }

                    game.Draft.ImagePath = saved;
                    game.CoverPreviewPath = saved;
                    found++;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    errors++;
                }
            }
        }
        catch (OperationCanceledException)
        {
            RebuildGameRows();
            _statusLabel.Text = "Поиск обложек отменён.";
            return;
        }
        finally
        {
            SetBatchBusy(false);
        }

        RebuildGameRows();
        _statusLabel.Text =
            $"Обложки: найдено {found}, без результата {noResult}, ошибок {errors}" +
            (skipped > 0 ? $", пропущено {skipped}" : string.Empty) + ".";
    }

    // ------------------------------------------------------------- config batch

    private List<AppEntry>? GetConfigTargets(string action)
    {
        if (_config is null || _config.Apps.Count == 0)
        {
            MessageBox.Show(this, "В конфиге нет записей для обработки.", "Пакетные действия",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }

        var selected = _config.Apps.Where(entry => entry.IsSelected).ToList();
        if (selected.Count > 0)
        {
            return selected;
        }

        var result = MessageBox.Show(this,
            $"Ни одна запись не отмечена. {action} для всех записей ({_config.Apps.Count})?",
            "Пакетные действия", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        return result == DialogResult.Yes ? _config.Apps.ToList() : null;
    }

    private async Task BatchEditConfigEntries()
    {
        var targets = GetConfigTargets("Применить редактирование");
        if (targets is null)
        {
            return;
        }

        using var dialog = new BatchEditForm(targets.Count);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        foreach (var entry in targets)
        {
            var game = _games.FirstOrDefault(g => ReferenceEquals(g.ExistingApp, entry));
            dialog.ApplyTo(entry, game?.FolderPath, CommandLineUtil.ExtractExe(entry.Cmd));
        }

        var coverResult = ApplyBatchCoverAction(dialog, targets);
        MarkDirty();
        GameMatcher.Apply(_games, _config);
        RebuildConfigRows();
        RebuildGameRows();
        UpdateStatus();

        if (dialog.CoverAction == BatchCoverAction.FindOnline)
        {
            await FindCoversForConfigAsync(targets);
            return;
        }

        _statusLabel.Text =
            $"Изменено записей конфига: {targets.Count}{coverResult}. Не забудьте сохранить.";
    }

    private async Task BatchFindConfigCoversAsync()
    {
        var targets = GetConfigTargets("Искать обложки");
        if (targets is null)
        {
            return;
        }

        await FindCoversForConfigAsync(targets);
    }

    private async Task FindCoversForConfigAsync(List<AppEntry> targets)
    {
        if (targets.Count == 0)
        {
            return;
        }

        RefreshProviders();
        var coversDirectory = _coverService.GetCoversDirectory(_configPath);

        _batchCts?.Cancel();
        _batchCts?.Dispose();
        _batchCts = new CancellationTokenSource();
        var token = _batchCts.Token;

        SetBatchBusy(true);
        var found = 0;
        var noResult = 0;
        var errors = 0;
        var skipped = 0;

        try
        {
            for (var index = 0; index < targets.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                var entry = targets[index];
                var name = entry.Name;
                _statusLabel.Text = $"Обложки {index + 1}/{targets.Count}: {name}";

                if (string.IsNullOrWhiteSpace(name))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    var saved = await FindAndSaveCoverAsync(name, coversDirectory, token);
                    if (saved is null)
                    {
                        noResult++;
                        continue;
                    }

                    entry.ImagePath = saved;
                    found++;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    errors++;
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (found > 0)
            {
                MarkDirty();
            }

            GameMatcher.Apply(_games, _config);
            RebuildConfigRows();
            RebuildGameRows();
            UpdateStatus();
            _statusLabel.Text = $"Поиск обложек отменён. Успели обновить: {found}.";
            return;
        }
        finally
        {
            SetBatchBusy(false);
        }

        if (found > 0)
        {
            MarkDirty();
        }

        GameMatcher.Apply(_games, _config);
        RebuildConfigRows();
        RebuildGameRows();
        UpdateStatus();
        _statusLabel.Text =
            $"Обложки конфига: найдено {found}, без результата {noResult}, ошибок {errors}" +
            (skipped > 0 ? $", пропущено {skipped}" : string.Empty) + ".";
    }

    private void SetAllConfigSelected(bool selected)
    {
        if (_config is null)
        {
            return;
        }

        foreach (var entry in _config.Apps)
        {
            entry.IsSelected = selected;
        }

        RebuildConfigRows();
        UpdateStatus();
    }

    /// <summary>
    /// Применяет к черновикам выбранное действие с обложками: очистка или
    /// одна картинка для всех. Поиск в интернете выполняется отдельно.
    /// </summary>
    private string ApplyBatchCoverAction(BatchEditForm dialog, IReadOnlyList<DetectedGame> games)
    {
        if (dialog.CoverAction is BatchCoverAction.None or BatchCoverAction.FindOnline)
        {
            return string.Empty;
        }

        var coversDirectory = _coverService.GetCoversDirectory(_configPath);
        var changed = 0;
        var errors = 0;

        foreach (var game in games)
        {
            if (dialog.CoverAction == BatchCoverAction.Clear)
            {
                game.Draft.ImagePath = string.Empty;
                game.CoverPreviewPath = null;
                changed++;
                continue;
            }

            var name = string.IsNullOrWhiteSpace(game.Draft.Name) ? game.Name : game.Draft.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            try
            {
                var saved = _coverService.SaveCover(dialog.CoverFilePath, coversDirectory, name);
                game.Draft.ImagePath = saved;
                game.CoverPreviewPath = saved;
                changed++;
            }
            catch
            {
                errors++;
            }
        }

        return FormatCoverResult(dialog.CoverAction, changed, errors);
    }

    /// <summary>То же для записей конфига.</summary>
    private string ApplyBatchCoverAction(BatchEditForm dialog, IReadOnlyList<AppEntry> entries)
    {
        if (dialog.CoverAction is BatchCoverAction.None or BatchCoverAction.FindOnline)
        {
            return string.Empty;
        }

        var coversDirectory = _coverService.GetCoversDirectory(_configPath);
        var changed = 0;
        var errors = 0;

        foreach (var entry in entries)
        {
            if (dialog.CoverAction == BatchCoverAction.Clear)
            {
                entry.ImagePath = string.Empty;
                changed++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                continue;
            }

            try
            {
                var saved = _coverService.SaveCover(dialog.CoverFilePath, coversDirectory, entry.Name);
                entry.ImagePath = saved;
                changed++;
            }
            catch
            {
                errors++;
            }
        }

        return FormatCoverResult(dialog.CoverAction, changed, errors);
    }

    private static string FormatCoverResult(BatchCoverAction action, int changed, int errors)
    {
        var text = action == BatchCoverAction.Clear
            ? $", обложки очищены: {changed}"
            : $", обложек установлено: {changed}";

        return errors > 0 ? text + $", ошибок: {errors}" : text;
    }

    private async Task<string?> FindAndSaveCoverAsync(string name, string coversDirectory,
        CancellationToken token)
    {
        var cover = await FindFirstCoverAsync(name, token);
        if (cover is null)
        {
            return null;
        }

        var cached = await _coverService.DownloadToCacheAsync(cover.FullUrl, token);
        return _coverService.SaveCover(cached, coversDirectory, name);
    }

    private async Task<CoverResult?> FindFirstCoverAsync(string name, CancellationToken token)
    {
        foreach (var provider in _providers)
        {
            try
            {
                var results = await provider.SearchAsync(name, token);
                if (results.Count > 0)
                {
                    return results[0];
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Пробуем следующий источник.
            }
        }

        return null;
    }

    private void SetBatchBusy(bool busy)
    {
        _batchCoverButton.Enabled = !busy;
        _batchEditButton.Enabled = !busy;
        _configCoverButton.Enabled = !busy;
        _configEditButton.Enabled = !busy;
        _scanButton.Enabled = !busy;
        _batchCancelButton.Visible = busy;
        _configCancelButton.Visible = busy;
        UseWaitCursor = busy;
    }

    // ------------------------------------------------------------- status

    private void UpdateStatus()
    {
        if (_statusLabel.IsDisposed)
        {
            return;
        }

        var newCount = _games.Count(g => g.Status == GameStatus.New);
        var existingCount = _games.Count(g => g.Status == GameStatus.Existing);
        var noExeCount = _games.Count(g => g.Status == GameStatus.NoExe);
        var selectedCount = _games.Count(g => g.Status == GameStatus.New && g.IsSelected);

        if (_games.Count == 0)
        {
            _statusLabel.Text = _config is null
                ? "Выберите конфиг и папку с играми, затем нажмите «Сканировать»."
                : $"Конфиг: {(_configPath ?? "новый")} (записей: {_config.Apps.Count}). Папка не просканирована.";
        }
        else
        {
            _statusLabel.Text =
                $"Новых: {newCount}   Уже в конфиге: {existingCount}   Без exe: {noExeCount}" +
                $"   |   Конфиг: {(_configPath is null ? "новый" : Path.GetFileName(_configPath))}" +
                $" (записей: {_config?.Apps.Count ?? 0})";
        }

        _addButton.Text = $"Добавить отмеченные ({selectedCount})";
        _addButton.Enabled = selectedCount > 0;
        _saveButton.Enabled = _config is not null;
        _saveAsButton.Enabled = _config is not null;

        UpdateTitle();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        DpiScale.Refresh(this);
        DpiScale.ScaleTree(this);
        AppIcons.RefreshTree(this);
        Size = DpiScale.SizeOf(1080, 760);
        MinimumSize = DpiScale.SizeOf(720, 480);
        DpiScale.FitToScreen(this);

        if (!string.IsNullOrWhiteSpace(_settingsService.Settings.LastConfigPath) &&
            File.Exists(_settingsService.Settings.LastConfigPath))
        {
            LoadConfig(_settingsService.Settings.LastConfigPath!, silent: true);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _scanCts?.Cancel();
        _batchCts?.Cancel();

        if (_configDirty)
        {
            var result = MessageBox.Show(this,
                "Есть несохранённые изменения конфига. Сохранить перед выходом?",
                "Apollo Game Scanner", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            if (result == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (result == DialogResult.Yes && !SaveConfig())
            {
                e.Cancel = true;
                return;
            }
        }

        _settingsService.Settings.ShowOnlyNew = _onlyNewCheck.Checked;
        SaveSettings();

        _scanCts?.Dispose();
        base.OnFormClosing(e);
    }
}
