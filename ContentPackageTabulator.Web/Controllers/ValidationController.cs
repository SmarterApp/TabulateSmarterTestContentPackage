using System.Collections.Generic;
using System.Linq;
using ContentPackageTabulator;
using Microsoft.AspNetCore.Mvc;
using ContentPackageTabulator.Web.Models;
using System;

namespace TabulateSmarterTestContentPackage.Web.Controllers
{
    [Route("api/v1/[controller]")]
    public class ValidationController : Controller
    {
        [HttpPost]
        public IEnumerable<TabulationErrorDto> Post([FromBody] ValidationDto validationDto)
        {
            var result = new Tabulator().TabulateErrors(validationDto.Path);
            return result.Select(x => new TabulationErrorDto
            {
                Category = x.Category.ToString(),
                Detail = x.Detail,
                Message = x.Message,
                Severity = x.Severity.ToString()
            }).OrderBy(x => x.Severity, new CustomStringComparer(StringComparer.OrdinalIgnoreCase))
                         .ThenBy(x => x.Category)
                         .ThenBy(x => x.Message)
                         .ThenBy(x => x.Detail);
        }
    }

	class CustomStringComparer : IComparer<string>
	{
		private readonly IComparer<string> _baseComparer;
		public CustomStringComparer(IComparer<string> baseComparer)
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
