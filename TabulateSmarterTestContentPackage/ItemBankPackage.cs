using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TabulateSmarterTestContentPackage.Models;
using System.Xml.Linq;

namespace TabulateSmarterTestContentPackage
{
    class ItemBankPackage : TestPackage
    {
        string m_name;
        GitLab m_gitLab;
        string m_namespace;

        public ItemBankPackage(string url, string accessToken, string ns)
        {
            var uri = new Uri(url);
            m_name = uri.Host;
            m_gitLab = new GitLab(url, accessToken);
            m_namespace = ns;
            m_gitLab.VerifyAccess(m_namespace);
        }

        public override string Name => m_name;

        public override bool TryGetItem(ItemIdentifier ii, out FileFolder ff)
        {
            try
            {
                if (ii == null) // Attempt to get root folder for manifest. Can't do that on an item bank.
                {
                    ff = null;
                    return false;
                }

                string projectId = m_gitLab.ProjectIdFromName(m_namespace, ii.FullId);
                ff = new ItemBankProject(this, ii, projectId);
                return true;
            }
            catch (HttpNotFoundException)
            {
                ff = null;
                return false;
            }
        }

        protected override IEnumerator<ItemIdentifier> GetItemEnumerator()
        {
            return new PackageItemEnumerator(m_gitLab.GetProjectsInNamespace(m_namespace).GetEnumerator());
        }

        public override void Dispose()
        {
            // Nothing to do.
        }

        private class PackageItemEnumerator : IEnumerator<ItemIdentifier>
        {
            IEnumerator<XElement> m_enum;
            ItemIdentifier m_current;

            public PackageItemEnumerator(IEnumerator<XElement> enumerator)
            {
                m_enum = enumerator;
            }

            public ItemIdentifier Current => m_current;

            object IEnumerator.Current => m_current;

            public bool MoveNext()
            {
                for (; ; )
                {
                    if (!m_enum.MoveNext())
                    {
                        m_current = null;
                        return false;
                    }

                    var ele = m_enum.Current.Element("name");
                    if (ele == null) continue;

                    if (!ItemIdentifier.TryParse(ele.Value, out m_current)) continue;

                    return true;
                }
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }

            public void Dispose()
            {
                if (m_enum != null)
                {
                    m_enum.Dispose();
                }
                m_enum = null;
            }
        }


        private class ItemBankProject : FileFolder
        {
            ItemBankPackage m_package;
            string m_projectId;
            ItemBankFolder m_root;

            public ItemBankProject(ItemBankPackage package, ItemIdentifier ii, string projectId)
                : base("/" + ii.FolderName, ii.FullId)
            {
                m_package = package;
                m_projectId = projectId;
            }

            public override ICollection<FileFolder> Folders
            {
                get
                {
                    if (m_root == null) LoadTree();
                    return m_root.Folders;
                }
            }

            public override ICollection<FileFile> Files
            {
                get
                {
                    if (m_root == null) LoadTree();
                    return m_root.Files;
                }
            }

            public override bool TryGetFile(string path, out FileFile value)
            {
                if (m_root == null) LoadTree();
                return m_root.TryGetFile(path, out value);
            }

            public override bool TryGetFolder(string path, out FileFolder value)
            {
                if (m_root == null) LoadTree();
                return m_root.TryGetFolder(path, out value);
            }

            private void LoadTree()
            {
                m_root = new ItemBankFolder(this, RootedName, Name);
                foreach (var pair in m_package.m_gitLab.ListRepositoryTree(m_projectId))
                {
                    System.Diagnostics.Debug.Assert(pair.Key.IndexOf('\\') < 0);    // Should use forward slashes exclusively
                    m_root.Add(pair.Key.ToLower(), pair.Value);
                }
            }

            private class ItemBankFolder : FileFolder
            {
                ItemBankProject m_project;
                Dictionary<string, FileFolder> m_folders = new Dictionary<string, FileFolder>();
                Dictionary<string, FileFile> m_files = new Dictionary<string, FileFile>();

                public ItemBankFolder(ItemBankProject package, string rootedName, string name)
                    : base(rootedName, name)
                {
                    m_project = package;
                }

                public void Add(string path, string blobId)
                {
                    // If has directory path, recursively add
                    int slash = path.IndexOf('/');
                    if (slash >= 0)
                    {
                        string name = path.Substring(0, slash);
                        string tail = path.Substring(slash + 1);

                        FileFolder folder;
                        if (!m_folders.TryGetValue(name, out folder))
                        {
                            string subRootedName = (RootedName.Length <= 1) ? string.Concat("/", name) : string.Concat(RootedName, "/", name);
                            folder = new ItemBankFolder(m_project, subRootedName, name);
                            m_folders.Add(name, folder);
                        }
                        ((ItemBankFolder)folder).Add(tail, blobId);
                    }

                    // Otherwise add file
                    else
                    {
                        m_files[path] = new ItemBankFile(m_project, string.Concat(RootedName, "/", path), path, blobId);
                    }
                }

                public override ICollection<FileFolder> Folders => m_folders.Values;

                public override ICollection<FileFile> Files => m_files.Values;

                public override bool TryGetFile(string path, out FileFile value)
                {
                    ItemBankFolder folder;
                    string name;
                    if (!FollowPath(path, out folder, out name)
                        || !folder.m_files.TryGetValue(name, out value))
                    {
                        value = null;
                        return false;
                    }
                    return true;
                }

                public override bool TryGetFolder(string path, out FileFolder value)
                {
                    ItemBankFolder folder;
                    string name;
                    if (!FollowPath(path, out folder, out name)
                        || !folder.m_folders.TryGetValue(name, out value))
                    {
                        value = null;
                        return false;
                    }
                    return true;
                }

                private bool FollowPath(string path, out ItemBankFolder folder, out string name)
                {
                    string[] parts = path.ToLowerInvariant().Split('/');
                    if (parts.Length < 1) throw new FileNotFoundException("Empty path: " + path);
                    if (parts[0].Length == 0) throw new FileNotFoundException("Absolute paths not supported: " + path);

                    ItemBankFolder f = this;
                    for (int i = 0; i < parts.Length - 1; ++i)
                    {
                        FileFolder next;
                        if (!f.m_folders.TryGetValue(parts[i], out next))
                        {
                            folder = null;
                            name = null;
                            return false;
                        }
                        f = (ItemBankFolder)next;
                    }

                    folder = f;
                    name = parts[parts.Length - 1];
                    return true;
                }
            } // ItemBankProject.ItemBankFolder

            private class ItemBankFile : FileFile
            {
                ItemBankProject m_project;
                string m_blobId;

                // TODO: Figure out how to get length without reading the whole blob. Possibly an HTTP HEAD.
                long m_length = -1;

                public ItemBankFile(ItemBankProject project, string rootedName, string name, string blobId)
                    : base(rootedName, name)
                {
                    m_project = project;
                    m_blobId = blobId;
                }

                public override long Length
                {
                    get
                    {
                        if (m_length < 0)
                        {
                            //m_length = m_project.m_package.m_gitLab.GetBlobSize(m_project.m_projectId, m_blobId);
                            m_length = 0;   // The call to GetBlobSize is expensive. Until we find a better answer, just set it to zero.
                        }
                        return m_length;
                    }
                }

                public override Stream Open()
                {
                    return m_project.m_package.m_gitLab.ReadBlob(m_project.m_projectId, m_blobId, out m_length);
                }
            } // ItemBankProject.ItemBankFile

        } // ItemBankProject

    }
}
