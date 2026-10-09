using System.Collections.Generic;
using System.Xml.Serialization;

namespace VirtoCommerce.Punchout.Core.Cxml.Models;

public class CxmlItemDetail
{
    [XmlElement("UnitPrice")]
    public CxmlAmount UnitPrice { get; set; }

    [XmlElement("Description")]
    public CxmlDescription Description { get; set; }

    /// <summary>
    /// UN/CEFACT unit of measure code, e.g. 'EA'.
    /// </summary>
    [XmlElement("UnitOfMeasure")]
    public string UnitOfMeasure { get; set; }

    /// <summary>
    /// cXML requires at least one classification per item.
    /// </summary>
    [XmlElement("Classification")]
    public List<CxmlClassification> Classifications { get; set; }
}
