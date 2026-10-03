using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Notelet.Models;
using Notelet.Services;
using WinForms = System.Windows.Forms;

namespace Notelet;

public partial class MainWindow : Window
{
    readonly NotesRepo _repo;
    readonly AppSettings _settings;
    ThemeConfig _theme = new();

    string _search = "";
    int _colorIdx;
    bool _buildingPanel;

    Point? _downPos;
    Note? _dragCandidate;

    public ListCollectionView View { get; }
    public static readonly DependencyProperty TrashModeProperty = DependencyProperty.Register(
        nameof(TrashMode), typeof(bool), typeof(MainWindow),
        new PropertyMetadata(false, (d, _) => ((MainWindow)d).RefreshView()));
    public bool TrashMode
    {
        get => (bool)GetValue(TrashModeProperty);
        set => SetValue(TrashModeProperty, value);
    }

    readonly DispatcherTimer _settingsSaveTimer;

    public MainWindow()
    {
        InitializeComponent();

        TempExport.Cleanup();

        _settings = SettingsRepo.Load() ?? new AppSettings();
        _repo = NotesRepo.LoadOrCreate();
        _repo.Saved += () => Dispatcher.BeginInvoke(UpdateStatus);

        View = (ListCollectionView)CollectionViewSource.GetDefaultView(_repo.Items);
        View.Filter = FilterNote;
        View.CustomSort = NoteSort;
        CardsList.ItemsSource = View;

        ApplyThemeFromSettings();

        Width = _settings.WindowWidth;
        Height = _settings.WindowHeight;
        Topmost = _settings.Topmost;
        PinWinBtn.IsChecked = _settings.Topmost;

        SourceInitialized += (_, __) => ThemeService.ApplyBackdrop(this);

        _settingsSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _settingsSaveTimer.Tick += (_, __) => { _settingsSaveTimer.Stop(); SettingsRepo.Save(_settings); };

        RefreshView();
        SettingsRepo.Save(_settings);
    }

    // ───────────────────────── 排序 / 过滤 / 状态 ─────────────────────────

    static readonly IComparer NoteSort = Comparer<object>.Create((a, b) =>
    {
        var x = (Note)a!;
        var y = (Note)b!;
        if (x.Pinned != y.Pinned) return x.Pinned ? -1 : 1;
        return DateTime.Compare(y.UpdatedAt, x.UpdatedAt);
    });

    bool FilterNote(object o)
    {
        var n = (Note)o;
        if (TrashMode) return n.Deleted;
        if (n.Deleted) return false;
        if (_search.Length == 0) return true;
        return n.Text.Contains(_search, StringComparison.OrdinalIgnoreCase);
    }

    void RefreshView()
    {
        View.Refresh();
        int active = _repo.Items.Count(n => !n.Deleted);
        int trash = _repo.Items.Count - active;
        EmptyState.Visibility = !TrashMode && active == 0 ? Visibility.Visible : Visibility.Collapsed;
        TrashBanner.Visibility = TrashMode ? Visibility.Visible : Visibility.Collapsed;
        TrashCountText.Text = $"回收站 {trash} 条";
        UpdateStatus();
    }

    void UpdateStatus()
    {
        int active = _repo.Items.Count(n => !n.Deleted);
        int pinned = _repo.Items.Count(n => !n.Deleted && n.Pinned);
        StatusLeft.Text = TrashMode
            ? $"回收站 · 卡片上可直接恢复或彻底删除"
            : $"{active} 条便签 · 置顶 {pinned}";
        StatusRight.Text = _repo.LastSaved is DateTime d ? $"已自动保存 {d:HH:mm:ss}" : "就绪";
    }

    // ───────────────────────── 便签增删改 ─────────────────────────

    string NextColor() => Palette.Cycle[_colorIdx++ % Palette.Cycle.Length];

    void NewNote(string text = "", string? colorKey = null)
    {
        var n = new Note { Text = text, ColorKey = colorKey ?? NextColor() };
        n.IsEditing = true;
        _repo.Items.Insert(0, n);
        _repo.MarkDirty();
        RefreshView();
        CardsScroll.ScrollToTop();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => FocusEditor(n));
    }

    void FocusEditor(Note n)
    {
        if (CardsList.ItemContainerGenerator.ContainerFromItem(n) is not ContentPresenter cp) return;
        var tb = FindDescendant<TextBox>(cp);
        tb?.Focus();
        if (tb is not null) tb.CaretIndex = tb.Text.Length;
    }

    void BeginEdit(Note n, Border card)
    {
        if (n.Deleted || TrashMode) return;
        n.IsEditing = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => FocusEditor(n));
    }

    void EndEdit(Note n)
    {
        if (!n.IsEditing) return;
        n.IsEditing = false;
        n.UpdatedAt = DateTime.Now;
        _repo.MarkDirty();
        Dispatcher.BeginInvoke(() => { View.Refresh(); UpdateStatus(); });
    }

    void DelBtn_Click(object s, RoutedEventArgs e)
    {
        if ((s as FrameworkElement)?.DataContext is not Note n) return;
        n.IsEditing = false;
        n.Deleted = true;
        _repo.MarkDirty();
        RefreshView();
    }

    void RestoreBtn_Click(object s, RoutedEventArgs e)
    {
        if ((s as FrameworkElement)?.DataContext is not Note n) return;
        n.Deleted = false;
        n.UpdatedAt = DateTime.Now;
        _repo.MarkDirty();
        RefreshView();
    }

    void PurgeBtn_Click(object s, RoutedEventArgs e)
    {
        if ((s as FrameworkElement)?.DataContext is not Note n) return;
        _repo.Items.Remove(n);
        _repo.MarkDirty();
        RefreshView();
    }

    void EmptyTrash_Click(object s, RoutedEventArgs e)
    {
        var dead = _repo.Items.Where(n => n.Deleted).ToList();
        if (dead.Count == 0) return;
        if (MessageBox.Show(this, $"彻底删除回收站中的 {dead.Count} 条便签？此操作不可恢复。",
                "简签 Notelet", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        foreach (var n in dead) _repo.Items.Remove(n);
        _repo.MarkDirty();
        RefreshView();
    }

    void ExitTrash_Click(object s, RoutedEventArgs e) => TrashMode = false;

    void PinBtn_Click(object s, RoutedEventArgs e)
    {
        if ((s as FrameworkElement)?.DataContext is not Note n) return;
        n.Pinned = !n.Pinned;
        n.UpdatedAt = DateTime.Now;
        _repo.MarkDirty();
        View.Refresh();
    }

    void ColorBtn_Click(object s, RoutedEventArgs e)
    {
        if ((s as Button)?.DataContext is not Note n) return;
        var btn = (Button)s!;

        var popup = new Popup
        {
            StaysOpen = false,
            PlacementTarget = btn,
            Placement = PlacementMode.Bottom,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
        };
        var wrap = new WrapPanel { Margin = new Thickness(8) };
        foreach (var c in Palette.All)
        {
            var sw = new Border
            {
                Width = 22, Height = 22, CornerRadius = new CornerRadius(7),
                Background = Solid(Palette.FromHex(c.Bg)),
                Margin = new Thickness(3), Cursor = Cursors.Hand, ToolTip = c.Name,
                BorderThickness = new Thickness(n.ColorKey == c.Key ? 2 : 1),
                BorderBrush = n.ColorKey == c.Key
                    ? (Brush)Application.Current.Resources["Accent.Brush"]
                    : (Brush)Application.Current.Resources["Border.Brush"],
            };
            sw.MouseLeftButtonDown += (_, __) =>
            {
                n.ColorKey = c.Key;
                n.UpdatedAt = DateTime.Now;
                _repo.MarkDirty();
                popup.IsOpen = false;
            };
            wrap.Children.Add(sw);
        }
        popup.Child = new Border
        {
            Background = (Brush)Application.Current.Resources["Card.Brush"],
            CornerRadius = new CornerRadius(10),
            Effect = (System.Windows.Media.Effects.DropShadowEffect)Application.Current.Resources["CardHover.Shadow"],
            Margin = new Thickness(6, 0, 6, 6),
            Child = wrap,
        };
        popup.IsOpen = true;
    }

    static SolidColorBrush Solid(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    // ───────────────────────── 卡片拖拽（拖出导出 / 双击编辑） ─────────────────────────

    void Card_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { DataContext: Note n }) return;
        if (e.ClickCount == 2)
        {
            _dragCandidate = null;
            BeginEdit(n, (Border)sender);
            return;
        }
        if (e.ChangedButton == MouseButton.Left && !n.IsEditing)
        {
            _downPos = e.GetPosition(this);
            _dragCandidate = n;
        }
    }

    void Card_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragCandidate is null || _downPos is null) return;
        if (e.LeftButton != MouseButtonState.Pressed) { _dragCandidate = null; return; }

        var p = e.GetPosition(this);
        if (Math.Abs(p.X - _downPos.Value.X) + Math.Abs(p.Y - _downPos.Value.Y) <= 8) return;

        if (FindAncestor<Button>(e.OriginalSource as DependencyObject) is not null) { _dragCandidate = null; return; }

        var n = _dragCandidate;
        _dragCandidate = null;
        DragOut(n, (Border)sender);
    }

    void DragOut(Note n, Border card)
    {
        try
        {
            var path = TempExport.Write(n);
            var data = new DataObject(DataFormats.FileDrop, new[] { path });
            DragDrop.DoDragDrop(card, data, DragDropEffects.Copy);
        }
        catch { /* 拖出失败不影响应用 */ }
    }

    // ───────────────────────── 拖入导入 ─────────────────────────

    void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.UnicodeText)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    void Window_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
                const string exts = ".txt.md.markdown.log.csv.json.ini";
                int count = 0;
                foreach (var f in files)
                {
                    if (!File.Exists(f)) continue;
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    if (!exts.Contains(ext)) continue;
                    NewNote(ReadTextSmart(f), NextColor());
                    count++;
                }
                if (count > 0) _repo.SaveNow();
            }
            else if (e.Data.GetDataPresent(DataFormats.UnicodeText) &&
                     e.Data.GetData(DataFormats.UnicodeText) is string text &&
                     !string.IsNullOrWhiteSpace(text))
            {
                NewNote(text);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"导入失败：{ex.Message}", "简签 Notelet");
        }
    }

    static string ReadTextSmart(string path)
    {
        var bytes = File.ReadAllBytes(path);
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
        }
        catch
        {
            return Encoding.GetEncoding("GB18030").GetString(bytes);
        }
    }

    // ───────────────────────── 编辑框事件 ─────────────────────────

    void Editor_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: Note n }) return;
        if (e.Key == Key.Escape)
        {
            EndEdit(n);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            EndEdit(n);
            e.Handled = true;
        }
    }

    void Editor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: Note n } && n.IsEditing)
            EndEdit(n);
    }

    // ───────────────────────── 顶栏 ─────────────────────────

    void NewBtn_Click(object sender, RoutedEventArgs e) => NewNote();

    void ThemeBtn_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        ThemePopup.IsOpen = !ThemePopup.IsOpen;
    }

    void PinWinBtn_Click(object sender, RoutedEventArgs e)
    {
        Topmost = PinWinBtn.IsChecked == true;
        _settings.Topmost = Topmost;
        SaveSettingsSoon();
    }

    void TrashBtn_Click(object sender, RoutedEventArgs e) => TrashMode = !TrashMode;

    void MoreBtn_Click(object sender, RoutedEventArgs e)
    {
        ThemePopup.IsOpen = false;
        MenuPopup.IsOpen = !MenuPopup.IsOpen;
    }

    void MinBtn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _search = SearchBox.Text.Trim();
        ClearSearchBtn.Visibility = _search.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshView();
    }

    void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        SearchBox.Focus();
    }

    // ───────────────────────── 更多菜单 ─────────────────────────

    void OpenData_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        Paths.Ensure();
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = Paths.Root,
            UseShellExecute = true,
        });
    }

    void ExportAll_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON 备份|*.json",
            FileName = $"notelet-backup-{DateTime.Now:yyyyMMdd}.json",
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            Json.Save(dlg.FileName, _repo.Items);
            MessageBox.Show(this, $"已导出 {_repo.Items.Count} 条便签。\n{dlg.FileName}", "简签 Notelet");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"导出失败：{ex.Message}", "简签 Notelet");
        }
    }

    void ImportAll_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "JSON 备份|*.json" };
        if (dlg.ShowDialog(this) != true) return;
        var list = Json.Load<List<Note>>(dlg.FileName);
        if (list is null || list.Count == 0)
        {
            MessageBox.Show(this, "没有可导入的便签。", "简签 Notelet");
            return;
        }
        var ids = _repo.Items.Select(n => n.Id).ToHashSet();
        int added = 0;
        foreach (var n in list)
        {
            if (string.IsNullOrEmpty(n.Id) || !ids.Add(n.Id))
            {
                n.Id = Guid.NewGuid().ToString("N");
                _repo.Items.Add(n);
                added++;
            }
            else
            {
                _repo.Items.Add(n);
                added++;
            }
        }
        _repo.MarkDirty();
        RefreshView();
        MessageBox.Show(this, $"已导入 {added} 条便签。", "简签 Notelet");
    }

    void About_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        MessageBox.Show(this,
            $"简签 Notelet v{ver}\n\n小而美的桌面便签。\n\n· 数据保存在 %APPDATA%\\Notelet\n· 纯本地存储，不上传任何内容\n· MIT 开源",
            "关于 简签", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ───────────────────────── 快捷键 / 画布 ─────────────────────────

    void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N)
        {
            NewNote();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.K)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V)
        {
            if (Keyboard.FocusedElement is TextBox) return; // 让焦点框内正常粘贴
            if (Clipboard.ContainsText())
            {
                NewNote(Clipboard.GetText());
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Escape && TrashMode)
        {
            TrashMode = false;
            e.Handled = true;
        }
    }

    void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (InsideCard(e.OriginalSource as DependencyObject)) return;
        NewNote();
    }

    static bool InsideCard(DependencyObject? d)
    {
        while (d is not null)
        {
            if (d is Border b && b.Name == "CardRoot") return true;
            d = VisualTreeHelper.GetParent(d);
        }
        return false;
    }

    static T? FindDescendant<T>(DependencyObject? root) where T : class
    {
        if (root is null) return null;
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) return hit;
            var deep = FindDescendant<T>(child);
            if (deep is not null) return deep;
        }
        return null;
    }

    static T? FindAncestor<T>(DependencyObject? d) where T : class
    {
        while (d is not null)
        {
            if (d is T b) return b;
            d = VisualTreeHelper.GetParent(d);
        }
        return null;
    }

    // ───────────────────────── 主题 / 美化面板 ─────────────────────────

    void ApplyThemeFromSettings()
    {
        var t = ThemeService.Presets.FirstOrDefault(p => p.Id == _settings.ThemeId)
             ?? _settings.CustomThemes.FirstOrDefault(c => c.Id == _settings.ThemeId)
             ?? ThemeService.Presets[0];
        _theme = t.Clone();
        ThemeService.ApplyBrushes(_theme);
    }

    void ThemePopup_Opened(object sender, EventArgs e) => BuildThemePanel();

    object Res(string key) => Application.Current.Resources[key];

    TextBlock Label(string text, double top = 12) => new()
    {
        Text = text,
        FontSize = 12,
        FontWeight = FontWeights.SemiBold,
        Foreground = (Brush)Res("Sub.Brush"),
        Margin = new Thickness(0, top, 0, 8),
    };

    void BuildThemePanel()
    {
        _buildingPanel = true;
        var root = ThemePanelRoot;
        root.Children.Clear();

        // ── 主题预设 ──
        root.Children.Add(Label("主题预设", 4));
        var presets = new WrapPanel();
        foreach (var p in ThemeService.Presets.Concat(_settings.CustomThemes)
                     .GroupBy(t => t.Id).Select(g => g.First()))
            presets.Children.Add(PresetChip(p));
        root.Children.Add(presets);

        // ── 数值 ──
        root.Children.Add(Label("细节"));
        AddSlider(root, "圆角", 0, 24, _theme.Radius, v => _theme.Radius = v);
        AddSlider(root, "字号", 12, 20, _theme.FontSize, v => _theme.FontSize = v);
        AddSlider(root, "卡片宽", 200, 320, _theme.CardWidth, v => _theme.CardWidth = v);

        // ── 背景效果 ──
        root.Children.Add(Label("背景效果"));
        root.Children.Add(ChipRow(new[]
        {
            ("无", "none"), ("云母", "mica"), ("亚克力", "acrylic"),
        }, _theme.Backdrop, v => _theme.Backdrop = v));

        // ── 字体 ──
        root.Children.Add(Label("字体"));
        root.Children.Add(ChipRow(new[]
        {
            ("微软雅黑", "Microsoft YaHei UI"), ("等线", "DengXian"),
            ("楷体", "KaiTi"), ("Segoe UI", "Segoe UI"),
        }, _theme.Font, v => _theme.Font = v));

        // ── 颜色 ──
        root.Children.Add(Label("颜色"));
        AddColorRow(root, "窗口底色", WinBgSwatches, () => _theme.Bg, v => _theme.Bg = v);
        AddColorRow(root, "卡片底色", CardSwatches, () => _theme.Card, v => _theme.Card = v);
        AddColorRow(root, "强调色", AccentSwatches, () => _theme.Accent, v => _theme.Accent = v);
        AddColorRow(root, "文字色", TextSwatches, () => _theme.Text, v => _theme.Text = v);

        // ── 导出 / 导入 ──
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        row.Children.Add(MiniLink("导出主题", ExportTheme_Click));
        row.Children.Add(MiniLink("导入主题", ImportTheme_Click));
        row.Children.Add(new TextBlock
        {
            Text = "改动实时生效",
            FontSize = 11,
            Foreground = (Brush)Res("Sub.Brush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
        });
        root.Children.Add(row);

        _buildingPanel = false;
    }

    static readonly string[] WinBgSwatches =
    {
        "#F7F3EC", "#FBF1EE", "#EDF5F1", "#EFF3F8", "#F6F1F8", "#F4F4F1",
        "#FAF6EA", "#EFEDE6", "#1D1E23", "#20222A", "#26221F", "#121317",
    };
    static readonly string[] CardSwatches =
    {
        "#FFFFFF", "#FFF9EF", "#FFF3F0", "#F2FBF7", "#F0F7FF", "#F6F3FF",
        "#FFF5F9", "#2B2D36", "#26272E", "#2E2F33", "#33342B", "#1F2024",
    };
    static readonly string[] AccentSwatches =
    {
        "#D97E5F", "#E4795F", "#3BA98B", "#5B9BD5", "#7C6BD9", "#D96AA7",
        "#C9A227", "#4A7A8C", "#8FB7F0", "#B08659", "#6B7280", "#C0504D",
    };
    static readonly string[] TextSwatches =
    {
        "#3D3A34", "#2F4440", "#4A3A36", "#33415C", "#3D3A5C", "#5A5246",
        "#E9E7E2", "#DDE3E8", "#E5DCCF", "#F2EFE9", "#A8B2BD", "#6B7280",
    };

    UIElement PresetChip(ThemeConfig p)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, 10, 10) };
        var b = new Border
        {
            Width = 58, Height = 38, CornerRadius = new CornerRadius(8),
            Background = Solid(Palette.FromHex(p.Bg)),
            BorderThickness = new Thickness(p.Id == _theme.Id ? 2 : 1),
            BorderBrush = p.Id == _theme.Id
                ? (Brush)Res("Accent.Brush") : (Brush)Res("Border.Brush"),
            Cursor = Cursors.Hand,
        };
        b.Child = new Ellipse
        {
            Width = 10, Height = 10, Fill = Solid(Palette.FromHex(p.Accent)),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(5, 0, 0, 4),
        };
        b.MouseLeftButtonDown += (_, e) => { e.Handled = true; ApplyThemeConfig(p.Clone()); };
        sp.Children.Add(b);
        sp.Children.Add(new TextBlock
        {
            Text = p.Name, FontSize = 10.5,
            Foreground = (Brush)Res("Sub.Brush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 3, 0, 0),
        });
        return sp;
    }

    UIElement ChipRow((string label, string value)[] items, string current, Action<string> set)
    {
        var wrap = new WrapPanel();
        foreach (var (label, value) in items)
        {
            bool selected = value == current;
            var chip = new Border
            {
                Padding = new Thickness(12, 5, 12, 5),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 8, 8),
                Cursor = Cursors.Hand,
                Background = selected ? (Brush)Res("Field.Brush") : Brushes.Transparent,
                BorderThickness = new Thickness(1),
                BorderBrush = selected ? (Brush)Res("Accent.Brush") : (Brush)Res("Border.Brush"),
                Child = new TextBlock { Text = label, FontSize = 12 },
            };
            chip.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                set(value);
                MarkCustom();
                ThemeService.ApplyBrushes(_theme);
                ThemeService.ApplyBackdrop(this);
                SaveSettingsSoon();
                BuildThemePanel();
            };
            wrap.Children.Add(chip);
        }
        return wrap;
    }

    void AddSlider(StackPanel root, string label, double min, double max, double value, Action<double> set)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        g.Children.Add(new TextBlock
        {
            Text = label, FontSize = 12,
            Foreground = (Brush)Res("Text.Brush"),
            VerticalAlignment = VerticalAlignment.Center,
        });

        var s = new Slider
        {
            Minimum = min, Maximum = max, Value = value,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0),
        };
        var readout = new TextBlock
        {
            Text = ((int)Math.Round(value)).ToString(), FontSize = 11.5,
            Foreground = (Brush)Res("Sub.Brush"),
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 24, TextAlignment = TextAlignment.Right,
        };
        Grid.SetColumn(s, 1);
        Grid.SetColumn(readout, 2);
        g.Children.Add(s);
        g.Children.Add(readout);

        s.ValueChanged += (_, __) =>
        {
            if (_buildingPanel) return;
            set(s.Value);
            readout.Text = ((int)Math.Round(s.Value)).ToString();
            MarkCustom();
            ThemeService.ApplyBrushes(_theme);
            SaveSettingsSoon();
        };

        root.Children.Add(g);
    }

    void AddColorRow(StackPanel root, string label, string[] swatches, Func<string> get, Action<string> set)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        g.Children.Add(new TextBlock
        {
            Text = label, FontSize = 12,
            Foreground = (Brush)Res("Text.Brush"),
            VerticalAlignment = VerticalAlignment.Center,
        });

        var wrap = new WrapPanel();
        foreach (var hex in swatches)
        {
            bool selected = string.Equals(hex, get(), StringComparison.OrdinalIgnoreCase);
            var sw = new Border
            {
                Width = 20, Height = 20, CornerRadius = new CornerRadius(6),
                Background = Solid(Palette.FromHex(hex)),
                Margin = new Thickness(0, 0, 6, 6),
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(selected ? 2 : 1),
                BorderBrush = selected ? (Brush)Res("Accent.Brush") : (Brush)Res("Border.Brush"),
                ToolTip = hex,
            };
            sw.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                set(hex);
                MarkCustom();
                AfterThemeVisualChange();
                BuildThemePanel();
            };
            wrap.Children.Add(sw);
        }
        Grid.SetColumn(wrap, 1);
        g.Children.Add(wrap);

        var custom = new Button
        {
            Content = "…", FontSize = 11, Width = 24, Height = 20,
            Style = (Style)Res("FlatBtn"), Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Foreground = (Brush)Res("Text.Brush"),
            ToolTip = "自定义颜色…",
        };
        custom.Click += (_, __) =>
        {
            using var dlg = new WinForms.ColorDialog { FullOpen = true };
            var cur = Palette.FromHex(get());
            dlg.Color = System.Drawing.Color.FromArgb(cur.A, cur.R, cur.G, cur.B);
            if (dlg.ShowDialog() != WinForms.DialogResult.OK) return;
            set($"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}");
            MarkCustom();
            AfterThemeVisualChange();
            BuildThemePanel();
        };
        Grid.SetColumn(custom, 2);
        g.Children.Add(custom);

        root.Children.Add(g);
    }

    void AfterThemeVisualChange()
    {
        ThemeService.ApplyBrushes(_theme);
        ThemeService.ApplyBackdrop(this);
        SaveSettingsSoon();
    }

    UIElement MiniLink(string text, RoutedEventHandler onClick)
    {
        var b = new Button
        {
            Content = text,
            Style = (Style)Res("FlatBtn"),
            FontSize = 12,
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(0, 0, 8, 0),
            Foreground = (Brush)Res("Accent.Brush"),
        };
        b.Click += onClick;
        return b;
    }

    void MarkCustom()
    {
        if (_theme.Id == "custom") return;
        _theme.Id = "custom";
        _theme.Name = "自定义";
        _settings.CustomThemes.RemoveAll(t => t.Id == "custom");
        _settings.CustomThemes.Add(_theme);
        _settings.ThemeId = "custom";
    }

    void ApplyThemeConfig(ThemeConfig t)
    {
        _theme = t;
        _settings.ThemeId = t.Id;
        ThemeService.ApplyBrushes(_theme);
        ThemeService.ApplyBackdrop(this);
        SaveSettingsSoon();
        BuildThemePanel();
    }

    void ExportTheme_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "主题文件|*.json",
            FileName = $"notelet-theme-{_theme.Name}.json",
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            Json.Save(dlg.FileName, _theme);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"导出失败：{ex.Message}", "简签 Notelet");
        }
    }

    void ImportTheme_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "主题文件|*.json" };
        if (dlg.ShowDialog(this) != true) return;
        var t = Json.Load<ThemeConfig>(dlg.FileName);
        if (t is null)
        {
            MessageBox.Show(this, "主题文件无效。", "简签 Notelet");
            return;
        }
        try
        {
            // 校验颜色格式
            _ = Palette.FromHex(t.Bg);
            _ = Palette.FromHex(t.Card);
            _ = Palette.FromHex(t.Text);
            _ = Palette.FromHex(t.Sub);
            _ = Palette.FromHex(t.Accent);
        }
        catch
        {
            MessageBox.Show(this, "主题文件中的颜色格式不正确。", "简签 Notelet");
            return;
        }
        t.Radius = Math.Clamp(t.Radius, 0, 24);
        t.FontSize = Math.Clamp(t.FontSize, 12, 20);
        t.CardWidth = Math.Clamp(t.CardWidth, 200, 320);
        t.Id = "custom";
        t.Name = "导入主题";
        _settings.CustomThemes.RemoveAll(x => x.Id == "custom");
        _settings.CustomThemes.Add(t);
        ApplyThemeConfig(t.Clone());
    }

    void SaveSettingsSoon()
    {
        _settingsSaveTimer.Stop();
        _settingsSaveTimer.Start();
    }

    // ───────────────────────── 生命周期 ─────────────────────────

    void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        foreach (var n in _repo.Items.Where(n => n.IsEditing).ToList())
        {
            n.IsEditing = false;
            n.UpdatedAt = DateTime.Now;
        }
        _repo.SaveNow();

        _settings.WindowWidth = Width;
        _settings.WindowHeight = Height;
        _settings.Topmost = Topmost;
        SettingsRepo.Save(_settings);
    }
}
