using System.Configuration;
using TabulateSmarterTestContentPackage.Models;

namespace TabulateSmarterTestContentPackage.Utilities
{
    public static class SettingsUtility
    {
        public static void RetrieveAslValues()
        {
            TabulatorSettings.AslMean = double.Parse(ConfigurationManager.AppSettings["AslMean"]);
            TabulatorSettings.AslStandardDeviation =
                double.Parse(ConfigurationManager.AppSettings["AslStandardDeviation"]);
            TabulatorSettings.AslTolerance = int.Parse(ConfigurationManager.AppSettings["AslTolerance"]);
        }
    }
}