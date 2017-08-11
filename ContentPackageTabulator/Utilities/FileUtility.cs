using System.Xml.Linq;
using ContentPackageTabulator.Extensions;
using ContentPackageTabulator.Models;

namespace ContentPackageTabulator.Utilities
{
    public static class FileUtility
    {
        public static string GetAttachmentFilename(ItemContext it, XDocument xml, string attachType)
        {
            var xp =
                $"itemrelease/{(it.IsPassage ? "passage" : "item")}/content/attachmentlist/attachment[@type='{attachType}']";

            var xmlEle = xml.SelectSingleNode(xp) as XElement;
            return xmlEle?.GetAttribute("file") ?? string.Empty;
        }
    }
}