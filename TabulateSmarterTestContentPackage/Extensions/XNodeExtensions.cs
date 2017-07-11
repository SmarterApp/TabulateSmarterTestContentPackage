using System;
using System.Xml.Linq;

namespace TabulateSmarterTestContentPackage.Extensions
{
    public static class XNodeExtensions
    {
        public static XElement Cast(this XNode node)
        {
            try
            {
                return XElement.Parse(node.ToString());
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
