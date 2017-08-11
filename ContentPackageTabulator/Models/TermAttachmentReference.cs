namespace ContentPackageTabulator.Models
{
    public class TermAttachmentReference
    {
        public TermAttachmentReference(int termIndex, string listType, string filename)
        {
            TermIndex = termIndex;
            ListType = listType;
            Filename = filename;
        }

        public int TermIndex { get; set; }
        public string ListType { get; set; }
        public string Filename { get; set; }
    }
}