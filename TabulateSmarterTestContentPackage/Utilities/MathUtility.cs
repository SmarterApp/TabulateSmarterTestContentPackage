using System;
using System.Collections.Generic;
using System.Linq;

namespace TabulateSmarterTestContentPackage.Utilities
{
    public class StatAccumulator
    {
        int m_count = 0;        // Count of samples
        double m_sum = 0.0;     // Sum of samples
        double m_sqSum = 0.0;   // Sum of squares of samples

        public void Clear()
        {
            m_count = 0;
            m_sum = 0.0;
            m_sqSum = 0.0;
        }

        public int AddDatum(double value)
        {
            m_sum += value;
            m_sqSum += value * value;
            ++m_count;
            return m_count;
        }

        public int Count
        {
            get { return m_count; }
        }

        public double Mean
        {
            get { return m_sum / m_count; }
        }

        public double StandardDeviation
        {
            get { return Math.Sqrt((m_sqSum - (m_sum * m_sum) / m_count) / (m_count - 1)); }
        }
    }
}