namespace TabulateSmarterTestContentPackage.Models
{
    public static class TabulatorSettings
    {
        const double c_defAslMean = 0.197121;
        const double c_defAslStandardDeviation = 0.162031;
        const double c_defAslToleranceInStdev = 3.0;

        public static void Load()
        {
            // TODO: Allow command-line overrides
            AslMean = c_defAslMean;
            AslStandardDeviation = c_defAslStandardDeviation;
            AslToleranceInStdev = c_defAslToleranceInStdev;
        }

        public static double AslMean { get; private set; }
        public static double AslStandardDeviation { get; private set; }
        public static double AslToleranceInStdev { get; private set; }
    }
}