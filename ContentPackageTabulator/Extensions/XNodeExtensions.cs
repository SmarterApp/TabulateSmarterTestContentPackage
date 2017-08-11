using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace ContentPackageTabulator.Extensions
{
    public static class XNodeExtensions
    {
        public static XElement Cast(this XNode node)
        {
            try
            {
                return node as XElement;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static XObject SelectSingleNode(this XNode node, string xPath, XmlNamespaceManager manager = null)
        {
            var result = (IEnumerable) node?.XPathEvaluate(xPath, manager);
            var element = result?.OfType<XElement>()?.ToList();
            var attribute = result?.OfType<XAttribute>()?.ToList();
            if (element?.FirstOrDefault() != null)
            {
                return element.FirstOrDefault();
            }
            return attribute?.FirstOrDefault() != null ? attribute.FirstOrDefault() : null;
        }

        public static IEnumerable<XObject> SelectNodes(this XNode node, string xPath, XmlNamespaceManager manager = null)
        {
            var result = (IEnumerable) node?.XPathEvaluate(xPath, manager);
            var elements = result?.OfType<XElement>()?.ToList();
            var attributes = result?.OfType<XAttribute>()?.ToList();
            if (elements != null)
            {
                return elements;
            }
            return attributes?.FirstOrDefault() != null ? attributes : null;
        }

        public static string InnerText(this XNode node)
        {
            return node.NodeType == XmlNodeType.Element
                ? ((XElement) node).Value
                : node.ToString();
        }

        public static string OuterXml(this XNode node)
        {
            return node?.ToString();
        }

        public static XNode FirstChild(this XNode node)
        {
            var element = node.Cast();
            return element?.DescendantNodes().FirstOrDefault();
        }
    }
}