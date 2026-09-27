using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using DropLite.Models;

namespace DropLite.Services;

internal static class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string DirectoryPath
    {
        get
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DropLite");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string FilePath => Path.Combine(DirectoryPath, "config.json");

    public static bool Exists => File.Exists(FilePath);

    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new AppConfig();
            }

            return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath), JsonOptions) ?? new AppConfig();
        }
        catch (Exception)
        {
            // A broken config must never stop the app from starting.
            return new AppConfig();
        }
    }

    public static void Save(AppConfig config)
    {
        string path = FilePath;
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(config, JsonOptions));
        File.Move(temp, path, overwrite: true);
    }

    public static T Clone<T>(T value) where T : class
    {
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!;
    }
}
