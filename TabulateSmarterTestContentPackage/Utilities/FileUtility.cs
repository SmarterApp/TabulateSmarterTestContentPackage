using System.Xml.XPath;
using TabulateSmarterTestContentPackage.Models;

namespace TabulateSmarterTestContentPackage.Utilities
{
    public static class FileUtility
    {
        public static string GetAttachmentFilename(ItemContext it, IXPathNavigable xml, string attachType)
        {
            var xp = !it.IsStimulus
                ? string.Concat("itemrelease/item/content/attachmentlist/attachment[@type='", attachType, "']")
                : string.Concat("itemrelease/passage/content/attachmentlist/attachment[@type='", attachType, "']");

            var xmlEle = xml.CreateNavigator().SelectSingleNode(xp);
            return xmlEle?.GetAttribute("file", string.Empty) ?? string.Empty;
        }
    }
}