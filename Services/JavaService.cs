using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LauncherApp;

public sealed class JavaService
{
    private LauncherSettings Settings;
    private readonly Paths Paths;
    private string? CachedJavaPath;

    public JavaService(LauncherSettings Settings, Paths Paths)
    {
        this.Settings = Settings;
        this.Paths = Paths;
    }

    public void UpdateSettings(LauncherSettings Settings)
    {
        this.Settings = Settings;
    }

    public void ResetCache()
    {
        CachedJavaPath = null;
    }

    public async Task<string> GetJavaPathAsync(CancellationToken CancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(CachedJavaPath) && File.Exists(CachedJavaPath))
        {
            return CachedJavaPath;
        }

        string? JavaPath = Shared.FindJavaExecutable(Paths.JavaExtractDirectory);
        if (string.IsNullOrWhiteSpace(JavaPath))
        {
            JavaPath = await EnsureJavaAsync(null, CancellationToken).ConfigureAwait(false);
        }

        Shared.ValidateJavaLayout(JavaPath);
        CachedJavaPath = JavaPath;
        return JavaPath;
    }

    public async Task<string> EnsureJavaAsync(IProgress<LauncherProgress>? Progress, CancellationToken CancellationToken)
    {
        string? ExistingJava = Shared.FindJavaExecutable(Paths.JavaExtractDirectory);
        if (!string.IsNullOrWhiteSpace(ExistingJava))
        {
            Shared.ValidateJavaLayout(ExistingJava);
            Shared.ReportProgress(Progress, "Java", "Using existing Java runtime.", 15);
            CachedJavaPath = ExistingJava;
            return ExistingJava;
        }

        if (Directory.Exists(Paths.JavaExtractDirectory))
        {
            Directory.Delete(Paths.JavaExtractDirectory, true);
        }

        if (File.Exists(Paths.JavaZipPath))
        {
            File.Delete(Paths.JavaZipPath);
        }

        Directory.CreateDirectory(Paths.RuntimeDirectory);
        Shared.ReportProgress(Progress, "Java", "Downloading Java runtime.", 10);

        using HttpClient Client = new();
        using HttpResponseMessage Response = await Client.GetAsync(
            Settings.OracleJdkZipUrl,
            HttpCompletionOption.ResponseHeadersRead,
            CancellationToken).ConfigureAwait(false);

        Response.EnsureSuccessStatusCode();

        long? TotalLength = Response.Content.Headers.ContentLength;

        await using (Stream Input = await Response.Content.ReadAsStreamAsync(CancellationToken).ConfigureAwait(false))
        await using (FileStream Output = File.Create(Paths.JavaZipPath))
        {
            byte[] Buffer = new byte[81920];
            long TotalRead = 0;
            int Read;

            while ((Read = await Input.ReadAsync(Buffer.AsMemory(0, Buffer.Length), CancellationToken).ConfigureAwait(false)) > 0)
            {
                await Output.WriteAsync(Buffer.AsMemory(0, Read), CancellationToken).ConfigureAwait(false);
                TotalRead += Read;

                if (TotalLength.HasValue && TotalLength.Value > 0)
                {
                    double Ratio = (double)TotalRead / TotalLength.Value;
                    double Percent = 10d + (Ratio * 35d);
                    Shared.ReportProgress(Progress, "Java", "Downloading Java runtime.", Percent);
                }
            }
        }

        Shared.ReportProgress(Progress, "Java", "Extracting Java runtime.", 48);
        Directory.CreateDirectory(Paths.JavaExtractDirectory);
        ZipFile.ExtractToDirectory(Paths.JavaZipPath, Paths.JavaExtractDirectory, true);

        string JavaPath = Shared.FindJavaExecutable(Paths.JavaExtractDirectory)
            ?? throw new FileNotFoundException("java.exe was not found after extracting Oracle JDK.");

        Shared.ValidateJavaLayout(JavaPath);
        Shared.ReportProgress(Progress, "Java", "Java runtime is ready.", 55);

        CachedJavaPath = JavaPath;
        return JavaPath;
    }
}
