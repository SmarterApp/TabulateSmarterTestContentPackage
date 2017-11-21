namespace TabulateSmarterTestContentPackage.Models
{
    public class ItemStandard
    {
        public string Standard = string.Empty;
        public string Subject = string.Empty;
        public string Grade = string.Empty;
        public string Claim = string.Empty;
        public string Target = string.Empty;
        public string ContentDomain = string.Empty;
        public string ContentCategory = string.Empty;
        public string Emphasis = string.Empty;
        public string TargetSet = string.Empty;
        public string CCSS = string.Empty;

        public bool IsEmpty
        {
            get
            {
                return string.IsNullOrEmpty(Standard)
                    && string.IsNullOrEmpty(Subject)
                    && string.IsNullOrEmpty(Grade)
                    && string.IsNullOrEmpty(Claim)
                    && string.IsNullOrEmpty(Target)
                    && string.IsNullOrEmpty(ContentDomain)
                    && string.IsNullOrEmpty(ContentCategory)
                    && string.IsNullOrEmpty(Emphasis)
                    && string.IsNullOrEmpty(TargetSet)
                    && string.IsNullOrEmpty(CCSS);
            }
        }
    }
}