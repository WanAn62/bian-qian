using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Notelet.Models;

public class Note : INotifyPropertyChanged
{
    string _text = "";
    bool _pinned;
    bool _editing;
    bool _previewing;
    bool _isMarkdown;
    bool _tiled;
    bool _split;
    bool _deleted;
    DateTime? _remindAt;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>是否按 Markdown 渲染。</summary>
    public bool IsMarkdown
    {
        get => _isMarkdown;
        set
        {
            if (_isMarkdown == value) return;
            _isMarkdown = value;
            OnP();
            OnP(nameof(TaskProgress));
            OnP(nameof(TaskPercent));
        }
    }

    public DateTime? RemindAt
    {
        get => _remindAt;
        set
        {
            if (_remindAt == value) return;
            _remindAt = value;
            OnP();
            OnP(nameof(RemindText));
        }
    }

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            OnP();
            OnP(nameof(Title));
            OnP(nameof(Body));
            OnP(nameof(TaskProgress));
            OnP(nameof(TaskPercent));
        }
    }

    public string ColorKey { get; set; } = "auto";

    /// <summary>附带的图片文件名（存于 %APPDATA%\Notelet\images）。</summary>
    public List<string> ImageFiles { get; set; } = new();

    /// <summary>提醒是否已触发过（防止重复弹通知）。</summary>
    public bool Reminded { get; set; }

    /// <summary>该便签的字号缩放（0.7 ~ 2.0，Ctrl+滚轮调整）。</summary>
    public double Zoom { get; set; } = 1.0;

    /// <summary>提醒状态文案（供卡片显示；空 = 不显示）。</summary>
    [JsonIgnore]
    public string RemindText
    {
        get
        {
            if (RemindAt is null) return "";
            if (Reminded) return "⏰ 已到期";
            var t = RemindAt.Value;
            return t.Year == DateTime.Now.Year
                ? $"⏰ {t:MM/dd HH:mm}"
                : $"⏰ {t:yyyy/MM/dd HH:mm}";
        }
    }

    /// <summary>Markdown 任务清单进度（如 "2/5"；无任务时为空）。</summary>
    [JsonIgnore]
    public string TaskProgress
    {
        get
        {
            if (!IsMarkdown) return "";
            int total = 0, done = 0;
            foreach (var line in _text.Replace("\r", "").Split('\n'))
            {
                var t = line.TrimStart();
                if (t.StartsWith("- [ ] ") || t.StartsWith("- [X] ") || t.StartsWith("- [x] "))
                {
                    total++;
                    if (t[3] != ' ') done++;
                }
            }
            return total == 0 ? "" : $"{done}/{total}";
        }
    }

    /// <summary>任务进度百分比（0-100，无任务 -1，供进度条绑定）。</summary>
    [JsonIgnore]
    public int TaskPercent
    {
        get
        {
            var p = TaskProgress;
            if (p == "") return -1;
            var parts = p.Split('/');
            return (int)(double.Parse(parts[0]) / double.Parse(parts[1]) * 100);
        }
    }

    public bool Pinned
    {
        get => _pinned;
        set { if (_pinned == value) return; _pinned = value; OnP(); }
    }

    public bool Deleted
    {
        get => _deleted;
        set { if (_deleted == value) return; _deleted = value; OnP(); }
    }

    [JsonIgnore]
    public bool IsEditing
    {
        get => _editing;
        set { if (_editing == value) return; _editing = value; OnP(); }
    }

    /// <summary>编辑中的卡片是否处于预览态（仅运行时）。</summary>
    [JsonIgnore]
    public bool IsPreviewing
    {
        get => _previewing;
        set { if (_previewing == value) return; _previewing = value; OnP(); }
    }

    /// <summary>是否已打开磁贴窗口（仅运行时）。</summary>
    [JsonIgnore]
    public bool IsTiled
    {
        get => _tiled;
        set { if (_tiled == value) return; _tiled = value; OnP(); }
    }

    /// <summary>编辑时是否分屏实时渲染（仅运行时）。</summary>
    [JsonIgnore]
    public bool IsSplit
    {
        get => _split;
        set { if (_split == value) return; _split = value; OnP(); }
    }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public string Title
    {
        get
        {
            foreach (var raw in _text.Replace("\r", "").Split('\n'))
            {
                var s = raw.Trim();
                if (s.Length > 0) return s;
            }
            return ImageFiles.Count > 0 ? "图片便签" : "新便签";
        }
    }

    [JsonIgnore]
    public string Body
    {
        get
        {
            var lines = _text.Replace("\r", "").Split('\n');
            bool seenTitle = false;
            var rest = new List<string>();
            foreach (var l in lines)
            {
                if (!seenTitle)
                {
                    if (string.IsNullOrWhiteSpace(l)) continue;
                    seenTitle = true;
                    continue;
                }
                rest.Add(l);
            }
            return string.Join("\n", rest);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnP([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
