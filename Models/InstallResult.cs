namespace LauncherApp;

public sealed class InstallResult
{
    public string RootDirectory { get; init; } = string.Empty;
    public string GameDirectory { get; init; } = string.Empty;
    public string JavaPath { get; init; } = string.Empty;
    public string VersionId { get; init; } = string.Empty;
}
