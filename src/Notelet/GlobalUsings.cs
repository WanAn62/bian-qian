// 项目同时启用 UseWPF 与 UseWindowsForms（为使用 ColorDialog），
// 用全局别名把常用类型统一裁定到 WPF 侧；WinForms 类型一律通过 WinForms 别名访问。
global using Application = System.Windows.Application;
global using Brush = System.Windows.Media.Brush;
global using Brushes = System.Windows.Media.Brushes;
global using Button = System.Windows.Controls.Button;
global using Clipboard = System.Windows.Clipboard;
global using Color = System.Windows.Media.Color;
global using Cursors = System.Windows.Input.Cursors;
global using DataFormats = System.Windows.DataFormats;
global using DataObject = System.Windows.DataObject;
global using DragDropEffects = System.Windows.DragDropEffects;
global using DragEventArgs = System.Windows.DragEventArgs;
global using HorizontalAlignment = System.Windows.HorizontalAlignment;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;
global using MessageBox = System.Windows.MessageBox;
global using MouseEventArgs = System.Windows.Input.MouseEventArgs;
global using FontFamily = System.Windows.Media.FontFamily;
global using Orientation = System.Windows.Controls.Orientation;
global using Path = System.IO.Path;
global using Point = System.Windows.Point;
global using TextBox = System.Windows.Controls.TextBox;
global using VerticalAlignment = System.Windows.VerticalAlignment;
