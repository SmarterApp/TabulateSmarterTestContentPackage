using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.IO;

namespace TabulateSmarterTestContentPackage.Extractors
{
    static class RubricExtractor
    {
        static public bool ExtractRubric(XmlDocument xml, TextWriter writer)
        {
            var rubrics = xml.SelectNodes("itemrelease/item/content[@language='ENU']/rubriclist/rubric");
            if (rubrics.Count == 0) return false;

            writer.WriteLine("<html><head><style>table {border-collapse: collapse;} table, th, td {border: 1px solid black;}</style></head><body><table>");
            writer.WriteLine("<tr><th>Scorepoint</th><th>Name</th><th>Content</th></tr>");

            foreach (XmlElement xmlRubric in rubrics)
            {
                writer.WriteLine("<tr><td>{0}</td><td>{1}</td><td>{2}</td></tr>",
                    xmlRubric.XpEvalE("@scorepoint"),
                    xmlRubric.XpEvalE("name"),
                    xmlRubric.XpEvalE("val/text()"));
            }
            writer.WriteLine("</table></body></html>");

            return true;
        }

        static public bool ExtractRubric(XmlDocument xml, Stream stream)
        {
            using (var writer = new StreamWriter(stream, Encoding.UTF8, 256, true))
            {
                return ExtractRubric(xml, writer);
            }
        }

        static public string ExtractRubric(XmlDocument xml)
        {
            using (StringWriter writer = new StringWriter())
            {
                if (!ExtractRubric(xml, writer)) return null;
                writer.Flush();
                return writer.ToString();
            }
        }
    }
}
