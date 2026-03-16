using System;
using System.IO;

namespace LauncherApp;

public sealed class LauncherSettings
{
    public string MinecraftVersion { get; set; } = "1.21.1";
    public string NeoForgeVersion { get; set; } = "21.1.219";
    public string OracleJdkZipUrl { get; set; } = "https://download.oracle.com/java/21/archive/jdk-21.0.9_windows-x64_bin.zip";
    public string PlayerName { get; set; } = "Player";
    public int MinRamMb { get; set; } = 512;
    public int MaxRamMb { get; set; } = 4096;

    public string RootDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "IndustrialityLauncher");

    public string RuntimeDirectory { get; set; } = string.Empty;
    public string GameDirectory { get; set; } = string.Empty;
    public string JavaExtractDirectory { get; set; } = string.Empty;
    public string JavaZipPath { get; set; } = string.Empty;

    public static LauncherSettings CreateDefault()
    {
        LauncherSettings Settings = new();
        Settings.Normalize();
        return Settings;
    }

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(RootDirectory))
        {
            RootDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "IndustrialityLauncher");
        }

        MinRamMb = Math.Max(512, MinRamMb);
        MaxRamMb = Math.Max(MinRamMb, MaxRamMb);
        RuntimeDirectory = Path.Combine(RootDirectory, "runtime");
        GameDirectory = Path.Combine(RootDirectory, "minecraft");
        JavaExtractDirectory = Path.Combine(RuntimeDirectory, "jdk-21");
        JavaZipPath = Path.Combine(RuntimeDirectory, "jdk-21.zip");
    }

    public LauncherSettings Clone()
    {
        return new LauncherSettings
        {
            MinecraftVersion = MinecraftVersion,
            NeoForgeVersion = NeoForgeVersion,
            OracleJdkZipUrl = OracleJdkZipUrl,
            PlayerName = PlayerName,
            MinRamMb = MinRamMb,
            MaxRamMb = MaxRamMb,
            RootDirectory = RootDirectory,
            RuntimeDirectory = RuntimeDirectory,
            GameDirectory = GameDirectory,
            JavaExtractDirectory = JavaExtractDirectory,
            JavaZipPath = JavaZipPath
        };
    }
}
