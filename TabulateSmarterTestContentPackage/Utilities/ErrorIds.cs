using System;
using System.Collections.Generic;
using TabulateSmarterTestContentPackage.Models;

namespace TabulateSmarterTestContentPackage.Utilities
{
    static class Errors
    {
        public static readonly ItemIdentifier ManifestItemId = new ItemIdentifier("item", 0, 0);

        public static ErrorInfo[] ErrorTable = new ErrorInfo[]
        {
            new ErrorInfo(/* 0000 */ ErrorId.None, string.Empty, ErrorCategory.System, ErrorSeverity.Message, ErrorReviewArea.None),
            new ErrorInfo(ErrorId.T0001, "Allow Calculator field not present for MATH subject item", ErrorCategory.Metadata, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(/* 0002 */ ErrorId.None, string.Empty, ErrorCategory.System, ErrorSeverity.Message, ErrorReviewArea.None),
            new ErrorInfo(/* 0003 */ ErrorId.None, string.Empty, ErrorCategory.System, ErrorSeverity.Message, ErrorReviewArea.None),
            new ErrorInfo(ErrorId.T0004, "Attachment missing type attribute.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0005, "Audio Glossary file is not in expected format.", ErrorCategory.Wordlist, ErrorSeverity.Degraded, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0006, "Automatic/HandScored scoring metadata error.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0007, "Braille embossing attachment has unknown subtype.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Braille),
            new ErrorInfo(ErrorId.T0008, "Braille embossing file is missing.", ErrorCategory.Item, ErrorSeverity.Tolerable, ErrorReviewArea.Braille),
            new ErrorInfo(ErrorId.T0009, "Braille embossing filename doesn't match expected braille type.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Braille),
            new ErrorInfo(ErrorId.T0010, "Braille embossing filename indicates item ID mismatch.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Braille),
            new ErrorInfo(/* 0011 */ ErrorId.None, string.Empty, ErrorCategory.System, ErrorSeverity.Message, ErrorReviewArea.None),
            new ErrorInfo(ErrorId.T0012, "Burmese translated glossary uses Zawgyi characters, should be Unicode.", ErrorCategory.Wordlist, ErrorSeverity.Degraded, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0013, "CCSS standard is missing from item.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0014, "Dangling reference to attached file that does not exist.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0015, "Duplicate attachment IDs in attachmentlist element", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0016, "Expected blank CCSS for Math Claim 2, 3, or 4", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0017, "Glossary tags overlap or are nested.", ErrorCategory.Item, ErrorSeverity.Tolerable, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0018, "Img element does not contain an id attribute necessary to provide alt text.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Tts),
            new ErrorInfo(ErrorId.T0019, "Img element does not reference alt text for braille presentation (no corresponding brailleText element).", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Braille),
            new ErrorInfo(ErrorId.T0020, "Img element for a graphic resource has an empty audioText element.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Tts),
            new ErrorInfo(ErrorId.T0021, "Img element does not reference alt text for text-to-speech (no corresponding readAloud element).", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Tts),
            new ErrorInfo(ErrorId.T0022, "Img element for a graphic resource has an empty textToSpeechPronunciation element.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Tts),
            new ErrorInfo(ErrorId.T0023, "Incorrect ScoringEngine metadata.", ErrorCategory.Metadata, ErrorSeverity.Benign, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0024, "Invalid grade attribute.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0025, "Invalid item file.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(/* 0026 */ ErrorId.None, string.Empty, ErrorCategory.System, ErrorSeverity.Message, ErrorReviewArea.None),
            new ErrorInfo(/* 0027 */ ErrorId.None, string.Empty, ErrorCategory.System, ErrorSeverity.Message, ErrorReviewArea.None),
            new ErrorInfo(/* 0028 */ ErrorId.None, string.Empty, ErrorCategory.System, ErrorSeverity.Message, ErrorReviewArea.None),
            new ErrorInfo(/* 0029 */ ErrorId.None, string.Empty, ErrorCategory.System, ErrorSeverity.Message, ErrorReviewArea.None),
            new ErrorInfo(ErrorId.T0030, "Item content includes language but metadata does not have a corresponding <Language> entry.", ErrorCategory.Metadata, ErrorSeverity.Benign, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0031, "Item does not have any answer options.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0032, "Item has ASL but not indicated in the metadata.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Asl),
            new ErrorInfo(ErrorId.T0033, "Item has improper TTS Silencing Tag", ErrorCategory.Item, ErrorSeverity.Tolerable, ErrorReviewArea.Tts),
            new ErrorInfo(ErrorId.T0034, "Item has terms marked for glossary but does not reference a wordlist.", ErrorCategory.Item, ErrorSeverity.Benign, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0035, "Item lacks QRX scoring key but not marked as HandScored.", ErrorCategory.AnswerKey, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0036, "Item metadata indicates language but item content does not include that language.", ErrorCategory.Metadata, ErrorSeverity.Severe, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0037, "Item metadata specifies ASL but no ASL in item.", ErrorCategory.Metadata, ErrorSeverity.Severe, ErrorReviewArea.Asl),
            new ErrorInfo(ErrorId.T0038, "Item Point attribute (item_att_Item Point) not found.", ErrorCategory.Item, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0039, "Item Point attribute does not begin with an integer.", ErrorCategory.Item, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0040, "Item references non-existent wordlist (WIT)", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0041, "Item references non-existent wordlist term.", ErrorCategory.Wordlist, ErrorSeverity.Benign, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0042, "Item stimulus ID doesn't match metadata AssociatedStimulus.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0043, "Item text does not match wordlist term.", ErrorCategory.Wordlist, ErrorSeverity.Degraded, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0044, "Item type not specified.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0045, "Machine scoring file found but not referenced in <MachineRubric> element.", ErrorCategory.AnswerKey, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0046, "Machine scoring file not found.", ErrorCategory.AnswerKey, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0047, "Math Claim 2, 3, 4 primary alignment should be paired with a claim 1 secondary alignment.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0048, "Mathematical Practice field not present for MATH claim 2, 3, or 4 item", ErrorCategory.Metadata, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0049, "MaximumNumberOfPoints field not present in metadata", ErrorCategory.Metadata, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0050, "MaximumNumberOfPoints for WER item exceeds 10.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0051, "Metadata for PT item is missing <PtWritingType> element.", ErrorCategory.Metadata, ErrorSeverity.Benign, ErrorReviewArea.Content),
            new ErrorInfo(/* 0052 */ ErrorId.None, string.Empty, ErrorCategory.System, ErrorSeverity.Message, ErrorReviewArea.None),
            new ErrorInfo(ErrorId.T0053, "Metadata indicates no braille but braille content included.", ErrorCategory.Metadata, ErrorSeverity.Benign, ErrorReviewArea.Braille),
            new ErrorInfo(ErrorId.T0054, "Metadata ScorePoints doesn't include a maximum score.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0055, "Metadata ScorePoints doesn't include a zero score.", ErrorCategory.Metadata, ErrorSeverity.Benign, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0056, "Metadata ScorePoints value is not integer.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0057, "Metadata ScorePoints value is out of range.", ErrorCategory.Metadata, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0058, "Missing <IntendedGrade> in item metadata.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0059, "Missing grade in item attributes (itm_att_Grade).", ErrorCategory.Attribute, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0060, "Missing subject in item attributes (itm_item_subject).", ErrorCategory.Attribute, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0061, "Missing subject in item metadata.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0062, "No PrimaryStandard found for StandardPublication.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0063, "PerformanceTaskComponentItem metadata should be 'Y' or 'N'.", ErrorCategory.Metadata, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(/* 0064 */ ErrorId.None, string.Empty, ErrorCategory.System, ErrorSeverity.Message, ErrorReviewArea.None),
            new ErrorInfo(/* 0065 */ ErrorId.None, string.Empty, ErrorCategory.System, ErrorSeverity.Message, ErrorReviewArea.None),
            new ErrorInfo(/* 0066 */ ErrorId.None, string.Empty, ErrorCategory.System, ErrorSeverity.Message, ErrorReviewArea.None),
            new ErrorInfo(ErrorId.T0067, "Rubric is missing for Hand-scored or QRX-scored item.", ErrorCategory.AnswerKey, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0068, "Same wordlist attachment used for different languages or types.", ErrorCategory.Wordlist, ErrorSeverity.Severe, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0069, "ScorePoints value missing leading quote.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0070, "ScorePoints value missing trailing quote.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(/* 0071 */ ErrorId.None, string.Empty, ErrorCategory.System, ErrorSeverity.Message, ErrorReviewArea.None),
            new ErrorInfo(/* 0072 */ ErrorId.None, string.Empty, ErrorCategory.System, ErrorSeverity.Message, ErrorReviewArea.None),
            new ErrorInfo(ErrorId.T0073, "Standards from different publications don't match.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0074, "Term that is tagged for glossary is not tagged when it occurs elsewhere in the item.", ErrorCategory.Item, ErrorSeverity.Tolerable, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0075, "Translated glossary entry lacks audio.", ErrorCategory.Wordlist, ErrorSeverity.Degraded, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0076, "Tutorial id missing from item.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0077, "Two different wordlist terms reference the same attachment.", ErrorCategory.Wordlist, ErrorSeverity.Severe, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0078, "Unexpected EBSR answer key attribute.", ErrorCategory.AnswerKey, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0079, "Unexpected EBSR Key Part II attribute.", ErrorCategory.AnswerKey, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0080, "Unexpected item type.", ErrorCategory.Unsupported, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0081, "Unexpected MC answer key attribute.", ErrorCategory.AnswerKey, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0082, "Unexpected MS answer attribute.", ErrorCategory.AnswerKey, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0083, "Unreferenced file found.", ErrorCategory.Item, ErrorSeverity.Benign, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0084, "Wordlist attachment filename indicates wordlist ID mismatch.", ErrorCategory.Wordlist, ErrorSeverity.Degraded, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0085, "Wordlist attachment not found.", ErrorCategory.Wordlist, ErrorSeverity.Severe, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0086, "Wordlist audio filename differs in capitalization (will fail on certain platforms).", ErrorCategory.Wordlist, ErrorSeverity.Degraded, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0087, "Wordlist audio filename indicates attachment language mismatch.", ErrorCategory.Wordlist, ErrorSeverity.Degraded, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0088, "Wordlist has multiple terms with the same index.", ErrorCategory.Wordlist, ErrorSeverity.Severe, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0089, "Wordlist is not referenced by any item.", ErrorCategory.Wordlist, ErrorSeverity.Benign, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0090, "WordList reference missing end tag.", ErrorCategory.Item, ErrorSeverity.Tolerable, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0091, "WordList reference term index is not integer", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0092, "Wordlist term does not include all expected translations.", ErrorCategory.Wordlist, ErrorSeverity.Tolerable, ErrorReviewArea.Language),
            new ErrorInfo(/* 0093 */ ErrorId.None, string.Empty, ErrorCategory.System, ErrorSeverity.Message, ErrorReviewArea.None),
            new ErrorInfo(ErrorId.T0094, "Metadata <PtSequence> is not an integer.", ErrorCategory.Metadata, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0095, "Resource listed multiple times in manifest.", ErrorCategory.Manifest, ErrorSeverity.Benign, ErrorReviewArea.Lead),
            new ErrorInfo(/* 0096 */ ErrorId.None, string.Empty, ErrorCategory.System, ErrorSeverity.Message, ErrorReviewArea.None),
            new ErrorInfo(ErrorId.T0097, "Img element for a graphic has an empty brailleTextString element.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Tts),
            new ErrorInfo(ErrorId.T0098, "Resource specified in manifest does not exist.", ErrorCategory.Manifest, ErrorSeverity.Benign, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0099, "Tutorial not found.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0100, "Item not found in manifest.", ErrorCategory.Manifest, ErrorSeverity.Benign, ErrorReviewArea.Lead),
            new ErrorInfo(/* 0101 */ ErrorId.None, string.Empty, ErrorCategory.System, ErrorSeverity.Message, ErrorReviewArea.None),
            new ErrorInfo(ErrorId.T0102, "Item stimulus not found.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0103, "Item does not appear in the manifest.", ErrorCategory.Manifest, ErrorSeverity.Benign, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0104, "Resource does not appear in the manifest.", ErrorCategory.Manifest, ErrorSeverity.Benign, ErrorReviewArea.Lead),
            new ErrorInfo(/* 0105 */ ErrorId.None, string.Empty, ErrorCategory.System, ErrorSeverity.Message, ErrorReviewArea.None),
            new ErrorInfo(ErrorId.T0106, "ASL video files must have 2 file references.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Asl),
            new ErrorInfo(ErrorId.T0107, "Invalid wordlist file.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0108, "Dependency in manifest repeated multiple times.", ErrorCategory.Manifest, ErrorSeverity.Benign, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0109, "File listed multiple times in manifest.", ErrorCategory.Manifest, ErrorSeverity.Benign, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0110, "Item not found in package.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0111, "Item type is not fully supported by the open source TDS.", ErrorCategory.Unsupported, ErrorSeverity.Severe, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0112, "Invalid metadata.xml.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0113, "Incorrect metadata <InteractionType>.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0114, "Major version number inconsistent between item and metadata.", ErrorCategory.Item, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0115, "Subject mismatch between item and metadata.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0116, "Grade mismatch between item and metadata.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0117, "Machine scoring file type is not supported.", ErrorCategory.AnswerKey, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0118, "Missing EBSR answer key part II attribute.", ErrorCategory.AnswerKey, ErrorSeverity.Benign, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0119, "Unexpected answer key attribute.", ErrorCategory.AnswerKey, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0120, "Validation of scoring keys for this type is not supported.", ErrorCategory.Unsupported, ErrorSeverity.Benign, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0121, "Capitalization error in ScoringEngine metadata.", ErrorCategory.Metadata, ErrorSeverity.Benign, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0122, "Unexpected machine scoring file found for selected-response or handscored item type.", ErrorCategory.AnswerKey, ErrorSeverity.Benign, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0123, "MaximumNumberOfPoints not found in metadata.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0124, "Metadata MaximumNumberOfPoints value is not integer.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0125, "Metadata MaximumNumberOfPoints does not match item point attribute.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0126, "ScorePoints not found in metadata.", ErrorCategory.Metadata, ErrorSeverity.Benign, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0127, "Metadata ScorePoints are not in ascending order.", ErrorCategory.Metadata, ErrorSeverity.Benign, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0128, "Item stimulus ID is not an integer.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0129, "PT Item missing associated passage ID (associatedpassage).", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0130, "Metadata for PT item is missing <PtSequence> element.", ErrorCategory.Metadata, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0131, "PtWritingType metadata has invalid value.", ErrorCategory.Metadata, ErrorSeverity.Benign, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0132, "Capitalization error in PtWritingType metadata.", ErrorCategory.Metadata, ErrorSeverity.Benign, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0133, "Stimulus not found in package.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0134, "Tutorial not found in package.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0135, "Unexpected extension for attached file.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0136, "Dependency not found in manifest.", ErrorCategory.Manifest, ErrorSeverity.Benign, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0137, "Manifest does not record dependency between items.", ErrorCategory.Manifest, ErrorSeverity.Benign, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0138, "Attachment missing id attribute.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0139, "More than one braille embossing file type in attachment list", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Braille),
            new ErrorInfo(ErrorId.T0140, "Attachment missing file attribute.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0141, "Braille embossing filename has unexpected extension.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Braille),
            new ErrorInfo(ErrorId.T0142, "Braille subtype prefix is deprecated.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Braille),
            new ErrorInfo(ErrorId.T0143, "Multiple braille embossing files of same form.", ErrorCategory.Item, ErrorSeverity.Tolerable, ErrorReviewArea.Braille),
            new ErrorInfo(ErrorId.T0144, "Braille embossing filename does not match naming convention.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Braille),
            new ErrorInfo(ErrorId.T0145, "Braille embossing filename indicates item/stim mismatch.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Braille),
            new ErrorInfo(ErrorId.T0146, "Braille embossing filename transcript naming convention doesn't match subtype", ErrorCategory.Item, ErrorSeverity.Tolerable, ErrorReviewArea.Braille),
            new ErrorInfo(ErrorId.T0147, "Braille embossing filename extension does not match type", ErrorCategory.Item, ErrorSeverity.Tolerable, ErrorReviewArea.Braille),
            new ErrorInfo(ErrorId.T0148, "Braille transcript does not include the same forms as braille stem.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Braille),
            new ErrorInfo(ErrorId.T0149, "Metadata indicates not braillable but braille content included.", ErrorCategory.Metadata, ErrorSeverity.Benign, ErrorReviewArea.Braille),
            new ErrorInfo(ErrorId.T0150, "Metadata indicates braille support but no braille content included.", ErrorCategory.Metadata, ErrorSeverity.Degraded, ErrorReviewArea.Braille),
            new ErrorInfo(ErrorId.T0151, "Item has no content element.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0152, "Item references blank wordList id or bankkey.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0153, "Item references multiple wordlists.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0154, "WordList reference lacks an ID", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0155, "Invalid html content.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0156, "Media file not referenced in item.", ErrorCategory.Item, ErrorSeverity.Benign, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0157, "Not a content package; 'imsmanifest.xml' must exist in root.", ErrorCategory.Manifest, ErrorSeverity.Severe, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0158, "Invalid manifest.", ErrorCategory.Manifest, ErrorSeverity.Benign, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0159, "Resource in manifest is missing id.", ErrorCategory.Manifest, ErrorSeverity.Benign, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0160, "Resource specified in manifest has no filename.", ErrorCategory.Manifest, ErrorSeverity.Benign, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0161, "Dependency in manifest is missing identifierref attribute.", ErrorCategory.Manifest, ErrorSeverity.Benign, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0162, "Manifest does not list any resources.", ErrorCategory.Manifest, ErrorSeverity.Benign, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0163, "Manifest does not express resource dependency.", ErrorCategory.Manifest, ErrorSeverity.Benign, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0164, "WordList not found in package.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0165, "WordList reference is to a non-wordList item.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0166, "Wordlist file id mismatch.", ErrorCategory.Wordlist, ErrorSeverity.Severe, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0167, "Wordlist term is not referenced by item.", ErrorCategory.Wordlist, ErrorSeverity.Benign, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0168, "Wordlist attachment filename indicates term index mismatch.", ErrorCategory.Wordlist, ErrorSeverity.Degraded, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0169, "Illustration glossary entry does not include image.", ErrorCategory.Wordlist, ErrorSeverity.Degraded, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0170, "Unreferenced wordlist attachment file.", ErrorCategory.Wordlist, ErrorSeverity.Benign, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0171, "Wordlist attachment not found. Benign because corresponding term is not referenced.", ErrorCategory.Wordlist, ErrorSeverity.Benign, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0172, "Wordlist attachment filename differs in capitalization. Benign because corresponding term is not referenced.", ErrorCategory.Wordlist, ErrorSeverity.Benign, ErrorReviewArea.Language),
            new ErrorInfo(ErrorId.T0173, "ASL video file name missing from the item attachment 'file' attribute.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Asl),
            new ErrorInfo(ErrorId.T0174, "ASL video files are missing from the attachment source list.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Asl),
            new ErrorInfo(ErrorId.T0175, "ASL video file name attribute value not found in source element.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Asl),
            new ErrorInfo(ErrorId.T0176, "ASL video file is missing.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Asl),
            new ErrorInfo(ErrorId.T0177, "ASL video filename contains an incorrect ID", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Asl),
            new ErrorInfo(ErrorId.T0178, "ASL video filename indicates item, but base folder is a stim", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Asl),
            new ErrorInfo(ErrorId.T0179, "ASL video filename indicates stim, but base folder is an item", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Asl),
            new ErrorInfo(ErrorId.T0180, "ASL video filename does not match expected pattern", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Asl),
            new ErrorInfo(ErrorId.T0181, "Item content has element that should not be present.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0182, "Item content has attribute that should not be present.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0183, "Item content has style property that should not be present.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0184, "Img element for an equation resource has an empty audioShortDesc element.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0185, "Img element for an equation resource does not have an audioShortDesc child element within the readAloud element.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0186, "Img element for a graphic resource does not have a textToSpeechPronunciation child element within the readAloud element.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0187, "Img element for a graphic resource does not have a audioText child element within the readAloud element.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0188, "Img element for a graphic resource does not have a brailleTextString child element within the brailleText element.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0189, "Found more than one PrimaryStandard.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0190, "Standard ID failed to parse.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0191, "Standard ID has correctable error.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0192, "Standard ID validation error.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0193, "Math Claim 2, 3, 4 primary alignment is missing CCSS standard.", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.TabulatorStart, "Tabulator Start", ErrorCategory.System, ErrorSeverity.Message, ErrorReviewArea.None),
            new ErrorInfo(ErrorId.Exception, "Exception Thrown", ErrorCategory.Exception, ErrorSeverity.Severe, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0196, "TutorialId is not expected value for item type.", ErrorCategory.Exception, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0197, "ASL video file is empty.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Asl),
            new ErrorInfo(ErrorId.T0198, "Rubric is blank or is an empty template.", ErrorCategory.AnswerKey, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0199, "Attached file is empty (size=0).", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0200, "Multiple sets of TTS information for a single its-tag", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Tts),
            new ErrorInfo(ErrorId.T0201, "Attached file not found.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0202, "Braille PRN file is not compatable with Tiger embossers.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Braille),
            new ErrorInfo(ErrorId.T0203, "Format of braille files does not match Metadata.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Braille),
            new ErrorInfo(ErrorId.T0204, "Braille embossing file has a Braille Form Code that is not permitted for this subject.", ErrorCategory.Item, ErrorSeverity.Benign, ErrorReviewArea.Braille),
            new ErrorInfo(ErrorId.T0205, "Missing braille embossing file for a required Braille Form Code for this subject.", ErrorCategory.Item, ErrorSeverity.Benign, ErrorReviewArea.Braille),
            new ErrorInfo(ErrorId.T0206, "Filenames differ only by case.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0207, "Item content has CSS class that should not be present.", ErrorCategory.Item, ErrorSeverity.Benign, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0208, "Resource referenced in html content not found in package.", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0209, "Incorrect editor for Short Answer item", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0210, "Unknown or prohibited HTML tag in content", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0211, "SA item lacks a valid RendererSpec (GAX)", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0212, "Folder name contains incorrect letter case", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0213, "ASL video file type mismatch", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Asl),
            new ErrorInfo(ErrorId.T0214, "Failed to parse XML file", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0215, "Unreferenced MathML error", ErrorCategory.Item, ErrorSeverity.Tolerable, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0216, "Student-facing MathML error", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0217, "Educator-facing MathML error", ErrorCategory.Item, ErrorSeverity.Tolerable, ErrorReviewArea.Lead),
            new ErrorInfo(ErrorId.T0218, "Failed to parse XML node", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0219, "Unbolded text in MI table", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0220, "Listening stimulus with an unsupported image slideshow", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0221, "Image referenced in GAX file not found", ErrorCategory.Item, ErrorSeverity.Severe, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0222, "Multiple references to the same attachment", ErrorCategory.Item, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0223, "Invalid DepthOfKnowledge metadata", ErrorCategory.Metadata, ErrorSeverity.Tolerable, ErrorReviewArea.Content),
        };

        const string c_errIdPrefix = "CTAB-";

        public static bool TryParseErrorId(string value, out ErrorId id)
        {
            if (value.StartsWith(c_errIdPrefix, StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(c_errIdPrefix.Length);
            }
            int intVal;
            if (!int.TryParse(value, out intVal))
            {
                id = ErrorId.None;
                return false;
            }
            id = (ErrorId)intVal;
            return Enum.IsDefined(typeof(ErrorId), id);
        }

        public static string ErrorIdToString(ErrorId errId)
        {
            return $"{c_errIdPrefix}{(int)errId:d4}";
        }

        // Values: 1=enabled, 0=disabled, -1=disabledByDefault
        // If not present defaults to enabled.
        private static Dictionary<ErrorId, int> s_errorOptions = new Dictionary<ErrorId, int>();

        public static void Enable(ErrorId id)
        {
            s_errorOptions[id] = 1;
        }

        public static void Enable(string errorId)
        {
            if (TryParseErrorId(errorId, out ErrorId id))
            {
                Enable(id);
            }
        }

        public static void Disable(ErrorId id)
        {
            s_errorOptions[id] = 0;
        }

        public static void Disable(string errorId)
        {
            if (TryParseErrorId(errorId, out ErrorId id))
            {
                Disable(id);
            }
        }

        public static void SetEnabled(ErrorId id, bool enabled)
        {
            s_errorOptions[id] = enabled ? 1 : 0;
        }

        public static void DisableByDefault(ErrorId id)
        {
            s_errorOptions[id] = -1;
        }

        public static void EnableAll()
        {
            s_errorOptions.Clear();
        }

        public static bool IsEnabled(ErrorId id)
        {
            int value;
            if (!s_errorOptions.TryGetValue(id, out value)) return true;
            return value > 0;
        }

        public static string ReportOptions()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var option in s_errorOptions)
            {
                if (sb.Length != 0) sb.Append(' ');
                if (option.Value >= 0) // Don't report defaulted values
                {
                    sb.Append($"{ErrorIdToString(option.Key)}({((option.Value > 0) ? "On" : "Off")})");
                }
            }
            return sb.ToString();
        }

#if DEBUG
        // Validate that the ID of each error in the table matches its position.
        static Errors()
        {
            for (int i = 0; i < ErrorTable.Length; ++i)
            {
                if (ErrorTable[i].Id != ErrorId.None && (int)ErrorTable[i].Id != i)
                {
                    // Really, really notify about the problem.
                    string message = $"Errors.ErrorTable: ID doesn't match position in table. id={ErrorTable[i].Id} position={i}";
                    Console.WriteLine(message);
                    System.Diagnostics.Debug.Fail(message);
                    throw new ApplicationException(message);
                }
            }
        }
#endif
    }

    // The only reason for this enum is to detect at compile-time that only defined IDs are used.
    public enum ErrorId : int
    {
        None = 0,
        T0001 = 1,
        T0004 = 4,
        T0005 = 5,
        T0006 = 6,
        T0007 = 7,
        T0008 = 8,
        T0009 = 9,
        T0010 = 10,
        T0012 = 12,
        T0013 = 13,
        T0014 = 14,
        T0015 = 15,
        T0016 = 16,
        T0017 = 17,
        T0018 = 18,
        T0019 = 19,
        T0020 = 20,
        T0021 = 21,
        T0022 = 22,
        T0023 = 23,
        T0024 = 24,
        T0025 = 25,
        T0030 = 30,
        T0031 = 31,
        T0032 = 32,
        T0033 = 33,
        T0034 = 34,
        T0035 = 35,
        T0036 = 36,
        T0037 = 37,
        T0038 = 38,
        T0039 = 39,
        T0040 = 40,
        T0041 = 41,
        T0042 = 42,
        T0043 = 43,
        T0044 = 44,
        T0045 = 45,
        T0046 = 46,
        T0047 = 47,
        T0048 = 48,
        T0049 = 49,
        T0050 = 50,
        T0051 = 51,
        T0053 = 53,
        T0054 = 54,
        T0055 = 55,
        T0056 = 56,
        T0057 = 57,
        T0058 = 58,
        T0059 = 59,
        T0060 = 60,
        T0061 = 61,
        T0062 = 62,
        T0063 = 63,
        T0067 = 67,
        T0068 = 68,
        T0069 = 69,
        T0070 = 70,
        T0073 = 73,
        T0074 = 74,
        T0075 = 75,
        T0076 = 76,
        T0077 = 77,
        T0078 = 78,
        T0079 = 79,
        T0080 = 80,
        T0081 = 81,
        T0082 = 82,
        T0083 = 83,
        T0084 = 84,
        T0085 = 85,
        T0086 = 86,
        T0087 = 87,
        T0088 = 88,
        T0089 = 89,
        T0090 = 90,
        T0091 = 91,
        T0092 = 92,
        T0094 = 94,
        T0095 = 95,
        T0097 = 97,
        T0098 = 98,
        T0099 = 99,
        T0100 = 100,
        T0102 = 102,
        T0103 = 103,
        T0104 = 104,
        T0106 = 106,
        T0107 = 107,
        T0108 = 108,
        T0109 = 109,
        T0110 = 110,
        T0111 = 111,
        T0112 = 112,
        T0113 = 113,
        T0114 = 114,
        T0115 = 115,
        T0116 = 116,
        T0117 = 117,
        T0118 = 118,
        T0119 = 119,
        T0120 = 120,
        T0121 = 121,
        T0122 = 122,
        T0123 = 123,
        T0124 = 124,
        T0125 = 125,
        T0126 = 126,
        T0127 = 127,
        T0128 = 128,
        T0129 = 129,
        T0130 = 130,
        T0131 = 131,
        T0132 = 132,
        T0133 = 133,
        T0134 = 134,
        T0135 = 135,
        T0136 = 136,
        T0137 = 137,
        T0138 = 138,
        T0139 = 139,
        T0140 = 140,
        T0141 = 141,
        T0142 = 142,
        T0143 = 143,
        T0144 = 144,
        T0145 = 145,
        T0146 = 146,
        T0147 = 147,
        T0148 = 148,
        T0149 = 149,
        T0150 = 150,
        T0151 = 151,
        T0152 = 152,
        T0153 = 153,
        T0154 = 154,
        T0155 = 155,
        T0156 = 156,
        T0157 = 157,
        T0158 = 158,
        T0159 = 159,
        T0160 = 160,
        T0161 = 161,
        T0162 = 162,
        T0163 = 163,
        T0164 = 164,
        T0165 = 165,
        T0166 = 166,
        T0167 = 167,
        T0168 = 168,
        T0169 = 169,
        T0170 = 170,
        T0171 = 171,
        T0172 = 172,
        T0173 = 173,
        T0174 = 174,
        T0175 = 175,
        T0176 = 176,
        T0177 = 177,
        T0178 = 178,
        T0179 = 179,
        T0180 = 180,
        T0181 = 181,
        T0182 = 182,
        T0183 = 183,
        T0184 = 184,
        T0185 = 185,
        T0186 = 186,
        T0187 = 187,
        T0188 = 188,
        T0189 = 189,
        T0190 = 190,
        T0191 = 191,
        T0192 = 192,
        T0193 = 193,
        TabulatorStart = 194,
        Exception = 195,
        T0196 = 196,
        T0197 = 197,
        T0198 = 198,
        T0199 = 199,
        T0200 = 200,
        T0201 = 201,
        T0202 = 202,
        T0203 = 203,
        T0204 = 204,
        T0205 = 205,
        T0206 = 206,
        T0207 = 207,
        T0208 = 208,
        T0209 = 209,
        T0210 = 210,
        T0211 = 211,
        T0212 = 212,
        T0213 = 213,
        T0214 = 214,
        T0215 = 215,
        T0216 = 216,
        T0217 = 217,
        T0218 = 218,
        T0219 = 219,
        T0220 = 220,
        T0221 = 221,
        T0222 = 222,
        T0223 = 223,
    }

    public enum ErrorReviewArea : int
    {
        None = 0,
        Lead = 1,
        Content = 2,
        Language = 3,
        Tts = 4,
        Braille = 5,
        Asl = 6
    }

    class ErrorInfo
    {
        public ErrorInfo(ErrorId id, string message, ErrorCategory category, ErrorSeverity severity, ErrorReviewArea reviewArea)
        {
            Id = id;
            Message = message;
            Category = category;
            Severity = severity;
            ReviewArea = reviewArea;
        }
        public readonly ErrorId Id;
        public readonly string Message;
        public readonly ErrorCategory Category;
        public readonly ErrorSeverity Severity;
        public readonly ErrorReviewArea ReviewArea;
    }
}
