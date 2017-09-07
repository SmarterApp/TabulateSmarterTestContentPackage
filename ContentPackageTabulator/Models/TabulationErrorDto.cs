using Newtonsoft.Json;

namespace ContentPackageTabulator.Models
{
    public class TabulationErrorDto
    {
        [JsonProperty(PropertyName="category")]
        public string Category { get; set; } = string.Empty;
        [JsonProperty(PropertyName = "severity")]
        public string Severity { get; set; } = string.Empty;
        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; } = string.Empty;
        [JsonProperty(PropertyName = "detail")]
        public string Detail { get; set; } = string.Empty;
    }
}