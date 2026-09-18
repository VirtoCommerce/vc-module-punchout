using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using VirtoCommerce.Punchout.Core.Cxml;
using VirtoCommerce.Punchout.Core.Cxml.Models;
using VirtoCommerce.Punchout.Core.Cxml.Services;

namespace VirtoCommerce.Punchout.Data.Cxml.Services;

public class CxmlSerializer : ICxmlSerializer
{
    // cXML documents declare the cXML DTD. Ignore it instead of resolving it: the schema adds nothing to the
    // mapping, and fetching an external DTD would make deserialization depend on a remote host.
    private static readonly XmlReaderSettings _readerSettings = new()
    {
        DtdProcessing = DtdProcessing.Ignore,
        XmlResolver = null,
    };

    private static readonly XmlWriterSettings _writerSettings = new()
    {
        // Without the explicit UTF8Encoding(false) the writer emits a BOM into the string.
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        Indent = true,
        OmitXmlDeclaration = false,
    };

    // cXML has no namespace, so the xsi/xsd declarations XmlSerializer adds by default must be suppressed.
    private static readonly XmlSerializerNamespaces _emptyNamespaces = new([XmlQualifiedName.Empty]);

    public T Deserialize<T>(string xml)
    {
        var serializer = new XmlSerializer(typeof(T));

        using var stringReader = new StringReader(xml);
        using var xmlReader = XmlReader.Create(stringReader, _readerSettings);

        return (T)serializer.Deserialize(xmlReader);
    }

    public string Serialize<T>(T value)
    {
        var serializer = new XmlSerializer(typeof(T));

        using var stream = new MemoryStream();

        using (var writer = XmlWriter.Create(stream, _writerSettings))
        {
            if (value is CxmlDocument)
            {
                writer.WriteDocType(CxmlConstants.RootElementName, pubid: null, CxmlConstants.DtdSystemId, subset: null);
            }

            serializer.Serialize(writer, value, _emptyNamespaces);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
