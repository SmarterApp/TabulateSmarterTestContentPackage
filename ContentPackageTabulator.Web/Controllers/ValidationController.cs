using System.Net.Http;
using ContentPackageTabulator;
using Microsoft.AspNetCore.Mvc;

namespace TabulateSmarterTestContentPackage.Web.Controllers
{
    [Route("api/[controller]")]
    public class ValidationController : Controller
    {
        [HttpGet]
        [Route("{type}/{id}")]
        public HttpResponseMessage Get([FromRoute] string type, [FromRoute] string id)
        {
            var tabulator = new Tabulator();
            var path = $"{type}-{id}";
            var result = tabulator.TabulateOne(path);
            return new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK
            };
        }
    }
}
