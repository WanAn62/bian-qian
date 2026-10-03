using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Notelet.Models;
using Notelet.Services;

namespace Notelet;

/// <summary>
/// 磁贴窗口：把一张便签固定在桌面上随时速览/复制（模仿花笺的磁贴模式）。
/// 随主题与卡片颜色联动，便签内容编辑后实时刷新，便签删除后自动关闭。
/// </summary>
public partial class TileWindow : Window
{
    static int _cascade;

    readonly Note _note;
    readonly DispatcherTimer _rebuild;

    public Note Note => _note;

    public TileWindow(Note note)
    {
        InitializeComponent();
        _note = note;
        Topmost = true;

        SourceInitialized += (_, __) =>
            Dwm.RoundCorners(new WindowInteropHelper(this).EnsureHandle());

        var mw = App.Current.MainWindow;
        int step = _cascade++ % 6;
        if (mw is not null)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = mw.Left + 90 + step * 34;
            Top = mw.Top + 80 + step * 30;
        }

        _rebuild = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _rebuild.Tick += (_, __) => { _rebuild.Stop(); RebuildBody(); };

        note.PropertyChanged += NotePc;

        Closed += (_, __) => note.PropertyChanged -= NotePc;

        TileTitle.Text = note.Title;
        UpdateTopVisual();
        RebuildBody();
    }

    void NotePc(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Note.Text):
                _rebuild.Stop();
                _rebuild.Start();
                break;
            case nameof(Note.Title):
                Dispatcher.BeginInvoke(() => TileTitle.Text = _note.Title);
                break;
            case nameof(Note.ColorKey):
                Dispatcher.BeginInvoke(RebuildBody);
                break;
            case nameof(Note.Deleted):
                if (_note.Deleted) Close();
                break;
        }
    }

    void RebuildBody()
    {
        var fg = NoteCardConverter.FgBrushFor(_note.ColorKey);
        var sub = NoteCardConverter.SubBrushFor(_note.ColorKey);
        var accent = (Brush)Application.Current.Resources["Accent.Brush"];
        var codeBg = (Brush)Application.Current.Resources["Field.Brush"];

        Background = NoteCardConverter.BgBrushFor(_note.ColorKey);
        TileTitle.Foreground = fg;
        TileDate.Text = _note.UpdatedAt.ToString("yyyy/MM/dd HH:mm");

        BodyHost.Children.Clear();
        if (_note.IsMarkdown)
        {
            var box = new RichTextBox
            {
                IsReadOnly = true,
                IsDocumentEnabled = true,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Padding = new Thickness(0),
                MaxHeight = 400,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontSize = 13.5,
            };
            box.Document = Markdown.FlowDoc(_note, fg, sub, accent, codeBg, codeBg, 13.5);
            BodyHost.Children.Add(box);
        }
        else
        {
            BodyHost.Children.Add(new TextBlock
            {
                Text = _note.Body,
                TextWrapping = TextWrapping.Wrap,
                Foreground = fg,
                FontSize = 13.5,
                MaxHeight = 400,
            });
        }
    }

    void UpdateTopVisual()
        => TopBtn.Foreground = Topmost
            ? (Brush)Application.Current.Resources["Accent.Brush"]
            : (Brush)Application.Current.Resources["Text.Brush"];

    void CopyBtn_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(_note.Text); } catch { }
    }

    void TopBtn_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        UpdateTopVisual();
    }

    void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
}
