using System;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using NLog;
using TabulateSmarterTestContentPackage.Extensions;

namespace TabulateSmarterTestContentPackage.Validators
{
    public static class CDataValidator
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static bool IsValid(XCData cData)
        {
            if (cData == null)
            {
                Logger.Error("invalid input");
                return false;
            }
            try
            {
                var cDataSection = new XDocument().LoadXml(cData.Value);

                // There is no way to predict where the images will appear in the CData (if they appear at all)
                // use a global selector.
                var imgTags = cDataSection.XPathSelectElements("//img");

                // Right now, this is the only validity check we're doing on CData. This will be expanded
                // as more validations become necessary.
                return imgTags.All(ImgElementHasValidAltTag);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }
            return false;
        }

        //<summary>This method takes a <img> element tag and determines whether
        //the provided <img> element contains a valid "alt" attribute </summary>
        //<param name="image"> The <img> tag to be validated </param>
        public static bool ImgElementHasValidAltTag(XElement imageElement)
        {
            if (imageElement == null)
            {
                Logger.Error("stub");
                return false;
            }
            if (!imageElement.HasAttributes)
            {
                Logger.Error("stub");
                return false;
            }
            var altTag = imageElement.Attributes().Select(x =>
                new
                {
                    Name = x.Name.LocalName,
                    x.Value
                }).FirstOrDefault(x => x.Name.Equals("alt"));
            if (altTag == null)
            {
                Logger.Error("no valid alt tag");
                return false;
            }
            if (string.IsNullOrEmpty(altTag.Value))
            {
                Logger.Error("Alt tag present, but value is not valid");
                return false;
            }
            return true;
        }
    }
}