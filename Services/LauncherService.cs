using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LauncherApp;

public sealed class LauncherService
{
    private LauncherSettings Settings;
    private readonly JavaService JavaService;
    private readonly InstallService InstallService;
    private readonly ModpackService ModpackService;
    private readonly PlayService PlayService;
    private readonly Paths Paths;
    private string? InstalledVersionId;

    public string RootDirectory => Paths.RootDirectory;
    public string RuntimeDirectory => Paths.RuntimeDirectory;
    public string GameDirectory => Paths.GameDirectory;
    public string SettingsFilePath => Paths.SettingsFilePath;
    public string ModpackZipPath => Paths.ModpackZipPath;
    public string ModpackVersionFilePath => Paths.ModpackVersionFilePath;

    public LauncherService()
        : this(LauncherSettings.CreateDefault())
    {
    }

    public LauncherService(LauncherSettings Settings)
    {
        this.Settings = Settings ?? throw new ArgumentNullException(nameof(Settings));
        this.Settings.Normalize();

        Paths = new Paths(this.Settings);
        JavaService = new JavaService(this.Settings, Paths);
        InstallService = new InstallService(this.Settings, Paths);
        ModpackService = new ModpackService(this.Settings, Paths);
        PlayService = new PlayService(this.Settings, Paths, JavaService);

        Paths.EnsureDirectories();
    }

    public LauncherSettings GetSettings()
    {
        return Settings.Clone();
    }

    public void UpdateSettings(Action<LauncherSettings> UpdateAction)
    {
        ArgumentNullException.ThrowIfNull(UpdateAction);

        var NewSettings = Settings.Clone();
        UpdateAction(NewSettings);
        NewSettings.Normalize();

        NewSettings.PlayerName = string.IsNullOrWhiteSpace(NewSettings.PlayerName) ? "Player" : NewSettings.PlayerName;
        NewSettings.MinRamMb = Math.Max(512, NewSettings.MinRamMb);
        NewSettings.MaxRamMb = Math.Max(NewSettings.MinRamMb, NewSettings.MaxRamMb);

        bool PathsChanged =
            !string.Equals(Settings.RootDirectory, NewSettings.RootDirectory, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Settings.RuntimeDirectory, NewSettings.RuntimeDirectory, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Settings.GameDirectory, NewSettings.GameDirectory, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Settings.JavaExtractDirectory, NewSettings.JavaExtractDirectory, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Settings.JavaZipPath, NewSettings.JavaZipPath, StringComparison.OrdinalIgnoreCase);

        Settings = NewSettings;
        Paths.UpdateSettings(NewSettings);
        JavaService.UpdateSettings(NewSettings);
        InstallService.UpdateSettings(NewSettings);
        ModpackService.UpdateSettings(NewSettings);
        PlayService.UpdateSettings(NewSettings);

        if (PathsChanged)
        {
            InstalledVersionId = null;
            JavaService.ResetCache();
        }

        Paths.EnsureDirectories();
    }

    public bool IsInstalled()
    {
        InstalledVersionId = VersionResolver.TryResolveInstalledVersionIdFromDisk(Settings, Paths);
        return !string.IsNullOrWhiteSpace(InstalledVersionId) && ModpackService.IsModpackContentInstalled();
    }

    public string? GetInstalledVersionId()
    {
        InstalledVersionId = VersionResolver.TryResolveInstalledVersionIdFromDisk(Settings, Paths);
        return InstalledVersionId;
    }

    public async Task<InstallResult> InstallAsync(IProgress<LauncherProgress>? Progress = null, CancellationToken CancellationToken = default)
    {
        Paths.EnsureDirectories();
        Shared.ReportProgress(Progress, "Preparing", "Creating launcher directories.", 2);

        string JavaPath = await JavaService.EnsureJavaAsync(Progress, CancellationToken).ConfigureAwait(false);
        Shared.PrepareJavaProcessEnvironment(JavaPath);

        await InstallService.InstallVanillaAndNeoForgeAsync(JavaPath, Progress, CancellationToken).ConfigureAwait(false);
        await ModpackService.DownloadAndInstallModpackAsync(Progress, CancellationToken).ConfigureAwait(false);

        Shared.ReportProgress(Progress, "Verifying", "Checking installed files.", 99);
        string VersionId = InstallService.VerifyInstalledVersion();
        InstalledVersionId = VersionId;

        Shared.ReportProgress(Progress, "Done", "Installation complete.", 100);

        return new InstallResult
        {
            RootDirectory = Paths.RootDirectory,
            GameDirectory = Paths.GameDirectory,
            JavaPath = JavaPath,
            VersionId = VersionId
        };
    }

    public async Task<bool> UpdateModpackAsync(IProgress<LauncherProgress>? Progress = null, CancellationToken CancellationToken = default)
    {
        Paths.EnsureDirectories();

        ModpackUpdateInfo UpdateInfo = await ModpackService.GetModpackUpdateInfoAsync(CancellationToken).ConfigureAwait(false);
        if (!UpdateInfo.UpdateAvailable)
        {
            Shared.ReportProgress(Progress, "Update", "Modpack is already up to date.", 100);
            return false;
        }

        await ModpackService.DownloadAndInstallModpackAsync(Progress, CancellationToken).ConfigureAwait(false);
        Shared.ReportProgress(Progress, "Done", "Update complete.", 100);
        return true;
    }

    public async Task PlayAsync(CancellationToken CancellationToken = default)
    {
        Paths.EnsureDirectories();

        string? VersionId = InstalledVersionId;
        if (string.IsNullOrWhiteSpace(VersionId))
        {
            VersionId = VersionResolver.TryResolveInstalledVersionIdFromDisk(Settings, Paths);
            if (string.IsNullOrWhiteSpace(VersionId))
            {
                throw new InvalidOperationException("NeoForge is not installed. Call InstallAsync() first.");
            }

            InstalledVersionId = VersionId;
        }

        await PlayService.PlayAsync(VersionId, CancellationToken).ConfigureAwait(false);
    }

    public void OpenRootFolder()
    {
        Paths.EnsureDirectories();
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{Paths.RootDirectory}\"",
            UseShellExecute = true
        });
    }

    public void DeleteModpack()
    {
        if (Directory.Exists(Paths.GameDirectory))
        {
            Directory.Delete(Paths.GameDirectory, true);
        }

        Directory.CreateDirectory(Paths.GameDirectory);

        if (File.Exists(Paths.ModpackVersionFilePath))
        {
            File.Delete(Paths.ModpackVersionFilePath);
        }

        if (File.Exists(Paths.ModpackZipPath))
        {
            File.Delete(Paths.ModpackZipPath);
        }

        InstalledVersionId = null;
    }

    public Task<ModpackUpdateInfo> GetModpackUpdateInfoAsync(CancellationToken CancellationToken = default)
    {
        return ModpackService.GetModpackUpdateInfoAsync(CancellationToken);
    }
}
