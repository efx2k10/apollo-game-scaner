using System.Text.Json.Nodes;
using ApolloGameScanner.Models;
using ApolloGameScanner.Services;
using ApolloGameScanner.Services.Covers;

namespace ApolloGameScanner.Forms;

/// <summary>Окно редактирования записи игры (основные параметры + полный JSON).</summary>
public sealed class AppEditForm : Form
{
    private readonly AppEntry _entry;
    private readonly CoverService _coverService;
    private readonly string _coversDirectory;
    private readonly IReadOnlyList<ICoverProvider> _providers;
    private readonly WorkingDirMode _workingDirMode;
    private readonly string? _gameFolder;

    private TextBox _nameBox = null!;
    private TextBox _exeBox = null!;
    private TextBox _argsBox = null!;
    private TextBox _workingDirBox = null!;
    private CheckBox _elevatedCheck = null!;
    private CheckBox _autoDetachCheck = null!;
    private CheckBox _waitAllCheck = null!;
    private CheckBox _virtualDisplayCheck = null!;
    private CheckBox _useAppIdentityCheck = null!;
    private CheckBox _perClientAppIdentityCheck = null!;
    private CheckBox _allowClientCommandsCheck = null!;
    private CheckBox _terminateOnPauseCheck = null!;
    private CheckBox _excludeGlobalPrepCheck = null!;
    private CheckBox _excludeGlobalStateCheck = null!;
    private NumericUpDown _scaleFactorBox = null!;
    private NumericUpDown _exitTimeoutBox = null!;
    private ComboBox _gamepadBox = null!;
    private PictureBox _coverPreview = null!;
    private Label _coverHint = null!;
    private TabControl _tabs = null!;
    private TabPage _mainTab = null!;
    private TabPage _jsonTab = null!;
    private TextBox _jsonBox = null!;
    private Label _jsonHint = null!;

    private bool _jsonDirty;
    private bool _suppressJson;
    private string? _coverSourcePath;

    public AppEntry Entry => _entry;

    public AppEditForm(
        AppEntry entry,
        bool isNew,
        CoverService coverService,
        string coversDirectory,
        IReadOnlyList<ICoverProvider> providers,
        WorkingDirMode workingDirMode = WorkingDirMode.ExeFolder,
        string? gameFolder = null)
    {
        _entry = entry;
        _coverService = coverService;
        _coversDirectory = coversDirectory;
        _providers = providers;
        _workingDirMode = workingDirMode;
        _gameFolder = gameFolder;

        AutoScaleMode = AutoScaleMode.None;

        Text = isNew ? "Добавление игры" : "Редактирование игры";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(780, 720);
        MinimumSize = new Size(700, 620);
        MinimizeBox = false;
        ShowInTaskbar = false;

        BuildUi();
        ApplyEntryToControls(_entry);
        RefreshJsonFromControls();

        Shown += (_, _) => _nameBox.Focus();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _tabs = new TabControl { Dock = DockStyle.Fill };
        AppIcons.ApplyTabs(_tabs);

        _mainTab = new TabPage("Основные") { Padding = new Padding(6), ImageIndex = 0 };
        _jsonTab = new TabPage("JSON") { Padding = new Padding(6), ImageIndex = 1 };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 0,
            Padding = new Padding(6, 4, 6, 4),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _nameBox = new TextBox();
        AddRow(table, "Название:", _nameBox);

        _exeBox = new TextBox();
        AddRow(table, "Файл (exe):", MakeBrowsePanel(_exeBox, "Обзор…", PickExe, IconKind.Document));

        _argsBox = new TextBox();
        AddRow(table, "Аргументы:", _argsBox);

        _workingDirBox = new TextBox();
        AddRow(table, "Рабочая папка:", MakeBrowsePanel(_workingDirBox, "Обзор…", PickWorkingDirectory));

        var coverPanel = BuildCoverPanel();
        AddRow(table, "Обложка:", coverPanel);

        var extraGroup = BuildExtraGroup();
        var extraRow = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.Controls.Add(extraGroup, 0, extraRow);
        table.SetColumnSpan(extraGroup, 2);

        _mainTab.Controls.Add(table);

        _jsonBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            AcceptsTab = true,
            MaxLength = 2_000_000,
            Font = new Font("Consolas", 9.5f),
        };
        _jsonBox.TextChanged += (_, _) =>
        {
            if (!_suppressJson)
            {
                _jsonDirty = true;
            }
        };

        _jsonHint = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 38,
            ForeColor = Color.DimGray,
            Text = "Здесь можно править любые поля Apollo: detached, prep-cmd, state-cmd и др. " +
                   "Изменения применяются при возврате на вкладку «Основные» или по кнопке «OK».",
        };

        _jsonTab.Controls.Add(_jsonBox);
        _jsonTab.Controls.Add(_jsonHint);

        _tabs.TabPages.Add(_mainTab);
        _tabs.TabPages.Add(_jsonTab);
        _tabs.Selecting += Tabs_Selecting;

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

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
        };

        var ok = new Button
        {
            Text = "OK",
            Width = 110,
            Height = 30,
            DialogResult = DialogResult.None,
        };
        AppIcons.Apply(ok, IconKind.Check);
        ok.Click += (_, _) =>
        {
            if (Commit())
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        };

        var cancel = new Button
        {
            Text = "Отмена",
            Width = 110,
            Height = 30,
            DialogResult = DialogResult.Cancel,
            Margin = new Padding(0, 0, 8, 0),
        };
        AppIcons.Apply(cancel, IconKind.Cancel);

        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        bottom.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 0);
        bottom.Controls.Add(buttons, 1, 0);

        root.Controls.Add(_tabs, 0, 0);
        root.Controls.Add(bottom, 0, 1);

        Controls.Add(root);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private Panel MakeBrowsePanel(TextBox box, string buttonText, EventHandler handler,
        IconKind kind = IconKind.Folder)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            Margin = new Padding(0),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        box.Dock = DockStyle.Fill;
        box.Margin = new Padding(0, 3, 6, 3);

        var button = new Button
        {
            Text = buttonText,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Height = 26,
            Margin = new Padding(0, 1, 0, 1),
        };
        button.Click += handler;
        AppIcons.Apply(button, kind, 14);

        panel.Controls.Add(box, 0, 0);
        panel.Controls.Add(button, 1, 0);
        return panel;
    }

    private Panel BuildCoverPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 1,
            Height = 140,
            AutoSize = false,
            Margin = new Padding(0, 4, 0, 4),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _coverPreview = new PictureBox
        {
            Width = 90,
            Height = 124,
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(248, 248, 248),
            Margin = new Padding(0, 0, 10, 0),
        };

        var side = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = false,
        };
        side.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        side.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        side.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
        };

        var pickFile = new Button { Text = "Выбрать файл…", AutoSize = true, Height = 30 };
        pickFile.Click += (_, _) => PickCoverFile();
        AppIcons.Apply(pickFile, IconKind.Image);

        var findOnline = new Button { Text = "Найти в интернете…", AutoSize = true, Height = 30, Margin = new Padding(8, 3, 3, 3) };
        findOnline.Click += async (_, _) => await FindCoverOnlineAsync();
        AppIcons.Apply(findOnline, IconKind.Search);

        buttons.Controls.Add(pickFile);
        buttons.Controls.Add(findOnline);

        _coverHint = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 8, 0, 0),
            Text = "PNG/JPG/BMP/GIF → PNG. Файл копируется в папку covers при сохранении.",
        };

        side.Controls.Add(buttons, 0, 0);
        side.Controls.Add(_coverHint, 0, 1);

        panel.Controls.Add(_coverPreview, 0, 0);
        panel.Controls.Add(side, 1, 0);
        return panel;
    }

    private GroupBox BuildExtraGroup()
    {
        var group = new GroupBox
        {
            Text = "Дополнительно",
            AutoSize = false,
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 10, 8),
            Margin = new Padding(0, 10, 0, 0),
        };

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
        };

        _elevatedCheck = MakeCheck("elevated (от администратора)");
        _autoDetachCheck = MakeCheck("auto-detach");
        _waitAllCheck = MakeCheck("wait-all");
        _virtualDisplayCheck = MakeCheck("virtual-display");
        _useAppIdentityCheck = MakeCheck("use-app-identity");
        _perClientAppIdentityCheck = MakeCheck("per-client-app-identity");
        _allowClientCommandsCheck = MakeCheck("allow-client-commands");
        _terminateOnPauseCheck = MakeCheck("terminate-on-pause");
        _excludeGlobalPrepCheck = MakeCheck("exclude-global-prep-cmd");
        _excludeGlobalStateCheck = MakeCheck("exclude-global-state-cmd");

        flow.Controls.Add(_elevatedCheck);
        flow.Controls.Add(_autoDetachCheck);
        flow.Controls.Add(_waitAllCheck);
        flow.Controls.Add(_virtualDisplayCheck);
        flow.Controls.Add(_useAppIdentityCheck);
        flow.Controls.Add(_perClientAppIdentityCheck);
        flow.Controls.Add(_allowClientCommandsCheck);
        flow.Controls.Add(_terminateOnPauseCheck);
        flow.Controls.Add(_excludeGlobalPrepCheck);
        flow.Controls.Add(_excludeGlobalStateCheck);

        _scaleFactorBox = new NumericUpDown { Minimum = 1, Maximum = 400, Width = 70 };
        _exitTimeoutBox = new NumericUpDown { Minimum = 0, Maximum = 600, Width = 70 };

        flow.Controls.Add(MakeNumberPanel("scale-factor:", _scaleFactorBox));
        flow.Controls.Add(MakeNumberPanel("exit-timeout:", _exitTimeoutBox));

        _gamepadBox = new ComboBox
        {
            Width = 140,
            DropDownStyle = ComboBoxStyle.DropDown,
            Margin = new Padding(4, 6, 8, 4),
        };
        _gamepadBox.Items.AddRange(new object[]
        {
            "", "disabled", "auto", "generic", "ds4", "ds5", "switch", "x360", "xone", "xseries",
        });

        var gamepadPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(8, 4, 8, 4),
        };
        gamepadPanel.Controls.Add(new Label
        {
            Text = "gamepad:",
            AutoSize = true,
            Margin = new Padding(0, 9, 4, 0),
        });
        gamepadPanel.Controls.Add(_gamepadBox);

        flow.Controls.Add(gamepadPanel);
        group.Controls.Add(flow);
        return group;
    }

    private static CheckBox MakeCheck(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(4, 4, 14, 4),
    };

    private static FlowLayoutPanel MakeNumberPanel(string label, NumericUpDown box) => new()
    {
        AutoSize = true,
        WrapContents = false,
        Margin = new Padding(8, 4, 8, 4),
        Controls =
        {
            new Label { Text = label, AutoSize = true, Margin = new Padding(0, 9, 4, 0) },
            box,
        },
    };

    private static void AddRow(TableLayoutPanel table, string label, Control control)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 8, 0),
        }, 0, row);

        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 4, 0, 4);
        table.Controls.Add(control, 1, row);
    }

    private void ApplyEntryToControls(AppEntry entry)
    {
        _nameBox.Text = entry.Name;

        var (exe, args) = CommandLineUtil.Split(entry.Cmd);
        _exeBox.Text = exe;
        _argsBox.Text = args;
        _workingDirBox.Text = entry.WorkingDir;

        _elevatedCheck.Checked = entry.Elevated;
        _autoDetachCheck.Checked = entry.AutoDetach;
        _waitAllCheck.Checked = entry.WaitAll;
        _virtualDisplayCheck.Checked = entry.VirtualDisplay;
        _useAppIdentityCheck.Checked = entry.UseAppIdentity;
        _perClientAppIdentityCheck.Checked = entry.PerClientAppIdentity;
        _allowClientCommandsCheck.Checked = entry.AllowClientCommands;
        _terminateOnPauseCheck.Checked = entry.TerminateOnPause;
        _excludeGlobalPrepCheck.Checked = entry.ExcludeGlobalPrepCmd;
        _excludeGlobalStateCheck.Checked = entry.ExcludeGlobalStateCmd;

        _scaleFactorBox.Value = Math.Clamp(entry.ScaleFactor, (int)_scaleFactorBox.Minimum, (int)_scaleFactorBox.Maximum);
        _exitTimeoutBox.Value = Math.Clamp(entry.ExitTimeout, (int)_exitTimeoutBox.Minimum, (int)_exitTimeoutBox.Maximum);
        _gamepadBox.Text = entry.Gamepad;

        UpdateCoverPreview(entry.ImagePath);
    }

    private void ApplyControlsTo(AppEntry target)
    {
        target.Name = _nameBox.Text.Trim();

        var exe = _exeBox.Text.Trim();
        target.Cmd = exe.Length == 0 ? string.Empty : CommandLineUtil.Combine(exe, _argsBox.Text.Trim());

        target.WorkingDir = _workingDirBox.Text.Trim();
        if (target.WorkingDir.Length == 0 && exe.Length > 0 &&
            !exe.Contains("://", StringComparison.Ordinal))
        {
            var auto = AutoWorkingDir(exe);
            if (auto.Length > 0)
            {
                target.WorkingDir = auto;
            }
        }

        target.Elevated = _elevatedCheck.Checked;
        target.AutoDetach = _autoDetachCheck.Checked;
        target.WaitAll = _waitAllCheck.Checked;
        target.VirtualDisplay = _virtualDisplayCheck.Checked;
        target.UseAppIdentity = _useAppIdentityCheck.Checked;
        target.PerClientAppIdentity = _perClientAppIdentityCheck.Checked;
        target.AllowClientCommands = _allowClientCommandsCheck.Checked;
        target.TerminateOnPause = _terminateOnPauseCheck.Checked;
        target.ExcludeGlobalPrepCmd = _excludeGlobalPrepCheck.Checked;
        target.ExcludeGlobalStateCmd = _excludeGlobalStateCheck.Checked;
        target.ScaleFactor = (int)_scaleFactorBox.Value;
        target.ExitTimeout = (int)_exitTimeoutBox.Value;
        target.Gamepad = _gamepadBox.Text.Trim();
    }

    private void RefreshJsonFromControls()
    {
        var temp = _entry.Clone();
        ApplyControlsTo(temp);

        _suppressJson = true;
        _jsonBox.Text = temp.ToJson().ToJsonString(JsonUtil.Options);
        _suppressJson = false;
        _jsonDirty = false;
    }

    private bool TryApplyJson()
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(_jsonBox.Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Некорректный JSON: " + ex.Message, "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (node is not JsonObject obj)
        {
            MessageBox.Show(this, "Корневой элемент записи должен быть JSON-объектом.", "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        try
        {
            var parsed = AppEntry.FromJson(obj);
            if (string.IsNullOrWhiteSpace(parsed.Uuid))
            {
                parsed.Uuid = _entry.Uuid;
            }

            if (string.IsNullOrWhiteSpace(parsed.Name))
            {
                parsed.Name = _entry.Name;
            }

            _entry.CopyFrom(parsed);
            ApplyEntryToControls(_entry);
            _jsonDirty = false;
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Не удалось разобрать запись: " + ex.Message, "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    private void Tabs_Selecting(object? sender, TabControlCancelEventArgs e)
    {
        if (_tabs.SelectedTab == _jsonTab && _jsonDirty)
        {
            if (!TryApplyJson())
            {
                e.Cancel = true;
                return;
            }
        }

        if (e.TabPage == _jsonTab)
        {
            RefreshJsonFromControls();
        }
    }

    private bool Commit()
    {
        var fromJson = false;

        if (_tabs.SelectedTab == _jsonTab && _jsonDirty)
        {
            if (!TryApplyJson())
            {
                return false;
            }

            fromJson = true;
        }

        if (!fromJson)
        {
            ApplyControlsTo(_entry);
        }

        if (string.IsNullOrWhiteSpace(_entry.Name))
        {
            MessageBox.Show(this, "Укажите название игры.", "Проверка",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _tabs.SelectedTab = _mainTab;
            _nameBox.Focus();
            return false;
        }

        var exe = CommandLineUtil.ExtractExe(_entry.Cmd);
        if (exe.Length > 0 && !exe.Contains("://", StringComparison.Ordinal) && !File.Exists(exe))
        {
            var result = MessageBox.Show(this,
                "Файл не найден:" + Environment.NewLine + exe + Environment.NewLine + Environment.NewLine +
                "Всё равно сохранить запись?",
                "Проверка", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
            {
                return false;
            }
        }

        if (_coverSourcePath is not null)
        {
            try
            {
                _entry.ImagePath = _coverService.SaveCover(_coverSourcePath, _coversDirectory, _entry.Name);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось сохранить обложку: " + ex.Message, "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        return true;
    }

    private void PickExe(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Выберите исполняемый файл игры",
            Filter = "Исполняемые файлы (*.exe)|*.exe|Все файлы (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _exeBox.Text = dialog.FileName;
        if (string.IsNullOrWhiteSpace(_workingDirBox.Text))
        {
            _workingDirBox.Text = AutoWorkingDir(dialog.FileName);
        }
    }

    /// <summary>Рабочая папка, вычисленная по настройке.</summary>
    private string AutoWorkingDir(string exe)
    {
        if (_workingDirMode == WorkingDirMode.Empty)
        {
            return string.Empty;
        }

        if (_workingDirMode == WorkingDirMode.GameFolder &&
            !string.IsNullOrWhiteSpace(_gameFolder))
        {
            return _gameFolder!;
        }

        // Для ручной записи без папки игры остаётся прежнее поведение — папка exe.
        return WorkingDirUtil.Resolve(WorkingDirMode.ExeFolder, _gameFolder, exe);
    }

    private void PickWorkingDirectory(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Рабочая папка игры",
            UseDescriptionForTitle = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _workingDirBox.Text = dialog.SelectedPath;
        }
    }

    private void PickCoverFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Выберите обложку",
            Filter = "Изображения (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Все файлы (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _coverSourcePath = dialog.FileName;
        UpdateCoverPreview(dialog.FileName);
    }

    private async Task FindCoverOnlineAsync()
    {
        if (_providers.Count == 0)
        {
            MessageBox.Show(this, "Онлайн-провайдеры недоступны.", "Поиск обложки",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var picker = new CoverPickerForm(_providers, _nameBox.Text.Trim());
        if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedResult is null)
        {
            return;
        }

        try
        {
            UseWaitCursor = true;
            _coverHint.Text = "Скачивание обложки…";
            var path = await _coverService.DownloadToCacheAsync(picker.SelectedResult.FullUrl, CancellationToken.None);
            _coverSourcePath = path;
            UpdateCoverPreview(path);
            _coverHint.Text = "Обложка будет скопирована в папку covers при сохранении.";
        }
        catch (Exception ex)
        {
            _coverHint.Text = "Не удалось скачать обложку.";
            MessageBox.Show(this, "Не удалось скачать обложку: " + ex.Message, "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void UpdateCoverPreview(string? path)
    {
        var old = _coverPreview.Image;
        _coverPreview.Image = null;
        old?.Dispose();

        var image = CoverService.LoadImageNoLock(path);
        if (image is not null)
        {
            _coverPreview.Image = image;
            _coverHint.Text = Path.GetFileName(path);
        }
        else if (_coverSourcePath is null)
        {
            _coverHint.Text = "Обложка не выбрана. PNG/JPG/BMP/GIF → PNG, копируется в covers.";
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        DpiScale.Refresh(this);
        DpiScale.ScaleTree(this);
        AppIcons.RefreshTree(this);
        Size = DpiScale.SizeOf(780, 720);
        MinimumSize = DpiScale.SizeOf(700, 600);
        DpiScale.FitToScreen(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var image = _coverPreview.Image;
            _coverPreview.Image = null;
            image?.Dispose();
        }

        base.Dispose(disposing);
    }
}
