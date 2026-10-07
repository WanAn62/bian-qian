using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Notelet.Models;
using Notelet.Services;

namespace Notelet;

/// <summary>
/// 提醒设置小窗：快捷选择或自定义时间，结果直接写回便签的 RemindAt。
/// </summary>
public partial class ReminderWindow : Window
{
    readonly Note _note;
    readonly Action _onChanged;

    public ReminderWindow(Note note, Action onChanged)
    {
        InitializeComponent();
        _note = note;
        _onChanged = onChanged;
        if (App.Current.MainWindow is { IsVisible: true } mw)
            Owner = mw;

        SourceInitialized += (_, __) =>
            Dwm.RoundCorners(new WindowInteropHelper(this).EnsureHandle());

        RemindDate.SelectedDate = _note.RemindAt?.Date ?? DateTime.Today.AddDays(1);
        for (int h = 0; h < 24; h++) HourBox.Items.Add($"{h:00}");
        for (int m = 0; m < 60; m += 5) MinuteBox.Items.Add($"{m:00}");
        var t = _note.RemindAt ?? DateTime.Now.AddHours(1);
        HourBox.SelectedItem = $"{t.Hour:00}";
        MinuteBox.SelectedItem = $"{t.Minute / 5 * 5:00}";

        ClearBtn.Visibility = _note.RemindAt is null ? Visibility.Collapsed : Visibility.Visible;
    }

    DateTime? PickedTime()
    {
        if (RemindDate.SelectedDate is not DateTime d) return null;
        if (HourBox.SelectedItem is not string hs || MinuteBox.SelectedItem is not string ms) return null;
        return new DateTime(d.Year, d.Month, d.Day, int.Parse(hs), int.Parse(ms), 0);
    }

    void Apply(DateTime time)
    {
        _note.RemindAt = time;
        _note.Reminded = false;
        _onChanged();
        Close();
    }

    void Quick_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as FrameworkElement)?.Tag as string;
        var now = DateTime.Now;
        switch (tag)
        {
            case "30m": Apply(now.AddMinutes(30)); break;
            case "1h": Apply(now.AddHours(1)); break;
            case "3h": Apply(now.AddHours(3)); break;
            case "tomorrow": Apply(DateTime.Now.Date.AddDays(1).AddHours(9)); break;
        }
    }

    void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (PickedTime() is not DateTime t)
        {
            Dialog.Warn(this, "设置提醒", "请先选择日期。");
            return;
        }
        Apply(t);
    }

    void Clear_Click(object sender, RoutedEventArgs e)
    {
        _note.RemindAt = null;
        _note.Reminded = false;
        _onChanged();
        Close();
    }

    void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
