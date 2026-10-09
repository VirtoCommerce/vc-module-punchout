using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlPunchoutOrderMessageHeader
{
    /// <summary>
    /// 'create', 'inspect' or 'edit'.
    /// </summary>
    [XmlAttribute("operationAllowed")]
    public string OperationAllowed { get; set; }

    [XmlElement("Total")]
    public CxmlAmount Total { get; set; }
}
