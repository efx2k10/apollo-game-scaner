using System.Diagnostics;
using ApolloGameScanner.Models;
using ApolloGameScanner.Services.Covers;

namespace ApolloGameScanner.Forms;

/// <summary>Настройки программы: шаблоны исключений, обложки, SteamGridDB.</summary>
public sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;

    private readonly ListBox _fileList;
    private readonly ListBox _folderList;
    private readonly TextBox _fileInput;
    private readonly TextBox _folderInput;
    private readonly NumericUpDown _depthBox;
    private readonly TextBox _coversBox;
    private readonly TextBox _sgdbBox;
    private readonly RadioButton _workDirGameFolder;
    private readonly RadioButton _workDirExeFolder;
    private readonly RadioButton _workDirEmpty;

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;

        AutoScaleMode = AutoScaleMode.None;

        Text = "Настройки программы";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(800, 900);
        MinimumSize = new Size(700, 600);
        MinimizeBox = false;
        ShowInTaskbar = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(10),
            AutoScroll = true,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // --- файлы ---
        _fileList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        foreach (var pattern in settings.ExcludedFilePatterns)
        {
            _fileList.Items.Add(pattern);
        }

        _fileInput = new TextBox { Dock = DockStyle.Fill };
        _fileInput.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                AddPattern(_fileList, _fileInput);
            }
        };

        var filesGroup = BuildPatternGroup(
            "Исключённые exe (шаблоны имён, поддерживаются * и ?)",
            _fileList,
            _fileInput,
            () => AddPattern(_fileList, _fileInput),
            () => RemoveSelected(_fileList),
            () => ResetPatterns(_fileList, AppSettings.DefaultFilePatterns()));

        // --- папки ---
        _folderList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        foreach (var pattern in settings.ExcludedFolderPatterns)
        {
            _folderList.Items.Add(pattern);
        }

        _folderInput = new TextBox { Dock = DockStyle.Fill };
        _folderInput.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                AddPattern(_folderList, _folderInput);
            }
        };

        var foldersGroup = BuildPatternGroup(
            "Исключённые папки (шаблоны имён на любом уровне)",
            _folderList,
            _folderInput,
            () => AddPattern(_folderList, _folderInput),
            () => RemoveSelected(_folderList),
            () => ResetPatterns(_folderList, AppSettings.DefaultFolderPatterns()));

        // --- обложки и поиск ---
        var coversGroup = new GroupBox
        {
            Text = "Обложки и поиск",
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(10, 6, 10, 8),
            Margin = new Padding(0, 8, 0, 0),
        };

        var coversTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 4,
        };
        coversTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        coversTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        coversTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        coversTable.Controls.Add(new Label
        {
            Text = "Папка для обложек (пусто — covers рядом с apps.json):",
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 2),
        }, 0, 0);
        coversTable.SetColumnSpan(coversTable.GetControlFromPosition(0, 0)!, 3);

        _coversBox = new TextBox
        {
            Dock = DockStyle.Fill,
            // AutoSize-таблица не растягивает процентную колонку, поэтому задаём минимум.
            MinimumSize = new Size(520, 0),
            Text = settings.CoversFolder ?? string.Empty,
        };
        var coversBrowse = new Button { Text = "Обзор…", Width = 100, Height = 26, Margin = new Padding(6, 2, 0, 2) };
        coversBrowse.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "Папка для сохранения обложек" };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _coversBox.Text = dialog.SelectedPath;
            }
        };

        coversTable.Controls.Add(_coversBox, 0, 1);
        AppIcons.Apply(coversBrowse, IconKind.Folder, 14);
        coversTable.Controls.Add(coversBrowse, 1, 1);

        var coversNote = new Label
        {
            Text = "Обложки конвертируются в PNG без изменения разрешения. " +
                   "Apollo принимает только PNG.",
            AutoSize = true,
            MaximumSize = new Size(640, 0),
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 4, 0, 6),
        };
        coversTable.Controls.Add(coversNote, 0, 2);
        coversTable.SetColumnSpan(coversNote, 3);

        var limits = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 2, 0, 0),
        };
        limits.Controls.Add(new Label
        {
            Text = "Максимальная глубина поиска exe (0 — без ограничений):",
            AutoSize = true,
            Margin = new Padding(0, 8, 6, 0),
        });

        _depthBox = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 30,
            Width = 70,
            Value = Math.Clamp(settings.MaxDepth, 0, 30),
        };
        limits.Controls.Add(_depthBox);

        var clearCache = new Button
        {
            Text = "Очистить кэш превью",
            AutoSize = true,
            Height = 28,
            Margin = new Padding(18, 2, 0, 0),
        };
        clearCache.Click += (_, _) =>
        {
            CoverService.ClearCache();
            MessageBox.Show(this, "Кэш превью очищен.", "Настройки",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        AppIcons.Apply(clearCache, IconKind.Delete, 14);
        limits.Controls.Add(clearCache);

        coversTable.Controls.Add(limits, 0, 3);
        coversTable.SetColumnSpan(limits, 3);

        coversGroup.Controls.Add(coversTable);

        // --- SteamGridDB ---
        var sgdbGroup = new GroupBox
        {
            Text = "SteamGridDB (онлайн-обложки)",
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(10, 6, 10, 8),
            Margin = new Padding(0, 8, 0, 0),
        };

        var sgdbTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 3,
        };
        sgdbTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sgdbTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sgdbTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        sgdbTable.Controls.Add(new Label
        {
            Text = "API-ключ SteamGridDB:",
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 2),
        }, 0, 0);
        sgdbTable.SetColumnSpan(sgdbTable.GetControlFromPosition(0, 0)!, 3);

        _sgdbBox = new TextBox { Dock = DockStyle.Fill, Text = settings.SteamGridDbKey };
        sgdbTable.Controls.Add(_sgdbBox, 0, 1);

        var getKey = new LinkLabel
        {
            Text = "Получить ключ",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(8, 6, 8, 0),
        };
        getKey.LinkClicked += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.steamgriddb.com/profile/preferences/api",
                    UseShellExecute = true,
                });
            }
            catch
            {
                // игнорируем
            }
        };
        sgdbTable.Controls.Add(getKey, 1, 1);

        var sgdbNote = new Label
        {
            Text = "DuckDuckGo доступен без ключа, но это неофициальный источник и он может работать нестабильно.",
            AutoSize = true,
            MaximumSize = new Size(640, 0),
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 6, 0, 0),
        };
        sgdbTable.Controls.Add(sgdbNote, 0, 2);
        sgdbTable.SetColumnSpan(sgdbNote, 3);

        sgdbGroup.Controls.Add(sgdbTable);

        // --- рабочая папка ---
        var workDirGroup = new GroupBox
        {
            Text = "Рабочая папка (working-dir) новых записей",
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(10, 6, 10, 8),
            Margin = new Padding(0, 8, 0, 0),
        };

        var workDirTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 4,
            Margin = new Padding(0),
        };
        workDirTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _workDirGameFolder = new RadioButton
        {
            Text = "1. Папка игры (как раньше) — первый уровень сканирования",
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 2),
        };
        _workDirExeFolder = new RadioButton
        {
            Text = "2. Папка выбранного exe (рекомендуется)",
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 2),
        };
        _workDirEmpty = new RadioButton
        {
            Text = "3. Пусто — поле working-dir не записывается",
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 2),
        };

        switch (settings.WorkingDirMode)
        {
            case WorkingDirMode.GameFolder:
                _workDirGameFolder.Checked = true;
                break;
            case WorkingDirMode.Empty:
                _workDirEmpty.Checked = true;
                break;
            default:
                _workDirExeFolder.Checked = true;
                break;
        }

        workDirTable.Controls.Add(_workDirGameFolder, 0, 0);
        workDirTable.Controls.Add(_workDirExeFolder, 0, 1);
        workDirTable.Controls.Add(_workDirEmpty, 0, 2);
        workDirTable.Controls.Add(new Label
        {
            Text = "Применяется при сканировании, при смене exe и при добавлении новых записей. " +
                   "Уже добавленные записи конфига не меняются.",
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 6, 0, 0),
        }, 0, 3);

        workDirGroup.Controls.Add(workDirTable);

        // --- кнопки ---
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(0, 10, 0, 0),
        };

        var ok = new Button
        {
            Text = "OK",
            Width = 110,
            Height = 30,
            DialogResult = DialogResult.None,
        };
        ok.Click += (_, _) => SaveAndClose();
        AppIcons.Apply(ok, IconKind.Check);

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

        root.Controls.Add(filesGroup, 0, 0);
        root.Controls.Add(foldersGroup, 0, 1);
        root.Controls.Add(coversGroup, 0, 2);
        root.Controls.Add(sgdbGroup, 0, 3);
        root.Controls.Add(workDirGroup, 0, 4);
        root.Controls.Add(buttons, 0, 5);

        Controls.Add(root);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        DpiScale.Refresh(this);
        DpiScale.ScaleTree(this);
        AppIcons.RefreshTree(this);
        Size = DpiScale.SizeOf(800, 900);
        MinimumSize = DpiScale.SizeOf(680, 520);
        DpiScale.FitToScreen(this);
    }

    private GroupBox BuildPatternGroup(string title, ListBox list, TextBox input,
        Action add, Action remove, Action reset)
    {
        var group = new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            Height = 190,
            Padding = new Padding(10, 6, 10, 8),
            Margin = new Padding(0, 0, 0, 8),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        list.Margin = new Padding(0, 2, 8, 2);

        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = false,
        };

        input.Width = 210;
        input.Margin = new Padding(0, 2, 0, 6);

        var addButton = new Button { Text = "Добавить", Width = 210, Height = 28, Margin = new Padding(0, 0, 0, 4) };
        addButton.Click += (_, _) => add();
        AppIcons.Apply(addButton, IconKind.Add, 14);

        var removeButton = new Button { Text = "Удалить выбранное", Width = 210, Height = 28, Margin = new Padding(0, 0, 0, 4) };
        removeButton.Click += (_, _) => remove();
        AppIcons.Apply(removeButton, IconKind.Delete, 14);

        var resetButton = new Button { Text = "По умолчанию", Width = 210, Height = 28, Margin = new Padding(0, 0, 0, 4) };
        resetButton.Click += (_, _) => reset();
        AppIcons.Apply(resetButton, IconKind.Refresh, 14);

        right.Controls.Add(input);
        right.Controls.Add(addButton);
        right.Controls.Add(removeButton);
        right.Controls.Add(resetButton);

        layout.Controls.Add(list, 0, 0);
        layout.SetRowSpan(list, 2);
        layout.Controls.Add(right, 1, 0);
        layout.SetRowSpan(right, 2);

        group.Controls.Add(layout);
        return group;
    }

    private static void AddPattern(ListBox list, TextBox input)
    {
        var value = input.Text.Trim();
        if (value.Length == 0)
        {
            return;
        }

        foreach (var item in list.Items)
        {
            if (string.Equals(item?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                input.Clear();
                return;
            }
        }

        list.Items.Add(value);
        input.Clear();
    }

    private static void RemoveSelected(ListBox list)
    {
        if (list.SelectedIndex >= 0)
        {
            list.Items.RemoveAt(list.SelectedIndex);
        }
    }

    private static void ResetPatterns(ListBox list, IEnumerable<string> defaults)
    {
        list.Items.Clear();
        foreach (var pattern in defaults)
        {
            list.Items.Add(pattern);
        }
    }

    private void SaveAndClose()
    {
        _settings.ExcludedFilePatterns = _fileList.Items.Cast<object>()
            .Select(item => item?.ToString() ?? string.Empty)
            .Where(item => item.Length > 0)
            .ToList();

        _settings.ExcludedFolderPatterns = _folderList.Items.Cast<object>()
            .Select(item => item?.ToString() ?? string.Empty)
            .Where(item => item.Length > 0)
            .ToList();

        _settings.MaxDepth = (int)_depthBox.Value;
        _settings.CoversFolder = string.IsNullOrWhiteSpace(_coversBox.Text)
            ? null
            : _coversBox.Text.Trim();
        _settings.SteamGridDbKey = _sgdbBox.Text.Trim();
        _settings.WorkingDirMode = _workDirGameFolder.Checked
            ? WorkingDirMode.GameFolder
            : _workDirEmpty.Checked
                ? WorkingDirMode.Empty
                : WorkingDirMode.ExeFolder;
        _settings.Normalize();

        DialogResult = DialogResult.OK;
        Close();
    }
}
