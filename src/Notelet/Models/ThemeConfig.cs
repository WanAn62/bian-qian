namespace Notelet.Models;

public class ThemeConfig
{
    public string Id { get; set; } = "paper";
    public string Name { get; set; } = "暖纸";

    public string Bg { get; set; } = "#F7F3EC";       // 窗口底色
    public string Card { get; set; } = "#FFFFFF";     // 默认卡片底色
    public string Text { get; set; } = "#3D3A34";     // 主文字
    public string Sub { get; set; } = "#8A857C";      // 次要文字
    public string Accent { get; set; } = "#D97E5F";   // 强调色

    public double Radius { get; set; } = 14;          // 卡片圆角 0-24
    public double FontSize { get; set; } = 14;        // 基础字号
    public double CardWidth { get; set; } = 244;      // 卡片宽度
    public string Font { get; set; } = "Microsoft YaHei UI";

    public string Backdrop { get; set; } = "none";    // none | mica | acrylic
    public bool IsDark { get; set; }

    public ThemeConfig Clone() => (ThemeConfig)MemberwiseClone();
}
