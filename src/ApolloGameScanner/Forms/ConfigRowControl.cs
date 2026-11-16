using ApolloGameScanner.Models;

namespace ApolloGameScanner.Forms;

/// <summary>Строка списка "Текущий конфиг".</summary>
public sealed class ConfigRowControl : UserControl
{
    public AppEntry Entry { get; }
    public int Index { get; }

    public event EventHandler? EditRequested;
    public event EventHandler? DeleteRequested;
    public event EventHandler? MoveUpRequested;
    public event EventHandler? MoveDownRequested;
    public event EventHandler? SelectionChanged;

    public ConfigRowControl(AppEntry entry, int index, bool canMoveUp, bool canMoveDown)
    {
        Entry = entry;
        Index = index;

        AutoScaleMode = AutoScaleMode.None;

        Height = DpiScale.Px(64);
        Margin = DpiScale.Pad(0, 0, 0, 6);
        BackColor = Color.White;
        BorderStyle = BorderStyle.FixedSingle;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = DpiScale.Pad(8, 6, 6, 6),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DpiScale.Px(32)));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var check = new CheckBox
        {
            Checked = entry.IsSelected,
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            Margin = DpiScale.Pad(2, 0, 2, 0),
        };
        check.CheckedChanged += (_, _) =>
        {
            Entry.IsSelected = check.Checked;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        };

        var info = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
        };
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        info.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiScale.Px(26)));
        info.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var nameLabel = new Label
        {
            Text = string.IsNullOrWhiteSpace(entry.Name) ? "(без названия)" : entry.Name,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            Font = new Font(Font.FontFamily, 10f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0),
        };

        var cmdLabel = new Label
        {
            Text = entry.DisplayCmd,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0),
        };

        info.Controls.Add(nameLabel, 0, 0);
        info.Controls.Add(cmdLabel, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = DpiScale.Pad(4, 0, 0, 0),
        };

        var up = MakeArrowButton(IconKind.Up, canMoveUp, "Переместить выше");
        up.Click += (_, _) => MoveUpRequested?.Invoke(this, EventArgs.Empty);

        var down = MakeArrowButton(IconKind.Down, canMoveDown, "Переместить ниже");
        down.Click += (_, _) => MoveDownRequested?.Invoke(this, EventArgs.Empty);

        var edit = MakeTextButton("Изменить…", IconKind.Edit);
        edit.Click += (_, _) => EditRequested?.Invoke(this, EventArgs.Empty);

        var delete = MakeTextButton("Удалить", IconKind.Delete);
        delete.Click += (_, _) => DeleteRequested?.Invoke(this, EventArgs.Empty);

        buttons.Controls.Add(up);
        buttons.Controls.Add(down);
        buttons.Controls.Add(edit);
        buttons.Controls.Add(delete);

        layout.Controls.Add(check, 0, 0);
        layout.Controls.Add(info, 1, 0);
        layout.Controls.Add(buttons, 2, 0);

        Controls.Add(layout);
    }

    private static Button MakeArrowButton(IconKind kind, bool enabled, string tooltip)
    {
        var button = new Button
        {
            AutoSize = false,
            Width = DpiScale.Px(34),
            Height = DpiScale.Px(28),
            Enabled = enabled,
            Margin = DpiScale.Pad(0, 0, 3, 0),
            Image = AppIcons.Get(kind, DpiScale.Px(14)),
            ImageAlign = ContentAlignment.MiddleCenter,
        };
        new ToolTip().SetToolTip(button, tooltip);
        return button;
    }

    private static Button MakeTextButton(string text, IconKind kind)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = DpiScale.Pad(6, 2, 6, 2),
            Margin = DpiScale.Pad(5, 0, 0, 0),
        };
        AppIcons.Apply(button, kind, 14);
        return button;
    }
}
