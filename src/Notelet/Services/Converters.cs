using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
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
        var fallback = ThemeService.Current?.Card ?? "#FFFFFF";
        var hex = Palette.BgOf(key, fallback);

        var cacheKey = $"{ThemeService.Version}|{key}|{mode}";
        if (_cache.TryGetValue(cacheKey, out var b)) return b;

        b = mode switch
        {
            "fg" => Solid(Palette.IsDarkBg(hex) ? Color.FromRgb(244, 242, 238) : Color.FromRgb(58, 56, 51)),
            "sub" => Solid(Palette.IsDarkBg(hex) ? Color.FromArgb(168, 244, 242, 238) : Color.FromArgb(168, 58, 56, 51)),
            _ => Solid(Palette.FromHex(hex)),
        };
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
