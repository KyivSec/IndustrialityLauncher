using System.IO;

namespace LauncherApp;

public sealed class Paths
{
    private LauncherSettings Settings;

    public Paths(LauncherSettings Settings)
    {
        this.Settings = Settings;
    }

    public string RootDirectory => Settings.RootDirectory;
    public string RuntimeDirectory => Settings.RuntimeDirectory;
    public string GameDirectory => Settings.GameDirectory;
    public string JavaExtractDirectory => Settings.JavaExtractDirectory;
    public string JavaZipPath => Settings.JavaZipPath;
    public string SettingsFilePath => Path.Combine(Settings.RootDirectory, "launcher-settings.json");
    public string ModpackZipPath => Path.Combine(Settings.RootDirectory, "Industriality.NeoForge.zip");
    public string ModpackVersionFilePath => Path.Combine(Settings.RootDirectory, "modpack-version.txt");

    public void UpdateSettings(LauncherSettings Settings)
    {
        this.Settings = Settings;
    }

    public void EnsureDirectories()
    {
        Settings.Normalize();
        Directory.CreateDirectory(Settings.RootDirectory);
        Directory.CreateDirectory(Settings.RuntimeDirectory);
        Directory.CreateDirectory(Settings.GameDirectory);
    }
}
