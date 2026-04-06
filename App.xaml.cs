using System;
using System.Text;
using System.Windows;

namespace SimpleSshClient;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
}
