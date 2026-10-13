using VirtoCommerce.Punchout.Core.Cxml.Models;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Cxml.Services;

public interface IPunchoutOrderMapper
{
    PunchoutOrderRequest MapRequest(CxmlDocument document);

    public CxmlDocument MapResponse(PunchoutOrderResult result);
}
