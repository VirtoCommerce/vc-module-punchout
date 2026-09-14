using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class PunchoutSetupResponse
{
    [XmlElement("StartPage")]
    public StartPage StartPage { get; set; }
}

public class StartPage
{
    [XmlElement("URL")]
    public string Url { get; set; }
}
