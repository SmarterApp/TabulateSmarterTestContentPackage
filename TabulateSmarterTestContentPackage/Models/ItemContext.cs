using System;

namespace TabulateSmarterTestContentPackage.Models
{
    public class ItemIdentifier
    {
        string m_itemType;
        bool m_isStimulus;
        int m_bankKey;
        int m_itemId;

        public ItemIdentifier(string itemType, int bankKey, int itemId)
        {
            if (itemType == null) throw new ArgumentException("itemType may not be null");
            m_itemType = itemType;
            m_isStimulus = itemType.Equals("stim", StringComparison.OrdinalIgnoreCase);
            m_bankKey = bankKey;
            m_itemId = itemId;
        }

        public ItemIdentifier(string itemType, string bankKey, string itemId)
        {
            if (itemType == null) throw new ArgumentException("itemType may not be null");
            m_itemType = itemType;
            m_isStimulus = itemType.Equals("stim", StringComparison.OrdinalIgnoreCase);
            if (!int.TryParse(bankKey, out m_bankKey)) throw new ArgumentException("bankKey must be integer");
            if (!int.TryParse(itemId, out m_itemId)) throw new ArgumentException("itemId must be integer");
        }

        public string ItemType
        {
            get { return m_itemType; }
        }
        public int BankKey
        {
            get { return m_bankKey; }
        }
        public int ItemId
        {
            get { return m_itemId; }
        }
        public bool IsStimulus
        {
            get { return m_isStimulus; }
        }
        public string FullId
        {
            get
            {
                return $"{(m_isStimulus ? "stim" : "item")}-{BankKey}-{ItemId}";
            }
        }
        public string FolderName
        {
            get
            {
                // This is an inconsistency. Folder names use capitalized "Item" while item names use lower case.
                return $"{(m_isStimulus ? "Stimuli/stim" : "Items/Item")}-{BankKey}-{ItemId}";
            }
        }

        public override string ToString()
        {
            return FullId;
        }

        public override int GetHashCode()
        {
            return m_isStimulus.GetHashCode() ^ BankKey.GetHashCode() ^ ItemId.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var b = obj as ItemIdentifier;
            if (b == null) return false;
            return b.m_isStimulus == m_isStimulus && b.m_bankKey == m_bankKey && b.ItemId == m_itemId;
        }
    }

    public class ItemContext : ItemIdentifier
    {
        FileFolder m_ff;
        string m_folderDescription;

        public ItemContext(string packageName, FileFolder packageFolder, ItemIdentifier ii)
            : base(ii.ItemType, ii.BankKey, ii.ItemId)
        {
            m_ff = packageFolder.GetFolder(FolderName);
            m_folderDescription = string.Concat(packageName, "/", FolderName);
        }

        public ItemContext(string packageName, FileFolder packageFolder, string itemType, string bankKey, string itemId)
            : base(itemType, bankKey, itemId)
        {
            m_ff = packageFolder.GetFolder(FolderName);
            m_folderDescription = string.Concat(packageName, "/", FolderName);
        }

        public FileFolder FfItem
        {
            get { return m_ff; }
        }

        public string FolderDescription
        {
            get { return m_folderDescription; }
        }

    }
}