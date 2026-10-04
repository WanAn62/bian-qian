using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Navigation;
using Notelet.Models;
using Notelet.Services;

namespace Notelet;

/// <summary>
/// 设置窗口：通用开关 / 外观 / 快捷键一览 / 存储与备份 / 关于。所有改动即时生效。
/// </summary>
public partial class SettingsWindow : Window
{
    readonly MainWindow _mw;

    public SettingsWindow(MainWindow mw)
    {
        InitializeComponent();
        _mw = mw;
        if (mw.IsVisible) Owner = mw;

        SourceInitialized += (_, __) =>
            Dwm.RoundCorners(new WindowInteropHelper(this).EnsureHandle());

        LoadStates();
    }

    void LoadStates()
    {
        var s = _mw.Settings;
        AutostartSwitch.IsChecked = s.Autostart;
        TraySwitch.IsChecked = s.CloseToTray;
        FollowSwitch.IsChecked = s.FollowSystem;
        TopmostSwitch.IsChecked = s.Topmost;
        ThemeNameText.Text = $"当前主题：{_mw.CurrentThemeName}";
        DataPathText.Text = Paths.Root;
        DataPathText.ToolTip = Paths.Root;
        BackupCountText.Text = $"自动备份 · {_mw.BackupCount()} 份";
        VersionText.Text = $"简签 Notelet v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.2.0"}";

        BuildHotkeyList();
    }

    void BuildHotkeyList()
    {
        HotkeyList.Children.Clear();
        AddHotkey("呼出 / 隐藏主窗口", "Ctrl + Alt + N");
        AddHotkey("快速便签（可多开）", "Ctrl + Alt + Q");
        AddHotkey("新建便签", "Ctrl + N");
        AddHotkey("搜索", "Ctrl + K");
        AddHotkey("粘贴新建 / 粘贴截图", "Ctrl + V");
        AddHotkey("编辑 / 完成编辑", "双击 · Ctrl + Enter");
        AddHotkey("唤出主题美化", "主窗口调色板按钮");
    }

    void AddHotkey(string label, string keys)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        g.Children.Add(new TextBlock { Text = label, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
        var pill = new Border
        {
            Background = (System.Windows.Media.Brush)FindResource("Card.Brush"),
            BorderBrush = (System.Windows.Media.Brush)FindResource("Border.Brush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(9, 3, 9, 4),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = keys,
                FontSize = 11.5,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            },
        };
        g.Children.Add(pill);
        HotkeyList.Children.Add(g);
    }

    void Autostart_Click(object sender, RoutedEventArgs e)
    {
        _mw.ApplyAutostart(AutostartSwitch.IsChecked == true);
    }

    void Tray_Click(object sender, RoutedEventArgs e)
    {
        _mw.SetCloseToTray(TraySwitch.IsChecked == true);
    }

    void Follow_Click(object sender, RoutedEventArgs e)
    {
        _mw.SetFollowSystem(FollowSwitch.IsChecked == true);
    }

    void Topmost_Click(object sender, RoutedEventArgs e)
    {
        _mw.SetTopmost(TopmostSwitch.IsChecked == true);
    }

    void OpenTheme_Click(object sender, RoutedEventArgs e)
    {
        Close();
        _mw.ShowMain();
        _mw.OpenThemePanel();
    }

    void OpenData_Click(object sender, RoutedEventArgs e) => _mw.OpenDataFolder();

    void OpenBackups_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Paths.BackupDir);
            Process.Start(new ProcessStartInfo { FileName = Paths.BackupDir, UseShellExecute = true });
        }
        catch { }
    }

    void CleanBackups_Click(object sender, RoutedEventArgs e)
    {
        int removed = _mw.CleanBackups();
        BackupCountText.Text = $"自动备份 · {_mw.BackupCount()} 份";
        CleanBackupsBtn.Content = removed > 0 ? $"已清理 {removed} 份" : "没有可清理的";
    }

    void GitHub_Link(object sender, RequestNavigateEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(e.Uri.ToString()) { UseShellExecute = true }); }
        catch { }
    }

    void Close_Click(object sender, RoutedEventArgs e) => Close();
}
