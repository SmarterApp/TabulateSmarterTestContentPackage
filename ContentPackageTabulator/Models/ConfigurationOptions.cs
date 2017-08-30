using System.IO;
using Microsoft.Extensions.Configuration;

namespace ContentPackageTabulator.Models
{
    public static class ConfigurationOptions
    {
        static ConfigurationOptions()
        {
            Configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("tabulationsettings.json", false, true)
                .Build();
            AslMean = double.Parse(Configuration["AslMean"]);
            AslStandardDeviation = double.Parse(Configuration["AslStandardDeviation"]);
            AslTolerance = int.Parse(Configuration["AslTolerance"]);
        }

        private static IConfigurationRoot Configuration { get; }
        public static double AslMean { get; set; }
        public static double AslStandardDeviation { get; set; }
        public static int AslTolerance { get; set; }
    }
}