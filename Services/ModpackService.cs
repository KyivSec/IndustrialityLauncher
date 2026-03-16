using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace LauncherApp;

public sealed class ModpackService
{
    private const string ModpackDownloadUrl = "https://github.com/KyivSec/IndustrialityProject/releases/latest/download/Industriality.NeoForge.zip";
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/KyivSec/IndustrialityProject/releases/latest";

    private LauncherSettings Settings;
    private readonly Paths Paths;
    private int LastReportedModpackPercent = -1;

    public ModpackService(LauncherSettings Settings, Paths Paths)
    {
        this.Settings = Settings;
        this.Paths = Paths;
    }

    public void UpdateSettings(LauncherSettings Settings)
    {
        this.Settings = Settings;
    }

    public async Task<ModpackUpdateInfo> GetModpackUpdateInfoAsync(CancellationToken CancellationToken = default)
    {
        string LatestVersion = await GetLatestModpackVersionAsync(CancellationToken).ConfigureAwait(false);
        string CurrentVersion = GetInstalledModpackVersion();
        bool IsInstalledNow = IsModpackContentInstalled();
        bool UpdateAvailable = !IsInstalledNow || !string.Equals(CurrentVersion, LatestVersion, StringComparison.OrdinalIgnoreCase);

        return new ModpackUpdateInfo(IsInstalledNow, CurrentVersion, LatestVersion, UpdateAvailable);
    }

    public async Task DownloadAndInstallModpackAsync(IProgress<LauncherProgress>? Progress = null, CancellationToken CancellationToken = default)
    {
        Paths.EnsureDirectories();

        if (File.Exists(Paths.ModpackZipPath))
        {
            File.Delete(Paths.ModpackZipPath);
        }

        Shared.ReportProgress(Progress, "Modpack", "Downloading modpack.", 84);

        using HttpClient Client = Shared.CreateGitHubHttpClient();
        using HttpResponseMessage Response = await Client.GetAsync(
            ModpackDownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            CancellationToken).ConfigureAwait(false);

        Response.EnsureSuccessStatusCode();

        long? TotalLength = Response.Content.Headers.ContentLength;
        LastReportedModpackPercent = -1;

        await using (Stream Input = await Response.Content.ReadAsStreamAsync(CancellationToken).ConfigureAwait(false))
        await using (FileStream Output = File.Create(Paths.ModpackZipPath))
        {
            byte[] Buffer = new byte[1024 * 256];
            long TotalRead = 0;
            int Read;

            while ((Read = await Input.ReadAsync(Buffer.AsMemory(0, Buffer.Length), CancellationToken).ConfigureAwait(false)) > 0)
            {
                await Output.WriteAsync(Buffer.AsMemory(0, Read), CancellationToken).ConfigureAwait(false);
                TotalRead += Read;

                if (TotalLength.HasValue && TotalLength.Value > 0)
                {
                    double Ratio = (double)TotalRead / TotalLength.Value;
                    int Percent = (int)Math.Clamp(Math.Floor(84d + (Ratio * 8d)), 84, 92);

                    if (Percent != LastReportedModpackPercent)
                    {
                        LastReportedModpackPercent = Percent;
                        Shared.ReportProgress(Progress, "Modpack", "Downloading modpack.", Percent);
                    }
                }
            }
        }

        Shared.ReportProgress(Progress, "Modpack", "Extracting modpack.", 93);
        ExtractMinecraftFolderOnly(Paths.ModpackZipPath, Paths.GameDirectory);

        string InstalledVersion;
        try
        {
            InstalledVersion = await GetLatestModpackVersionAsync(CancellationToken).ConfigureAwait(false);
        }
        catch
        {
            InstalledVersion = "unknown";
        }

        File.WriteAllText(Paths.ModpackVersionFilePath, InstalledVersion);
        Shared.ReportProgress(Progress, "Modpack", "Modpack installed.", 98);
    }

    public bool IsModpackContentInstalled()
    {
        return Directory.Exists(Path.Combine(Paths.GameDirectory, "mods")) ||
               Directory.Exists(Path.Combine(Paths.GameDirectory, "config")) ||
               Directory.Exists(Path.Combine(Paths.GameDirectory, "kubejs")) ||
               Directory.Exists(Path.Combine(Paths.GameDirectory, "resourcepacks")) ||
               File.Exists(Path.Combine(Paths.GameDirectory, "mmc-pack.json"));
    }

    private static void ExtractMinecraftFolderOnly(string ZipPath, string GameDirectory)
    {
        using ZipArchive Archive = ZipFile.OpenRead(ZipPath);

        foreach (ZipArchiveEntry Entry in Archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(Entry.FullName))
            {
                continue;
            }

            string NormalizedEntry = Entry.FullName.Replace('\\', '/').TrimStart('/');
            if (!NormalizedEntry.StartsWith("minecraft/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string RelativeTargetPath = NormalizedEntry["minecraft/".Length..];
            if (string.IsNullOrWhiteSpace(RelativeTargetPath))
            {
                continue;
            }

            string DestinationPath = Path.GetFullPath(Path.Combine(
                GameDirectory,
                RelativeTargetPath.Replace('/', Path.DirectorySeparatorChar)));

            string RootPath = Path.GetFullPath(GameDirectory + Path.DirectorySeparatorChar);
            if (!DestinationPath.StartsWith(RootPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Zip entry attempted to escape the game directory: " + Entry.FullName);
            }

            if (Entry.FullName.EndsWith("/", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(DestinationPath);
                continue;
            }

            string? DestinationDirectory = Path.GetDirectoryName(DestinationPath);
            if (!string.IsNullOrWhiteSpace(DestinationDirectory))
            {
                Directory.CreateDirectory(DestinationDirectory);
            }

            Entry.ExtractToFile(DestinationPath, true);
        }
    }

    private async Task<string> GetLatestModpackVersionAsync(CancellationToken CancellationToken)
    {
        using HttpClient Client = Shared.CreateGitHubHttpClient();
        using HttpResponseMessage Response = await Client.GetAsync(LatestReleaseApiUrl, CancellationToken).ConfigureAwait(false);
        Response.EnsureSuccessStatusCode();

        string Json = await Response.Content.ReadAsStringAsync(CancellationToken).ConfigureAwait(false);

        GitHubReleaseInfo? ReleaseInfo = JsonSerializer.Deserialize<GitHubReleaseInfo>(Json);
        if (ReleaseInfo is null || string.IsNullOrWhiteSpace(ReleaseInfo.TagName))
        {
            throw new InvalidOperationException("Could not resolve latest modpack version from GitHub.");
        }

        return ReleaseInfo.TagName.Trim();
    }

    private string GetInstalledModpackVersion()
    {
        if (!File.Exists(Paths.ModpackVersionFilePath))
        {
            return string.Empty;
        }

        return File.ReadAllText(Paths.ModpackVersionFilePath).Trim();
    }

    private sealed class GitHubReleaseInfo
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;
    }
}
