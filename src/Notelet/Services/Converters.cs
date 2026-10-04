using System.Collections;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Notelet.Models;
using Notelet.Services;

namespace Notelet;

/// <summary>
/// 把便签的 ColorKey 转成卡片底色 / 正文字色 / 次要文字色。
/// ConverterParameter: bg | fg | sub
/// </summary>
public class NoteCardConverter : IValueConverter
{
    static readonly Dictionary<string, SolidColorBrush> _cache = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value as string ?? "auto";
        var mode = parameter as string ?? "bg";
        return mode switch
        {
            "fg" => FgBrushFor(key),
            "sub" => SubBrushFor(key),
            _ => BgBrushFor(key),
        };
    }

    public static SolidColorBrush BgBrushFor(string key)
    {
        var fallback = ThemeService.Current?.Card ?? "#FFFFFF";
        var hex = Palette.BgOf(key, fallback);
        return Cached($"{ThemeService.Version}|{key}|bg", Palette.FromHex(hex));
    }

    public static SolidColorBrush FgBrushFor(string key)
    {
        var hex = Palette.BgOf(key, ThemeService.Current?.Card ?? "#FFFFFF");
        return Cached($"{ThemeService.Version}|{key}|fg",
            Palette.IsDarkBg(hex) ? Color.FromRgb(244, 242, 238) : Color.FromRgb(58, 56, 51));
    }

    public static SolidColorBrush SubBrushFor(string key)
    {
        var hex = Palette.BgOf(key, ThemeService.Current?.Card ?? "#FFFFFF");
        return Cached($"{ThemeService.Version}|{key}|sub",
            Palette.IsDarkBg(hex) ? Color.FromArgb(168, 244, 242, 238) : Color.FromArgb(168, 58, 56, 51));
    }

    static SolidColorBrush Cached(string cacheKey, Color c)
    {
        if (_cache.TryGetValue(cacheKey, out var b)) return b;
        b = Solid(c);
        _cache[cacheKey] = b;
        return b;
    }

    static SolidColorBrush Solid(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>图片文件名 → 卡片缩略图（images 目录内）。</summary>
public class ImgSourceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string name || name.Length == 0) return null!;
        try
        {
            var path = Path.Combine(Paths.ImagesDir, name);
            if (!File.Exists(path)) return null!;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.UriSource = new Uri(path);
            bi.DecodePixelWidth = 300;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch
        {
            return null!;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>集合非空 → Visible，否则 Collapsed。</summary>
public class CollectionToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is ICollection c && c.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
