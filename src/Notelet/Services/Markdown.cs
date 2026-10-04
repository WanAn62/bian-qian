using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Notelet.Models;

namespace Notelet.Services;

/// <summary>
/// 轻量 Markdown 渲染：GFM 子集（标题/列表/任务清单/引用/代码块/分隔线/
/// 粗体/斜体/删除线/行内代码/链接），任务框可点击直接回写原文。
/// </summary>
public static partial class Markdown
{
    static readonly FontFamily Mono = new("Cascadia Mono, Consolas, Microsoft YaHei UI");

    [GeneratedRegex(@"`([^`]+)`|\*\*(.+?)\*\*|~~(.+?)~~|\*([^*\n]+?)\*|\[([^\]]+)\]\(([^)\s]+)\)")]
    private static partial Regex InlineRx();

    // ─────────────── 行内解析（卡片正文等轻量场景） ───────────────

    public static List<Inline> Inlines(string text, Brush fg, Brush sub, Brush accent, Brush codeBg, double fontSize)
        => ParseInlines(InlineView(text), fg, sub, accent, codeBg, fontSize, false, false);

    /// <summary>把块级语法降级为小卡片可用的行内表达（标题去井号、任务转勾选框、列表转圆点等）。</summary>
    internal static string InlineView(string text)
    {
        var sb = new StringBuilder();
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var t = raw.Trim();
            if (t.Length == 0) { sb.Append('\n'); continue; }
            if (t.StartsWith("```") || t is "---" or "***" or "___") continue;
            if (t.StartsWith("![") && t.Contains("](") && t.EndsWith(")")) { sb.Append("[图片]\n"); continue; }
            if (t.StartsWith("### ")) t = t[4..];
            else if (t.StartsWith("## ")) t = t[3..];
            else if (t.StartsWith("# ")) t = t[2..];
            else if (t.StartsWith("> ")) t = t[2..];
            else if (t.StartsWith("- [ ] ")) t = "☐ " + t[6..];
            else if (t.StartsWith("- [x] ") || t.StartsWith("- [X] ")) t = "☑ " + t[6..];
            else if (t.StartsWith("- ") || t.StartsWith("* ")) t = "•  " + t[2..];
            sb.Append(t).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>把解析结果并入现有 InlineCollection。</summary>
    internal static void AddTo(InlineCollection target, List<Inline> items)
    {
        foreach (var i in items) target.Add(i);
    }

    static List<Inline> ParseInlines(string text, Brush fg, Brush sub, Brush accent, Brush codeBg, double fs, bool bold, bool italic)
    {
        var list = new List<Inline>();
        int pos = 0;
        for (var m = InlineRx().Match(text, pos); m.Success; m = InlineRx().Match(text, pos))
        {
            if (m.Index > pos) list.Add(Plain(text[pos..m.Index], fg, bold, italic));
            var v = m.Value;
            if (v[0] == '`')
            {
                var sp = new Span { FontFamily = Mono, Background = codeBg, Foreground = accent };
                sp.Inlines.Add(new Run(m.Groups[1].Value) { FontSize = Math.Max(fs - 1.5, 10) });
                list.Add(sp);
            }
            else if (v.StartsWith("**"))
            {
                var sp = new Span { FontWeight = FontWeights.Bold };
                AddTo(sp.Inlines, ParseInlines(m.Groups[2].Value, fg, sub, accent, codeBg, fs, true, italic));
                list.Add(sp);
            }
            else if (v.StartsWith("~~"))
            {
                var sp = new Span { TextDecorations = TextDecorations.Strikethrough, Foreground = sub };
                AddTo(sp.Inlines, ParseInlines(m.Groups[3].Value, sub, sub, accent, codeBg, fs, bold, italic));
                list.Add(sp);
            }
            else if (v[0] == '*')
            {
                var sp = new Span { FontStyle = FontStyles.Italic };
                AddTo(sp.Inlines, ParseInlines(m.Groups[4].Value, fg, sub, accent, codeBg, fs, bold, true));
                list.Add(sp);
            }
            else
            {
                var link = new Hyperlink
                {
                    Foreground = accent,
                    Tag = m.Groups[6].Value,
                    ToolTip = m.Groups[6].Value,
                };
                link.Inlines.Add(new Run(m.Groups[5].Value) { TextDecorations = TextDecorations.Underline });
                link.Click += Link_Click;
                list.Add(link);
            }
            pos = m.Index + m.Length;
        }
        if (pos < text.Length) list.Add(Plain(text[pos..], fg, bold, italic));
        return list;
    }

    static Run Plain(string s, Brush fg, bool bold, bool italic) => new(s)
    {
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        FontStyle = italic ? FontStyles.Italic : FontStyles.Normal,
        Foreground = fg,
    };

    static void Link_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Hyperlink { Tag: string url })
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { }
        }
    }

    // ─────────────── 块级解析（预览 / 磁贴） ───────────────

    public static FlowDocument FlowDoc(Note note, Brush fg, Brush sub, Brush accent, Brush codeBg, Brush quoteBg, double fontSize)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            Foreground = fg,
            FontSize = fontSize,
        };
        var src = note.Text;
        int idx = 0;
        bool inCode = false;
        var codeBuf = new List<string>();

        void FlushCode()
        {
            if (codeBuf.Count == 0) return;
            var p = new Paragraph
            {
                Background = codeBg,
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 3, 0, 6),
                FontFamily = Mono,
            };
            for (int i = 0; i < codeBuf.Count; i++)
            {
                if (i > 0) p.Inlines.Add(new LineBreak());
                p.Inlines.Add(new Run(codeBuf[i]) { Foreground = accent, FontSize = Math.Max(fontSize - 1.5, 10) });
            }
            doc.Blocks.Add(p);
            codeBuf.Clear();
        }

        while (true)
        {
            int nl = src.IndexOf('\n', idx);
            string line = (nl < 0 ? src[idx..] : src.Substring(idx, nl - idx)).TrimEnd('\r');
            int lineStart = idx;
            if (nl < 0) idx = src.Length; else idx = nl + 1;

            var t = line.TrimStart();
            if (t.StartsWith("```"))
            {
                if (inCode) { FlushCode(); inCode = false; }
                else inCode = true;
                if (nl < 0) break;
                continue;
            }
            if (inCode) { codeBuf.Add(line); if (nl < 0) { FlushCode(); inCode = false; break; } continue; }

            if (t.Length == 0) { if (nl < 0) break; continue; }

            if (t.StartsWith("### ")) doc.Blocks.Add(Heading(t[4..], fg, accent, codeBg, fontSize * 1.12, fontSize));
            else if (t.StartsWith("## ")) doc.Blocks.Add(Heading(t[3..], fg, accent, codeBg, fontSize * 1.28, fontSize));
            else if (t.StartsWith("# ")) doc.Blocks.Add(Heading(t[2..], fg, accent, codeBg, fontSize * 1.5, fontSize));
            else if (t is "---" or "***" or "___")
            {
                doc.Blocks.Add(new BlockUIContainer(new Rectangle
                {
                    Height = 1,
                    Fill = sub,
                    Margin = new Thickness(0, 4, 0, 4),
                }));
            }
            else if (t.StartsWith("![") && t.Contains("](") && t.EndsWith(")"))
            {
                // 图片行：![说明](路径) —— 仅支持本地路径 / images 目录内文件名
                var m2 = Regex.Match(t, @"^!\[([^\]]*)\]\((.+)\)$");
                var path = ResolveImagePath(m2.Success ? m2.Groups[2].Value : "");
                if (path is not null)
                {
                    doc.Blocks.Add(new BlockUIContainer(ImageBlock(path, 240)));
                }
                else
                {
                    var p = new Paragraph { Margin = new Thickness(0, 1, 0, 1) };
                    p.Inlines.Add(new Run("🖼 [图片]") { Foreground = sub });
                    doc.Blocks.Add(p);
                }
                if (nl < 0) break;
                continue;
            }
            else if (t.StartsWith("> "))
            {
                var p = new Paragraph
                {
                    Margin = new Thickness(0, 3, 0, 3),
                    Padding = new Thickness(9, 3, 4, 3),
                    BorderBrush = accent,
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    Background = quoteBg,
                };
                AddTo(p.Inlines, ParseInlines(t[2..], fg, sub, accent, codeBg, fontSize, false, false));
                doc.Blocks.Add(p);
            }
            else
            {
                // 任务清单：- [ ] / - [x]
                var task = Regex.Match(t, @"^([-*])\s+\[([ xX])\]\s+(.*)$");
                if (task.Success)
                {
                    int stateOff = lineStart + line.IndexOf('[') + 1;
                    bool done = task.Groups[2].Value is "x" or "X";

                    var p = new Paragraph { Margin = new Thickness(0, 1, 0, 1) };
                    var cb = new CheckBox
                    {
                        IsChecked = done,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 6, 1),
                        Cursor = Cursors.Hand,
                    };
                    var target = note;
                    var off = stateOff;
                    cb.Click += (_, __) => FlipTask(target, off);
                    p.Inlines.Add(new InlineUIContainer(cb) { BaselineAlignment = BaselineAlignment.Center });
                    if (done)
                    {
                        var sp = new Span { Foreground = sub, TextDecorations = TextDecorations.Strikethrough };
                        AddTo(sp.Inlines, ParseInlines(task.Groups[3].Value, sub, sub, accent, codeBg, fontSize, false, false));
                        p.Inlines.Add(sp);
                    }
                    else
                    {
                        AddTo(p.Inlines, ParseInlines(task.Groups[3].Value, fg, sub, accent, codeBg, fontSize, false, false));
                    }
                    doc.Blocks.Add(p);
                    if (nl < 0) break;
                    continue;
                }

                // 无序 / 有序列表
                var ul = Regex.Match(t, @"^([-*])\s+(.*)$");
                var ol = Regex.Match(t, @"^(\d+)[.、]\s+(.*)$");
                if (ul.Success || ol.Success)
                {
                    int indent = line.Length - line.TrimStart().Length;
                    var p = new Paragraph
                    {
                        Margin = new Thickness(indent * fontSize * 0.55 + 6, 1, 0, 1),
                        TextIndent = -fontSize * 0.9,
                    };
                    var marker = new Run(ul.Success ? "•  " : $"{ol.Groups[1].Value}.  ") { Foreground = accent };
                    p.Inlines.Add(marker);
                    AddTo(p.Inlines, ParseInlines(ul.Success ? ul.Groups[2].Value : ol.Groups[2].Value, fg, sub, accent, codeBg, fontSize, false, false));
                    doc.Blocks.Add(p);
                    if (nl < 0) break;
                    continue;
                }

                var para = new Paragraph { Margin = new Thickness(0, 0, 0, fontSize * 0.3) };
                AddTo(para.Inlines, ParseInlines(t, fg, sub, accent, codeBg, fontSize, false, false));
                doc.Blocks.Add(para);
            }

            if (nl < 0) break;
        }
        FlushCode();
        return doc;
    }

    static Paragraph Heading(string content, Brush fg, Brush accent, Brush codeBg, double size, double baseFs)
    {
        var p = new Paragraph
        {
            Margin = new Thickness(0, baseFs * 0.7, 0, baseFs * 0.35),
            FontSize = size,
            FontWeight = FontWeights.Bold,
            Foreground = fg,
        };
        AddTo(p.Inlines, ParseInlines(content, fg, fg, accent, codeBg, size, true, false));
        return p;
    }

    /// <summary>解析图片引用：支持 images 目录内文件名与本地绝对路径，联网地址一律不支持。</summary>
    internal static string? ResolveImagePath(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return null;
        reference = reference.Trim();
        if (reference.StartsWith("http:", StringComparison.OrdinalIgnoreCase) ||
            reference.StartsWith("https:", StringComparison.OrdinalIgnoreCase)) return null;
        var path = Path.IsPathRooted(reference)
            ? reference
            : System.IO.Path.Combine(Paths.ImagesDir, reference);
        return File.Exists(path) ? path : null;
    }

    internal static Image ImageBlock(string path, double maxHeight)
    {
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.UriSource = new Uri(path);
        bi.DecodePixelWidth = 520;
        bi.EndInit();
        bi.Freeze();
        return new Image
        {
            Source = bi,
            MaxHeight = maxHeight,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 2, 0, 2),
            Cursor = Cursors.Hand,
        };
    }

    /// <summary>勾选/取消任务清单项：把原文对应偏移处的 ' ' 与 'x' 互换。</summary>
    public static void FlipTask(Note n, int off)
    {
        var t = n.Text;
        if (off < 0 || off >= t.Length) return;
        char c = t[off];
        if (c != ' ' && c != 'x' && c != 'X') return;
        n.Text = t[..off] + (c == ' ' ? 'x' : ' ') + t[(off + 1)..];
    }
}

/// <summary>
/// 附加行为：让卡片正文的 TextBlock 在 note.IsMarkdown 时用富文本行内格式显示。
/// </summary>
public static class MarkdownBody
{
    static readonly Dictionary<Note, TextBlock> Map = new();

    public static readonly DependencyProperty EnableProperty = DependencyProperty.RegisterAttached(
        "Enable", typeof(bool), typeof(MarkdownBody), new PropertyMetadata(false, OnEnableChanged));

    public static void SetEnable(DependencyObject d, bool v) => d.SetValue(EnableProperty, v);
    public static bool GetEnable(DependencyObject d) => (bool)d.GetValue(EnableProperty);

    static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb) return;
        if ((bool)e.NewValue)
        {
            tb.DataContextChanged += DcChanged;
            tb.Unloaded += TbUnloaded;
            Attach(tb);
        }
    }

    static void DcChanged(object sender, DependencyPropertyChangedEventArgs e) => Attach((TextBlock)sender);

    static void TbUnloaded(object sender, RoutedEventArgs e)
    {
        var tb = (TextBlock)sender;
        if (tb.DataContext is Note n) Map.Remove(n);
    }

    static void Attach(TextBlock tb)
    {
        if (tb.DataContext is not Note n) return;
        Map[n] = tb;
        n.PropertyChanged -= NoteChanged;
        n.PropertyChanged += NoteChanged;
        Update(tb, n);
    }

    static void NoteChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not Note n) return;
        if (e.PropertyName is nameof(Note.Text) or nameof(Note.IsMarkdown) or nameof(Note.ColorKey))
        {
            if (Map.TryGetValue(n, out var tb)) tb.Dispatcher.BeginInvoke(() => Update(tb, n));
        }
    }

    static void Update(TextBlock tb, Note n)
    {
        var fg = NoteCardConverter.FgBrushFor(n.ColorKey);
        var sub = NoteCardConverter.SubBrushFor(n.ColorKey);
        var accent = (Brush)Application.Current.Resources["Accent.Brush"];
        var codeBg = (Brush)Application.Current.Resources["Field.Brush"];
        if (n.IsMarkdown)
        {
            tb.Inlines.Clear();
            Markdown.AddTo(tb.Inlines, Markdown.Inlines(n.Body, fg, sub, accent, codeBg, tb.FontSize));
        }
        else
        {
            tb.Inlines.Clear();
            tb.Text = n.Body;
        }
    }
}
