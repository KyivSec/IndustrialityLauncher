namespace LauncherApp;

public sealed class UiSettings
{
    public string Username { get; set; } = string.Empty;
    public int MinRamMb { get; set; } = 512;
    public int MaxRamMb { get; set; } = 4096;
}
