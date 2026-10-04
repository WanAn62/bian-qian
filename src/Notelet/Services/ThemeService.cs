using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Notelet.Models;

namespace Notelet.Services;

public static class ThemeService
{
    public static ThemeConfig? Current { get; private set; }
    public static int Version { get; private set; }

    public static List<ThemeConfig> Presets { get; } = new()
    {
        new() { Id = "paper",   Name = "暖纸",     Bg = "#F7F3EC", Card = "#FFFFFF", Text = "#3D3A34", Sub = "#8A857C", Accent = "#D97E5F", Backdrop = "none" },
        new() { Id = "ink",     Name = "墨夜",     Bg = "#1D1E23", Card = "#26272E", Text = "#E9E7E2", Sub = "#9A968E", Accent = "#E08A6D", IsDark = true,  Backdrop = "none" },
        new() { Id = "mint",    Name = "薄荷",     Bg = "#EDF5F1", Card = "#FFFFFF", Text = "#2F4440", Sub = "#7E938D", Accent = "#3BA98B", Backdrop = "none" },
        new() { Id = "peach",   Name = "蜜桃",     Bg = "#FBF1EE", Card = "#FFFFFF", Text = "#4A3A36", Sub = "#9C8680", Accent = "#E4795F", Backdrop = "none" },
        new() { Id = "mica",    Name = "云母",     Bg = "#F5F4F2", Card = "#FFFFFF", Text = "#3B3A38", Sub = "#8B8884", Accent = "#5B9BD5", Backdrop = "mica" },
        new() { Id = "acrylic", Name = "亚克力夜", Bg = "#20222A", Card = "#2B2D36", Text = "#E9E7E2", Sub = "#96949C", Accent = "#8FB7F0", IsDark = true,  Backdrop = "acrylic" },
    };

    /// <summary>把主题写入全局动态资源（可随时调用，窗口未创建也安全）。</summary>
    public static void ApplyBrushes(ThemeConfig t)
    {
        Current = t;
        Version++;

        var r = Application.Current.Resources;
        r["Bg.Brush"] = Brush(t.Bg);
        r["Card.Brush"] = Brush(t.Card);
        r["Text.Brush"] = Brush(t.Text);
        r["Sub.Brush"] = Brush(t.Sub);
        r["Accent.Brush"] = Brush(t.Accent);
        r["Field.Brush"] = BrushAlpha(t.IsDark ? "#FFFFFF" : "#FFFFFF", (byte)(t.IsDark ? 0x16 : 0x66));
        r["Border.Brush"] = BrushAlpha(t.IsDark ? "#FFFFFF" : "#000000", (byte)(t.IsDark ? 0x22 : 0x1A));
        r["ThumbBrush"] = t.IsDark ? BrushAlpha("#FFFFFF", 0x33) : BrushAlpha("#000000", 0x2E);
        r["Radius.CornerRadius"] = new CornerRadius(t.Radius);
        r["BaseFontSize.Double"] = t.FontSize;
        r["TitleSize.Double"] = t.FontSize + 1.5;
        r["BodyMax.Double"] = t.FontSize * 1.5 * 6;
        r["CardWidth.Double"] = t.CardWidth;
        r["BaseFont.FontFamily"] = new FontFamily(t.Font);
    }

    /// <summary>应用 DWM 背景（云母/亚克力），失败时回退纯色。须在窗口句柄就绪后调用。</summary>
    public static void ApplyBackdrop(Window w)
    {
        var t = Current;
        if (t is null) return;

        var hwnd = new WindowInteropHelper(w).EnsureHandle();
        Dwm.RoundCorners(hwnd);

        var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(w);
        if (chrome is null) return;

        bool want = t.Backdrop is "mica" or "acrylic";
        bool ok = want && Dwm.TryBackdrop(hwnd, t.Backdrop == "mica" ? 2 : 3, t.IsDark);

        if (ok)
        {
            w.Background = Brushes.Transparent;
            chrome.GlassFrameThickness = new Thickness(-1);
        }
        else
        {
            Dwm.DisableBackdrop(hwnd);
            chrome.GlassFrameThickness = new Thickness(0);
            w.Background = Brush(t.Bg);
        }
    }

    public static Brush WindowTint()
    {
        var t = Current!;
        var c = Palette.FromHex(t.Bg);
        c.A = (byte)(t.IsDark ? 150 : 125);
        return BrushFrom(c);
    }

    static SolidColorBrush Brush(string hex) => BrushFrom(Palette.FromHex(hex));

    static SolidColorBrush BrushAlpha(string hex, byte a)
    {
        var c = Palette.FromHex(hex);
        c.A = a;
        return BrushFrom(c);
    }

    static SolidColorBrush BrushFrom(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
