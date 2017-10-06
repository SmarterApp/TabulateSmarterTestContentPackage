using System.Xml;
using TabulateSmarterTestContentPackage.Models;

namespace TabulateSmarterTestContentPackage.Utilities
{
    public static class FileUtility
    {
        public static string GetAttachmentFilename(ItemContext it, XmlDocument xml, string attachType)
        {
            var xp = !it.IsStimulus
                ? string.Concat("itemrelease/item/content/attachmentlist/attachment[@type='", attachType, "']")
                : string.Concat("itemrelease/passage/content/attachmentlist/attachment[@type='", attachType, "']");

            var xmlEle = xml.SelectSingleNode(xp) as XmlElement;
            return xmlEle?.GetAttribute("file") ?? string.Empty;
        }
    }
}