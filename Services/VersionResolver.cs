using System.IO;

namespace LauncherApp;

public static class VersionResolver
{
    public static string? TryResolveInstalledVersionIdFromDisk(LauncherSettings Settings, Paths Paths)
    {
        string VersionsDirectory = Path.Combine(Paths.GameDirectory, "versions");
        if (!Directory.Exists(VersionsDirectory))
        {
            return null;
        }

        string[] Candidates =
        {
            $"neoforge-{Settings.NeoForgeVersion}",
            $"{Settings.MinecraftVersion}-neoforge-{Settings.NeoForgeVersion}",
            Settings.NeoForgeVersion
        };

        foreach (string VersionId in Candidates)
        {
            string VersionDirectory = Path.Combine(VersionsDirectory, VersionId);
            string VersionJsonPath = Path.Combine(VersionDirectory, VersionId + ".json");

            if (Directory.Exists(VersionDirectory) && File.Exists(VersionJsonPath))
            {
                return VersionId;
            }
        }

        return null;
    }
}
