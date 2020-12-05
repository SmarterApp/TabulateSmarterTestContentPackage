using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using TabulateSmarterTestContentPackage.Models;
using TabulateSmarterTestContentPackage.Extractors;
using TabulateSmarterTestContentPackage.Utilities;


namespace TabulateSmarterTestContentPackage.Validators
{
    static class VersionValidator
    {
        public static void Validate(ItemContext it, XmlDocument xml, XmlDocument xmlMetadata)
        {
            // Get the version
            var itemVersion = xml.XpEvalE(
                it.IsStimulus ? "itemrelease/passage/@version"
                : "itemrelease/item/@version");
            it.Version = itemVersion; // Make version available for error reporting.

            // Get metadata version (which includes the minor version number)
            var metadataVersion = xmlMetadata.XpEvalE("metadata/sa:smarterAppMetadata/sa:Version", Tabulator.XmlNsMgr);

            // Check for consistency between the version number in item xml and metadata xml.
            // The metadata XML stores the version number in "major.minor" format, while 
            // the item xml stores the version in "major" format. Only the "major" number
            // is to be compared.
            var metadataVersionValues = metadataVersion.Split('.');
            if (!itemVersion.Equals(metadataVersionValues[0]))
            {
                ReportingUtility.ReportError(it, ErrorId.T0114, "Item version='{0}' Metadata major version='{1}'", itemVersion, metadataVersionValues[0]);
            }
            else
            {
                it.Version = metadataVersion; // Update to include minor version number when present
            }
        }
    }

}