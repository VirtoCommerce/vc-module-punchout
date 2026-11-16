using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace VirtoCommerce.Punchout.Data.Cxml;

public static class CxmlSanitizer
{
    /// <summary>
    /// Credential secrets allowed by the cXML DTD: Credential (SharedSecret | DigitalSignature | CredentialMac)?
    /// </summary>
    public static readonly IReadOnlySet<string> SecretElementNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "SharedSecret",
        "DigitalSignature",
        "CredentialMac",
    };

    private static readonly XmlReaderSettings _readerSettings = new()
    {
        DtdProcessing = DtdProcessing.Ignore,
        XmlResolver = null,
    };

    /// <summary>
    /// Returns the document without credential secrets, or null when it cannot be parsed
    /// </summary>
    public static string RemoveSecrets(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        XDocument document;

        try
        {
            using var stringReader = new StringReader(xml);
            using var xmlReader = XmlReader.Create(stringReader, _readerSettings);

            document = XDocument.Load(xmlReader, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException)
        {
            return null;
        }

        document
            .Descendants()
            .Where(x => SecretElementNames.Contains(x.Name.LocalName))
            .ToList()
            .Remove();

        // The DOCTYPE is not kept, DTD processing is disabled for security
        var body = document.ToString(SaveOptions.DisableFormatting);

        return document.Declaration is null ? body : $"{document.Declaration}\n{body}";
    }
}
