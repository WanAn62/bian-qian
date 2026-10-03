using System.Windows.Media;

namespace Notelet.Models;

public sealed record NoteColor(string Key, string Name, string Bg);

public static class Palette
{
    public static readonly NoteColor[] All =
    {
        new("auto",     "跟随主题", "#FFFFFF"),
        new("cream",    "奶油",    "#FFF3D9"),
        new("peach",    "蜜桃",    "#FFE2D9"),
        new("lemon",    "柠檬",    "#FBF3C2"),
        new("mint",     "薄荷",    "#DCF3E7"),
        new("sky",      "天空",    "#DCEBFF"),
        new("lavender", "薰衣草",  "#E9E3FF"),
        new("rose",     "玫瑰",    "#FFDDE7"),
        new("sand",     "沙杏",    "#F0E4D4"),
        new("ink",      "墨黑",    "#2E2F33"),
    };

    // 用于自动给新便签上色的循环序列（跳过 auto 与墨黑）
    public static readonly string[] Cycle = { "cream", "peach", "lemon", "mint", "sky", "lavender", "rose", "sand" };

    public static string BgOf(string key, string fallback)
    {
        if (string.IsNullOrEmpty(key) || key == "auto") return fallback;
        foreach (var c in All)
            if (c.Key == key) return c.Bg;
        return fallback;
    }

    public static Color FromHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6) hex = "FF" + hex;
        return Color.FromArgb(
            Convert.ToByte(hex.Substring(0, 2), 16),
            Convert.ToByte(hex.Substring(2, 2), 16),
            Convert.ToByte(hex.Substring(4, 2), 16),
            Convert.ToByte(hex.Substring(6, 2), 16));
    }

    public static bool IsDarkBg(string hex)
    {
        var c = FromHex(hex);
        return (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0 < 0.55;
    }
}
