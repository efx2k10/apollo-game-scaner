using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;

namespace ApolloGameScanner.Forms;

/// <summary>Набор векторных иконок интерфейса (рисуются GDI+, без внешних файлов).</summary>
public enum IconKind
{
    Folder,
    New,
    Open,
    Save,
    SaveAs,
    Exit,
    Settings,
    Help,
    Search,
    Scan,
    Add,
    AddList,
    Edit,
    Delete,
    Up,
    Down,
    Cancel,
    Check,
    Image,
    SelectAll,
    ClearAll,
    Document,
    Gamepad,
    Refresh,
}

/// <summary>
/// Иконки в едином стиле: рисуются программно, кэшируются и масштабируются
/// под текущий DPI. Не требуют ресурсов и внешних библиотек.
/// </summary>
internal static class AppIcons
{
    private static readonly Dictionary<(IconKind Kind, int Size, int Argb), Bitmap> Cache = new();
    private static readonly ConditionalWeakTable<object, Binding> Bindings = new();

    private sealed class Binding
    {
        public IconKind Kind { get; init; }
        public int LogicalSize { get; init; } = 16;
        public Color? Color { get; init; }
    }

    public static Bitmap Get(IconKind kind, int size = 16, Color? color = null)
    {
        size = Math.Max(8, size);
        var accent = color ?? Accent(kind);
        var key = (kind, size, accent.ToArgb());
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var bitmap = Render(kind, size, accent);
        Cache[key] = bitmap;
        return bitmap;
    }

    /// <summary>Ставит иконку на кнопку слева от текста.</summary>
    public static void Apply(Button button, IconKind kind, int logicalSize = 16, Color? color = null)
    {
        button.Image = Get(kind, DpiScale.Px(logicalSize), color);
        button.ImageAlign = ContentAlignment.MiddleLeft;
        button.TextImageRelation = TextImageRelation.ImageBeforeText;

        if (button.Padding == Padding.Empty)
        {
            button.Padding = DpiScale.Pad(6, 2, 8, 2);
        }

        Bindings.AddOrUpdate(button, new Binding
        {
            Kind = kind,
            LogicalSize = logicalSize,
            Color = color,
        });
    }

    /// <summary>Ставит иконку на пункт меню.</summary>
    public static void Apply(ToolStripMenuItem item, IconKind kind, int logicalSize = 16)
    {
        item.Image = Get(kind, DpiScale.Px(logicalSize));
        item.ImageScaling = ToolStripItemImageScaling.None;
        Bindings.AddOrUpdate(item, new Binding { Kind = kind, LogicalSize = logicalSize });
    }

    /// <summary>Помечает вкладки как использующие стандартные иконки.</summary>
    public static void ApplyTabs(TabControl tabs)
    {
        tabs.ImageList = CreateTabImages();
        Bindings.AddOrUpdate(tabs, new Binding { Kind = IconKind.Gamepad, LogicalSize = 16 });
    }

    /// <summary>
    /// Пересоздаёт иконки после того, как форма узнала реальный DPI
    /// (в ctor размер иконки ещё рассчитывается по 96 DPI).
    /// </summary>
    public static void RefreshTree(Form form)
    {
        if (form.MainMenuStrip is { } menu)
        {
            menu.ImageScalingSize = new Size(DpiScale.Px(16), DpiScale.Px(16));
            foreach (ToolStripItem item in menu.Items)
            {
                RefreshItem(item);
            }
        }

        RefreshControl(form);
    }

    private static void RefreshItem(ToolStripItem item)
    {
        if (Bindings.TryGetValue(item, out var binding) &&
            item is ToolStripMenuItem menuItem)
        {
            menuItem.Image = Get(binding.Kind, DpiScale.Px(binding.LogicalSize), binding.Color);
        }

        if (item is ToolStripDropDownItem dropDown)
        {
            foreach (ToolStripItem child in dropDown.DropDownItems)
            {
                RefreshItem(child);
            }
        }
    }

    private static void RefreshControl(Control control)
    {
        if (Bindings.TryGetValue(control, out var binding))
        {
            switch (control)
            {
                case Button button:
                    button.Image = Get(binding.Kind, DpiScale.Px(binding.LogicalSize), binding.Color);
                    break;
                case TabControl tabs:
                {
                    var list = CreateTabImages();
                    var previous = tabs.ImageList;
                    tabs.ImageList = list;
                    previous?.Dispose();
                    break;
                }
            }
        }

        foreach (Control child in control.Controls)
        {
            RefreshControl(child);
        }
    }

    /// <summary>Список картинок для вкладок (0 — игры, 1 — конфиг).</summary>
    public static ImageList CreateTabImages()
    {
        var size = DpiScale.Px(16);
        var list = new ImageList
        {
            ImageSize = new Size(size, size),
            ColorDepth = ColorDepth.Depth32Bit,
        };
        list.Images.Add("games", Get(IconKind.Gamepad, size));
        list.Images.Add("config", Get(IconKind.Document, size));
        return list;
    }

    private static Color Accent(IconKind kind) => kind switch
    {
        IconKind.Folder => Color.FromArgb(196, 132, 38),
        IconKind.New or IconKind.Add or IconKind.AddList or IconKind.SelectAll
            => Color.FromArgb(46, 125, 50),
        IconKind.Delete or IconKind.Cancel => Color.FromArgb(198, 40, 40),
        IconKind.Save => Color.FromArgb(0, 121, 107),
        IconKind.SaveAs => Color.FromArgb(2, 119, 189),
        IconKind.Settings => Color.FromArgb(84, 110, 122),
        IconKind.Image => Color.FromArgb(94, 83, 168),
        IconKind.Help => Color.FromArgb(25, 118, 210),
        IconKind.Edit => Color.FromArgb(230, 126, 34),
        IconKind.Gamepad => Color.FromArgb(60, 110, 160),
        IconKind.Up or IconKind.Down => Color.FromArgb(96, 125, 139),
        IconKind.ClearAll => Color.FromArgb(117, 117, 117),
        IconKind.Exit => Color.FromArgb(123, 75, 75),
        IconKind.Document => Color.FromArgb(96, 125, 139),
        IconKind.Check => Color.FromArgb(46, 125, 50),
        IconKind.Scan or IconKind.Refresh => Color.FromArgb(25, 118, 210),
        _ => Color.FromArgb(70, 90, 110),
    };

    private static Bitmap Render(IconKind kind, int size, Color color)
    {
        const int supersample = 4;
        var scale = size * supersample;

        using var source = new Bitmap(scale, scale, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(source))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);
            Draw(graphics, kind, scale, color);
        }

        var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(result))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);
            graphics.DrawImage(source, new Rectangle(0, 0, size, size));
        }

        return result;
    }

    private static void Draw(Graphics g, IconKind kind, int s, Color color)
    {
        PointF P(float x, float y) => new(x * s, y * s);
        float U(float value) => value * s;
        float W(float value) => Math.Max(1f, value * s);

        Pen MakePen(float width) => new(color, W(width))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };

        using var fill = new SolidBrush(color);

        switch (kind)
        {
            case IconKind.Folder:
            {
                using var path = new GraphicsPath();
                path.AddPolygon(new[]
                {
                    P(0.10f, 0.26f), P(0.40f, 0.26f), P(0.48f, 0.36f),
                    P(0.90f, 0.36f), P(0.90f, 0.84f), P(0.10f, 0.84f),
                });
                g.FillPath(fill, path);
                using var outline = new Pen(Darken(color, 0.22f), W(0.06f))
                {
                    LineJoin = LineJoin.Round,
                };
                g.DrawPath(outline, path);
                using var highlight = new Pen(Color.FromArgb(110, 255, 255, 255), W(0.05f));
                g.DrawLine(highlight, P(0.15f, 0.46f), P(0.40f, 0.46f));
                break;
            }

            case IconKind.Search:
            {
                using var pen = MakePen(0.11f);
                g.DrawEllipse(pen, U(0.13f), U(0.13f), U(0.52f), U(0.52f));
                g.DrawLine(pen, P(0.60f, 0.60f), P(0.88f, 0.88f));
                break;
            }

            case IconKind.Scan:
            {
                using var pen = MakePen(0.09f);
                g.DrawEllipse(pen, U(0.10f), U(0.10f), U(0.80f), U(0.80f));
                g.DrawEllipse(pen, U(0.30f), U(0.30f), U(0.40f), U(0.40f));
                g.DrawLine(pen, P(0.50f, 0.50f), P(0.82f, 0.20f));
                using var dot = new SolidBrush(color);
                g.FillEllipse(dot, U(0.44f), U(0.44f), U(0.12f), U(0.12f));
                break;
            }

            case IconKind.Refresh:
            {
                using var pen = MakePen(0.12f);
                g.DrawArc(pen, U(0.14f), U(0.14f), U(0.72f), U(0.72f), 40, 280);
                g.FillPolygon(fill, new[]
                {
                    P(0.72f, 0.08f), P(0.94f, 0.34f), P(0.62f, 0.34f),
                });
                break;
            }

            case IconKind.Add:
            {
                using var pen = MakePen(0.15f);
                g.DrawLine(pen, P(0.50f, 0.14f), P(0.50f, 0.86f));
                g.DrawLine(pen, P(0.14f, 0.50f), P(0.86f, 0.50f));
                break;
            }

            case IconKind.AddList:
            {
                using var pen = MakePen(0.09f);
                g.DrawLine(pen, P(0.10f, 0.22f), P(0.48f, 0.22f));
                g.DrawLine(pen, P(0.10f, 0.42f), P(0.48f, 0.42f));
                g.DrawLine(pen, P(0.10f, 0.62f), P(0.34f, 0.62f));
                using var plus = MakePen(0.11f);
                g.DrawLine(plus, P(0.72f, 0.52f), P(0.72f, 0.88f));
                g.DrawLine(plus, P(0.54f, 0.70f), P(0.90f, 0.70f));
                break;
            }

            case IconKind.Save:
                DrawSave(g, s, color, withArrow: false);
                break;

            case IconKind.SaveAs:
                DrawSave(g, s, color, withArrow: true);
                break;

            case IconKind.Exit:
            {
                using var pen = MakePen(0.10f);
                g.DrawLine(pen, P(0.16f, 0.14f), P(0.16f, 0.86f));
                g.DrawLine(pen, P(0.16f, 0.14f), P(0.50f, 0.14f));
                g.DrawLine(pen, P(0.16f, 0.86f), P(0.50f, 0.86f));
                g.DrawLine(pen, P(0.36f, 0.50f), P(0.88f, 0.50f));
                g.DrawLine(pen, P(0.70f, 0.32f), P(0.88f, 0.50f));
                g.DrawLine(pen, P(0.70f, 0.68f), P(0.88f, 0.50f));
                break;
            }

            case IconKind.Settings:
            {
                var points = new List<PointF>();
                for (var i = 0; i < 16; i++)
                {
                    var angle = i * Math.PI / 8 - Math.PI / 2;
                    var radius = i % 2 == 0 ? 0.46f : 0.34f;
                    points.Add(new PointF(
                        (0.5f + radius * (float)Math.Cos(angle)) * s,
                        (0.5f + radius * (float)Math.Sin(angle)) * s));
                }

                using var path = new GraphicsPath();
                path.AddPolygon(points.ToArray());
                path.AddEllipse(U(0.36f), U(0.36f), U(0.28f), U(0.28f));
                path.FillMode = FillMode.Alternate;
                g.FillPath(fill, path);
                break;
            }

            case IconKind.Help:
            {
                g.FillEllipse(fill, U(0.10f), U(0.10f), U(0.80f), U(0.80f));
                using var font = new Font("Segoe UI", s * 0.46f, FontStyle.Bold, GraphicsUnit.Pixel);
                using var white = new SolidBrush(Color.White);
                using var format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                };
                g.DrawString("i", font, white, new RectangleF(0, 0, s, s * 0.98f), format);
                break;
            }

            case IconKind.Edit:
            {
                using var body = new GraphicsPath();
                body.AddPolygon(new[]
                {
                    P(0.14f, 0.86f), P(0.22f, 0.60f), P(0.66f, 0.16f),
                    P(0.84f, 0.34f), P(0.40f, 0.76f),
                });
                g.FillPath(fill, body);

                using var tip = new SolidBrush(Darken(color, 0.30f));
                using var tipPath = new GraphicsPath();
                tipPath.AddPolygon(new[] { P(0.14f, 0.86f), P(0.22f, 0.60f), P(0.40f, 0.76f) });
                g.FillPath(tip, tipPath);
                break;
            }

            case IconKind.Delete:
            {
                using var pen = MakePen(0.09f);
                g.DrawLine(pen, P(0.14f, 0.24f), P(0.86f, 0.24f));
                g.DrawLine(pen, P(0.38f, 0.14f), P(0.62f, 0.14f));
                g.DrawLine(pen, P(0.38f, 0.14f), P(0.38f, 0.24f));
                g.DrawLine(pen, P(0.62f, 0.14f), P(0.62f, 0.24f));

                using var body = new GraphicsPath();
                body.AddPolygon(new[]
                {
                    P(0.24f, 0.32f), P(0.76f, 0.32f), P(0.69f, 0.88f), P(0.31f, 0.88f),
                });
                g.FillPath(fill, body);

                using var slot = new Pen(Color.FromArgb(220, 255, 255, 255), W(0.06f));
                g.DrawLine(slot, P(0.40f, 0.42f), P(0.40f, 0.78f));
                g.DrawLine(slot, P(0.50f, 0.42f), P(0.50f, 0.78f));
                g.DrawLine(slot, P(0.60f, 0.42f), P(0.60f, 0.78f));
                break;
            }

            case IconKind.Up:
            case IconKind.Down:
            {
                var flip = kind == IconKind.Down;
                float Y(float value) => flip ? 1f - value : value;
                using var pen = MakePen(0.12f);
                g.DrawLine(pen, P(0.50f, Y(0.86f)), P(0.50f, Y(0.34f)));
                using var head = new GraphicsPath();
                head.AddPolygon(new[]
                {
                    P(0.24f, Y(0.48f)), P(0.50f, Y(0.16f)), P(0.76f, Y(0.48f)),
                });
                g.FillPath(fill, head);
                break;
            }

            case IconKind.Cancel:
            {
                using var pen = MakePen(0.10f);
                g.DrawEllipse(pen, U(0.10f), U(0.10f), U(0.80f), U(0.80f));
                g.DrawLine(pen, P(0.32f, 0.32f), P(0.68f, 0.68f));
                g.DrawLine(pen, P(0.68f, 0.32f), P(0.32f, 0.68f));
                break;
            }

            case IconKind.Check:
            {
                using var pen = MakePen(0.15f);
                g.DrawLine(pen, P(0.16f, 0.52f), P(0.40f, 0.76f));
                g.DrawLine(pen, P(0.40f, 0.76f), P(0.84f, 0.24f));
                break;
            }

            case IconKind.Image:
            {
                using var frame = MakePen(0.08f);
                using var framePath = RoundRect(U(0.09f), U(0.17f), U(0.82f), U(0.66f), U(0.10f));
                g.DrawPath(frame, framePath);

                using var sun = new SolidBrush(color);
                g.FillEllipse(sun, U(0.22f), U(0.28f), U(0.14f), U(0.14f));

                using var mountain = new GraphicsPath();
                mountain.AddPolygon(new[]
                {
                    P(0.14f, 0.76f), P(0.40f, 0.46f), P(0.58f, 0.66f),
                    P(0.68f, 0.56f), P(0.86f, 0.76f),
                });
                using var hill = new SolidBrush(Color.FromArgb(190, color));
                g.FillPath(hill, mountain);
                break;
            }

            case IconKind.SelectAll:
            {
                using var pen = MakePen(0.08f);
                using var box = RoundRect(U(0.10f), U(0.10f), U(0.80f), U(0.80f), U(0.14f));
                g.DrawPath(pen, box);
                using var check = MakePen(0.13f);
                g.DrawLine(check, P(0.28f, 0.52f), P(0.43f, 0.67f));
                g.DrawLine(check, P(0.43f, 0.67f), P(0.73f, 0.33f));
                break;
            }

            case IconKind.ClearAll:
            {
                using var pen = MakePen(0.08f);
                using var box = RoundRect(U(0.10f), U(0.10f), U(0.80f), U(0.80f), U(0.14f));
                g.DrawPath(pen, box);
                using var minus = MakePen(0.12f);
                g.DrawLine(minus, P(0.30f, 0.50f), P(0.70f, 0.50f));
                break;
            }

            case IconKind.Document:
            case IconKind.New:
            {
                DrawDocument(g, s, color, kind == IconKind.New);
                break;
            }

            case IconKind.Gamepad:
            {
                using var body = RoundRect(U(0.05f), U(0.34f), U(0.90f), U(0.44f), U(0.20f));
                g.FillPath(fill, body);

                using var white = new Pen(Color.White, W(0.065f))
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                };
                g.DrawLine(white, P(0.14f, 0.56f), P(0.38f, 0.56f));
                g.DrawLine(white, P(0.26f, 0.44f), P(0.26f, 0.68f));

                using var dot = new SolidBrush(Color.White);
                g.FillEllipse(dot, U(0.62f), U(0.44f), U(0.12f), U(0.12f));
                g.FillEllipse(dot, U(0.76f), U(0.58f), U(0.12f), U(0.12f));
                break;
            }

            case IconKind.Open:
            {
                using var path = new GraphicsPath();
                path.AddPolygon(new[]
                {
                    P(0.08f, 0.30f), P(0.38f, 0.30f), P(0.46f, 0.40f),
                    P(0.92f, 0.40f), P(0.92f, 0.84f), P(0.08f, 0.84f),
                });
                g.FillPath(fill, path);
                using var outline = new Pen(Darken(color, 0.22f), W(0.06f))
                {
                    LineJoin = LineJoin.Round,
                };
                g.DrawPath(outline, path);
                break;
            }
        }
    }

    private static void DrawSave(Graphics g, int s, Color color, bool withArrow)
    {
        PointF P(float x, float y) => new(x * s, y * s);
        float U(float value) => value * s;
        float W(float value) => Math.Max(1f, value * s);

        using var fill = new SolidBrush(color);
        using var body = RoundRect(U(0.12f), U(0.12f), U(0.76f), U(0.76f), U(0.10f));
        g.FillPath(fill, body);

        using var outline = new Pen(Darken(color, 0.24f), W(0.05f))
        {
            LineJoin = LineJoin.Round,
        };
        g.DrawPath(outline, body);

        using var light = new SolidBrush(Color.FromArgb(235, 255, 255, 255));
        g.FillRectangle(light, U(0.30f), U(0.14f), U(0.40f), U(0.22f));
        g.FillRectangle(light, U(0.28f), U(0.54f), U(0.44f), U(0.30f));

        using var slot = new Pen(Color.FromArgb(170, color), W(0.045f));
        g.DrawLine(slot, P(0.36f, 0.62f), P(0.64f, 0.62f));
        g.DrawLine(slot, P(0.36f, 0.72f), P(0.64f, 0.72f));

        if (withArrow)
        {
            using var arrow = new Pen(Color.FromArgb(46, 125, 50), W(0.09f))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };
            g.DrawLine(arrow, P(0.52f, 0.92f), P(0.92f, 0.92f));
            g.DrawLine(arrow, P(0.80f, 0.80f), P(0.92f, 0.92f));
            g.DrawLine(arrow, P(0.80f, 1.00f), P(0.92f, 0.92f));
        }
    }

    private static void DrawDocument(Graphics g, int s, Color color, bool withPlus)
    {
        PointF P(float x, float y) => new(x * s, y * s);
        float W(float value) => Math.Max(1f, value * s);

        using var page = new GraphicsPath();
        page.AddPolygon(new[]
        {
            P(0.26f, 0.10f), P(0.62f, 0.10f), P(0.80f, 0.28f),
            P(0.80f, 0.90f), P(0.26f, 0.90f),
        });

        using var fill = new SolidBrush(Color.FromArgb(38, color));
        g.FillPath(fill, page);

        using var pen = new Pen(color, W(0.08f)) { LineJoin = LineJoin.Round };
        g.DrawPath(pen, page);
        g.DrawLine(pen, P(0.62f, 0.10f), P(0.62f, 0.28f));
        g.DrawLine(pen, P(0.62f, 0.28f), P(0.80f, 0.28f));

        using var line = new Pen(Color.FromArgb(150, color), W(0.055f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        g.DrawLine(line, P(0.36f, 0.44f), P(0.68f, 0.44f));
        g.DrawLine(line, P(0.36f, 0.58f), P(0.68f, 0.58f));
        g.DrawLine(line, P(0.36f, 0.72f), P(0.58f, 0.72f));

        if (withPlus)
        {
            using var plus = new Pen(Color.FromArgb(46, 125, 50), W(0.10f))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };
            g.DrawLine(plus, P(0.70f, 0.66f), P(0.70f, 0.92f));
            g.DrawLine(plus, P(0.57f, 0.79f), P(0.83f, 0.79f));
        }
    }

    private static GraphicsPath RoundRect(float x, float y, float width, float height, float radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + width - d, y, d, d, 270, 90);
        path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
        path.AddArc(x, y + height - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Color Darken(Color color, float amount) =>
        Color.FromArgb(
            color.A,
            (int)(color.R * (1 - amount)),
            (int)(color.G * (1 - amount)),
            (int)(color.B * (1 - amount)));
}
