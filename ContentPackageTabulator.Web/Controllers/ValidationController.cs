using System.Net.Http;
using ContentPackageTabulator;
using Microsoft.AspNetCore.Mvc;

namespace TabulateSmarterTestContentPackage.Web.Controllers
{
    [Route("api/v1/[controller]")]
    public class ValidationController : Controller
    {
        [HttpPost]
        [Route("{path}")]
        public HttpResponseMessage Get([FromBody] string path)
        {
            var tabulator = new Tabulator();
            //tabulator.ProduceErrors(path);
            return new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK
            };
        }
    }
}
