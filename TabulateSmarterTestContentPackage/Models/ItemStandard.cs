namespace TabulateSmarterTestContentPackage.Models
{
    public class ItemStandard
    {
        public string Standard { get; set; } = string.Empty;
        public string Claim { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string ContentDomain { get; set; } = string.Empty;

        public override bool Equals(object obj)
        {
            if (obj.GetType() != typeof(ItemStandard))
            {
                return false;
            }
            var other = obj as ItemStandard;
            return Standard.Equals(other.Standard) && 
                Claim.Equals(other.Claim) && 
                Target.Equals(other.Target) &&
                ContentDomain.Equals(other.ContentDomain);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}