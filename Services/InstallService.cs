using CmlLib.Core;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace LauncherApp;

public sealed class InstallService
{
    private LauncherSettings Settings;
    private readonly Paths Paths;

    public InstallService(LauncherSettings Settings, Paths Paths)
    {
        this.Settings = Settings;
        this.Paths = Paths;
    }

    public void UpdateSettings(LauncherSettings Settings)
    {
        this.Settings = Settings;
    }

    public async Task InstallVanillaAndNeoForgeAsync(string JavaPath, IProgress<LauncherProgress>? Progress, CancellationToken CancellationToken)
    {
        var MinecraftPath = new MinecraftPath(Paths.GameDirectory);
        var Launcher = new MinecraftLauncher(MinecraftPath);

        Shared.ReportProgress(Progress, "Vanilla", "Installing vanilla Minecraft.", 60);
        await Launcher.InstallAsync(Settings.MinecraftVersion).ConfigureAwait(false);

        Shared.ReportProgress(Progress, "NeoForge", "Installing NeoForge.", 82);
        await InstallNeoForgeAsync(
            MinecraftPath,
            Launcher,
            JavaPath,
            Settings.MinecraftVersion,
            Settings.NeoForgeVersion,
            CancellationToken).ConfigureAwait(false);
    }

    public string VerifyInstalledVersion()
    {
        string? VersionId = VersionResolver.TryResolveInstalledVersionIdFromDisk(Settings, Paths);
        if (!string.IsNullOrWhiteSpace(VersionId))
        {
            return VersionId;
        }

        throw new DirectoryNotFoundException(
            "NeoForge installer returned, but expected version files were not found in: " +
            Path.Combine(Paths.GameDirectory, "versions"));
    }

    private static async Task<object?> InstallNeoForgeAsync(
        MinecraftPath MinecraftPath,
        MinecraftLauncher Launcher,
        string JavaPath,
        string MinecraftVersion,
        string NeoForgeVersion,
        CancellationToken CancellationToken)
    {
        Type InstallerType = Type.GetType(
            "CmlLib.Core.Installer.NeoForge.NeoForgeInstaller, CmlLib.Core.Installer.NeoForge",
            throwOnError: false)
            ?? throw new InvalidOperationException(
                "NeoForge installer type was not found. Make sure CmlLib.Core.Installer.NeoForge is installed.");

        Type? OptionsType = Type.GetType(
            "CmlLib.Core.Installer.NeoForge.NeoForgeInstallOptions, CmlLib.Core.Installer.NeoForge",
            throwOnError: false);

        object? Options = null;
        if (OptionsType is not null)
        {
            Options = Activator.CreateInstance(OptionsType);
            if (Options is not null)
            {
                Shared.SetPropertyIfExists(OptionsType, Options, "JavaPath", JavaPath);
                Shared.SetPropertyIfExists(OptionsType, Options, "JavaExecutablePath", JavaPath);
                Shared.SetPropertyIfExists(OptionsType, Options, "MinecraftPath", MinecraftPath);
                Shared.SetPropertyIfExists(OptionsType, Options, "Launcher", Launcher);
                Shared.SetPropertyIfExists(OptionsType, Options, "MinecraftLauncher", Launcher);
            }
        }

        object? InstallerInstance = null;
        foreach (ConstructorInfo Constructor in InstallerType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
        {
            ParameterInfo[] Parameters = Constructor.GetParameters();
            object?[]? Arguments = Parameters.Length switch
            {
                0 => [],
                1 when Parameters[0].ParameterType.IsInstanceOfType(MinecraftPath) => [MinecraftPath],
                1 when Parameters[0].ParameterType.IsInstanceOfType(Launcher) => [Launcher],
                1 when Parameters[0].ParameterType == typeof(string) => [JavaPath],
                2 when Parameters[0].ParameterType.IsInstanceOfType(MinecraftPath) && Parameters[1].ParameterType == typeof(string) => [MinecraftPath, JavaPath],
                2 when Parameters[0].ParameterType.IsInstanceOfType(Launcher) && Parameters[1].ParameterType == typeof(string) => [Launcher, JavaPath],
                _ => null
            };

            if (Arguments is not null)
            {
                InstallerInstance = Constructor.Invoke(Arguments);
                break;
            }
        }

        MethodInfo[] Methods = InstallerType
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .Where(Method => Method.Name is "Install" or "InstallAsync")
            .OrderByDescending(Method => Method.GetParameters().Length)
            .ToArray();

        foreach (MethodInfo Method in Methods)
        {
            CancellationToken.ThrowIfCancellationRequested();

            object?[]? Arguments = BuildNeoForgeArguments(Method.GetParameters(), MinecraftVersion, NeoForgeVersion, Options);
            if (Arguments is null)
            {
                continue;
            }

            object? Target = Method.IsStatic ? null : InstallerInstance;
            if (!Method.IsStatic && Target is null)
            {
                continue;
            }

            object? InvocationResult = Method.Invoke(Target, Arguments);
            if (InvocationResult is Task Task)
            {
                await Task.WaitAsync(CancellationToken).ConfigureAwait(false);
                return Task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance)?.GetValue(Task);
            }

            return InvocationResult;
        }

        throw new InvalidOperationException("Could not find a compatible NeoForge install method.");
    }

    private static object?[]? BuildNeoForgeArguments(
        ParameterInfo[] Parameters,
        string MinecraftVersion,
        string NeoForgeVersion,
        object? Options)
    {
        if (Parameters.Length < 2 || Parameters[0].ParameterType != typeof(string) || Parameters[1].ParameterType != typeof(string))
        {
            return null;
        }

        object?[] Arguments = new object?[Parameters.Length];
        bool UsedOptions = false;

        for (int Index = 0; Index < Parameters.Length; Index++)
        {
            ParameterInfo Parameter = Parameters[Index];

            if (Parameter.ParameterType == typeof(string))
            {
                Arguments[Index] = Index == 0 ? MinecraftVersion : Index == 1 ? NeoForgeVersion : null;
                if (Arguments[Index] is null)
                {
                    return null;
                }

                continue;
            }

            if (!UsedOptions && Options is not null && Parameter.ParameterType.IsInstanceOfType(Options))
            {
                Arguments[Index] = Options;
                UsedOptions = true;
                continue;
            }

            if (Parameter.HasDefaultValue)
            {
                Arguments[Index] = Parameter.DefaultValue;
                continue;
            }

            return null;
        }

        return Arguments;
    }
}
