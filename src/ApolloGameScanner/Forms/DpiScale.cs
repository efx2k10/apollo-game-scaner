namespace ApolloGameScanner.Forms;

/// <summary>
/// Масштаб интерфейса относительно 96 DPI.
/// Формы создаются в коде, поэтому WinForms не масштабирует их при старте на
/// мониторе с высоким DPI. Размеры контролов, отступы и абсолютные размеры
/// колонок/строк таблиц масштабируются вручную; позиции вычисляет layout.
/// Строки списков создаются во время работы и масштабируются через Px()/Pad().
/// </summary>
internal static class DpiScale
{
    public static float Factor { get; private set; } = 1f;

    public static int Px(double value) => (int)Math.Round(value * Factor);

    public static Padding Pad(int left, int top, int right, int bottom) =>
        new(Px(left), Px(top), Px(right), Px(bottom));

    public static Padding Pad(int all) => Pad(all, all, all, all);

    public static Size SizeOf(int width, int height) => new(Px(width), Px(height));

    /// <summary>Обновляет масштаб по текущему DPI элемента (после создания окна).</summary>
    public static void Refresh(Control control)
    {
        var dpi = control.DeviceDpi;
        Factor = dpi > 0 ? dpi / 96f : 1f;
    }

    /// <summary>Масштабирует размеры контролов формы под текущий DPI.</summary>
    public static void ScaleTree(Control root)
    {
        if (Math.Abs(Factor - 1f) < 0.01f)
        {
            return;
        }

        ScaleControl(root);
    }

    private static void ScaleControl(Control control)
    {
        if (control is TableLayoutPanel table)
        {
            foreach (ColumnStyle style in table.ColumnStyles)
            {
                if (style.SizeType == SizeType.Absolute)
                {
                    style.Width *= Factor;
                }
            }

            foreach (RowStyle style in table.RowStyles)
            {
                if (style.SizeType == SizeType.Absolute)
                {
                    style.Height *= Factor;
                }
            }
        }

        foreach (Control child in control.Controls)
        {
            child.Size = ScaleSize(child.Size);
            child.Margin = ScalePadding(child.Margin);
            child.Padding = ScalePadding(child.Padding);

            if (child.MinimumSize != Size.Empty)
            {
                child.MinimumSize = ScaleSize(child.MinimumSize);
            }

            if (child.MaximumSize != Size.Empty)
            {
                child.MaximumSize = ScaleSize(child.MaximumSize);
            }

            ScaleControl(child);
        }
    }

    /// <summary>Не даёт окну выйти за пределы рабочего стола и центрирует его.</summary>
    public static void FitToScreen(Form form)
    {
        var area = Screen.FromControl(form).WorkingArea;
        var maxWidth = Math.Max(Px(320), area.Width - Px(40));
        var maxHeight = Math.Max(Px(240), area.Height - Px(60));

        var width = Math.Min(form.Width, maxWidth);
        var height = Math.Min(form.Height, maxHeight);
        form.Size = new Size(width, height);

        if (form.StartPosition != FormStartPosition.Manual)
        {
            form.Location = new Point(
                area.X + Math.Max(0, (area.Width - width) / 2),
                area.Y + Math.Max(0, (area.Height - height) / 2));
        }
    }

    private static Padding ScalePadding(Padding padding) =>
        new(Px(padding.Left), Px(padding.Top), Px(padding.Right), Px(padding.Bottom));

    private static Size ScaleSize(Size size) => new(Px(size.Width), Px(size.Height));
}
