using System.Xml.Linq;

namespace ContentPackageTabulator.Models
{
    public class CssElement
    {
        public XElement Element { get; set; }
        public string Style { get; set; }
    }
}