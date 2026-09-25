using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlPunchoutSetupResponse
{
    [XmlElement("StartPage")]
    public CxmlStartPage StartPage { get; set; }
}
