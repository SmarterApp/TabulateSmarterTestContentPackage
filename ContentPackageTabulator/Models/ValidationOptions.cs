using System.Collections.Generic;

namespace ContentPackageTabulator.Models
{
    public class ValidationOptions : Dictionary<string, bool>
	{
		public void Enable(string option)
		{
			this[option] = true;
		}

		public void Disable(string option)
		{
			this[option] = false;
		}

		public void EnableAll()
		{
			Clear(); // Since options default to enabled, clearing enables all.
		}

		public bool IsEnabled(string option)
		{
			bool value;
			return !TryGetValue(option, out value) || value;
		}
	}
}
