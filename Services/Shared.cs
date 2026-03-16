using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;

namespace LauncherApp;

public static class Shared
{
    public static void ReportProgress(IProgress<LauncherProgress>? Progress, string Stage, string Message, double Percent)
    {
        double ClampedPercent = Math.Clamp(Percent, 0, 100);
        Progress?.Report(new LauncherProgress(Stage, Message, ClampedPercent));
    }

    public static HttpClient CreateGitHubHttpClient()
    {
        HttpClient Client = new();
        Client.DefaultRequestHeaders.UserAgent.ParseAdd("IndustrialityLauncher/1.0");
        Client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return Client;
    }

    public static string? FindJavaExecutable(string Root)
    {
        if (!Directory.Exists(Root))
        {
            return null;
        }

        return Directory.EnumerateFiles(Root, "java.exe", SearchOption.AllDirectories).FirstOrDefault();
    }

    public static void ValidateJavaLayout(string JavaPath)
    {
        if (string.IsNullOrWhiteSpace(JavaPath))
        {
            throw new InvalidOperationException("Java path is empty.");
        }

        if (!File.Exists(JavaPath))
        {
            throw new FileNotFoundException("java.exe was not found.", JavaPath);
        }

        string? BinDirectory = Path.GetDirectoryName(JavaPath);
        if (string.IsNullOrWhiteSpace(BinDirectory) || !Directory.Exists(BinDirectory))
        {
            throw new InvalidOperationException("Java bin directory was not found.");
        }

        string? JavaHomeDirectory = Directory.GetParent(BinDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(JavaHomeDirectory) || !Directory.Exists(JavaHomeDirectory))
        {
            throw new InvalidOperationException("JAVA_HOME directory was not found.");
        }

        string JavawPath = Path.Combine(BinDirectory, "javaw.exe");
        if (!File.Exists(JavawPath))
        {
            throw new FileNotFoundException("javaw.exe was not found.", JavawPath);
        }

        string ModulesPath = Path.Combine(JavaHomeDirectory, "lib", "modules");
        if (!File.Exists(ModulesPath))
        {
            throw new FileNotFoundException("Java runtime modules file was not found.", ModulesPath);
        }
    }

    public static void PrepareJavaProcessEnvironment(string JavaPath)
    {
        if (string.IsNullOrWhiteSpace(JavaPath) || !File.Exists(JavaPath))
        {
            throw new FileNotFoundException("Java executable was not found.", JavaPath);
        }

        string BinDirectory = Path.GetDirectoryName(JavaPath)
            ?? throw new InvalidOperationException("Could not resolve Java bin directory.");

        string JavaHomeDirectory = Directory.GetParent(BinDirectory)?.FullName
            ?? throw new InvalidOperationException("Could not resolve JAVA_HOME.");

        string CurrentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string[] PathParts = CurrentPath.Split(';', StringSplitOptions.RemoveEmptyEntries);

        if (!PathParts.Any(Value => string.Equals(Value.Trim(), BinDirectory, StringComparison.OrdinalIgnoreCase)))
        {
            string NewPath = string.IsNullOrWhiteSpace(CurrentPath)
                ? BinDirectory
                : BinDirectory + ";" + CurrentPath;

            Environment.SetEnvironmentVariable("PATH", NewPath, EnvironmentVariableTarget.Process);
        }

        Environment.SetEnvironmentVariable("JAVA_HOME", JavaHomeDirectory, EnvironmentVariableTarget.Process);
    }

    public static void SetPropertyIfExists(Type TargetType, object Target, string PropertyName, object? Value)
    {
        if (Value is null)
        {
            return;
        }

        PropertyInfo? Property = TargetType.GetProperty(PropertyName, BindingFlags.Public | BindingFlags.Instance);
        if (Property is null || !Property.CanWrite)
        {
            return;
        }

        if (Property.PropertyType.IsAssignableFrom(Value.GetType()))
        {
            Property.SetValue(Target, Value);
        }
    }

    public static void ApplyJvmMemoryArgumentsIfPossible(object LaunchOption, int MinRamMb, int MaxRamMb)
    {
        Type LaunchOptionType = LaunchOption.GetType();
        string[] Arguments =
        {
            $"-Xms{MinRamMb}m",
            $"-Xmx{MaxRamMb}m"
        };

        PropertyInfo? Property = LaunchOptionType.GetProperty("JvmArguments", BindingFlags.Public | BindingFlags.Instance)
            ?? LaunchOptionType.GetProperty("GameJvmArguments", BindingFlags.Public | BindingFlags.Instance)
            ?? LaunchOptionType.GetProperty("AdditionalJvmArguments", BindingFlags.Public | BindingFlags.Instance);

        if (Property is null || !Property.CanWrite)
        {
            return;
        }

        if (Property.PropertyType == typeof(string[]))
        {
            string[] Existing = Property.GetValue(LaunchOption) as string[] ?? Array.Empty<string>();
            Property.SetValue(LaunchOption, Existing.Concat(Arguments).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            return;
        }

        if (typeof(IList<string>).IsAssignableFrom(Property.PropertyType) && Property.GetValue(LaunchOption) is IList<string> List)
        {
            foreach (string Argument in Arguments)
            {
                if (!List.Contains(Argument, StringComparer.OrdinalIgnoreCase))
                {
                    List.Add(Argument);
                }
            }
        }
    }
}
