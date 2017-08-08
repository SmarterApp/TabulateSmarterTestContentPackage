using System;
using System.Collections.Generic;
using System.Linq;

namespace ContentPackageTabulator.Utilities
{
    public static class MathUtility
    {
        public static double StandardDeviation(IList<double> values)
        {
            var average = values.Average();
            return Math.Sqrt(values.Average(v => Math.Pow(v - average, 2)));
        }
    }
}