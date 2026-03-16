using System.Text.Json.Serialization;

namespace LauncherApp;

public sealed class GitHubReleaseInfo
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;
}
