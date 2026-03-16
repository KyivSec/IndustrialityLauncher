namespace LauncherApp
{
    public sealed class ModpackUpdateInfo
    {
        public bool IsInstalled { get; }
        public string CurrentVersion { get; }
        public string LatestVersion { get; }
        public bool UpdateAvailable { get; }

        public ModpackUpdateInfo(bool IsInstalled, string CurrentVersion, string LatestVersion, bool UpdateAvailable)
        {
            this.IsInstalled = IsInstalled;
            this.CurrentVersion = CurrentVersion;
            this.LatestVersion = LatestVersion;
            this.UpdateAvailable = UpdateAvailable;
        }
    }
}