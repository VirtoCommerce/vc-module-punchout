using VirtoCommerce.Punchout.Core.Cxml.Models;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Cxml.Services;

public interface IPunchoutSetupMapper
{
    PunchoutSetupContext MapRequest(CxmlDocument document);

    public CxmlDocument MapResponse(PunchoutSetupResult result);
}
