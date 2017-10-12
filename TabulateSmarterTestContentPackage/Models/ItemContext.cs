using System;

namespace TabulateSmarterTestContentPackage.Models
{
    public class ItemIdentifier : IComparable<ItemIdentifier>
    {
        const string c_itemPrefix = "item";
        const string c_stimPrefix = "stim";

        string m_itemType;
        bool m_isStimulus;
        int m_bankKey;
        int m_itemId;

        public ItemIdentifier(string itemType, int bankKey, int itemId)
        {
            if (itemType == null) throw new ArgumentException("itemType may not be null");
            m_itemType = itemType;
            m_isStimulus = itemType.Equals(c_stimPrefix, StringComparison.OrdinalIgnoreCase);
            m_bankKey = bankKey;
            m_itemId = itemId;
        }

        public ItemIdentifier(string itemType, string bankKey, string itemId)
        {
            if (itemType == null) throw new ArgumentException("itemType may not be null");
            m_itemType = itemType;
            m_isStimulus = itemType.Equals(c_stimPrefix, StringComparison.OrdinalIgnoreCase);
            if (!int.TryParse(bankKey, out m_bankKey)) throw new ArgumentException("bankKey must be integer");
            if (!int.TryParse(itemId, out m_itemId)) throw new ArgumentException("itemId must be integer");
        }

        public string ItemType
        {
            get { return m_itemType; }
            set
            {
                if (m_itemType.Equals(value, StringComparison.OrdinalIgnoreCase)) return;
                // Can only change from generic "item" to a specific item type and cannot change to "stim"
                if (!m_itemType.Equals(c_itemPrefix, StringComparison.OrdinalIgnoreCase)
                    || value.Equals(c_stimPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"Cannot change item type from '{m_itemType}' to '{value}'.");
                }
                m_itemType = value;
            }
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
                return $"{(m_isStimulus ? c_stimPrefix : c_itemPrefix)}-{BankKey}-{ItemId}";
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

        public int CompareTo(ItemIdentifier other)
        {
            int c = m_isStimulus.CompareTo(other.m_isStimulus);
            if (c != 0) return c;
            c = m_bankKey.CompareTo(other.m_bankKey);
            if (c != 0) return c;
            return m_itemId.CompareTo(other.m_itemId);
        }

        #region Static methods

        public static bool TryParse(string value, out ItemIdentifier ii)
        {
            ii = null;
            string[] parts = value.Split('-');
            if (parts.Length != 3) return false;

            string itemType = parts[0].ToLowerInvariant();
            if (!itemType.Equals(c_itemPrefix, StringComparison.Ordinal) && !itemType.Equals(c_stimPrefix)) return false;

            int bankKey;
            if (!int.TryParse(parts[1], out bankKey)) return false;

            int itemId;
            if (!int.TryParse(parts[2], out itemId)) return false;

            ii = new ItemIdentifier(itemType, bankKey, itemId);
            return true;
        }

        public static bool TryParse(string str, int defaultBankKey, out ItemIdentifier value)
        {
            int id;
            if (int.TryParse(str, out id))
            {
                value = new ItemIdentifier(c_itemPrefix, defaultBankKey, id);
                return true;
            }

            return TryParse(str, out value);
        }


        public static ItemIdentifier Parse(string value)
        {
            ItemIdentifier ii;
            if (!TryParse(value, out ii))
            {
                throw new ArgumentException("Invalid value, failed to parse ItemIdentifier.");
            }
            return ii;
        }

        #endregion // Static methods
    }

    public class ItemContext : ItemIdentifier
    {
        FileFolder m_ff;
        string m_folderDescription;

        public ItemContext(TestPackage package, ItemIdentifier ii)
            : base(ii.ItemType, ii.BankKey, ii.ItemId)
        {
            m_ff = package.GetItem(ii);
            m_folderDescription = string.Concat(package.Name, "/", FolderName);
        }

        // This is more efficient when the folder has already been retrieved
        public ItemContext(TestPackage package, FileFolder folder, ItemIdentifier ii)
            : base(ii.ItemType, ii.BankKey, ii.ItemId)
        {
            System.Diagnostics.Debug.Assert(folder.Name.Equals(ii.FullId, StringComparison.OrdinalIgnoreCase));
            m_ff = folder;
            m_folderDescription = string.Concat(package.Name, "/", FolderName);
        }

        public FileFolder FfItem
        {
            get { return m_ff; }
        }

        public string FolderDescription
        {
            get { return m_folderDescription; }
        }

        public static bool TryCreate(TestPackage package, ItemIdentifier ii, out ItemContext it)
        {
            FileFolder folder;
            if (!package.TryGetItem(ii, out folder))
            {
                it = null;
                return false;
            }
            it = new ItemContext(package, folder, ii);
            return true;
        }
    }
}