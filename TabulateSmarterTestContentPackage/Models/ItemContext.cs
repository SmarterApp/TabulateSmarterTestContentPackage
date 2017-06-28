using System;

namespace TabulateSmarterTestContentPackage.Models
{
    public class ItemContext
    {
        public ItemContext(Tabulator tabulator, FileFolder ffItem, string itemId, string itemType)
        {
            FfItem = ffItem;
            ItemId = itemId ?? string.Empty;
            ItemType = itemType ?? string.Empty;
            Folder = tabulator.mPackageName + ffItem.RootedName;
        }

        public FileFolder FfItem { get; }
        public string ItemId { get; }
        public string ItemType { get; }
        public string Folder { get; }

        public bool IsPassage => string.Equals(ItemType, "pass", StringComparison.OrdinalIgnoreCase);
    }
}
