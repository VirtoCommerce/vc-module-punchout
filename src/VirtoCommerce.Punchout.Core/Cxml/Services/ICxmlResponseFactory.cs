using VirtoCommerce.Punchout.Core.Cxml.Models;

namespace VirtoCommerce.Punchout.Core.Cxml.Services;

/// <summary>
/// Creates a response document with a new payload ID, the current timestamp and the given status.
/// </summary>
public interface ICxmlResponseFactory
{
    CxmlDocument CreateResponse(string code, string text, string message = null);
}
