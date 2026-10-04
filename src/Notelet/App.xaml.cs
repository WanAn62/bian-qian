using System.IO;
using System.Text;
using System.Windows;
using System.Threading;
using Notelet.Services;

namespace Notelet;

public partial class App : Application
{
    static Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        _mutex = new Mutex(true, "Notelet.SingleInstance", out bool fresh);
        if (!fresh)
        {
            MessageBox.Show("简签已在运行中。", "简签 Notelet", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                Paths.Ensure();
                File.WriteAllText(
                    System.IO.Path.Combine(Paths.Root, "crash.log"),
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{args.Exception}\n\n内部异常：{args.Exception.InnerException}\n");
            }
            catch { }
            MessageBox.Show($"出现异常：{args.Exception.Message}\n\n详细信息已写入 %APPDATA%\\Notelet\\crash.log",
                "简签 Notelet", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        base.OnStartup(e);
    }
}
