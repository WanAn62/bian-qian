using System.Text;
using System.Windows;
using System.Threading;

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
            MessageBox.Show($"出现异常：{args.Exception.Message}", "简签 Notelet",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        base.OnStartup(e);
    }
}
