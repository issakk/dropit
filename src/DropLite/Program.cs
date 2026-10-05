using System;
using System.Threading;
using System.Windows.Forms;
using DropLite.Services;

namespace DropLite;

internal static class Program
{
    private static string? _lastErrorKey;
    private static DateTime _lastErrorShown;

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

        // 全局异常兜底：记录到日志，UI 线程异常不结束程序。
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            Logger.Error("UI thread exception", e.Exception);
            // 同一错误 10 秒内只弹一次，防止异常风暴连环弹窗（日志仍全量记录）。
            string key = e.Exception.GetType().FullName + "|" + e.Exception.Message;
            if (key == _lastErrorKey && DateTime.Now - _lastErrorShown < TimeSpan.FromSeconds(10))
            {
                return;
            }
            _lastErrorKey = key;
            _lastErrorShown = DateTime.Now;
            MessageBox.Show(
                "发生了一个内部错误，程序将继续运行。\n详情请查看日志文件夹（托盘菜单 → 打开日志文件夹）。",
                "DropLite",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Logger.Error("unhandled exception", e.ExceptionObject as Exception);
        };

        Application.Run(new AppContext());
    }
}
