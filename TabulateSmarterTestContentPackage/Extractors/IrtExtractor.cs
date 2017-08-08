using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using ContentPackageTabulator.Models;

namespace ContentPackageTabulator.Extractors
{
    public static class IrtExtractor
    {
        public static IEnumerable<ItemScoring> RetrieveIrtInformation(XDocument root)
        {
            var namespaceManager = new XmlNamespaceManager(new NameTable());
            namespaceManager.AddNamespace("sa", "http://www.smarterapp.org/ns/1/assessment_item_metadata");
            var irt = root.XPathSelectElements(
                "metadata/sa:smarterAppMetadata/sa:IrtDimension", namespaceManager);
            return irt.Select(x => new ItemScoring
            {
                Domain = x.XPathSelectElement("./ sa:IrtStatDomain", namespaceManager)?.Value ?? string.Empty,
                MeasurementModel = x.XPathSelectElement("./sa:IrtModelType", namespaceManager)?.Value ?? string.Empty,
                Dimension = x.XPathSelectElement("./sa:IrtDimensionPurpose", namespaceManager)?.Value ?? string.Empty,
                ScorePoints = x.XPathSelectElement("./sa:IrtScore", namespaceManager)?.Value ?? string.Empty,
                Weight = x.XPathSelectElement("./sa:IrtWeight", namespaceManager)?.Value ?? string.Empty,
                a =
                    x.XPathSelectElement("./sa:IrtParameter[sa:Name/text() = \"a\"]/sa:Value", namespaceManager)?.Value ??
                    string.Empty,
                b =
                    x.XPathSelectElement("./sa:IrtParameter[sa:Name/text() = \"b\"]/sa:Value", namespaceManager)?.Value ??
                    string.Empty,
                b0 =
                    x.XPathSelectElement("./sa:IrtParameter[sa:Name/text() = \"b0\"]/sa:Value", namespaceManager)?.Value ??
                    string.Empty,
                b1 =
                    x.XPathSelectElement("./sa:IrtParameter[sa:Name/text() = \"b1\"]/sa:Value", namespaceManager)?.Value ??
                    string.Empty,
                b2 =
                    x.XPathSelectElement("./sa:IrtParameter[sa:Name/text() = \"b2\"]/sa:Value", namespaceManager)?.Value ??
                    string.Empty,
                b3 =
                    x.XPathSelectElement("./sa:IrtParameter[sa:Name/text() = \"b3\"]/sa:Value", namespaceManager)?.Value ??
                    string.Empty,
                b4 =
                    x.XPathSelectElement("./sa:IrtParameter[sa:Name/text() = \"b4\"]/sa:Value", namespaceManager)?.Value ??
                    string.Empty,
                c =
                    x.XPathSelectElement("./sa:IrtParameter[sa:Name/text() = \"c\"]/sa:Value", namespaceManager)?.Value ??
                    string.Empty
            });
        }
    }
}