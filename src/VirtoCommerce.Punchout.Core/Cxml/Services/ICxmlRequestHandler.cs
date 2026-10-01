using System.Threading.Tasks;
using VirtoCommerce.Punchout.Core.Cxml.Models;

namespace VirtoCommerce.Punchout.Core.Cxml.Services;

/// <summary>
/// Handles one type of cXML request (PunchOutSetupRequest, OrderRequest, ...) then calls the business service
/// </summary>
public interface ICxmlRequestHandler
{
    bool CanHandle(CxmlDocument document);

    Task<CxmlDocument> HandleAsync(CxmlDocument document);
}
