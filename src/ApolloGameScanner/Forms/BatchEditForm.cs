using ApolloGameScanner.Models;
using ApolloGameScanner.Services;

namespace ApolloGameScanner.Forms;

/// <summary>Что делать с обложками при пакетном редактировании.</summary>
public enum BatchCoverAction
{
    None,
    FindOnline,
    SetFile,
    Clear,
}

/// <summary>
/// Пакетное редактирование записей: применяются только отмеченные поля.
/// </summary>
public sealed class BatchEditForm : Form
{
    private readonly CheckBox _argsCheck;
    private readonly TextBox _argsBox;
    private readonly CheckBox _workingDirCheck;
    private readonly ComboBox _workingDirBox;
    private readonly CheckBox _scaleCheck;
    private readonly NumericUpDown _scaleBox;
    private readonly CheckBox _exitCheck;
    private readonly NumericUpDown _exitBox;
    private readonly CheckBox _gamepadCheck;
    private readonly ComboBox _gamepadBox;

    private readonly RadioButton _coverNone;
    private readonly RadioButton _coverFind;
    private readonly RadioButton _coverFile;
    private readonly TextBox _coverFileBox;
    private readonly Button _coverBrowse;
    private readonly RadioButton _coverClear;

    private readonly List<(CheckBox Box, Action<AppEntry, bool> Set)> _flags = new();

    public BatchEditForm(int count)
    {
        AutoScaleMode = AutoScaleMode.None;

        Text = "Пакетное редактирование";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(780, 760);
        MinimumSize = new Size(640, 560);
        MinimizeBox = false;
        ShowInTaskbar = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(10),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(new Label
        {
            Text = $"Будет изменено записей: {count}. Применяются только отмеченные поля.",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
        }, 0, 0);

        // --- основные поля ---
        var fields = new GroupBox
        {
            Text = "Поля",
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(10, 6, 10, 8),
        };

        var fieldsTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 0,
            Margin = new Padding(0),
        };
        fieldsTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fieldsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _argsCheck = new CheckBox { Text = "Аргументы:", AutoSize = true, Margin = new Padding(0, 7, 8, 4) };
        _argsBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4) };
        _argsCheck.CheckedChanged += (_, _) => _argsBox.Enabled = _argsCheck.Checked;
        _argsBox.Enabled = false;
        AddFieldRow(fieldsTable, _argsCheck, _argsBox);

        _workingDirCheck = new CheckBox
        {
            Text = "Рабочая папка:",
            AutoSize = true,
            Margin = new Padding(0, 7, 8, 4),
        };
        _workingDirBox = new ComboBox
        {
            Dock = DockStyle.Left,
            Width = 260,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 4, 0, 4),
            Enabled = false,
        };
        _workingDirBox.Items.AddRange(new object[]
        {
            "Папка игры",
            "Папка выбранного exe",
            "Пусто (очистить working-dir)",
        });
        _workingDirBox.SelectedIndex = 1;
        _workingDirCheck.CheckedChanged += (_, _) => _workingDirBox.Enabled = _workingDirCheck.Checked;
        AddFieldRow(fieldsTable, _workingDirCheck, _workingDirBox);

        _scaleCheck = new CheckBox
        {
            Text = "scale-factor:", AutoSize = true, Margin = new Padding(0, 7, 8, 4),
        };
        _scaleBox = new NumericUpDown
        {
            Minimum = 1, Maximum = 400, Value = 100, Width = 80,
            Margin = new Padding(0, 4, 0, 4), Enabled = false,
        };
        _scaleCheck.CheckedChanged += (_, _) => _scaleBox.Enabled = _scaleCheck.Checked;
        AddFieldRow(fieldsTable, _scaleCheck, _scaleBox);

        _exitCheck = new CheckBox
        {
            Text = "exit-timeout:", AutoSize = true, Margin = new Padding(0, 7, 8, 4),
        };
        _exitBox = new NumericUpDown
        {
            Minimum = 0, Maximum = 600, Value = 5, Width = 80,
            Margin = new Padding(0, 4, 0, 4), Enabled = false,
        };
        _exitCheck.CheckedChanged += (_, _) => _exitBox.Enabled = _exitCheck.Checked;
        AddFieldRow(fieldsTable, _exitCheck, _exitBox);

        _gamepadCheck = new CheckBox
        {
            Text = "gamepad:", AutoSize = true, Margin = new Padding(0, 7, 8, 4),
        };
        _gamepadBox = new ComboBox
        {
            Dock = DockStyle.Left,
            Width = 160,
            DropDownStyle = ComboBoxStyle.DropDown,
            Margin = new Padding(0, 4, 0, 4),
            Enabled = false,
        };
        _gamepadBox.Items.AddRange(new object[]
        {
            "", "disabled", "auto", "generic", "ds4", "ds5", "switch", "x360", "xone", "xseries",
        });
        _gamepadCheck.CheckedChanged += (_, _) => _gamepadBox.Enabled = _gamepadCheck.Checked;
        AddFieldRow(fieldsTable, _gamepadCheck, _gamepadBox);

        fields.Controls.Add(fieldsTable);

        // --- флаги ---
        var flagsGroup = new GroupBox
        {
            Text = "Дополнительные флаги",
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 10, 8),
        };

        var flagsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
        };
        flagsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        flagsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        flagsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var flagsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0),
        };

        AddFlag(flagsFlow, "elevated", (entry, value) => entry.Elevated = value);
        AddFlag(flagsFlow, "auto-detach", (entry, value) => entry.AutoDetach = value);
        AddFlag(flagsFlow, "wait-all", (entry, value) => entry.WaitAll = value);
        AddFlag(flagsFlow, "virtual-display", (entry, value) => entry.VirtualDisplay = value);
        AddFlag(flagsFlow, "use-app-identity", (entry, value) => entry.UseAppIdentity = value);
        AddFlag(flagsFlow, "per-client-app-identity", (entry, value) => entry.PerClientAppIdentity = value);
        AddFlag(flagsFlow, "allow-client-commands", (entry, value) => entry.AllowClientCommands = value);
        AddFlag(flagsFlow, "terminate-on-pause", (entry, value) => entry.TerminateOnPause = value);
        AddFlag(flagsFlow, "exclude-global-prep-cmd", (entry, value) => entry.ExcludeGlobalPrepCmd = value);
        AddFlag(flagsFlow, "exclude-global-state-cmd", (entry, value) => entry.ExcludeGlobalStateCmd = value);

        flagsLayout.Controls.Add(flagsFlow, 0, 0);
        flagsLayout.Controls.Add(new Label
        {
            Text = "Серый флажок — не менять, установленный — включить, снятый — выключить.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 6, 0, 0),
        }, 0, 1);

        flagsGroup.Controls.Add(flagsLayout);

        // --- обложки ---
        var coverGroup = new GroupBox
        {
            Text = "Обложки (image-path)",
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(10, 6, 10, 8),
            Margin = new Padding(0, 8, 0, 0),
        };

        var coverTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 5,
            Margin = new Padding(0),
        };
        coverTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _coverNone = new RadioButton
        {
            Text = "Не менять",
            Checked = true,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 2),
        };
        _coverFind = new RadioButton
        {
            Text = "Найти в интернете для каждой записи",
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 2),
        };
        _coverClear = new RadioButton
        {
            Text = "Убрать обложку (очистить image-path)",
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 2),
        };
        _coverFile = new RadioButton
        {
            Text = "Одна картинка для всех:",
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 2),
        };

        var fileRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(22, 0, 0, 2),
        };
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _coverFileBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 6, 2),
            Enabled = false,
        };
        _coverBrowse = new Button
        {
            Text = "Выбрать…",
            Height = 26,
            AutoSize = true,
            Enabled = false,
            Margin = new Padding(0, 2, 0, 2),
        };
        _coverBrowse.Click += (_, _) => PickCoverFile();
        AppIcons.Apply(_coverBrowse, IconKind.Image, 14);
        _coverFile.CheckedChanged += (_, _) =>
        {
            _coverFileBox.Enabled = _coverFile.Checked;
            _coverBrowse.Enabled = _coverFile.Checked;
        };

        fileRow.Controls.Add(_coverFileBox, 0, 0);
        fileRow.Controls.Add(_coverBrowse, 1, 0);

        coverTable.Controls.Add(_coverNone, 0, 0);
        coverTable.Controls.Add(_coverFind, 0, 1);
        coverTable.Controls.Add(_coverFile, 0, 2);
        coverTable.Controls.Add(fileRow, 0, 3);
        coverTable.Controls.Add(_coverClear, 0, 4);

        coverGroup.Controls.Add(coverTable);

        // --- кнопки ---
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0),
        };

        var ok = new Button
        {
            Text = "Применить",
            Width = 120,
            Height = 30,
            DialogResult = DialogResult.None,
        };
        ok.Click += (_, _) =>
        {
            if (!HasChanges())
            {
                MessageBox.Show(this, "Отметьте хотя бы одно поле для изменения.",
                    "Пакетное редактирование", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (CoverAction == BatchCoverAction.SetFile && !File.Exists(CoverFilePath))
            {
                MessageBox.Show(this, "Укажите существующий файл изображения для обложки.",
                    "Пакетное редактирование", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };
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

        root.Controls.Add(fields, 0, 1);
        root.Controls.Add(flagsGroup, 0, 2);
        root.Controls.Add(coverGroup, 0, 3);
        root.Controls.Add(buttons, 0, 4);

        Controls.Add(root);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    /// <summary>Выбранный режим рабочей папки; <c>null</c> — не менять.</summary>
    public WorkingDirMode? SelectedWorkingDirMode
    {
        get
        {
            if (!_workingDirCheck.Checked)
            {
                return null;
            }

            return _workingDirBox.SelectedIndex switch
            {
                0 => Models.WorkingDirMode.GameFolder,
                2 => Models.WorkingDirMode.Empty,
                _ => Models.WorkingDirMode.ExeFolder,
            };
        }
    }

    /// <summary>Что делать с обложками.</summary>
    public BatchCoverAction CoverAction
    {
        get
        {
            if (_coverFind.Checked)
            {
                return BatchCoverAction.FindOnline;
            }

            if (_coverFile.Checked)
            {
                return BatchCoverAction.SetFile;
            }

            return _coverClear.Checked ? BatchCoverAction.Clear : BatchCoverAction.None;
        }
    }

    /// <summary>Путь к картинке для режима «Одна картинка для всех».</summary>
    public string CoverFilePath => _coverFileBox.Text.Trim();

    public void ApplyTo(AppEntry entry, string? gameFolder, string? exePath)
    {
        if (_argsCheck.Checked)
        {
            var exe = CommandLineUtil.ExtractExe(entry.Cmd);
            entry.Cmd = CommandLineUtil.Combine(exe, _argsBox.Text.Trim());
        }

        if (SelectedWorkingDirMode is { } mode)
        {
            var folder = gameFolder;
            if (mode == Models.WorkingDirMode.GameFolder && string.IsNullOrWhiteSpace(folder))
            {
                // Папка игры неизвестна (например, запись без совпадения) —
                // используем папку exe, чтобы не оставить пустое значение.
                mode = Models.WorkingDirMode.ExeFolder;
            }

            if (mode != Models.WorkingDirMode.Empty)
            {
                exePath = string.IsNullOrWhiteSpace(exePath)
                    ? CommandLineUtil.ExtractExe(entry.Cmd)
                    : exePath;
            }

            entry.WorkingDir = WorkingDirUtil.Resolve(mode, folder, exePath);
        }

        if (_scaleCheck.Checked)
        {
            entry.ScaleFactor = (int)_scaleBox.Value;
        }

        if (_exitCheck.Checked)
        {
            entry.ExitTimeout = (int)_exitBox.Value;
        }

        if (_gamepadCheck.Checked)
        {
            entry.Gamepad = _gamepadBox.Text.Trim();
        }

        foreach (var (box, set) in _flags)
        {
            if (box.CheckState != CheckState.Indeterminate)
            {
                set(entry, box.CheckState == CheckState.Checked);
            }
        }
    }

    private void PickCoverFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Картинка для обложки",
            Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Все файлы|*.*",
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _coverFileBox.Text = dialog.FileName;
            _coverFile.Checked = true;
        }
    }

    private bool HasChanges()
    {
        if (_argsCheck.Checked || _workingDirCheck.Checked || _scaleCheck.Checked ||
            _exitCheck.Checked || _gamepadCheck.Checked)
        {
            return true;
        }

        if (CoverAction != BatchCoverAction.None)
        {
            return true;
        }

        return _flags.Any(flag => flag.Box.CheckState != CheckState.Indeterminate);
    }

    private void AddFlag(FlowLayoutPanel flow, string text, Action<AppEntry, bool> set)
    {
        var box = new CheckBox
        {
            Text = text,
            AutoSize = true,
            ThreeState = true,
            CheckState = CheckState.Indeterminate,
            Margin = new Padding(4, 4, 16, 4),
        };
        _flags.Add((box, set));
        flow.Controls.Add(box);
    }

    private static void AddFieldRow(TableLayoutPanel table, CheckBox check, Control control)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(check, 0, row);
        table.Controls.Add(control, 1, row);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        DpiScale.Refresh(this);
        DpiScale.ScaleTree(this);
        AppIcons.RefreshTree(this);
        Size = DpiScale.SizeOf(780, 760);
        MinimumSize = DpiScale.SizeOf(640, 560);
        DpiScale.FitToScreen(this);
    }
}
