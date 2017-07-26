using System.Collections.Generic;
using System.Linq;

namespace TabulateSmarterTestContentPackage.Models
{
    public class ItemScoring
    {
        public string MeasurementModel { get; set; } = string.Empty;
        public string ScorePoints { get; set; } = string.Empty;
        public string Dimension { get; set; } = string.Empty;
        public string Weight { get; set; } = string.Empty;
        public string a { get; set; } = string.Empty;
        public string b { get; set; } = string.Empty;
        public string b0 { get; set; } = string.Empty;
        public string b1 { get; set; } = string.Empty;
        public string b2 { get; set; } = string.Empty;
        public string b3 { get; set; } = string.Empty;
        public string b4 { get; set; } = string.Empty;
        public string c { get; set; } = string.Empty;

        public string GetParameters()
        {
            var stringList = new List<string>
            {
                a,
                b,
                b0,
                b1,
                b2,
                b3,
                b4,
                c
            };
            return stringList.Any(x => !string.IsNullOrEmpty(x))
                ? stringList.Where(x => !string.IsNullOrEmpty(x)).Aggregate((x, y) => $"{x}|{y}")
                : string.Empty;
        }
    }
}