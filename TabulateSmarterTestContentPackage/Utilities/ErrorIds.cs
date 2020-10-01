using TabulateSmarterTestContentPackage.Models;

namespace TabulateSmarterTestContentPackage.Utilities
{
    static class Errors
    {
        public static readonly ItemIdentifier ManifestItemId = new ItemIdentifier("item", 0, 0);

        static ErrorInfo[] ErrorTable = new ErrorInfo[]
        {
            new ErrorInfo(ErrorId.T0001, "Allow Calculator field not present for MATH subject item", ErrorCategory.Metadata, ErrorSeverity.Degraded, ErrorReviewArea.Content),
            new ErrorInfo(ErrorId.T0002, "ASL video length doesn't correlate with text length; possible mismatch.", ErrorCategory.Item, ErrorSeverity.Degraded, ErrorReviewArea.Asl),
        };
    }

    // The only reason for this enum is to detect at compile-time that only defined IDs are used.
    public enum ErrorId : int
    {
        None = 0,
        T0001 = 1,
        T0002 = 3,
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