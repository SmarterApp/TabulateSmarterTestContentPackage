using System;
using System.Collections.Generic;
using System.IO;
using TabulateSmarterTestContentPackage.Models;

namespace TabulateSmarterTestContentPackage
{
    class SingleItemPackage : TestPackage
    {
        string m_name;
        ItemIdentifier m_ii;
        FileFolder m_folder;

        public SingleItemPackage(string rootFolder)
        {
            m_name = Path.GetFileName(rootFolder);

            if (!ItemIdentifier.TryParse(m_name, out m_ii))
            {
                throw new ArgumentException($"SingleItemPackage does not specify a valid Item ID. folderName='{m_name}'");
            }

            // This two-step method fills in the internal variables
            var ff = new FsFolder(rootFolder);
            ff.TryGetFolder(rootFolder, out m_folder);
        }

        public override string Name
        {
            get { return m_name; }
        }

        public override bool TryGetItem(ItemIdentifier ii, out FileFolder out_ff)
        {
            if (ii != m_ii)
            {
                out_ff = null;
                return false;
            }

            out_ff = m_folder;
            return true;
        }

        protected override IEnumerator<ItemIdentifier> GetItemEnumerator()
        {
            return new List<ItemIdentifier>()
            {
                m_ii
            }.GetEnumerator();
        }

        public override void Dispose()
        {
            // Nothing to do here
        }
    }
}
