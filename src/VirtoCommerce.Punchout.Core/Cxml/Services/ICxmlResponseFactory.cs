using VirtoCommerce.Punchout.Core.Cxml.Models;

namespace VirtoCommerce.Punchout.Core.Cxml.Services;

/// <summary>
/// Creates cXML documents with a new payload ID and the current timestamp.
/// </summary>
public interface ICxmlResponseFactory
{
    CxmlDocument CreateDocument();

    CxmlDocument CreateResponse(string code, string text, string message = null);
}
