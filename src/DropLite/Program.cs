using System;
using System.Threading;
using System.Windows.Forms;

namespace DropLite;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, @"Local\DropLite.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "DropLite 已经在运行中，请查看桌面上的悬浮图标。",
                "DropLite",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new AppContext());
    }
}
