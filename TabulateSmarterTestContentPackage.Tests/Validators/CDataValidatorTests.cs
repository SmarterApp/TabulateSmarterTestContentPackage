using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NUnit.Framework;
using TabulateSmarterTestContentPackage.Extensions;
using TabulateSmarterTestContentPackage.Extractors;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Utilities;
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
            ReportingUtility.ErrorReportPath = "./debugErrors.csv";
        }

        // Source: item-187-1832 2016.2.24 IrpTestPackageAndContent
        private XElement ItemXml { get; set; }

        [Test]
        public void AllMatchingTermsTaggedShouldReturnTrue()
        {
            // Arrange
            const string nodeText =
                "<choiceInteraction responseIdentifier=\"EBSR2\" shuffle=\"false\" maxChoice=\"1\"><prompt>"
                +
                "<p style=\"\">Which sentence from the passage <span style=\"\">best </span>supports your answer in part A?</p></prompt>"
                + "<simpleChoice identifier=\"A\">"
                +
                "<span id=\"item_2493_TAG_10\" class=\"its-tag\" data-tag=\"word\" data-tag-boundary=\"start\" data-word-index=\"1\"></span>over<span class=\"its-tag\" data-tag-ref=\"item_2493_TAG_10\" data-tag-boundary=\"end\"></span>"
                +
                "<p style=\"\">“A bird's nest sat right in the middle of <span id=\"item_2493_TAG_5_BEGIN\">Mrs.</span> Baxter's wreath.”</p></simpleChoice>"
                + "<simpleChoice identifier=\"B\">"
                +
                "<p style=\"\">“Jessie and <span id=\"item_2493_TAG_6_BEGIN\">Mrs.</span> Baxter talked about the birds for a while.”</p></simpleChoice>"
                + "<simpleChoice identifier=\"C\">"
                + "<p style=\"\">“One morning, Jessie saw a pink head poking out of the nest.”</p></simpleChoice>"
                + "<simpleChoice identifier=\"D\">"
                +
                "<p style=\"\">“‘You can't use this door,’ Jessie said, holding her arms out stiff.”</p></simpleChoice></choiceInteraction>";
            var node = XDocument.Parse(nodeText).Root;
            var dictionary = new Dictionary<string, int>
            {
                {"over", 1 }
            };

            // Act
            var result = CDataValidator.AllMatchingTermsTagged(node, new ItemContext(null, null, null, null),
                ErrorSeverity.Degraded, dictionary);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void EmptyEndTagShouldReturnFalse()
        {
            // Arrange
            const string nodeText = "<span></span>";
            var node = XDocument.Parse(nodeText).Root;

            // Act
            var result = CDataValidator.IsMatchingEndTag(node, "item_1832_TAG_3");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void EmptyStartTagShouldReturnFalse()
        {
            // Arrange
            const string nodeText = "<span></span>";
            var node = XDocument.Parse(nodeText).Root;

            // Act
            var result = CDataValidator.IsStartingTag(node);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void MatchingEndTagMissingDataTagBoundaryAttributesShouldReturnFalse()
        {
            // Arrange
            const string nodeText = "<span class=\"its-tag\" data-tag-ref=\"item_1832_TAG_3\"></span>";
            var node = XDocument.Parse(nodeText).Root;

            // Act
            var result = CDataValidator.IsMatchingEndTag(node, "item_1832_TAG_3");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void MatchingEndTagMissingDataTagRefAttributesShouldReturnFalse()
        {
            // Arrange
            const string nodeText = "<span class=\"its-tag\" data-tag-boundary=\"end\"></span>";
            var node = XDocument.Parse(nodeText).Root;

            // Act
            var result = CDataValidator.IsMatchingEndTag(node, "item_1832_TAG_3");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void MatchingEndTagShouldReturnTrue()
        {
            // Arrange
            const string nodeText =
                "<span class=\"its-tag\" data-tag-ref=\"item_1832_TAG_3\" data-tag-boundary=\"end\"></span>";
            var node = XDocument.Parse(nodeText).Root;

            // Act
            var result = CDataValidator.IsMatchingEndTag(node, "item_1832_TAG_3");

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void NestedStartTagShouldReturnFalse()
        {
            // Arrange
            const string nodeText = "<test>" +
                                    "<span id=\"item_1832_TAG_3\" class=\"its-tag\" data-tag=\"word\" data-tag-boundary=\"start\" data-word-index=\"1\">" +
                                    "<span id=\"item_1832_TAG_4\" class=\"its-tag\" data-tag=\"word\" data-tag-boundary=\"start\" data-word-index=\"2\"></span>" +
                                    "</span>" +
                                    "</test>";
            var node = XDocument.Parse(nodeText).Root;

            // Act
            var result = CDataValidator.CDataGlossaryTagsAreNotIllegallyNested(node,
                new ItemContext(null, null, null, null), ErrorSeverity.Degraded);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void NonMatchingEndTagShouldReturnFalse()
        {
            // Arrange
            const string nodeText =
                "<span class=\"its-tag\" data-tag-ref=\"item_1832_TAG_3\" data-tag-boundary=\"end\"></span>";
            var node = XDocument.Parse(nodeText).Root;

            // Act
            var result = CDataValidator.IsMatchingEndTag(node, "id");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void NonNestedStartTagShouldReturnTrue()
        {
            // Arrange
            const string nodeText = "<test>" +
                                    "<span id=\"item_1832_TAG_3\" class=\"its-tag\" data-tag=\"word\" data-tag-boundary=\"start\" data-word-index=\"1\"></span>" +
                                    "<span id=\"item_1832_TAG_4\" class=\"its-tag\" data-tag=\"word\" data-tag-boundary=\"start\" data-word-index=\"2\"></span>" +
                                    "</test>";
            var node = XDocument.Parse(nodeText).Root;

            // Act
            var result = CDataValidator.CDataGlossaryTagsAreNotIllegallyNested(node,
                new ItemContext(null, null, null, null), ErrorSeverity.Degraded);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void NonSpanEndTagShouldReturnFalse()
        {
            // Arrange
            const string nodeText =
                "<div class=\"its-tag\" data-tag-ref=\"item_1832_TAG_3\" data-tag-boundary=\"end\"></div>";
            var node = XDocument.Parse(nodeText).Root;

            // Act
            var result = CDataValidator.IsMatchingEndTag(node, "item_1832_TAG_3");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void NonSpanStartTagShouldReturnFalse()
        {
            // Arrange
            const string nodeText =
                "<div id=\"item_1832_TAG_3\" class=\"its-tag\" data-tag=\"word\" data-tag-boundary=\"start\" data-word-index=\"1\"></div>";
            var node = XDocument.Parse(nodeText).Root;

            // Act
            var result = CDataValidator.IsStartingTag(node);

            // Assert
            Assert.IsFalse(result);
        }

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
            var result =
                itemCData.Select(x => CDataValidator.IsValid(x, new ItemContext(null, null, null, null))).ToList();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Any());
            Assert.IsFalse(result.First());
            Assert.IsTrue(result.Last());
        }

        [Test]
        public void StartTagMissingDataTagAttributesShouldReturnFalse()
        {
            // Arrange
            const string nodeText =
                "<span id=\"item_1832_TAG_3\" class=\"its-tag\" data-tag-boundary=\"start\" data-word-index=\"1\"></span>";
            var node = XDocument.Parse(nodeText).Root;

            // Act
            var result = CDataValidator.IsStartingTag(node);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void StartTagMissingDataTagBoundaryAttributesShouldReturnFalse()
        {
            // Arrange
            const string nodeText =
                "<span id=\"item_1832_TAG_3\" class=\"its-tag\" data-tag=\"word\" data-word-index=\"1\"></span>";
            var node = XDocument.Parse(nodeText).Root;

            // Act
            var result = CDataValidator.IsStartingTag(node);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void StartTagMissingIdAttributesShouldReturnFalse()
        {
            // Arrange
            const string nodeText =
                "<span class=\"its-tag\" data-tag=\"word\" data-word-index=\"1\" data-tag-boundary=\"start\"></span>";
            var node = XDocument.Parse(nodeText).Root;

            // Act
            var result = CDataValidator.IsStartingTag(node);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void ValidStartTagShouldReturnTrue()
        {
            // Arrange
            const string nodeText =
                "<span id=\"item_1832_TAG_3\" class=\"its-tag\" data-tag=\"word\" data-tag-boundary=\"start\" data-word-index=\"1\"></span>";
            var node = XDocument.Parse(nodeText).Root;

            // Act
            var result = CDataValidator.IsStartingTag(node);

            // Assert
            Assert.IsTrue(result);
        }
    }
}