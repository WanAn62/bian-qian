using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Notelet.Models;
using Notelet.Services;

namespace Notelet;

public enum DialogIcon
{
    Info,
    Warn,
}

/// <summary>
/// 主题化对话框：替代系统 MessageBox，跟随当前主题配色。
/// Dialog.Info / Dialog.Warn / Dialog.Confirm
/// </summary>
public static class Dialog
{
    public static void Info(Window? owner, string title, string message)
        => ShowDialog(owner, title, message, DialogIcon.Info, null);

    public static void Warn(Window? owner, string title, string message)
        => ShowDialog(owner, title, message, DialogIcon.Warn, null);

    public static bool Confirm(Window? owner, string title, string message, string confirmLabel = "确定", bool danger = false)
        => ShowDialog(owner, title, message, danger ? DialogIcon.Warn : DialogIcon.Info, confirmLabel);

    static bool ShowDialog(Window? owner, string title, string message, DialogIcon kind, string? confirmLabel)
    {
        bool confirmed = false;
        var w = Build(owner, title, message, kind, confirmLabel, () => confirmed = true);
        w.ShowDialog();
        return confirmed;
    }

    static Window Build(Window? owner, string title, string message, DialogIcon kind, string? confirmLabel, Action onConfirm)
    {
        object Res(string k) => Application.Current.Resources[k];

        var w = new Window
        {
            Width = 400,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = owner?.IsVisible == true
                ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Background = (Brush)Res("Card.Brush"),
            Foreground = (Brush)Res("Text.Brush"),
            FontFamily = (FontFamily)Res("BaseFont.FontFamily"),
            FontSize = 13,
            BorderBrush = (Brush)Res("Border.Brush"),
            BorderThickness = new Thickness(1),
        };
        if (owner?.IsVisible == true) w.Owner = owner;

        w.SourceInitialized += (_, __) =>
            Dwm.RoundCorners(new WindowInteropHelper(w).EnsureHandle());

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        titleRow.Children.Add(new TextBlock
        {
            Text = kind == DialogIcon.Warn ? "\uE7BA" : "\uE946",
            FontFamily = (FontFamily)Res("IconFont.FontFamily"),
            FontSize = 18,
            Foreground = kind == DialogIcon.Warn
                ? Brush("#E6A23C")
                : (Brush)Res("Accent.Brush"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        titleRow.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14.5,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
        });

        var msgTb = new TextBlock
        {
            Text = message,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 21,
            Margin = new Thickness(0, 12, 0, 0),
        };

        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0),
        };

        if (confirmLabel is not null)
        {
            btnRow.Children.Add(MakeButton(w, Res, "否", accent: false, () => { }));
            btnRow.Children.Add(MakeButton(w, Res, confirmLabel, accent: true, onConfirm));
        }
        else
        {
            btnRow.Children.Add(MakeButton(w, Res, "知道了", accent: true, () => { }));
        }

        var root = new Border
        {
            Padding = new Thickness(22, 18, 22, 18),
            Child = new StackPanel { Children = { titleRow, msgTb, btnRow } },
        };
        w.Content = root;

        w.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                w.Close();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                w.Close();
                onConfirm();
                e.Handled = true;
            }
        };
        return w;
    }

    static Button MakeButton(Window w, Func<string, object> res, string label, bool accent, Action click)
    {
        var b = new Button
        {
            Content = label,
            MinWidth = 92,
            Height = 32,
            FontSize = 13,
            Cursor = Cursors.Hand,
            Style = (Style)res(accent ? "DlgBtnAccent" : "DlgBtn"),
        };
        b.Click += (_, __) =>
        {
            w.Close();
            click();
        };
        return b;
    }

    static SolidColorBrush Brush(string hex)
    {
        var b = new SolidColorBrush(Palette.FromHex(hex));
        b.Freeze();
        return b;
    }
}
