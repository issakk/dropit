using System;
using System.IO;

namespace DropLite.Services;

/// <summary>
/// 轻量文件日志：%APPDATA%\DropLite\logs\dropit-yyyy-MM-dd.log，保留最近 14 天。
/// 日志失败绝不抛出，避免影响主流程。
/// </summary>
internal static class Logger
{
    private const int RetentionDays = 14;

    private static readonly object Lock = new();
    private static bool _cleanedUp;

    public static string LogDirectory
    {
        get
        {
            string dir = Path.Combine(ConfigStore.DirectoryPath, "logs");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string CurrentLogFile
        => Path.Combine(LogDirectory, $"dropit-{DateTime.Now:yyyy-MM-dd}.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null)
        => Write("ERROR", ex is null ? message : $"{message}: {ex}");

    public static void Write(string level, string message)
    {
        try
        {
            lock (Lock)
            {
                CleanUpOldLogs();
                File.AppendAllText(
                    CurrentLogFile,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
        }
    }

    private static void CleanUpOldLogs()
    {
        if (_cleanedUp)
        {
            return;
        }
        _cleanedUp = true;
        try
        {
            DateTime cutoff = DateTime.Now.AddDays(-RetentionDays);
            foreach (string file in Directory.GetFiles(LogDirectory, "dropit-*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
        }
    }
}
