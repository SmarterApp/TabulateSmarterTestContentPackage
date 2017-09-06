using System.Collections.Generic;

namespace ContentPackageTabulator.Utilities
{
    public class ErrorSeverityComparer : IComparer<string>
	{
		private readonly IComparer<string> _baseComparer;
		public ErrorSeverityComparer(IComparer<string> baseComparer)
		{
			_baseComparer = baseComparer;
		}

		public int Compare(string x, string y)
		{
			if (_baseComparer.Compare(x, y) == 0)
				return 0;

			// "severe" comes before everything else
			if (_baseComparer.Compare(x, "Severe") == 0)
				return -1;
			if (_baseComparer.Compare(y, "Severe") == 0)
				return 1;

			// "degraded" comes next
			if (_baseComparer.Compare(x, "Degraded") == 0)
				return -1;
			if (_baseComparer.Compare(y, "Degraded") == 0)
				return 1;

			// "tolerable" comes next
			if (_baseComparer.Compare(x, "Tolerable") == 0)
				return -1;
			if (_baseComparer.Compare(y, "Tolerable") == 0)
				return 1;

			// "benign" comes last
			if (_baseComparer.Compare(x, "Benign") == 0)
				return -1;
			if (_baseComparer.Compare(y, "Benign") == 0)
				return 1;

			return _baseComparer.Compare(x, y);
		}
	}
}
