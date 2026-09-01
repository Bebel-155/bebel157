namespace BebelEquipe155
{
    public sealed class CommandResult
    {
        public int ExitCode { get; set; }
        public string StdOut { get; set; }
        public string StdErr { get; set; }
        public bool TimedOut { get; set; }
        public string Combined { get { return ((StdOut ?? "") + "\r\n" + (StdErr ?? "")).Trim(); } }
    }

    public interface ICommandRunner
    {
        CommandResult Run(string file, string arguments, int timeoutSeconds);
    }
}
