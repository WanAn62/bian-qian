namespace Notelet.Models;

public class AppSettings
{
    public string ThemeId { get; set; } = "paper";
    public List<ThemeConfig> CustomThemes { get; set; } = new();
    public bool Topmost { get; set; }
    public bool CloseToTray { get; set; } = true;
    public bool Autostart { get; set; }
    public bool FollowSystem { get; set; }
    public double WindowWidth { get; set; } = 1000;
    public double WindowHeight { get; set; } = 680;
}
