namespace ContentPackageTabulator.Models
{
    public class TabulationError
    {
        public ItemContext Context { get; set; }
        public ErrorCategory Category { get; set; }
        public ErrorSeverity Severity { get; set; }
        public string Message { get; set; }
        public string Detail { get; set; }
    }
}
