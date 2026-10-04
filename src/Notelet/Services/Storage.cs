using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows.Threading;
using Notelet.Models;

namespace Notelet.Services;

public static class Paths
{
    public static readonly string Root =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Notelet");
    public static readonly string NotesFile = Path.Combine(Root, "notes.json");
    public static readonly string SettingsFile = Path.Combine(Root, "settings.json");
    public static readonly string BackupDir = Path.Combine(Root, "backups");
    public static readonly string ImagesDir = Path.Combine(Root, "images");

    public static void Ensure()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(BackupDir);
        Directory.CreateDirectory(ImagesDir);
    }
}

public static class Json
{
    public static readonly JsonSerializerOptions Opt = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static T? Load<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Opt); }
        catch { return null; }
    }

    public static void Save<T>(string path, T data)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(data, Opt), new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else File.Move(tmp, path);
    }
}

public static class SettingsRepo
{
    public static AppSettings? Load() => Json.Load<AppSettings>(Paths.SettingsFile);
    public static void Save(AppSettings s) { try { Json.Save(Paths.SettingsFile, s); } catch { } }
}

public class NotesRepo
{
    List<Note> _notes = new();
    bool _dirty;
    DispatcherTimer? _timer;
    DateTime? _lastSaved;

    public List<Note> Items => _notes;
    public DateTime? LastSaved => _lastSaved;
    public event Action? Saved;

    public static NotesRepo LoadOrCreate()
    {
        Paths.Ensure();
        var r = new NotesRepo();
        var list = Json.Load<List<Note>>(Paths.NotesFile);
        if (list is null)
        {
            Seed(r._notes);
            r.SaveNow();
        }
        else
        {
            r._notes = list;
        }
        r.Backup();
        return r;
    }

    static void Seed(List<Note> ns)
    {
        ns.Add(new Note
        {
            ColorKey = "mint",
            Text = "欢迎使用 简签 Notelet\n\n这是一块小而美的桌面便签：\n· 双击卡片即可编辑，首行是标题\n· 数据只保存在你自己的电脑上\n· 点右上角的调色板按钮，可以换主题、调颜色",
        });
        ns.Add(new Note
        {
            ColorKey = "sky",
            Text = "拖进拖出 · 随呼随用\n\n· 把 .txt / .md 文件拖进窗口，会变成一张便签\n· 把卡片拖到桌面或资源管理器，会导出成文件\n· 关闭窗口只是收到托盘，Ctrl+Alt+N 随时唤出\n· 点卡片上的 📌 图标，可以把便签贴成桌面磁贴",
        });
        ns.Add(new Note
        {
            ColorKey = "peach",
            IsMarkdown = true,
            Text = "Markdown 试试看\n\n## 待办清单\n- [x] 把卡片拖出窗口导出文件\n- [ ] 双击卡片，点「预览」看渲染效果\n- [ ] 点 MD 按钮切换 Markdown 模式\n- [ ] 点 📌 把这张卡片贴在桌面上\n\n> 提示：预览里可以直接点击任务框勾选。\n\n**粗体**、*斜体*、~~删除线~~、`行内代码` 都支持。",
        });
        ns.Add(new Note
        {
            ColorKey = "cream",
            Pinned = true,
            Text = "快捷键\n\nCtrl+N    新建便签\nCtrl+K    搜索\nCtrl+V    粘贴新建\nCtrl+Alt+N    呼出 / 隐藏窗口\n双击      编辑卡片\nCtrl+Enter / Esc   完成编辑\n\n试着按住我拖到桌面 →\n就会得到一个文件。\n\n删除的便签会进回收站，\n随时可以恢复。",
        });
    }

    void Backup()
    {
        try
        {
            if (_notes.Count == 0 || !File.Exists(Paths.NotesFile)) return;
            var old = Directory.GetFiles(Paths.BackupDir, "notes-*.json")
                               .OrderBy(f => f, StringComparer.Ordinal).ToArray();
            if (old.Length >= 10) File.Delete(old[0]);
            var path = Path.Combine(Paths.BackupDir, $"notes-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Copy(Paths.NotesFile, path, overwrite: true);
        }
        catch { }
    }

    public void MarkDirty()
    {
        _dirty = true;
        if (_timer is not null) return;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _timer.Tick += (_, __) =>
        {
            _timer.Stop();
            if (_dirty) SaveNow();
        };
        _timer.Start();
    }

    public void SaveNow()
    {
        try
        {
            Json.Save(Paths.NotesFile, _notes);
            _dirty = false;
            _lastSaved = DateTime.Now;
            Saved?.Invoke();
        }
        catch { }
    }
}

public static class TempExport
{
    static string Dir => Path.Combine(Path.GetTempPath(), "NoteletExport");

    /// <summary>把便签导出为临时文件；有图片时一并拷贝，返回主文件 + 图片路径（供系统拖拽多文件）。</summary>
    public static List<string> Write(Note n)
    {
        Directory.CreateDirectory(Dir);
        var path = Path.Combine(Dir, FileName(n));
        File.WriteAllText(path, n.Text, new UTF8Encoding(true));
        var files = new List<string> { path };
        foreach (var img in n.ImageFiles)
        {
            var src = Path.Combine(Paths.ImagesDir, img);
            if (!File.Exists(src)) continue;
            var dst = Path.Combine(Dir, img);
            File.Copy(src, dst, overwrite: true);
            files.Add(dst);
        }
        return files;
    }

    public static string FileName(Note n)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var t = n.Title == "新便签" ? "note" : n.Title;
        var sb = new StringBuilder();
        foreach (var ch in t)
        {
            sb.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
            if (sb.Length >= 24) break;
        }
        var ext = n.IsMarkdown ? ".md" : ".txt";
        return $"简签-{sb}-{DateTime.Now:HHmmss}{ext}";
    }

    public static void Cleanup()
    {
        try
        {
            if (!Directory.Exists(Dir)) return;
            foreach (var f in Directory.GetFiles(Dir))
                if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-1))
                    File.Delete(f);
        }
        catch { }
    }
}
