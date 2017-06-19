using System;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using NLog;

namespace TabulateSmarterTestContentPackage.Validators
{
    public class CDataValidator
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public bool IsValid(XCData cData)
        {
            if (cData == null)
            {
                Logger.Error("invalid input");
                return false;
            }
            try
            {
                // The CData nodes in the content package should all contain valid HTML.
                // The longer-form call is being used here to avoid collisions with the overloaded
                // string-only parameter that also takes a URL argument.
                var cDataSection = XDocument.Load(cData.Value, LoadOptions.None);

                // There is no way to predict where the images will appear in the CData (if they appear at all)
                // use a global selector.
                var imgTags = cDataSection.XPathSelectElements("//img");
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
        public bool ImgElementHasValidAltTag(XElement imageElement)
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