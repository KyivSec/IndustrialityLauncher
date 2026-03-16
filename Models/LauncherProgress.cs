namespace LauncherApp
{
    public sealed class LauncherProgress
    {
        public string Stage { get; }
        public string Message { get; }
        public double Percent { get; }

        public LauncherProgress(string Stage, string Message, double Percent)
        {
            this.Stage = Stage;
            this.Message = Message;
            this.Percent = Percent;
        }
    }
}