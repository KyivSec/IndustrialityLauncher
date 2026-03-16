using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace LauncherApp;

public sealed class PlayService
{
    private LauncherSettings Settings;
    private readonly Paths Paths;
    private readonly JavaService JavaService;

    public PlayService(LauncherSettings Settings, Paths Paths, JavaService JavaService)
    {
        this.Settings = Settings;
        this.Paths = Paths;
        this.JavaService = JavaService;
    }

    public void UpdateSettings(LauncherSettings Settings)
    {
        this.Settings = Settings;
    }

    public async Task PlayAsync(string VersionId, CancellationToken CancellationToken)
    {
        string JavaPath = await JavaService.GetJavaPathAsync(CancellationToken).ConfigureAwait(false);
        Shared.PrepareJavaProcessEnvironment(JavaPath);

        var MinecraftPath = new MinecraftPath(Paths.GameDirectory);
        var Launcher = new MinecraftLauncher(MinecraftPath);

        var LaunchOption = new MLaunchOption
        {
            Session = MSession.CreateOfflineSession(Settings.PlayerName)
        };

        Shared.SetPropertyIfExists(typeof(MLaunchOption), LaunchOption, "JavaPath", JavaPath);
        Shared.SetPropertyIfExists(typeof(MLaunchOption), LaunchOption, "MinimumRamMb", Settings.MinRamMb);
        Shared.SetPropertyIfExists(typeof(MLaunchOption), LaunchOption, "MaximumRamMb", Settings.MaxRamMb);
        Shared.ApplyJvmMemoryArgumentsIfPossible(LaunchOption, Settings.MinRamMb, Settings.MaxRamMb);

        object ProcessObject = await Launcher.BuildProcessAsync(VersionId, LaunchOption).ConfigureAwait(false)
            ?? throw new InvalidOperationException("CmlLib returned null from BuildProcessAsync.");

        ProcessStartInfo StartInfo = ExtractStartInfo(ProcessObject, JavaPath);

        var MinecraftProcess = new Process
        {
            StartInfo = StartInfo,
            EnableRaisingEvents = true
        };

        if (!MinecraftProcess.Start())
        {
            throw new InvalidOperationException("Minecraft process failed to start.");
        }
    }

    private ProcessStartInfo ExtractStartInfo(object ProcessObject, string JavaPath)
    {
        PropertyInfo? StartInfoProperty = ProcessObject.GetType().GetProperty("StartInfo", BindingFlags.Public | BindingFlags.Instance);
        if (StartInfoProperty?.GetValue(ProcessObject) is not ProcessStartInfo StartInfo)
        {
            throw new InvalidOperationException("Could not extract ProcessStartInfo from built Minecraft process.");
        }

        StartInfo.FileName = JavaPath;
        StartInfo.UseShellExecute = false;
        StartInfo.CreateNoWindow = true;
        StartInfo.RedirectStandardOutput = false;
        StartInfo.RedirectStandardError = false;

        string JavaBinDirectory = Path.GetDirectoryName(JavaPath)
            ?? throw new InvalidOperationException("Could not resolve Java bin directory.");

        StartInfo.WorkingDirectory = string.IsNullOrWhiteSpace(StartInfo.WorkingDirectory)
            ? Paths.GameDirectory
            : StartInfo.WorkingDirectory;

        StartInfo.Environment["JAVA_HOME"] = Directory.GetParent(JavaBinDirectory)?.FullName ?? string.Empty;

        string ExistingPath = StartInfo.Environment.TryGetValue("PATH", out string? PathValue)
            ? PathValue ?? string.Empty
            : string.Empty;

        if (!ExistingPath.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Any(Value => string.Equals(Value.Trim(), JavaBinDirectory, StringComparison.OrdinalIgnoreCase)))
        {
            StartInfo.Environment["PATH"] = string.IsNullOrWhiteSpace(ExistingPath)
                ? JavaBinDirectory
                : JavaBinDirectory + ";" + ExistingPath;
        }

        return StartInfo;
    }
}
