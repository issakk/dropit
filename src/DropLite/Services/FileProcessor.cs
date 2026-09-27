using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DropLite.Models;

namespace DropLite.Services;

internal sealed class ProcessResult
{
    public int Moved;
    public int Copied;
    public int Deleted;
    public int Compressed;
    public int Extracted;
    public int Renamed;
    public int Opened;
    public int Ignored;
    public int Skipped;
    public int Failed;

    public readonly List<string> Errors = new();

    /// <summary>本批次涉及的目标目录（用于气泡通知与日志）。</summary>
    public readonly List<string> Targets = new();

    /// <summary>目标位置的人读摘要：最多列两处，多则折叠。</summary>
    public string TargetSummary()
    {
        var distinct = new List<string>();
        foreach (string target in Targets)
        {
            if (!distinct.Contains(target))
            {
                distinct.Add(target);
            }
        }
        if (distinct.Count == 0)
        {
            return string.Empty;
        }
        if (distinct.Count <= 2)
        {
            return string.Join("、", distinct);
        }
        return $"{distinct[0]}、{distinct[1]} 等 {distinct.Count} 处";
    }

    public string Summary()
    {
        var parts = new List<string>();
        if (Moved > 0) parts.Add($"已移动 {Moved}");
        if (Copied > 0) parts.Add($"已复制 {Copied}");
        if (Compressed > 0) parts.Add($"已压缩 {Compressed}");
        if (Extracted > 0) parts.Add($"已解压 {Extracted}");
        if (Renamed > 0) parts.Add($"已重命名 {Renamed}");
        if (Deleted > 0) parts.Add($"已回收 {Deleted}");
        if (Opened > 0) parts.Add($"已打开 {Opened}");
        if (Skipped > 0) parts.Add($"跳过 {Skipped}");
        if (Ignored > 0) parts.Add($"已忽略 {Ignored}");
        if (Failed > 0) parts.Add($"失败 {Failed}");
        return parts.Count == 0 ? "没有可处理的文件" : string.Join("，", parts);
    }
}

/// <summary>Thrown to mark an item as skipped without treating it as an error.</summary>
internal sealed class SkipItemException : Exception
{
    public SkipItemException(string message) : base(message) { }
}

internal static class FileProcessor
{
    private static readonly ConcurrentDictionary<string, Regex> RegexCache =
        new(StringComparer.OrdinalIgnoreCase);

    public static async Task<ProcessResult> ProcessAsync(
        Profile profile, IReadOnlyList<string> paths, Action<string>? log = null)
    {
        var result = new ProcessResult();
        // Snapshot so concurrent settings edits can't mutate what we iterate.
        var snapshot = ConfigStore.Clone(profile);

        var groups = new List<KeyValuePair<Destination, List<string>>>();
        foreach (string path in paths)
        {
            Destination? dest = MatchDestination(snapshot, path);
            if (dest is null || dest.Action == DropAction.Ignore)
            {
                lock (result) result.Ignored++;
                log?.Invoke($"ignored: {path}");
                continue;
            }

            List<string>? bucket = null;
            foreach (var g in groups)
            {
                if (ReferenceEquals(g.Key, dest)) { bucket = g.Value; break; }
            }
            if (bucket is null)
            {
                bucket = new List<string>();
                groups.Add(new KeyValuePair<Destination, List<string>>(dest, bucket));
            }
            bucket.Add(path);
        }

        await Task.Run(() =>
        {
            foreach (var group in groups)
            {
                try
                {
                    ExecuteGroup(group.Key, group.Value, result, log);
                }
                catch (Exception ex)
                {
                    lock (result)
                    {
                        result.Failed += group.Value.Count;
                        result.Errors.Add($"{group.Key.Name}: {ex.Message}");
                    }
                    log?.Invoke($"error in '{group.Key.Name}': {ex.Message}");
                }
            }
        }).ConfigureAwait(false);

        return result;
    }

    // ---------------------------------------------------------------- matching

    public static Destination? MatchDestination(Profile profile, string path)
    {
        string name = Path.GetFileName(path);
        foreach (Destination dest in profile.Destinations)
        {
            if (MatchesPattern(dest.Pattern, name))
            {
                return dest;
            }
        }
        return null;
    }

    public static bool MatchesPattern(string pattern, string name)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return true;
        }

        foreach (string token in pattern.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (GetWildcardRegex(token).IsMatch(name))
            {
                return true;
            }
        }
        return false;
    }

    private static Regex GetWildcardRegex(string wildcard)
    {
        return RegexCache.GetOrAdd(wildcard, w =>
        {
            var sb = new StringBuilder("^");
            foreach (char c in w)
            {
                switch (c)
                {
                    case '*': sb.Append(".*"); break;
                    case '?': sb.Append('.'); break;
                    default: sb.Append(Regex.Escape(c.ToString())); break;
                }
            }
            sb.Append('$');
            return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        });
    }

    // ---------------------------------------------------------------- dispatch

    private static void ExecuteGroup(Destination dest, List<string> items, ProcessResult result, Action<string>? log)
    {
        switch (dest.Action)
        {
            case DropAction.Compress:
                // One shared ZIP archive for the whole batch, entries appended sequentially.
                CompressItems(dest, items, result, log);
                break;

            case DropAction.Delete:
                DeleteItems(items, result, log);
                break;

            case DropAction.Extract:
                Parallel.For(0, items.Count, Options(), i =>
                {
                    string item = items[i];
                    try
                    {
                        ExtractOne(dest, item, result);
                        log?.Invoke($"extracted: {item}");
                    }
                    catch (SkipItemException)
                    {
                        lock (result) result.Skipped++;
                        log?.Invoke($"skipped: {item}");
                    }
                    catch (Exception ex)
                    {
                        RecordFailure(result, item, ex);
                        log?.Invoke($"failed: {item} ({ex.Message})");
                    }
                });
                break;

            default:
                Parallel.For(0, items.Count, Options(), i =>
                {
                    string item = items[i];
                    try
                    {
                        switch (dest.Action)
                        {
                            case DropAction.Move:
                                string movedTo = MoveOne(dest, item);
                                lock (result)
                                {
                                    result.Moved++;
                                    if (!result.Targets.Contains(movedTo)) result.Targets.Add(movedTo);
                                }
                                log?.Invoke($"moved: {item} -> {movedTo}");
                                break;

                            case DropAction.Copy:
                                string copiedTo = CopyOne(dest, item);
                                lock (result)
                                {
                                    result.Copied++;
                                    if (!result.Targets.Contains(copiedTo)) result.Targets.Add(copiedTo);
                                }
                                log?.Invoke($"copied: {item} -> {copiedTo}");
                                break;

                            case DropAction.Rename:
                                RenameOne(dest, item, i);
                                lock (result) result.Renamed++;
                                log?.Invoke($"renamed: {item}");
                                break;

                            case DropAction.Open:
                                OpenOne(item);
                                lock (result) result.Opened++;
                                log?.Invoke($"opened: {item}");
                                break;
                        }
                    }
                    catch (SkipItemException)
                    {
                        lock (result) result.Skipped++;
                        log?.Invoke($"skipped: {item}");
                    }
                    catch (Exception ex)
                    {
                        RecordFailure(result, item, ex);
                        log?.Invoke($"failed: {item} ({ex.Message})");
                    }
                });
                break;
        }
    }

    private static ParallelOptions Options() => new()
    {
        MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
    };

    private static void RecordFailure(ProcessResult result, string item, Exception ex)
    {
        lock (result)
        {
            result.Failed++;
            result.Errors.Add($"{Path.GetFileName(item)}: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- actions

    private static string MoveOne(Destination dest, string src)
    {
        string targetDir = RequiredTarget(dest);
        Directory.CreateDirectory(targetDir);
        string dst = ResolveConflict(Path.Combine(targetDir, Path.GetFileName(src)), dest.Conflict);

        if (Directory.Exists(src))
        {
            MoveDirectory(src, dst);
        }
        else
        {
            File.Move(src, dst, overwrite: false);
        }
        return targetDir;
    }

    private static string CopyOne(Destination dest, string src)
    {
        string targetDir = RequiredTarget(dest);
        Directory.CreateDirectory(targetDir);
        string dst = ResolveConflict(Path.Combine(targetDir, Path.GetFileName(src)), dest.Conflict);

        if (Directory.Exists(src))
        {
            CopyDirectoryCore(src, dst, dest.Conflict);
        }
        else
        {
            File.Copy(src, dst, overwrite: true);
        }
        return targetDir;
    }

    private static void RenameOne(Destination dest, string src, int index)
    {
        string template = dest.TargetPath.Trim();
        if (template.Length == 0)
        {
            template = "{name} (renamed){ext}";
        }

        string dir = Path.GetDirectoryName(Path.GetFullPath(src))!;
        string newName = TemplateResolver.Resolve(template, Path.GetFileNameWithoutExtension(src), Path.GetExtension(src), index);
        string dst = ResolveConflict(Path.Combine(dir, newName), dest.Conflict);

        if (Directory.Exists(src))
        {
            Directory.Move(src, dst);
        }
        else
        {
            File.Move(src, dst, overwrite: false);
        }
    }

    private static void OpenOne(string item)
    {
        Process.Start(new ProcessStartInfo(item) { UseShellExecute = true });
    }

    private static void ExtractOne(Destination dest, string item, ProcessResult result)
    {
        if (!File.Exists(item))
        {
            throw new SkipItemException("archive not found");
        }

        string rawTarget = dest.TargetPath.Trim();
        string baseDir = rawTarget.Length > 0
            ? Environment.ExpandEnvironmentVariables(rawTarget)
            : Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(item))!,
                Path.GetFileNameWithoutExtension(item));
        baseDir = ResolveConflict(baseDir, dest.Conflict);
        Directory.CreateDirectory(baseDir);

        string fullBase = Path.GetFullPath(baseDir);
        int extracted = 0;

        using (var archive = ZipFile.OpenRead(item))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string name = entry.FullName;
                bool isDirectory = name.EndsWith('/') || name.EndsWith('\\');
                string targetPath = Path.GetFullPath(Path.Combine(fullBase, name));

                // Zip-slip guard: entries must stay inside the destination.
                if (!targetPath.StartsWith(fullBase + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(targetPath, fullBase, StringComparison.OrdinalIgnoreCase))
                {
                    lock (result) result.Skipped++;
                    continue;
                }

                if (isDirectory)
                {
                    Directory.CreateDirectory(targetPath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                string finalPath = targetPath;
                if (File.Exists(finalPath))
                {
                    if (dest.Conflict == ConflictPolicy.Skip)
                    {
                        lock (result) result.Skipped++;
                        continue;
                    }
                    if (dest.Conflict == ConflictPolicy.AutoRename)
                    {
                        finalPath = ResolveConflict(finalPath, ConflictPolicy.AutoRename);
                    }
                }

                entry.ExtractToFile(finalPath, overwrite: true);
                extracted++;
            }
        }

        lock (result)
        {
            result.Extracted += extracted;
            if (!result.Targets.Contains(baseDir)) result.Targets.Add(baseDir);
        }
    }

    private static void CompressItems(Destination dest, List<string> items, ProcessResult result, Action<string>? log)
    {
        string rawTarget = dest.TargetPath.Trim();
        string targetDir = rawTarget.Length > 0
            ? Environment.ExpandEnvironmentVariables(rawTarget)
            : Path.GetDirectoryName(Path.GetFullPath(items[0]))!;
        Directory.CreateDirectory(targetDir);

        string zipName = dest.ZipName.Trim();
        if (zipName.Length == 0)
        {
            zipName = "Archive {date:yyyy-MM-dd}";
        }
        zipName = TemplateResolver.Resolve(zipName);
        if (!zipName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            zipName += ".zip";
        }
        string zipPath = Path.Combine(targetDir, zipName);

        // Existing archive is appended to, so repeated drops keep collecting into one ZIP.
        lock (result)
        {
            if (!result.Targets.Contains(targetDir)) result.Targets.Add(targetDir);
        }
        using var stream = new FileStream(zipPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Update);

        foreach (string item in items)
        {
            if (File.Exists(item))
            {
                AddFileToZip(zip, item, Path.GetFileName(item), result, log);
                lock (result) result.Compressed++;
            }
            else if (Directory.Exists(item))
            {
                string rootName = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(item)));
                foreach (string file in Directory.GetFiles(item, "*", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(item, file).Replace('\\', '/');
                    AddFileToZip(zip, file, rootName + "/" + relative, result, log);
                }
                lock (result) result.Compressed++;
            }
            else
            {
                lock (result) result.Skipped++;
            }
        }
    }

    private static void AddFileToZip(ZipArchive zip, string file, string entryName, ProcessResult result, Action<string>? log)
    {
        if (zip.GetEntry(entryName) is not null)
        {
            lock (result) result.Skipped++;
            log?.Invoke($"zip entry exists, skipped: {entryName}");
            return;
        }

        ZipArchiveEntry entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var output = entry.Open();
        input.CopyTo(output, 1024 * 1024);
    }

    private static void DeleteItems(List<string> items, ProcessResult result, Action<string>? log)
    {
        var existing = new List<string>();
        foreach (string item in items)
        {
            if (File.Exists(item) || Directory.Exists(item))
            {
                existing.Add(item);
            }
            else
            {
                lock (result) result.Skipped++;
            }
        }

        if (existing.Count == 0)
        {
            return;
        }

        if (!ShellDelete.ToRecycleBin(existing))
        {
            lock (result)
            {
                result.Failed += existing.Count;
                result.Errors.Add("recycle bin operation failed");
            }
            return;
        }

        lock (result) result.Deleted += existing.Count;
        foreach (string item in existing)
        {
            log?.Invoke($"recycled: {item}");
        }
    }

    // ---------------------------------------------------------------- helpers

    private static string RequiredTarget(Destination dest)
    {
        string raw = dest.TargetPath.Trim();
        if (raw.Length == 0)
        {
            throw new InvalidOperationException("target folder is not set");
        }
        return Environment.ExpandEnvironmentVariables(raw);
    }

    private static string ResolveConflict(string path, ConflictPolicy policy)
    {
        bool fileExists = File.Exists(path);
        bool dirExists = Directory.Exists(path);
        if (!fileExists && !dirExists)
        {
            return path;
        }

        switch (policy)
        {
            case ConflictPolicy.Overwrite:
                if (dirExists && !fileExists)
                {
                    Directory.Delete(path, recursive: true);
                }
                else
                {
                    File.Delete(path);
                }
                return path;

            case ConflictPolicy.Skip:
                throw new SkipItemException($"already exists: {path}");

            default: // AutoRename
                string dir = Path.GetDirectoryName(path)!;
                string name = Path.GetFileNameWithoutExtension(path);
                string ext = Path.GetExtension(path);
                for (int i = 2; ; i++)
                {
                    string candidate = Path.Combine(dir, $"{name} ({i}){ext}");
                    if (!File.Exists(candidate) && !Directory.Exists(candidate))
                    {
                        return candidate;
                    }
                }
        }
    }

    private static void MoveDirectory(string src, string dst)
    {
        try
        {
            Directory.Move(src, dst);
        }
        catch (IOException)
        {
            // Cross-volume move: copy in parallel, then remove the source.
            CopyDirectoryCore(src, dst, ConflictPolicy.Overwrite);
            Directory.Delete(src, recursive: true);
        }
    }

    private static void CopyDirectoryCore(string srcDir, string dstDir, ConflictPolicy policy)
    {
        Directory.CreateDirectory(dstDir);

        string[] files = Directory.GetFiles(srcDir, "*", SearchOption.TopDirectoryOnly);
        Parallel.For(0, files.Length, Options(), i =>
        {
            string dst = ResolveConflict(Path.Combine(dstDir, Path.GetFileName(files[i])), policy);
            File.Copy(files[i], dst, overwrite: true);
        });

        foreach (string sub in Directory.GetDirectories(srcDir, "*", SearchOption.TopDirectoryOnly))
        {
            CopyDirectoryCore(sub, Path.Combine(dstDir, Path.GetFileName(sub)), policy);
        }
    }
}

internal static class TemplateResolver
{
    private static readonly Regex TokenRegex =
        new(@"\{(name|ext|date:[^}]+|n)\}", RegexOptions.Compiled);

    /// <summary>Expands {name} {ext} {date:format} {n} tokens in user templates.</summary>
    public static string Resolve(string template, string? name = null, string? ext = null, int index = 0)
    {
        return TokenRegex.Replace(template, match =>
        {
            if (match.Value == "{name}") return name ?? string.Empty;
            if (match.Value == "{ext}") return ext ?? string.Empty;
            if (match.Value == "{n}") return index.ToString();
            try
            {
                return DateTime.Now.ToString(match.Value[6..^1]);
            }
            catch (FormatException)
            {
                return string.Empty;
            }
        });
    }
}
