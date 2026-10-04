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
    bool _deleted;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>是否按 Markdown 渲染。</summary>
    public bool IsMarkdown
    {
        get => _isMarkdown;
        set { if (_isMarkdown == value) return; _isMarkdown = value; OnP(); }
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
        }
    }

    public string ColorKey { get; set; } = "auto";

    /// <summary>附带的图片文件名（存于 %APPDATA%\Notelet\images）。</summary>
    public List<string> ImageFiles { get; set; } = new();

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
