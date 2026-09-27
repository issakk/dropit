namespace DropLite.Models;

internal enum DropAction
{
    Move,
    Copy,
    Delete,
    Compress,
    Extract,
    Rename,
    Open,
    Ignore,
}

internal enum ConflictPolicy
{
    AutoRename,
    Overwrite,
    Skip,
}

/// <summary>A single rule: file mask + action + target. Order inside a profile matters.</summary>
internal sealed class Destination
{
    public string Name { get; set; } = "New rule";

    public DropAction Action { get; set; } = DropAction.Move;

    /// <summary>Semicolon-separated wildcards, e.g. "*.jpg;*.png". Empty matches everything.</summary>
    public string Pattern { get; set; } = "";

    /// <summary>Target folder (or rename template when Action == Rename). Environment variables are expanded.</summary>
    public string TargetPath { get; set; } = "";

    /// <summary>ZIP file name template used when Action == Compress. Supports {date:...} and {n}.</summary>
    public string ZipName { get; set; } = "";

    public ConflictPolicy Conflict { get; set; } = ConflictPolicy.AutoRename;
}

/// <summary>A profile owns one floating icon and its list of destinations.</summary>
internal sealed class Profile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "Profile";

    public int IconColorArgb { get; set; } = unchecked((int)0xFF4A90D9);

    public int X { get; set; } = 120;

    public int Y { get; set; } = 120;

    public bool ShowIcon { get; set; } = true;

    public List<Destination> Destinations { get; set; } = new();
}

internal sealed class AppConfig
{
    public List<Profile> Profiles { get; set; } = new();

    public bool StartWithWindows { get; set; }

    public bool ShowNotifications { get; set; } = true;

    public int IconSize { get; set; } = 56;
}
