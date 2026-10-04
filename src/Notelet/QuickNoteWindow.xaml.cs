using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Notelet.Models;
using Notelet.Services;

namespace Notelet;

/// <summary>
/// 快速便签小窗：随手记一条，关闭即保存进便签墙；可多开（对标花笺「快捷便签」）。
/// 空内容关闭时自动丢弃。
/// </summary>
public partial class QuickNoteWindow : Window
{
    static int _cascade;

    readonly Note _note;
    readonly Action _onChanged;
    readonly Action<Note> _onDiscard;
    readonly Action _onSaveAll;

    public QuickNoteWindow(Note note, Action onChanged, Action<Note> onDiscard, Action onSaveAll)
    {
        InitializeComponent();
        _note = note;
        _onChanged = onChanged;
        _onDiscard = onDiscard;
        _onSaveAll = onSaveAll;

        DataContext = note;
        BodyBox.DataContext = note;
        ColorDot.Fill = NoteCardConverter.BgBrushFor(note.ColorKey);
        BodyBox.Foreground = NoteCardConverter.FgBrushFor(note.ColorKey);
        BodyBox.CaretIndex = 0;

        SourceInitialized += (_, __) =>
            Dwm.RoundCorners(new WindowInteropHelper(this).EnsureHandle());

        var mw = App.Current.MainWindow;
        int step = _cascade++ % 8;
        if (mw is not null)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = mw.Left + 140 + step * 30;
            Top = mw.Top + 110 + step * 26;
        }

        note.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Note.Text))
                Dispatcher.BeginInvoke(() =>
                    CountText.Text = $"{_note.Text.Length} 字");
        };
        CountText.Text = $"{_note.Text.Length} 字";

        Closed += (_, __) => Commit();
    }

    void Commit()
    {
        if (string.IsNullOrWhiteSpace(_note.Text) && _note.ImageFiles.Count == 0)
            _onDiscard(_note);
        else
        {
            _note.UpdatedAt = DateTime.Now;
            _onChanged();
        }
        _onSaveAll();
    }

    public void FocusBox()
    {
        Activate();
        BodyBox.Focus();
        BodyBox.CaretIndex = BodyBox.Text.Length;
    }

    void BodyBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape || (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)))
        {
            Close();
            e.Handled = true;
        }
    }

    void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
}
