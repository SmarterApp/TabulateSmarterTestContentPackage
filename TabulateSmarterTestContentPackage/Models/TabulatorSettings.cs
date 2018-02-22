using System.Configuration;

namespace TabulateSmarterTestContentPackage.Models
{
    public static class TabulatorSettings
    {
        const string c_defAslMean = "0.197121";
        const string c_defAslStandardDeviation = "0.162031";
        const string c_defAslToleranceInStdev = "3.0";

        public static void Load()
        {
            AslMean = double.Parse(ConfigurationManager.AppSettings["AslMean"] ?? c_defAslMean);
            AslStandardDeviation = double.Parse(ConfigurationManager.AppSettings["AslStandardDeviation"] ?? c_defAslStandardDeviation);
            AslToleranceInStdev = double.Parse(ConfigurationManager.AppSettings["AslToleranceInStdev"] ?? c_defAslToleranceInStdev);
        }

        public static double AslMean { get; private set; }
        public static double AslStandardDeviation { get; private set; }
        public static double AslToleranceInStdev { get; private set; }
    }
}