using ApolloGameScanner.Models;
using ApolloGameScanner.Services;
using ApolloGameScanner.Services.Covers;

namespace ApolloGameScanner.Forms;

/// <summary>Строка списка найденных игр: имя, список exe, галочка добавления, превью, кнопка правки.</summary>
public sealed class GameRowControl : UserControl
{
    private static readonly Color NewBack = Color.White;
    private static readonly Color ExistingBack = Color.FromArgb(233, 245, 233);
    private static readonly Color NoExeBack = Color.FromArgb(242, 242, 242);

    private readonly CheckBox _check;
    private readonly Label _nameLabel;
    private readonly Label _statusLabel;
    private readonly ComboBox _exeBox;
    private readonly PictureBox _preview;
    private readonly Button _editButton;
    private readonly ToolTip _toolTip = new();
    private readonly WorkingDirMode _workingDirMode;
    private bool _updating;

    public DetectedGame Game { get; }

    public event EventHandler? StateChanged;
    public event EventHandler? EditRequested;

    public GameRowControl(DetectedGame game, WorkingDirMode workingDirMode)
    {
        Game = game;
        _workingDirMode = workingDirMode;

        // Строка создаётся во время работы, поэтому масштабируем размеры вручную.
        AutoScaleMode = AutoScaleMode.None;

        Height = DpiScale.Px(78);
        Margin = DpiScale.Pad(0, 0, 0, 6);
        BackColor = NewBack;
        BorderStyle = BorderStyle.FixedSingle;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Padding = DpiScale.Pad(4, 5, 4, 5),
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DpiScale.Px(32)));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DpiScale.Px(56)));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _check = new CheckBox
        {
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            Margin = DpiScale.Pad(2, 0, 2, 0),
        };
        _check.CheckedChanged += Check_CheckedChanged;

        var info = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
        };
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        info.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiScale.Px(28)));
        info.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titlePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
        };
        titlePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        titlePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _nameLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            Font = new Font(Font.FontFamily, 10f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0),
        };

        _statusLabel = new Label
        {
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = DpiScale.Pad(8, 0, 0, 0),
        };

        titlePanel.Controls.Add(_nameLabel, 0, 0);
        titlePanel.Controls.Add(_statusLabel, 1, 0);

        _exeBox = new ComboBox
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = DpiScale.Pad(0, 3, 0, 0),
            IntegralHeight = false,
            MaxDropDownItems = 14,
        };
        _exeBox.SelectedIndexChanged += ExeBox_SelectedIndexChanged;

        info.Controls.Add(titlePanel, 0, 0);
        info.Controls.Add(_exeBox, 0, 1);

        _preview = new PictureBox
        {
            Size = DpiScale.SizeOf(48, 62),
            Anchor = AnchorStyles.None,
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(250, 250, 250),
            Margin = DpiScale.Pad(2),
        };

        _editButton = new Button
        {
            Text = "Изменить…",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.None,
            Margin = DpiScale.Pad(4, 2, 2, 2),
            Padding = DpiScale.Pad(6, 2, 6, 2),
        };
        _editButton.Click += (_, _) => EditRequested?.Invoke(this, EventArgs.Empty);
        AppIcons.Apply(_editButton, IconKind.Edit);

        layout.Controls.Add(_check, 0, 0);
        layout.Controls.Add(info, 1, 0);
        layout.Controls.Add(_preview, 2, 0);
        layout.Controls.Add(_editButton, 3, 0);

        Controls.Add(layout);

        SyncFromModel();
    }

    public void SyncFromModel()
    {
        _updating = true;
        try
        {
            _nameLabel.Text = string.IsNullOrWhiteSpace(Game.Draft.Name) ? Game.Name : Game.Draft.Name;

            BackColor = Game.Status switch
            {
                GameStatus.Existing => ExistingBack,
                GameStatus.NoExe => NoExeBack,
                _ => NewBack,
            };

            _statusLabel.Text = Game.Status switch
            {
                GameStatus.Existing => "уже в конфиге",
                GameStatus.NoExe => "нет exe",
                _ => string.Empty,
            };

            _statusLabel.ForeColor = Game.Status switch
            {
                GameStatus.Existing => Color.FromArgb(27, 94, 32),
                GameStatus.NoExe => Color.Gray,
                _ => Color.DimGray,
            };

            // Отмечать можно и новые игры (для добавления), и уже сохранённые
            // в конфиге (для пакетных действий с их записями).
            var selectable = Game.Status == GameStatus.Existing ||
                             (Game.Status == GameStatus.New && Game.Candidates.Count > 0);
            _check.Enabled = selectable;
            _check.Checked = selectable && Game.IsSelected;

            _toolTip.SetToolTip(_check, Game.Status switch
            {
                GameStatus.Existing => "Отметить игру для пакетных действий с её записью в конфиге.",
                GameStatus.NoExe => "Исполняемые файлы не найдены или исключены шаблонами.",
                _ => Game.Candidates.Count > 0
                    ? "Отметить игру для добавления в конфиг и пакетных действий."
                    : "Нет подходящих exe — отметить нельзя.",
            });

            _exeBox.BeginUpdate();
            try
            {
                _exeBox.Items.Clear();

                if (Game.Status == GameStatus.NoExe)
                {
                    var suffix = Game.ExcludedFiles > 0
                        ? $" (исключено файлов: {Game.ExcludedFiles})"
                        : string.Empty;
                    _exeBox.Items.Add("exe не найдены" + suffix);
                    _exeBox.SelectedIndex = 0;
                    _exeBox.Enabled = false;
                }
                else if (Game.Status == GameStatus.Existing)
                {
                    var exe = CommandLineUtil.ExtractExe(Game.ExistingApp?.Cmd);
                    var display = string.IsNullOrEmpty(exe) ? "команда не задана" : exe;
                    _exeBox.Items.Add(new ExeChoice(null, display));
                    _exeBox.SelectedIndex = 0;
                    _exeBox.Enabled = false;
                }
                else
                {
                    foreach (var candidate in Game.Candidates)
                    {
                        _exeBox.Items.Add(new ExeChoice(candidate, candidate.ToString()));
                    }

                    var selectedIndex = 0;
                    if (Game.SelectedCandidate is not null)
                    {
                        for (var index = 0; index < Game.Candidates.Count; index++)
                        {
                            if (ReferenceEquals(Game.Candidates[index], Game.SelectedCandidate))
                            {
                                selectedIndex = index;
                                break;
                            }
                        }
                    }

                    if (_exeBox.Items.Count > 0)
                    {
                        _exeBox.SelectedIndex = selectedIndex;
                    }

                    _exeBox.Enabled = true;
                    _exeBox.DropDownWidth = Math.Max(DpiScale.Px(500), _exeBox.Width * 2);
                }
            }
            finally
            {
                _exeBox.EndUpdate();
            }

            LoadPreview(Game.Status == GameStatus.Existing
                ? Game.ExistingApp?.ImagePath
                : string.IsNullOrWhiteSpace(Game.CoverPreviewPath) ? Game.Draft.ImagePath : Game.CoverPreviewPath);

            _toolTip.SetToolTip(_nameLabel, Game.FolderPath);
        }
        finally
        {
            _updating = false;
        }
    }

    private void LoadPreview(string? path)
    {
        var old = _preview.Image;
        _preview.Image = null;
        old?.Dispose();

        var image = CoverService.LoadImageNoLock(path);
        if (image is not null)
        {
            _preview.Image = image;
        }
    }

    private void Check_CheckedChanged(object? sender, EventArgs e)
    {
        if (_updating)
        {
            return;
        }

        Game.IsSelected = _check.Checked;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ExeBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_updating || Game.Status != GameStatus.New)
        {
            return;
        }

        if (_exeBox.SelectedItem is not ExeChoice choice || choice.Candidate is null)
        {
            return;
        }

        Game.SelectedCandidate = choice.Candidate;
        var existingArgs = CommandLineUtil.Split(Game.Draft.Cmd).Args;
        Game.Draft.Cmd = CommandLineUtil.Combine(choice.Candidate.FullPath, existingArgs);
        Game.Draft.WorkingDir = WorkingDirUtil.Resolve(
            _workingDirMode, Game.FolderPath, choice.Candidate.FullPath);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var image = _preview.Image;
            _preview.Image = null;
            image?.Dispose();
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class ExeChoice
    {
        public ExeCandidate? Candidate { get; }
        private readonly string _display;

        public ExeChoice(ExeCandidate? candidate, string display)
        {
            Candidate = candidate;
            _display = display;
        }

        public override string ToString() => _display;
    }
}
