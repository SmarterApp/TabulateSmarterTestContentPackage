using System.Collections.Generic;
using System.Linq;
using ContentPackageTabulator;
using ContentPackageTabulator.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using ContentPackageTabulator.Utilities;
using ContentPackageTabulator.Web.Models;

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
            }).OrderBy(x => x.Severity, new ErrorSeverityComparer(StringComparer.OrdinalIgnoreCase))
                         .ThenBy(x => x.Category)
                         .ThenBy(x => x.Message)
                         .ThenBy(x => x.Detail);
        }
    }
}
