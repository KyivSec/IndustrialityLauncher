using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;

internal static class Program
{
    [STAThread]
    private static int Main(string[] Args)
    {
        var InstallRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IndustrialityLauncher");

        var LogPath = Path.Combine(InstallRoot, "Bootstrapper.log");

        try
        {
            var BootstrapperPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(BootstrapperPath))
            {
                Log(LogPath, "Environment.ProcessPath returned null or empty.");
                return 1;
            }

            var AppRoot = Path.Combine(InstallRoot, "App");
            var AppExePath = Path.Combine(AppRoot, "LauncherApp.exe");
            var MarkerPath = Path.Combine(InstallRoot, "LauncherApp.version");
            var DesktopShortcutPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "Industriality Launcher.lnk");

            Directory.CreateDirectory(InstallRoot);

            var CurrentVersion = GetPayloadVersion(LogPath);
            var InstalledVersion = File.Exists(MarkerPath) ? File.ReadAllText(MarkerPath) : string.Empty;

            var NeedsExtract =
                !File.Exists(AppExePath) ||
                !string.Equals(CurrentVersion, InstalledVersion, StringComparison.Ordinal);

            if (NeedsExtract)
            {
                if (Directory.Exists(AppRoot))
                {
                    Directory.Delete(AppRoot, true);
                }

                Directory.CreateDirectory(AppRoot);

                using var PayloadStream = GetPayloadStream(LogPath);
                if (PayloadStream is null)
                {
                    Log(LogPath, "Could not find embedded LauncherApp.zip resource.");
                    return 2;
                }

                using var Archive = new ZipArchive(PayloadStream, ZipArchiveMode.Read);
                Archive.ExtractToDirectory(AppRoot, true);

                File.WriteAllText(MarkerPath, CurrentVersion);
                Log(LogPath, $"Payload extracted to {AppRoot}");
            }

            CreateDesktopShortcutIfMissing(DesktopShortcutPath, BootstrapperPath, AppExePath, LogPath);

            if (!File.Exists(AppExePath))
            {
                Log(LogPath, $"LauncherApp.exe was not found after extraction. Expected path: {AppExePath}");
                return 3;
            }

            var StartInfo = new ProcessStartInfo
            {
                FileName = AppExePath,
                WorkingDirectory = AppRoot,
                UseShellExecute = true,
                Arguments = BuildArguments(Args)
            };

            Process.Start(StartInfo);
            return 0;
        }
        catch (Exception Ex)
        {
            Log(LogPath, Ex.ToString());
            return 10;
        }
    }

    private static Stream? GetPayloadStream(string LogPath)
    {
        var AssemblyExec = Assembly.GetExecutingAssembly();
        var ResourceName = AssemblyExec
            .GetManifestResourceNames()
            .FirstOrDefault(Name => Name.EndsWith("LauncherApp.zip", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(ResourceName))
        {
            Log(
                LogPath,
                "Embedded zip was not found. Resources: " +
                string.Join(", ", AssemblyExec.GetManifestResourceNames()));

            return null;
        }

        return AssemblyExec.GetManifestResourceStream(ResourceName);
    }

    private static string GetPayloadVersion(string LogPath)
    {
        using var PayloadStream = GetPayloadStream(LogPath);
        if (PayloadStream is null)
        {
            return string.Empty;
        }

        using var Memory = new MemoryStream();
        PayloadStream.CopyTo(Memory);
        var Bytes = Memory.ToArray();
        return Convert.ToHexString(SHA256.HashData(Bytes));
    }

    private static string BuildArguments(string[] Args)
    {
        if (Args.Length == 0)
        {
            return string.Empty;
        }

        return string.Join(" ", Args.Select(QuoteArgument));
    }

    private static string QuoteArgument(string Value)
    {
        if (string.IsNullOrEmpty(Value))
        {
            return "\"\"";
        }

        if (!Value.Contains(' ') && !Value.Contains('"'))
        {
            return Value;
        }

        return "\"" + Value.Replace("\"", "\\\"") + "\"";
    }

    private static void CreateDesktopShortcutIfMissing(string ShortcutPath, string TargetPath, string IconPath, string LogPath)
    {
        try
        {
            if (File.Exists(ShortcutPath))
            {
                return;
            }

            var WorkingDirectory = Path.GetDirectoryName(TargetPath) ?? string.Empty;

            var Script = string.Join(
                Environment.NewLine,
                "$Shell = New-Object -ComObject WScript.Shell",
                $"$Shortcut = $Shell.CreateShortcut('{EscapePowerShellString(ShortcutPath)}')",
                $"$Shortcut.TargetPath = '{EscapePowerShellString(TargetPath)}'",
                $"$Shortcut.WorkingDirectory = '{EscapePowerShellString(WorkingDirectory)}'",
                $"$Shortcut.IconLocation = '{EscapePowerShellString(IconPath)},0'",
                "$Shortcut.Save()");

            var StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command \"{EscapeForPowerShellCommand(Script)}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var ShortcutProcess = Process.Start(StartInfo);
            ShortcutProcess?.WaitForExit(5000);
        }
        catch (Exception Ex)
        {
            Log(LogPath, "Failed to create desktop shortcut: " + Ex);
        }
    }

    private static string EscapePowerShellString(string Value)
    {
        return Value.Replace("'", "''");
    }

    private static string EscapeForPowerShellCommand(string Value)
    {
        return Value.Replace("\"", "`\"");
    }

    private static void Log(string LogPath, string Message)
    {
        try
        {
            var DirectoryPath = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrWhiteSpace(DirectoryPath))
            {
                Directory.CreateDirectory(DirectoryPath);
            }

            File.AppendAllText(
                LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {Message}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}