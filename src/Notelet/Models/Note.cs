using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Notelet.Models;

public class Note : INotifyPropertyChanged
{
    string _text = "";
    bool _pinned;
    bool _editing;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

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

    public bool Pinned
    {
        get => _pinned;
        set { if (_pinned == value) return; _pinned = value; OnP(); }
    }

    public bool Deleted { get; set; }

    [JsonIgnore]
    public bool IsEditing
    {
        get => _editing;
        set { if (_editing == value) return; _editing = value; OnP(); }
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
            return "新便签";
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
