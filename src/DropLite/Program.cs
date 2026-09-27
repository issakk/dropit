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
                "DropLite is already running. Look for the floating icon on your desktop.",
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
