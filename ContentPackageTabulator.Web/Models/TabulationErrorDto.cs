namespace ContentPackageTabulator.Web.Models
{
    public class TabulationErrorDto
    {
        public string Category { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
    }
}