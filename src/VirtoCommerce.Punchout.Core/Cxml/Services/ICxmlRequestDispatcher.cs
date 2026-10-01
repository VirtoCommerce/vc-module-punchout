using System.Threading.Tasks;
using VirtoCommerce.Punchout.Core.Cxml.Models;

namespace VirtoCommerce.Punchout.Core.Cxml.Services;

public interface ICxmlRequestDispatcher
{
    /// <summary>
    /// Single entry point for all cXML requests, routed by the request type(PunchOutSetupRequest, OrderRequest, ...)
    /// </summary>
    Task<CxmlDocument> DispatchAsync(CxmlDocument document);
}
