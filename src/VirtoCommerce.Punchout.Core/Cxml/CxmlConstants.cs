namespace VirtoCommerce.Punchout.Core.Cxml;

public static class CxmlConstants
{
    public const string RootElementName = "cXML";

    public const string DtdSystemId = "http://xml.cxml.org/schemas/cXML/1.2.041/cXML.dtd";

    /// <summary>
    /// cXML timestamps are ISO 8601 with an explicit offset, e.g. 2026-09-14T13:36:29+00:00.
    /// </summary>
    public const string TimestampFormat = "yyyy-MM-ddTHH:mm:sszzz";

    /// <summary>
    /// cXML carries the outcome in a Status element whose code and text mirror the HTTP status codes.
    /// </summary>
    public static class Status
    {
        public const string OkCode = "200";
        public const string OkText = "OK";

        public const string BadRequestCode = "400";
        public const string BadRequestText = "Bad Request";

        public const string UnauthorizedCode = "401";
        public const string UnauthorizedText = "Unauthorized";

        public const string InternalServerErrorCode = "500";
        public const string InternalServerErrorText = "Internal Server Error";
    }
}
