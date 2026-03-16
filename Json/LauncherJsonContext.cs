using System.Text.Json.Serialization;

namespace LauncherApp;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(UiSettings))]
[JsonSerializable(typeof(GitHubReleaseInfo))]
internal partial class LauncherJsonContext : JsonSerializerContext
{
}
