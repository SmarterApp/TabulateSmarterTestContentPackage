using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NUnit.Framework;
using TabulateSmarterTestContentPackage.Extensions;
using TabulateSmarterTestContentPackage.Extractors;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Validators;

namespace TabulateSmarterTestContentPackage.Tests.Validators
{
    [TestFixture]
    public class CDataValidatorTests
    {
        [SetUp]
        public void Setup()
        {
            const string xml = "<content language =\"ENU\" version=\"2.0\" approvedVersion=\"3\">\r\n      " +
                               "<rationaleoptlist />\r\n      " +
                               "<stem><![CDATA[<p style=\"\"><span id=\"item_1832_TAG_3\" class=\"its-tag\" data-tag=\"word\" data-tag-boundary=\"start\" data-word-index=\"1\"></span>Enter<span class=\"its-tag\" data-tag-ref=\"item_1832_TAG_3\" data-tag-boundary=\"end\"></span> the value of <img id=\"item_1832_Object1\" style=\"vertical-align:middle;\" src=\"item_1832_v3_Object1_png16malpha.png\" width=\"125\" height=\"39\" />.</p>]]></stem>\r\n      " +
                               "<rubriclist>\r\n        " +
                               "<rubric scorepoint=\"\">\r\n          " +
                               "<name>\r\n        " +
                               "Rubric\u00A0</name>\r\n          " +
                               "<val><![CDATA[<p style=\"\">&#xA0;</p><p style=\"\">Exemplar: <img id=\"item_1832_Object2\" style=\"vertical-align:middle;\" src=\"item_1832_v3_Object2_png16malpha.png\" width=\"28\" height=\"39\" alt=\"testValue\" /></p><p style=\"\">&#xA0;</p><p style=\"\">1 point: Student enters 5 1/3 or the equivalent.</p><p style=\"\">&#xA0;</p>]]></val>\r\n        " +
                               "</rubric>\r\n        " +
                               "<samplelist maxval=\"\" minval=\"\" />\r\n      " +
                               "</rubriclist>\r\n" +
                               "</content>";
            ItemXml = new XDocument().LoadXml(xml).Root;
        }

        // Source: item-187-1832 2016.2.24 IrpTestPackageAndContent
        private XElement ItemXml { get; set; }

        [Test]
        public void RetrieveAllCData()
        {
            // Arrange
            // Act
            var result = CDataExtractor.ExtractCData(ItemXml).ToList();

            // Assert
            Assert.AreEqual(result.Count(), 2);
            Assert.IsTrue(result.All(x => x.NodeType == XmlNodeType.CDATA));
        }

        [Test]
        public void RetrieveImagesFromCData()
        {
            // Arrange
            var itemCData = CDataExtractor.ExtractCData(ItemXml);

            // Act
            var result = itemCData.Select(x => CDataValidator.IsValid(x, new ItemContext(null,null,null,null))).ToList();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Any());
            Assert.IsFalse(result.First());
            Assert.IsFalse(result.Last());
        }
    }
}